// <copyright file="Icarus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Maps;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;

/// <summary>
/// The initialization for the Icarus map.
/// </summary>
internal class Icarus : Version095d.Maps.Icarus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Icarus"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public Icarus(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <summary>
    /// Gets the spawns of the older version which Season 6 leaves out: the Season 6/9 server suites don't have these spots.
    /// </summary>
    protected override IReadOnlySet<short> LeftOutSpawnNumbers { get; } = new HashSet<short> { 103, 105, 119, 132, 154 };

    /// <inheritdoc/>
    protected override IEnumerable<MonsterSpawnArea> CreateMonsterSpawns()
    {
        foreach (var spawn in base.CreateMonsterSpawns())
        {
            yield return spawn;
        }

        // The spots of the Season 6/9 server suites which the older version doesn't have.
        yield return this.CreateMonsterSpawn(164, this.NpcDictionary[69], 036, 029); // Alquamos
        yield return this.CreateMonsterSpawn(165, this.NpcDictionary[69], 037, 041); // Alquamos
        yield return this.CreateMonsterSpawn(166, this.NpcDictionary[69], 026, 041); // Alquamos
        yield return this.CreateMonsterSpawn(167, this.NpcDictionary[69], 021, 024); // Alquamos
        yield return this.CreateMonsterSpawn(168, this.NpcDictionary[69], 021, 036); // Alquamos
        yield return this.CreateMonsterSpawn(169, this.NpcDictionary[70], 084, 036); // Queen Rainer
        yield return this.CreateMonsterSpawn(170, this.NpcDictionary[70], 090, 048); // Queen Rainer
        yield return this.CreateMonsterSpawn(171, this.NpcDictionary[70], 095, 056); // Queen Rainer
        yield return this.CreateMonsterSpawn(172, this.NpcDictionary[70], 074, 084); // Queen Rainer
        yield return this.CreateMonsterSpawn(173, this.NpcDictionary[70], 074, 072); // Queen Rainer
        yield return this.CreateMonsterSpawn(174, this.NpcDictionary[70], 057, 071); // Queen Rainer
        yield return this.CreateMonsterSpawn(175, this.NpcDictionary[70], 064, 084); // Queen Rainer
        yield return this.CreateMonsterSpawn(176, this.NpcDictionary[70], 069, 077); // Queen Rainer
        yield return this.CreateMonsterSpawn(177, this.NpcDictionary[71], 050, 033); // Mega Crust
        yield return this.CreateMonsterSpawn(178, this.NpcDictionary[71], 059, 027); // Mega Crust
        yield return this.CreateMonsterSpawn(179, this.NpcDictionary[71], 074, 038); // Mega Crust
        yield return this.CreateMonsterSpawn(180, this.NpcDictionary[73], 028, 067); // Drakan
        yield return this.CreateMonsterSpawn(181, this.NpcDictionary[75], 032, 231); // Great Drakan
        yield return this.CreateMonsterSpawn(182, this.NpcDictionary[75], 045, 173); // Great Drakan
        yield return this.CreateMonsterSpawn(183, this.NpcDictionary[75], 059, 163); // Great Drakan
        yield return this.CreateMonsterSpawn(184, this.NpcDictionary[75], 052, 168); // Great Drakan
    }

    /// <inheritdoc />
    protected override void InitializeDropItemGroups()
    {
        base.InitializeDropItemGroups();
        var lochsFeather = this.GameConfiguration.Items.First(item => item.Group == 13 && item.Number == 14);

        var feather = this.Context.CreateNew<DropItemGroup>();
        feather.SetGuid(this.MapNumber, 1);
        feather.Chance = 0.001;
        feather.Description = LocalizedString.FromResource(() => DropGroupDescriptions.LochsFeather);
        feather.MinimumMonsterLevel = 82;
        feather.PossibleItems.Add(lochsFeather);
        this.MapDefinition!.DropItemGroups.Add(feather);
        this.GameConfiguration.DropItemGroups.Add(feather);

        var crest = this.Context.CreateNew<DropItemGroup>();
        crest.SetGuid(this.MapNumber, 2);
        crest.Chance = 0.001;
        crest.Description = LocalizedString.FromResource(() => DropGroupDescriptions.CrestOfMonarch);
        crest.MinimumMonsterLevel = 82;
        crest.ItemLevel = 1;
        crest.PossibleItems.Add(lochsFeather);
        this.MapDefinition.DropItemGroups.Add(crest);
        this.GameConfiguration.DropItemGroups.Add(crest);
    }
}
