// <copyright file="PhoenixShotPvpBlockUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// Phoenix Shot's "decrease block" against a player lowers the player's PvP defense rate, which PvP hits read. It
/// lowered the defense rate against monsters instead, so the PvP debuff never made a player easier to hit by players.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("C17D4A06-83E2-4B5F-9A61-E2B05F3D8C47")]
public class PhoenixShotPvpBlockUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Phoenix Shot: PvP block decrease";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Phoenix Shot's decrease block against a player lowers the PvP defense rate (it lowered the rate against monsters).";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 21, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.MagicEffects.FirstOrDefault(e => e.Number == (short)MagicEffectNumber.DecreaseBlock) is { } effect
            && gameConfiguration.Attributes.FirstOrDefault(a => a == Stats.DefenseRatePvp) is { } pvpDefenseRate)
        {
            foreach (var powerUp in effect.PowerUpDefinitionsPvp.Where(p => p.TargetAttribute == Stats.DefenseRatePvm))
            {
                powerUp.TargetAttribute = pvpDefenseRate;
            }
        }

        return ValueTask.CompletedTask;
    }
}
