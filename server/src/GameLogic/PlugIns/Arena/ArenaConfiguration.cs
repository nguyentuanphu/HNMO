// <copyright file="ArenaConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>The configuration of the <see cref="ArenaPlugIn"/>: the arena map and the arena character of every class.</summary>
public sealed class ArenaConfiguration
{
    /// <summary>Gets or sets the map arena characters play on (6: the Arena).</summary>
    [Range(0, 1000)]
    public short MapNumber { get; set; } = 6;

    /// <summary>Gets or sets the HNMO state file of the Arena's ranked records (in the Arena's own save folder).</summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets the arena points a ranked 1v1 win gives (owner's choice 2026-11-20: 5).</summary>
    [Range(0, 1000)]
    public int PointsPerWin { get; set; } = 5;

    /// <summary>Gets or sets the arena points a ranked 1v1 loss gives (owner's choice 2026-11-20: 2).</summary>
    [Range(0, 1000)]
    public int PointsPerLoss { get; set; } = 2;

    /// <summary>Gets or sets the most arena points an account earns a day (Sydney day; 0: no cap), against farming.</summary>
    [Range(0, 100000)]
    public int DailyPointsCap { get; set; } = 100;

    /// <summary>Gets or sets how many large healing potions every arena character carries.</summary>
    [Range(0, 255)]
    public byte HealingPotions { get; set; } = 100;

    /// <summary>Gets or sets where arena characters stand on the arena map (enter, come back, respawn): X1. The map's own gates lie partly on blocked tiles.</summary>
    [Range(0, 255)]
    public byte HomeX1 { get; set; } = 95;

    /// <summary>Gets or sets where arena characters stand on the arena map (enter, come back, respawn): Y1. The map's own gates lie partly on blocked tiles.</summary>
    [Range(0, 255)]
    public byte HomeY1 { get; set; } = 105;

    /// <summary>Gets or sets where arena characters stand on the arena map (enter, come back, respawn): X2. The map's own gates lie partly on blocked tiles.</summary>
    [Range(0, 255)]
    public byte HomeX2 { get; set; } = 100;

    /// <summary>Gets or sets where arena characters stand on the arena map (enter, come back, respawn): Y2. The map's own gates lie partly on blocked tiles.</summary>
    [Range(0, 255)]
    public byte HomeY2 { get; set; } = 110;

    /// <summary>Gets or sets the map 5v5 team battles play on (owner's choice 2026-11-20: 30, Valley of Loren).</summary>
    [Range(0, 1000)]
    public short TeamMapNumber { get; set; } = 30;

    /// <summary>Gets or sets the players a 5v5 team has at most (owner's choice 2026-11-20: 5).</summary>
    [Range(1, 20)]
    public int TeamSize { get; set; } = 5;

    /// <summary>Gets or sets the fewest players a team may start a battle with when the wait is long (3: a 3v3).</summary>
    [Range(1, 20)]
    public int TeamMinSize { get; set; } = 3;

    /// <summary>Gets or sets the kills that win a team battle at once (owner's choice: 30).</summary>
    [Range(1, 1000)]
    public int TeamKillsToWin { get; set; } = 30;

    /// <summary>Gets or sets how many minutes a team battle lasts before the most kills wins (owner's choice: 10).</summary>
    [Range(1, 120)]
    public int TeamMinutes { get; set; } = 10;

    /// <summary>Gets or sets the seconds a fallen player waits before coming back at their team's gate (owner's choice: 5).</summary>
    [Range(0, 120)]
    public int TeamRespawnSeconds { get; set; } = 5;

    /// <summary>Gets or sets the arena points a 5v5 win gives (owner's choice 2026-11-20: 8).</summary>
    [Range(0, 1000)]
    public int TeamPointsPerWin { get; set; } = 8;

    /// <summary>Gets or sets the arena points a 5v5 loss gives (owner's choice 2026-11-20: 3).</summary>
    [Range(0, 1000)]
    public int TeamPointsPerLoss { get; set; } = 3;

    /// <summary>Gets or sets the red team's gate on the battle map: X1 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamAX1 { get; set; } = 60;

    /// <summary>Gets or sets the red team's gate on the battle map: Y1 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamAY1 { get; set; } = 24;

    /// <summary>Gets or sets the red team's gate on the battle map: X2 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamAX2 { get; set; } = 66;

    /// <summary>Gets or sets the red team's gate on the battle map: Y2 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamAY2 { get; set; } = 30;

    /// <summary>Gets or sets the blue team's gate on the battle map: X1 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamBX1 { get; set; } = 112;

    /// <summary>Gets or sets the blue team's gate on the battle map: Y1 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamBY1 { get; set; } = 24;

    /// <summary>Gets or sets the blue team's gate on the battle map: X2 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamBX2 { get; set; } = 118;

    /// <summary>Gets or sets the blue team's gate on the battle map: Y2 (where it starts and comes back).</summary>
    [Range(0, 255)]
    public byte TeamBY2 { get; set; } = 30;

