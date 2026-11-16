// <copyright file="JewelBankStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// Serializes Jewel Bank configuration and account balances into plain-ASCII JSON for packet <c>F5 3B</c> and website APIs.
/// </summary>
public static class JewelBankStateWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false,
    };

    /// <summary>Writes public Jewel Bank configuration/rules (no account balances).</summary>
    /// <param name="engine">Jewel Bank engine.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WritePublic(JewelBankEngine engine) =>
        WriteAccountState(engine, loginName: null, masterFree: 0, masterSpent: 0, bagJewels: null);

    /// <summary>Writes full Jewel Bank account state for in-game UI and authenticated website API.</summary>
    /// <param name="engine">Jewel Bank engine.</param>
    /// <param name="loginName">Account login name.</param>
    /// <param name="masterFree">Free master skill points on current character.</param>
    /// <param name="masterSpent">Spent master skill points on current character.</param>
    /// <param name="bagJewels">Jewel slots currently in the bag (<c>(Slot, Key, NameEn, NameVi)</c>).</param>
    /// <param name="actionResult">Optional action result code.</param>
    /// <param name="messageEn">Optional English feedback.</param>
    /// <param name="messageVi">Optional Vietnamese feedback.</param>
    /// <returns>Plain-ASCII JSON.</returns>
    public static string WriteAccountState(
        JewelBankEngine engine,
        string? loginName,
        int masterFree,
        int masterSpent,
        IReadOnlyList<(byte Slot, string Key, string NameEn, string NameVi)>? bagJewels,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        var cfg = engine.Configuration;
        var balances = string.IsNullOrWhiteSpace(loginName)
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : engine.GetBalances(loginName);
        var pending = string.IsNullOrWhiteSpace(loginName)
            ? []
            : engine.PeekPending(loginName)
                .Select(p => new { id = p.Id, key = p.JewelKey, quantity = p.Quantity })
                .ToList();

        var jewels = cfg.Jewels.Select(j => new
        {
            key = j.Key,
            nameEn = j.NameEn,
            nameVi = j.NameVi,
            group = j.Group,
            number = j.Number,
            enabled = j.Enabled,
            maxCapacity = j.MaxCapacity,
            balance = balances.TryGetValue(j.Key, out var count) ? count : 0,
        }).ToList();

        var payload = new
        {
            v = 1,
            enabled = engine.Enabled,
            allowWebsite = cfg.AllowWebsite,
            rules = new
            {
                withdrawZenFee = cfg.WithdrawZenFee,
                depositZenFee = cfg.DepositZenFee,
                safeZoneOnly = cfg.SafeZoneOnly,
                allowClearMasterSkills = cfg.AllowClearMasterSkills,
                clearMasterZen = cfg.ClearMasterZen,
                clearMasterCoins = cfg.ClearMasterCoins,
                clearMasterCoinType = cfg.ClearMasterCoinType.ToString(),
            },
            jewels,
            pending,
            bagJewels = (bagJewels ?? []).Select(b => new { slot = b.Slot, key = b.Key, nameEn = b.NameEn, nameVi = b.NameVi }).ToList(),
            master = new { freePoints = masterFree, spentPoints = masterSpent },
            @event = actionResult is null ? null : new { result = actionResult, textEn = messageEn, textVi = messageVi },
        };

        return EscapeNonAscii(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static string EscapeNonAscii(string json)
    {
        var sb = new StringBuilder(json.Length);
        foreach (var c in json)
        {
            if (c <= 0x7F)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append("\\u");
                sb.Append(((int)c).ToString("x4"));
            }
        }

        return sb.ToString();
    }
}
