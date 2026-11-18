// <copyright file="GiftBoxPackingStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

/// <summary>The outcome of planning all promised items without changing the real inventory.</summary>
public enum GiftBoxPackingStatus
{
    /// <summary>Every item fits in a real bag slot with its exact promised options.</summary>
    Ready,

    /// <summary>The complete reward does not fit; the owed letter must remain unchanged.</summary>
    BagFull,

    /// <summary>An item or option is no longer supported; never silently reduce the reward.</summary>
    InvalidReward,

    /// <summary>The existing inventory cannot be represented safely without moving its items.</summary>
    InvalidInventory,
}
