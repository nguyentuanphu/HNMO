// <copyright file="EscrowHostVndEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Pure atomic rule engine for Host-mediated VND real-money escrow contracts (ROADMAP 16.2):
/// buyer pays via Host VietQR (<c>HNMO-XXXX</c>), Casso/SePay webhook or admin confirmation
/// releases goods atomically, records host VND fee in <see cref="EscrowFeeLedgerEntry"/>,
/// queues net seller payout within N days, and supports a post-delivery dispute window.
/// </summary>
public sealed partial class EscrowHostVndEngine
{
    /// <summary>Mode discriminator for Host-mediated VND contracts.</summary>
    public const string ModeName = "HostVnd";

    private const int MaxFeeLedgerEntries = 500;
    private readonly EscrowConfiguration _cfg;
    private readonly EscrowStore _store;

    /// <summary>Initializes a new instance of the <see cref="EscrowHostVndEngine"/> class.</summary>
    /// <param name="configuration">Active escrow configuration.</param>
    /// <param name="store">Persistent atomic escrow store.</param>
    public EscrowHostVndEngine(EscrowConfiguration configuration, EscrowStore store)
    {
        this._cfg = configuration ?? new EscrowConfiguration();
        this._store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Returns whether Host-mediated VND mode is both enabled and legally acknowledged.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <returns><see langword="true"/> only when enabled and compliance-acknowledged.</returns>
    public static bool IsCompliantAndEnabled(EscrowConfiguration cfg) =>
        cfg is { Enabled: true, HostVndEnabled: true, HostVndComplianceAcknowledged: true };

    /// <summary>Computes host VND fee and net seller payout for <paramref name="priceVnd"/>.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="priceVnd">Gross VND sale amount.</param>
    /// <returns>Host fee and net seller VND payout.</returns>
    public static (long HostFeeVnd, long NetSellerPayoutVnd) ComputeHostFeeVnd(EscrowConfiguration cfg, long priceVnd)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (priceVnd <= 0 || cfg.HostVndFeePercent <= 0)
        {
            return (0, Math.Max(0, priceVnd));
        }

        var pct = Math.Clamp(cfg.HostVndFeePercent, 0.0, 25.0);
        var fee = (long)Math.Round(priceVnd * (pct / 100.0), MidpointRounding.AwayFromZero);
        if (fee < cfg.HostVndMinFeeVnd)
        {
            fee = cfg.HostVndMinFeeVnd;
        }

        if (cfg.HostVndMaxFeeVnd > 0 && fee > cfg.HostVndMaxFeeVnd)
        {
            fee = cfg.HostVndMaxFeeVnd;
        }

        fee = Math.Clamp(fee, 0, priceVnd);
        return (fee, priceVnd - fee);
    }

    /// <summary>Extracts a normalized <c>HNMO-XXXX</c> reference token from bank transfer memo text.</summary>
    /// <param name="description">Bank transfer memo/description.</param>
    /// <returns>Normalized <c>HNMO-XXXX</c> code or empty string.</returns>
    public static string ExtractHnmoReference(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = HnmoRefRegex().Match(description);
        if (!match.Success)
        {
            return string.Empty;
        }

        var suffix = match.Groups["code"].Value.ToUpperInvariant();
        return $"HNMO-{suffix}";
    }

