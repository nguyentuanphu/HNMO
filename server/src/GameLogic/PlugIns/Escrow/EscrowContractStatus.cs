// <copyright file="EscrowContractStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Lifecycle status of an escrow contract.
/// </summary>
public enum EscrowContractStatus
{
    /// <summary>Open and awaiting buyer payment/acceptance.</summary>
    Open = 0,

    /// <summary>Atomically swapped and delivered through owed rewards.</summary>
    Completed = 1,

    /// <summary>Cancelled prior to payment; escrowed goods returned.</summary>
    Cancelled = 2,

    /// <summary>Expired past deadline; escrowed goods returned.</summary>
    Expired = 3,

    /// <summary>Frozen by admin review.</summary>
    Frozen = 4,
}
