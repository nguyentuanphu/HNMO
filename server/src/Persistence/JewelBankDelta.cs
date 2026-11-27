// <copyright file="JewelBankDelta.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Persistence;

/// <summary>A relative change of one Jewel Bank balance, applied only if the result stays within 0 and the capacity.</summary>
/// <param name="JewelKey">The jewel key.</param>
/// <param name="Delta">The change (positive: credit, negative: debit).</param>
/// <param name="Capacity">The highest balance a credit may reach.</param>
public sealed record JewelBankDelta(string JewelKey, int Delta, int Capacity);