    /// <summary>Builds a privacy-safe projection hiding seller payout bank details from non-seller/non-admin callers.</summary>
    /// <param name="contract">Source contract.</param>
    /// <param name="viewerLogin">Viewer login.</param>
    /// <param name="isAdmin">Whether viewer is an admin.</param>
    /// <returns>Privacy-safe projection.</returns>
    public static EscrowContract ProjectForViewer(EscrowContract contract, string viewerLogin, bool isAdmin = false)
    {
        if (contract.HostVnd is null)
        {
            return contract;
        }

        var isSeller = !string.IsNullOrWhiteSpace(viewerLogin)
            && string.Equals(contract.SellerLogin, viewerLogin, StringComparison.OrdinalIgnoreCase);
        var isBuyer = !string.IsNullOrWhiteSpace(viewerLogin)
            && string.Equals(contract.AcceptedByLogin, viewerLogin, StringComparison.OrdinalIgnoreCase);

        var showSellerBank = isAdmin || isSeller;
        var showHostPayment = isAdmin || isSeller || isBuyer;

        return new EscrowContract
        {
            ContractId = contract.ContractId,
            Mode = contract.Mode,
            Status = contract.Status,
            CreatedAtUtc = contract.CreatedAtUtc,
            CreatedAtSydney = contract.CreatedAtSydney,
            ExpiresAtUtc = contract.ExpiresAtUtc,
            ExpiresAtSydney = contract.ExpiresAtSydney,
            ClosedAtUtc = contract.ClosedAtUtc,
            SellerLogin = contract.SellerLogin,
            SellerCharacter = contract.SellerCharacter,
            TargetBuyer = contract.TargetBuyer,
            AcceptedByLogin = contract.AcceptedByLogin,
            AcceptedByCharacter = contract.AcceptedByCharacter,
            Items = contract.Items,
            EscrowedZen = contract.EscrowedZen,
            EscrowedJewels = contract.EscrowedJewels,
            PriceCurrency = contract.PriceCurrency,
            PriceAmount = contract.PriceAmount,
            FeePayer = contract.FeePayer,
            FeeAmount = contract.FeeAmount,
            BuyerTotalDebit = contract.BuyerTotalDebit,
            NetSellerProceeds = contract.NetSellerProceeds,
            Note = contract.Note,
            FrozenByAdmin = contract.FrozenByAdmin,
            AdminReason = contract.AdminReason,
            HostVnd = new EscrowHostVndDetails
            {
                SellerBankBin = showSellerBank ? contract.HostVnd.SellerBankBin : string.Empty,
                SellerBankName = contract.HostVnd.SellerBankName,
                SellerBankAccountNumber = showSellerBank ? contract.HostVnd.SellerBankAccountNumber : string.Empty,
                SellerBankAccountName = showSellerBank ? contract.HostVnd.SellerBankAccountName : string.Empty,
                HostBankBin = showHostPayment ? contract.HostVnd.HostBankBin : string.Empty,
                HostBankName = contract.HostVnd.HostBankName,
                HostBankAccountNumber = showHostPayment ? contract.HostVnd.HostBankAccountNumber : string.Empty,
                HostBankAccountName = showHostPayment ? contract.HostVnd.HostBankAccountName : string.Empty,
                PaymentReference = showHostPayment ? contract.HostVnd.PaymentReference : string.Empty,
                ReservedAtUtc = contract.HostVnd.ReservedAtUtc,
                PaymentDeadlineUtc = contract.HostVnd.PaymentDeadlineUtc,
                PaymentDeadlineSydney = contract.HostVnd.PaymentDeadlineSydney,
                PaymentTxId = contract.HostVnd.PaymentTxId,
                PaidAtUtc = contract.HostVnd.PaidAtUtc,
                DisputeWindowEndsUtc = contract.HostVnd.DisputeWindowEndsUtc,
                PayoutDueAtUtc = contract.HostVnd.PayoutDueAtUtc,
                PayoutDueAtSydney = contract.HostVnd.PayoutDueAtSydney,
                PayoutStatus = contract.HostVnd.PayoutStatus,
                HostFeeVnd = contract.HostVnd.HostFeeVnd,
                NetSellerPayoutVnd = contract.HostVnd.NetSellerPayoutVnd,
                PayoutPaidAtUtc = contract.HostVnd.PayoutPaidAtUtc,
                PayoutReference = contract.HostVnd.PayoutReference,
                DisputeReason = contract.HostVnd.DisputeReason,
                DisputeResolution = contract.HostVnd.DisputeResolution,
            },
        };
    }

