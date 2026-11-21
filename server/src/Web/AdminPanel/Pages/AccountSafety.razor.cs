// <copyright file="AccountSafety.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The page "Account safety": every option of the <see cref="AccountSafetyPlugIn"/> (saved like the plug-in editor
/// does): which actions need the second password, the wrong-try limits, the item lock and its wait, the histories;
/// and an account's view: its full login history (with the whole addresses), its trades, its locked items (removed at
/// once from here), the lockout (cleared) and the second password (reset). English and Vietnamese.
/// </summary>
public partial class AccountSafety : ComponentBase
{
    private AccountSafetyConfiguration? _configuration;
    private AccountSafetyEngine? _engine;
    private string _account = string.Empty;
    private string? _shownAccount;
    private bool? _hasPin;
    private string? _message;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private ILoginServer LoginServer { get; set; } = null!;

    [Inject]
    private IServerProvider ServerProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<AccountSafety> Logger { get; set; } = null!;

    private static bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    /// <summary>
    /// Puts the numbers of an edited configuration into their ranges.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    internal static void Clamp(AccountSafetyConfiguration configuration)
    {
        configuration.ValuableMinimumLevel = Math.Clamp(configuration.ValuableMinimumLevel, 0, 15);
        configuration.UnlockedMinutes = Math.Clamp(configuration.UnlockedMinutes, 0, 1440);
        configuration.MaxWrongTries = Math.Clamp(configuration.MaxWrongTries, 1, 100);
        configuration.LockoutMinutes = Math.Clamp(configuration.LockoutMinutes, 1, 10_080);
        configuration.UnlockWaitHours = Math.Clamp(configuration.UnlockWaitHours, 0, 720);
        configuration.MaxLockedItems = Math.Clamp(configuration.MaxLockedItems, 1, 1000);
        configuration.LoginHistoryDays = Math.Clamp(configuration.LoginHistoryDays, 1, 3650);
        configuration.LoginsShown = Math.Clamp(configuration.LoginsShown, 1, 500);
        configuration.TradeHistoryDays = Math.Clamp(configuration.TradeHistoryDays, 1, 3650);
        configuration.TradesShown = Math.Clamp(configuration.TradesShown, 1, 500);
        if (string.IsNullOrWhiteSpace(configuration.TimeZone))
        {
            configuration.TimeZone = "Australia/Sydney";
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static string T(string english, string vietnamese) => IsVietnamese ? vietnamese : english;

    private static string Source(string source) => source == "website" ? T("website", "trang web") : T("game", "game");

    private static string Kind(string kind) => kind switch
    {
        "store-sale" => T("store sale", "bán ở cửa hàng"),
        "store-buy" => T("store purchase", "mua ở cửa hàng"),
        _ => T("trade", "giao dịch"),
    };

    private static AccountSafetyConfiguration Copy(AccountSafetyConfiguration configuration) =>
        JsonSerializer.Deserialize<AccountSafetyConfiguration>(JsonSerializer.Serialize(configuration)) ?? new AccountSafetyConfiguration();

    private string Sydney(DateTime utc) =>
        this._engine is null ? string.Empty : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), this._engine.TimeZone).ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentUICulture);

    private IEnumerable<GameLogic.IGameContext> Contexts() =>
        this.ServerProvider.Servers.OfType<GameLogic.IGameServerContextProvider>().Select(s => s.Context).Concat(AccountSafetyPlugIn.KnownContexts()).Distinct();

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<AccountSafetyConfiguration>(this.PersistenceContextProvider, typeof(AccountSafetyPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            this._configuration = Copy(saved ?? new AccountSafetyConfiguration());

            // A copy for the account views, so that an unsaved edit doesn't change them.
            var live = Copy(this._configuration);
            this._engine = new AccountSafetyEngine(live, AccountSafetyStore.Open(live.FileName));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the account safety options.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        Clamp(configuration);
        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(AccountSafetyPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<AccountSafetyConfiguration>(this.PersistenceContextProvider, typeof(AccountSafetyPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the account safety options, active: {Active}.", this._isActive);
            this.ToastService.ShowSuccess(T("The account safety options were saved.", "Đã lưu cài đặt bảo vệ tài khoản."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the account safety options.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task ShowAccountAsync()
    {
        this._message = null;
        var login = this._account.Trim();
        if (login.Length == 0)
        {
            this._shownAccount = null;
            return;
        }

        this._shownAccount = login;
        this._hasPin = null;
        try
        {
            if (await AccountSafetyPlugIn.FindPlayerAsync(this.Contexts(), login).ConfigureAwait(true) is { Account: { } live })
            {
                this._hasPin = AccountSafetyEngine.HasPin(live.VaultPassword);
                return;
            }

            var configuration = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, default).ConfigureAwait(true);
            using var context = this.PersistenceContextProvider.CreateNewPlayerContext(configuration);
            if (await context.GetAccountByLoginNameAsync(login, default).ConfigureAwait(true) is { } account)
            {
                this._hasPin = AccountSafetyEngine.HasPin(account.VaultPassword);
            }
            else
            {
                this._message = T("There is no such account.", "Không có tài khoản này.");
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error reading the account {Login}.", login);
            this._message = string.Format(Resources.UnexpectedErrorOccurred, ex.Message);
        }
    }

    private async Task ResetPinAsync()
    {
        if (this._shownAccount is not { } login || this._engine is not { } engine)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            var configuration = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, default).ConfigureAwait(true);
            var result = await WebsiteAccountSafetyController.WritePinAsync(this.PersistenceContextProvider, configuration, this.LoginServer, this.Contexts(), login, string.Empty, default).ConfigureAwait(true);
            if (result == "ok")
            {
                engine.PinChanged(login, DateTime.UtcNow);
                this.SaveStore();
                this._hasPin = false;
                this.Logger.LogInformation("Account safety: the admin reset the second password of {Login}.", login);
                this._message = T($"The second password of {login} was cleared.", $"Đã xóa mật khẩu cấp 2 của {login}.");
            }
            else
            {
                this._message = result == "online"
                    ? T("The account is in the game on another server: try again when it's offline.", "Tài khoản đang trong game ở máy chủ khác: hãy thử lại khi offline.")
                    : T("There is no such account.", "Không có tài khoản này.");
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error resetting the second password of {Login}.", login);
            this._message = string.Format(Resources.UnexpectedErrorOccurred, ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void ClearLockout()
    {
        if (this._shownAccount is { } login && this._engine is { } engine)
        {
            engine.ClearLockout(login);
            this.SaveStore();
            this._message = T("The lockout was cleared.", "Đã gỡ khóa nhập sai.");
        }
    }

    private void RemoveLock(Guid itemId)
    {
        if (this._shownAccount is { } login && this._engine is { } engine && engine.RemoveLock(login, itemId))
        {
            this.SaveStore();
            this.Logger.LogInformation("Account safety: the admin removed the lock of item {Item} of {Login}.", itemId, login);
            this._message = T("The item was unlocked.", "Đã mở khóa vật phẩm.");
        }
    }

    private void SaveStore()
    {
        if (this._engine is { } engine)
        {
            lock (engine.Store.Lock)
            {
                engine.Store.SaveIfChanged();
            }
        }
    }
}
