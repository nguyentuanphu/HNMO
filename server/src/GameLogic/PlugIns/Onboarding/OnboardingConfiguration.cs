// <copyright file="OnboardingConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What a step of the new-player guide asks for. The server checks each one itself.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnboardingStepKind>))]
public enum OnboardingStepKind
{
    /// <summary>Talk to an NPC (<see cref="OnboardingStep.NpcNumber"/>; 0 is any NPC).</summary>
    TalkToNpc,

    /// <summary>Enter a map (<see cref="OnboardingStep.MapNumber"/>).</summary>
    EnterMap,

    /// <summary>Walk to a spot of a map (<see cref="OnboardingStep.MapNumber"/>, <see cref="OnboardingStep.X"/>, <see cref="OnboardingStep.Y"/>, <see cref="OnboardingStep.Radius"/>).</summary>
    WalkTo,

    /// <summary>Kill <see cref="OnboardingStep.Target"/> monsters, optionally of one monster number and/or on one map.</summary>
    KillMonsters,

    /// <summary>Use (drink, eat, read) <see cref="OnboardingStep.Target"/> items of a group and number.</summary>
    UseItem,

    /// <summary>Hit something <see cref="OnboardingStep.Target"/> times with a skill (<see cref="OnboardingStep.SkillNumber"/>; 0 is any skill).</summary>
    UseSkill,

    /// <summary>Learn <see cref="OnboardingStep.Target"/> new skills.</summary>
    LearnSkill,

    /// <summary>Reach level <see cref="OnboardingStep.Target"/>.</summary>
    ReachLevel,

    /// <summary>Be in a party.</summary>
    JoinParty,

    /// <summary>Add <see cref="OnboardingStep.Target"/> stat points.</summary>
    AddStatPoints,

    /// <summary>Open the cash shop.</summary>
    OpenCashShop,

    /// <summary>Open the missions window of the web game.</summary>
    OpenMissions,
}

/// <summary>
/// A step of the new-player guide.
/// </summary>
public class OnboardingStep
{
    /// <summary>Gets or sets the identifier; the progress is kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the step is part of the guide.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the player may skip this step alone (it gives no reward then).</summary>
    public bool Optional { get; set; }

    /// <summary>Gets or sets what the step asks for.</summary>
    public OnboardingStepKind Kind { get; set; }

    /// <summary>Gets or sets the count, level or points to reach.</summary>
    [Range(1, 2_000_000_000)]
    public int Target { get; set; } = 1;

    /// <summary>Gets or sets the NPC number of <see cref="OnboardingStepKind.TalkToNpc"/>; 0 is any NPC.</summary>
    [Range(0, 10000)]
    public short NpcNumber { get; set; }

    /// <summary>Gets or sets the map of <see cref="OnboardingStepKind.EnterMap"/> and <see cref="OnboardingStepKind.WalkTo"/>,
    /// or where kills count for <see cref="OnboardingStepKind.KillMonsters"/>; -1 is every map.</summary>
    [Range(-1, 255)]
    public short MapNumber { get; set; } = -1;

    /// <summary>Gets or sets the x coordinate of <see cref="OnboardingStepKind.WalkTo"/>.</summary>
    [Range(0, 255)]
    public byte X { get; set; }

    /// <summary>Gets or sets the y coordinate of <see cref="OnboardingStepKind.WalkTo"/>.</summary>
    [Range(0, 255)]
    public byte Y { get; set; }

    /// <summary>Gets or sets how near to the spot counts for <see cref="OnboardingStepKind.WalkTo"/>.</summary>
    [Range(0, 50)]
    public byte Radius { get; set; } = 4;

    /// <summary>Gets or sets the monster number which counts for <see cref="OnboardingStepKind.KillMonsters"/>; 0 counts every monster.</summary>
    [Range(0, 10000)]
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets the item group of <see cref="OnboardingStepKind.UseItem"/>; -1 is any group.</summary>
    [Range(-1, 15)]
    public short ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets the item number of <see cref="OnboardingStepKind.UseItem"/>; -1 is any number of the group.</summary>
    [Range(-1, 511)]
    public short ItemNumber { get; set; } = -1;

