// <copyright file="SeasonNineMasterTreeParityTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameServer.RemoteView;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The Season 9 master tree the web game draws (<c>client/web/public/data/Season9/master-tree.json</c>, 480 rows) against
/// the server's master skills: each row's skill must exist with a master definition of the same maximum level and
/// prerequisites, for a class of the row, at the same tree index (the index the server sends in F3:52/53).
/// </summary>
[TestFixture]
public class SeasonNineMasterTreeParityTest
{
    // classCode bit of the web tree -> the master class number of that line.
    private static readonly Dictionary<int, byte> MasterClassOfCode = new()
    {
        [1] = 7, // Blade Master
        [2] = 3, // Grand Master
        [4] = 11, // High Elf
        [8] = 23, // Dimension Master
        [16] = 13, // Duel Master
        [32] = 17, // Lord Emperor
        [64] = 25, // Fist Master
    };

    // Skills the web tree draws which aren't master skills of the server yet (docs/audit/s9c-evidence-2026-11-27.md).
    private const int MissingSkillCount = 108;

    private GameConfiguration _configuration = null!;

    /// <summary>Creates the Season 6 data once.</summary>
    /// <returns>A task.</returns>
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        this._configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }

    /// <summary>Every row of the web's Season 9 tree matches a server master skill.</summary>
    [Test]
    public void EveryWebTreeRowMatchesAServerMasterSkill()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TreeFile()));
        var problems = new List<string>();
        var missing = new SortedSet<short>();
        var rows = 0;
        foreach (var node in document.RootElement.GetProperty("nodes").EnumerateArray())
        {
            rows++;
            var number = node.GetProperty("skill").GetInt16();
            var code = node.GetProperty("classCode").GetInt32();
            var index = node.GetProperty("index").GetInt32();
            var where = $"{number} (class {code}, index {index})";
            if (this._configuration.Skills.FirstOrDefault(s => s.Number == number) is not { } skill)
            {
                missing.Add(number);
                continue;
            }

            if (skill.MasterDefinition is not { } definition)
            {
                problems.Add($"{where}: not a master skill");
                continue;
            }

            if (definition.MaximumLevel != node.GetProperty("maxLevel").GetInt32())
            {
                problems.Add($"{where}: maximum level {definition.MaximumLevel}, web {node.GetProperty("maxLevel").GetInt32()}");
            }

            var webRequired = node.GetProperty("requires").EnumerateArray().Select(r => r.GetInt32()).Where(r => r > 0).OrderBy(r => r).ToArray();
            var serverRequired = definition.RequiredMasterSkills.Where(s => s.MasterDefinition is not null).Select(s => (int)s.Number).OrderBy(r => r).ToArray();
            if (!webRequired.SequenceEqual(serverRequired))
            {
                problems.Add($"{where}: requires [{string.Join(",", serverRequired)}], web [{string.Join(",", webRequired)}]");
            }

            var masterClass = this._configuration.CharacterClasses.First(c => c.Number == MasterClassOfCode[code]);
            if (!skill.QualifiedCharacters.Contains(masterClass))
            {
                problems.Add($"{where}: not for class {masterClass.Number}");
            }

            if (skill.GetMasterSkillIndex(masterClass) != index)
            {
                problems.Add($"{where}: server index {skill.GetMasterSkillIndex(masterClass)}");
            }
        }

        Assert.That(rows, Is.EqualTo(480));
        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems.Take(40)));

        // Master skills the web tree draws which the server doesn't have yet (passives of ranks 6-9 and others): a row
        // of them can be seen but not learned. The count goes down as they are added.
        Assert.That(missing, Has.Count.EqualTo(MissingSkillCount), string.Join(",", missing));
    }

    private static string TreeFile([CallerFilePath] string testFilePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", "..", "client", "web", "public", "data", "Season9", "master-tree.json"));
}
