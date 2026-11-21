// <copyright file="SocketShieldProtectionUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// This update sets the level 5 value of the water socket option "shield protection increase" to 20 %, as in the
/// Season 6 server suite (owner's choice, 21 Nov 2026; the Season 9 suite has 30 %, for a future Season 9 data set).
/// A value the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("3C8A51E2-74D6-4B90-A1F3-6E2D9B07C458")]
public class SocketShieldProtectionUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Water socket option: shield protection level 5 of the Season 6 server suite";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Shield protection increase at seed sphere level 5 is 20 % (was 30 %), unless the host changed it.";

    /// <summary>The number of the "shield protection increase" option within the water socket options.</summary>
    internal const int OptionNumber = 2;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var levelFive = gameConfiguration.ItemOptions
            .SelectMany(o => o.PossibleOptions)
            .Where(o => o.Number == OptionNumber && o.OptionType == ItemOptionTypes.SocketOption)
            .SelectMany(o => o.LevelDependentOptions)
            .Where(l => l.Level == 5 && l.PowerUpDefinition?.TargetAttribute == Stats.ShieldItemDefenseIncrease);
        foreach (var level in levelFive)
        {
            if (level.PowerUpDefinition?.Boost?.ConstantValue is { } value && Math.Abs(value.Value - 0.30f) < 0.0001f)
            {
                value.Value = 0.20f;
            }
        }

        return ValueTask.CompletedTask;
    }
}
