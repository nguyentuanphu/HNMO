// <copyright file="Jewels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Version097d.Items;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;

/// <summary>
/// Jewels for MU Version 0.97d.
/// </summary>
public class Jewels : Version095d.Items.Jewels
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Jewels"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public Jewels(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();
        this.GameConfiguration.Items.Add(this.CreateJewelOfCreation());
    }

    /// <summary>
    /// Creates an <see cref="ItemDefinition"/> for the 'Jewel of Creation'.
    /// </summary>
    /// <returns><see cref="ItemDefinition"/> for the 'Jewel of Creation'.</returns>
    private ItemDefinition CreateJewelOfCreation()
    {
        var itemDefinition = this.Context.CreateNew<ItemDefinition>();
        itemDefinition.Name = LocalizedString.FromResource(() => ItemNames.JewelOfCreation);
        itemDefinition.Number = 22;
        itemDefinition.Group = 14;
        itemDefinition.DropsFromMonsters = false;
        itemDefinition.DropLevel = 72;
        itemDefinition.Durability = 1;
        itemDefinition.Width = 1;
        itemDefinition.Height = 1;
        itemDefinition.SetGuid(itemDefinition.Group, itemDefinition.Number);
        this.AddItemToJewelItemDrop(itemDefinition);
        return itemDefinition;
    }
}