    /// <summary>Gets or sets the map the battle royale is fought on (owner's choice 2026-11-20: 0, Lorencia).</summary>
    [Range(0, 1000)]
    public short RoyaleMapNumber { get; set; }

    /// <summary>Gets or sets the Sydney times battle royale matches start at, e.g. "12:00, 20:00, 22:00" (owner's choice: set times).</summary>
    public string RoyaleTimes { get; set; } = "12:00, 20:00, 22:00";

    /// <summary>Gets or sets the minutes before a start the sign-up opens (and the first notice goes out 5 minutes before that).</summary>
    [Range(1, 60)]
    public int RoyaleSignupMinutes { get; set; } = 5;

    /// <summary>Gets or sets the fewest players a battle royale starts with (fewer: it's called off).</summary>
    [Range(2, 200)]
    public int RoyaleMinPlayers { get; set; } = 3;

    /// <summary>Gets or sets the most players a battle royale takes.</summary>
    [Range(2, 200)]
    public int RoyaleMaxPlayers { get; set; } = 50;

    /// <summary>Gets or sets the longest a battle royale lasts in minutes (then the survivors are placed by kills and health).</summary>
    [Range(2, 120)]
    public int RoyaleMinutes { get; set; } = 12;

    /// <summary>Gets or sets the minutes before the safe zone starts to shrink.</summary>
    [Range(0, 60)]
    public int RoyaleGraceMinutes { get; set; } = 2;

    /// <summary>Gets or sets the minutes the safe zone takes to shrink to its final size.</summary>
    [Range(1, 120)]
    public int RoyaleShrinkMinutes { get; set; } = 8;

    /// <summary>Gets or sets the zone's centre at first (x); the default is Lorencia's middle.</summary>
    [Range(0, 255)]
    public byte RoyaleCentreX { get; set; } = 128;

    /// <summary>Gets or sets the zone's centre at first (y).</summary>
    [Range(0, 255)]
    public byte RoyaleCentreY { get; set; } = 128;

    /// <summary>Gets or sets the zone's radius at first (tiles).</summary>
    [Range(5, 255)]
    public int RoyaleStartRadius { get; set; } = 120;

    /// <summary>Gets or sets the zone's final radius (tiles).</summary>
    [Range(1, 255)]
    public int RoyaleFinalRadius { get; set; } = 6;

    /// <summary>Gets or sets the share of health (%) lost each second outside the zone at the start.</summary>
    [Range(0, 100)]
    public double RoyaleFirstDamagePercent { get; set; } = 2;

    /// <summary>Gets or sets the share of health (%) lost each second outside the zone at the end.</summary>
    [Range(0, 100)]
    public double RoyaleLastDamagePercent { get; set; } = 10;

    /// <summary>Gets or sets the winner's arena points (owner's choice 2026-11-20: 20).</summary>
    [Range(0, 1000)]
    public int RoyalePointsWinner { get; set; } = 20;

    /// <summary>Gets or sets everyone else's arena points (owner's choice 2026-11-20: 2).</summary>
    [Range(0, 1000)]
    public int RoyalePointsOthers { get; set; } = 2;

    /// <summary>Gets or sets the days of the guild war (owner's choice 2026-11-20: Saturday and Sunday).</summary>
    public string GuildWarDays { get; set; } = "Sat, Sun";

    /// <summary>Gets or sets the Sydney times of the guild war on those days (owner's choice: 21:00).</summary>
    public string GuildWarTimes { get; set; } = "21:00";

    /// <summary>Gets or sets the minutes before the start the sign-up closes (the owner's "until 20:50").</summary>
    [Range(0, 120)]
    public int GuildWarSignupCloseMinutes { get; set; } = 10;

    /// <summary>Gets or sets how long a guild war match lasts in minutes (owner's choice: 15).</summary>
    [Range(2, 120)]
    public int GuildWarMinutes { get; set; } = 15;

    /// <summary>Gets or sets the fewest members of a guild that must be in the Arena at the start (owner's choice: 5).</summary>
    [Range(1, 50)]
    public int GuildWarMinMembers { get; set; } = 5;

    /// <summary>Gets or sets the most members of a guild that fight (owner's choice: 10).</summary>
    [Range(1, 50)]
    public int GuildWarMaxMembers { get; set; } = 10;

    /// <summary>Gets or sets the capture points on the battle map, "x,y; x,y" (west, middle and east of the Valley of Loren).</summary>
    public string GuildWarFlags { get; set; } = "79,27; 89,27; 100,33";

    /// <summary>Gets or sets how near (tiles) a fighter must stand to count at a flag.</summary>
    [Range(1, 20)]
    public int GuildWarFlagRadius { get; set; } = 3;

    /// <summary>Gets or sets the seconds a guild must hold a flag alone to capture it.</summary>
    [Range(1, 120)]
    public int GuildWarCaptureSeconds { get; set; } = 5;

    /// <summary>Gets or sets every how many seconds a held flag gives its guild a point (a kill is a point too).</summary>
    [Range(1, 600)]
    public int GuildWarFlagPointSeconds { get; set; } = 10;

