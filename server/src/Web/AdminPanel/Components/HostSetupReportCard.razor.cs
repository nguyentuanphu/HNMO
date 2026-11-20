// <copyright file="HostSetupReportCard.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components;

using System.Globalization;
using HNMO.GameLogic;

/// <summary>
/// The setup page's card about the first-setup report: what is sent to the HNMO developer, the host's optional name and
/// contact, and whether it was sent. Saving a changed name or contact after it was sent sends the report once more.
/// </summary>
public partial class HostSetupReportCard
{
    private string _name = string.Empty;
    private string _contact = string.Empty;
    private DeveloperHubState _state = new();
    private string? _saved;

    private string Status
    {
        get
        {
            var sent = this._state.SetupReportedUtc is { } at
                ? L($"Sent on {Sydney(at)} (Sydney time).", $"Đã gửi lúc {Sydney(at)} (giờ Sydney).")
                : L("Not sent yet.", "Chưa gửi.");
            return this._saved is null ? sent : $"{this._saved} {sent}";
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this._state = DeveloperHub.ReadState();
        this._name = this._state.HostName;
        this._contact = this._state.HostContact;
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string Sydney(DateTime utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private void Save()
    {
        var name = this._name.Trim();
        var contact = this._contact.Trim();
        this._state = DeveloperHub.ChangeState(s =>
        {
            if (s.HostName == name && s.HostContact == contact)
            {
                return;
            }

            s.HostName = name;
            s.HostContact = contact;

            // Sent already: send it again with the new name and contact.
            s.SetupReportedUtc = null;
            s.SetupTriedUtc = null;
        });
        this._saved = this._state.SetupReportedUtc is null
            ? L("Saved; it is sent within the hour.", "Đã lưu; sẽ được gửi trong vòng một giờ.")
            : L("Saved.", "Đã lưu.");
    }
}
