// <copyright file="EscrowFeeBreakdown.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

/// <summary>
/// Computed escrow host fee and net counterparty amounts.
/// </summary>
/// <param name="TotalFee">Total host fee charged on the transaction.</param>
/// <param name="SellerFeePaid">Portion borne by seller.</param>
/// <param name="BuyerFeePaid">Portion paid by buyer in addition to asking price.</param>
/// <param name="BuyerTotalDebit">Total currency debited from buyer.</param>
/// <param name="NetSellerProceeds">Net currency delivered to seller.</param>
public readonly record struct EscrowFeeBreakdown(
    long TotalFee,
    long SellerFeePaid,
    long BuyerFeePaid,
    long BuyerTotalDebit,
    long NetSellerProceeds);
