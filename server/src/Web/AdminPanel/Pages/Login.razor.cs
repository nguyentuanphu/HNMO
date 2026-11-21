// <copyright file="Login.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.ComponentModel.DataAnnotations;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Properties;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// The login page of the admin panel.
/// </summary>
/// <remarks>
/// The whole login - including the second factor - runs inside the blazor circuit. Only when the
/// credentials checked out, the browser exchanges a single use ticket for the authentication cookie
/// in the background. That's why neither entering the second factor nor the successful login
/// requires a page reload.
/// </remarks>
public partial class Login : IAsyncDisposable
{
    private readonly PasswordInput _passwordInput = new();
    private readonly TwoFactorInput _twoFactorInput = new();

    private LoginStep _step = LoginStep.Password;
    private string? _errorMessage;
    private bool _isBusy;
    private IJSObjectReference? _authModule;

    /// <summary>
    /// The steps of the login.
    /// </summary>
    private enum LoginStep
    {
        /// <summary>
        /// The user enters its login name and password.
        /// </summary>
        Password,

        /// <summary>
        /// The user enters the code of its second factor.
        /// </summary>
        TwoFactor,
    }

    /// <summary>
    /// Gets or sets the relative url to which the user is sent after a successful login.
    /// </summary>
    [Parameter]
    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    [Inject]
    private AdminLoginService LoginService { get; set; } = null!;

    [Inject]
    private Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

    /// <summary>Gets a value indicating whether the page was opened from afar (through the website), not on the host computer.</summary>
    private bool IsFromAfar => !Services.HostComputerRequest.IsFromHostComputer(this.HttpContextAccessor.HttpContext);

    /// <summary>Gets this browser's trusted-browser cookie, if it has one (owner, 2026-11-21).</summary>
    private string? TrustedBrowserCookie => this.HttpContextAccessor.HttpContext?.Request.Cookies[TrustedBrowserService.CookieName];

    [Inject]
    private AdminAuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Login> Logger { get; set; } = null!;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._authModule is { } module)
        {
            this._authModule = null;
            try
            {
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone - nothing to clean up on the client anymore.
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender).ConfigureAwait(true);
        if (firstRender)
        {
            this._authModule = await this.JsRuntime
                .InvokeAsync<IJSObjectReference>("import", AdminAuthenticationDefaults.AuthScriptPath)
                .ConfigureAwait(true);
        }
    }

    private async Task OnPasswordSubmittedAsync()
    {
        if (this._isBusy)
        {
            return;
        }

        this._isBusy = true;
        this._errorMessage = null;
        try
        {
            var result = await this.LoginService
                .CheckPasswordAsync(this._passwordInput.LoginName, this._passwordInput.Password, this._passwordInput.RememberMe, this.IsFromAfar, this.TrustedBrowserCookie)
                .ConfigureAwait(true);
            this._passwordInput.Password = string.Empty;

            switch (result.Status)
            {
                case AdminLoginStatus.TwoFactorRequired:
                    this._step = LoginStep.TwoFactor;
                    break;
                case AdminLoginStatus.LockedOut:
                    this._errorMessage = Resources.AccountLockedOut;
                    break;
                case AdminLoginStatus.TwoFactorSetupRequired:
                    this._errorMessage = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi"
                        ? "Đăng nhập từ xa cần mã 6 số. Hãy bật mã xác thực cho tài khoản này trên máy chủ trước (Bảo mật tài khoản), rồi đăng nhập lại."
                        : "Signing in from afar needs the 6-digit code. Set up the authenticator for this user on the host computer first (Account security), then sign in again.";
                    break;
                case AdminLoginStatus.Succeeded:
                    await this.CompleteLoginAsync(result).ConfigureAwait(true);
                    break;
                default:
                    this._errorMessage = Resources.InvalidCredentials;
                    break;
            }
        }
        finally
        {
            this._isBusy = false;
        }
    }

    private async Task OnTwoFactorSubmittedAsync()
    {
        if (this._isBusy)
        {
            return;
        }

        this._isBusy = true;
        this._errorMessage = null;
        try
        {
            var result = await this.LoginService
                .CheckTwoFactorAsync(this._twoFactorInput.Code, this._twoFactorInput.UseRecoveryCode, this._twoFactorInput.TrustBrowser)
                .ConfigureAwait(true);
            this._twoFactorInput.Code = string.Empty;

            switch (result.Status)
            {
                case AdminLoginStatus.Succeeded:
                    await this.CompleteLoginAsync(result).ConfigureAwait(true);
                    break;
                case AdminLoginStatus.LockedOut:
                    this._step = LoginStep.Password;
                    this._errorMessage = Resources.AccountLockedOut;
                    break;
                default:
                    this._errorMessage = Resources.InvalidTwoFactorCode;
                    break;
            }
        }
        finally
        {
            this._isBusy = false;
        }
    }

    private async Task CompleteLoginAsync(AdminLoginResult result)
    {
        if (result.Ticket is not { } ticket || result.Claims is not { } claims)
        {
            this._errorMessage = Resources.InvalidCredentials;
            return;
        }

        if (this._authModule is not { } module)
        {
            this.Logger.LogError("The authentication script module is not loaded, so the login can't be completed.");
            this._errorMessage = Resources.UnhandledErrorOccurred;
            return;
        }

        var isSignedIn = await module.InvokeAsync<bool>("signIn", ticket).ConfigureAwait(true);
        if (!isSignedIn)
        {
            this.Logger.LogError("The sign in endpoint rejected the ticket.");
            this._errorMessage = Resources.UnhandledErrorOccurred;
            return;
        }

        // The cookie is set now, so the circuit can switch to the authenticated state without a reload.
        this.AuthenticationStateProvider.NotifySignedIn(claims);

        // The return url may belong to another application behind the same reverse proxy, e.g. Grafana.
        // When the admin panel is hosted at the root path, the router would handle it as one of its own pages,
        // so it has to be loaded from the server.
        var returnUrl = ReturnUrlValidator.GetSafeReturnUrl(this.ReturnUrl, this.NavigationManager.BaseUri);
        this.NavigationManager.NavigateTo(returnUrl, forceLoad: returnUrl.Length > 0);
    }

    private void ToggleRecoveryCode()
    {
        this._twoFactorInput.UseRecoveryCode = !this._twoFactorInput.UseRecoveryCode;
        this._twoFactorInput.Code = string.Empty;
        this._errorMessage = null;
    }

    private void BackToPasswordStep()
    {
        this._step = LoginStep.Password;
        this._twoFactorInput.Code = string.Empty;
        this._twoFactorInput.UseRecoveryCode = false;
        this._errorMessage = null;
    }

    /// <summary>
    /// The input of the first login step.
    /// </summary>
    private class PasswordInput
    {
        [Required]
        public string LoginName { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// The input of the second login step.
    /// </summary>
    private class TwoFactorInput
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        public bool UseRecoveryCode { get; set; }

        public bool TrustBrowser { get; set; } = true;
    }
}
