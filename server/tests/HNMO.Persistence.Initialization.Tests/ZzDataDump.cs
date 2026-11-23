// <copyright file="ZzDataDump.cs.txt" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

// Explicit throwaway dump test: copy into server/tests/HNMO.Persistence.Initialization.Tests/ZzDataDump.cs
// and run `dotnet test --filter ZzDataDump` (optionally set HNMO_DUMP_OUT and HNMO_SEASON=s9) to regenerate
// docs/audit/data/hnmo/*.csv from a freshly initialized in-memory database.

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using System.IO;
using System.Text;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.Updates;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Dumps HNMO's initialized game configuration tables to CSV files for external comparison.
/// </summary>
[TestFixture]
[Explicit("Regenerates docs/audit/data/hnmo/*.csv from an in-memory database.")]
internal class ZzDataDump
{
    /// <summary>
    /// Initializes an in-memory Season 6 (or Season 9 when HNMO_SEASON=s9) configuration and writes CSV tables.
    /// </summary>
    [Test]
    public async Task DumpAllTablesAsync()
    {
        var outDir = Environment.GetEnvironmentVariable("HNMO_DUMP_OUT")
            ?? Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "docs", "audit", "data", "hnmo"));
        Directory.CreateDirectory(outDir);

        var provider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        await DumpConfigurationAsync(context, config, outDir).ConfigureAwait(false);
    }

    internal static ValueTask DumpConfigurationAsync(IContext context, GameConfiguration config, string outDir)
    {
        _ = context;
        Directory.CreateDirectory(outDir);

        WriteItems(config, Path.Combine(outDir, "items.csv"));
        WriteSkills(config, Path.Combine(outDir, "skills.csv"));
        WriteMonsters(config, Path.Combine(outDir, "monsters.csv"));
        WriteSpawns(config, Path.Combine(outDir, "spawns.csv"));
        WriteMaps(config, Path.Combine(outDir, "maps.csv"));
        WriteGates(config, Path.Combine(outDir, "enter_gates.csv"), Path.Combine(outDir, "exit_gates.csv"));
        WriteWarps(config, Path.Combine(outDir, "warps.csv"));
        WriteShops(config, Path.Combine(outDir, "shops.csv"));
        WriteMixes(config, Path.Combine(outDir, "mixes.csv"));
        WriteItemOptions(config, Path.Combine(outDir, "item_options.csv"));
        WriteSocketOptions(config, Path.Combine(outDir, "socket_options.csv"));
        WriteAncientSets(config, Path.Combine(outDir, "ancient_sets.csv"));
        WriteEvents(config, Path.Combine(outDir, "events.csv"));
        WriteExpAndGlobals(config, Path.Combine(outDir, "exp_and_globals.csv"));

        return ValueTask.CompletedTask;
    }

    private static void WriteItems(GameConfiguration config, string path)
    {
        var rows = new List<string>
        {
            "Group,Number,Name,DropLevel,Slot,Width,Height,Durability,Value,DropsFromMonsters,MaxItemLevel,Bound,Ammo,Tradable,Droppable,SellableToNpc,Repairable,PersonalStoreSellable,Storable,Classes,SkillNumber,ReqStr,ReqAgi,ReqVit,ReqEne,ReqCmd,ReqLvl,BasePowers,Options,Sets",
        };
        foreach (var i in config.Items.OrderBy(x => x.Group).ThenBy(x => x.Number))
        {
            var slot = string.Join("+", i.ItemSlot?.ItemSlots.Select(s => s.ToString()) ?? []);
            var classes = string.Join(";", i.QualifiedCharacters.Select(c => c.Number).OrderBy(n => n));
            var reqs = i.Requirements
                .GroupBy(r => r.Attribute?.Designation ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Max(r => r.MinimumValue));
            var powers = string.Join(";", i.BasePowerUpAttributes.Select(p => $"{p.TargetAttribute?.Designation}={Inv(p.BaseValue)}:{p.AggregateType}"));
            var opts = string.Join(";", i.PossibleItemOptions.Select(o => o.Name.ToString()));
            var sets = string.Join(";", i.PossibleItemSetGroups.Select(s => s.Name.ToString()));
            rows.Add(CsvRow(
                i.Group,
                i.Number,
                i.Name.ToString(),
                i.DropLevel,
                slot,
                i.Width,
                i.Height,
                i.Durability,
                i.Value,
                i.DropsFromMonsters ? 1 : 0,
                i.MaximumItemLevel,
                i.IsBoundToCharacter ? 1 : 0,
                i.IsAmmunition ? 1 : 0,
                i.IsTradable ? 1 : 0,
                i.IsDroppable ? 1 : 0,
                i.IsSellableToNpc ? 1 : 0,
                i.IsRepairable ? 1 : 0,
                i.IsPersonalStoreSellable ? 1 : 0,
                i.IsStorable ? 1 : 0,
                classes,
                i.Skill?.Number.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Req(reqs, "Total Strength Requirement Value", "Total Strength"),
                Req(reqs, "Total Agility Requirement Value", "Total Agility"),
                Req(reqs, "Total Vitality Requirement Value", "Total Vitality"),
                Req(reqs, "Total Energy Requirement Value", "Total Energy"),
                Req(reqs, "Total Leadership Requirement Value", "Total Leadership"),
                Req(reqs, "Level"),
                powers,
                opts,
                sets));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteSkills(GameConfiguration config, string path)
    {
        var rows = new List<string>
        {
            "Number,Name,SkillType,DamageType,Range,MovesToTarget,Classes,ReqLevel,ReqEnergy,ReqStrength,ReqLeadership,ManaCost,AbilityCost,ImplicitTargetRange,MasterRank,MasterMaxLevel,MasterFormula",
        };
        foreach (var s in config.Skills.OrderBy(x => x.Number))
        {
            var reqs = s.Requirements
                .GroupBy(r => r.Attribute?.Designation ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Max(r => r.MinimumValue));
            var cons = s.ConsumeRequirements
                .GroupBy(r => r.Attribute?.Designation ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Max(r => r.MinimumValue));
            var classes = string.Join(";", s.QualifiedCharacters.Select(c => c.Number).OrderBy(n => n));
            rows.Add(CsvRow(
                s.Number,
                s.Name.ToString(),
                s.SkillType,
                s.DamageType,
                s.Range,
                s.MovesToTarget ? 1 : 0,
                classes,
                Req(reqs, "Level"),
                Req(reqs, "Total Energy Requirement Value", "Total Energy"),
                Req(reqs, "Total Strength Requirement Value", "Total Strength"),
                Req(reqs, "Total Leadership Requirement Value", "Total Leadership"),
                Req(cons, "Current Mana"),
                Req(cons, "Current Ability"),
                Inv(s.ImplicitTargetRange),
                s.MasterDefinition?.Rank.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                s.MasterDefinition?.MaximumLevel.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                s.MasterDefinition?.ValueFormula ?? string.Empty));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteMonsters(GameConfiguration config, string path)
    {
        var rows = new List<string>
        {
            "Number,Designation,Kind,NpcWindow,AttackSkill,MoveRange,AttackRange,ViewRange,MoveDelayMs,AttackDelayMs,RespawnDelaySec,MaxDrops,Level,MaxHp,MinDmg,MaxDmg,Defense,AttackRatePvm,DefenseRatePvm,IceRes,PoisonRes,LightRes,FireRes,WaterRes",
        };
        foreach (var m in config.Monsters.OrderBy(x => x.Number))
        {
            float Attr(string designation) => m.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Designation == designation)?.Value ?? 0f;
            rows.Add(CsvRow(
                m.Number,
                m.Designation.ToString(),
                m.ObjectKind,
                m.NpcWindow,
                m.AttackSkill?.Number.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                m.MoveRange,
                m.AttackRange,
                m.ViewRange,
                (int)m.MoveDelay.TotalMilliseconds,
                (int)m.AttackDelay.TotalMilliseconds,
                (int)m.RespawnDelay.TotalSeconds,
                m.NumberOfMaximumItemDrops,
                Inv(Attr("Level")),
                            Inv(Attr("Maximum Health")),
                            Inv(Attr("Minimum Physical Base Damage")),
                            Inv(Attr("Maximum Physical Base Damage")),
                            Inv(Attr("Base Defense")),
                            Inv(Attr("Attack Rate (PvM)")),
                            Inv(Attr("Defense Rate (PvM)")),
                Inv(Attr("Ice Resistance")),
                Inv(Attr("Poison Resistance")),
                Inv(Attr("Lightning Resistance")),
                Inv(Attr("Fire Resistance")),
                Inv(Attr("Water Resistance"))));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteSpawns(GameConfiguration config, string path)
    {
        var rows = new List<string> { "MapNumber,MapName,MonsterNumber,MonsterName,Quantity,X1,Y1,X2,Y2,Direction,SpawnTrigger" };
        foreach (var map in config.Maps.OrderBy(m => m.Number).ThenBy(m => m.Discriminator))
        {
            foreach (var sp in map.MonsterSpawns.OrderBy(s => s.MonsterDefinition?.Number ?? 0).ThenBy(s => s.X1).ThenBy(s => s.Y1))
            {
                rows.Add(CsvRow(
                    map.Number,
                    map.Name.ToString(),
                    sp.MonsterDefinition?.Number ?? 0,
                    sp.MonsterDefinition?.Designation.ToString() ?? string.Empty,
                    sp.Quantity,
                    sp.X1,
                    sp.Y1,
                    sp.X2,
                    sp.Y2,
                    sp.Direction,
                    sp.SpawnTrigger));
            }
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteMaps(GameConfiguration config, string path)
    {
        var rows = new List<string> { "Number,Discriminator,Name,ExpMultiplier,IsActive,SafezoneCount,EnterGates,ExitGates,Spawns" };
        foreach (var m in config.Maps.OrderBy(x => x.Number).ThenBy(x => x.Discriminator))
        {
            rows.Add(CsvRow(
                m.Number,
                m.Discriminator,
                m.Name.ToString(),
                Inv(m.ExpMultiplier),
                m.IsActive ? 1 : 0,
                m.SafezoneMap?.Number ?? -1,
                m.EnterGates.Count,
                m.ExitGates.Count,
                m.MonsterSpawns.Sum(s => s.Quantity)));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteGates(GameConfiguration config, string enterPath, string exitPath)
    {
        var enterRows = new List<string> { "MapNumber,MapName,Number,X1,Y1,X2,Y2,TargetMapNumber,TargetX1,TargetY1,TargetX2,TargetY2,TargetDirection,LevelReq" };
        var exitRows = new List<string> { "MapNumber,MapName,X1,Y1,X2,Y2,Direction,IsSpawnGate" };
        foreach (var map in config.Maps.OrderBy(m => m.Number).ThenBy(m => m.Discriminator))
        {
            foreach (var g in map.EnterGates.OrderBy(x => x.Number))
            {
                enterRows.Add(CsvRow(
                    map.Number,
                    map.Name.ToString(),
                    g.Number,
                    g.X1,
                    g.Y1,
                    g.X2,
                    g.Y2,
                    g.TargetGate?.Map?.Number ?? -1,
                    g.TargetGate?.X1 ?? 0,
                    g.TargetGate?.Y1 ?? 0,
                    g.TargetGate?.X2 ?? 0,
                    g.TargetGate?.Y2 ?? 0,
                    g.TargetGate?.Direction ?? Direction.Undefined,
                    g.LevelRequirement));
            }

            foreach (var eg in map.ExitGates.OrderBy(x => x.X1).ThenBy(x => x.Y1))
            {
                exitRows.Add(CsvRow(map.Number, map.Name.ToString(), eg.X1, eg.Y1, eg.X2, eg.Y2, eg.Direction, eg.IsSpawnGate ? 1 : 0));
            }
        }

        File.WriteAllLines(enterPath, enterRows, Encoding.UTF8);
        File.WriteAllLines(exitPath, exitRows, Encoding.UTF8);
    }

    private static void WriteWarps(GameConfiguration config, string path)
    {
        var rows = new List<string> { "Index,Name,LevelRequirement,Costs,TargetMapNumber,TargetMapName,GateX1,GateY1,GateX2,GateY2" };
        foreach (var w in config.WarpList.OrderBy(x => x.Index))
        {
            rows.Add(CsvRow(
                w.Index,
                w.Name.ToString(),
                w.LevelRequirement,
                w.Costs,
                w.Gate?.Map?.Number ?? -1,
                w.Gate?.Map?.Name.ToString() ?? string.Empty,
                w.Gate?.X1 ?? 0,
                w.Gate?.Y1 ?? 0,
                w.Gate?.X2 ?? 0,
                w.Gate?.Y2 ?? 0));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteShops(GameConfiguration config, string path)
    {
        var rows = new List<string> { "NpcNumber,NpcName,Slot,Group,Number,ItemName,Level,Durability,HasSkill,HasLuck,OptionLevel,ExcellentOptions" };
        foreach (var m in config.Monsters.Where(x => x.MerchantStore is not null).OrderBy(x => x.Number))
        {
            foreach (var item in m.MerchantStore!.Items.OrderBy(i => i.ItemSlot))
            {
                var opt = item.ItemOptions.FirstOrDefault(o => o.ItemOption?.OptionType == ItemOptionTypes.Option)?.Level ?? 0;
                var hasLuck = item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck) ? 1 : 0;
                var excCount = item.ItemOptions.Count(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent);
                rows.Add(CsvRow(
                    m.Number,
                    m.Designation.ToString(),
                    item.ItemSlot,
                    item.Definition?.Group ?? -1,
                    item.Definition?.Number ?? -1,
                    item.Definition?.Name.ToString() ?? string.Empty,
                    item.Level,
                    item.Durability,
                    item.HasSkill ? 1 : 0,
                    hasLuck,
                    opt,
                    excCount));
            }
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteMixes(GameConfiguration config, string path)
    {
        var rows = new List<string> { "NpcNumber,Number,Name,Handler,Money,MoneyPerPercent,SuccessPercent,MaxSuccessPercent,LuckBonus,SocketBonus,RequiredItems,ResultItems" };
        foreach (var m in config.Monsters.Where(x => x.ItemCraftings.Count > 0).OrderBy(x => x.Number))
        {
            foreach (var c in m.ItemCraftings.OrderBy(x => x.Number))
            {
                var s = c.SimpleCraftingSettings;
                var reqs = s is null
                    ? string.Empty
                    : string.Join(";", s.RequiredItems.Select(r => $"{r.MinimumAmount}..{r.MaximumAmount}x[{string.Join("|", r.PossibleItems.Select(pi => $"{pi.Group}-{pi.Number}"))}]"));
                var res = s is null
                    ? string.Empty
                    : string.Join(";", s.ResultItems.Select(r => r.ItemDefinition is { } d ? $"{d.Group}-{d.Number}" : $"ref:{r.Reference}+{r.AddLevel}"));
                rows.Add(CsvRow(
                    m.Number,
                    c.Number,
                    c.Name.ToString(),
                    c.ItemCraftingHandlerClassName,
                    s?.Money ?? 0,
                    s?.MoneyPerFinalSuccessPercentage ?? 0,
                    s?.SuccessPercent ?? 0,
                    s?.MaximumSuccessPercent ?? 0,
                    s?.SuccessPercentageAdditionForLuck ?? 0,
                    s?.SuccessPercentageAdditionForSocketItem ?? 0,
                    reqs,
                    res));
            }
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteItemOptions(GameConfiguration config, string path)
    {
        var rows = new List<string> { "Definition,AddChance,MaxPerItem,OptionNumber,SubOptionType,OptionType,LevelType,TargetAttribute,AggregateType,BaseValue,LevelValues" };
        foreach (var def in config.ItemOptions.OrderBy(d => d.Name.ToString()))
        {
            foreach (var opt in def.PossibleOptions.OrderBy(o => o.Number).ThenBy(o => o.SubOptionType))
            {
                var lvls = string.Join(";", opt.LevelDependentOptions.OrderBy(l => l.Level).Select(l => $"{l.Level}:{Inv(l.PowerUpDefinition?.Boost?.ConstantValue?.Value ?? 0)}"));
                rows.Add(CsvRow(
                    def.Name.ToString(),
                    Inv(def.AddChance),
                    def.MaximumOptionsPerItem,
                    opt.Number,
                    opt.SubOptionType,
                    opt.OptionType?.Name ?? string.Empty,
                    opt.LevelType,
                    opt.PowerUpDefinition?.TargetAttribute?.Designation ?? opt.LevelDependentOptions.FirstOrDefault()?.PowerUpDefinition?.TargetAttribute?.Designation ?? string.Empty,
                    opt.PowerUpDefinition?.Boost?.ConstantValue?.AggregateType.ToString() ?? opt.LevelDependentOptions.FirstOrDefault()?.PowerUpDefinition?.Boost?.ConstantValue?.AggregateType.ToString() ?? string.Empty,
                    Inv(opt.PowerUpDefinition?.Boost?.ConstantValue?.Value ?? 0),
                    lvls));
            }
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteSocketOptions(GameConfiguration config, string path)
    {
        var rows = new List<string> { "Definition,OptionNumber,SubOptionType,TargetAttribute,AggregateType,Levels,BonusSocketRequirements" };
        foreach (var def in config.ItemOptions.Where(d => d.PossibleOptions.Any(o => o.OptionType == ItemOptionTypes.SocketOption)).OrderBy(d => d.Name.ToString()))
        {
            foreach (var opt in def.PossibleOptions.OrderBy(o => o.Number).ThenBy(o => o.SubOptionType))
            {
                var lvls = string.Join(";", opt.LevelDependentOptions.OrderBy(l => l.Level).Select(l => $"{l.Level}={Inv(l.PowerUpDefinition?.Boost?.ConstantValue?.Value ?? 0)}"));
                rows.Add(CsvRow(
                    def.Name.ToString(),
                    opt.Number,
                    opt.SubOptionType,
                    opt.PowerUpDefinition?.TargetAttribute?.Designation ?? opt.LevelDependentOptions.FirstOrDefault()?.PowerUpDefinition?.TargetAttribute?.Designation ?? string.Empty,
                    opt.LevelDependentOptions.FirstOrDefault()?.PowerUpDefinition?.Boost?.ConstantValue?.AggregateType.ToString() ?? string.Empty,
                    lvls,
                    string.Empty));
            }
        }

        foreach (var bonus in config.ItemOptionCombinationBonuses.OrderBy(b => b.Number))
        {
            var reqs = string.Join(";", bonus.Requirements.OrderBy(r => r.SubOptionType).Select(r => $"{r.MinimumCount}:{r.SubOptionType}"));
            rows.Add(CsvRow(
                $"Bonus:{bonus.Description}",
                bonus.Number,
                0,
                bonus.Bonus?.TargetAttribute?.Designation ?? string.Empty,
                bonus.Bonus?.Boost?.ConstantValue?.AggregateType.ToString() ?? string.Empty,
                Inv(bonus.Bonus?.Boost?.ConstantValue?.Value ?? 0),
                reqs));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteAncientSets(GameConfiguration config, string path)
    {
        var rows = new List<string> { "Name,AlwaysApplies,CountDistinct,MinimumItemCount,SetLevel,Items,Options" };
        foreach (var g in config.ItemSetGroups.OrderBy(x => x.Name.ToString()))
        {
            var items = string.Join(";", g.Items.Select(i => $"{i.ItemDefinition?.Group}-{i.ItemDefinition?.Number}:{i.BonusOption?.Number}:{i.BonusOption?.SubOptionType}"));
            var opts = g.Options is null
                ? string.Empty
                : string.Join(";", g.Options.PossibleOptions.OrderBy(o => o.Number).Select(o => $"{o.Number}:{o.PowerUpDefinition?.TargetAttribute?.Designation}={Inv(o.PowerUpDefinition?.Boost?.ConstantValue?.Value ?? 0)}"));
            rows.Add(CsvRow(g.Name.ToString(), g.AlwaysApplies ? 1 : 0, g.CountDistinct ? 1 : 0, g.MinimumItemCount, g.SetLevel, items, opts));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteEvents(GameConfiguration config, string path)
    {
        var rows = new List<string> { "Type,GameLevel,Name,EnterDurationSec,GameDurationSec,ExitDurationSec,MaxPlayers,ArePlayerKillersAllowedToEnter,AllowParty" };
        foreach (var e in config.MiniGameDefinitions.OrderBy(x => x.Type).ThenBy(x => x.GameLevel))
        {
            rows.Add(CsvRow(
                e.Type,
                e.GameLevel,
                e.Name.ToString(),
                (int)e.EnterDuration.TotalSeconds,
                (int)e.GameDuration.TotalSeconds,
                (int)e.ExitDuration.TotalSeconds,
                e.MaximumPlayerCount,
                e.ArePlayerKillersAllowedToEnter ? 1 : 0,
                e.AllowParty ? 1 : 0));
        }

        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static void WriteExpAndGlobals(GameConfiguration config, string path)
    {
        var rows = new List<string>
        {
            "Key,Value",
            CsvRow("MaximumLevel", config.MaximumLevel),
            CsvRow("MaximumMasterLevel", config.MaximumMasterLevel),
            CsvRow("ExperienceRate", Inv(config.ExperienceRate)),
            CsvRow("MasterExperienceRate", Inv(config.MasterExperienceRate)),
            CsvRow("ExperienceFormula", config.ExperienceFormula ?? string.Empty),
            CsvRow("MasterExperienceFormula", config.MasterExperienceFormula ?? string.Empty),
            CsvRow("MaximumPartySize", config.MaximumPartySize),
            CsvRow("MinimumMonsterLevelForMasterExperience", config.MinimumMonsterLevelForMasterExperience),
            CsvRow("InfoRange", config.InfoRange),
        };
        File.WriteAllLines(path, rows, Encoding.UTF8);
    }

    private static string Req(Dictionary<string, int> reqs, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (reqs.TryGetValue(key, out var val))
            {
                return val.ToString(CultureInfo.InvariantCulture);
            }
        }

        return string.Empty;
    }

    private static string Inv(float v) => v.ToString("G9", CultureInfo.InvariantCulture);
    private static string Inv(double v) => v.ToString("G15", CultureInfo.InvariantCulture);

    private static string CsvRow(params object?[] cells) => string.Join(",", cells.Select(CsvEscape));

    private static string CsvEscape(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
