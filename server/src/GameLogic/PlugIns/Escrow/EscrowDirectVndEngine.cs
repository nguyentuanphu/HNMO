// <copyright file="EscrowDirectVndEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Pure atomic rule engine for Direct P2P VND real-money escrow contracts (ROADMAP 16.4):
/// Vietnamese regulatory compliance gate, privacy-redacted bank details until accepted,
/// unique <c>HNMO-XXXX</c> reference, 30-min payment window extendable once, optional seller coin bond,
/// receipt image magic-byte/size verification, per-seller HMAC-SHA256 webhook confirmation with replay defense,
/// dispute auto-escalation, and bond/reputation forfeiture on dispute loss.
/// </summary>
public sealed class EscrowDirectVndEngine
{
    /// <summary>Mode discriminator for Direct P2P VND contracts.</summary>
    public const string ModeName = "DirectVnd";

    private const int MaxStoredNonces = 1000;
    private const long MaxWebhookSkewSeconds = 300;
    private const int MaxFeeLedgerEntries = 500;

    private readonly EscrowConfiguration _cfg;
    private readonly EscrowStore _store;

    /// <summary>Initializes a new instance of the <see cref="EscrowDirectVndEngine"/> class.</summary>
    /// <param name="configuration">Active escrow configuration.</param>
    /// <param name="store">Persistent atomic escrow store.</param>
    public EscrowDirectVndEngine(EscrowConfiguration configuration, EscrowStore store)
    {
        this._cfg = configuration ?? new EscrowConfiguration();
        this._store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Returns whether Direct P2P VND mode is both enabled and legally acknowledged by admin.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <returns><see langword="true"/> only when enabled and compliance-acknowledged.</returns>
    public static bool IsCompliantAndEnabled(EscrowConfiguration cfg) =>
        cfg is { Enabled: true, DirectVndEnabled: true, DirectVndComplianceAcknowledged: true };

    /// <summary>Computes coin service fee breakdown for a Direct P2P VND trade.</summary>
    /// <param name="cfg">Active configuration.</param>
    /// <param name="priceVnd">Contract VND price.</param>
    /// <returns>Service fee breakdown in <see cref="EscrowConfiguration.DirectVndServiceFeeCurrency"/>.</returns>
    public static EscrowFeeBreakdown ComputeServiceFeeCoins(EscrowConfiguration cfg, long priceVnd)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        var pctFee = cfg.DirectVndServiceFeePercent > 0 && priceVnd > 0
            ? (long)Math.Round((priceVnd / 1000.0) * (cfg.DirectVndServiceFeePercent / 100.0), MidpointRounding.AwayFromZero)
            : 0;
        var total = Math.Max(0, cfg.DirectVndServiceFeeCoins) + Math.Max(0, pctFee);
        if (total <= 0)
        {
            return new EscrowFeeBreakdown(0, 0, 0, 0, 0);
        }

        return cfg.DirectVndFeePayer switch
        {
            EscrowFeePayer.Buyer => new EscrowFeeBreakdown(total, 0, total, total, 0),
            EscrowFeePayer.Split => new EscrowFeeBreakdown(total, total - (total / 2), total / 2, total / 2, 0),
            _ => new EscrowFeeBreakdown(total, total, 0, 0, 0),
        };
    }

