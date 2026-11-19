// <copyright file="MountBagItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

/// <summary>Bag mount descriptor for pure rule checks.</summary>
/// <param name="Slot">Inventory slot index.</param>
/// <param name="Group">Item group.</param>
/// <param name="Number">Item number.</param>
/// <param name="Level">Item level.</param>
/// <param name="Locked">Whether account-safety locked.</param>
public sealed record MountBagItem(byte Slot, byte Group, short Number, byte Level, bool Locked = false);
