// <copyright file="JewelBankPendingOperation.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

/// <summary>
/// An in-game deposit or withdrawal between the bank and a bag that a stop could cut in two: written before the bag is
/// saved, removed once the bank matches the saved bag. The bag's save carries a marker with the operation's id, so after
/// a stop the next login knows which side was saved (<see cref="JewelBankPlugIn.FinishPendingOperationsAsync"/>).
/// </summary>
public sealed class JewelBankPendingOperation
{
    /// <summary>The kind of a deposit (the bank is credited once the bag without the jewels is saved).</summary>
    public const string DepositKind = "deposit";

    /// <summary>The kind of a withdrawal (the bank was debited; it is credited back unless the bag with the jewels is saved).</summary>
    public const string WithdrawKind = "withdraw";

    /// <summary>Gets or sets the operation id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the account login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets <see cref="DepositKind"/> or <see cref="WithdrawKind"/>.</summary>
    public string Kind { get; set; } = DepositKind;

    /// <summary>Gets or sets the jewels, by jewel key.</summary>
    public Dictionary<string, int> Jewels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the UTC time it was written.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
