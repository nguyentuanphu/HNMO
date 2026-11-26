// <copyright file="AuditDataFixesUpdatePlugInSeason6.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Items;
using HNMO.PlugIns;

/// <summary>
/// The 25 Nov 2026 audit fixes for Season 6 databases: Hanzo's store, the excellent damage chance of the Kantata set
/// (10.0, i.e. every hit excellent, instead of 0.10) and the SD of the Guardian option on boots (700, was 200).
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("3E5B6C41-8A0D-4B7E-9C2F-6D1A4E8B7F21")]
public class AuditDataFixesUpdatePlugInSeason6 : AuditDataFixesUpdatePlugInBase
{
    private const float GuardianBootsShield = 700;

    /// <inheritdoc />
    public override string Name => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    protected override void ApplyVersionFixes(GameConfiguration gameConfiguration)
    {
        // A chance of 1 or more means every hit: such a value was written as a percent (10.0 for 10 %).
        foreach (var option in gameConfiguration.ItemSetGroups.Select(set => set.Options).OfType<ItemOptionDefinition>().SelectMany(o => o.PossibleOptions))
        {
            if (option.PowerUpDefinition is { TargetAttribute: { } target, Boost.ConstantValue: { } value }
                && target == Stats.ExcellentDamageChance
                && value.Value >= 1)
            {
                value.Value /= 100;
            }
        }

        foreach (var option in gameConfiguration.ItemOptions.SelectMany(o => o.PossibleOptions))
        {
            if (option.OptionType == ItemOptionTypes.GuardianOption
                && option.Number == (int)ItemGroups.Boots
                && option.PowerUpDefinition is { TargetAttribute: { } target, Boost.ConstantValue: { } value }
                && target == Stats.MaximumShield
                && value.Value < GuardianBootsShield)
            {
                value.Value = GuardianBootsShield;
            }
        }
    }
}
