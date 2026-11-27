// <copyright file="TeleportNpcEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TeleportNpc;

/// <summary>
/// Pure rule evaluation engine for <see cref="TeleportNpcPlugIn"/>.
/// </summary>
public static class TeleportNpcEngine
{
    /// <summary>Checks whether <paramref name="npcNumber"/> is handled as a Teleport NPC.</summary>
    /// <param name="config">Plug-in configuration.</param>
    /// <param name="plugInActive">Whether plug-in is active in runtime config.</param>
    /// <param name="npcNumber">Talked NPC definition number.</param>
    /// <returns><c>true</c> if handled by Teleport NPC.</returns>
    public static bool HandlesNpc(TeleportNpcConfiguration config, bool plugInActive, short npcNumber)
    {
        if (!plugInActive || !config.Enabled)
        {
            return false;
        }

        return config.NpcNumbers is not null && config.NpcNumbers.Contains(npcNumber);
    }

    /// <summary>Evaluates whether a character may warp to <paramref name="destinationId"/>.</summary>
    /// <param name="config">Plug-in configuration.</param>
    /// <param name="plugInActive">Whether plug-in is active.</param>
    /// <param name="destinationId">Target destination id.</param>
    /// <param name="level">Character level.</param>
    /// <param name="resets">Character reset count.</param>
    /// <param name="vipLevel">Account/character VIP level.</param>
    /// <param name="zen">Available inventory Zen.</param>
    /// <param name="isPk">Whether character is currently PK/murderer.</param>
    /// <param name="inMiniGame">Whether character is inside an active mini-game.</param>
    /// <returns>The warp decision.</returns>
    public static TeleportDecision EvaluateWarp(
        TeleportNpcConfiguration config,
        bool plugInActive,
        string? destinationId,
        int level,
        int resets,
        int vipLevel,
        long zen,
        bool isPk,
        bool inMiniGame)
    {
        if (!plugInActive || !config.Enabled)
        {
            return Deny(TeleportDenialReason.Disabled, null, "Teleport NPC is currently disabled.", "NPC Dịch Chuyển hiện đang tắt.");
        }

        var cleanId = (destinationId ?? string.Empty).Trim();
        var dest = (config.Destinations ?? [])
            .FirstOrDefault(d => string.Equals(d.Id, cleanId, StringComparison.OrdinalIgnoreCase));
        if (dest is null)
        {
            return Deny(TeleportDenialReason.DestinationNotFound, null, $"Unknown destination '{cleanId}'.", $"Không tìm thấy điểm dịch chuyển '{cleanId}'.");
        }

        if (!dest.Enabled)
        {
            return Deny(TeleportDenialReason.DestinationDisabled, dest, $"{dest.NameEn} is currently disabled.", $"{dest.NameVi} hiện đang tạm đóng.");
        }

        if (inMiniGame)
        {
            return Deny(TeleportDenialReason.InMiniGame, dest, "Cannot teleport out of an active event.", "Không thể dịch chuyển khi đang ở trong sự kiện.");
        }

        if (!dest.AllowPk && isPk)
        {
            return Deny(TeleportDenialReason.PkBlocked, dest, $"PK players cannot use teleport to {dest.NameEn}.", $"Nhân vật PK không thể dịch chuyển tới {dest.NameVi}.");
        }

        if (dest.VipOnly && vipLevel <= 0)
        {
            return Deny(TeleportDenialReason.VipRequired, dest, $"{dest.NameEn} requires VIP status.", $"{dest.NameVi} chỉ dành cho tài khoản VIP.");
        }

        if (dest.MinLevel > 0 && level < dest.MinLevel)
        {
            return Deny(
                TeleportDenialReason.LevelTooLow,
                dest,
                $"You need at least level {dest.MinLevel} to warp to {dest.NameEn}.",
                $"Cần đạt tối thiểu cấp {dest.MinLevel} để dịch chuyển tới {dest.NameVi}.");
        }

        if (dest.MaxLevel > 0 && level > dest.MaxLevel)
        {
            return Deny(
                TeleportDenialReason.LevelTooHigh,
                dest,
                $"{dest.NameEn} is limited to level {dest.MaxLevel} or below.",
                $"{dest.NameVi} chỉ dành cho cấp {dest.MaxLevel} trở xuống.");
        }

        if (dest.MinResets > 0 && resets < dest.MinResets)
        {
            return Deny(
                TeleportDenialReason.InsufficientResets,
                dest,
                $"You need at least {dest.MinResets} resets to warp to {dest.NameEn}.",
                $"Cần tối thiểu {dest.MinResets} lần Reset để dịch chuyển tới {dest.NameVi}.");
        }

        var cost = Math.Max(0, dest.ZenCost);
        if (zen < cost)
        {
            return Deny(
                TeleportDenialReason.InsufficientZen,
                dest,
                $"Not enough Zen ({cost:N0} Zen required) for {dest.NameEn}.",
                $"Không đủ Zen (cần {cost:N0} Zen) để dịch chuyển tới {dest.NameVi}.");
        }

        var okEn = cost > 0
            ? $"Warped to {dest.NameEn} for {cost:N0} Zen."
            : $"Warped to {dest.NameEn}.";
        var okVi = cost > 0
            ? $"Đã dịch chuyển tới {dest.NameVi} (phí {cost:N0} Zen)."
            : $"Đã dịch chuyển tới {dest.NameVi}.";
        return new TeleportDecision(true, TeleportDenialReason.None, dest, cost, okEn, okVi);
    }

    private static TeleportDecision Deny(
        TeleportDenialReason reason,
        TeleportDestinationConfig? dest,
        string msgEn,
        string msgVi) =>
        new(false, reason, dest, Math.Max(0, dest?.ZenCost ?? 0), msgEn, msgVi);
}
