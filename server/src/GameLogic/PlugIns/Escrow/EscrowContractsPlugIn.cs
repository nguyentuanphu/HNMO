// <copyright file="EscrowContractsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// In-game escrow contract plug-in (ROADMAP 16.1 + 16.3, disabled by default):
/// atomically holds seller items/Zen/Jewel Bank jewels, swaps payment and goods inside one lock,
/// verifies second password &amp; account age &amp; post-escrow trade cooldowns, shows item origins
/// from <see cref="ActivityLogPlugIn"/>, and reports fee income to <see cref="EconomyDashboardPlugIn"/>.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Escrow Contracts", Description = "Atomic escrow contracts for items, Zen, coins, and Jewel Bank jewels (OFF by default).")]
[Guid("5A9D3F10-6C4E-4B18-9A72-1E3D5F7A9B02")]
public sealed class EscrowContractsPlugIn :
    IPeriodicTaskPlugIn,
    IPlayerStateChangedPlugIn,
    IEconomyExternalAssetProvider,
    ISupportCustomConfiguration<EscrowConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private const string GrantedBy = "HNMO Escrow";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private (EscrowConfiguration Config, EscrowEngine Engine)? _engine;
    private DateTime _lastExpireUtc = DateTime.MinValue;

    /// <inheritdoc />
    public EscrowConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="EscrowContractsPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c>.</summary>
    /// <param name="gameContext">Game context.</param>
    /// <returns>Active plug-in instance or <c>null</c>.</returns>
    public static EscrowContractsPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<EscrowContractsPlugIn>().FirstOrDefault();

    /// <summary>Checks whether <paramref name="item"/> is blocked from re-trading or re-escrowing due to post-escrow cooldown.</summary>
    /// <param name="player">Holding player.</param>
    /// <param name="item">Candidate item.</param>
    /// <returns><see langword="true"/> when refused with an EN/VI notice.</returns>
    public static async ValueTask<bool> RefuseCooldownItemAsync(Player player, Item item)
    {
        if (player.Account?.LoginName is not { Length: > 0 } login || item.Definition is null)
        {
            return false;
        }

        if (Get(player.GameContext) is not { } plugin || plugin.Configuration is not { Enabled: true })
        {
            return false;
        }

        var engine = plugin.GetEngine();
        if (engine.GetItemTradeLockUntil(login, item.Definition.Group, item.Definition.Number, item.Level, DateTime.UtcNow) is not { } untilUtc)
        {
            return false;
        }

        var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        var sydneyStr = EscrowEngine.ToSydneyText(untilUtc);
        var msg = isVi
            ? $"Vật phẩm mới nhận qua hợp đồng ký gửi đang bị khóa giao dịch tới {sydneyStr}."
            : $"This escrow-received item has a trade hold until {sydneyStr}.";
        await player.ShowBlueMessageAsync(msg).ConfigureAwait(false);
        return true;
    }

    /// <summary>Builds a rich item snapshot plus Activity Log origin lookup.</summary>
    /// <param name="item">Inventory item.</param>
    /// <param name="ownerLogin">Current owner account login.</param>
    /// <param name="activityLogFileName">Activity log state path.</param>
    /// <returns>Populated <see cref="EscrowItemSnapshot"/>.</returns>
    public static EscrowItemSnapshot BuildItemSnapshot(Item item, string ownerLogin, string? activityLogFileName)
    {
        var def = item.Definition!;
        var optionLevel = (byte)(item.ItemOptions.FirstOrDefault(o => o.ItemOption?.OptionType == ItemOptionTypes.Option)?.Level ?? 0);
        var hasLuck = item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck);
        var exMask = (byte)item.ItemOptions
            .Where(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent && o.ItemOption.Number is >= 1 and <= 8)
            .Aggregate(0, (mask, o) => mask | (1 << (o.ItemOption!.Number - 1)));
        var en = def.Name.GetTranslation(CultureInfo.GetCultureInfo("en")) ?? def.Name.ValueInNeutralLanguage;
        var vi = def.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? en;

        var origin = string.Empty;
        try
        {
            var idStr = HNMO.Persistence.ObjectExtensions.GetId(item).ToString();
            var matches = ActivityLogStore.Open(activityLogFileName).Query(new ActivityLogQuery
            {
                Account = ownerLogin,
                Item = en,
                MaxResults = 3,
            });
            var matched = matches.FirstOrDefault(m => m.ItemIds.Contains(idStr, StringComparison.OrdinalIgnoreCase))
                ?? matches.FirstOrDefault();
            if (matched is not null)
            {
                origin = $"{matched.Kind} ({EscrowEngine.ToSydneyText(matched.TimestampUtc)}): {matched.Details}";
            }
        }
        catch
        {
            origin = string.Empty;
        }

        return new EscrowItemSnapshot
        {
            Group = def.Group,
            Number = def.Number,
            Level = item.Level,
            Durability = (byte)Math.Clamp((int)item.Durability, 0, 255),
            Skill = item.HasSkill,
            Luck = hasLuck,
            OptionLevel = optionLevel,
            ExcellentOptions = exMask,
            NameEn = en,
            NameVi = vi,
            OriginSummary = string.IsNullOrWhiteSpace(origin) ? "No prior Activity Log record" : origin,
        };
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new EscrowConfiguration();

    /// <summary>Gets or builds the engine for current configuration.</summary>
    /// <returns>Active <see cref="EscrowEngine"/>.</returns>
    public EscrowEngine GetEngine()
    {
        var cfg = this.Configuration ??= new EscrowConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Config, cfg))
        {
            return cached.Engine;
        }

        var engine = new EscrowEngine(cfg, EscrowStore.Open(cfg.FileName));
        this._engine = (cfg, engine);
        return engine;
    }

    /// <summary>Gets the Direct P2P VND rule engine sharing the same atomic store.</summary>
    /// <returns>Active <see cref="EscrowDirectVndEngine"/>.</returns>
    public EscrowDirectVndEngine GetDirectEngine()
    {
        var core = this.GetEngine();
        return new EscrowDirectVndEngine(core.Configuration, core.Store);
    }

    /// <inheritdoc />
    public void ForceStart() => this._lastExpireUtc = DateTime.MinValue;

    /// <inheritdoc />
    public ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var cfg = this.Configuration ?? new EscrowConfiguration();
        if (!cfg.Enabled)
        {
            return ValueTask.CompletedTask;
        }

        var now = DateTime.UtcNow;
        if (now - this._lastExpireUtc < TimeSpan.FromMinutes(1))
        {
            return ValueTask.CompletedTask;
        }

        this._lastExpireUtc = now;
        var expired = this.GetEngine().ExpireDueContracts(now);
        var directChanged = this.GetDirectEngine().ProcessTimeouts(now);
        if (expired > 0 || directChanged > 0)
        {
            ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
            {
                Id = $"esc-exp-{Guid.NewGuid():N}",
                TimestampUtc = now,
                Kind = ActivityKind.Trade,
                Account = "SYSTEM",
                ItemSummary = $"Expired/updated {expired + directChanged} escrow contract(s)",
                Details = "Auto-expired contracts returned goods or auto-escalated unpaid confirmations.",
            });
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld && this.Configuration is { Enabled: true })
        {
            await this.ClaimOwedAsync(player, silentWhenEmpty: true).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<EconomyAccountTotals>> GetExternalAssetsAsync(CancellationToken cancellationToken)
    {
        if (this.Configuration is not { Enabled: true })
        {
            return ValueTask.FromResult<IReadOnlyList<EconomyAccountTotals>>([]);
        }

        var list = this.GetEngine().Store.Read(data =>
        {
            var byAcc = new Dictionary<string, EconomyAccountTotals>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in data.Contracts.Where(c => c.Status is EscrowContractStatus.Open or EscrowContractStatus.Frozen))
            {
                if (!byAcc.TryGetValue(c.SellerLogin, out var entry))
                {
                    entry = new EconomyAccountTotals { AccountLogin = c.SellerLogin };
                    byAcc[c.SellerLogin] = entry;
                }

                entry.Zen += c.EscrowedZen;
                entry.Jewels += c.EscrowedJewels.Sum(j => Math.Max(0, j.Quantity));
                entry.ValuableItems += c.Items.Count;
            }

            foreach (var o in data.OwedDeliveries)
            {
                if (!byAcc.TryGetValue(o.RecipientLogin, out var entry))
                {
                    entry = new EconomyAccountTotals { AccountLogin = o.RecipientLogin };
                    byAcc[o.RecipientLogin] = entry;
                }

                entry.Zen += o.Zen;
                entry.Jewels += o.Jewels.Sum(j => Math.Max(0, j.Quantity));
                entry.ValuableItems += o.Items.Count;
            }

            return (IReadOnlyList<EconomyAccountTotals>)byAcc.Values.ToList();
        });

        return ValueTask.FromResult(list);
    }

    /// <summary>Handles <c>HnmoEscrowCommand</c> packet from web client (<c>F5 4C</c>).</summary>
    /// <param name="player">Sender player.</param>
    /// <param name="action">Command action byte.</param>
    /// <param name="payloadJson">JSON parameters.</param>
    /// <param name="targetPrefill">Optional target character pre-fill for open request.</param>
    /// <returns>A task representing command handling.</returns>
    public async ValueTask HandlePacketAsync(Player player, byte action, string? payloadJson, string? targetPrefill = null)
    {
        if (this.Configuration is not { Enabled: true })
        {
            await this.SendStateAsync(player, requested: true, noticeEn: "Escrow contracts are disabled on this server.", noticeVi: "Hợp đồng ký gửi đang tắt trên máy chủ.", isError: true).ConfigureAwait(false);
            return;
        }

        JsonDocument? doc = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                doc = JsonDocument.Parse(payloadJson);
            }
        }
        catch
        {
            doc = null;
        }

        using (doc)
        {
            var root = doc?.RootElement;
            switch (action)
            {
                case 0: // RequestState
                    this.GetEngine().ExpireDueContracts(DateTime.UtcNow);
                    this.GetDirectEngine().ProcessTimeouts(DateTime.UtcNow);
                    await this.SendStateAsync(player, requested: true, prefillTarget: targetPrefill).ConfigureAwait(false);
                    break;
                case 1: // CreateContract
                    await this.CreateContractFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 2: // AcceptContract
                    await this.AcceptContractFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 3: // CancelContract
                    await this.CancelContractFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 4: // ClaimOwed
                    await this.ClaimOwedAsync(player, silentWhenEmpty: false).ConfigureAwait(false);
                    break;
                case 5: // CreateDirectVnd
                    await this.CreateDirectVndFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 6: // ReserveDirectVnd
                    await this.ReserveDirectVndFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 7: // ExtendDirectVnd
                    await this.ExtendDirectVndFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 8: // BuyerMarkPaid
                    await this.BuyerMarkPaidFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 9: // SellerConfirmPaid
                    await this.SellerConfirmPaidFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                case 10: // OpenDispute
                    await this.OpenDisputeFromPlayerAsync(player, root).ConfigureAwait(false);
                    break;
                default:
                    await this.SendStateAsync(player, requested: true).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>Sends serialized <see cref="EscrowStoreData"/> projection to <paramref name="player"/> (<c>F5 4D</c>).</summary>
    /// <param name="player">Recipient player.</param>
    /// <param name="requested">Whether triggered by explicit request.</param>
    /// <param name="noticeEn">Optional EN status message.</param>
    /// <param name="noticeVi">Optional VI status message.</param>
    /// <param name="isError">Whether status is an error.</param>
    /// <param name="prefillTarget">Optional counterparty character name.</param>
    /// <returns>A task representing the send operation.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        string? noticeEn = null,
        string? noticeVi = null,
        bool isError = false,
        string? prefillTarget = null)
    {
        var login = player.Account?.LoginName ?? string.Empty;
        var charName = player.SelectedCharacter?.Name ?? string.Empty;
        var engine = this.GetEngine();
        var cfg = engine.Configuration;
        var enabled = cfg.Enabled;

        var bagItems = (player.Inventory?.Items ?? [])
            .Where(i => i.Definition is not null
                && i.ItemSlot >= InventoryConstants.EquippableSlotsCount
                && i.ItemSlot < InventoryConstants.FirstStoreItemSlotIndex)
            .OrderBy(i => i.ItemSlot)
            .Select(i => new
            {
                slot = i.ItemSlot,
                nameEn = i.Definition!.Name.GetTranslation(CultureInfo.GetCultureInfo("en")) ?? i.Definition.Name.ValueInNeutralLanguage,
                nameVi = i.Definition.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? i.Definition.Name.ValueInNeutralLanguage,
                level = i.Level,
                durability = (int)Math.Ceiling(i.Durability),
            })
            .ToList();

        var jewelBalances = JewelBankStore.Open(cfg.JewelBankFileName).Read(d =>
            d.Accounts.TryGetValue(login, out var m) ? m : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        var snapshot = engine.Store.Read(data => new
        {
            enabled,
            accountLogin = login,
            characterName = charName,
            prefillTarget = prefillTarget ?? string.Empty,
            requiresPin = cfg.RequireSecondPassword && !string.IsNullOrWhiteSpace(player.Account?.VaultPassword),
            feePercent = cfg.FeePercent,
            minFee = cfg.MinFee,
            maxFee = cfg.MaxFee,
            feePayer = cfg.FeePayer.ToString(),
            allowedCurrencies = cfg.AllowedCurrencies.Select(c => c.ToString()).ToList(),
            highValueCoins = cfg.HighValueThresholdCoins,
            highValueZen = cfg.HighValueThresholdZen,
            highValueJewels = cfg.HighValueThresholdJewels,
            cooldownHours = cfg.PostEscrowTradeCooldownHours,
            directVndEnabled = EscrowDirectVndEngine.IsCompliantAndEnabled(cfg),
            directVndPaymentWindowMinutes = cfg.DirectVndPaymentWindowMinutes,
            directVndExtendMinutes = cfg.DirectVndExtendMinutes,
            directVndServiceFeeCurrency = cfg.DirectVndServiceFeeCurrency.ToString(),
            directVndServiceFeeCoins = cfg.DirectVndServiceFeeCoins,
            directVndServiceFeePercent = cfg.DirectVndServiceFeePercent,
            directVndFeePayer = cfg.DirectVndFeePayer.ToString(),
            directVndMaxSellerBondCoins = cfg.DirectVndMaxSellerBondCoins,
            directVndMaxReceiptBytes = cfg.DirectVndMaxReceiptBytes,
            reputation = data.Reputations.TryGetValue(login, out var rep) ? rep : new EscrowReputationEntry { LoginName = login },
            bagItems,
            jewelBalances,
            openContracts = data.Contracts
                .Where(c => c.Status is EscrowContractStatus.Open or EscrowContractStatus.Frozen or EscrowContractStatus.ReservedForPayment or EscrowContractStatus.BuyerMarkedPaid or EscrowContractStatus.Disputed)
                .Where(c => string.IsNullOrWhiteSpace(c.TargetBuyer)
                    || string.Equals(c.SellerLogin, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.AcceptedByLogin, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.TargetBuyer, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.TargetBuyer, charName, StringComparison.OrdinalIgnoreCase))
                .Where(c => c.Status is EscrowContractStatus.Open or EscrowContractStatus.Frozen
                    || string.Equals(c.SellerLogin, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.AcceptedByLogin, login, StringComparison.OrdinalIgnoreCase))
                .Take(100)
                .Select(c => EscrowDirectVndEngine.ProjectForViewer(c, login))
                .ToList(),
            historyContracts = data.Contracts
                .Where(c => c.Status is not EscrowContractStatus.Open and not EscrowContractStatus.Frozen and not EscrowContractStatus.ReservedForPayment and not EscrowContractStatus.BuyerMarkedPaid and not EscrowContractStatus.Disputed)
                .Where(c => string.Equals(c.SellerLogin, login, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.AcceptedByLogin, login, StringComparison.OrdinalIgnoreCase))
                .Take(50)
                .Select(c => EscrowDirectVndEngine.ProjectForViewer(c, login))
                .ToList(),
            owedDeliveries = data.OwedDeliveries
                .Where(o => string.Equals(o.RecipientLogin, login, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            noticeEn,
            noticeVi,
            isError,
        });

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await player.InvokeViewPlugInAsync<IHnmoEscrowViewPlugIn>(p => p.ShowEscrowAsync(json, requested)).ConfigureAwait(false);
    }

    private static bool TryDebitAccountCoins(Player player, CashShopCoinType coinType, int amount)
    {
        if (player.Account is not { } account || amount <= 0)
        {
            return amount == 0;
        }

        var available = account.GetCashShopCoins(coinType);
        if (available < amount)
        {
            return false;
        }

        switch (coinType)
        {
            case CashShopCoinType.WCoinC:
                account.WCoinC -= amount;
                break;
            case CashShopCoinType.WCoinP:
                account.WCoinP -= amount;
                break;
            case CashShopCoinType.GoblinPoints:
                account.GoblinPoints -= amount;
                break;
            default:
                return false;
        }

        return true;
    }

    private static void RefundAccountCoins(Player player, CashShopCoinType coinType, int amount)
    {
        if (player.Account is not { } account || amount <= 0)
        {
            return;
        }

        switch (coinType)
        {
            case CashShopCoinType.WCoinC:
                account.WCoinC += amount;
                break;
            case CashShopCoinType.WCoinP:
                account.WCoinP += amount;
                break;
            case CashShopCoinType.GoblinPoints:
                account.GoblinPoints += amount;
                break;
        }
    }

    private static CashShopCoinType MapCoinType(EscrowCurrency currency) => currency switch
    {
        EscrowCurrency.WCoinP => CashShopCoinType.WCoinP,
        EscrowCurrency.GoblinPoints => CashShopCoinType.GoblinPoints,
        _ => CashShopCoinType.WCoinC,
    };

    private static void RefundJewelsToBank(JewelBankStore store, string login, IReadOnlyList<EscrowJewelLine> jewels)
    {
        store.Mutate(d =>
        {
            if (!d.Accounts.TryGetValue(login, out var bal))
            {
                bal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                d.Accounts[login] = bal;
            }

            foreach (var j in jewels)
            {
                bal.TryGetValue(j.JewelKey, out var have);
                bal[j.JewelKey] = have + j.Quantity;
            }

            return (true, true);
        });
    }

    private static (string En, string Vi) TranslateError(string err, EscrowConfiguration cfg) => err switch
    {
        "BannedItem" => ("That item is banned from escrow.", "Vật phẩm nằm trong danh sách cấm ký gửi."),
        "NeedsHighValueConfirmation" => ("High-value transaction — tick the confirmation checkbox to proceed.", "Giao dịch giá trị lớn — vui lòng đánh dấu xác nhận thêm."),
        "TooManyOpenContracts" => ($"Maximum {cfg.MaxOpenContractsPerAccount} open contracts per account.", $"Đạt tối đa {cfg.MaxOpenContractsPerAccount} hợp đồng mở mỗi tài khoản."),
        "SelfTargetNotAllowed" => ("Cannot target your own account.", "Không thể tạo hợp đồng cho chính tài khoản mình."),
        "Frozen" => ("This contract is frozen by an administrator.", "Hợp đồng đang bị quản trị viên tạm khóa."),
        "Expired" => ("Contract expired; goods returned to seller.", "Hợp đồng đã hết hạn; hàng đã trả lại người bán."),
        "AlreadyClosed" => ("Contract has already been accepted or closed.", "Hợp đồng đã có người nhận hoặc đã đóng."),
        "WrongBuyer" => ("This private contract is reserved for a different buyer.", "Hợp đồng riêng này chỉ dành cho người mua được chỉ định."),
        "OwnContract" => ("You cannot accept your own contract.", "Bạn không thể tự nhận hợp đồng của chính mình."),
        "DirectVndDisabledOrUnacknowledged" => ("Direct P2P VND contracts are disabled or missing admin legal acknowledgement.", "Hợp đồng VND trực tiếp đang tắt hoặc chưa được xác nhận pháp lý."),
        "InvalidVndPrice" => ("Direct VND price must be at least 1,000 VND.", "Giá VND trực tiếp phải tối thiểu 1.000 VNĐ."),
        "MissingBankDetails" => ("Seller bank BIN, account number, and holder name are required.", "Cần nhập đủ mã BIN ngân hàng, số tài khoản và tên chủ TK."),
        "InvalidSellerBond" => ($"Seller security bond must be between 0 and {cfg.DirectVndMaxSellerBondCoins}.", $"Tiền cọc người bán phải từ 0 đến {cfg.DirectVndMaxSellerBondCoins}."),
        "NotReserved" => ("Contract is not currently reserved for payment.", "Hợp đồng hiện không ở trạng thái chờ chuyển khoản."),
        "AlreadyExtended" => ("Payment window can only be extended once.", "Thời gian thanh toán chỉ được gia hạn 1 lần."),
        "PaymentWindowExpired" => ("Payment window has already expired.", "Thời gian chuyển khoản đã hết hạn."),
        "InvalidReceiptImage" => ("Receipt must be a valid PNG, JPG, or WEBP image within size limit.", "Ảnh biên lai phải là PNG/JPG/WEBP hợp lệ trong giới hạn dung lượng."),
        "NotSeller" => ("Only the seller can confirm receipt of funds.", "Chỉ người bán mới có quyền xác nhận đã nhận tiền."),
        "InvalidState" => ("Operation is not valid in the contract's current status.", "Thao tác không hợp lệ với trạng thái hợp đồng hiện tại."),
        "NotParticipant" => ("Only the seller or accepted buyer may dispute this contract.", "Chỉ người bán hoặc người mua mới có thể mở tranh chấp."),
        _ => ($"Escrow action failed ({err}).", $"Thao tác ký gửi thất bại ({err})."),
    };

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? string.Empty : string.Empty;

    private static long ReadLong(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.TryGetInt64(out var val) ? val : 0;

    private static bool ReadBool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && (prop.ValueKind == JsonValueKind.True || (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var b) && b));

    private ValueTask CreateContractFromPlayerAsync(Player player, JsonElement? root) =>
        this.CreateContractCoreAsync(player, root, directVnd: false);

    private ValueTask CreateDirectVndFromPlayerAsync(Player player, JsonElement? root) =>
        this.CreateContractCoreAsync(player, root, directVnd: true);

    private async ValueTask CreateContractCoreAsync(Player player, JsonElement? root, bool directVnd)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login || player.SelectedCharacter?.Name is not { Length: > 0 } charName)
        {
            return;
        }

        var engine = this.GetEngine();
        var cfg = engine.Configuration;
        var now = DateTime.UtcNow;

        if (directVnd && !EscrowDirectVndEngine.IsCompliantAndEnabled(cfg))
        {
            await this.SendStateAsync(player, true, "Direct P2P VND mode is disabled.", "Hợp đồng VND trực tiếp hiện đang tắt.", true).ConfigureAwait(false);
            return;
        }

        if (!EscrowEngine.MeetsAccountAgeRequirement(cfg, player.Account.RegistrationDate, now))
        {
            await this.SendStateAsync(player, true, $"Accounts must be at least {cfg.MinAccountAgeDays} day(s) old to use escrow.", $"Tài khoản phải đủ {cfg.MinAccountAgeDays} ngày tuổi mới được ký gửi.", true).ConfigureAwait(false);
            return;
        }

        var pin = ReadString(json, "pin");
        if (!await this.VerifySecondPasswordAsync(player, cfg, pin).ConfigureAwait(false))
        {
            return;
        }

        var slotList = new List<byte>();
        if (json.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in slotsEl.EnumerateArray())
            {
                if (s.TryGetInt32(out var slotNum) && slotNum is >= 0 and <= 255)
                {
                    slotList.Add((byte)slotNum);
                }
            }
        }
        else if (json.TryGetProperty("slot", out var singleSlot) && singleSlot.TryGetInt32(out var singleNum) && singleNum is > 0 and <= 255)
        {
            slotList.Add((byte)singleNum);
        }

        var escrowedZen = Math.Max(0, ReadLong(json, "escrowedZen"));
        var targetBuyer = ReadString(json, "targetBuyer");
        var priceCurrency = Enum.TryParse<EscrowCurrency>(ReadString(json, "priceCurrency"), true, out var pc) ? pc : EscrowCurrency.WCoinC;
        var priceAmount = Math.Max(0, ReadLong(json, "priceAmount"));
        var deadlineHours = (int)Math.Max(0, ReadLong(json, "deadlineHours"));
        var confirmHighValue = ReadBool(json, "confirmHighValue");

        var jewelsList = new List<EscrowJewelLine>();
        var jKey = ReadString(json, "escrowedJewelKey");
        var jQty = (int)Math.Max(0, ReadLong(json, "escrowedJewelQty"));
        if (!string.IsNullOrWhiteSpace(jKey) && jQty > 0)
        {
            jewelsList.Add(new EscrowJewelLine { JewelKey = jKey, Quantity = jQty });
        }

        // Validate & snapshot bag items cleanly BEFORE removing them from inventory.
        var pickedItems = new List<(Item Entity, EscrowItemSnapshot Snapshot)>();
        var safety = AccountSafetyPlugIn.Get(player.GameContext);
        foreach (var slot in slotList.Distinct().Take(5))
        {
            var item = player.Inventory?.GetItem(slot);
            if (item?.Definition is null || slot < InventoryConstants.EquippableSlotsCount || slot >= InventoryConstants.FirstStoreItemSlotIndex)
            {
                await this.SendStateAsync(player, true, $"Invalid bag slot {slot}.", $"Ô túi đồ {slot} không hợp lệ.", true).ConfigureAwait(false);
                return;
            }

            if (safety is not null && await safety.RefuseLockedItemAsync(player, item).ConfigureAwait(false))
            {
                return;
            }

            if (await RefuseCooldownItemAsync(player, item).ConfigureAwait(false))
            {
                return;
            }

            pickedItems.Add((item, BuildItemSnapshot(item, login, cfg.ActivityLogFileName)));
        }

        if (escrowedZen > 0 && player.Money < escrowedZen)
        {
            await this.SendStateAsync(player, true, "Insufficient Zen in inventory.", "Không đủ Zen trong túi đồ.", true).ConfigureAwait(false);
            return;
        }

        if (pickedItems.Count == 0 && escrowedZen <= 0 && jewelsList.Count == 0)
        {
            await this.SendStateAsync(player, true, "Select at least one item, Zen, or Jewel Bank balance to place in escrow.", "Hãy chọn ít nhất 1 vật phẩm, Zen hoặc Ngọc để ký gửi.", true).ConfigureAwait(false);
            return;
        }

        var sellerCoinDebit = 0;
        var sellerBond = 0L;
        var coinType = MapCoinType(cfg.DirectVndServiceFeeCurrency);
        if (directVnd)
        {
            sellerBond = Math.Max(0, ReadLong(json, "sellerBondCoins"));
            var fee = EscrowDirectVndEngine.ComputeServiceFeeCoins(cfg, priceAmount);
            sellerCoinDebit = (int)Math.Min(int.MaxValue, sellerBond + fee.SellerFeePaid);
            if (!TryDebitAccountCoins(player, coinType, sellerCoinDebit))
            {
                await this.SendStateAsync(player, true, $"Insufficient {cfg.DirectVndServiceFeeCurrency} for bond/service fee ({sellerCoinDebit} needed).", $"Không đủ {cfg.DirectVndServiceFeeCurrency} cho tiền cọc/phí dịch vụ (cần {sellerCoinDebit}).", true).ConfigureAwait(false);
                return;
            }
        }

        var jewelStore = JewelBankStore.Open(cfg.JewelBankFileName);
        if (jewelsList.Count > 0)
        {
            var okJewels = jewelStore.Mutate(d =>
            {
                if (!d.Accounts.TryGetValue(login, out var bal))
                {
                    return (false, false);
                }

                foreach (var j in jewelsList)
                {
                    if (!bal.TryGetValue(j.JewelKey, out var have) || have < j.Quantity)
                    {
                        return (false, false);
                    }
                }

                foreach (var j in jewelsList)
                {
                    bal[j.JewelKey] -= j.Quantity;
                }

                return (true, true);
            });
            if (!okJewels)
            {
                RefundAccountCoins(player, coinType, sellerCoinDebit);
                await this.SendStateAsync(player, true, "Not enough jewels in your Jewel Bank.", "Không đủ ngọc trong Ngân Hàng Ngọc.", true).ConfigureAwait(false);
                return;
            }
        }

        var snapshots = pickedItems.Select(p => p.Snapshot).ToList();
        var (err, created) = directVnd
            ? this.GetDirectEngine().CreateDirectVndContract(
                login,
                charName,
                targetBuyer,
                snapshots,
                escrowedZen,
                jewelsList,
                priceAmount,
                ReadString(json, "bankBin"),
                ReadString(json, "bankName"),
                ReadString(json, "bankAccountNumber"),
                ReadString(json, "bankAccountName"),
                sellerBond,
                ReadString(json, "webhookSecret"),
                deadlineHours,
                confirmHighValue,
                now)
            : engine.CreateContract(
                login,
                charName,
                targetBuyer,
                snapshots,
                escrowedZen,
                jewelsList,
                priceCurrency,
                priceAmount,
                deadlineHours,
                confirmHighValue,
                now);

        if (err != "Ok" || created is null)
        {
            RefundAccountCoins(player, coinType, sellerCoinDebit);
            if (jewelsList.Count > 0)
            {
                RefundJewelsToBank(jewelStore, login, jewelsList);
            }

            var (en, vi) = TranslateError(err, cfg);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        if (escrowedZen > 0)
        {
            player.TryRemoveMoney((int)Math.Min(int.MaxValue, escrowedZen));
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        foreach (var (entity, _) in pickedItems)
        {
            var removedSlot = entity.ItemSlot;
            await player.Inventory!.RemoveItemAsync(entity).ConfigureAwait(false);
            await player.PersistenceContext.DeleteAsync(entity).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(removedSlot)).ConfigureAwait(false);
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-new-{Guid.NewGuid():N}",
            TimestampUtc = now,
            Kind = ActivityKind.Trade,
            Account = login,
            Character = charName,
            PartnerAccount = targetBuyer,
            ItemSummary = string.Join(", ", pickedItems.Select(x => x.Snapshot.NameEn)),
            ZenDelta = -escrowedZen,
            Details = directVnd
                ? $"Created Direct VND escrow {created.ContractId}: price {priceAmount:N0} VND (ref {created.DirectVnd?.PaymentReference})"
                : $"Created escrow {created.ContractId}: price {priceAmount} {priceCurrency}",
        });

        await this.SendStateAsync(player, true, $"Created escrow contract {created.ContractId}.", $"Đã tạo hợp đồng ký gửi {created.ContractId}.").ConfigureAwait(false);
    }

    private async ValueTask ReserveDirectVndFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login || player.SelectedCharacter?.Name is not { Length: > 0 } charName)
        {
            return;
        }

        var direct = this.GetDirectEngine();
        var cfg = this.GetEngine().Configuration;
        var now = DateTime.UtcNow;

        if (!EscrowEngine.MeetsAccountAgeRequirement(cfg, player.Account.RegistrationDate, now))
        {
            await this.SendStateAsync(player, true, $"Accounts must be at least {cfg.MinAccountAgeDays} day(s) old to use escrow.", $"Tài khoản phải đủ {cfg.MinAccountAgeDays} ngày tuổi mới được ký gửi.", true).ConfigureAwait(false);
            return;
        }

        if (!await this.VerifySecondPasswordAsync(player, cfg, ReadString(json, "pin")).ConfigureAwait(false))
        {
            return;
        }

        var contractId = ReadString(json, "contractId");
        var coinType = MapCoinType(cfg.DirectVndServiceFeeCurrency);
        var (err, reserved) = direct.ReserveForPayment(
            login,
            charName,
            contractId,
            now,
            fee => TryDebitAccountCoins(player, coinType, (int)Math.Min(int.MaxValue, fee)));

        if (err != "Ok" || reserved is null)
        {
            var (en, vi) = err == "PaymentFailed"
                ? ($"Insufficient {cfg.DirectVndServiceFeeCurrency} for service fee.", $"Không đủ {cfg.DirectVndServiceFeeCurrency} trả phí dịch vụ.")
                : TranslateError(err, cfg);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-dvres-{Guid.NewGuid():N}",
            TimestampUtc = now,
            Kind = ActivityKind.Trade,
            Account = login,
            Character = charName,
            PartnerAccount = reserved.SellerLogin,
            Details = $"Reserved Direct VND {reserved.ContractId} ({reserved.DirectVnd?.PaymentReference}) until {reserved.DirectVnd?.PaymentDeadlineSydney}",
        });

        await this.SendStateAsync(player, true, $"Reserved {reserved.ContractId}. Transfer {reserved.PriceAmount:N0} VND with ref {reserved.DirectVnd?.PaymentReference}.", $"Đã giữ hợp đồng {reserved.ContractId}. Chuyển {reserved.PriceAmount:N0} VNĐ kèm nội dung {reserved.DirectVnd?.PaymentReference}.").ConfigureAwait(false);
    }

    private async ValueTask ExtendDirectVndFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var (err, updated) = this.GetDirectEngine().ExtendPaymentWindowOnce(login, ReadString(json, "contractId"), DateTime.UtcNow);
        if (err != "Ok" || updated is null)
        {
            var (en, vi) = TranslateError(err, this.GetEngine().Configuration);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        await this.SendStateAsync(player, true, $"Extended deadline to {updated.DirectVnd?.PaymentDeadlineSydney}.", $"Đã gia hạn thanh toán tới {updated.DirectVnd?.PaymentDeadlineSydney}.").ConfigureAwait(false);
    }

    private async ValueTask BuyerMarkPaidFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var (err, updated) = this.GetDirectEngine().MarkBuyerPaid(
            login,
            ReadString(json, "contractId"),
            ReadString(json, "receiptMime"),
            ReadString(json, "receiptBase64"),
            ReadString(json, "note"),
            DateTime.UtcNow);
        if (err != "Ok" || updated is null)
        {
            var (en, vi) = TranslateError(err, this.GetEngine().Configuration);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        await this.SendStateAsync(player, true, $"Marked {updated.ContractId} paid. Waiting for seller confirmation.", $"Đã báo chuyển khoản cho {updated.ContractId}. Đang chờ người bán xác nhận.").ConfigureAwait(false);
    }

    private async ValueTask SellerConfirmPaidFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var (err, completed) = this.GetDirectEngine().SellerConfirmMoneyReceived(login, ReadString(json, "contractId"), now);
        if (err != "Ok" || completed is null)
        {
            var (en, vi) = TranslateError(err, this.GetEngine().Configuration);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-dvok-{Guid.NewGuid():N}",
            TimestampUtc = now,
            Kind = ActivityKind.Trade,
            Account = completed.SellerLogin,
            Character = completed.SellerCharacter,
            PartnerAccount = completed.AcceptedByLogin,
            Details = $"Seller confirmed VND payment on {completed.ContractId} ({completed.PriceAmount:N0} VND).",
        });

        await this.ClaimOwedAsync(player, silentWhenEmpty: true).ConfigureAwait(false);
        await this.SendStateAsync(player, true, $"Confirmed payment on {completed.ContractId}; goods delivered to buyer.", $"Đã xác nhận nhận tiền {completed.ContractId}; đã giao hàng cho người mua.").ConfigureAwait(false);
    }

    private async ValueTask OpenDisputeFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var (err, disputed) = this.GetDirectEngine().OpenDispute(login, ReadString(json, "contractId"), ReadString(json, "reason"), now);
        if (err != "Ok" || disputed is null)
        {
            var (en, vi) = TranslateError(err, this.GetEngine().Configuration);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-disp-{Guid.NewGuid():N}",
            TimestampUtc = now,
            Kind = ActivityKind.Trade,
            Account = login,
            PartnerAccount = string.Equals(login, disputed.SellerLogin, StringComparison.OrdinalIgnoreCase) ? disputed.AcceptedByLogin : disputed.SellerLogin,
            Details = $"Opened dispute on {disputed.ContractId}: {disputed.DirectVnd?.DisputeReason}",
        });

        await this.SendStateAsync(player, true, $"Dispute opened on {disputed.ContractId}. Admin will review.", $"Đã mở tranh chấp cho {disputed.ContractId}. QTV sẽ xem xét.").ConfigureAwait(false);
    }

    private async ValueTask AcceptContractFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login || player.SelectedCharacter?.Name is not { Length: > 0 } charName)
        {
            return;
        }

        var contractId = ReadString(json, "contractId");
        var pin = ReadString(json, "pin");
        var confirmHighValue = ReadBool(json, "confirmHighValue");
        var engine = this.GetEngine();
        var cfg = engine.Configuration;
        var now = DateTime.UtcNow;

        if (!EscrowEngine.MeetsAccountAgeRequirement(cfg, player.Account.RegistrationDate, now))
        {
            await this.SendStateAsync(player, true, $"Accounts must be at least {cfg.MinAccountAgeDays} day(s) old to use escrow.", $"Tài khoản phải đủ {cfg.MinAccountAgeDays} ngày tuổi mới được ký gửi.", true).ConfigureAwait(false);
            return;
        }

        if (!await this.VerifySecondPasswordAsync(player, cfg, pin).ConfigureAwait(false))
        {
            return;
        }

        EscrowContract? completed = null;
        string err = "InvalidRequest";

        // Peek contract currency first so payment check can verify/debit atomically before finalizing.
        var targetContract = engine.Store.Read(d => d.Contracts.FirstOrDefault(c => string.Equals(c.ContractId, contractId, StringComparison.OrdinalIgnoreCase)));
        if (targetContract is null)
        {
            await this.SendStateAsync(player, true, "Contract not found.", "Không tìm thấy hợp đồng.", true).ConfigureAwait(false);
            return;
        }

        var jewelStore = JewelBankStore.Open(cfg.JewelBankFileName);
        if (targetContract.PriceCurrency == EscrowCurrency.Zen)
        {
            (err, completed) = engine.AcceptContractAtomic(
                login,
                charName,
                contractId,
                confirmHighValue,
                now,
                c =>
                {
                    if (player.Money < c.BuyerTotalDebit)
                    {
                        return false;
                    }

                    return player.TryRemoveMoney((int)Math.Min(int.MaxValue, c.BuyerTotalDebit));
                });
            if (err == "Ok")
            {
                await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            }
        }
        else if (targetContract.PriceCurrency is >= EscrowCurrency.JewelBless and <= EscrowCurrency.JewelLife)
        {
            var jk = targetContract.PriceCurrency.ToString();
            (err, completed) = engine.AcceptContractAtomic(
                login,
                charName,
                contractId,
                confirmHighValue,
                now,
                c => jewelStore.Mutate(d =>
                {
                    if (!d.Accounts.TryGetValue(login, out var bal) || !bal.TryGetValue(jk, out var have) || have < c.BuyerTotalDebit)
                    {
                        return (false, false);
                    }

                    bal[jk] -= (int)c.BuyerTotalDebit;
                    return (true, true);
                }));
        }
        else
        {
            // Coin payment (WCoinC / WCoinP / GoblinPoints)
            var coinType = MapCoinType(targetContract.PriceCurrency);
            (err, completed) = engine.AcceptContractAtomic(
                login,
                charName,
                contractId,
                confirmHighValue,
                now,
                c => TryDebitAccountCoins(player, coinType, (int)Math.Min(int.MaxValue, c.BuyerTotalDebit)));
        }

        if (err != "Ok" || completed is null)
        {
            var (en, vi) = err == "PaymentFailed"
                ? ($"Insufficient balance ({targetContract.BuyerTotalDebit:N0} {targetContract.PriceCurrency} needed).", $"Không đủ số dư (cần {targetContract.BuyerTotalDebit:N0} {targetContract.PriceCurrency}).")
                : TranslateError(err, cfg);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        // Payout seller coins immediately via MissionCoinGranter (works online/offline idempotently).
        if (completed.PriceCurrency is EscrowCurrency.WCoinC or EscrowCurrency.WCoinP or EscrowCurrency.GoblinPoints
            && completed.NetSellerProceeds > 0)
        {
            var coinType = MapCoinType(completed.PriceCurrency);
            await MissionCoinGranter.GrantAsync(
                player.GameContext,
                new CoinGrant(completed.SellerLogin, coinType, (int)Math.Min(int.MaxValue, completed.NetSellerProceeds), $"Escrow {completed.ContractId} payout", $"escrow:{completed.ContractId}"),
                GrantedBy).ConfigureAwait(false);
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-ok-{Guid.NewGuid():N}",
            TimestampUtc = now,
            Kind = ActivityKind.Trade,
            Account = login,
            Character = charName,
            PartnerAccount = completed.SellerLogin,
            PartnerCharacter = completed.SellerCharacter,
            ItemSummary = string.Join(", ", completed.Items.Select(x => x.NameEn)),
            Details = $"Completed escrow {completed.ContractId}: paid {completed.BuyerTotalDebit} {completed.PriceCurrency} (fee {completed.FeeAmount})",
        });

        await this.ClaimOwedAsync(player, silentWhenEmpty: true).ConfigureAwait(false);
        await this.SendStateAsync(player, true, $"Contract {completed.ContractId} completed!", $"Đã hoàn tất hợp đồng {completed.ContractId}!").ConfigureAwait(false);
    }

    private async ValueTask CancelContractFromPlayerAsync(Player player, JsonElement? root)
    {
        if (root is not { } json || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var contractId = ReadString(json, "contractId");
        var engine = this.GetEngine();
        var (err, cancelled) = engine.CancelContract(login, contractId, isAdmin: false, "Seller cancelled", DateTime.UtcNow);
        if (err != "Ok" || cancelled is null)
        {
            var (en, vi) = TranslateError(err, engine.Configuration);
            await this.SendStateAsync(player, true, en, vi, true).ConfigureAwait(false);
            return;
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-can-{Guid.NewGuid():N}",
            TimestampUtc = DateTime.UtcNow,
            Kind = ActivityKind.Trade,
            Account = login,
            Character = player.SelectedCharacter?.Name ?? string.Empty,
            Details = $"Cancelled escrow {cancelled.ContractId}; returned goods to owed rewards.",
        });

        await this.ClaimOwedAsync(player, silentWhenEmpty: true).ConfigureAwait(false);
        await this.SendStateAsync(player, true, $"Cancelled {cancelled.ContractId}; escrowed goods returned.", $"Đã hủy hợp đồng {cancelled.ContractId}; vật phẩm đã hoàn về.").ConfigureAwait(false);
    }

    private async ValueTask ClaimOwedAsync(Player player, bool silentWhenEmpty)
    {
        if (player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var engine = this.GetEngine();
        var deliveries = engine.PeekOwedDeliveries(login);
        if (deliveries.Count == 0)
        {
            if (!silentWhenEmpty)
            {
                await this.SendStateAsync(player, true, "No pending escrow deliveries.", "Không có vật phẩm ký gửi nào chờ nhận.").ConfigureAwait(false);
            }

            return;
        }

        var cfg = engine.Configuration;
        var jewelStore = JewelBankStore.Open(cfg.JewelBankFileName);
        var deliveredCount = 0;

        foreach (var delivery in deliveries)
        {
            // Jewels -> Jewel Bank directly!
            if (delivery.Jewels.Count > 0)
            {
                RefundJewelsToBank(jewelStore, login, delivery.Jewels);
                delivery.Jewels = [];
            }

            // Zen + Items -> Bag via GiftCodeRewards all-or-nothing!
            var giftReward = new GiftCodes.GiftCodeOwedReward
            {
                Reference = delivery.DeliveryId,
                CodeId = delivery.ContractId,
                Code = delivery.ReasonEn,
                Zen = (int)Math.Min(int.MaxValue, Math.Max(0L, delivery.Zen)),
                Items = delivery.Items.Select(snap => new GiftCodes.GiftCodeItem
                {
                    Group = snap.Group,
                    Number = snap.Number,
                    Level = snap.Level,
                    Durability = snap.Durability,
                    Skill = snap.Skill,
                    Luck = snap.Luck,
                    OptionLevel = snap.OptionLevel,
                    ExcellentOptions = snap.ExcellentOptions,
                    Count = 1,
                }).ToList(),
            };

            if ((giftReward.Items.Count > 0 || giftReward.Zen > 0)
                && !await GiftCodes.GiftCodeRewards.GiveGoodsAsync(player, giftReward).ConfigureAwait(false))
            {
                continue;
            }

            if (delivery.Coins > 0)
            {
                await MissionCoinGranter.GrantAsync(
                    player.GameContext,
                    new CoinGrant(login, MapCoinType(delivery.CoinCurrency), (int)Math.Min(int.MaxValue, delivery.Coins), delivery.ReasonEn, $"escrow-owed:{delivery.DeliveryId}"),
                    GrantedBy).ConfigureAwait(false);
            }

            engine.RemoveOwedDelivery(delivery.DeliveryId);
            deliveredCount++;
        }

        if (!silentWhenEmpty || deliveredCount > 0)
        {
            await this.SendStateAsync(
                player,
                !silentWhenEmpty,
                deliveredCount > 0 ? $"Delivered {deliveredCount} owed escrow package(s)." : "Bag or Zen full — free space and click Claim again.",
                deliveredCount > 0 ? $"Đã nhận {deliveredCount} gói hợp đồng ký gửi." : "Túi đồ hoặc Zen đầy — dọn chỗ trống và nhấn Nhận lại.").ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> VerifySecondPasswordAsync(Player player, EscrowConfiguration cfg, string pin)
    {
        if (!cfg.RequireSecondPassword || string.IsNullOrWhiteSpace(player.Account?.VaultPassword))
        {
            return true;
        }

        if (AccountSafetyPlugIn.Get(player.GameContext) is { } safety)
        {
            if (safety.IsEntered(player))
            {
                return true;
            }

            var check = await safety.EnterPinAsync(player, AccountSafetyEngine.NormalizePin(pin), tell: true).ConfigureAwait(false);
            if (check.Result is PinCheckResult.Ok or PinCheckResult.NoPin)
            {
                return true;
            }

            await this.SendStateAsync(player, true, "Valid 4-digit second password (PIN) required.", "Cần nhập đúng mật khẩu cấp 2 (4 chữ số).", true).ConfigureAwait(false);
            return false;
        }

        if (AccountSafetyEngine.PinMatches(player.Account.VaultPassword, AccountSafetyEngine.NormalizePin(pin)))
        {
            return true;
        }

        await this.SendStateAsync(player, true, "Invalid second password (PIN).", "Sai mật khẩu cấp 2 (PIN).", true).ConfigureAwait(false);
        return false;
    }
}
