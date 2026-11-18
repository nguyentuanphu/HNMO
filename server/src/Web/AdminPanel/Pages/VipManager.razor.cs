// <copyright file="VipManager.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>Plain-language VIP configuration and attributed, reviewed changes to offline accounts.</summary>
public partial class VipManager
{
    private VipAdminSnapshot? _snapshot;
    private VipConfiguration _configuration = new();
    private string _filter = string.Empty;
    private string _account = string.Empty;
    private string _reason = string.Empty;
    private int _levelId = 1;
    private int _days = 7;
    private int _offset;
    private bool _busy;
    private string? _message;
    private ChangeDraft? _change;
    private string? _confirmation;
    private DateTime? _currentUntil;

    [Inject]
    private VipAdminOperations Operations { get; set; } = null!;

    [Inject]
    private PluginWriteOperations Plugins { get; set; } = null!;

    [Inject]
    private ILogger<VipManager> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync() => await this.LoadAsync(true).ConfigureAwait(true);

    private static bool Valid(VipConfiguration configuration)
        => configuration.Levels is { Count: > 0 and <= 12 }
        && configuration.Levels.Select(level => level.Id).Distinct().Count() == configuration.Levels.Count
        && configuration.Levels.All(level => level.Id is > 0 and <= 1000
            && !string.IsNullOrWhiteSpace(level.NameEn) && level.NameEn.Length <= 60
            && !string.IsNullOrWhiteSpace(level.NameVi) && level.NameVi.Length <= 60
            && level.ExperiencePercent is >= 0 and <= 10000 && level.ZenPercent is >= 0 and <= 10000
            && level.DailyCoins >= 0 && Enum.IsDefined(level.DailyCoinType) && level.Offers.Count <= 4
            && level.Offers.Select(offer => offer.Days).Distinct().Count() == level.Offers.Count
            && level.Offers.All(offer => offer.Days is > 0 and <= 3650 && offer.Price is >= 0 and <= int.MaxValue && Enum.IsDefined(offer.CoinType)));

    private string Name(string english, string vietnamese) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? vietnamese : english;

    private string Sydney(DateTime utc) => CashShopHistoryText.Sydney(utc);

    private string Coin(CashShopCoinType coin) => coin switch
    {
        CashShopCoinType.WCoinC => Resources.VipManagerCoinC,
        CashShopCoinType.WCoinP => Resources.VipManagerCoinP,
        CashShopCoinType.GoblinPoints => Resources.VipManagerGoblin,
        _ => Resources.VipManagerInvalid,
    };

