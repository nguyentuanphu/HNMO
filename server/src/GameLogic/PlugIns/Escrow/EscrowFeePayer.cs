// <copyright file="EscrowFeePayer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Controls who bears the escrow service fee.
/// </summary>
public enum EscrowFeePayer
{
    /// <summary>Fee deducted from the seller's proceeds.</summary>
    Seller = 0,

    /// <summary>Fee added on top of the buyer's payment.</summary>
    Buyer = 1,

    /// <summary>Fee split evenly between buyer and seller.</summary>
    Split = 2,
}
