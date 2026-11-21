// <copyright file="VipActivityText.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Globalization;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Web.AdminPanel.Properties;

/// <summary>Shows VIP host activity in the selected language while retaining the exact host-written reason.</summary>
public static class VipActivityText
{
    /// <summary>Formats known VIP administration metadata without changing other activity entries.</summary>
    /// <param name="entry">The privileged activity row.</param>
    /// <returns>Localized VIP details, or the existing details for another activity kind.</returns>
    public static string Format(ActivityLogEntry entry)
    {
        if (entry.Kind != ActivityKind.Administration)
        {
            return entry.Details;
        }

        try
        {
            using var document = JsonDocument.Parse(entry.Details);
            var root = document.RootElement;
            if (!root.TryGetProperty("operation", out var operation) || operation.GetString() is not ("vip_grant" or "vip_remove"))
            {
                return entry.Details;
            }

            var action = operation.GetString() == "vip_remove" ? Resources.VipAuditRemove : Resources.VipAuditGrant;
            var status = root.GetProperty("status").GetString() switch
            {
                "Requested" => Resources.VipAuditRequested,
                "Applied" => Resources.VipAuditApplied,
                "Pending" => Resources.VipAuditPending,
                "Conflict" => Resources.VipAuditConflict,
                "Refused" => Resources.VipAuditRefused,
                _ => Resources.VipManagerUnavailable,
            };
            return string.Format(CultureInfo.CurrentUICulture, Resources.VipAuditDetails, action, status, entry.PartnerAccount, root.GetProperty("reason").GetString(), root.GetProperty("operationId").GetString());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return entry.Details;
        }
    }
}