    /// <summary>Creates a Host-mediated VND escrow contract atomically.</summary>
    /// <param name="sellerLogin">Seller account login.</param>
    /// <param name="sellerCharacter">Seller character name.</param>
    /// <param name="targetBuyer">Optional target buyer.</param>
    /// <param name="items">Escrowed items.</param>
    /// <param name="escrowedZen">Escrowed Zen.</param>
    /// <param name="escrowedJewels">Escrowed jewels.</param>
    /// <param name="priceVnd">Asking VND price.</param>
    /// <param name="sellerBankBin">Seller payout NAPAS BIN.</param>
    /// <param name="sellerBankName">Seller bank label.</param>
    /// <param name="sellerBankAccountNumber">Seller payout account number.</param>
    /// <param name="sellerBankAccountName">Seller payout holder name.</param>
    /// <param name="deadlineHours">Listing expiry hours.</param>
    /// <param name="confirmHighValue">High-value confirmation flag.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and created contract.</returns>
    public (string Error, EscrowContract? Contract) CreateHostVndContract(
        string sellerLogin,
        string sellerCharacter,
        string? targetBuyer,
        IReadOnlyList<EscrowItemSnapshot>? items,
        long escrowedZen,
        IReadOnlyList<EscrowJewelLine>? escrowedJewels,
        long priceVnd,
        string sellerBankBin,
        string sellerBankName,
        string sellerBankAccountNumber,
        string sellerBankAccountName,
        int deadlineHours,
        bool confirmHighValue,
        DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("HostVndDisabledOrUnacknowledged", null);
        }

        if (string.IsNullOrWhiteSpace(sellerLogin))
        {
            return ("NotAuthenticated", null);
        }

        if (priceVnd < 1000)
        {
            return ("InvalidVndPrice", null);
        }

        if (string.IsNullOrWhiteSpace(sellerBankBin) || string.IsNullOrWhiteSpace(sellerBankAccountNumber) || string.IsNullOrWhiteSpace(sellerBankAccountName))
        {
            return ("MissingBankDetails", null);
        }

        if (string.IsNullOrWhiteSpace(this._cfg.HostVndBankAccountNumber))
        {
            return ("HostBankNotConfigured", null);
        }

        var itemList = items?.ToList() ?? [];
        var jewelList = escrowedJewels?.Where(j => j.Quantity > 0).ToList() ?? [];
        if (itemList.Count == 0 && escrowedZen <= 0 && jewelList.Count == 0)
        {
            return ("EmptyGoods", null);
        }

        foreach (var it in itemList)
        {
            if (EscrowEngine.IsBannedItem(this._cfg, it.Group, it.Number, it.NameEn))
            {
                return ("BannedItem", null);
            }
        }

        var trimmedBuyer = (targetBuyer ?? string.Empty).Trim();
        if (string.Equals(trimmedBuyer, sellerLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmedBuyer, sellerCharacter, StringComparison.OrdinalIgnoreCase))
        {
            return ("SelfTargetNotAllowed", null);
        }

        if (priceVnd >= 1_000_000 && !confirmHighValue)
        {
            return ("NeedsHighValueConfirmation", null);
        }

        var (fee, netPayout) = ComputeHostFeeVnd(this._cfg, priceVnd);
        var effectiveDeadline = Math.Clamp(
            deadlineHours <= 0 ? this._cfg.DefaultDeadlineHours : deadlineHours,
            1,
            Math.Max(1, this._cfg.MaxDeadlineHours));
        var expiresAt = utcNow.AddHours(effectiveDeadline);

