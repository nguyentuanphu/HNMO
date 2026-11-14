// <copyright file="BankTopUpService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.RegularExpressions;
using System.Threading;
using HNMO.DataModel.Configuration;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// An incoming bank transfer as a payment service (Casso, SePay) reported it.
/// </summary>
/// <param name="Provider">The service which reported it ("casso", "sepay").</param>
/// <param name="ProviderId">The service's own id of the transaction.</param>
/// <param name="BankReference">The bank's reference of the transaction (the same for every service), if known.</param>
/// <param name="AmountVnd">The amount in VND.</param>
/// <param name="Message">The transfer message.</param>
public sealed record BankTransfer(string Provider, string ProviderId, string? BankReference, long AmountVnd, string Message);

/// <summary>
/// The outcome of a reported transfer.
/// </summary>
public enum BankTopUpResult
{
    /// <summary>The coins were added.</summary>
    Credited,

    /// <summary>This transaction was already turned into coins (a repeated report, or reported by both services).</summary>
    AlreadyCredited,

    /// <summary>The message has no account name after the transfer word.</summary>
    NoAccountInMessage,

    /// <summary>The account named in the message doesn't exist.</summary>
    AccountNotFound,

    /// <summary>The amount is below the minimum.</summary>
    BelowMinimum,
}

/// <summary>
/// Turns transfers reported by Casso or SePay into cash shop coins for the account named in the transfer
/// message ("HNMO test2"), at the rate of the <see cref="BankTopUpPlugIn"/>. Every bank transaction counts
/// once: the grant's reference is the bank's reference (or the service's id), which the database keeps unique.
/// </summary>
public class BankTopUpService
{
    private readonly CashShopCoinService _coins;
    private readonly ILogger<BankTopUpService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankTopUpService"/> class.
    /// </summary>
    /// <param name="coins">The cash shop coin service.</param>
    /// <param name="logger">The logger.</param>
    public BankTopUpService(CashShopCoinService coins, ILogger<BankTopUpService> logger)
    {
        this._coins = coins;
        this._logger = logger;
    }

    /// <summary>
    /// Finds the account name after the transfer word in a transfer message. Banks often add their own
    /// text, change the case or replace spaces with dots, e.g. "MBVCB.123456.HNMO.test2.CT tu ...".
    /// </summary>
    /// <param name="message">The transfer message.</param>
    /// <param name="prefix">The transfer word, e.g. "HNMO".</param>
    /// <returns>The account name as written, or <c>null</c>.</returns>
    public static string? FindAccountName(string? message, string prefix)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        var match = Regex.Match(
            message,
            $@"(?<![A-Za-z0-9]){Regex.Escape(prefix.Trim())}[\s.\-_:]*([A-Za-z0-9]{{3,10}})(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The coins an amount buys: whole coins only, the rest is dropped.
    /// </summary>
    /// <param name="amountVnd">The amount in VND.</param>
    /// <param name="vndPerCoin">The VND per coin.</param>
    /// <returns>The coins.</returns>
    public static int CoinsFor(long amountVnd, int vndPerCoin)
    {
        if (amountVnd <= 0 || vndPerCoin <= 0)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, amountVnd / vndPerCoin);
    }

    /// <summary>
    /// The grant reference of a transfer: the bank's reference when known, so that the same transaction
    /// reported by both services counts once; otherwise the service's own id.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <returns>The reference.</returns>
    public static string ReferenceOf(BankTransfer transfer)
    {
        return string.IsNullOrWhiteSpace(transfer.BankReference)
            ? $"topup:{transfer.Provider}:{transfer.ProviderId}"
            : $"topup:bank:{transfer.BankReference.Trim().ToUpperInvariant()}";
    }

    /// <summary>
    /// Adds the coins of a reported transfer.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    /// <param name="configuration">The top-up configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened.</returns>
    public async ValueTask<BankTopUpResult> CreditAsync(BankTransfer transfer, BankTopUpConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (transfer.AmountVnd < configuration.MinimumVnd)
        {
            this._logger.LogInformation("Top-up {Provider} {Id}: {Amount} VND is below the minimum of {Minimum} VND; not turned into coins.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, configuration.MinimumVnd);
            return BankTopUpResult.BelowMinimum;
        }

        if (FindAccountName(transfer.Message, configuration.TransferPrefix) is not { } written)
        {
            this._logger.LogWarning("Top-up {Provider} {Id}: {Amount} VND without an account name in the message \"{Message}\"; add the coins by hand.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, transfer.Message);
            return BankTopUpResult.NoAccountInMessage;
        }

        var coins = CoinsFor(transfer.AmountVnd, configuration.VndPerCoin);
        var reason = $"Bank transfer {transfer.AmountVnd:N0} VND ({transfer.Provider})";
        var reference = ReferenceOf(transfer);

        // Banks often change the case of the message: try it as written, then lower and upper case.
        foreach (var login in new[] { written, written.ToLowerInvariant(), written.ToUpperInvariant() }.Distinct())
        {
            var (status, _) = await this._coins.GrantAsync(login, configuration.CoinType, coins, reason, reference, transfer.Provider, cancellationToken).ConfigureAwait(false);
            switch (status)
            {
                case CashShopCoinGrantStatus.Created:
                    this._logger.LogInformation("Top-up {Provider} {Id}: {Amount} VND -> {Coins} {CoinType} for {Login}.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, coins, configuration.CoinType, login);
                    return BankTopUpResult.Credited;
                case CashShopCoinGrantStatus.AlreadyGranted:
                case CashShopCoinGrantStatus.ReferenceUsedByOtherAccount:
                    return BankTopUpResult.AlreadyCredited;
                case CashShopCoinGrantStatus.AccountNotFound:
                    continue;
            }
        }

        this._logger.LogWarning("Top-up {Provider} {Id}: {Amount} VND for account \"{Login}\", which doesn't exist; add the coins by hand.", transfer.Provider, transfer.ProviderId, transfer.AmountVnd, written);
        return BankTopUpResult.AccountNotFound;
    }
}
