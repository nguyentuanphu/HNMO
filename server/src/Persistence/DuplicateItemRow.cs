// <copyright file="DuplicateItemRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>An item which shares its serial with another one.</summary>
/// <param name="Serial">The serial.</param>
/// <param name="ItemId">The item's database id.</param>
/// <param name="StorageId">The storage it's in.</param>
/// <param name="Place">Where it is: "inventory", "vault" or "held" (market, escrow or a vault page not in use).</param>
/// <param name="CharacterName">The character whose bag holds it, if any.</param>
/// <param name="AccountLogin">The account whose bag or vault holds it, if known.</param>
/// <param name="ItemName">The item's name.</param>
/// <param name="Level">The item's level.</param>
public sealed record DuplicateItemRow(Guid Serial, Guid ItemId, Guid? StorageId, string Place, string? CharacterName, string? AccountLogin, string ItemName, int Level);
