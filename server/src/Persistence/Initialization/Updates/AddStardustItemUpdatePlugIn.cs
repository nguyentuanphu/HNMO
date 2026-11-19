// <copyright file="AddStardustItemUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Adds Stardust (14:153), an item of the game's item list the server didn't define: a GM's <c>/item 14 153</c> said
/// it doesn't exist. As the Season 9 server's item list has it: 1x1, no monster drop, 60 zen.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddStardustItemUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddStardustItemUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("24D673FF-7089-4E8E-824D-74A9476788A5")]
public class AddStardustItemUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Add Stardust Item";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "This update adds the Stardust item (14:153), which the game's item list has.";

    private const byte ItemGroup = 14;

    private const short ItemNumber = 153;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
#pragma warning disable CS1998
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
#pragma warning restore CS1998
    {
        if (gameConfiguration.Items.Any(item => item.Group == ItemGroup && item.Number == ItemNumber))
        {
            return;
        }

        var itemDefinition = context.CreateNew<ItemDefinition>();
        itemDefinition.Name = LocalizedString.FromResource(() => HNMO.Persistence.Initialization.Properties.ItemNames.Stardust);
        itemDefinition.Number = ItemNumber;
        itemDefinition.Group = ItemGroup;
        itemDefinition.DropLevel = 0;
        itemDefinition.DropsFromMonsters = false;
        itemDefinition.Durability = 1;
        itemDefinition.Width = 1;
        itemDefinition.Height = 1;
        itemDefinition.Value = 60;
        itemDefinition.SetGuid(itemDefinition.Group, itemDefinition.Number);
        gameConfiguration.Items.Add(itemDefinition);
    }
}
