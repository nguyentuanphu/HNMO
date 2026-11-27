// <copyright file="MountBagItem.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

/// <summary>Bag mount descriptor for pure rule checks.</summary>
/// <param name="Slot">Inventory slot index.</param>
/// <param name="Group">Item group.</param>
/// <param name="Number">Item number.</param>
/// <param name="Level">Item level.</param>
/// <param name="Locked">Whether account-safety locked.</param>
public sealed record MountBagItem(byte Slot, byte Group, short Number, byte Level, bool Locked = false);
