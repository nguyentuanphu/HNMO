// <copyright file="IArenaShared.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>
/// What the HNMO Arena shares with the players' home servers, in the shared (Season 6.3) database: each account's arena
/// points (earned in the Arena, spent in the Arena points shop at home) and ranked rating (for the arena titles).
/// </summary>
public interface IArenaShared
{
    /// <summary>Adds points to an account, within a daily cap (Sydney day).</summary>
    /// <param name="login">The login name.</param>
    /// <param name="points">The points to add.</param>
    /// <param name="dailyCap">The most an account earns a day (0: no cap).</param>
    /// <param name="dayKey">The day, e.g. "2026-11-20".</param>
    /// <returns>The points actually added.</returns>
    int AddPoints(string login, int points, int dailyCap, string dayKey);

    /// <summary>Takes points from an account when it has enough.</summary>
    /// <param name="login">The login name.</param>
    /// <param name="points">The points to take.</param>
    /// <returns><c>true</c>, if they were taken.</returns>
    bool TrySpendPoints(string login, int points);

    /// <summary>Gives points back (a purchase that couldn't be delivered).</summary>
    /// <param name="login">The login name.</param>
    /// <param name="points">The points.</param>
    void RefundPoints(string login, int points);

    /// <summary>Gets an account's points.</summary>
    /// <param name="login">The login name.</param>
    /// <returns>The points.</returns>
    int GetPoints(string login);

    /// <summary>Records an account's ranked rating.</summary>
    /// <param name="login">The login name.</param>
    /// <param name="rating">The rating.</param>
    void SetRating(string login, int rating);

    /// <summary>Gets an account's ranked rating (from a copy refreshed every minute), or <c>null</c> without one.</summary>
    /// <param name="login">The login name.</param>
    /// <returns>The rating.</returns>
    int? GetRating(string login);

    /// <summary>
    /// Gets the guild an account's characters belong to on Season 6.3 (the guild war in the Arena fights for it): the one
    /// where the account holds the highest rank, or <c>null</c> without a guild.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <returns>The guild's id and name.</returns>
    (Guid Id, string Name)? GuildOf(string login) => null;

    /// <summary>Publishes one of the Arena's scheduled battles, so that the home servers can show and announce it.</summary>
    /// <param name="kind">The battle ("royale" or "guildwar").</param>
    /// <param name="days">The days (empty: every day).</param>
    /// <param name="times">The Sydney times.</param>
    /// <param name="nextStartUtc">The next start, if one is planned.</param>
    void PublishSchedule(string kind, string days, string times, DateTime? nextStartUtc)
    {
    }

    /// <summary>Gets the Arena's scheduled battles as last published (only those published in the last ten minutes: the Arena runs).</summary>
    /// <returns>The battles.</returns>
    IReadOnlyList<ArenaScheduleEntry> Schedules() => [];

    /// <summary>Owes guild experience to a Season 6.3 guild (the guild war); kept once per reference.</summary>
    /// <param name="reference">The reference (paid once).</param>
    /// <param name="guildId">The guild.</param>
    /// <param name="guildName">The guild's name.</param>
    /// <param name="xp">The experience.</param>
    /// <param name="reason">Why (shown in the logs).</param>
    void OweGuildXp(string reference, Guid guildId, string guildName, int xp, string reason)
    {
    }

    /// <summary>Takes the guild experience owed and not taken yet (each row is taken once, by one program).</summary>
    /// <param name="max">The most rows to take.</param>
    /// <returns>The rows taken.</returns>
    IReadOnlyList<(string Reference, Guid GuildId, string GuildName, int Xp, string Reason)> ClaimGuildXp(int max) => [];

    /// <summary>Publishes who plays on one game server of this program (the rows of that server not in the list are removed).</summary>
    /// <param name="season">This program ("s6", "s9" or "arena").</param>
    /// <param name="serverId">The game server.</param>
    /// <param name="online">The accounts' login names and their characters.</param>
    void PublishOnline(string season, int serverId, IReadOnlyList<(string Login, string Character)> online)
    {
    }

    /// <summary>Gets who plays in the other programs (published in the last two minutes).</summary>
    /// <param name="season">This program.</param>
    /// <returns>The accounts playing elsewhere.</returns>
    IReadOnlyList<OnlinePresence> OnlineElsewhere(string season) => [];

    /// <summary>Leaves a whisper for a character playing in another program.</summary>
    /// <param name="from">The sender's character.</param>
    /// <param name="fromSeason">The sender's program.</param>
    /// <param name="toSeason">The receiver's program.</param>
    /// <param name="toCharacter">The receiver's character.</param>
    /// <param name="text">The text.</param>
    void SendCrossWhisper(string from, string fromSeason, string toSeason, string toCharacter, string text)
    {
    }

    /// <summary>Takes the whispers waiting for this program (each once; older than five minutes are dropped).</summary>
    /// <param name="season">This program.</param>
    /// <returns>The whispers.</returns>
    IReadOnlyList<CrossWhisper> TakeCrossWhispers(string season) => [];
}
