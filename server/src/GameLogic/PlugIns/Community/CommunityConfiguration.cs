// <copyright file="CommunityConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The kinds of automatic posts (to Discord and to the website's recent highlights).
/// </summary>
public enum CommunityPostKind
{
    /// <summary>A rare item dropped by a monster.</summary>
    RareDrop,

    /// <summary>A world boss was killed.</summary>
    WorldBossKill,

    /// <summary>A guild reached a level.</summary>
    GuildLevelUp,

    /// <summary>The weekly guild ladder's winners.</summary>
    GuildLadderWinner,

    /// <summary>A guild won the castle siege.</summary>
    CastleSiegeWinner,
}

/// <summary>
/// A one-time coin reward (linking Discord, joining the Zalo group).
/// </summary>
public class CommunityCoinReward
{
    /// <summary>Gets or sets a value indicating whether the reward is given.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coins, once per account.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; } = 50;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;
}

/// <summary>
/// The setting of one kind of automatic post. The texts take placeholders in braces: {player}, {item}, {monster},
/// {map}, {boss}, {guild}, {level}, {rank}, {points}.
/// </summary>
public class CommunityPostSetting
{
    /// <summary>Gets or sets a value indicating whether this kind is posted at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it's posted to Discord (when the Discord bridge is set up).</summary>
    public bool ToDiscord { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it's shown in the website's recent highlights.</summary>
    public bool ToWebsite { get; set; } = true;

    /// <summary>Gets or sets the English text.</summary>
    [StringLength(300)]
    public string TextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text.</summary>
    [StringLength(300)]
    public string TextVi { get; set; } = string.Empty;
}

/// <summary>
/// Which dropped items count as rare.
/// </summary>
public class CommunityDropFilter
{
    /// <summary>Gets or sets a value indicating whether excellent items count.</summary>
    public bool Excellent { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether ancient (set) items count.</summary>
    public bool Ancient { get; set; } = true;

    /// <summary>Gets or sets the item level from which an item counts; 0 for none.</summary>
    [Range(0, 15)]
    public int MinimumLevel { get; set; }

    /// <summary>Gets or sets the items which always count, as "group number" (e.g. "14 13" for a Jewel of Bless).</summary>
    public ICollection<string> Items { get; set; } = new List<string>();

    /// <summary>Gets or sets the map numbers on which drops are posted; empty for every map.</summary>
    public ICollection<short> Maps { get; set; } = new List<short>();
}

/// <summary>
/// A community event: a code word players enter in the game (once per account), or a list of winning characters, and
/// the reward.
/// </summary>
public class CommunityEventDefinition
{
    /// <summary>Gets or sets the identifier; the entries are kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the event is on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(120)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English description.</summary>
    [StringLength(600)]
    public string DescriptionEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese description.</summary>
    [StringLength(600)]
    public string DescriptionVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the start, in the configured time zone (Sydney), e.g. "2026-11-20 18:00".</summary>
    [StringLength(20)]
    public string Start { get; set; } = string.Empty;

    /// <summary>Gets or sets the end, in the configured time zone (Sydney), e.g. "2026-11-27 23:59".</summary>
    [StringLength(20)]
    public string End { get; set; } = string.Empty;