        return this._store.Mutate(data =>
        {
            var openCount = data.Contracts.Count(c =>
                c.Status is EscrowContractStatus.Open or EscrowContractStatus.ReservedForPayment
                && string.Equals(c.SellerLogin, sellerLogin, StringComparison.OrdinalIgnoreCase));
            if (openCount >= Math.Max(1, this._cfg.MaxOpenContractsPerAccount))
            {
                return (false, ("TooManyOpenContracts", (EscrowContract?)null));
            }

            var contract = new EscrowContract
            {
                ContractId = "HV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                Mode = ModeName,
                Status = EscrowContractStatus.Open,
                CreatedAtUtc = utcNow,
                CreatedAtSydney = EscrowEngine.ToSydneyText(utcNow),
                ExpiresAtUtc = expiresAt,
                ExpiresAtSydney = EscrowEngine.ToSydneyText(expiresAt),
                SellerLogin = sellerLogin,
                SellerCharacter = sellerCharacter,
                TargetBuyer = trimmedBuyer,
                Items = itemList,
                EscrowedZen = Math.Max(0, escrowedZen),
                EscrowedJewels = jewelList,
                PriceCurrency = EscrowCurrency.Zen,
                PriceAmount = priceVnd,
                FeePayer = EscrowFeePayer.Seller,
                FeeAmount = fee,
                BuyerTotalDebit = priceVnd,
                NetSellerProceeds = netPayout,
                HostVnd = new EscrowHostVndDetails
                {
                    SellerBankBin = sellerBankBin.Trim(),
                    SellerBankName = string.IsNullOrWhiteSpace(sellerBankName) ? "Bank" : sellerBankName.Trim(),
                    SellerBankAccountNumber = sellerBankAccountNumber.Trim(),
                    SellerBankAccountName = sellerBankAccountName.Trim(),
                    HostBankBin = this._cfg.HostVndBankBin.Trim(),
                    HostBankName = this._cfg.HostVndBankName.Trim(),
                    HostBankAccountNumber = this._cfg.HostVndBankAccountNumber.Trim(),
                    HostBankAccountName = this._cfg.HostVndBankAccountName.Trim(),
                    PaymentReference = EscrowDirectVndEngine.GeneratePaymentReference(),
                    HostFeeVnd = fee,
                    NetSellerPayoutVnd = netPayout,
                },
            };

            data.Contracts.Insert(0, contract);
            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>Reserves an open Host-mediated VND contract for buyer payment to Host VietQR.</summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="buyerCharacter">Buyer character name.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and reserved contract.</returns>
    public (string Error, EscrowContract? Contract) ReserveForHostPayment(
        string buyerLogin,
        string buyerCharacter,
        string contractId,
        DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("HostVndDisabledOrUnacknowledged", null);
        }

        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.HostVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (c.FrozenByAdmin || c.Status == EscrowContractStatus.Frozen)
            {
                return (false, ("Frozen", (EscrowContract?)null));
            }

            if (c.Status != EscrowContractStatus.Open)
            {
                return (false, ("AlreadyClosed", (EscrowContract?)null));
            }

            if (utcNow >= c.ExpiresAtUtc)
            {
                c.Status = EscrowContractStatus.Expired;
                c.ClosedAtUtc = utcNow;
                QueueSellerReturnLocked(data, c, utcNow, "Expired Host VND escrow return", "Hoàn trả hợp đồng VND hệ thống hết hạn");
                return (true, ("Expired", (EscrowContract?)null));
            }

            if (string.Equals(c.SellerLogin, buyerLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("OwnContract", (EscrowContract?)null));
            }

            if (!string.IsNullOrWhiteSpace(c.TargetBuyer)
                && !string.Equals(c.TargetBuyer, buyerLogin, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(c.TargetBuyer, buyerCharacter, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("WrongBuyer", (EscrowContract?)null));
            }

            var deadline = utcNow.AddMinutes(Math.Max(5, this._cfg.HostVndPaymentWindowMinutes));
            c.Status = EscrowContractStatus.ReservedForPayment;
            c.AcceptedByLogin = buyerLogin;
            c.AcceptedByCharacter = buyerCharacter;
            c.HostVnd.ReservedAtUtc = utcNow;
            c.HostVnd.PaymentDeadlineUtc = deadline;
            c.HostVnd.PaymentDeadlineSydney = EscrowEngine.ToSydneyText(deadline);
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>
    /// Matches an incoming host bank transfer (from Casso/SePay webhook or admin manual confirm)
    /// against reserved <c>HostVnd</c> contracts by reference and amount, releases escrowed items to buyer,
    /// records the host VND fee in the ledger, and queues net seller VND payout within N days.
    /// </summary>
    /// <param name="descriptionOrReference">Bank transfer memo or explicit reference code.</param>
    /// <param name="amountVnd">Incoming VND amount.</param>
    /// <param name="externalTxId">Unique bank/provider transaction id for idempotency.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code (<c>Ok</c>, <c>NoMatch</c>, <c>AlreadyProcessed</c>, <c>Underpaid</c>) and matched contract.</returns>
    public (string Error, EscrowContract? Contract) ConfirmHostPaymentReceived(
        string? descriptionOrReference,
        long amountVnd,
        string? externalTxId,
        DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("HostVndDisabledOrUnacknowledged", null);
        }

        var reference = ExtractHnmoReference(descriptionOrReference);
        if (string.IsNullOrEmpty(reference))
        {
            return ("NoMatch", null);
        }

        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x =>
                x.HostVnd is not null
                && string.Equals(x.HostVnd.PaymentReference, reference, StringComparison.OrdinalIgnoreCase));
            if (c?.HostVnd is null)
            {
                return (false, ("NoMatch", (EscrowContract?)null));
            }

            if (c.Status == EscrowContractStatus.Completed && c.HostVnd.PaidAtUtc is not null)
            {
                return (false, ("AlreadyProcessed", (EscrowContract?)c));
            }

            if (c.Status != EscrowContractStatus.ReservedForPayment || string.IsNullOrWhiteSpace(c.AcceptedByLogin))
            {
                return (false, ("InvalidState", (EscrowContract?)c));
            }

            if (amountVnd < c.PriceAmount)
            {
                return (false, ("Underpaid", (EscrowContract?)c));
            }

            if (!string.IsNullOrWhiteSpace(externalTxId)
                && data.Contracts.Any(other => string.Equals(other.HostVnd?.PaymentTxId, externalTxId.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return (false, ("AlreadyProcessed", (EscrowContract?)c));
            }

            var hv = c.HostVnd;
            c.Status = EscrowContractStatus.Completed;
            c.ClosedAtUtc = utcNow;
            hv.PaidAtUtc = utcNow;
            hv.PaymentTxId = string.IsNullOrWhiteSpace(externalTxId) ? $"HOST-{Guid.NewGuid():N}"[..16] : externalTxId.Trim();
            hv.DisputeWindowEndsUtc = utcNow.AddHours(Math.Max(1, this._cfg.HostVndDisputeWindowHours));
            var payoutDue = utcNow.AddDays(Math.Max(0, this._cfg.HostVndPayoutDays));
            hv.PayoutDueAtUtc = payoutDue;
            hv.PayoutDueAtSydney = EscrowEngine.ToSydneyText(payoutDue);
            hv.PayoutStatus = "Queued";

            EscrowEngine.RecordCooldownAndDeliverToBuyerLocked(this._cfg, data, c, c.AcceptedByLogin, utcNow);

            if (hv.HostFeeVnd > 0)
            {
                data.FeeLedger.Insert(0, new EscrowFeeLedgerEntry
                {
                    TimestampUtc = utcNow,
                    TimestampSydney = EscrowEngine.ToSydneyText(utcNow),
                    ContractId = c.ContractId,
                    Mode = ModeName,
                    Currency = "VND",
                    FeeAmount = hv.HostFeeVnd,
                });
                if (data.FeeLedger.Count > MaxFeeLedgerEntries)
                {
                    data.FeeLedger = data.FeeLedger.Take(MaxFeeLedgerEntries).ToList();
                }
            }

            EscrowEngine.ApplyReputationLocked(data, c.SellerLogin, completedDelta: 1, cancelledDelta: 0);
            EscrowEngine.ApplyReputationLocked(data, c.AcceptedByLogin, completedDelta: 1, cancelledDelta: 0);
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Opens a dispute while buyer transfer is pending or within the post-delivery payout hold window.</summary>
    /// <param name="actorLogin">Buyer or seller login.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="reason">Dispute explanation.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and affected contract.</returns>
    public (string Error, EscrowContract? Contract) OpenHostDispute(string actorLogin, string contractId, string reason, DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.HostVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            var isParticipant = string.Equals(c.SellerLogin, actorLogin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.AcceptedByLogin, actorLogin, StringComparison.OrdinalIgnoreCase);
            if (!isParticipant)
            {
                return (false, ("NotParticipant", (EscrowContract?)null));
            }

            var hv = c.HostVnd;
            var inWindow = c.Status == EscrowContractStatus.ReservedForPayment
                || (c.Status == EscrowContractStatus.Completed
                    && hv.PayoutStatus == "Queued"
                    && (hv.DisputeWindowEndsUtc is null || utcNow <= hv.DisputeWindowEndsUtc));
            if (!inWindow)
            {
                return (false, ("DisputeWindowClosed", (EscrowContract?)null));
            }

            c.Status = EscrowContractStatus.Disputed;
            hv.PayoutStatus = "Disputed";
            hv.DisputeReason = string.IsNullOrWhiteSpace(reason) ? "Participant opened dispute" : reason.Trim();
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Admin marks a queued Host-mediated seller payout as transferred.</summary>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="payoutRef">Bank payout reference code.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and updated contract.</returns>
    public (string Error, EscrowContract? Contract) MarkPayoutPaid(string contractId, string payoutRef, DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.HostVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (c.HostVnd.PayoutStatus != "Queued" || c.Status == EscrowContractStatus.Disputed)
            {
                return (false, ("NotQueuedForPayout", (EscrowContract?)null));
            }

            c.HostVnd.PayoutStatus = "Paid";
            c.HostVnd.PayoutPaidAtUtc = utcNow;
            c.HostVnd.PayoutReference = string.IsNullOrWhiteSpace(payoutRef) ? $"PAID-{utcNow:yyyyMMdd}" : payoutRef.Trim();
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Admin resolves a Host-mediated dispute: either clear seller payout or cancel payout for buyer bank refund.</summary>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="releasePayoutToSeller">True to resume payout to seller; false to mark buyer refund owed.</param>
    /// <param name="adminNote">Resolution note.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code, resolved contract, and losing account login.</returns>
    public (string Error, EscrowContract? Contract, string LosingLogin) ResolveHostDispute(
        string contractId,
        bool releasePayoutToSeller,
        string adminNote,
        DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.HostVnd is null || c.Status != EscrowContractStatus.Disputed)
            {
                return (false, ("NotDisputed", (EscrowContract?)null, string.Empty));
            }

            var hv = c.HostVnd;
            c.AdminReason = adminNote;
            hv.DisputeResolution = string.IsNullOrWhiteSpace(adminNote)
                ? (releasePayoutToSeller ? "Payout cleared for seller" : "Cancelled payout — refund VND to buyer")
                : adminNote.Trim();

            string loser;
            if (releasePayoutToSeller)
            {
                c.Status = EscrowContractStatus.Completed;
                hv.PayoutStatus = "Queued";
                loser = c.AcceptedByLogin;
            }
            else
            {
                c.Status = EscrowContractStatus.Cancelled;
                hv.PayoutStatus = "Refunded";
                if (hv.PaidAtUtc is null)
                {
                    QueueSellerReturnLocked(data, c, utcNow, "Host VND dispute closed before payment", "Hoàn hàng tranh chấp VND hệ thống");
                }

                loser = c.SellerLogin;
            }

            if (!string.IsNullOrWhiteSpace(loser))
            {
                EscrowEngine.ApplyReputationLocked(data, loser, completedDelta: 0, cancelledDelta: 0, disputesLostDelta: 1);
            }

            return (true, ("Ok", (EscrowContract?)c, loser));
        });
    }

    /// <summary>Releases unpaid expired Host VND reservations back to Open or Expired.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Number of updated contracts.</returns>
    public int ProcessHostTimeouts(DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var changed = 0;
            foreach (var c in data.Contracts.Where(x => x.HostVnd is not null && x.Status == EscrowContractStatus.ReservedForPayment))
            {
                if (c.HostVnd!.PaymentDeadlineUtc is { } dl && utcNow >= dl)
                {
                    if (utcNow >= c.ExpiresAtUtc)
                    {
                        c.Status = EscrowContractStatus.Expired;
                        c.ClosedAtUtc = utcNow;
                        QueueSellerReturnLocked(data, c, utcNow, "Expired Host VND escrow return", "Hoàn trả hợp đồng VND hệ thống hết hạn");
                    }
                    else
                    {
                        c.Status = EscrowContractStatus.Open;
                        c.AcceptedByLogin = string.Empty;
                        c.AcceptedByCharacter = string.Empty;
                        c.HostVnd.ReservedAtUtc = null;
                        c.HostVnd.PaymentDeadlineUtc = null;
                        c.HostVnd.PaymentDeadlineSydney = string.Empty;
                        c.HostVnd.PaymentReference = EscrowDirectVndEngine.GeneratePaymentReference();
                    }

                    changed++;
                }
            }

            return (changed > 0, changed);
        });
    }

    private static void QueueSellerReturnLocked(EscrowStoreData data, EscrowContract contract, DateTime utcNow, string en, string vi)
    {
        data.OwedDeliveries.Add(new EscrowOwedDelivery
        {
            ContractId = contract.ContractId,
            RecipientLogin = contract.SellerLogin,
            ReasonEn = en,
            ReasonVi = vi,
            Items = contract.Items.ToList(),
            Zen = contract.EscrowedZen,
            Jewels = contract.EscrowedJewels.ToList(),
            CreatedAtUtc = utcNow,
        });
    }

    [GeneratedRegex(@"HNMO-?(?<code>[A-Z0-9]{4,16})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HnmoRefRegex();
}
