// <copyright file="EscrowEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Pure atomic rule engine for in-game escrow contracts (16.1 + 16.3):
/// fees, safety thresholds, account age, banned items, post-escrow cooldowns, reputation, and concurrency-safe transitions.
/// </summary>
public sealed class EscrowEngine
{
    private const int MaxFeeLedgerEntries = 500;
    private readonly EscrowConfiguration _cfg;
    private readonly EscrowStore _store;

    /// <summary>Initializes a new instance of the <see cref="EscrowEngine"/> class.</summary>
    /// <param name="configuration">Active escrow configuration.</param>
    /// <param name="store">Persistent atomic escrow store.</param>
    public EscrowEngine(EscrowConfiguration configuration, EscrowStore store)
    {
        this._cfg = configuration ?? new EscrowConfiguration();
        this._store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Gets configuration snapshot.</summary>
    public EscrowConfiguration Configuration => this._cfg;

    /// <summary>Gets underlying atomic store.</summary>
    public EscrowStore Store => this._store;

    /// <summary>Converts UTC timestamp into canonical Sydney formatted string.</summary>
    /// <param name="utc">UTC time.</param>
    /// <returns>Sydney formatted timestamp.</returns>
    public static string ToSydneyText(DateTime utc)
    {
        var tz = HnmoStateStore.ResolveTimeZone(null);
        var local = HnmoStateStore.ToTimeZone(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        return local.ToString("yyyy-MM-dd HH:mm 'Sydney'", CultureInfo.InvariantCulture);
    }

    /// <summary>Computes host fee breakdown for <paramref name="priceAmount"/> using current configuration.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="priceAmount">Gross contract asking price.</param>
    /// <returns>Computed breakdown.</returns>
    public static EscrowFeeBreakdown ComputeFee(EscrowConfiguration cfg, long priceAmount)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (priceAmount <= 0 || cfg.FeePercent <= 0)
        {
            return new EscrowFeeBreakdown(0, 0, 0, priceAmount, priceAmount);
        }

        var pct = Math.Clamp(cfg.FeePercent, 0.0, 25.0);
        var raw = (long)Math.Round(priceAmount * (pct / 100.0), MidpointRounding.AwayFromZero);
        if (raw < cfg.MinFee)
        {
            raw = cfg.MinFee;
        }

        if (cfg.MaxFee > 0 && raw > cfg.MaxFee)
        {
            raw = cfg.MaxFee;
        }

        var totalFee = Math.Clamp(raw, 0, Math.Max(0, priceAmount));
        return cfg.FeePayer switch
        {
            EscrowFeePayer.Buyer => new EscrowFeeBreakdown(totalFee, 0, totalFee, priceAmount + totalFee, priceAmount),
            EscrowFeePayer.Split => new EscrowFeeBreakdown(
                totalFee,
                totalFee - (totalFee / 2),
                totalFee / 2,
                priceAmount + (totalFee / 2),
                Math.Max(0, priceAmount - (totalFee - (totalFee / 2)))),
            _ => new EscrowFeeBreakdown(totalFee, totalFee, 0, priceAmount, Math.Max(0, priceAmount - totalFee)),
        };
    }

    /// <summary>Checks whether <paramref name="registrationDateUtc"/> satisfies <see cref="EscrowConfiguration.MinAccountAgeDays"/>.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="registrationDateUtc">Account creation UTC timestamp.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns><see langword="true"/> if old enough.</returns>
    public static bool MeetsAccountAgeRequirement(EscrowConfiguration cfg, DateTime registrationDateUtc, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (cfg.MinAccountAgeDays <= 0 || registrationDateUtc == DateTime.MinValue)
        {
            return true;
        }

        return (utcNow - DateTime.SpecifyKind(registrationDateUtc, DateTimeKind.Utc)).TotalDays >= cfg.MinAccountAgeDays;
    }

    /// <summary>Checks whether an item is banned from escrow.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="group">Item group.</param>
    /// <param name="number">Item number.</param>
    /// <param name="nameEn">Optional item English name.</param>
    /// <returns><see langword="true"/> when banned.</returns>
    public static bool IsBannedItem(EscrowConfiguration cfg, byte group, short number, string? nameEn = null)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (cfg.BannedItems is null || cfg.BannedItems.Count == 0)
        {
            return false;
        }

        var code = $"{group}:{number}";
        var slashCode = $"{group}/{number}";
        return cfg.BannedItems.Any(b =>
            string.Equals(b?.Trim(), code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(b?.Trim(), slashCode, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(nameEn) && string.Equals(b?.Trim(), nameEn.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Checks whether a currency is currently permitted by <see cref="EscrowConfiguration.AllowedCurrencies"/>.</summary>
    /// <param name="currency">Candidate currency.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public bool IsCurrencyAllowed(EscrowCurrency currency)
    {
        if (currency == EscrowCurrency.TradeCredit && !EscrowTradeCreditEngine.IsCompliantAndEnabled(this._cfg))
        {
            return false;
        }

        return this._cfg.AllowedCurrencies is null || this._cfg.AllowedCurrencies.Count == 0 || this._cfg.AllowedCurrencies.Contains(currency);
    }

    /// <summary>Determines whether a contract exceeds configured high-value extra-confirmation thresholds.</summary>
    /// <param name="priceCurrency">Asking currency.</param>
    /// <param name="priceAmount">Asking price amount.</param>
    /// <param name="escrowedZen">Escrowed seller Zen.</param>
    /// <param name="escrowedJewels">Escrowed seller jewels.</param>
    /// <returns><see langword="true"/> when explicit confirmation is required.</returns>
    public bool IsHighValue(EscrowCurrency priceCurrency, long priceAmount, long escrowedZen, IReadOnlyCollection<EscrowJewelLine>? escrowedJewels)
    {
        if (priceCurrency == EscrowCurrency.Zen && this._cfg.HighValueThresholdZen > 0 && priceAmount >= this._cfg.HighValueThresholdZen)
        {
            return true;
        }

        if (priceCurrency is EscrowCurrency.WCoinC or EscrowCurrency.WCoinP or EscrowCurrency.GoblinPoints
            && this._cfg.HighValueThresholdCoins > 0
            && priceAmount >= this._cfg.HighValueThresholdCoins)
        {
            return true;
        }

        var askingJewels = priceCurrency is >= EscrowCurrency.JewelBless and <= EscrowCurrency.JewelLife ? priceAmount : 0;
        var sellerJewels = escrowedJewels?.Sum(j => Math.Max(0, j.Quantity)) ?? 0;
        if (this._cfg.HighValueThresholdJewels > 0 && (askingJewels >= this._cfg.HighValueThresholdJewels || sellerJewels >= this._cfg.HighValueThresholdJewels))
        {
            return true;
        }

        if (this._cfg.HighValueThresholdZen > 0 && escrowedZen >= this._cfg.HighValueThresholdZen)
        {
            return true;
        }

        return false;
    }

    /// <summary>Checks if <paramref name="login"/> holds a post-escrow trade lock covering the specified item.</summary>
    /// <param name="login">Account login.</param>
    /// <param name="group">Item group.</param>
    /// <param name="number">Item number.</param>
    /// <param name="level">Item level.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Lock expiry UTC timestamp if locked; otherwise <c>null</c>.</returns>
    public DateTime? GetItemTradeLockUntil(string login, byte group, short number, byte level, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(login) || this._cfg.PostEscrowTradeCooldownHours <= 0)
        {
            return null;
        }

        return this._store.Read(data =>
            data.ItemTradeCooldowns
                .Where(c => string.Equals(c.LoginName, login, StringComparison.OrdinalIgnoreCase)
                    && c.Group == group
                    && c.Number == number
                    && c.Level == level
                    && c.LockedUntilUtc > utcNow)
                .OrderByDescending(c => c.LockedUntilUtc)
                .Select(c => (DateTime?)c.LockedUntilUtc)
                .FirstOrDefault());
    }

    /// <summary>Creates and persists a new open escrow contract atomically after validating rules.</summary>
    /// <param name="sellerLogin">Seller account login.</param>
    /// <param name="sellerCharacter">Seller character name.</param>
    /// <param name="targetBuyer">Optional named buyer login or character.</param>
    /// <param name="items">Escrowed items.</param>
    /// <param name="escrowedZen">Escrowed Zen amount.</param>
    /// <param name="escrowedJewels">Escrowed Jewel Bank jewels.</param>
    /// <param name="priceCurrency">Asking price currency.</param>
    /// <param name="priceAmount">Asking price amount.</param>
    /// <param name="deadlineHours">Expiry window in hours.</param>
    /// <param name="confirmHighValue">High-value confirmation flag.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Result error code (<c>Ok</c> when created) and created contract.</returns>
    public (string Error, EscrowContract? Contract) CreateContract(
        string sellerLogin,
        string sellerCharacter,
        string? targetBuyer,
        IReadOnlyList<EscrowItemSnapshot>? items,
        long escrowedZen,
        IReadOnlyList<EscrowJewelLine>? escrowedJewels,
        EscrowCurrency priceCurrency,
        long priceAmount,
        int deadlineHours,
        bool confirmHighValue,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(sellerLogin))
        {
            return ("NotAuthenticated", null);
        }

        var itemList = items?.ToList() ?? [];
        var jewelList = escrowedJewels?.Where(j => j.Quantity > 0).ToList() ?? [];
        if (itemList.Count == 0 && escrowedZen <= 0 && jewelList.Count == 0)
        {
            return ("EmptyGoods", null);
        }

        if (priceAmount <= 0 || !this.IsCurrencyAllowed(priceCurrency))
        {
            return ("InvalidPrice", null);
        }

        foreach (var it in itemList)
        {
            if (IsBannedItem(this._cfg, it.Group, it.Number, it.NameEn))
            {
                return ("BannedItem", null);
            }
        }

        if (this.IsHighValue(priceCurrency, priceAmount, escrowedZen, jewelList) && !confirmHighValue)
        {
            return ("NeedsHighValueConfirmation", null);
        }

        var trimmedBuyer = (targetBuyer ?? string.Empty).Trim();
        if (string.Equals(trimmedBuyer, sellerLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedBuyer, sellerCharacter, StringComparison.OrdinalIgnoreCase))
        {
            return ("SelfTargetNotAllowed", null);
        }

        var effectiveDeadline = Math.Clamp(
            deadlineHours <= 0 ? this._cfg.DefaultDeadlineHours : deadlineHours,
            1,
            Math.Max(1, this._cfg.MaxDeadlineHours));
        var fee = ComputeFee(this._cfg, priceAmount);
        var expiresAt = utcNow.AddHours(effectiveDeadline);

        return this._store.Mutate(data =>
        {
            var openCount = data.Contracts.Count(c =>
                c.Status == EscrowContractStatus.Open
                && string.Equals(c.SellerLogin, sellerLogin, StringComparison.OrdinalIgnoreCase));
            if (openCount >= Math.Max(1, this._cfg.MaxOpenContractsPerAccount))
            {
                return (false, ("TooManyOpenContracts", (EscrowContract?)null));
            }

            var contract = new EscrowContract
            {
                ContractId = "C-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                Mode = "InGame",
                Status = EscrowContractStatus.Open,
                CreatedAtUtc = utcNow,
                CreatedAtSydney = ToSydneyText(utcNow),
                ExpiresAtUtc = expiresAt,
                ExpiresAtSydney = ToSydneyText(expiresAt),
                SellerLogin = sellerLogin,
                SellerCharacter = sellerCharacter,
                TargetBuyer = trimmedBuyer,
                Items = itemList,
                EscrowedZen = Math.Max(0, escrowedZen),
                EscrowedJewels = jewelList,
                PriceCurrency = priceCurrency,
                PriceAmount = priceAmount,
                FeePayer = this._cfg.FeePayer,
                FeeAmount = fee.TotalFee,
                BuyerTotalDebit = fee.BuyerTotalDebit,
                NetSellerProceeds = fee.NetSellerProceeds,
            };
            data.Contracts.Insert(0, contract);
            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>
    /// Atomically validates and executes the swap in ONE store transaction:
    /// guarantees only ONE buyer can win any race, and fail/crash rolls back cleanly.
    /// </summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="buyerCharacter">Buyer character name.</param>
    /// <param name="contractId">Target contract identifier.</param>
    /// <param name="confirmHighValue">High-value confirmation flag.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <param name="paymentDebiter">Optional payment debiter callback invoked inside transaction lock.</param>
    /// <returns>Status code (<c>Ok</c>, <c>AlreadyClosed</c>, <c>Frozen</c>, <c>Expired</c>, <c>OwnContract</c>, <c>WrongBuyer</c>, <c>NeedsHighValueConfirmation</c>) and swap snapshot.</returns>
    public (string Error, EscrowContract? Contract) AcceptContractAtomic(
        string buyerLogin,
        string buyerCharacter,
        string contractId,
        bool confirmHighValue,
        DateTime utcNow,
        Func<EscrowContract, bool>? paymentDebiter = null)
    {
        if (string.IsNullOrWhiteSpace(buyerLogin) || string.IsNullOrWhiteSpace(contractId))
        {
            return ("InvalidRequest", null);
        }

        return this._store.Mutate(data =>
        {
            var contract = data.Contracts.FirstOrDefault(c => string.Equals(c.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (contract is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (contract.FrozenByAdmin || contract.Status == EscrowContractStatus.Frozen)
            {
                return (false, ("Frozen", (EscrowContract?)null));
            }

            if (contract.Status != EscrowContractStatus.Open)
            {
                return (false, ("AlreadyClosed", (EscrowContract?)null));
            }

            if (utcNow >= contract.ExpiresAtUtc)
            {
                ExpireSingleLocked(data, contract, utcNow);
                return (true, ("Expired", (EscrowContract?)null));
            }

            if (string.Equals(contract.SellerLogin, buyerLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("OwnContract", (EscrowContract?)null));
            }

            if (!string.IsNullOrWhiteSpace(contract.TargetBuyer)
                && !string.Equals(contract.TargetBuyer, buyerLogin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(contract.TargetBuyer, buyerCharacter, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("WrongBuyer", (EscrowContract?)null));
            }

            if (this.IsHighValue(contract.PriceCurrency, contract.PriceAmount, contract.EscrowedZen, contract.EscrowedJewels)
                && !confirmHighValue)
            {
                return (false, ("NeedsHighValueConfirmation", (EscrowContract?)null));
            }

            if (paymentDebiter is not null && !paymentDebiter(contract))
            {
                return (false, ("PaymentFailed", (EscrowContract?)null));
            }

            contract.Status = EscrowContractStatus.Completed;
            contract.AcceptedByLogin = buyerLogin;
            contract.AcceptedByCharacter = buyerCharacter;
            contract.ClosedAtUtc = utcNow;

            DateTime? cooldownUntil = this._cfg.PostEscrowTradeCooldownHours > 0
                ? utcNow.AddHours(this._cfg.PostEscrowTradeCooldownHours)
                : null;

            // Enqueue buyer's goods delivery (items + escrowed Zen + escrowed jewels).
            data.OwedDeliveries.Add(new EscrowOwedDelivery
            {
                ContractId = contract.ContractId,
                RecipientLogin = buyerLogin,
                ReasonEn = $"Escrow contract {contract.ContractId} goods from {contract.SellerCharacter}",
                ReasonVi = $"Hàng hợp đồng ký gửi {contract.ContractId} từ {contract.SellerCharacter}",
                Items = contract.Items.ToList(),
                Zen = contract.EscrowedZen,
                Jewels = contract.EscrowedJewels.ToList(),
                CreatedAtUtc = utcNow,
                ItemTradeCooldownUntilUtc = cooldownUntil,
            });

            if (cooldownUntil is { } lockUntil)
            {
                foreach (var it in contract.Items)
                {
                    data.ItemTradeCooldowns.Add(new EscrowTradeCooldownEntry
                    {
                        LoginName = buyerLogin,
                        Group = it.Group,
                        Number = it.Number,
                        Level = it.Level,
                        ItemName = it.NameEn,
                        LockedUntilUtc = lockUntil,
                    });
                }
            }

            // Enqueue seller's non-coin proceeds (Zen or Jewels; Coins delivered via MissionCoinGranter idempotently).
            if (contract.PriceCurrency == EscrowCurrency.Zen && contract.NetSellerProceeds > 0)
            {
                data.OwedDeliveries.Add(new EscrowOwedDelivery
                {
                    ContractId = contract.ContractId,
                    RecipientLogin = contract.SellerLogin,
                    ReasonEn = $"Escrow contract {contract.ContractId} Zen payout",
                    ReasonVi = $"Thanh toán Zen hợp đồng {contract.ContractId}",
                    Zen = contract.NetSellerProceeds,
                    CreatedAtUtc = utcNow,
                });
            }
            else if (contract.PriceCurrency is >= EscrowCurrency.JewelBless and <= EscrowCurrency.JewelLife && contract.NetSellerProceeds > 0)
            {
                data.OwedDeliveries.Add(new EscrowOwedDelivery
                {
                    ContractId = contract.ContractId,
                    RecipientLogin = contract.SellerLogin,
                    ReasonEn = $"Escrow contract {contract.ContractId} Jewel payout",
                    ReasonVi = $"Thanh toán Ngọc hợp đồng {contract.ContractId}",
                    Jewels = [new EscrowJewelLine { JewelKey = contract.PriceCurrency.ToString(), Quantity = (int)Math.Min(int.MaxValue, contract.NetSellerProceeds) }],
                    CreatedAtUtc = utcNow,
                });
            }

            if (contract.FeeAmount > 0)
            {
                data.FeeLedger.Insert(0, new EscrowFeeLedgerEntry
                {
                    TimestampUtc = utcNow,
                    TimestampSydney = ToSydneyText(utcNow),
                    ContractId = contract.ContractId,
                    Mode = contract.Mode,
                    Currency = contract.PriceCurrency.ToString(),
                    FeeAmount = contract.FeeAmount,
                });
                if (data.FeeLedger.Count > MaxFeeLedgerEntries)
                {
                    data.FeeLedger = data.FeeLedger.Take(MaxFeeLedgerEntries).ToList();
                }
            }

            UpdateReputationLocked(data, contract.SellerLogin, completedDelta: 1, cancelledDelta: 0);
            UpdateReputationLocked(data, buyerLogin, completedDelta: 1, cancelledDelta: 0);

            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>Cancels an open contract atomically and enqueues return of all escrowed seller goods.</summary>
    /// <param name="actorLogin">Requesting actor login.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="isAdmin">Whether triggered by admin override.</param>
    /// <param name="reason">Cancellation reason.</param>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Status code and affected contract.</returns>
    public (string Error, EscrowContract? Contract) CancelContract(
        string actorLogin,
        string contractId,
        bool isAdmin,
        string reason,
        DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var contract = data.Contracts.FirstOrDefault(c => string.Equals(c.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (contract is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (contract.Status != EscrowContractStatus.Open && !(isAdmin && contract.Status == EscrowContractStatus.Frozen))
            {
                return (false, ("NotOpen", (EscrowContract?)null));
            }

            if (!isAdmin && contract.FrozenByAdmin)
            {
                return (false, ("Frozen", (EscrowContract?)null));
            }

            if (!isAdmin && !string.Equals(contract.SellerLogin, actorLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("NotSeller", (EscrowContract?)null));
            }

            contract.Status = EscrowContractStatus.Cancelled;
            contract.FrozenByAdmin = false;
            contract.AdminReason = reason;
            contract.ClosedAtUtc = utcNow;

            var bondRefund = contract.DirectVnd is { SellerBondCoins: > 0, BondReleased: false, BondForfeited: false } dv ? dv.SellerBondCoins : 0;
            if (bondRefund > 0 && contract.DirectVnd is not null)
            {
                contract.DirectVnd.BondReleased = true;
            }

            data.OwedDeliveries.Add(new EscrowOwedDelivery
            {
                ContractId = contract.ContractId,
                RecipientLogin = contract.SellerLogin,
                ReasonEn = $"Cancelled escrow {contract.ContractId} return",
                ReasonVi = $"Hoàn trả hàng hủy hợp đồng {contract.ContractId}",
                Items = contract.Items.ToList(),
                Zen = contract.EscrowedZen,
                Jewels = contract.EscrowedJewels.ToList(),
                CoinCurrency = contract.DirectVnd?.SellerBondCurrency ?? EscrowCurrency.WCoinC,
                Coins = bondRefund,
                CreatedAtUtc = utcNow,
            });

            if (!isAdmin)
            {
                UpdateReputationLocked(data, contract.SellerLogin, completedDelta: 0, cancelledDelta: 1);
            }

            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>Freezes or unfreezes a contract by admin action.</summary>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="freeze">True to freeze, false to unfreeze.</param>
    /// <param name="reason">Admin reason note.</param>
    /// <returns><see langword="true"/> when updated.</returns>
    public bool SetFrozen(string contractId, bool freeze, string reason)
    {
        return this._store.Mutate(data =>
        {
            var contract = data.Contracts.FirstOrDefault(c => string.Equals(c.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (contract is null || contract.Status is EscrowContractStatus.Completed or EscrowContractStatus.Cancelled or EscrowContractStatus.Expired)
            {
                return (false, false);
            }

            contract.FrozenByAdmin = freeze;
            contract.Status = freeze ? EscrowContractStatus.Frozen : EscrowContractStatus.Open;
            contract.AdminReason = reason;
            return (true, true);
        });
    }

    /// <summary>Expires all open contracts past deadline and returns seller goods into owed deliveries.</summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns>Number of expired contracts.</returns>
    public int ExpireDueContracts(DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var count = 0;
            foreach (var c in data.Contracts.Where(c => c.Status == EscrowContractStatus.Open && !c.FrozenByAdmin && utcNow >= c.ExpiresAtUtc))
            {
                ExpireSingleLocked(data, c, utcNow);
                count++;
            }

            return (count > 0, count);
        });
    }

    /// <summary>Returns owed deliveries queued for <paramref name="login"/>.</summary>
    /// <param name="login">Account login.</param>
    /// <returns>Queued deliveries.</returns>
    public List<EscrowOwedDelivery> PeekOwedDeliveries(string login) =>
        this._store.Read(d => d.OwedDeliveries.Where(o => string.Equals(o.RecipientLogin, login, StringComparison.OrdinalIgnoreCase)).ToList());

    /// <summary>Removes a completed owed delivery.</summary>
    /// <param name="deliveryId">Delivery id.</param>
    public void RemoveOwedDelivery(string deliveryId)
    {
        this._store.Mutate(d =>
        {
            var removed = d.OwedDeliveries.RemoveAll(o => string.Equals(o.DeliveryId, deliveryId, StringComparison.Ordinal));
            return (removed > 0, true);
        });
    }

    /// <summary>Looks up reputation for <paramref name="login"/>.</summary>
    /// <param name="login">Account login.</param>
    /// <returns>Reputation snapshot.</returns>
    public EscrowReputationEntry GetReputation(string login) =>
        this._store.Read(d => d.Reputations.TryGetValue(login, out var rep) ? rep : new EscrowReputationEntry { LoginName = login });

    internal static void ApplyReputationLocked(EscrowStoreData data, string login, int completedDelta, int cancelledDelta, int disputesLostDelta = 0)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        if (!data.Reputations.TryGetValue(login, out var rep))
        {
            rep = new EscrowReputationEntry { LoginName = login };
            data.Reputations[login] = rep;
        }

        rep.Completed += completedDelta;
        rep.Cancelled += cancelledDelta;
        rep.DisputesLost += disputesLostDelta;
        rep.Score = Math.Max(0, (rep.Completed * 10) - (rep.Cancelled * 2) - (rep.DisputesLost * 25));
    }

    internal static void RecordCooldownAndDeliverToBuyerLocked(
        EscrowConfiguration cfg,
        EscrowStoreData data,
        EscrowContract contract,
        string buyerLogin,
        DateTime utcNow)
    {
        DateTime? cooldownUntil = cfg.PostEscrowTradeCooldownHours > 0
            ? utcNow.AddHours(cfg.PostEscrowTradeCooldownHours)
            : null;
        if (cooldownUntil is { } until)
        {
            foreach (var it in contract.Items)
            {
                data.ItemTradeCooldowns.Add(new EscrowTradeCooldownEntry
                {
                    LoginName = buyerLogin,
                    Group = it.Group,
                    Number = it.Number,
                    Level = it.Level,
                    LockedUntilUtc = until,
                });
            }

            data.ItemTradeCooldowns.RemoveAll(c => c.LockedUntilUtc <= utcNow.AddDays(-1));
        }

        data.OwedDeliveries.Add(new EscrowOwedDelivery
        {
            ContractId = contract.ContractId,
            RecipientLogin = buyerLogin,
            ReasonEn = $"Escrow contract {contract.ContractId} purchase",
            ReasonVi = $"Nhận vật phẩm hợp đồng ký gửi {contract.ContractId}",
            Items = contract.Items.ToList(),
            Zen = contract.EscrowedZen,
            Jewels = contract.EscrowedJewels.ToList(),
            CreatedAtUtc = utcNow,
            ItemTradeCooldownUntilUtc = cooldownUntil,
        });
    }

    private static void ExpireSingleLocked(EscrowStoreData data, EscrowContract contract, DateTime utcNow)
    {
        contract.Status = EscrowContractStatus.Expired;
        contract.ClosedAtUtc = utcNow;
        var bondRefund = contract.DirectVnd is { SellerBondCoins: > 0, BondReleased: false, BondForfeited: false } dv ? dv.SellerBondCoins : 0;
        if (bondRefund > 0 && contract.DirectVnd is not null)
        {
            contract.DirectVnd.BondReleased = true;
        }

        data.OwedDeliveries.Add(new EscrowOwedDelivery
        {
            ContractId = contract.ContractId,
            RecipientLogin = contract.SellerLogin,
            ReasonEn = $"Expired escrow {contract.ContractId} return",
            ReasonVi = $"Hoàn trả hàng hết hạn hợp đồng {contract.ContractId}",
            Items = contract.Items.ToList(),
            Zen = contract.EscrowedZen,
            Jewels = contract.EscrowedJewels.ToList(),
            CoinCurrency = contract.DirectVnd?.SellerBondCurrency ?? EscrowCurrency.WCoinC,
            Coins = bondRefund,
            CreatedAtUtc = utcNow,
        });
    }

    private static void UpdateReputationLocked(EscrowStoreData data, string login, int completedDelta, int cancelledDelta) =>
        ApplyReputationLocked(data, login, completedDelta, cancelledDelta, 0);
}
