// <copyright file="BanManager.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Moderation;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Components;

/// <summary>Admin Panel page for managing timed/permanent bans, chat mutes, and salted network/device bans (ROADMAP 14.16).</summary>
public partial class BanManager
{
    private readonly Dictionary<string, string> _unbanReasons = new(StringComparer.Ordinal);
    private IReadOnlyList<BanRecord> _records = Array.Empty<BanRecord>();
    private int _total;
    private BanTargetKind _kind = BanTargetKind.AccountBan;
    private string _target = string.Empty;
    private bool _permanent;
    private int _durationMinutes = 60;
    private string _reasonEn = string.Empty;
    private string _reasonVi = string.Empty;
    private string _auditReason = string.Empty;
    private string _search = string.Empty;
    private bool _activeOnly = true;
    private bool _showHelp;
    private bool _busy;
    private string? _statusMessage;
    private bool _statusError;

    [Inject]
    private BanAdminOperations Operations { get; set; } = null!;

    private bool IsVi => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
        => await this.LoadAsync().ConfigureAwait(true);

    private static string L(string en, string vi)
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private async Task LoadAsync()
    {
        try
        {
            var (items, total) = await this.Operations.SearchAsync(this._search, null, this._activeOnly, 0, 50, default).ConfigureAwait(true);
            this._records = items;
            this._total = total;
        }
        catch (Exception ex)
        {
            this._statusError = true;
            this._statusMessage = ex.Message;
        }
    }

    private string GetUnbanReason(string id)
        => this._unbanReasons.GetValueOrDefault(id) ?? string.Empty;

    private void SetUnbanReason(string id, string? value)
        => this._unbanReasons[id] = value ?? string.Empty;

    private async Task IssueAsync()
    {
        if (this._busy)
        {
            return;
        }

        this._busy = true;
        this._statusError = false;
        this._statusMessage = null;
        try
        {
            DateTime? expiresUtc = this._permanent ? null : DateTime.UtcNow.AddMinutes(Math.Clamp(this._durationMinutes, 1, 5256000));
            var created = await this.Operations.IssueAsync(
                this._kind,
                this._target,
                this._reasonEn,
                this._reasonVi,
                expiresUtc,
                this._auditReason,
                default).ConfigureAwait(true);

            this._target = string.Empty;
            this._reasonEn = string.Empty;
            this._reasonVi = string.Empty;
            this._auditReason = string.Empty;
            this._statusMessage = L($"Applied {created.Kind} for {created.TargetDisplay}.", $"Đã áp dụng {created.Kind} cho {created.TargetDisplay}.");
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this._statusError = true;
            this._statusMessage = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task LiftAsync(string recordId)
    {
        if (this._busy)
        {
            return;
        }

        this._busy = true;
        this._statusError = false;
        this._statusMessage = null;
        try
        {
            var reason = this.GetUnbanReason(recordId);
            var lifted = await this.Operations.LiftAsync(recordId, reason, default).ConfigureAwait(true);
            this._unbanReasons.Remove(recordId);
            this._statusMessage = L($"Lifted restriction on {lifted.TargetDisplay}.", $"Đã gỡ hạn chế cho {lifted.TargetDisplay}.");
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this._statusError = true;
            this._statusMessage = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }
}