    /// <summary>Gets or sets the skill number of <see cref="OnboardingStepKind.UseSkill"/>; 0 is any skill.</summary>
    [Range(0, 1000)]
    public short SkillNumber { get; set; }

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(120)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English hint (how to do it).</summary>
    [StringLength(300)]
    public string HintEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese hint.</summary>
    [StringLength(300)]
    public string HintVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward of the step.</summary>
    public MissionReward Reward { get; set; } = new();
}

/// <summary>
/// An item of the starter kit.
/// </summary>
public class StarterKitItem
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; }

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>Gets or sets how many of the item are given (separate pieces).</summary>
    [Range(1, 50)]
    public int Count { get; set; } = 1;

    /// <summary>Gets or sets the durability of a piece; for stackable items (potions, arrows) the stack size. 0 is full.</summary>
    [Range(0, 255)]
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has its skill (when it has one).</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck (when it can).</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets the level of the item's additional option (0-4, when it can have one).</summary>
    [Range(0, 7)]
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets the excellent options as bits (1-63; 0 is none).</summary>
    [Range(0, 63)]
    public byte ExcellentOptions { get; set; }

    /// <summary>Gets or sets the character class number which gets it (a class a new character can have); -1 is every class.</summary>
    [Range(-1, 255)]
    public short ClassNumber { get; set; } = -1;
}

/// <summary>
/// The temporary EXP buff of the starter kit: the effect of an item of the game's data (by default the Seal of Ascension).
/// </summary>
public class StarterKitBuffConfiguration
{
    /// <summary>Gets or sets a value indicating whether the kit gives the buff.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the group of the item whose effect is given.</summary>
    [Range(0, 15)]
    public short ItemGroup { get; set; } = 13;

    /// <summary>Gets or sets the number of the item whose effect is given.</summary>
    [Range(0, 511)]
    public short ItemNumber { get; set; } = 43;

    /// <summary>Gets or sets how long it lasts, in minutes.</summary>
    [Range(1, 10080)]
    public int Minutes { get; set; } = 60;
}

/// <summary>
/// The starter kit, given once to a new character (or account) at its first login; each piece can be switched off.
/// </summary>
public class StarterKitConfiguration
{
    /// <summary>Gets or sets a value indicating whether the starter kit is given.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it's given once per account (to its first new character) instead of once per character.</summary>
    public bool PerAccount { get; set; }

    /// <summary>Gets or sets a value indicating whether the items are given.</summary>
    public bool ItemsEnabled { get; set; } = true;

    /// <summary>Gets or sets the items; an item with a class number only goes to that class.</summary>
    public ICollection<StarterKitItem> Items { get; set; } = CreateDefaultItems();

    /// <summary>Gets or sets a value indicating whether the zen is given.</summary>
    public bool ZenEnabled { get; set; } = true;

    /// <summary>Gets or sets the zen.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; } = 50_000;

    /// <summary>Gets or sets a value indicating whether the coins are given.</summary>
    public bool CoinsEnabled { get; set; } = true;

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; } = 20;

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether extra stat points are given (when the character is created).</summary>
    public bool PointsEnabled { get; set; }

    /// <summary>Gets or sets the extra stat points (a few levels' worth).</summary>
    [Range(0, 10_000)]
    public int LevelUpPoints { get; set; } = 15;

    /// <summary>Gets or sets the temporary EXP buff.</summary>
    public StarterKitBuffConfiguration ExperienceBuff { get; set; } = new();

    /// <summary>
    /// Creates the default items: potions and a town portal scroll for everyone, a first weapon for the
    /// three classic classes.
    /// </summary>
    /// <returns>The items.</returns>
    public static List<StarterKitItem> CreateDefaultItems() =>
    [
        new() { Group = 14, Number = 1, Count = 3, Durability = 3 },
        new() { Group = 14, Number = 4, Count = 2, Durability = 3 },
        new() { Group = 14, Number = 10, Count = 1 },
        new() { Group = 5, Number = 0, Level = 3, Skill = false, Luck = true, ClassNumber = 0 },
        new() { Group = 0, Number = 1, Level = 3, Skill = true, Luck = true, ClassNumber = 4 },
        new() { Group = 4, Number = 0, Level = 3, Skill = true, Luck = true, ClassNumber = 8 },
        new() { Group = 4, Number = 15, Count = 1, Durability = 255, ClassNumber = 8 },
    ];
}

