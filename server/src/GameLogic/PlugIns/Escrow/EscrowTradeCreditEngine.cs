// <copyright file="EscrowTradeCreditEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Pure atomic rule &amp; double-entry accounting engine for Trade Credit currency (ROADMAP 16.6, OFF by default):
/// fixed-rate VND top-up, cash-sale escrow spend/proceeds with 72h maturity hold, 1-way coin conversion,
/// verified-once bank payout requests (proceeds only), daily circulating vs ledger reconciliation with variance alarm,
/// and CSV accounting export.
/// </summary>
public sealed class EscrowTradeCreditEngine
{
    private const int MaxLedgerEntries = 5000;
    private readonly EscrowConfiguration _cfg;
    private readonly EscrowStore _store;

    /// <summary>Initializes a new instance of the <see cref="EscrowTradeCreditEngine"/> class.</summary>
    /// <param name="configuration">Active escrow configuration.</param>
    /// <param name="store">Persistent atomic escrow store.</param>
    public EscrowTradeCreditEngine(EscrowConfiguration configuration, EscrowStore store)
    {
        this._cfg = configuration ?? new EscrowConfiguration();
        this._store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Returns whether Trade Credit is enabled AND legal compliance is acknowledged.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <returns><see langword="true"/> only when enabled and acknowledged.</returns>
    public static bool IsCompliantAndEnabled(EscrowConfiguration cfg) =>
        cfg is { Enabled: true, TradeCreditEnabled: true, TradeCreditComplianceAcknowledged: true };

    /// <summary>Computes cash-out fee in Trade Credit units and resulting VND payout.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="grossUnits">Gross cash-out request units.</param>
    /// <returns>Fee units, net units, and net VND amount.</returns>
    public static (long FeeUnits, long NetUnits, long NetVnd) ComputeCashOutFee(EscrowConfiguration cfg, long grossUnits)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (grossUnits <= 0)
        {
            return (0, 0, 0);
        }

        long fee = 0;
        if (cfg.TradeCreditCashOutFeePercent > 0 || cfg.TradeCreditCashOutMinFeeUnits > 0)
        {
            var pct = Math.Clamp(cfg.TradeCreditCashOutFeePercent, 0.0, 25.0);
            fee = (long)Math.Round(grossUnits * (pct / 100.0), MidpointRounding.AwayFromZero);
            if (fee < cfg.TradeCreditCashOutMinFeeUnits)
            {
                fee = cfg.TradeCreditCashOutMinFeeUnits;
            }

            fee = Math.Clamp(fee, 0, grossUnits);
        }

        var net = grossUnits - fee;
        var netVnd = net * Math.Max(1, cfg.TradeCreditVndPerUnit);
        return (fee, net, netVnd);
    }

    /// <summary>Gets an account's current Trade Credit snapshot.</summary>
    /// <param name="login">Account login name.</param>
    /// <returns>Cloned snapshot or fresh zeroed account state.</returns>
    public TradeCreditAccountState GetAccount(string login) =>
        this._store.Read(d => d.TradeCreditAccounts.TryGetValue(login, out var a)
            ? a
            : new TradeCreditAccountState { LoginName = login });

