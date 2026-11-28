// <copyright file="BankTopUpPending.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

/// <summary>What became of a top-up a player asked for.</summary>
public enum BankTopUpPendingStatus
{
    /// <summary>Waiting for the transfer (or for the host to see it).</summary>
    Pending,

    /// <summary>The coins were added.</summary>
    Credited,

    /// <summary>The host said no transfer arrived (or it was wrong).</summary>
    Rejected,
}

/// <summary>
/// A top-up a player asked for on the top-up page: the transfer code in the message tells the host (or Casso/SePay)
/// which account the coins go to. The coins are added once: the grant's reference is this top-up's id.
/// </summary>
public class BankTopUpPending
{
    /// <summary>Gets or sets the id (also the grant's reference, see <see cref="Reference"/>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the transfer code players write in the message, e.g. HNMO7K3QX2.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount the player chose, in VND.</summary>
    public long AmountVnd { get; set; }

    /// <summary>Gets or sets the coins this amount buys at the rate of the moment.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the bank id of the host's account.</summary>
    public string Bin { get; set; } = string.Empty;

    /// <summary>Gets or sets the host's account number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets when the player asked (UTC).</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>Gets or sets until when the code is shown as waiting (UTC); a transfer arriving later still counts.</summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public BankTopUpPendingStatus Status { get; set; }

    /// <summary>Gets or sets when it was credited or rejected (UTC).</summary>
    public DateTime? DoneUtc { get; set; }

    /// <summary>Gets or sets who credited or rejected it: the admin's name, or the service (casso, sepay).</summary>
    public string DoneBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount that arrived, in VND, when a service reported it.</summary>
    public long? ReceivedVnd { get; set; }

    /// <summary>Gets or sets the SHA-256 (hex) of the tickets the player's browser holds to follow this top-up.</summary>
    public List<string> TicketHashes { get; set; } = [];

    /// <summary>Gets the reference of the coin grant: one top-up, one grant, whoever confirms it.</summary>
    public string Reference => $"topup:pending:{this.Id}";

    /// <summary>Whether the code still shows as waiting at a time.</summary>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c> while pending and not expired.</returns>
    public bool IsOpen(DateTime nowUtc) => this.Status == BankTopUpPendingStatus.Pending && nowUtc < this.ExpiresUtc;
}