/// <summary>
/// The configuration of the <see cref="OnboardingPlugIn"/>: the steps of the new-player guide with their rewards, the
/// final reward and the starter kit.
/// </summary>
public class OnboardingConfiguration
{
    /// <summary>Gets or sets a value indicating whether the guide is shown to new characters.</summary>
    public bool GuideEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the highest level of a character which counts as new when it's seen for the first time (characters created
    /// after the plug-in was turned on always count); 0: only characters created after it.
    /// </summary>
    [Range(0, 1000)]
    public int NewCharacterMaxLevel { get; set; } = 10;

    /// <summary>Gets or sets the steps, in their order.</summary>
    public ICollection<OnboardingStep> Steps { get; set; } = CreateDefaultSteps();

    /// <summary>Gets or sets the reward when every step is done.</summary>
    public MissionReward FinalReward { get; set; } = new()
    {
        Zen = 200_000,
        Coins = 50,
        Items = new List<MissionItemReward> { new() { Group = 14, Number = 13, Count = 1 } },
    };

    /// <summary>Gets or sets the starter kit.</summary>
    public StarterKitConfiguration StarterKit { get; set; } = new();

    /// <summary>
    /// Gets or sets the file which keeps the progress, relative to the server's folder (by default in the save folder,
    /// next to the database); empty keeps it in memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/onboarding.json";

