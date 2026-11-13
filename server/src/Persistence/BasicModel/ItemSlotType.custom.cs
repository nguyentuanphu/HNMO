// <copyright file="ItemSlotType.custom.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.BasicModel;

/// <summary>
/// A plain implementation of <see cref="HNMO.DataModel.Configuration.Items.ItemSlotType"/>.
/// </summary>
public partial class ItemSlotType
{
    /// <inheritdoc />
    public override ICollection<int> ItemSlots => base.ItemSlots ?? (base.ItemSlots = new List<int>());
}