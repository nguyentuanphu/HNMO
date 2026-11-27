// <copyright file="GmCommandPermissionCatalog.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GmManager;

/// <summary>Central mapping of every in-game GM chat command key to its required <see cref="GmPermission"/>.</summary>
public static class GmCommandPermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, GmPermission> Map = new Dictionary<string, GmPermission>(StringComparer.OrdinalIgnoreCase)
    {
        // Announce
        ["/goldnotice"] = GmPermission.Announce,
        ["/slidenotice"] = GmPermission.Announce,
        ["/gmannounce"] = GmPermission.Announce,

        // Move / Summon / GoTo
        ["/teleport"] = GmPermission.MoveSummonGoto,
        ["/trace"] = GmPermission.MoveSummonGoto,
        ["/goto"] = GmPermission.MoveSummonGoto,
        ["/track"] = GmPermission.MoveSummonGoto,
        ["/summon"] = GmPermission.MoveSummonGoto,
        ["/move"] = GmPermission.MoveSummonGoto,
        ["/guildmove"] = GmPermission.MoveSummonGoto,
        ["/gmmove"] = GmPermission.MoveSummonGoto,
        ["/hide"] = GmPermission.MoveSummonGoto,
        ["/unhide"] = GmPermission.MoveSummonGoto,

        // Spawn monster / boss & world effects
        ["/createmonster"] = GmPermission.SpawnMonsterBoss,
        ["/spawn"] = GmPermission.SpawnMonsterBoss,
        ["/boss"] = GmPermission.SpawnMonsterBoss,
        ["/npc"] = GmPermission.SpawnMonsterBoss,
        ["/removenpc"] = GmPermission.SpawnMonsterBoss,
        ["/killallmonsters"] = GmPermission.SpawnMonsterBoss,
        ["/walkmonster"] = GmPermission.SpawnMonsterBoss,
        ["/movemonster"] = GmPermission.SpawnMonsterBoss,
        ["/fireworks"] = GmPermission.SpawnMonsterBoss,
        ["/xmasfireworks"] = GmPermission.SpawnMonsterBoss,
        ["/shownpcids"] = GmPermission.SpawnMonsterBoss,

        // Start events
        ["/startbc"] = GmPermission.StartEvent,
        ["/startcc"] = GmPermission.StartEvent,
        ["/startds"] = GmPermission.StartEvent,
        ["/startit"] = GmPermission.StartEvent,
        ["/startkanturu"] = GmPermission.StartEvent,
        ["/skipminigamewait"] = GmPermission.StartEvent,
        ["/crywolf"] = GmPermission.StartEvent,
        ["/startevent"] = GmPermission.StartEvent,

        // Inspect character / inventory
        ["/inspectinv"] = GmPermission.InspectInventory,
        ["/charinfo"] = GmPermission.InspectInventory,
        ["/getlevel"] = GmPermission.InspectInventory,
        ["/getleveluppoints"] = GmPermission.InspectInventory,
        ["/getmasterlevel"] = GmPermission.InspectInventory,
        ["/getmasterleveluppoints"] = GmPermission.InspectInventory,
        ["/getmoney"] = GmPermission.InspectInventory,
        ["/getresets"] = GmPermission.InspectInventory,
        ["/getstat"] = GmPermission.InspectInventory,
        ["/online"] = GmPermission.InspectInventory,

        // Mute / Kick / Ban
        ["/chatban"] = GmPermission.MuteKick,
        ["/chatunban"] = GmPermission.MuteKick,
        ["/mute"] = GmPermission.MuteKick,
        ["/disconnect"] = GmPermission.MuteKick,
        ["/kick"] = GmPermission.MuteKick,
        ["/guilddisconnect"] = GmPermission.MuteKick,
        ["/banacc"] = GmPermission.MuteKick,
        ["/banchar"] = GmPermission.MuteKick,
        ["/unbanacc"] = GmPermission.MuteKick,
        ["/unbanchar"] = GmPermission.MuteKick,

        // Direct character edits
        ["/setlevel"] = GmPermission.EditCharacter,
        ["/setleveluppoints"] = GmPermission.EditCharacter,
        ["/setmasterlevel"] = GmPermission.EditCharacter,
        ["/setmasterleveluppoints"] = GmPermission.EditCharacter,
        ["/setmoney"] = GmPermission.EditCharacter,
        ["/setresets"] = GmPermission.EditCharacter,
        ["/setstat"] = GmPermission.EditCharacter,
        ["/item"] = GmPermission.EditCharacter,
        ["/clearinventory"] = GmPermission.EditCharacter,
        ["/openwarehouse"] = GmPermission.EditCharacter,
        ["/pk"] = GmPermission.EditCharacter,
        ["/pkclear"] = GmPermission.EditCharacter,
        ["/skin"] = GmPermission.EditCharacter,
    };

    /// <summary>Gets all known command mappings.</summary>
    public static IReadOnlyDictionary<string, GmPermission> AllMappings => Map;

    /// <summary>Resolves the required permission for <paramref name="commandKey"/>.</summary>
    /// <param name="commandKey">The chat command prefix.</param>
    /// <returns>The required permission flag (defaults to <see cref="GmPermission.EditCharacter"/> if unlisted).</returns>
    public static GmPermission Resolve(string commandKey)
        => Map.TryGetValue(commandKey.Trim(), out var permission) ? permission : GmPermission.EditCharacter;

    /// <summary>Formats a human-readable bilingual permission name.</summary>
    /// <param name="permission">The permission flag.</param>
    /// <param name="isVi">True for Vietnamese.</param>
    /// <returns>Localized permission label.</returns>
    public static string FormatLabel(GmPermission permission, bool isVi) => permission switch
    {
        GmPermission.Announce => isVi ? "Thông báo máy chủ (Announce)" : "Announce",
        GmPermission.MoveSummonGoto => isVi ? "Di chuyển / Triệu hồi / Đến người chơi" : "Move / Summon / Go To Player",
        GmPermission.SpawnMonsterBoss => isVi ? "Thả quái / Boss" : "Spawn Monster / Boss",
        GmPermission.StartEvent => isVi ? "Bắt đầu sự kiện" : "Start Event",
        GmPermission.InspectInventory => isVi ? "Kiểm tra túi đồ / thông tin" : "Inspect Inventory & Info",
        GmPermission.MuteKick => isVi ? "Cấm chat / Kick / Khóa" : "Mute / Kick / Ban",
        GmPermission.EditCharacter => isVi ? "Chỉnh sửa nhân vật trực tiếp" : "Direct Character Edits",
        _ => permission.ToString(),
    };
}
