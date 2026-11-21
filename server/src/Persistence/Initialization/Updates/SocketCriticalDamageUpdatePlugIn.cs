// <copyright file="SocketCriticalDamageUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// This update sets the values of the lightning socket option "critical damage increase" to the values of the
/// Season 6/9 server suites: +30, +32, +35, +40, +50 for the seed sphere levels 1 to 5. HNMO had copied the values of
/// "excellent damage increase" (+15, +20, +25, +30, +40). A value the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E61B4A28-93D7-4C5F-A0E2-7B48C1D9F356")]
public class SocketCriticalDamageUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Lightning socket option: critical damage of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Critical damage increase +30/+32/+35/+40/+50 (was +15/+20/+25/+30/+40), unless the host changed the value.";

    /// <summary>The number of the "critical damage increase" option within the lightning socket options.</summary>
    internal const int OptionNumber = 2;

    /// <summary>Gets the values by seed sphere level 1 to 5 (Season 6/9 server suites).</summary>
    internal static IReadOnlyList<float> NewValues { get; } = [30, 32, 35, 40, 50];

    private static readonly float[] OldValues = [15, 20, 25, 30, 40];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var lightning = gameConfiguration.ItemOptions.FirstOrDefault(o => o.GetId() == GuidHelper.CreateGuid<DataModel.Configuration.Items.ItemOptionDefinition>(ItemOptionDefinitionNumbers.SocketLightning));
        var option = lightning?.PossibleOptions.FirstOrDefault(o => o.Number == OptionNumber);
        if (option is null)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var optionOfLevel in option.LevelDependentOptions)
        {
            if (optionOfLevel.Level is < 1 or > 5
                || optionOfLevel.PowerUpDefinition is not { } powerUp
                || powerUp.TargetAttribute != Stats.CriticalDamageBonus
                || powerUp.Boost?.ConstantValue is not { } value)
            {
                continue;
            }

            var index = optionOfLevel.Level - 1;
            if (Math.Abs(value.Value - OldValues[index]) < 0.001f)
            {
                value.Value = NewValues[index];
            }
        }

        return ValueTask.CompletedTask;
    }
}
