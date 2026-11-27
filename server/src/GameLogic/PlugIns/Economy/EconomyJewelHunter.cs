// <copyright file="EconomyJewelHunter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Economy;

/// <summary>A private snapshot candidate with a matching recorded monster drop and pickup.</summary>
/// <param name="AccountKey">The internal account key used to honor current visibility and never published.</param>
/// <param name="Character">The recorded character name.</param>
/// <param name="Pieces">The recorded jewels collected.</param>
public sealed record EconomyJewelHunter(string AccountKey, string Character, long Pieces);
