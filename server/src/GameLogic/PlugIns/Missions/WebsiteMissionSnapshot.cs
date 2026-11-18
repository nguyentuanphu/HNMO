// <copyright file="WebsiteMissionSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Projects one account's board without allowing rollover reads to mutate the live engine.</summary>
public static class WebsiteMissionSnapshot
{
    /// <summary>Reads a deep copy of the owned state, preserving actual referral codes and aggregate referral counts.</summary>
    /// <param name="engine">The authoritative mission engine.</param>
    /// <param name="characterId">The owned character key.</param>
    /// <param name="loginName">The authenticated account name.</param>
    /// <param name="configuration">The game's current item definitions.</param>
    /// <param name="nowUtc">The snapshot instant.</param>
    /// <param name="boardEvent">The optional action result.</param>
    /// <returns>A safe, independent JSON board.</returns>
    public static JsonObject Read(MissionEngine engine, string characterId, string loginName, GameConfiguration configuration, DateTime nowUtc, MissionBoardEvent? boardEvent = null)
    {
        var copy = new MissionStore();
        string? actualCode;
        int invited;
        int qualified;
        lock (engine.Store.Lock)
        {
            var original = engine.Store.Data;
            original.Accounts.TryGetValue(loginName, out var account);
            original.Characters.TryGetValue(characterId, out var character);
            actualCode = account?.InviteCode;
            copy.Data.Accounts[loginName] = Clone(account ?? new MissionAccountState());
            copy.Data.Characters[characterId] = Clone(character ?? new MissionCharacterState());
            var referrals = original.Accounts.Values.Where(a => string.Equals(a.InvitedBy, loginName, StringComparison.OrdinalIgnoreCase)).ToList();
            invited = referrals.Count;
            qualified = referrals.Count(a => a.InviteRewarded);
        }

        var board = JsonNode.Parse(MissionBoardWriter.WriteBoard(new MissionEngine(engine.Configuration, copy), characterId, loginName, nowUtc, boardEvent))!.AsObject();
        if (board["invite"] is JsonObject invitation)
        {
            // ReadBoard may create a code in its private copy; only an actually saved code may be shared.
            invitation["code"] = actualCode;
            invitation["invitedCount"] = invited;
            invitation["qualifiedCount"] = qualified;
        }

        AddItemNames(board, configuration);
        return board;
    }

    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static void AddItemNames(JsonNode? node, GameConfiguration configuration)
    {
        if (node is JsonObject item)
        {
            if (item["group"] is JsonValue group && item["number"] is JsonValue number)
            {
                var definition = configuration.Items.FirstOrDefault(d => d.Group == group.GetValue<short>() && d.Number == number.GetValue<short>());
                item["nameEn"] = definition?.Name.ValueInNeutralLanguage;
                item["nameVi"] = definition?.Name.GetTranslation(CultureInfo.GetCultureInfo("vi"));
            }

            foreach (var child in item.Select(p => p.Value).ToList())
            {
                AddItemNames(child, configuration);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                AddItemNames(child, configuration);
            }
        }
    }
}
