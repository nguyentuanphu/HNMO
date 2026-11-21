// <copyright file="SwellLifeDurationUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Updates the Swell Life and Swell Life Proficiency buff durations from 60 + Energy / 180 s (capped at 180 s)
/// to 60 + Energy / 10 s uncapped, matching the Season 6 server suite.
/// </summary>
[PlugIn]
[Display(
    Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SwellLifeDurationUpdatePlugIn_Name),
    Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SwellLifeDurationUpdatePlugIn_Description),
    ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("87843ADB-B3AA-4F61-B6DC-34FEBB8EB265")]
public class SwellLifeDurationUpdatePlugIn : UpdatePlugInBase
{
    private const float OldEnergyDivisor = 1f / 180f;
    private const float NewEnergyDivisor = 1f / 10f;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "Align Swell Life duration with the Season 6 suite";

    /// <inheritdoc />
    public override string Description => "Updates Swell Life and Swell Life Proficiency duration from 60 + Energy / 180 s (180 s cap) to 60 + Energy / 10 s without a cap, matching the Season 6 server suite.";

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 1, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        UpdateDuration(gameConfiguration, MagicEffectNumber.GreaterFortitude);
        UpdateDuration(gameConfiguration, MagicEffectNumber.GreaterFortitudeProficiency);
        return ValueTask.CompletedTask;
    }

    private static void UpdateDuration(GameConfiguration gameConfiguration, MagicEffectNumber effectNumber)
    {
        if (gameConfiguration.MagicEffects.FirstOrDefault(e => e.Number == (short)effectNumber)?.Duration is not { } duration)
        {
            return;
        }

        if (duration.MaximumValue == 180f)
        {
            duration.MaximumValue = null;
        }

        if (duration.RelatedValues.FirstOrDefault(r =>
                r.InputAttribute == Stats.TotalEnergy
                && r.InputOperator == InputOperator.Multiply
                && Math.Abs(r.InputOperand - OldEnergyDivisor) < 1e-6f) is { } energyRelationship)
        {
            energyRelationship.InputOperand = NewEnergyDivisor;
        }
    }
}