    /// <summary>
    /// Gets the steps which are part of the guide, in their order.
    /// </summary>
    /// <returns>The steps.</returns>
    public IEnumerable<OnboardingStep> EnabledSteps() => this.Steps.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Id));

    /// <summary>
    /// Creates the default steps: a merchant, the first monsters, a potion, levels, stat points, a skill, the missions
    /// window, a party and the cash shop. Steps which need another player or a class skill are optional.
    /// </summary>
    /// <returns>The steps.</returns>
    public static List<OnboardingStep> CreateDefaultSteps()
    {
        static MissionReward Zen(int zen, int coins = 0, params MissionItemReward[] items) => new() { Zen = zen, Coins = coins, Items = items.ToList() };
        static MissionItemReward Item(short group, short number, int count = 1) => new() { Group = group, Number = number, Count = count };
        return
        [
            new()
            {
                Id = "talk-merchant", Kind = OnboardingStepKind.TalkToNpc, Target = 1,
                TitleEn = "Talk to a merchant", TitleVi = "Nói chuyện với một thương nhân",
                HintEn = "Click an NPC in town; the arrow points to the nearest merchant. Merchants sell potions, weapons and armour.",
                HintVi = "Bấm vào một NPC trong thành; mũi tên chỉ tới thương nhân gần nhất. Thương nhân bán bình máu, vũ khí và giáp.",
                Reward = Zen(10_000),
            },
            new()
            {
                Id = "kill-10", Kind = OnboardingStepKind.KillMonsters, Target = 10,
                TitleEn = "Defeat 10 monsters", TitleVi = "Hạ gục 10 quái vật",
                HintEn = "Walk out of town and click a monster to attack it. Keep clicking to keep fighting.",
                HintVi = "Đi ra khỏi thành và bấm vào quái vật để tấn công. Bấm tiếp để tiếp tục đánh.",
                Reward = Zen(20_000, 0, Item(14, 1, 2)),
            },
            new()
            {
                Id = "use-potion", Kind = OnboardingStepKind.UseItem, Target = 1, ItemGroup = 14,
                TitleEn = "Drink a potion", TitleVi = "Uống một bình thuốc",
                HintEn = "Right-click a potion in your bag (I), or press Q, W, E or R.",
                HintVi = "Bấm chuột phải vào bình thuốc trong túi đồ (I), hoặc nhấn Q, W, E hay R.",
                Reward = Zen(10_000),
            },
            new()
            {
                Id = "level-5", Kind = OnboardingStepKind.ReachLevel, Target = 5,
                TitleEn = "Reach level 5", TitleVi = "Đạt cấp 5",
                HintEn = "Defeat monsters to gain experience.",
                HintVi = "Hạ gục quái vật để nhận kinh nghiệm.",
                Reward = Zen(30_000, 5),
            },
            new()
            {
                Id = "add-points", Kind = OnboardingStepKind.AddStatPoints, Target = 5,
                TitleEn = "Add 5 stat points", TitleVi = "Cộng 5 điểm",
                HintEn = "Open the character window (C) and click + next to a stat.",
                HintVi = "Mở cửa sổ nhân vật (C) và bấm + bên cạnh một chỉ số.",
                Reward = Zen(20_000),
            },
            new()
            {
                Id = "use-skill", Kind = OnboardingStepKind.UseSkill, Target = 3, Optional = true,
                TitleEn = "Hit monsters 3 times with a skill", TitleVi = "Dùng kỹ năng đánh quái 3 lần",
                HintEn = "Put a skill on the keys 1-5, choose it and right-click a monster.",
                HintVi = "Gán kỹ năng vào phím 1-5, chọn nó rồi bấm chuột phải vào quái vật.",
                Reward = Zen(20_000),
            },
            new()
            {
                Id = "open-missions", Kind = OnboardingStepKind.OpenMissions, Target = 1, Optional = true,
                TitleEn = "Look at the daily missions", TitleVi = "Xem nhiệm vụ hằng ngày",
                HintEn = "Press J: daily and weekly missions give zen, items and coins.",
                HintVi = "Nhấn J: nhiệm vụ hằng ngày và hằng tuần cho Zen, vật phẩm và xu.",
                Reward = Zen(10_000, 5),
            },
            new()
            {
                Id = "join-party", Kind = OnboardingStepKind.JoinParty, Target = 1, Optional = true,
                TitleEn = "Join a party", TitleVi = "Vào một tổ đội",
                HintEn = "Press P, or click another player and ask them to party. Hunting together gives more experience.",
                HintVi = "Nhấn P, hoặc bấm vào người chơi khác để mời vào tổ đội. Săn cùng nhau được nhiều kinh nghiệm hơn.",
                Reward = Zen(20_000, 5),
            },
            new()
            {
                Id = "level-20", Kind = OnboardingStepKind.ReachLevel, Target = 20,
                TitleEn = "Reach level 20", TitleVi = "Đạt cấp 20",
                HintEn = "Hunt stronger monsters a little further from town.",
                HintVi = "Săn quái mạnh hơn ở xa thành hơn một chút.",
                Reward = Zen(50_000, 10),
            },
            new()
            {
                Id = "open-cash-shop", Kind = OnboardingStepKind.OpenCashShop, Target = 1, Optional = true,
                TitleEn = "Open the cash shop", TitleVi = "Mở cửa hàng xu",
                HintEn = "Press X in town. Coins come from missions, events, bosses and playing time.",
                HintVi = "Nhấn X trong thành. Xu có từ nhiệm vụ, sự kiện, boss và thời gian chơi.",
                Reward = Zen(10_000),
            },
            new()
            {
                Id = "walk-fountain", Enabled = false, Kind = OnboardingStepKind.WalkTo, MapNumber = 0, X = 135, Y = 128, Radius = 5,
                TitleEn = "Walk to the fountain of Lorencia", TitleVi = "Đi tới đài phun nước ở Lorencia",
                HintEn = "Follow the arrow.", HintVi = "Đi theo mũi tên.",
                Reward = Zen(5_000),
            },
            new()
            {
                Id = "enter-noria", Enabled = false, Kind = OnboardingStepKind.EnterMap, MapNumber = 3,
                TitleEn = "Visit Noria", TitleVi = "Ghé thăm Noria",
                HintEn = "Press M and choose Noria.", HintVi = "Nhấn M và chọn Noria.",
                Reward = Zen(5_000),
            },
        ];
    }
}
