// <copyright file="GmManager.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Threading;
using HNMO.GameLogic.PlugIns.GmManager;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Components;

/// <summary>Code-behind for <see cref="GmManager"/> (ROADMAP 14.17 + 14.11).</summary>
public partial class GmManager
{
    private static readonly TimeZoneInfo SydneyZone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    private static readonly GmPermission[] PermissionFlags =
    [
        GmPermission.Announce,
        GmPermission.MoveSummonGoto,
        GmPermission.SpawnMonsterBoss,
        GmPermission.StartEvent,
        GmPermission.InspectInventory,
        GmPermission.MuteKick,
        GmPermission.EditCharacter,
    ];

    private IReadOnlyList<GmAssignment> _assignments = [];
    private IReadOnlyList<GmCommandAuditEntry> _audits = [];
    private bool _requireListed;
    private string _policyReason = "Security policy update";
    private string _characterName = string.Empty;
    private string _assignReason = string.Empty;
    private bool _promoteOffline = true;
    private GmPermission _selectedPermissions = GmPermission.Announce | GmPermission.MoveSummonGoto | GmPermission.InspectInventory;
    private bool _showHelp;
    private bool _isVi;
    private bool _busy;
    private string _error = string.Empty;
    private string _status = string.Empty;

    [Inject]
    private GmManagerOperations Operations { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isVi = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        await this.ReloadAsync().ConfigureAwait(true);
    }

    private static string FormatSydney(DateTime utc)
    {
        var safeUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var sydney = TimeZoneInfo.ConvertTimeFromUtc(safeUtc, SydneyZone);
        return sydney.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private bool HasPermission(GmPermission flag) => (this._selectedPermissions & flag) == flag;

    private void TogglePermission(GmPermission flag, bool checkedState)
    {
        if (checkedState)
        {
            this._selectedPermissions |= flag;
        }
        else
        {
            this._selectedPermissions &= ~flag;
        }
    }

    private void SelectForEdit(GmAssignment gm)
    {
        this._characterName = gm.CharacterName;
        this._selectedPermissions = gm.Permissions;
        this._assignReason = gm.Notes;
    }

    private async Task ReloadAsync()
    {
        this._error = string.Empty;
        try
        {
            var (assignments, requireListed, audits) = await this.Operations.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            this._assignments = assignments;
            this._requireListed = requireListed;
            this._audits = audits;
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
    }

    private async Task SavePolicyAsync()
    {
        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            await this.Operations.SetRequireListedAsync(this._requireListed, this._policyReason, CancellationToken.None).ConfigureAwait(true);
            await this.ReloadAsync().ConfigureAwait(true);
            this._status = this.L("Saved GM enforcement policy.", "Đã lưu chính sách kiểm tra GM.");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task SaveAssignmentAsync()
    {
        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            var saved = await this.Operations.AssignGmAsync(
                this._characterName,
                this._selectedPermissions,
                this._promoteOffline,
                this._assignReason,
                CancellationToken.None).ConfigureAwait(true);
            await this.ReloadAsync().ConfigureAwait(true);
            this._status = this.L(
                $"Updated GM permissions for {saved.CharacterName}.",
                $"Đã cập nhật quyền GM cho {saved.CharacterName}.");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task RevokeAsync(string characterName)
    {
        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            var reason = string.IsNullOrWhiteSpace(this._assignReason) ? "GM access revoked" : this._assignReason;
            await this.Operations.RevokeGmAsync(characterName, this._promoteOffline, reason, CancellationToken.None).ConfigureAwait(true);
            await this.ReloadAsync().ConfigureAwait(true);
            this._status = this.L($"Revoked GM permissions for {characterName}.", $"Đã thu hồi quyền GM của {characterName}.");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }
}
