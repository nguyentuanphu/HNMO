// <copyright file="ResetsRelife.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page "Resets and relife": every option of the <see cref="ResetOptionsPlugIn"/> (saved like the plug-in editor
/// does): tiers, VIP (and giving VIP days to an account), relife, the delegated reset and the website; the rankings,
/// the log and who has the delegated reset on. English and Vietnamese.
/// </summary>
public partial class ResetsRelife : ComponentBase
{
    /// <summary>The log lines shown.</summary>
    internal const int LogShown = 200;

    private readonly List<string> _errors = new();
    private readonly List<TierRow> _tiers = new();
    private ResetOptionsConfiguration? _configuration;
    private ResetEngine? _engine;
    private string _relifeItems = string.Empty;
    private string _logFilter = string.Empty;
    private string _logSource = string.Empty;
    private string _vipAccount = string.Empty;
    private int _vipValue = 1;
    private int _vipDays = 30;
    private string? _vipMessage;
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
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<ResetsRelife> Logger { get; set; } = null!;

    private static bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    /// <summary>
    /// Reads items written as "group:number:level:count; ...".
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="items">The items.</param>
    /// <returns><c>true</c>, if every part was valid.</returns>
    internal static bool TryParseItems(string? text, out List<ResetItemCost> items)
    {
        items = new List<ResetItemCost>();
        foreach (var part in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var numbers = part.Split(':', StringSplitOptions.TrimEntries);
            if (numbers.Length is < 2 or > 4 || !numbers.All(n => int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                return false;
            }

            var values = numbers.Select(n => int.Parse(n, CultureInfo.InvariantCulture)).ToArray();
            var item = new ResetItemCost
            {
                Group = (short)values[0],
                Number = (short)values[1],
                MinimumLevel = (byte)(values.Length > 2 ? values[2] : 0),
                Count = values.Length > 3 ? values[3] : 1,
            };
            if (values[0] is < 0 or > 15 || values[1] is < 0 or > 511 || (values.Length > 2 && values[2] is < 0 or > 15) || item.Count is < 1 or > 100)
            {
                return false;
            }

            items.Add(item);
        }

        return true;
    }

    /// <summary>
    /// Writes items as "group:number:level:count; ...".
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The text.</returns>
    internal static string ItemsText(IEnumerable<ResetItemCost> items) =>
        string.Join("; ", items.Select(i => string.Create(CultureInfo.InvariantCulture, $"{i.Group}:{i.Number}:{i.MinimumLevel}:{i.Count}")));

    /// <summary>
    /// Checks the edited configuration and puts the tiers and items into it.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="tiers">The edited tiers with their items text.</param>
    /// <param name="relifeItems">The relife's items text.</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Validate(ResetOptionsConfiguration configuration, IReadOnlyList<(ResetTier Tier, string Items)> tiers, string relifeItems)
    {
        var errors = new List<string>();
        var list = new List<ResetTier>();
        for (var index = 0; index < tiers.Count; index++)
        {
            var (tier, itemsText) = tiers[index];
            var number = index + 1;
            if (!TryParseItems(itemsText, out var items))
            {
                errors.Add(T($"Tier {number}: the items aren't written as group:number:level:count.", $"Mức {number}: vật phẩm phải viết dạng nhóm:số:cấp:số lượng."));
            }

            if (tier.ToResets >= 0 && tier.ToResets < tier.FromResets)
            {
                errors.Add(T($"Tier {number}: \"to\" is lower than \"from\".", $"Mức {number}: \"đến\" nhỏ hơn \"từ\"."));
            }

            tier.FromResets = Math.Max(0, tier.FromResets);
            tier.ToResets = Math.Max(-1, tier.ToResets);
            tier.RequiredLevel = Math.Clamp(tier.RequiredLevel, 1, 2000);
            tier.LevelAfterReset = Math.Clamp(tier.LevelAfterReset, 1, 2000);
            tier.Zen = Math.Max(0, tier.Zen);
            tier.Coins = Math.Clamp(tier.Coins, 0, 1_000_000);
            tier.Points = Math.Clamp(tier.Points, 0, 100_000);
            tier.DailyLimit = Math.Clamp(tier.DailyLimit, 0, 1000);
            tier.Items = items;
            list.Add(tier);
        }

        for (var a = 0; a < list.Count; a++)
        {
            for (var b = a + 1; b < list.Count; b++)
            {
                var (x, y) = (list[a], list[b]);
                var xEnd = x.ToResets < 0 ? int.MaxValue : x.ToResets;
                var yEnd = y.ToResets < 0 ? int.MaxValue : y.ToResets;
                if (x.FromResets <= yEnd && y.FromResets <= xEnd)
                {
                    errors.Add(T($"Tiers {a + 1} and {b + 1} overlap: the first one counts.", $"Mức {a + 1} và {b + 1} trùng nhau: mức đầu được dùng."));
                }
            }
        }

        if (!TryParseItems(relifeItems, out var relife))
        {
            errors.Add(T("Relife: the items aren't written as group:number:level:count.", "Relife: vật phẩm phải viết dạng nhóm:số:cấp:số lượng."));
        }

        configuration.Tiers = list;
        configuration.Relife.Items = relife;
        configuration.MaximumResets = Math.Max(0, configuration.MaximumResets);
        configuration.DayStartHour = Math.Clamp(configuration.DayStartHour, 0, 23);
        configuration.RankingSize = Math.Clamp(configuration.RankingSize, 5, 500);
        configuration.LogKept = Math.Clamp(configuration.LogKept, 10, 100_000);
        configuration.Vip.MinimumVipLevel = Math.Clamp(configuration.Vip.MinimumVipLevel, 1, 100);
        configuration.Vip.ZenDiscountPercent = Math.Clamp(configuration.Vip.ZenDiscountPercent, 0, 100);
        configuration.Vip.CoinsDiscountPercent = Math.Clamp(configuration.Vip.CoinsDiscountPercent, 0, 100);
        configuration.Vip.LevelReduction = Math.Clamp(configuration.Vip.LevelReduction, 0, 1000);
        configuration.Vip.ExtraDailyResets = Math.Clamp(configuration.Vip.ExtraDailyResets, 0, 1000);
        configuration.Vip.ExtraPointsPercent = Math.Clamp(configuration.Vip.ExtraPointsPercent, 0, 1000);
        configuration.Relife.ResetsNeeded = Math.Clamp(configuration.Relife.ResetsNeeded, 1, 100_000);
        configuration.Relife.RequiredLevel = Math.Clamp(configuration.Relife.RequiredLevel, 1, 2000);
        configuration.Relife.LevelAfterRelife = Math.Clamp(configuration.Relife.LevelAfterRelife, 1, 2000);
        configuration.Relife.KeepResetsPercent = Math.Clamp(configuration.Relife.KeepResetsPercent, 0, 100);
        configuration.Relife.Zen = Math.Max(0, configuration.Relife.Zen);
        configuration.Relife.Coins = Math.Clamp(configuration.Relife.Coins, 0, 1_000_000);
        configuration.Relife.PointsPerRelife = Math.Clamp(configuration.Relife.PointsPerRelife, 0, 1_000_000);
        configuration.Relife.MaximumRelifes = Math.Max(0, configuration.Relife.MaximumRelifes);
        configuration.Relife.DailyLimit = Math.Clamp(configuration.Relife.DailyLimit, 0, 100);
        configuration.Delegated.ExtraZen = Math.Max(0, configuration.Delegated.ExtraZen);
        configuration.Delegated.ExtraCoins = Math.Clamp(configuration.Delegated.ExtraCoins, 0, 1_000_000);
        configuration.Delegated.DailyLimit = Math.Clamp(configuration.Delegated.DailyLimit, 0, 1000);
        configuration.Delegated.CheckSeconds = Math.Clamp(configuration.Delegated.CheckSeconds, 5, 3600);
        configuration.Website.ClearStatsZen = Math.Max(0, configuration.Website.ClearStatsZen);
        configuration.Website.ClearStatsCoins = Math.Clamp(configuration.Website.ClearStatsCoins, 0, 1_000_000);
        if (string.IsNullOrWhiteSpace(configuration.TimeZone))
        {
            configuration.TimeZone = "Australia/Sydney";
        }

        return errors.Distinct().ToList();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static string T(string english, string vietnamese) => IsVietnamese ? vietnamese : english;

    private static string ModeCaption(ResetPointsMode mode) => mode switch
    {
        ResetPointsMode.Total => T("Total", "Tổng"),
        ResetPointsMode.Add => T("Add", "Cộng thêm"),
        _ => T("Keep", "Giữ nguyên"),
    };

    private static string SourceCaption(ResetSource source) => source switch
    {
        ResetSource.Game => T("Game", "Trong game"),
        ResetSource.Website => "Website",
        _ => T("Delegated", "Ủy thác"),
    };

    private string MonthKey() => this._engine?.Clock.MonthKey(DateTime.UtcNow) ?? string.Empty;

    private IReadOnlyList<ResetRankingEntry> Monthly() =>
        this._engine is { } engine ? engine.MonthlyRanking(engine.Clock.MonthKey(DateTime.UtcNow), 20) : Array.Empty<ResetRankingEntry>();

    private IReadOnlyList<ResetRankingEntry> Relifes() => this._engine?.RelifeRanking(20) ?? Array.Empty<ResetRankingEntry>();

    private IReadOnlyList<ResetLogEntry> Log() =>
        this._engine?.Log(LogShown, this._logFilter, Enum.TryParse<ResetSource>(this._logSource, out var source) ? source : null) ?? Array.Empty<ResetLogEntry>();

    private List<(string Name, string Login, string Blocked)> Delegated()
    {
        if (this._engine is not { } engine)
        {
            return new List<(string, string, string)>();
        }

        lock (engine.Store.Lock)
        {
            return engine.Store.Data.Characters.Values.Where(r => r.Delegated).OrderBy(r => r.Name, StringComparer.Ordinal)
                .Select(r => (r.Name, r.Login, r.DelegatedBlocked)).Take(200).ToList();
        }
    }

    private string Sydney(DateTime utc) =>
        this._engine is null ? string.Empty : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), this._engine.Clock.TimeZone).ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentUICulture);

    private void AddTier()
    {
        var last = this._tiers.LastOrDefault()?.Tier;
        var from = last is null ? 0 : (last.ToResets >= 0 ? last.ToResets + 1 : last.FromResets + 1);
        if (last is { ToResets: < 0 })
        {
            last.ToResets = Math.Max(last.FromResets, from + 49) - 1;
            from = last.ToResets + 1;
        }

        this._tiers.Add(new TierRow(new ResetTier { FromResets = from, ToResets = -1, RequiredLevel = last?.RequiredLevel ?? 400, Zen = last?.Zen ?? 0, Points = last?.Points ?? 500 }, string.Empty));
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<ResetOptionsConfiguration>(this.PersistenceContextProvider, typeof(ResetOptionsPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = Copy(saved ?? new ResetOptionsConfiguration());
            this._configuration = configuration;
            this._tiers.Clear();
            this._tiers.AddRange(configuration.Tiers.Select(t => new TierRow(t, ItemsText(t.Items))));
            this._relifeItems = ItemsText(configuration.Relife.Items);

            // A copy for the rankings and the log, so that an unsaved edit doesn't change them.
            var live = Copy(configuration);
            this._engine = new ResetEngine(live, ResetStore.Open(live.FileName));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the reset options.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private static ResetOptionsConfiguration Copy(ResetOptionsConfiguration configuration) =>
        JsonSerializer.Deserialize<ResetOptionsConfiguration>(JsonSerializer.Serialize(configuration)) ?? new ResetOptionsConfiguration();

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Validate(configuration, this._tiers.Select(r => (r.Tier, r.ItemsText)).ToList(), this._relifeItems));
        if (this._errors.Count > 0)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(ResetOptionsPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<ResetOptionsConfiguration>(this.PersistenceContextProvider, typeof(ResetOptionsPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the reset options, active: {Active}.", this._isActive);
            this.ToastService.ShowSuccess(T("The reset options were saved.", "Đã lưu cài đặt reset."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the reset options.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task GiveVipAsync()
    {
        var login = this._vipAccount.Trim();
        var days = Math.Clamp(this._vipDays, 1, 3650);
        var value = Math.Clamp(this._vipValue, 1, 100);
        if (login.Length == 0)
        {
            this._vipMessage = T("Enter the account name.", "Hãy nhập tên tài khoản.");
            return;
        }

        this._isSaving = true;
        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        var reserved = false;
        try
        {
            var configuration = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, default).ConfigureAwait(true);
            var attribute = configuration.Attributes.FirstOrDefault(a => a.Id == Stats.IsVip.Id);
            if (attribute is null)
            {
                this._vipMessage = T("The game configuration has no \"Is VIP\" attribute.", "Cấu hình game không có thuộc tính \"Is VIP\".");
                return;
            }

            reserved = await this.LoginServer.TryReserveAccountAsync(login, reservation).ConfigureAwait(true);
            if (!reserved)
            {
                this._vipMessage = T("The account is in the game: try again when it's offline.", "Tài khoản đang trong game: hãy thử lại khi tài khoản offline.");
                return;
            }

            using var context = this.PersistenceContextProvider.CreateNewPlayerContext(configuration);
            if (await context.GetAccountByLoginNameAsync(login, default).ConfigureAwait(true) is not { } account)
            {
                this._vipMessage = T("There is no such account.", "Không có tài khoản này.");
                return;
            }

            var grant = context.CreateNew<AccountAttributeGrant>();
            grant.Attribute = attribute;
            grant.Value = value;
            grant.ValidUntil = DateTime.UtcNow.AddDays(days);
            account.AttributeGrants.Add(grant);
            await context.SaveChangesAsync().ConfigureAwait(true);
            this.Logger.LogInformation("Resets: gave VIP {Value} for {Days} days to {Login}.", value, days, login);
            this._vipMessage = T($"{account.LoginName} is VIP {value} until {this.Sydney(grant.ValidUntil.Value)} (Sydney time).", $"{account.LoginName} là VIP {value} đến {this.Sydney(grant.ValidUntil.Value)} (giờ Sydney).");
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error giving VIP.");
            this._vipMessage = string.Format(Resources.UnexpectedErrorOccurred, ex.Message);
        }
        finally
        {
            if (reserved)
            {
                await this.LoginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(true);
            }

            this._isSaving = false;
        }
    }

    /// <summary>
    /// A tier being edited with its items as text.
    /// </summary>
    private sealed class TierRow(ResetTier tier, string itemsText)
    {
        public ResetTier Tier { get; } = tier;

        public string ItemsText { get; set; } = itemsText;
    }
}
