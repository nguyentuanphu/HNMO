// <copyright file="MaintenanceDuplicateItemOccurrence.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>
/// Represents a single occurrence of an item serial/GUID found in more than one storage location.
/// </summary>
/// <param name="ItemId">Duplicated item identifier.</param>
/// <param name="AccountLogin">Owning account login name.</param>
/// <param name="LocationLabel">Human-readable location (e.g. Vault, or Character inventory).</param>
/// <param name="ItemSummary">Item name and level summary.</param>
/// <param name="Slot">Storage slot index.</param>
public sealed record MaintenanceDuplicateItemOccurrence(
    Guid ItemId,
    string AccountLogin,
    string LocationLabel,
    string ItemSummary,
    byte Slot);
