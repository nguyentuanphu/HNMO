// <copyright file="PlusItemUpgradeRatesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update sets the base success rates of the chaos machine's item upgrades to +12 and +14 to the rates of the
/// Season 6/9 server suites (60 % and 55 %; they were 55 % and 50 %). The rates of +10, +11, +13 and +15 were already
/// the same. A rate the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("C3E0B7A4-5D21-4F86-9A3B-2E7F14D8C690")]
public class PlusItemUpgradeRatesUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Item upgrade +12 and +14 rates of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The chaos machine's +12 upgrade succeeds at 60 % (was 55 %) and +14 at 55 % (was 50 %), unless the host changed the rate.";

    /// <summary>The crafting number, the level of the item put in, the old and the new rate.</summary>
    private static readonly (byte Number, byte ItemLevel, byte OldRate, byte NewRate)[] Changes =
    [
        (22, 11, 55, 60),
        (49, 13, 50, 55),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var chaosMachines = gameConfiguration.Monsters.Where(m => m.NpcWindow == NpcWindow.ChaosMachine);
        foreach (var crafting in chaosMachines.SelectMany(m => m.ItemCraftings).Distinct())
        {
            if (crafting.SimpleCraftingSettings is not { } settings)
            {
                continue;
            }

            foreach (var (number, itemLevel, oldRate, newRate) in Changes)
            {
                if (crafting.Number == number
                    && settings.SuccessPercent == oldRate
                    && settings.RequiredItems.Any(r => r.Reference == 1 && r.MinimumItemLevel == itemLevel && r.MaximumItemLevel == itemLevel))
                {
                    settings.SuccessPercent = newRate;
                }
            }
        }

        return ValueTask.CompletedTask;
    }
}
