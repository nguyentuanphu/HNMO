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

    /// <summary>A top race started.</summary>
    TopRaceStarted,

    /// <summary>A top race's winners.</summary>
    TopRaceWinner,
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

    /// <summary>Gets or sets the posts of a top race's start.</summary>
    public CommunityPostSetting TopRaceStarted { get; set; } = new()
    {
        TextEn = "The top race \"{race}\" has started! It ends {end} (Sydney time).",
        TextVi = "Đua top \"{race}\" đã bắt đầu! Kết thúc lúc {end} (giờ Sydney).",
    };

    /// <summary>Gets or sets the posts of a top race's winners.</summary>
    public CommunityPostSetting TopRaceWinner { get; set; } = new()
    {
        TextEn = "Top race \"{race}\": {player} is rank {rank} with {value}!",
        TextVi = "Đua top \"{race}\": {player} đạt hạng {rank} với {value}!",
    };

    /// <summary>Gets or sets how many ranks of each top race table are posted (from the top).</summary>
    [Range(1, 10)]
    public int TopRaceRanksPosted { get; set; } = 3;

    /// <summary>Gets or sets how many ladder ranks are posted (from the top).</summary>
    [Range(1, 10)]
    public int LadderRanksPosted { get; set; } = 3;

    /// <summary>Gets or sets how many highlights the website shows.</summary>
    [Range(1, 100)]
    public int HighlightsShown { get; set; } = 15;

    /// <summary>Gets or sets the shortest time between two rare drop posts of the same player, in seconds (against floods).</summary>
    [Range(0, 3600)]
    public int DropCooldownSeconds { get; set; } = 30;

    /// <summary>Gets a value indicating whether any kind is posted.</summary>
    [JsonIgnore]
    public bool PostsAnything => Enum.GetValues<CommunityPostKind>().Any(k => this.Post(k).Enabled);

    /// <summary>Gets the setting of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The setting.</returns>
    public CommunityPostSetting Post(CommunityPostKind kind) => kind switch
    {
        CommunityPostKind.RareDrop => this.RareDrop,
        CommunityPostKind.WorldBossKill => this.WorldBossKill,
        CommunityPostKind.GuildLevelUp => this.GuildLevelUp,
        CommunityPostKind.GuildLadderWinner => this.GuildLadderWinner,
        CommunityPostKind.TopRaceStarted => this.TopRaceStarted,
        CommunityPostKind.TopRaceWinner => this.TopRaceWinner,
        _ => this.CastleSiegeWinner,
    };
}