    /// <summary>Gets or sets the code word players enter while the event runs; empty for none.</summary>
    [StringLength(40)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the winning characters, who get the reward once the event started (each once).</summary>
    public ICollection<string> Winners { get; set; } = new List<string>();

    /// <summary>Gets or sets the reward (coins, zen, items).</summary>
    public MissionReward Reward { get; set; } = new() { Coins = 20 };

    /// <summary>Gets or sets a value indicating whether the event is shown on the website.</summary>
    public bool ShowOnWebsite { get; set; } = true;
}

/// <summary>
/// The configuration of the <see cref="CommunityPlugIn"/>.
/// </summary>
public class CommunityConfiguration
{
    /// <summary>Gets or sets the time zone of the event dates.</summary>
    [StringLength(60)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>
    /// Gets or sets the file in which the state is kept, relative to the server's folder; empty keeps it in memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/community.json";

    /// <summary>Gets or sets the coins for linking the account to Discord (with the bot's /link), once per account and Discord user.</summary>
    public CommunityCoinReward Discord { get; set; } = new();

    /// <summary>Gets or sets the coins for joining the Zalo group, once per account, when the admin approves the claim.</summary>
    public CommunityCoinReward Zalo { get; set; } = new();

    /// <summary>Gets or sets the community events.</summary>
    public ICollection<CommunityEventDefinition> Events { get; set; } = new List<CommunityEventDefinition>();

    /// <summary>Gets or sets which drops count as rare.</summary>
    public CommunityDropFilter Drops { get; set; } = new();

    /// <summary>Gets or sets the rare drop posts.</summary>
    public CommunityPostSetting RareDrop { get; set; } = new()
    {
        TextEn = "{player} found {item} ({monster}, {map})!",
        TextVi = "{player} đã nhặt được {item} ({monster}, {map})!",
    };

    /// <summary>Gets or sets the world boss kill posts (Discord already gets the world bosses' own announcements, so it's off there by default).</summary>
    public CommunityPostSetting WorldBossKill { get; set; } = new()
    {
        ToDiscord = false,
        TextEn = "{boss} was defeated! Top damage: {player}.",
        TextVi = "{boss} đã bị hạ gục! Sát thương cao nhất: {player}.",
    };

    /// <summary>Gets or sets the guild level-up posts.</summary>
    public CommunityPostSetting GuildLevelUp { get; set; } = new()
    {
        TextEn = "The guild {guild} reached level {level}!",
        TextVi = "Bang hội {guild} đã lên cấp {level}!",
    };

    /// <summary>Gets or sets the weekly guild ladder winner posts.</summary>
    public CommunityPostSetting GuildLadderWinner { get; set; } = new()
    {
        TextEn = "Guild ladder of the week: {guild} is rank {rank} with {points} points!",
        TextVi = "Bảng xếp hạng bang hội của tuần: {guild} hạng {rank} với {points} điểm!",
    };

    /// <summary>Gets or sets the castle siege winner posts (the Discord bridge's own game events post the siege's end too, so it's off there by default).</summary>
    public CommunityPostSetting CastleSiegeWinner { get; set; } = new()
    {
        ToDiscord = false,
        TextEn = "The guild {guild} won the castle siege!",
        TextVi = "Bang hội {guild} đã chiến thắng công thành chiến!",
    };

    /// <summary>Gets or sets how many ladder ranks are posted (from the top).</summary>
    [Range(1, 10)]
    public int LadderRanksPosted { get; set; } = 3;

    /// <summary>Gets or sets how many highlights the website shows.</summary>
    [Range(1, 100)]
    public int HighlightsShown { get; set; } = 15;

    /// <summary>Gets or sets the shortest time between two rare drop posts of the same player, in seconds (against floods).</summary>
    [Range(0, 3600)]
    public int DropCooldownSeconds { get; set; } = 30;

    /// <summary>Gets the setting of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The setting.</returns>
    public CommunityPostSetting Post(CommunityPostKind kind) => kind switch
    {
        CommunityPostKind.RareDrop => this.RareDrop,
        CommunityPostKind.WorldBossKill => this.WorldBossKill,
        CommunityPostKind.GuildLevelUp => this.GuildLevelUp,
        CommunityPostKind.GuildLadderWinner => this.GuildLadderWinner,
        _ => this.CastleSiegeWinner,
    };

    /// <summary>Gets a value indicating whether any kind is posted.</summary>
    [JsonIgnore]
    public bool PostsAnything => Enum.GetValues<CommunityPostKind>().Any(k => this.Post(k).Enabled);
}
