// <copyright file="BlessPotionSiegeOnlyUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// This update takes the damage power-up out of the effect of the Potion of Bless (owner's decision of 26 Nov 2026):
/// its bonus applies only against Castle Siege gates and statues, and the damage calculation does that itself.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7C0E7B5E-2D4B-4F0B-9B1A-5E3C8A21D4F6")]
public class BlessPotionSiegeOnlyUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Potion of Bless only for the siege";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The Potion of Bless no longer raises the damage against monsters and players; it only does against Castle Siege gates and statues.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 5, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.MagicEffects.FirstOrDefault(m => m.Number == (short)MagicEffectNumber.PotionOfBless) is { } effect)
        {
            foreach (var powerUp in effect.PowerUpDefinitions.ToList())
            {
                effect.PowerUpDefinitions.Remove(powerUp);
            }
        }

        return ValueTask.CompletedTask;
    }
}
