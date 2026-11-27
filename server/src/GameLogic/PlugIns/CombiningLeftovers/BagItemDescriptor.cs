// <copyright file="BagItemDescriptor.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

/// <summary>Bag item slot descriptor used by <see cref="CombiningLeftoversEngine"/>.</summary>
/// <param name="Slot">Inventory slot index.</param>
/// <param name="Group">Item group.</param>
/// <param name="Number">Item number.</param>
/// <param name="Level">Item level.</param>
/// <param name="Locked">Whether account-safety locked.</param>
/// <param name="Quantity">Item stack quantity (durability).</param>
public sealed record BagItemDescriptor(byte Slot, byte Group, short Number, byte Level, bool Locked = false, int Quantity = 1);
