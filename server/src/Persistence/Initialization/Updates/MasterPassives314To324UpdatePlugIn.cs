// <copyright file="MasterPassives314To324UpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// Adds the Season 6 rank 6-9 passive master skills (314..324, stat/AG/attack/wizardry increases,
/// third-class wing passives, hit recovery, and critical/excellent/double/ignore-def chances),
/// their target attributes, and their <see cref="MasterSkillDefinition.PassivePowerUps"/>.
/// </summary>
[PlugIn]
[Display(
    Name = nameof(PlugInResources.MasterPassives314To324UpdatePlugIn_Name),
    Description = nameof(PlugInResources.MasterPassives314To324UpdatePlugIn_Description),
    ResourceType = typeof(PlugInResources))]
[Guid("7F3E21B8-4C9A-4E75-9A1D-6C2B8E5F3042")]
public class MasterPassives314To324UpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "Season 6: Rank 6-9 master passives";

    /// <inheritdoc />
    public override string Description => "Adds the Season 6 rank 6-9 passive master skills with passive power-ups.";

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 10, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(MasterTreeRanksAndLinksUpdatePlugIn).GUID, typeof(AddMasterSkillPassivePowerUpsPlugIn).GUID];

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Attributes.All(a => a.Id != Stats.MasterSkillValue.Id))
        {
            var attribute = context.CreateNew<AttributeDefinition>(Stats.MasterSkillValue.Id, Stats.MasterSkillValue.Designation, Stats.MasterSkillValue.Description);
            gameConfiguration.Attributes.Add(attribute);
        }

        new SkillsInitializer(context, gameConfiguration).AddMissingSeasonSixRankSixToNinePassives();
        return ValueTask.CompletedTask;
    }
}