    /// <summary>Binds a verified seller bank account once for Trade Credit cash-outs.</summary>
    /// <param name="login">Account login.</param>
    /// <param name="bankBin">6-digit NAPAS BIN.</param>
    /// <param name="bankName">Bank name.</param>
    /// <param name="bankAccountNumber">Account number.</param>
    /// <param name="bankAccountName">Account holder name.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="adminOverride">True allows admin overwrite if previously locked.</param>
    /// <returns>Error code (<c>Ok</c>, <c>BankAlreadyLocked</c>, <c>MissingBankDetails</c>).</returns>
    public string RegisterVerifiedBankOnce(
        string login,
        string bankBin,
        string bankName,
        string bankAccountNumber,
        string bankAccountName,
        DateTime utcNow,
        bool adminOverride = false)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return "TradeCreditDisabledOrUnacknowledged";
        }

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(bankBin) || string.IsNullOrWhiteSpace(bankAccountNumber) || string.IsNullOrWhiteSpace(bankAccountName))
        {
            return "MissingBankDetails";
        }

        return this._store.Mutate(data =>
        {
            var acct = GetOrCreateAccountLocked(data, login);
            if (acct.BankLocked && !adminOverride)
            {
                return (false, "BankAlreadyLocked");
            }

            acct.VerifiedBankBin = bankBin.Trim();
            acct.VerifiedBankName = string.IsNullOrWhiteSpace(bankName) ? "Bank" : bankName.Trim();
            acct.VerifiedBankAccountNumber = bankAccountNumber.Trim();
            acct.VerifiedBankAccountName = bankAccountName.Trim();
            acct.BankLocked = true;
            acct.BankVerifiedAtUtc = utcNow;
            return (true, "Ok");
        });
    }

    /// <summary>Credits non-cashoutable top-up Trade Credit from verified VND at fixed conversion rate.</summary>
    /// <param name="login">Target account login.</param>
    /// <param name="units">Number of Trade Credit units to issue.</param>
    /// <param name="reference">Payment reference (unique if possible).</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and new spendable balance.</returns>
    public (string Error, long NewBalance) TopUpCredit(string login, long units, string reference, DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("TradeCreditDisabledOrUnacknowledged", 0);
        }

        if (string.IsNullOrWhiteSpace(login) || units <= 0)
        {
            return ("InvalidAmount", 0);
        }

        return this._store.Mutate(data =>
        {
            var acct = GetOrCreateAccountLocked(data, login);
            acct.GeneralUnits += units;
            AddLedgerLocked(
                data,
                login,
                "TopUp",
                units,
                units * Math.Max(1, this._cfg.TradeCreditVndPerUnit),
                reference,
                "Fixed-rate VND top-up to Trade Credit (non-cashoutable)",
                utcNow);
            return (true, ("Ok", acct.SpendableUnits));
        });
    }

    /// <summary>
    /// Atomically accepts a <see cref="EscrowCurrency.TradeCredit"/> escrow contract inside ONE lock:
    /// debits buyer Trade Credit, delivers escrowed items/Zen/jewels via owed deliveries,
    /// credits seller a new matured-after-hold <see cref="TradeCreditProceedLot"/>,
    /// and books any fee into both Trade Credit double-entry ledger and host fee ledger.
    /// </summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="buyerCharacter">Buyer character name.</param>
    /// <param name="contractId">Target contract id.</param>
    /// <param name="confirmHighValue">High-value confirmation flag.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and completed contract.</returns>
    public (string Error, EscrowContract? Contract) AcceptTradeCreditEscrow(
        string buyerLogin,
        string buyerCharacter,
        string contractId,
        bool confirmHighValue,
        DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("TradeCreditDisabledOrUnacknowledged", null);
        }

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

            if (contract.PriceCurrency != EscrowCurrency.TradeCredit)
            {
                return (false, ("InvalidCurrency", (EscrowContract?)null));
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
                contract.Status = EscrowContractStatus.Expired;
                contract.ClosedAtUtc = utcNow;
                data.OwedDeliveries.Add(new EscrowOwedDelivery
                {
                    ContractId = contract.ContractId,
                    RecipientLogin = contract.SellerLogin,
                    ReasonEn = $"Expired escrow {contract.ContractId} return",
                    ReasonVi = $"Hoàn trả hàng hết hạn hợp đồng {contract.ContractId}",
                    Items = contract.Items.ToList(),
                    Zen = contract.EscrowedZen,
                    Jewels = contract.EscrowedJewels.ToList(),
                    CreatedAtUtc = utcNow,
                });
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

            if (this._cfg.HighValueThresholdCoins > 0
                && contract.PriceAmount >= this._cfg.HighValueThresholdCoins
                && !confirmHighValue)
            {
                return (false, ("NeedsHighValueConfirmation", (EscrowContract?)null));
            }

            var buyer = GetOrCreateAccountLocked(data, buyerLogin);
            if (buyer.SpendableUnits < contract.BuyerTotalDebit)
            {
                return (false, ("PaymentFailed", (EscrowContract?)null));
            }

            DebitSpendableLocked(buyer, contract.BuyerTotalDebit);

            var seller = GetOrCreateAccountLocked(data, contract.SellerLogin);
            var holdHours = Math.Max(0, this._cfg.TradeCreditCashOutHoldHours);
            if (contract.NetSellerProceeds > 0)
            {
                seller.ProceedLots.Add(new TradeCreditProceedLot
                {
                    ContractId = contract.ContractId,
                    OriginalUnits = contract.NetSellerProceeds,
                    RemainingUnits = contract.NetSellerProceeds,
                    EarnedAtUtc = utcNow,
                    EligibleAtUtc = utcNow.AddHours(holdHours),
                });
            }

            var rate = Math.Max(1, this._cfg.TradeCreditVndPerUnit);
            AddLedgerLocked(data, buyerLogin, "CashSaleSpend", -contract.BuyerTotalDebit, -contract.BuyerTotalDebit * rate, contract.ContractId, "Paid Trade Credit escrow contract", utcNow);
            AddLedgerLocked(data, contract.SellerLogin, "CashSaleProceeds", contract.NetSellerProceeds, contract.NetSellerProceeds * rate, contract.ContractId, $"Proceeds locked {holdHours}h before cash-out eligibility", utcNow);
            if (contract.FeeAmount > 0)
            {
                AddLedgerLocked(data, "HOST", "Fee", contract.FeeAmount, contract.FeeAmount * rate, contract.ContractId, "Escrow Trade Credit fee", utcNow);
                data.FeeLedger.Insert(0, new EscrowFeeLedgerEntry
                {
                    TimestampUtc = utcNow,
                    TimestampSydney = EscrowEngine.ToSydneyText(utcNow),
                    ContractId = contract.ContractId,
                    Mode = "TradeCredit",
                    Currency = EscrowCurrency.TradeCredit.ToString(),
                    FeeAmount = contract.FeeAmount,
                });
                if (data.FeeLedger.Count > 500)
                {
                    data.FeeLedger = data.FeeLedger.Take(500).ToList();
                }
            }

            contract.Status = EscrowContractStatus.Completed;
            contract.AcceptedByLogin = buyerLogin;
            contract.AcceptedByCharacter = buyerCharacter;
            contract.ClosedAtUtc = utcNow;
            EscrowEngine.RecordCooldownAndDeliverToBuyerLocked(this._cfg, data, contract, buyerLogin, utcNow);
            EscrowEngine.ApplyReputationLocked(data, contract.SellerLogin, completedDelta: 1, cancelledDelta: 0);
            EscrowEngine.ApplyReputationLocked(data, buyerLogin, completedDelta: 1, cancelledDelta: 0);

            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>One-way converts Trade Credit into normal <c>WCoinC</c> queued via owed delivery.</summary>
    /// <param name="login">Account login.</param>
    /// <param name="units">Trade Credit units to convert.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and WCoinC granted.</returns>
    public (string Error, long CoinsGranted) ConvertToCoins(string login, long units, DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("TradeCreditDisabledOrUnacknowledged", 0);
        }

        if (this._cfg.TradeCreditToCoinRate <= 0)
        {
            return ("ConversionDisabled", 0);
        }

        if (string.IsNullOrWhiteSpace(login) || units <= 0)
        {
            return ("InvalidAmount", 0);
        }

        return this._store.Mutate(data =>
        {
            var acct = GetOrCreateAccountLocked(data, login);
            if (acct.SpendableUnits < units)
            {
                return (false, ("InsufficientTradeCredit", 0L));
            }

            DebitSpendableLocked(acct, units);
            var coins = units * this._cfg.TradeCreditToCoinRate;
            AddLedgerLocked(data, login, "ConvertToCoins", -units, -units * Math.Max(1, this._cfg.TradeCreditVndPerUnit), $"CONV-{Guid.NewGuid():N}"[..12], $"1-way convert {units} TC to {coins} WCoinC", utcNow);
            data.OwedDeliveries.Add(new EscrowOwedDelivery
            {
                ContractId = "TC-CONV",
                RecipientLogin = login,
                ReasonEn = $"Trade Credit -> {coins} WCoinC conversion",
                ReasonVi = $"Đổi {units} Tín dụng -> {coins} WCoinC",
                CoinCurrency = EscrowCurrency.WCoinC,
                Coins = coins,
                CreatedAtUtc = utcNow,
            });
            return (true, ("Ok", coins));
        });
    }

    /// <summary>
    /// Queues a cash-out payout strictly from matured <see cref="TradeCreditProceedLot"/> balances,
    /// enforcing holding period (e.g. 72h), daily &amp; monthly cash-out caps, verified one-time bank binding,
    /// and cash-out fee (% + min).
    /// </summary>
    /// <param name="login">Seller login.</param>
    /// <param name="grossUnits">Gross proceeds units to cash out.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and queued cash-out request.</returns>
    public (string Error, TradeCreditCashOutRequest? Request) RequestCashOut(string login, long grossUnits, DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("TradeCreditDisabledOrUnacknowledged", null);
        }

        if (string.IsNullOrWhiteSpace(login) || grossUnits <= 0)
        {
            return ("InvalidAmount", null);
        }

        var (fee, net, netVnd) = ComputeCashOutFee(this._cfg, grossUnits);
        if (net <= 0)
        {
            return ("AmountBelowMinFee", null);
        }

        var tz = HnmoStateStore.ResolveTimeZone(null);
        var sydneyToday = HnmoStateStore.ToTimeZone(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);

        return this._store.Mutate(data =>
        {
            var acct = GetOrCreateAccountLocked(data, login);
            if (!acct.BankLocked || string.IsNullOrWhiteSpace(acct.VerifiedBankAccountNumber))
            {
                return (false, ("VerifiedBankRequired", (TradeCreditCashOutRequest?)null));
            }

            if (acct.EligibleCashOutUnits(utcNow) < grossUnits)
            {
                return (false, ("NoMaturedProceeds", (TradeCreditCashOutRequest?)null));
            }

            var myRequests = data.TradeCreditCashOuts.Where(r => string.Equals(r.AccountLogin, login, StringComparison.OrdinalIgnoreCase)).ToList();
            if (this._cfg.TradeCreditCashOutDailyLimitUnits > 0)
            {
                var usedToday = myRequests
                    .Where(r => HnmoStateStore.ToTimeZone(DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc), tz).Date == sydneyToday.Date)
                    .Sum(r => r.GrossUnits);
                if (usedToday + grossUnits > this._cfg.TradeCreditCashOutDailyLimitUnits)
                {
                    return (false, ("DailyCashOutLimitExceeded", (TradeCreditCashOutRequest?)null));
                }
            }

            if (this._cfg.TradeCreditCashOutMonthlyLimitUnits > 0)
            {
                var usedMonth = myRequests
                    .Where(r =>
                    {
                        var d = HnmoStateStore.ToTimeZone(DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc), tz);
                        return d.Year == sydneyToday.Year && d.Month == sydneyToday.Month;
                    })
                    .Sum(r => r.GrossUnits);
                if (usedMonth + grossUnits > this._cfg.TradeCreditCashOutMonthlyLimitUnits)
                {
                    return (false, ("MonthlyCashOutLimitExceeded", (TradeCreditCashOutRequest?)null));
                }
            }

            // Deduct strictly from matured proceed lots FIFO!
            var rem = grossUnits;
            foreach (var lot in acct.ProceedLots.Where(l => l.RemainingUnits > 0 && utcNow >= l.EligibleAtUtc))
            {
                var take = Math.Min(rem, lot.RemainingUnits);
                lot.RemainingUnits -= take;
                rem -= take;
                if (rem == 0)
                {
                    break;
                }
            }

            acct.LockedCashOutUnits += net;
            var req = new TradeCreditCashOutRequest
            {
                CreatedAtUtc = utcNow,
                CreatedAtSydney = EscrowEngine.ToSydneyText(utcNow),
                AccountLogin = login,
                BankBin = acct.VerifiedBankBin,
                BankName = acct.VerifiedBankName,
                BankAccountNumber = acct.VerifiedBankAccountNumber,
                BankAccountName = acct.VerifiedBankAccountName,
                GrossUnits = grossUnits,
                FeeUnits = fee,
                NetUnits = net,
                NetVnd = netVnd,
                Status = "Queued",
            };
            data.TradeCreditCashOuts.Insert(0, req);

            var rate = Math.Max(1, this._cfg.TradeCreditVndPerUnit);
            AddLedgerLocked(data, login, "CashOutRequest", -net, -net * rate, req.RequestId, $"Queued cash-out {netVnd:N0} VND (fee {fee})", utcNow);
            if (fee > 0)
            {
                AddLedgerLocked(data, "HOST", "Fee", fee, fee * rate, req.RequestId, "Cash-out fee", utcNow);
                data.FeeLedger.Insert(0, new EscrowFeeLedgerEntry
                {
                    TimestampUtc = utcNow,
                    TimestampSydney = EscrowEngine.ToSydneyText(utcNow),
                    ContractId = req.RequestId,
                    Mode = "TradeCreditCashOut",
                    Currency = EscrowCurrency.TradeCredit.ToString(),
                    FeeAmount = fee,
                });
            }

            return (true, ("Ok", (TradeCreditCashOutRequest?)req));
        });
    }

    /// <summary>Admin marks a queued Trade Credit cash-out request paid.</summary>
    /// <param name="requestId">Cash-out request identifier.</param>
    /// <param name="payoutRef">Bank payout transaction reference.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and updated request.</returns>
    public (string Error, TradeCreditCashOutRequest? Request) MarkCashOutPaid(string requestId, string payoutRef, DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var req = data.TradeCreditCashOuts.FirstOrDefault(r => string.Equals(r.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
            if (req is null)
            {
                return (false, ("NotFound", (TradeCreditCashOutRequest?)null));
            }

            if (req.Status != "Queued")
            {
                return (false, ("AlreadyProcessed", (TradeCreditCashOutRequest?)req));
            }

            var acct = GetOrCreateAccountLocked(data, req.AccountLogin);
            acct.LockedCashOutUnits = Math.Max(0, acct.LockedCashOutUnits - req.NetUnits);
            req.Status = "Paid";
            req.PaidAtUtc = utcNow;
            req.PayoutReference = string.IsNullOrWhiteSpace(payoutRef) ? $"TCPAID-{utcNow:yyyyMMdd}" : payoutRef.Trim();
            AddLedgerLocked(data, req.AccountLogin, "CashOutPaid", req.NetUnits, req.NetVnd, req.RequestId, $"Cash-out paid ({req.PayoutReference})", utcNow);
            return (true, ("Ok", (TradeCreditCashOutRequest?)req));
        });
    }

    /// <summary>Reconciles actual circulating balances + pending cash-outs against double-entry ledger totals.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Computed and saved reconciliation report.</returns>
    public TradeCreditReconciliationReport Reconcile(DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var activeBalances = data.TradeCreditAccounts.Values.Sum(a => a.SpendableUnits);
            var pendingLocked = data.TradeCreditAccounts.Values.Sum(a => Math.Max(0, a.LockedCashOutUnits));
            var actual = activeBalances + pendingLocked;

            var topUps = data.TradeCreditLedger.Where(l => l.Kind == "TopUp").Sum(l => l.Units);
            var conversions = data.TradeCreditLedger.Where(l => l.Kind == "ConvertToCoins").Sum(l => Math.Abs(l.Units));
            var cashOutsPaid = data.TradeCreditLedger.Where(l => l.Kind == "CashOutPaid").Sum(l => l.Units);
            var fees = data.TradeCreditLedger.Where(l => l.Kind == "Fee").Sum(l => l.Units);
            var expected = topUps - conversions - cashOutsPaid - fees;

            var report = new TradeCreditReconciliationReport
            {
                CheckedAtUtc = utcNow,
                CheckedAtSydney = EscrowEngine.ToSydneyText(utcNow),
                ActualCirculatingUnits = actual,
                ExpectedCirculatingUnits = expected,
                TotalTopUpUnits = topUps,
                TotalConvertedToCoinsUnits = conversions,
                TotalCashOutPaidUnits = cashOutsPaid,
                TotalFeeUnits = fees,
                PendingCashOutUnits = pendingLocked,
            };
            data.LastTradeCreditReconciliation = report;
            return (true, report);
        });
    }

    /// <summary>Exports full double-entry Trade Credit ledger plus reconciliation footer as RFC-4180 CSV.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>UTF-8 CSV text.</returns>
    public string ExportCsv(DateTime utcNow)
    {
        var report = this.Reconcile(utcNow);
        var entries = this._store.Read(d => d.TradeCreditLedger.ToList());
        var sb = new StringBuilder();
        sb.AppendLine("EntryId,TimestampUtc,TimestampSydney,AccountLogin,Kind,Units,VndAmount,Reference,Note");
        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(
                ',',
                CsvCell(e.EntryId),
                CsvCell(e.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                CsvCell(e.TimestampSydney),
                CsvCell(e.AccountLogin),
                CsvCell(e.Kind),
                e.Units.ToString(CultureInfo.InvariantCulture),
                e.VndAmount.ToString(CultureInfo.InvariantCulture),
                CsvCell(e.Reference),
                CsvCell(e.Note)));
        }

        sb.AppendLine(FormattableString.Invariant($"RECONCILIATION,{report.CheckedAtUtc:O},{CsvCell(report.CheckedAtSydney)},ALL,Balanced={report.IsBalanced},{report.ActualCirculatingUnits},{report.ExpectedCirculatingUnits},Variance={report.VarianceUnits},Pending={report.PendingCashOutUnits}"));
        return sb.ToString();
    }

    private static TradeCreditAccountState GetOrCreateAccountLocked(EscrowStoreData data, string login)
    {
        if (!data.TradeCreditAccounts.TryGetValue(login, out var acct))
        {
            acct = new TradeCreditAccountState { LoginName = login };
            data.TradeCreditAccounts[login] = acct;
        }

        return acct;
    }

    private static void DebitSpendableLocked(TradeCreditAccountState acct, long amount)
    {
        var remaining = amount;
        if (acct.GeneralUnits > 0)
        {
            var takeGen = Math.Min(remaining, acct.GeneralUnits);
            acct.GeneralUnits -= takeGen;
            remaining -= takeGen;
        }

        if (remaining > 0)
        {
            foreach (var lot in acct.ProceedLots.Where(l => l.RemainingUnits > 0))
            {
                var take = Math.Min(remaining, lot.RemainingUnits);
                lot.RemainingUnits -= take;
                remaining -= take;
                if (remaining == 0)
                {
                    break;
                }
            }
        }
    }

    private static void AddLedgerLocked(
        EscrowStoreData data,
        string login,
        string kind,
        long units,
        long vndAmount,
        string reference,
        string note,
        DateTime utcNow)
    {
        data.TradeCreditLedger.Insert(0, new TradeCreditLedgerEntry
        {
            TimestampUtc = utcNow,
            TimestampSydney = EscrowEngine.ToSydneyText(utcNow),
            AccountLogin = login,
            Kind = kind,
            Units = units,
            VndAmount = vndAmount,
            Reference = reference,
            Note = note,
        });
        if (data.TradeCreditLedger.Count > MaxLedgerEntries)
        {
            data.TradeCreditLedger = data.TradeCreditLedger.Take(MaxLedgerEntries).ToList();
        }
    }

    private static string CsvCell(string? value)
    {
        var v = value ?? string.Empty;
        return v.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
