// <copyright file="AccountSafetyPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.Views;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// HNMO's account safety: the second password (the account's vault PIN, 4 digits, like the game's vault lock) which
/// the admin can require for the vault, trades, the personal store and dropping or selling valuable items, with
/// wrong-try limits; item locks (locked items can't be traded, sold, dropped, put in a personal store or mixed; an
/// unlock needs the second password and waits); the login history (game and website, with the address) with a notice
/// in the game when the last login came from another address; and the trade history (trades and personal store sales).
/// The histories, locks and wrong tries are kept in a JSON file in the save folder; the second password is the
/// account's vault password in the database (hashed).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AccountSafetyPlugIn_Name), Description = nameof(PlugInResources.AccountSafetyPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("8E4C1B27-6A3D-4F95-B0D2-73C9E15A4F68")]
public class AccountSafetyPlugIn : IPlayerStateChangedPlugIn, IItemDropPlugIn, IItemMovedPlugIn, ISupportCustomConfiguration<AccountSafetyConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();

    private static readonly ConditionalWeakTable<Player, Session> Sessions = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private (AccountSafetyConfiguration Configuration, AccountSafetyEngine Engine)? _engine;

    /// <inheritdoc />
    public AccountSafetyConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active plug-in of a game context.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The plug-in; <c>null</c>, if it's deactivated.</returns>
    public static AccountSafetyPlugIn? Get(IGameContext? gameContext) =>
        gameContext?.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<AccountSafetyPlugIn>().FirstOrDefault();

    /// <summary>
    /// Gets the game contexts of this process which run the plug-in (for the website and the admin panel).
    /// </summary>
    /// <returns>The contexts.</returns>
    public static IReadOnlyCollection<IGameContext> KnownContexts() => Contexts.Values.Distinct().ToList();

    /// <summary>
    /// Describes an item for the lists, e.g. "Excellent Kris +7 +Skill +Luck".
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The text.</returns>
    public static string Describe(Item item)
    {
        var parts = new List<string>();
        if (item.IsExcellent())
        {
            parts.Add("Excellent");
        }

        if (item.IsAncient())
        {
            parts.Add("Ancient");
        }

        parts.Add(item.Definition?.Name.ValueInNeutralLanguage ?? "?");
        var text = string.Join(' ', parts);
        if (item.Level > 0)
        {
            text += $" +{item.Level}";
        }

        if (item.HasSkill)
        {
            text += " +Skill";
        }

        if (item.ItemOptions?.Any(o => o.ItemOption?.OptionType == DataModel.Configuration.Items.ItemOptionTypes.Luck) == true)
        {
            text += " +Luck";
        }

        if (item.IsStackable() && item.Durability > 1)
        {
            text += $" x{(int)item.Durability}";
        }

        return text;
    }

    /// <summary>
    /// Records a failed game login (for accounts which logged in before).
    /// </summary>
    /// <param name="player">The connection's player.</param>
    /// <param name="login">The login name which was tried.</param>
    public static void RecordFailedLogin(Player player, string login)
    {
        if (Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        var engine = plugIn.GetEngine();
        engine.RecordLogin(login, (player as IHasIpAddress)?.IpAddress, "game", false, DateTime.UtcNow);
        Save(engine);
    }

    /// <summary>
    /// Finds a player of an account who is in a game of this process (in the game or offline leveling).
    /// </summary>
    /// <param name="contexts">The game contexts.</param>
    /// <param name="login">The login name.</param>
    /// <returns>The player, if any.</returns>
    public static async ValueTask<Player?> FindPlayerAsync(IEnumerable<IGameContext> contexts, string login)
    {
        foreach (var context in contexts.Distinct())
        {
            var players = await context.GetPlayersAsync().ConfigureAwait(false);
            if (players.FirstOrDefault(p => !p.IsTemplatePlayer && string.Equals(p.Account?.LoginName, login, StringComparison.OrdinalIgnoreCase)) is { } player)
            {
                return player;
            }
        }

        return null;
    }

    /// <summary>
    /// Forgets that a player entered the second password (it was changed or cleared elsewhere).
    /// </summary>
    /// <param name="player">The player.</param>
    public static void ForgetEntered(Player player)
    {
        if (Sessions.TryGetValue(player, out var session))
        {
            session.EnteredAtUtc = null;
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new AccountSafetyConfiguration();

    /// <summary>
    /// Gets the engine of the current configuration. All game servers of this process share the store.
    /// </summary>
    /// <returns>The engine.</returns>
    public AccountSafetyEngine GetEngine()
    {
        var configuration = this.Configuration ??= new AccountSafetyConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, configuration))
        {
            return cached.Engine;
        }

        var engine = new AccountSafetyEngine(configuration, AccountSafetyStore.Open(configuration.FileName));
        this._engine = (configuration, engine);
        return engine;
    }

    /// <summary>
    /// Gets a value indicating whether the player entered the second password in this session (or has none).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the protected actions are open.</returns>
    public bool IsEntered(Player player)
    {
        if (!AccountSafetyEngine.HasPin(player.Account?.VaultPassword))
        {
            return true;
        }

        if (!Sessions.TryGetValue(player, out var session) || session.EnteredAtUtc is not { } at)
        {
            return false;
        }

        var minutes = this.GetEngine().Configuration.UnlockedMinutes;
        return minutes <= 0 || DateTime.UtcNow - at < TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Refuses an action which needs the second password while the player didn't enter it: tells the player in the
    /// chat and asks the web game for it.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="action">The action.</param>
    /// <returns><c>true</c>, if the action is refused.</returns>
    public async ValueTask<bool> RefuseAsync(Player player, SafetyAction action)
    {
        var engine = this.GetEngine();
        if (!engine.IsRequired(action) || this.IsEntered(player))
        {
            return false;
        }

        var vietnamese = IsVietnamese(player);
        var what = action switch
        {
            SafetyAction.Vault => vietnamese ? "lấy đồ ra khỏi hòm đồ" : "take things out of the vault",
            SafetyAction.Trade => vietnamese ? "giao dịch" : "trade",
            SafetyAction.PersonalStore => vietnamese ? "mở cửa hàng cá nhân" : "open your personal store",
            _ => vietnamese ? "vứt hoặc bán vật phẩm quý" : "drop or sell a valuable item",
        };
        await player.ShowBlueMessageAsync(vietnamese
            ? $"Hãy nhập mật khẩu cấp 2 để {what}: /unlock 1234 (hoặc mở hòm đồ và bấm vào ổ khóa)."
            : $"Enter your second password to {what}: /unlock 1234 (or open the vault and click its lock).").ConfigureAwait(false);
        await this.SendStateAsync(player, false, new { type = "prompt", action = ActionName(action) }).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Refuses a use of a locked item (trade, sale, drop, personal store, mix) with a message.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if the item is locked.</returns>
    public async ValueTask<bool> RefuseLockedItemAsync(Player player, Item item)
    {
        if (await DuplicateItems.DuplicateItemsPlugIn.RefuseFrozenAsync(player, item).ConfigureAwait(false))
        {
            return true;
        }

        if (player.Account?.LoginName is not { Length: > 0 } login || !this.GetEngine().IsLocked(login, HNMO.Persistence.ObjectExtensions.GetId(item), DateTime.UtcNow))
        {
            return false;
        }

        var name = Describe(item);
        await player.ShowBlueMessageAsync(IsVietnamese(player)
            ? $"{name} đang bị khóa (bảo vệ tài khoản): không thể giao dịch, bán, vứt, bày bán hay ép. Mở khóa trên trang tài khoản hoặc bằng /unlockitem."
            : $"{name} is locked (account safety): it can't be traded, sold, dropped, put in a store or mixed. Unlock it on the account page or with /unlockitem.").ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Gets a value indicating whether an item counts as valuable.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if it does.</returns>
    public bool IsValuable(Item item) => this.GetEngine().IsValuable(item.Level, item.IsExcellent(), item.IsAncient());

    /// <summary>
    /// Refuses a drop or a sale of an item: a locked one always, a valuable one while the second password is needed.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if it's refused.</returns>
    public async ValueTask<bool> RefuseDropOrSaleAsync(Player player, Item item)
    {
        if (await this.RefuseLockedItemAsync(player, item).ConfigureAwait(false))
        {
            return true;
        }

        return this.IsValuable(item) && await this.RefuseAsync(player, SafetyAction.ValuableItem).ConfigureAwait(false);
    }

    /// <summary>
    /// Enters the second password: the vault and the protected actions open for this session.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pin">The PIN; <c>null</c> when it wasn't four digits.</param>
    /// <param name="tell">Whether to answer in the chat (else the caller does, like the vault's own answer).</param>
    /// <returns>The check's result.</returns>
    public async ValueTask<PinCheck> EnterPinAsync(Player player, string? pin, bool tell)
    {
        var engine = this.GetEngine();
        var login = player.Account?.LoginName ?? string.Empty;
        var check = engine.CheckPin(login, player.Account?.VaultPassword, pin, DateTime.UtcNow);
        Save(engine);
        if (check.Result is PinCheckResult.Ok or PinCheckResult.NoPin)
        {
            // The vault's own state isn't sent here: the desktop answers an unasked "accepted" by moving its
            // backed-up item, which only exists after its own PIN dialog. The web game reads "entered" instead.
            this.MarkEntered(player);
        }

        if (tell || check.Result == PinCheckResult.LockedOut)
        {
            await player.ShowBlueMessageAsync(this.PinText(check, IsVietnamese(player))).ConfigureAwait(false);
        }

        await this.SendStateAsync(player, false, new { type = "pin", result = ResultName(check.Result), triesLeft = check.TriesLeft, until = Iso(check.LockedOutUntilUtc) }).ConfigureAwait(false);
        return check;
    }

    /// <summary>
    /// Verifies the second password and immediately re-locks protected actions for this session (<c>/lock PIN</c>).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="pin">The PIN; <c>null</c> when not four digits.</param>
    /// <returns><see langword="true"/> when locked cleanly.</returns>
    public async ValueTask<bool> LockPinAsync(Player player, string? pin)
    {
        var vietnamese = IsVietnamese(player);
        var engine = this.GetEngine();
        var login = player.Account?.LoginName ?? string.Empty;
        var check = engine.CheckPin(login, player.Account?.VaultPassword, pin, DateTime.UtcNow);
        Save(engine);
        if (check.Result != PinCheckResult.Ok)
        {
            await player.ShowBlueMessageAsync(this.PinText(check, vietnamese)).ConfigureAwait(false);
            return false;
        }

        Sessions.Remove(player);
        await player.ShowBlueMessageAsync(vietnamese
            ? "Đã khóa ngay mật khẩu cấp 2: hòm đồ, giao dịch và cửa hàng đã được bảo vệ."
            : "Second password locked: vault, trades and personal store now require /unlock.").ConfigureAwait(false);
        await this.SendStateAsync(player, false, new { type = "locked" }).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Notes a second password set in the game (the vault's own "set PIN"): it counts as entered.
    /// </summary>
    /// <param name="player">The player.</param>
    public void PinSetInGame(Player player)
    {
        if (player.Account?.LoginName is { Length: > 0 } login)
        {
            var engine = this.GetEngine();
            engine.PinChanged(login, DateTime.UtcNow);
            Save(engine);
        }

        this.MarkEntered(player);
    }

    /// <summary>
    /// The answer to an entered second password.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <returns>The text.</returns>
    public string PinText(PinCheck check, bool vietnamese) => check.Result switch
    {
        PinCheckResult.Ok => vietnamese ? "Đã nhập đúng mật khẩu cấp 2: hòm đồ, giao dịch và cửa hàng đã mở khóa." : "Second password accepted: the vault, trades and your store are unlocked.",
        PinCheckResult.NoPin => vietnamese ? "Tài khoản của bạn chưa có mật khẩu cấp 2 (đặt trên trang tài khoản hoặc ở hòm đồ)." : "Your account has no second password (set one on the account page or at the vault).",
        PinCheckResult.LockedOut => vietnamese
            ? $"Nhập sai quá nhiều lần. Hãy thử lại sau {(check.LockedOutUntilUtc is { } u ? this.GetEngine().Local(u, true) : "-")} (giờ Sydney)."
            : $"Too many wrong second passwords. Try again after {(check.LockedOutUntilUtc is { } until ? this.GetEngine().Local(until, false) : "-")} (Sydney time).",
        _ => vietnamese ? $"Sai mật khẩu cấp 2 (còn {check.TriesLeft} lần thử)." : $"Wrong second password ({check.TriesLeft} tries left).",
    };

    /// <summary>
    /// Locks the item at a slot of the player's inventory or vault.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="vault">Whether the slot is in the vault.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The result; <c>null</c> when there's no item.</returns>
    public async ValueTask<ItemLockResult?> LockItemAsync(Player player, bool vault, byte slot)
    {
        var vietnamese = IsVietnamese(player);
        if (player.Account?.LoginName is not { Length: > 0 } login || ItemAt(player, vault, slot) is not { } item)
        {
            await player.ShowBlueMessageAsync(vietnamese ? "Không có vật phẩm ở ô đó." : "There's no item in that slot.").ConfigureAwait(false);
            return null;
        }

        var engine = this.GetEngine();
        var name = Describe(item);
        var result = engine.Lock(login, HNMO.Persistence.ObjectExtensions.GetId(item), name, vault ? string.Empty : player.SelectedCharacter?.Name ?? string.Empty, DateTime.UtcNow);
        Save(engine);
        await player.ShowBlueMessageAsync(this.LockText(result, name, null, vietnamese)).ConfigureAwait(false);
        await this.SendStateAsync(player, false, new { type = "lock", result = result.ToString(), name }).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Asks the unlock of the item at a slot of the player's inventory or vault, with the second password.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="vault">Whether the slot is in the vault.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="pin">The second password (needed when the account has one).</param>
    /// <returns>The result; <c>null</c> when there's no item or the second password was wrong.</returns>
    public async ValueTask<ItemLockResult?> UnlockItemAsync(Player player, bool vault, byte slot, string? pin)
    {
        var vietnamese = IsVietnamese(player);
        if (player.Account?.LoginName is not { Length: > 0 } login || ItemAt(player, vault, slot) is not { } item)
        {
            await player.ShowBlueMessageAsync(vietnamese ? "Không có vật phẩm ở ô đó." : "There's no item in that slot.").ConfigureAwait(false);
            return null;
        }

        var engine = this.GetEngine();
        var name = Describe(item);
        var now = DateTime.UtcNow;
        if (!engine.IsLocked(login, HNMO.Persistence.ObjectExtensions.GetId(item), now))
        {
            await player.ShowBlueMessageAsync(this.LockText(ItemLockResult.NotLocked, name, null, vietnamese)).ConfigureAwait(false);
            return ItemLockResult.NotLocked;
        }

        var check = engine.CheckPin(login, player.Account.VaultPassword, pin, now);
        if (check.Result is not (PinCheckResult.Ok or PinCheckResult.NoPin))
        {
            Save(engine);
            await player.ShowBlueMessageAsync(this.PinText(check, vietnamese)).ConfigureAwait(false);
            await this.SendStateAsync(player, false, new { type = "pin", result = ResultName(check.Result), triesLeft = check.TriesLeft, until = Iso(check.LockedOutUntilUtc) }).ConfigureAwait(false);
            return null;
        }

        var (result, at) = engine.Unlock(login, HNMO.Persistence.ObjectExtensions.GetId(item), now);
        Save(engine);
        await player.ShowBlueMessageAsync(this.LockText(result, name, at, vietnamese)).ConfigureAwait(false);
        await this.SendStateAsync(player, false, new { type = "lock", result = result.ToString(), name, until = Iso(at) }).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The answer to a lock or unlock of an item.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="name">The item's text.</param>
    /// <param name="unlockAtUtc">The unlock time.</param>
    /// <param name="vietnamese">Whether in Vietnamese.</param>
    /// <returns>The text.</returns>
    public string LockText(ItemLockResult result, string name, DateTime? unlockAtUtc, bool vietnamese)
    {
        var engine = this.GetEngine();
        var at = unlockAtUtc is { } u ? engine.Local(u, vietnamese) : "-";
        return result switch
        {
            ItemLockResult.Locked => vietnamese ? $"Đã khóa {name}." : $"{name} is locked.",
            ItemLockResult.AlreadyLocked => vietnamese ? $"{name} đã bị khóa rồi." : $"{name} is locked already.",
            ItemLockResult.UnlockCancelled => vietnamese ? $"Đã hủy mở khóa {name}: vật phẩm vẫn bị khóa." : $"The unlock of {name} was cancelled: it stays locked.",
            ItemLockResult.UnlockStarted => vietnamese ? $"{name} sẽ được mở khóa lúc {at} (giờ Sydney)." : $"{name} unlocks at {at} (Sydney time).",
            ItemLockResult.UnlockWaiting => vietnamese ? $"{name} đang chờ mở khóa đến {at} (giờ Sydney)." : $"{name} already waits for its unlock at {at} (Sydney time).",
            ItemLockResult.Unlocked => vietnamese ? $"Đã mở khóa {name}." : $"{name} is unlocked.",
            ItemLockResult.NotLocked => vietnamese ? $"{name} không bị khóa." : $"{name} isn't locked.",
            ItemLockResult.TooMany => vietnamese ? $"Bạn đã khóa tối đa {engine.Configuration.MaxLockedItems} vật phẩm." : $"You locked the most items you may ({engine.Configuration.MaxLockedItems}).",
            _ => vietnamese ? "Máy chủ này chưa bật khóa vật phẩm." : "The item lock isn't on for this server.",
        };
    }

    /// <summary>
    /// Records a finished trade for both accounts.
    /// </summary>
    /// <param name="trader">One trader.</param>
    /// <param name="partner">The other trader.</param>
    /// <param name="traderItems">What the trader gave.</param>
    /// <param name="partnerItems">What the partner gave.</param>
    /// <param name="traderZen">The zen the trader gave.</param>
    /// <param name="partnerZen">The zen the partner gave.</param>
    public void RecordTrade(ITrader trader, ITrader partner, IReadOnlyList<Item> traderItems, IReadOnlyList<Item> partnerItems, long traderZen, long partnerZen)
    {
        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var given = traderItems.Select(Describe).ToList();
        var received = partnerItems.Select(Describe).ToList();
        if (LoginOf(trader) is { } traderLogin)
        {
            engine.RecordTrade(traderLogin, new SafetyTradeEntry { Kind = "trade", Character = trader.Name, Partner = partner.Name, PartnerLogin = LoginOf(partner) ?? string.Empty, Gave = given, Got = received, ZenGave = traderZen, ZenGot = partnerZen }, now);
        }

        if (LoginOf(partner) is { } partnerLogin)
        {
            engine.RecordTrade(partnerLogin, new SafetyTradeEntry { Kind = "trade", Character = partner.Name, Partner = trader.Name, PartnerLogin = LoginOf(trader) ?? string.Empty, Gave = received.ToList(), Got = given.ToList(), ZenGave = partnerZen, ZenGot = traderZen }, now);
        }

        Save(engine);
    }

    /// <summary>
    /// Records a personal store sale for the seller and the buyer.
    /// </summary>
    /// <param name="seller">The seller.</param>
    /// <param name="buyer">The buyer.</param>
    /// <param name="item">The item.</param>
    /// <param name="zen">The price in zen; 0 when it was paid otherwise.</param>
    /// <param name="otherPrice">The price when it wasn't zen (e.g. "3 Jewel of Bless").</param>
    public void RecordStoreSale(Player seller, Player buyer, Item item, long zen, string? otherPrice)
    {
        var engine = this.GetEngine();
        var now = DateTime.UtcNow;
        var name = Describe(item);
        var price = string.IsNullOrEmpty(otherPrice) ? new List<string>() : new List<string> { otherPrice };
        if (LoginOf(seller) is { } sellerLogin)
        {
            engine.RecordTrade(sellerLogin, new SafetyTradeEntry { Kind = "store-sale", Character = seller.Name, Partner = buyer.Name, PartnerLogin = LoginOf(buyer) ?? string.Empty, Gave = new List<string> { name }, Got = price, ZenGot = zen }, now);
        }

        if (LoginOf(buyer) is { } buyerLogin)
        {
            engine.RecordTrade(buyerLogin, new SafetyTradeEntry { Kind = "store-buy", Character = buyer.Name, Partner = seller.Name, PartnerLogin = LoginOf(seller) ?? string.Empty, Gave = price.ToList(), Got = new List<string> { name }, ZenGave = zen }, now);
        }

        Save(engine);
    }

    /// <summary>
    /// Sends the state to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client asked for it.</param>
    /// <param name="eventValue">What happened (an object serialized into "event").</param>
    /// <returns>The task.</returns>
    public async ValueTask SendStateAsync(Player player, bool requested, object? eventValue = null)
    {
        if (player is OfflinePlayer || player.Account is not { LoginName: { Length: > 0 } })
        {
            return;
        }

        var json = this.WriteState(player, eventValue);
        await player.InvokeViewPlugInAsync<IHnmoAccountSafetyViewPlugIn>(p => p.ShowAccountSafetyAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>
    /// The state of a player as JSON (ASCII; other characters are escaped).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="eventValue">What happened.</param>
    /// <returns>The JSON.</returns>
    public string WriteState(Player player, object? eventValue)
    {
        var engine = this.GetEngine();
        var configuration = engine.Configuration;
        var login = player.Account?.LoginName ?? string.Empty;
        var now = DateTime.UtcNow;
        var locks = engine.Locks(login, now).ToDictionary(l => l.ItemId);
        var slots = new List<object>();
        void Add(IStorage? storage, string kind)
        {
            foreach (var item in storage?.Items ?? Enumerable.Empty<Item>())
            {
                if (locks.TryGetValue(HNMO.Persistence.ObjectExtensions.GetId(item), out var itemLock))
                {
                    slots.Add(new { storage = kind, slot = (int)item.ItemSlot, name = itemLock.Description, unlockAt = Iso(itemLock.UnlockAtUtc) });
                }
            }
        }

        Add(player.Inventory, "inventory");
        Add(player.Vault, "vault");
        var state = new
        {
            v = 1,
            enabled = true,
            hasPin = AccountSafetyEngine.HasPin(player.Account?.VaultPassword),
            entered = this.IsEntered(player),
            lockedOutUntil = Iso(engine.LockedOutUntil(login, now)),
            required = new { vault = configuration.RequireForVault, trade = configuration.RequireForTrade, store = configuration.RequireForPersonalStore, valuable = configuration.RequireForValuableItems },
            valuableLevel = configuration.ValuableMinimumLevel,
            itemLock = new { enabled = configuration.ItemLockEnabled, waitHours = configuration.UnlockWaitHours },
            locks = slots,
            @event = eventValue,
        };
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (player is OfflinePlayer || player.IsTemplatePlayer || player.Account is not { IsBot: false, LoginName: { Length: > 0 } login })
        {
            return;
        }

        Contexts.TryAdd(player.GameContext is IGameServerContext server ? server.Id : (byte)0, player.GameContext);
        if (currentState == PlayerState.Authenticated && previousState == PlayerState.LoginScreen)
        {
            var engine = this.GetEngine();
            var address = (player as IHasIpAddress)?.IpAddress;
            var previous = engine.RecordLogin(login, address, "game", true, DateTime.UtcNow);
            Save(engine);
            var session = Sessions.GetOrCreateValue(player);
            session.EnteredAtUtc = null;
            session.NewAddressFrom = engine.Configuration.NotifyNewAddress && AccountSafetyEngine.IsNewAddress(previous, address) ? previous : null;
            return;
        }

        if (currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection)
        {
            return;
        }

        if (!this.GetEngine().Configuration.RequireForVault && player.IsVaultLocked)
        {
            player.IsVaultLocked = false;
        }

        if (Sessions.TryGetValue(player, out var current) && current.NewAddressFrom is { } from)
        {
            current.NewAddressFrom = null;
            var vietnamese = IsVietnamese(player);
            var engine = this.GetEngine();
            var where = from.Source == "website" ? (vietnamese ? "trang web" : "the website") : (vietnamese ? "game" : "the game");
            await player.ShowBlueMessageAsync(vietnamese
                ? $"Lần đăng nhập trước của bạn ({engine.Local(from.AtUtc, true)} giờ Sydney, {where}) đến từ địa chỉ khác: {AccountSafetyEngine.Mask(from.Address)}. Nếu không phải bạn, hãy đổi mật khẩu."
                : $"Your last login ({engine.Local(from.AtUtc, false)} Sydney time, {where}) came from another address: {AccountSafetyEngine.Mask(from.Address)}. If it wasn't you, change your password.").ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask HandleItemDropAsync(Player player, Item item, Point target, IItemDropPlugIn.ItemDropArguments dropArgs)
    {
        if (dropArgs.Cancel)
        {
            return;
        }

        if (await this.RefuseDropOrSaleAsync(player, item).ConfigureAwait(false))
        {
            dropArgs.WasHandled = true;
            dropArgs.Success = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask ItemMovedAsync(Player player, Item item)
    {
        if (player.Account?.LoginName is { Length: > 0 } login && this.GetEngine().IsLocked(login, HNMO.Persistence.ObjectExtensions.GetId(item), DateTime.UtcNow))
        {
            await this.SendStateAsync(player, false).ConfigureAwait(false);
        }
    }

    private static Item? ItemAt(Player player, bool vault, byte slot) => vault ? player.Vault?.GetItem(slot) : player.Inventory?.GetItem(slot);

    private static string? LoginOf(ITrader trader) =>
        trader is Player { IsTemplatePlayer: false, Account: { IsBot: false, LoginName: { Length: > 0 } login } } ? login : null;

    private static string? Iso(DateTime? utc) => utc is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : null;

    private static bool IsVietnamese(Player player) => player.Culture.TwoLetterISOLanguageName == "vi";

    private static string ActionName(SafetyAction action) => action switch
    {
        SafetyAction.Vault => "vault",
        SafetyAction.Trade => "trade",
        SafetyAction.PersonalStore => "store",
        _ => "valuable",
    };

    private static string ResultName(PinCheckResult result) => result switch
    {
        PinCheckResult.Ok => "ok",
        PinCheckResult.Wrong => "wrong",
        PinCheckResult.LockedOut => "lockedOut",
        _ => "noPin",
    };

    private static void Save(AccountSafetyEngine engine)
    {
        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }
    }

    private void MarkEntered(Player player)
    {
        Sessions.GetOrCreateValue(player).EnteredAtUtc = DateTime.UtcNow;
        player.IsVaultLocked = false;
    }

    /// <summary>
    /// The safety state of a player's session.
    /// </summary>
    private sealed class Session
    {
        public DateTime? EnteredAtUtc { get; set; }

        public SafetyLoginEntry? NewAddressFrom { get; set; }
    }
}