    /// <summary>Gets or sets the arena points of every member of the winning guild (owner's choice 2026-11-20: 15).</summary>
    [Range(0, 1000)]
    public int GuildWarPointsWin { get; set; } = 15;

    /// <summary>Gets or sets the arena points of every member of the losing guild, and of both in a draw (owner's choice: 4).</summary>
    [Range(0, 1000)]
    public int GuildWarPointsLoss { get; set; } = 4;

    /// <summary>Gets or sets the guild experience of the winning guild of a guild war (owner's choice 2026-11-21: 500).</summary>
    [Range(0, 1000000)]
    public int GuildWarXpWin { get; set; } = 500;

    /// <summary>Gets or sets the guild experience of the losing guild, and of both in a draw (owner's choice: 150).</summary>
    [Range(0, 1000000)]
    public int GuildWarXpLoss { get; set; } = 150;

    /// <summary>Gets or sets the extra guild experience of the week's guild war champion (owner's choice: 1000).</summary>
    [Range(0, 1000000)]
    public int GuildWarXpChampion { get; set; } = 1000;

    /// <summary>The battle royale's match settings.</summary>
    /// <returns>The settings.</returns>
    public ArenaRoyaleEngine.Settings RoyaleSettings() =>
        new(this.RoyaleMinutes, this.RoyaleGraceMinutes, this.RoyaleShrinkMinutes, this.RoyaleStartRadius, this.RoyaleFinalRadius, this.RoyaleFirstDamagePercent, this.RoyaleLastDamagePercent, this.RoyalePointsWinner, this.RoyalePointsOthers);

    /// <summary>The gate arena characters stand at on the arena map, if the map exists.</summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The gate, or <c>null</c>.</returns>
    public ExitGate? HomeGate(GameConfiguration gameConfiguration)
    {
        ArgumentNullException.ThrowIfNull(gameConfiguration);
        return gameConfiguration.Maps.FirstOrDefault(m => m.Number == this.MapNumber) is { } map
            ? new ExitGate { Map = map, X1 = this.HomeX1, Y1 = this.HomeY1, X2 = this.HomeX2, Y2 = this.HomeY2, Direction = Direction.South, IsSpawnGate = true }
            : null;
    }

    /// <summary>Gets or sets the arena character of every class.</summary>
    public List<ArenaClassTemplate> Templates { get; set; } = DefaultTemplates();

    /// <summary>The default arena characters: level 300, a +9 weapon and armour set of their class with luck, skill and +8.</summary>
    /// <returns>The templates.</returns>
    public static List<ArenaClassTemplate> DefaultTemplates()
    {
        static ArenaTemplateItem Item(byte slot, byte group, short number, bool skill = false) =>
            new() { Slot = slot, Group = group, Number = number, Level = 9, Luck = true, Skill = skill, OptionLevel = 2 };
        static List<ArenaTemplateItem> Set(short number, bool helm = true)
        {
            var items = new List<ArenaTemplateItem>();
            if (helm)
            {
                items.Add(Item(2, 7, number));
            }

            items.Add(Item(3, 8, number));
            items.Add(Item(4, 9, number));
            items.Add(Item(5, 10, number));
            items.Add(Item(6, 11, number));
            return items;
        }

        return
        [
            new() { ClassFamily = 0, Name = "Dark Wizard", Level = 300, Strength = 100, Agility = 300, Vitality = 300, Energy = 1300, Items = [Item(0, 5, 8, true), .. Set(18)] },
            new() { ClassFamily = 1, Name = "Dark Knight", Level = 300, Strength = 1100, Agility = 450, Vitality = 450, Energy = 100, Items = [Item(0, 0, 16, true), .. Set(16)] },
            new() { ClassFamily = 2, Name = "Fairy Elf", Level = 300, Strength = 200, Agility = 1100, Vitality = 400, Energy = 300, Items = [Item(1, 4, 17, true), new() { Slot = 0, Group = 4, Number = 15, Durability = 255 }, .. Set(19)] },
            new() { ClassFamily = 3, Name = "Magic Gladiator", Level = 300, Strength = 700, Agility = 400, Vitality = 350, Energy = 650, Items = [Item(0, 0, 31, true), .. Set(20, helm: false)] },
            new() { ClassFamily = 4, Name = "Dark Lord", Level = 300, Strength = 700, Agility = 350, Vitality = 300, Energy = 200, Leadership = 550, Items = [Item(0, 2, 10, true), .. Set(27)] },
            new() { ClassFamily = 5, Name = "Summoner", Level = 300, Strength = 100, Agility = 300, Vitality = 300, Energy = 1300, Items = [Item(0, 5, 18, true), .. Set(24)] },
            new() { ClassFamily = 6, Name = "Rage Fighter", Level = 300, Strength = 900, Agility = 300, Vitality = 700, Energy = 100, Items = [Item(0, 0, 33, true), .. Set(59)] },
        ];
    }
}
