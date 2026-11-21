// <copyright file="ThirdWingsLuckUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update sets the luck chance of the 3rd level wings mix to 10 %, as in the chaos box settings of the Season 6
/// server suite (owner's choice, 21 Nov 2026; it was 5 %). A chance the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("B7F20C59-3E81-4D6A-9C14-08A5E6D3F172")]
public class ThirdWingsLuckUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "3rd wings mix luck of the Season 6 server suite";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The 3rd level wings (stage 2) get luck at 10 % (was 5 %), unless the host changed it.";

    /// <summary>The number of the 3rd level wings, stage 2 crafting.</summary>
    internal const byte CraftingNumber = 39;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 10, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var crafting in gameConfiguration.Monsters.Where(m => m.NpcWindow == NpcWindow.ChaosMachine).SelectMany(m => m.ItemCraftings).Distinct())
        {
            if (crafting.Number == CraftingNumber && crafting.SimpleCraftingSettings is { ResultItemLuckOptionChance: 5 } settings)
            {
                settings.ResultItemLuckOptionChance = 10;
            }
        }

        return ValueTask.CompletedTask;
    }
}