    private async Task LoadAsync(bool resetEditor)
    {
        if (this._busy)
        {
            return;
        }

        this._busy = true;
        try
        {
            await this.ReadAsync(resetEditor).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "VIP administration state could not be loaded.");
            this._message = Resources.VipManagerUnavailable;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task ReadAsync(bool resetEditor)
    {
        this._snapshot = await this.Operations.ReadAsync(this._filter, default, this._offset).ConfigureAwait(true);
        if (resetEditor)
        {
            this._configuration = JsonSerializer.Deserialize<VipConfiguration>(JsonSerializer.Serialize(this._snapshot.Configuration))!;
            if (!this._snapshot.IsActive)
            {
                this._configuration.Enabled = false;
            }
        }

        if (!this._snapshot.Configuration.Levels.Any(level => level.Id == this._levelId))
        {
            this._levelId = this._snapshot.Configuration.Levels.FirstOrDefault()?.Id ?? 1;
        }
    }

    private async Task PageAsync(int amount)
    {
        this._offset = Math.Max(0, this._offset + amount);
        await this.LoadAsync(false).ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        if (this._busy || !Valid(this._configuration))
        {
            this._message = Resources.VipManagerInvalid;
            return;
        }

        this._busy = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(this._configuration, null);
            var id = typeof(VipPlugIn).GUID;
            var json = serialized.CustomConfiguration!;
            var preview = await this.Plugins.ReplaceFromPanelAsync(id, this._configuration, json, false, null, default).ConfigureAwait(true);
            await this.Plugins.ReplaceFromPanelAsync(id, this._configuration, json, true, preview["confirmationToken"]!.GetValue<string>(), default).ConfigureAwait(true);
            if (this._snapshot?.IsActive != true)
            {
                var activation = await this.Plugins.SetActiveAsync(id, true, false, null, default).ConfigureAwait(true);
                await this.Plugins.SetActiveAsync(id, true, true, activation["confirmationToken"]!.GetValue<string>(), default).ConfigureAwait(true);
            }

            await this.ReadAsync(true).ConfigureAwait(true);
            this._message = Resources.VipManagerSaved;
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "VIP settings could not be confirmed.");
            this._message = Resources.VipManagerUnavailable;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task PreviewAsync(bool remove)
    {
        if (this._busy || this._change is not null)
        {
            return;
        }

        if (this._snapshot?.IsActive != true)
        {
            this._message = Resources.VipManagerSaveFirst;
            return;
        }

        if (string.IsNullOrWhiteSpace(this._account) || this._account.Trim().Length > 10 || string.IsNullOrWhiteSpace(this._reason)
            || this._reason.Trim().Length > 500 || (!remove && (this._days is <= 0 or > 3650 || this._snapshot?.Configuration.Levels.All(level => level.Id != this._levelId) != false)))
        {
            this._message = Resources.VipManagerInvalid;
            return;
        }

        this._change = new(Guid.NewGuid(), this._account.Trim(), remove ? 0 : this._levelId, remove ? 0 : this._days, remove, this._reason.Trim());
        await this.ReviewAsync().ConfigureAwait(true);
    }

    private async Task RetryAsync() => await this.ReviewAsync().ConfigureAwait(true);

    private async Task ReviewAsync()
    {
        if (this._busy || this._change is not { } change)
        {
            return;
        }

        this._busy = true;
        try
        {
            var result = await this.ChangeAsync(change, false, null).ConfigureAwait(true);
            this._confirmation = result["confirmationToken"]?.GetValue<string>();
            var date = result["change"]?["currentExpiryUtc"]?.GetValue<string>();
            this._currentUntil = DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
            this._message = null;
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "VIP host preview could not be confirmed.");
            this._confirmation = null;
            this._message = Resources.VipManagerUnavailable;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task ConfirmAsync()
    {
        if (this._busy || this._change is not { } change || this._confirmation is not { } confirmation)
        {
            return;
        }

        this._busy = true;
        this._confirmation = null;
        try
        {
            var result = await this.ChangeAsync(change, true, confirmation).ConfigureAwait(true);
            var status = result["status"]?.GetValue<string>();
            this._message = status switch
            {
                "Applied" => change.Remove ? Resources.VipManagerAppliedRemove : Resources.VipManagerAppliedGrant,
                "Online" => Resources.VipManagerOnline,
                "Pending" => Resources.VipManagerPending,
                _ => Resources.VipManagerConflict,
            };
            if (status is not ("Pending" or "Online"))
            {
                this._change = null;
            }

            try
            {
                await this.ReadAsync(false).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                // A failed list refresh must not turn an already confirmed result back into an uncertain change.
                this.Logger.LogWarning(exception, "The VIP change result is known, but the account list could not refresh.");
            }
        }
        catch (Exception exception)
        {
            // Preserve the stable operation even when only the response/audit completion was lost.
            this.Logger.LogError(exception, "VIP host change could not be confirmed; retry the same operation.");
            this._message = Resources.VipManagerUnavailable;
        }
        finally
        {
            this._busy = false;
        }
    }

    private ValueTask<JsonObject> ChangeAsync(ChangeDraft change, bool apply, string? confirmation)
        => this.Operations.ChangeAsync(change.Login, change.Id, change.Level, change.Days, change.Remove, change.Reason, apply, confirmation, default);

    private string ReviewText(ChangeDraft change)
        => change.Remove ? string.Format(Resources.VipManagerReviewRemoveText, change.Login)
            : string.Format(Resources.VipManagerReviewGrantText, change.Days, change.Level, change.Login);

    private void CancelReview()
    {
        this._change = null;
        this._confirmation = null;
        this._currentUntil = null;
    }

    private void AddLevel()
    {
        if (this._configuration.Levels.Count >= 12)
        {
            return;
        }

        this._configuration.Levels.Add(new()
        {
            Id = this._configuration.Levels.Select(level => level.Id).DefaultIfEmpty().Max() + 1,
            NameEn = Resources.ResourceManager.GetString(nameof(Resources.VipManagerNewName), CultureInfo.GetCultureInfo("en"))!,
            NameVi = Resources.ResourceManager.GetString(nameof(Resources.VipManagerNewName), CultureInfo.GetCultureInfo("vi"))!,
            Offers = [new(7, 0, CashShopCoinType.WCoinC)],
        });
    }

    private void AddOffer(VipLevel level)
    {
        if (level.Offers.Count < 4)
        {
            var days = new[] { 7, 30, 90, 365 }.First(value => level.Offers.All(offer => offer.Days != value));
            level.Offers.Add(new(days, 0, CashShopCoinType.WCoinC));
        }
    }

    private void OfferDays(VipLevel level, int index, ChangeEventArgs change)
    {
        if (int.TryParse(change.Value?.ToString(), out var days))
        {
            level.Offers[index] = level.Offers[index] with { Days = days };
        }
    }

    private void OfferPrice(VipLevel level, int index, ChangeEventArgs change)
    {
        if (int.TryParse(change.Value?.ToString(), out var price))
        {
            level.Offers[index] = level.Offers[index] with { Price = price };
        }
    }

    private void OfferCoin(VipLevel level, int index, ChangeEventArgs change)
    {
        if (Enum.TryParse<CashShopCoinType>(change.Value?.ToString(), out var coin) && Enum.IsDefined(coin))
        {
            level.Offers[index] = level.Offers[index] with { CoinType = coin };
        }
    }

    private sealed record ChangeDraft(Guid Id, string Login, int Level, int Days, bool Remove, string Reason);
}