    /// <summary>Generates a unique <c>HNMO-XXXX</c> payment memo reference.</summary>
    /// <returns>Upper-case unique reference code.</returns>
    public static string GeneratePaymentReference()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return $"HNMO-{Convert.ToHexString(bytes)}";
    }

    /// <summary>Computes hex lowercase HMAC-SHA256 signature over <c>{timestamp}.{nonce}.{rawBody}</c>.</summary>
    /// <param name="secret">Shared seller webhook secret.</param>
    /// <param name="timestampUnix">Unix timestamp seconds.</param>
    /// <param name="nonce">Unique nonce string.</param>
    /// <param name="rawBody">Exact raw UTF-8 request body.</param>
    /// <returns>Lowercase hex HMAC-SHA256 digest.</returns>
    public static string ComputeWebhookSignature(string secret, long timestampUnix, string nonce, string rawBody)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var msg = Encoding.UTF8.GetBytes($"{timestampUnix.ToString(CultureInfo.InvariantCulture)}.{nonce}.{rawBody}");
        var mac = HMACSHA256.HashData(key, msg);
        return Convert.ToHexStringLower(mac);
    }

    /// <summary>Validates base64 image MIME &amp; magic header against PNG, JPEG, or WEBP and enforces size cap.</summary>
    /// <param name="mime">Declared MIME type.</param>
    /// <param name="base64">Raw base64 payload (without data URL prefix).</param>
    /// <param name="maxBytes">Configured max decoded byte length.</param>
    /// <returns>Validated status.</returns>
    public static bool IsValidReceiptImage(string? mime, string? base64, int maxBytes)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(mime))
        {
            return false;
        }

        var normalizedMime = mime.Trim().ToLowerInvariant();
        if (normalizedMime is not ("image/png" or "image/jpeg" or "image/jpg" or "image/webp"))
        {
            return false;
        }

        var trimmed = base64.Trim();
        var commaIdx = trimmed.IndexOf(',');
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx >= 0)
        {
            trimmed = trimmed[(commaIdx + 1)..];
        }

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(trimmed);
        }
        catch (FormatException)
        {
            return false;
        }

        if (raw.Length < 12 || raw.Length > Math.Max(1024, maxBytes))
        {
            return false;
        }

        var isPng = raw[0] == 0x89 && raw[1] == 0x50 && raw[2] == 0x4E && raw[3] == 0x47;
        var isJpeg = raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF;
        var isWebp = raw[0] == 0x52 && raw[1] == 0x49 && raw[2] == 0x46 && raw[3] == 0x46
            && raw[8] == 0x57 && raw[9] == 0x45 && raw[10] == 0x42 && raw[11] == 0x50;

        return normalizedMime switch
        {
            "image/png" => isPng,
            "image/webp" => isWebp,
            _ => isJpeg,
        };
    }

    /// <summary>Builds a privacy-safe projection of <paramref name="contract"/> for a caller.</summary>
    /// <param name="contract">Source contract.</param>
    /// <param name="viewerLogin">Viewing player login (empty = anonymous website).</param>
    /// <param name="isAdmin">Admin panel callers can always inspect bank &amp; dispute details.</param>
    /// <returns>Projected DTO object preserving redaction rules.</returns>
    public static EscrowContract ProjectForViewer(EscrowContract contract, string viewerLogin, bool isAdmin = false)
    {
        if (contract.DirectVnd is null)
        {
            return contract;
        }

        var isSeller = !string.IsNullOrWhiteSpace(viewerLogin)
            && string.Equals(contract.SellerLogin, viewerLogin, StringComparison.OrdinalIgnoreCase);
        var isAcceptedBuyer = !string.IsNullOrWhiteSpace(viewerLogin)
            && string.Equals(contract.AcceptedByLogin, viewerLogin, StringComparison.OrdinalIgnoreCase)
            && contract.Status is EscrowContractStatus.ReservedForPayment or EscrowContractStatus.BuyerMarkedPaid or EscrowContractStatus.Disputed or EscrowContractStatus.Completed;

        var showBankDetails = isAdmin || isSeller || isAcceptedBuyer;
        var showReceipt = isAdmin || isSeller || isAcceptedBuyer;
        var showSecret = isAdmin || isSeller;

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
            DirectVnd = new EscrowDirectVndDetails
            {
                BankBin = showBankDetails ? contract.DirectVnd.BankBin : string.Empty,
                BankName = showBankDetails ? contract.DirectVnd.BankName : contract.DirectVnd.BankName,
                BankAccountNumber = showBankDetails ? contract.DirectVnd.BankAccountNumber : string.Empty,
                BankAccountName = showBankDetails ? contract.DirectVnd.BankAccountName : string.Empty,
                PaymentReference = showBankDetails ? contract.DirectVnd.PaymentReference : string.Empty,
                WebhookSecret = showSecret ? contract.DirectVnd.WebhookSecret : string.Empty,
                WebhookVerified = contract.DirectVnd.WebhookVerified,
                WebhookNote = contract.DirectVnd.WebhookNote,
                SellerBondCurrency = contract.DirectVnd.SellerBondCurrency,
                SellerBondCoins = contract.DirectVnd.SellerBondCoins,
                BondReleased = contract.DirectVnd.BondReleased,
                BondForfeited = contract.DirectVnd.BondForfeited,
                ServiceFeeCurrency = contract.DirectVnd.ServiceFeeCurrency,
                ServiceFeeCoins = contract.DirectVnd.ServiceFeeCoins,
                ReservedAtUtc = contract.DirectVnd.ReservedAtUtc,
                PaymentDeadlineUtc = contract.DirectVnd.PaymentDeadlineUtc,
                PaymentDeadlineSydney = contract.DirectVnd.PaymentDeadlineSydney,
                ExtendedOnce = contract.DirectVnd.ExtendedOnce,
                BuyerMarkedPaidAtUtc = contract.DirectVnd.BuyerMarkedPaidAtUtc,
                BuyerNote = contract.DirectVnd.BuyerNote,
                ReceiptMime = showReceipt ? contract.DirectVnd.ReceiptMime : string.Empty,
                ReceiptBase64 = showReceipt ? contract.DirectVnd.ReceiptBase64 : string.Empty,
                DisputeOpenedAtUtc = contract.DirectVnd.DisputeOpenedAtUtc,
                DisputeOpenedBy = contract.DirectVnd.DisputeOpenedBy,
                DisputeReason = contract.DirectVnd.DisputeReason,
                DisputeResolution = contract.DirectVnd.DisputeResolution,
            },
        };
    }

    /// <summary>Creates a Direct P2P VND escrow contract atomically.</summary>
    /// <param name="sellerLogin">Seller account login.</param>
    /// <param name="sellerCharacter">Seller character name.</param>
    /// <param name="targetBuyer">Optional buyer login/character filter.</param>
    /// <param name="items">Escrowed items.</param>
    /// <param name="escrowedZen">Escrowed Zen.</param>
    /// <param name="escrowedJewels">Escrowed jewels.</param>
    /// <param name="priceVnd">VND sale price.</param>
    /// <param name="bankBin">Seller 6-digit NAPAS BIN.</param>
    /// <param name="bankName">Seller bank label.</param>
    /// <param name="bankAccountNumber">Seller bank account number.</param>
    /// <param name="bankAccountName">Seller account holder name.</param>
    /// <param name="sellerBondCoins">Optional seller security bond in coins.</param>
    /// <param name="webhookSecret">Optional per-seller webhook secret.</param>
    /// <param name="deadlineHours">Listing expiry hours.</param>
    /// <param name="confirmHighValue">High-value confirmation flag.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code and created contract.</returns>
    public (string Error, EscrowContract? Contract) CreateDirectVndContract(
        string sellerLogin,
        string sellerCharacter,
        string? targetBuyer,
        IReadOnlyList<EscrowItemSnapshot>? items,
        long escrowedZen,
        IReadOnlyList<EscrowJewelLine>? escrowedJewels,
        long priceVnd,
        string bankBin,
        string bankName,
        string bankAccountNumber,
        string bankAccountName,
        long sellerBondCoins,
        string? webhookSecret,
        int deadlineHours,
        bool confirmHighValue,
        DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("DirectVndDisabledOrUnacknowledged", null);
        }

        if (string.IsNullOrWhiteSpace(sellerLogin))
        {
            return ("NotAuthenticated", null);
        }

        if (priceVnd < 1000)
        {
            return ("InvalidVndPrice", null);
        }

        if (string.IsNullOrWhiteSpace(bankBin) || string.IsNullOrWhiteSpace(bankAccountNumber) || string.IsNullOrWhiteSpace(bankAccountName))
        {
            return ("MissingBankDetails", null);
        }

        if (sellerBondCoins < 0 || (this._cfg.DirectVndMaxSellerBondCoins >= 0 && sellerBondCoins > this._cfg.DirectVndMaxSellerBondCoins))
        {
            return ("InvalidSellerBond", null);
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

        var serviceFee = ComputeServiceFeeCoins(this._cfg, priceVnd);
        var effectiveDeadline = Math.Clamp(
            deadlineHours <= 0 ? this._cfg.DefaultDeadlineHours : deadlineHours,
            1,
            Math.Max(1, this._cfg.MaxDeadlineHours));
        var expiresAt = utcNow.AddHours(effectiveDeadline);

        return this._store.Mutate(data =>
        {
            var openCount = data.Contracts.Count(c =>
                c.Status is EscrowContractStatus.Open or EscrowContractStatus.ReservedForPayment or EscrowContractStatus.BuyerMarkedPaid
                && string.Equals(c.SellerLogin, sellerLogin, StringComparison.OrdinalIgnoreCase));
            if (openCount >= Math.Max(1, this._cfg.MaxOpenContractsPerAccount))
            {
                return (false, ("TooManyOpenContracts", (EscrowContract?)null));
            }

            var contract = new EscrowContract
            {
                ContractId = "DV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
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
                PriceCurrency = this._cfg.DirectVndServiceFeeCurrency,
                PriceAmount = priceVnd,
                FeePayer = this._cfg.DirectVndFeePayer,
                FeeAmount = serviceFee.TotalFee,
                BuyerTotalDebit = serviceFee.BuyerFeePaid,
                NetSellerProceeds = priceVnd,
                DirectVnd = new EscrowDirectVndDetails
                {
                    BankBin = bankBin.Trim(),
                    BankName = string.IsNullOrWhiteSpace(bankName) ? "Bank" : bankName.Trim(),
                    BankAccountNumber = bankAccountNumber.Trim(),
                    BankAccountName = bankAccountName.Trim(),
                    PaymentReference = GeneratePaymentReference(),
                    WebhookSecret = (webhookSecret ?? string.Empty).Trim(),
                    SellerBondCurrency = this._cfg.DirectVndServiceFeeCurrency,
                    SellerBondCoins = sellerBondCoins,
                    ServiceFeeCurrency = this._cfg.DirectVndServiceFeeCurrency,
                    ServiceFeeCoins = serviceFee.TotalFee,
                },
            };

            if (serviceFee.SellerFeePaid > 0)
            {
                data.FeeLedger.Insert(0, new EscrowFeeLedgerEntry
                {
                    TimestampUtc = utcNow,
                    TimestampSydney = EscrowEngine.ToSydneyText(utcNow),
                    ContractId = contract.ContractId,
                    Mode = ModeName,
                    Currency = contract.DirectVnd.ServiceFeeCurrency.ToString(),
                    FeeAmount = serviceFee.SellerFeePaid,
                });
                TrimLedgerLocked(data);
            }

            data.Contracts.Insert(0, contract);
            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>Atomically reserves an open Direct P2P VND contract for buyer payment (revealing bank QR).</summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="buyerCharacter">Buyer character name.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <param name="buyerFeeDebiter">Optional debiter for buyer share of service fee coins.</param>
    /// <returns>Status code and updated contract.</returns>
    public (string Error, EscrowContract? Contract) ReserveForPayment(
        string buyerLogin,
        string buyerCharacter,
        string contractId,
        DateTime utcNow,
        Func<long, bool>? buyerFeeDebiter = null)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("DirectVndDisabledOrUnacknowledged", null);
        }

        return this._store.Mutate(data =>
        {
            var contract = data.Contracts.FirstOrDefault(c => string.Equals(c.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (contract is null || contract.DirectVnd is null)
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
                ExpireDirectSingleLocked(data, contract, utcNow);
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

            var buyerFee = contract.BuyerTotalDebit;
            if (buyerFee > 0 && buyerFeeDebiter is not null && !buyerFeeDebiter(buyerFee))
            {
                return (false, ("PaymentFailed", (EscrowContract?)null));
            }

            var windowMins = Math.Max(5, this._cfg.DirectVndPaymentWindowMinutes);
            var deadlineUtc = utcNow.AddMinutes(windowMins);
            contract.Status = EscrowContractStatus.ReservedForPayment;
            contract.AcceptedByLogin = buyerLogin;
            contract.AcceptedByCharacter = buyerCharacter;
            contract.DirectVnd.ReservedAtUtc = utcNow;
            contract.DirectVnd.PaymentDeadlineUtc = deadlineUtc;
            contract.DirectVnd.PaymentDeadlineSydney = EscrowEngine.ToSydneyText(deadlineUtc);

            if (buyerFee > 0)
            {
                data.FeeLedger.Insert(0, new EscrowFeeLedgerEntry
                {
                    TimestampUtc = utcNow,
                    TimestampSydney = EscrowEngine.ToSydneyText(utcNow),
                    ContractId = contract.ContractId,
                    Mode = ModeName,
                    Currency = contract.DirectVnd.ServiceFeeCurrency.ToString(),
                    FeeAmount = buyerFee,
                });
                TrimLedgerLocked(data);
            }

            return (true, ("Ok", (EscrowContract?)contract));
        });
    }

    /// <summary>Extends the buyer's payment window ONCE while reserved.</summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and updated contract.</returns>
    public (string Error, EscrowContract? Contract) ExtendPaymentWindowOnce(string buyerLogin, string contractId, DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.DirectVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (!string.Equals(c.AcceptedByLogin, buyerLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("WrongBuyer", (EscrowContract?)null));
            }

            if (c.Status != EscrowContractStatus.ReservedForPayment)
            {
                return (false, ("NotReserved", (EscrowContract?)null));
            }

            if (c.DirectVnd.ExtendedOnce)
            {
                return (false, ("AlreadyExtended", (EscrowContract?)null));
            }

            if (c.DirectVnd.PaymentDeadlineUtc is { } d && utcNow >= d)
            {
                return (false, ("PaymentWindowExpired", (EscrowContract?)null));
            }

            var nextDeadline = (c.DirectVnd.PaymentDeadlineUtc ?? utcNow).AddMinutes(Math.Max(5, this._cfg.DirectVndExtendMinutes));
            c.DirectVnd.PaymentDeadlineUtc = nextDeadline;
            c.DirectVnd.PaymentDeadlineSydney = EscrowEngine.ToSydneyText(nextDeadline);
            c.DirectVnd.ExtendedOnce = true;
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Buyer marks payment sent with optional validated receipt image.</summary>
    /// <param name="buyerLogin">Buyer account login.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="receiptMime">Optional image MIME type.</param>
    /// <param name="receiptBase64">Optional base64 image content.</param>
    /// <param name="note">Optional buyer transfer note.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and updated contract.</returns>
    public (string Error, EscrowContract? Contract) MarkBuyerPaid(
        string buyerLogin,
        string contractId,
        string? receiptMime,
        string? receiptBase64,
        string? note,
        DateTime utcNow)
    {
        if (!IsValidReceiptImage(receiptMime, receiptBase64, this._cfg.DirectVndMaxReceiptBytes))
        {
            return ("InvalidReceiptImage", null);
        }

        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.DirectVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (!string.Equals(c.AcceptedByLogin, buyerLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("WrongBuyer", (EscrowContract?)null));
            }

            if (c.Status != EscrowContractStatus.ReservedForPayment)
            {
                return (false, ("NotReserved", (EscrowContract?)null));
            }

            c.Status = EscrowContractStatus.BuyerMarkedPaid;
            c.DirectVnd.BuyerMarkedPaidAtUtc = utcNow;
            c.DirectVnd.BuyerNote = (note ?? string.Empty).Trim();
            c.DirectVnd.ReceiptMime = string.IsNullOrWhiteSpace(receiptBase64) ? string.Empty : (receiptMime ?? string.Empty).Trim();
            c.DirectVnd.ReceiptBase64 = string.IsNullOrWhiteSpace(receiptBase64) ? string.Empty : StripDataPrefix(receiptBase64);
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Seller manually confirms VND receipt and releases goods atomically to buyer + returns bond.</summary>
    /// <param name="sellerLogin">Seller account login.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and completed contract.</returns>
    public (string Error, EscrowContract? Contract) SellerConfirmMoneyReceived(string sellerLogin, string contractId, DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.DirectVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (!string.Equals(c.SellerLogin, sellerLogin, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("NotSeller", (EscrowContract?)null));
            }

            if (c.Status is not (EscrowContractStatus.ReservedForPayment or EscrowContractStatus.BuyerMarkedPaid))
            {
                return (false, ("InvalidState", (EscrowContract?)null));
            }

            CompleteDirectSwapLocked(this._cfg, data, c, utcNow, viaWebhook: false, webhookNote: string.Empty);
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>
    /// Verifies a per-seller signed HMAC-SHA256 webhook notification (timestamp + nonce replay defense + amount + reference)
    /// and atomically releases the escrowed goods on valid confirmation.
    /// </summary>
    /// <param name="contractId">Target contract identifier.</param>
    /// <param name="rawBody">Exact raw request body.</param>
    /// <param name="signatureHex">Hex HMAC-SHA256 signature header.</param>
    /// <param name="timestampUnix">Unix seconds header.</param>
    /// <param name="nonce">Unique replay-protection nonce header.</param>
    /// <param name="amountVnd">Reported VND transfer amount.</param>
    /// <param name="reference">Reported transfer memo/reference.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and affected contract.</returns>
    public (string Error, EscrowContract? Contract) VerifyAndCompleteWebhook(
        string contractId,
        string rawBody,
        string signatureHex,
        long timestampUnix,
        string nonce,
        long amountVnd,
        string reference,
        DateTime utcNow)
    {
        if (!IsCompliantAndEnabled(this._cfg))
        {
            return ("DirectVndDisabledOrUnacknowledged", null);
        }

        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(signatureHex))
        {
            return ("MissingSignature", null);
        }

        var nowUnix = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
        if (Math.Abs(nowUnix - timestampUnix) > MaxWebhookSkewSeconds)
        {
            return ("StaleTimestamp", null);
        }

        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.DirectVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            if (string.IsNullOrWhiteSpace(c.DirectVnd.WebhookSecret))
            {
                return (false, ("WebhookNotConfigured", (EscrowContract?)null));
            }

            if (c.Status is not (EscrowContractStatus.ReservedForPayment or EscrowContractStatus.BuyerMarkedPaid))
            {
                return (false, ("InvalidState", (EscrowContract?)null));
            }

            if (data.UsedWebhookNonces.Any(n =>
                string.Equals(n.ContractId, c.ContractId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(n.Nonce, nonce, StringComparison.Ordinal)))
            {
                return (false, ("ReplayDetected", (EscrowContract?)null));
            }

            var expectedHex = ComputeWebhookSignature(c.DirectVnd.WebhookSecret, timestampUnix, nonce, rawBody);
            var sigNormalized = signatureHex.Trim().ToLowerInvariant();
            if (sigNormalized.StartsWith("sha256=", StringComparison.Ordinal))
            {
                sigNormalized = sigNormalized[7..];
            }

            var a = Encoding.UTF8.GetBytes(expectedHex);
            var b = Encoding.UTF8.GetBytes(sigNormalized);
            if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
            {
                return (false, ("InvalidSignature", (EscrowContract?)null));
            }

            if (amountVnd != c.PriceAmount
                || !string.Equals(reference.Trim(), c.DirectVnd.PaymentReference, StringComparison.OrdinalIgnoreCase))
            {
                return (false, ("AmountOrReferenceMismatch", (EscrowContract?)null));
            }

            data.UsedWebhookNonces.Insert(0, new EscrowWebhookNonceEntry
            {
                ContractId = c.ContractId,
                Nonce = nonce,
                TimestampUnix = timestampUnix,
                RecordedAtUtc = utcNow,
            });
            if (data.UsedWebhookNonces.Count > MaxStoredNonces)
            {
                data.UsedWebhookNonces = data.UsedWebhookNonces.Take(MaxStoredNonces).ToList();
            }

            CompleteDirectSwapLocked(this._cfg, data, c, utcNow, viaWebhook: true, webhookNote: $"Verified webhook nonce={nonce}");
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Moves a reserved/marked-paid contract into Disputed state for admin adjudication.</summary>
    /// <param name="actorLogin">Participant opening dispute.</param>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="reason">Dispute explanation.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Status code and disputed contract.</returns>
    public (string Error, EscrowContract? Contract) OpenDispute(string actorLogin, string contractId, string reason, DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.DirectVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null));
            }

            var isParticipant = string.Equals(c.SellerLogin, actorLogin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.AcceptedByLogin, actorLogin, StringComparison.OrdinalIgnoreCase);
            if (!isParticipant)
            {
                return (false, ("NotParticipant", (EscrowContract?)null));
            }

            if (c.Status is not (EscrowContractStatus.ReservedForPayment or EscrowContractStatus.BuyerMarkedPaid))
            {
                return (false, ("InvalidState", (EscrowContract?)null));
            }

            c.Status = EscrowContractStatus.Disputed;
            c.DirectVnd.DisputeOpenedAtUtc = utcNow;
            c.DirectVnd.DisputeOpenedBy = actorLogin;
            c.DirectVnd.DisputeReason = string.IsNullOrWhiteSpace(reason) ? "Manual dispute opened" : reason.Trim();
            return (true, ("Ok", (EscrowContract?)c));
        });
    }

    /// <summary>Processes expired unpaid reservations and auto-escalates overdue seller confirmations into Disputed.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Count of contracts transitioned.</returns>
    public int ProcessTimeouts(DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var changed = 0;
            foreach (var c in data.Contracts.Where(x => x.DirectVnd is not null))
            {
                var dv = c.DirectVnd!;
                if (c.Status == EscrowContractStatus.ReservedForPayment
                    && dv.PaymentDeadlineUtc is { } dl
                    && utcNow >= dl)
                {
                    // Unpaid window elapsed: release reservation cleanly back to Open (or Expired if outer deadline passed).
                    if (utcNow >= c.ExpiresAtUtc)
                    {
                        ExpireDirectSingleLocked(data, c, utcNow);
                    }
                    else
                    {
                        c.Status = EscrowContractStatus.Open;
                        c.AcceptedByLogin = string.Empty;
                        c.AcceptedByCharacter = string.Empty;
                        dv.ReservedAtUtc = null;
                        dv.PaymentDeadlineUtc = null;
                        dv.PaymentDeadlineSydney = string.Empty;
                        dv.ExtendedOnce = false;
                        dv.PaymentReference = GeneratePaymentReference();
                    }

                    changed++;
                }
                else if (c.Status == EscrowContractStatus.BuyerMarkedPaid
                    && dv.BuyerMarkedPaidAtUtc is { } paidAt
                    && utcNow >= paidAt.AddHours(Math.Max(1, this._cfg.DirectVndAutoDisputeHours)))
                {
                    c.Status = EscrowContractStatus.Disputed;
                    dv.DisputeOpenedAtUtc = utcNow;
                    dv.DisputeOpenedBy = "SYSTEM";
                    dv.DisputeReason = "Auto-disputed after seller confirmation timeout.";
                    changed++;
                }
            }

            return (changed > 0, changed);
        });
    }

    /// <summary>
    /// Admin dispute decision based on receipt &amp; bank evidence:
    /// awards goods to buyer (forfeiting seller bond) or returns goods + bond to seller, penalizing losing party reputation.
    /// </summary>
    /// <param name="contractId">Contract identifier.</param>
    /// <param name="awardToBuyer">True to release item to buyer; false to return to seller.</param>
    /// <param name="adminNote">Admin decision note.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Error code, resolved contract, and losing account login.</returns>
    public (string Error, EscrowContract? Contract, string LosingLogin) ResolveDispute(
        string contractId,
        bool awardToBuyer,
        string adminNote,
        DateTime utcNow)
    {
        return this._store.Mutate(data =>
        {
            var c = data.Contracts.FirstOrDefault(x => string.Equals(x.ContractId, contractId, StringComparison.OrdinalIgnoreCase));
            if (c?.DirectVnd is null)
            {
                return (false, ("NotFound", (EscrowContract?)null, string.Empty));
            }

            if (c.Status != EscrowContractStatus.Disputed)
            {
                return (false, ("NotDisputed", (EscrowContract?)null, string.Empty));
            }

            var dv = c.DirectVnd;
            c.ClosedAtUtc = utcNow;
            c.AdminReason = adminNote;
            dv.DisputeResolution = string.IsNullOrWhiteSpace(adminNote)
                ? (awardToBuyer ? "Awarded to buyer by admin" : "Returned to seller by admin")
                : adminNote.Trim();

            string loser;
            if (awardToBuyer && !string.IsNullOrWhiteSpace(c.AcceptedByLogin))
            {
                c.Status = EscrowContractStatus.Completed;
                EscrowEngine.RecordCooldownAndDeliverToBuyerLocked(this._cfg, data, c, c.AcceptedByLogin, utcNow);
                dv.BondForfeited = dv.SellerBondCoins > 0;
                loser = c.SellerLogin;
                EscrowEngine.ApplyReputationLocked(data, c.AcceptedByLogin, completedDelta: 1, cancelledDelta: 0, disputesLostDelta: 0);
                EscrowEngine.ApplyReputationLocked(data, c.SellerLogin, completedDelta: 0, cancelledDelta: 0, disputesLostDelta: 1);
            }
            else
            {
                c.Status = EscrowContractStatus.Cancelled;
                ExpireDirectSingleLocked(data, c, utcNow, "Returned after admin dispute ruling", "Hoàn hàng theo phán quyết tranh chấp");
                loser = c.AcceptedByLogin;
                EscrowEngine.ApplyReputationLocked(data, c.SellerLogin, completedDelta: 0, cancelledDelta: 0, disputesLostDelta: 0);
                if (!string.IsNullOrWhiteSpace(loser))
                {
                    EscrowEngine.ApplyReputationLocked(data, loser, completedDelta: 0, cancelledDelta: 0, disputesLostDelta: 1);
                }
            }

            return (true, ("Ok", (EscrowContract?)c, loser));
        });
    }

    private static string StripDataPrefix(string base64)
    {
        var t = base64.Trim();
        var comma = t.IndexOf(',');
        return t.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0 ? t[(comma + 1)..] : t;
    }

    private static void CompleteDirectSwapLocked(
        EscrowConfiguration cfg,
        EscrowStoreData data,
        EscrowContract contract,
        DateTime utcNow,
        bool viaWebhook,
        string webhookNote)
    {
        var dv = contract.DirectVnd!;
        contract.Status = EscrowContractStatus.Completed;
        contract.ClosedAtUtc = utcNow;
        dv.WebhookVerified = viaWebhook;
        if (!string.IsNullOrEmpty(webhookNote))
        {
            dv.WebhookNote = webhookNote;
        }

        EscrowEngine.RecordCooldownAndDeliverToBuyerLocked(cfg, data, contract, contract.AcceptedByLogin, utcNow);

        if (dv.SellerBondCoins > 0 && !dv.BondReleased && !dv.BondForfeited)
        {
            dv.BondReleased = true;
            data.OwedDeliveries.Add(new EscrowOwedDelivery
            {
                ContractId = contract.ContractId,
                RecipientLogin = contract.SellerLogin,
                ReasonEn = $"Direct VND {contract.ContractId} seller security bond refund",
                ReasonVi = $"Hoàn tiền cọc hợp đồng VND {contract.ContractId}",
                CoinCurrency = dv.SellerBondCurrency,
                Coins = dv.SellerBondCoins,
                CreatedAtUtc = utcNow,
            });
        }

        EscrowEngine.ApplyReputationLocked(data, contract.SellerLogin, completedDelta: 1, cancelledDelta: 0);
        EscrowEngine.ApplyReputationLocked(data, contract.AcceptedByLogin, completedDelta: 1, cancelledDelta: 0);
    }

    private static void ExpireDirectSingleLocked(
        EscrowStoreData data,
        EscrowContract contract,
        DateTime utcNow,
        string reasonEn = "Expired Direct VND escrow return",
        string reasonVi = "Hoàn trả hợp đồng VND hết hạn")
    {
        var dv = contract.DirectVnd!;
        if (contract.Status != EscrowContractStatus.Cancelled)
        {
            contract.Status = EscrowContractStatus.Expired;
        }

        contract.ClosedAtUtc = utcNow;
        var bondRefund = dv.SellerBondCoins > 0 && !dv.BondReleased && !dv.BondForfeited ? dv.SellerBondCoins : 0;
        if (bondRefund > 0)
        {
            dv.BondReleased = true;
        }

        data.OwedDeliveries.Add(new EscrowOwedDelivery
        {
            ContractId = contract.ContractId,
            RecipientLogin = contract.SellerLogin,
            ReasonEn = reasonEn,
            ReasonVi = reasonVi,
            Items = contract.Items.ToList(),
            Zen = contract.EscrowedZen,
            Jewels = contract.EscrowedJewels.ToList(),
            CoinCurrency = dv.SellerBondCurrency,
            Coins = bondRefund,
            CreatedAtUtc = utcNow,
        });
    }

    private static void TrimLedgerLocked(EscrowStoreData data)
    {
        if (data.FeeLedger.Count > MaxFeeLedgerEntries)
        {
            data.FeeLedger = data.FeeLedger.Take(MaxFeeLedgerEntries).ToList();
        }
    }
}
