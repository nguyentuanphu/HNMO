// <copyright file="TopUpMilestoneStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Writes plain-ASCII JSON payloads for <see cref="TopUpMilestonesPlugIn"/> (game packet and website API).
/// </summary>
public static class TopUpMilestoneStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>
    /// Writes the public milestones summary for the website home page (no account data).
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="itemNames">Lookup for item display names.</param>
    /// <returns>The JSON string.</returns>
    public static string WritePublic(TopUpMilestoneEngine engine, Func<short, short, string?> itemNames)
    {
        var steps = engine.OrderedSteps().Select(s => SerializeStep(s, reached: false, claimed: false, claimable: false, itemNames)).ToList();
        var state = new
        {
            v = 1,
            enabled = engine.Enabled,
            allowWebsite = engine.Configuration.AllowWebsite,
            timeZone = engine.TimeZone.Id,
            steps,
        };
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    /// <summary>
    /// Writes the full state for an authenticated account (web game packet or website account endpoint).
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="loginName">The account login name.</param>
    /// <param name="itemNames">Lookup for item display names.</param>
    /// <param name="actionResult">Optional result of the triggering action.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>The JSON string.</returns>
    public static string WriteAccountState(
        TopUpMilestoneEngine engine,
        string loginName,
        Func<short, short, string?> itemNames,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var (totalVnd, nextThresholdVnd, steps, owed, history) = engine.SnapshotFor(loginName);
        var zone = engine.TimeZone;

        var state = new
        {
            v = 1,
            enabled = engine.Enabled,
            allowWebsite = engine.Configuration.AllowWebsite,
            totalVnd,
            nextThresholdVnd,
            steps = steps.Select(p => SerializeStep(p.Step, p.Reached, p.Claimed, p.Claimable, itemNames)).ToList(),
            owed = owed.Select(o => new
            {
                reference = o.Reference,
                stepId = o.Code,
                zen = o.Zen,
                items = o.Items.Select(i => new
                {
                    group = i.Group,
                    number = i.Number,
                    level = i.Level,
                    count = i.Count,
                    name = itemNames(i.Group, i.Number) ?? $"Item {i.Group}:{i.Number}",
                }).ToList(),
            }).ToList(),
            history = history.Take(20).Select(h => new
            {
                atUtc = h.AtUtc,
                atSydney = HnmoStateStore.ToTimeZone(h.AtUtc, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                kind = h.Kind,
                amountVnd = h.AmountVnd,
                detail = h.Detail,
            }).ToList(),
            result = actionResult,
            messageEn,
            messageVi,
        };
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    private static object SerializeStep(
        TopUpMilestoneStep step,
        bool reached,
        bool claimed,
        bool claimable,
        Func<short, short, string?> itemNames)
    {
        return new
        {
            id = step.Id,
            thresholdVnd = step.ThresholdVnd,
            titleEn = step.TitleEn,
            titleVi = step.TitleVi,
            coinType = step.CoinType.ToString(),
            coins = step.Coins,
            zen = step.Zen,
            items = step.Items.Select(i => new
            {
                group = i.Group,
                number = i.Number,
                level = i.Level,
                optionLevel = i.OptionLevel,
                withLuck = i.WithLuck,
                withSkill = i.WithSkill,
                quantity = i.Quantity,
                name = itemNames(i.Group, i.Number) ?? $"Item {i.Group}:{i.Number}",
            }).ToList(),
            reached,
            claimed,
            claimable,
        };
    }
}
