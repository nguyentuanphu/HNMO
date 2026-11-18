// <copyright file="EmailSettings.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.IO;
using System.Security.Cryptography;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Components;

/// <summary>Configures outgoing mail without rendering the stored credential.</summary>
public partial class EmailSettings
{
    private WebsiteMailSettings? _settings;
    private string _password = string.Empty;
    private string? _message;
    private bool _saving;

    [Inject]
    private WebsiteMailSettingsStore Settings { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        try
        {
            this._settings = this.Settings.Read();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            this._message = Resources.WebsiteMailUnavailable;
        }
    }

    private Task SaveAsync()
    {
        if (this._saving || this._settings is null)
        {
            return Task.CompletedTask;
        }

        this._saving = true;
        try
        {
            this.Settings.Save(this._settings, this._password);
            this._settings = this.Settings.Read();
            this._password = string.Empty;
            this._message = Resources.WebsiteMailSaved;
        }
        catch (ArgumentException)
        {
            this._message = Resources.WebsiteMailInvalid;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            this._message = Resources.WebsiteMailUnavailable;
        }
        finally
        {
            this._saving = false;
        }

        return Task.CompletedTask;
    }
}
