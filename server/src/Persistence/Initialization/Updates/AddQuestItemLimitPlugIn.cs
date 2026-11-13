// <copyright file="AddQuestItemLimitPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.VersionSeasonSix.Items;
using HNMO.PlugIns;

/// <summary>
/// This updates adds the new <see cref="ItemDefinition.StorageLimitPerCharacter"/> for quest items.
/// Version 2 additionally marks them as quest items (<see cref="ItemDefinition.IsQuestItem"/>).
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddQuestItemLimitPlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddQuestItemLimitPlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("48D40F2E-2844-4058-B1FA-710EEE55157B")]
public class AddQuestItemLimitPlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Add quest item limits";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "This update adds limits to quest items, so that only one item can be picked up.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override int Version => 2;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2023, 05, 04, 20, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override DateTime UpdatedAt => new(2026, 09, 30, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var hashSet = new HashSet<short>
        {
            Quest.BrokenSwordNumber,
            Quest.EyeOfAbyssalNumber,
            Quest.FeatherOfDarkPhoenixNumber,
            Quest.FlameOfDeathBeamKnightNumber,
            Quest.HornOfHellMaineNumber,
            Quest.ScrollOfEmperorNumber,
            Quest.SoulShardOfWizardNumber,
            Quest.TearOfElfNumber,
        };
        var questItems = gameConfiguration.Items.Where(item => item.Group == 14 && hashSet.Contains(item.Number));
        foreach (var item in questItems)
        {
            item.StorageLimitPerCharacter = 1;
            item.IsQuestItem = true;
        }
    }
}