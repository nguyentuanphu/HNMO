// <copyright file="JewelDropsByMonsterLevelUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// This update lets the jewels of Bless, Soul, Life, Creation and Chaos drop by monster level, as in the monster item
/// drop rate table of the Season 6/9 server suites (owner's choice, 21 Nov 2026), instead of one group of ten items at
/// 0.1 %. The suites' table gives each jewel's share of the item drops for every monster level; HNMO turns it into a
/// chance per kill with HNMO's item drop chance of 30 %, averaged over bands of ten levels (1 to 149). The suites'
/// table has no jewels from level 150 on; there HNMO keeps its old rate of 0.01 % per jewel.
/// Every band is an ordinary drop item group, so the chances can be changed in the admin panel
/// (Configuration, Drop item groups).
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("E2B7C914-5A3F-4D68-8B01-C6F49D2E7A53")]
public class JewelDropsByMonsterLevelUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Jewel drops by monster level (Season 6/9 server suites)";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The five jewels drop by monster level band as in the Season 6/9 server suites instead of one 0.1 % group.";

    /// <summary>The first number of the drop item groups of this update (<see cref="GuidHelper.SetGuid{T}(T, short, short)"/>).</summary>
    internal const short GroupNumber = 0x4A;

    /// <summary>The chance of the old jewel group for the items which stay in it (angel, imp, uniria, ale, town portal).</summary>
    internal const double RemainingOldGroupChance = 0.0005;

    /// <summary>The chance per jewel from level 150 on: HNMO's old 0.1 % group shared by ten items.</summary>
    internal const double HighLevelChancePerJewel = 0.0001;

    /// <summary>The level from which the suites' table has no jewels.</summary>
    internal const byte HighLevel = 150;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 17, 30, 0, DateTimeKind.Utc);

    /// <summary>Gets the chance per kill of each jewel by monster level band: minimum and maximum level, group, number, chance.</summary>
    internal static IReadOnlyList<(byte MinimumLevel, byte MaximumLevel, byte Group, short Number, double Chance)> Bands { get; } =
    [
        (10, 19, 14, 16, 0.001963), // Jewel of Life
        (20, 29, 14, 13, 0.000089), // Jewel of Bless
        (20, 29, 14, 16, 0.001681), // Jewel of Life
        (30, 39, 14, 13, 0.000371), // Jewel of Bless
        (30, 39, 14, 14, 0.000333), // Jewel of Soul
        (30, 39, 14, 16, 0.001952), // Jewel of Life
        (40, 49, 14, 13, 0.000792), // Jewel of Bless
        (40, 49, 14, 14, 0.000797), // Jewel of Soul
        (40, 49, 14, 16, 0.003985), // Jewel of Life
        (50, 59, 14, 13, 0.000728), // Jewel of Bless
        (50, 59, 14, 14, 0.000708), // Jewel of Soul
        (50, 59, 14, 16, 0.003602), // Jewel of Life
        (60, 69, 14, 13, 0.000954), // Jewel of Bless
        (60, 69, 14, 14, 0.000964), // Jewel of Soul
        (60, 69, 14, 16, 0.003274), // Jewel of Life
        (70, 79, 14, 13, 0.000748), // Jewel of Bless
        (70, 79, 14, 14, 0.000781), // Jewel of Soul
        (70, 79, 14, 22, 0.000562), // Jewel of Creation
        (70, 79, 12, 15, 0.000093), // Jewel of Chaos
        (80, 89, 14, 13, 0.001098), // Jewel of Bless
        (80, 89, 14, 14, 0.001084), // Jewel of Soul
        (80, 89, 14, 22, 0.0011), // Jewel of Creation
        (80, 89, 12, 15, 0.001086), // Jewel of Chaos
        (90, 99, 14, 13, 0.000893), // Jewel of Bless
        (90, 99, 14, 14, 0.000892), // Jewel of Soul
        (90, 99, 14, 22, 0.000885), // Jewel of Creation
        (90, 99, 12, 15, 0.000879), // Jewel of Chaos
        (100, 109, 14, 13, 0.000633), // Jewel of Bless
        (100, 109, 14, 14, 0.000635), // Jewel of Soul
        (100, 109, 14, 22, 0.000644), // Jewel of Creation
        (100, 109, 12, 15, 0.000626), // Jewel of Chaos
        (110, 119, 14, 13, 0.000845), // Jewel of Bless
        (110, 119, 14, 14, 0.000853), // Jewel of Soul
        (110, 119, 14, 22, 0.000846), // Jewel of Creation
        (110, 119, 12, 15, 0.000872), // Jewel of Chaos
        (120, 129, 14, 13, 0.001708), // Jewel of Bless
        (120, 129, 14, 14, 0.001718), // Jewel of Soul
        (120, 129, 14, 22, 0.001722), // Jewel of Creation
        (120, 129, 12, 15, 0.001708), // Jewel of Chaos
        (130, 139, 14, 13, 0.001452), // Jewel of Bless
        (130, 139, 14, 14, 0.001473), // Jewel of Soul
        (130, 139, 14, 22, 0.001462), // Jewel of Creation
        (130, 139, 12, 15, 0.001476), // Jewel of Chaos
        (140, 149, 14, 13, 0.001526), // Jewel of Bless
        (140, 149, 14, 14, 0.001536), // Jewel of Soul
        (140, 149, 14, 22, 0.001547), // Jewel of Creation
        (140, 149, 12, 15, 0.001541), // Jewel of Chaos
    ];

    /// <summary>Gets the five jewels: group and number.</summary>
    internal static IReadOnlyList<(byte Group, short Number)> Jewels { get; } = [(14, 13), (14, 14), (14, 16), (14, 22), (12, 15)];

    /// <summary>Moves the five jewels out of the old group into the groups by monster level.</summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void Apply(IContext context, GameConfiguration gameConfiguration)
    {
        var oldGroupId = GuidHelper.CreateGuid<DropItemGroup>(4);
        if (gameConfiguration.DropItemGroups.FirstOrDefault(g => g.GetId() == oldGroupId) is not { } oldGroup
            || Math.Abs(oldGroup.Chance - 0.001) > 0.0000001
            || gameConfiguration.DropItemGroups.Any(g => g.GetId() == GuidHelper.CreateGuid<DropItemGroup>(GroupNumber, 0)))
        {
            // The host changed the old group, or the update ran already.
            return;
        }

        var jewels = Jewels
            .Select(j => gameConfiguration.Items.FirstOrDefault(i => i.Group == j.Group && i.Number == j.Number))
            .OfType<ItemDefinition>()
            .ToList();
        foreach (var jewel in jewels)
        {
            oldGroup.PossibleItems.Remove(jewel);
        }

        oldGroup.Chance = RemainingOldGroupChance;
        var maps = gameConfiguration.Maps.Where(m => m.DropItemGroups.Contains(oldGroup)).ToList();

        short index = 0;
        foreach (var (minimumLevel, maximumLevel, group, number, chance) in Bands)
        {
            if (jewels.FirstOrDefault(j => j.Group == group && j.Number == number) is { } jewel)
            {
                AddGroup(index, $"JewelByMonsterLevel{ResourceKeyOf(jewel)}{minimumLevel}To{maximumLevel}", minimumLevel, maximumLevel, chance, jewel);
            }

            index++;
        }

        foreach (var jewel in jewels)
        {
            AddGroup(index++, $"JewelByMonsterLevel{ResourceKeyOf(jewel)}From{HighLevel}", HighLevel, null, HighLevelChancePerJewel, jewel);
        }

        void AddGroup(short subNumber, string descriptionKey, byte minimumLevel, byte? maximumLevel, double chance, ItemDefinition jewel)
        {
            var dropGroup = context.CreateNew<DropItemGroup>();
            dropGroup.SetGuid(GroupNumber, subNumber);
            dropGroup.Description = DropGroupDescriptions.ResourceManager.GetLocalizedString(descriptionKey); // texts in every language
            dropGroup.Chance = chance;
            dropGroup.MinimumMonsterLevel = minimumLevel;
            dropGroup.MaximumMonsterLevel = maximumLevel;
            dropGroup.ItemType = SpecialItemType.Jewel;
            dropGroup.PossibleItems.Add(jewel);
            gameConfiguration.DropItemGroups.Add(dropGroup);
            foreach (var map in maps)
            {
                map.DropItemGroups.Add(dropGroup);
            }
        }
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        Apply(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    /// <summary>The resource key part of a jewel, as in the item names.</summary>
    private static string ResourceKeyOf(ItemDefinition jewel) => (jewel.Group, jewel.Number) switch
    {
        (14, 13) => "JewelOfBless",
        (14, 14) => "JewelOfSoul",
        (14, 16) => "JewelOfLife",
        (14, 22) => "JewelOfCreation",
        _ => "JewelOfChaos",
    };
}
