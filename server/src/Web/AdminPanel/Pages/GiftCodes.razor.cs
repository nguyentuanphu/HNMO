// <copyright file="GiftCodes.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The page "Gift codes": the settings of the <see cref="GiftCodesPlugIn"/> (saved like the plug-in editor does), and
/// the codes of its store: make a code or a batch of random single-use codes, edit or switch off a code, download a
/// code's or a batch's codes as CSV, see the uses and the redemption log.
/// </summary>
public partial class GiftCodes : ComponentBase
{
    /// <summary>The most codes of one batch.</summary>
    internal const int MaximumBatch = 10_000;

    /// <summary>The log lines shown.</summary>
    internal const int LogShown = 200;

    private readonly List<string> _errors = new();
    private GiftCodesConfiguration? _configuration;
    private GiftCodeEngine? _engine;
    private CodeDraft? _draft;
    private string? _confirmDelete;
    private (string FileName, string Href)? _csv;
    private string _logFilter = string.Empty;
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<GiftCodes> Logger { get; set; } = null!;

    /// <summary>
    /// Checks a code being made or edited and puts the cleaned values into it.
    /// </summary>
    /// <param name="draft">The draft.</param>
    /// <param name="others">The other codes of the store (normalized), which the code must not repeat.</param>
    /// <param name="zone">The time zone of the dates.</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Validate(CodeDraft draft, ISet<string> others, TimeZoneInfo zone)
    {
        var errors = new List<string>();
        var code = draft.Code;
        code.Note = code.Note?.Trim() ?? string.Empty;
        if (draft.IsBatch)
        {
            draft.Prefix = GiftCodeEngine.Normalize(draft.Prefix);
            if (draft.IsNew)
            {
                if (draft.Prefix.Length > 12 || (draft.Prefix.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(draft.Prefix, "^[A-Z0-9_-]+$")))
                {
                    errors.Add(Resources.GiftCodesErrorPrefix);
                }

                if (draft.Count < 1 || draft.Count > MaximumBatch)
                {
                    errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.GiftCodesErrorCount, MaximumBatch));
                }

                draft.Length = Math.Clamp(draft.Length, 4, 16);
            }

            code.Public = false;
        }
        else
        {
            code.Code = GiftCodeEngine.Normalize(code.Code);
            if (!GiftCodeEngine.IsValidCode(code.Code))
            {
                errors.Add(Resources.GiftCodesErrorCode);
            }
            else if (others.Contains(code.Code))
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.GiftCodesErrorDuplicate, code.Code));
            }
        }

        DateTime? start = null;
        DateTime? end = null;
        if (!string.IsNullOrWhiteSpace(code.Start))
        {
            start = TopRaceEngine.ParseLocal(code.Start);
            if (start is null)
            {
                errors.Add(Resources.GiftCodesErrorDates);
            }
            else
            {
                code.Start = start.Value.ToString(TopRaceEngine.DateFormat, CultureInfo.InvariantCulture);
            }
        }
        else
        {
            code.Start = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(code.End))
        {
            end = TopRaceEngine.ParseLocal(code.End);
            if (end is null)
            {
                errors.Add(Resources.GiftCodesErrorDates);
            }
            else
            {
                code.End = end.Value.ToString(TopRaceEngine.DateFormat, CultureInfo.InvariantCulture);
            }
        }
        else
        {
            code.End = string.Empty;
        }

        if (start is { } s && end is { } e && TopRaceEngine.LocalToUtc(e, zone) <= TopRaceEngine.LocalToUtc(s, zone))
        {
            errors.Add(Resources.GiftCodesErrorDates);
        }

        var reward = code.Reward;
        reward.Coins = Math.Max(0, reward.Coins);
        reward.Zen = Math.Max(0, reward.Zen);
        foreach (var item in reward.Items)
        {
            if (item.Group is < 0 or > 15 || item.Number is < 0 or > 511 || item.Level > 15 || item.Count is < 1 or > 50 || item.OptionLevel > 7 || item.ExcellentOptions > 63)
            {
                errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.GiftCodesErrorItem, item.Group, item.Number));
            }
        }

        if (reward.IsEmpty)
        {
            errors.Add(Resources.GiftCodesErrorReward);
        }

        code.MaxUses = Math.Max(0, code.MaxUses);
        code.MinLevel = Math.Max(0, code.MinLevel);
        code.MaxLevel = Math.Max(0, code.MaxLevel);
        code.MinResets = Math.Max(0, code.MinResets);
        code.MaxResets = Math.Max(-1, code.MaxResets);
        if ((code.MaxLevel > 0 && code.MaxLevel < code.MinLevel) || (code.MaxResets >= 0 && code.MaxResets < code.MinResets))
        {
            errors.Add(Resources.GiftCodesErrorLimits);
        }

        code.Classes = code.Classes.Distinct().OrderBy(c => c).ToList();
        return errors;
    }

    /// <summary>
    /// Writes a CSV text as a data link for a download.
    /// </summary>
    /// <param name="csv">The text.</param>
    /// <returns>The link.</returns>
    internal static string CsvHref(string csv) => "data:text/csv;charset=utf-8;base64," + Convert.ToBase64String(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray());

    /// <summary>Gets the caption of a class.</summary>
    /// <param name="value">The class.</param>
    /// <returns>The caption.</returns>
    internal static string ClassCaption(TopRaceClass value) => TopRaces.ClassCaption(value);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static GiftCodeDefinition Copy(GiftCodeDefinition code) =>
        JsonSerializer.Deserialize<GiftCodeDefinition>(JsonSerializer.Serialize(code)) ?? new GiftCodeDefinition();

    private static string RewardText(GiftCodeReward reward)
    {
        var parts = new List<string>();
        if (reward.Coins > 0)
        {
            parts.Add($"{reward.Coins:N0} {CashShopCoinCaption.Get(reward.CoinType)}");
        }

        if (reward.Zen > 0)
        {
            parts.Add($"{reward.Zen:N0} zen");
        }

        foreach (var item in reward.Items.Where(i => i.Count > 0))
        {
            parts.Add($"{item.Group} {item.Number}{(item.Level > 0 ? $" +{item.Level}" : string.Empty)} x{item.Count}");
        }

        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    private string Sydney(DateTime utc) =>
        this._engine is null ? string.Empty : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), this._engine.Clock.TimeZone).ToString("ddd d MMM yyyy HH:mm", CultureInfo.CurrentUICulture);

    private List<GiftCodeDefinition> Codes()
    {
        if (this._engine is not { } engine)
        {
            return new List<GiftCodeDefinition>();
        }

        lock (engine.Store.Lock)
        {
            return engine.Store.Data.Codes.OrderByDescending(c => c.CreatedAtUtc).ToList();
        }
    }

    private List<GiftCodeLogEntry> Log()
    {
        if (this._engine is not { } engine)
        {
            return new List<GiftCodeLogEntry>();
        }

        var filter = this._logFilter.Trim();
        lock (engine.Store.Lock)
        {
            return engine.Store.Data.Log
                .Where(e => filter.Length == 0 || e.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) || e.Login.Contains(filter, StringComparison.OrdinalIgnoreCase) || e.CharacterName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Take(LogShown)
                .ToList();
        }
    }

    private string UsesText(GiftCodeDefinition code)
    {
        var uses = this._engine?.UsesOf(code.Id) ?? 0;
        return code.MaxUses > 0 ? $"{uses:N0} / {code.MaxUses:N0}" : $"{uses:N0}";
    }

    private string LimitText(GiftCodeDefinition code)
    {
        var parts = new List<string> { code.Limit == GiftCodeLimit.OncePerCharacter ? Resources.GiftCodesOncePerCharacter : Resources.GiftCodesOncePerAccount };
        if (code.OncePerNetwork)
        {
            parts.Add(Resources.GiftCodesOncePerNetwork);
        }

        if (code.OncePerDevice)
        {
            parts.Add(Resources.GiftCodesOncePerDevice);
        }

        if (code.MinLevel > 0 || code.MaxLevel > 0)
        {
            parts.Add($"{Resources.GiftCodesLevel} {(code.MinLevel > 0 ? code.MinLevel : 1)}-{(code.MaxLevel > 0 ? code.MaxLevel.ToString(CultureInfo.InvariantCulture) : "∞")}");
        }

        if (code.MinResets > 0 || code.MaxResets >= 0)
        {
            parts.Add($"{Resources.GiftCodesResets} {code.MinResets}-{(code.MaxResets >= 0 ? code.MaxResets.ToString(CultureInfo.InvariantCulture) : "∞")}");
        }

        if (code.Classes.Count > 0)
        {
            parts.Add(string.Join("/", code.Classes.Select(ClassCaption)));
        }

        return string.Join(" · ", parts);
    }

    private string DatesText(GiftCodeDefinition code)
    {
        if (string.IsNullOrEmpty(code.Start) && string.IsNullOrEmpty(code.End))
        {
            return Resources.GiftCodesAlways;
        }

        return $"{(code.Start.Length > 0 ? code.Start : "…")} – {(code.End.Length > 0 ? code.End : "…")}";
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<GiftCodesConfiguration>(this.PersistenceContextProvider, typeof(GiftCodesPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new GiftCodesConfiguration();
            configuration = JsonSerializer.Deserialize<GiftCodesConfiguration>(JsonSerializer.Serialize(configuration)) ?? new GiftCodesConfiguration();
            this._configuration = configuration;

            // The codes live in the store; a copy of the settings, so that an unsaved edit doesn't change it.
            var live = JsonSerializer.Deserialize<GiftCodesConfiguration>(JsonSerializer.Serialize(configuration)) ?? new GiftCodesConfiguration();
            this._engine = new GiftCodeEngine(live, GiftCodeStore.Open(live.FileName));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the gift codes configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        configuration.WrongTriesLimit = Math.Clamp(configuration.WrongTriesLimit, 1, 1000);
        configuration.WrongTriesWindowMinutes = Math.Clamp(configuration.WrongTriesWindowMinutes, 1, 10_080);
        configuration.LogKept = Math.Clamp(configuration.LogKept, 10, 100_000);
        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(GiftCodesPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<GiftCodesConfiguration>(this.PersistenceContextProvider, typeof(GiftCodesPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the gift codes settings, active: {Active}.", this._isActive);
            this.ToastService.ShowSuccess(Resources.GiftCodesSaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the gift codes settings.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void NewCode(bool batch)
    {
        this._errors.Clear();
        this._csv = null;
        this._draft = new CodeDraft(new GiftCodeDefinition
        {
            Code = batch ? string.Empty : "GIFT" + GiftCodeEngine.GenerateCodes(string.Empty, 1, 6, new HashSet<string>())[0],
            Reward = new GiftCodeReward { Coins = 100 },
            Limit = GiftCodeLimit.OncePerAccount,
        })
        {
            IsNew = true,
            IsBatch = batch,
            Prefix = batch ? "HNMO" : string.Empty,
            Count = batch ? 50 : 1,
            Length = 8,
        };
    }

    private void Edit(GiftCodeDefinition code)
    {
        this._errors.Clear();
        this._csv = null;
        this._draft = new CodeDraft(Copy(code)) { IsNew = false, IsBatch = code.IsBatch, Prefix = code.BatchPrefix, Count = code.BatchCodes.Count };
    }

    private void CancelEdit()
    {
        this._draft = null;
        this._errors.Clear();
    }

    private void ToggleClass(TopRaceClass value, bool on)
    {
        if (this._draft is not { } draft)
        {
            return;
        }

        draft.Code.Classes.Remove(value);
        if (on)
        {
            draft.Code.Classes.Add(value);
        }
    }

    private void SaveCode()
    {
        if (this._draft is not { } draft || this._engine is not { } engine)
        {
            return;
        }

        this._errors.Clear();
        var store = engine.Store;
        var others = new HashSet<string>(StringComparer.Ordinal);
        lock (store.Lock)
        {
            foreach (var other in store.Data.Codes.Where(c => c.Id != draft.Code.Id))
            {
                if (!other.IsBatch)
                {
                    others.Add(GiftCodeEngine.Normalize(other.Code));
                }

                others.UnionWith(other.BatchCodes);
            }
        }

        this._errors.AddRange(Validate(draft, others, engine.Clock.TimeZone));
        if (this._errors.Count > 0)
        {
            return;
        }

        var code = draft.Code;
        try
        {
            lock (store.Lock)
            {
                if (draft.IsNew)
                {
                    code.Id = GiftCodeEngine.NewId();
                    code.CreatedAtUtc = DateTime.UtcNow;
                    if (draft.IsBatch)
                    {
                        code.Code = string.Empty;
                        code.BatchPrefix = draft.Prefix;
                        code.BatchCodes = GiftCodeEngine.GenerateCodes(draft.Prefix, draft.Count, draft.Length, others);
                    }

                    store.Data.Codes.Add(code);
                }
                else
                {
                    var index = store.Data.Codes.FindIndex(c => c.Id == code.Id);
                    if (index < 0)
                    {
                        this._errors.Add(Resources.GiftCodesErrorGone);
                        return;
                    }

                    // A batch keeps its codes.
                    code.BatchCodes = store.Data.Codes[index].BatchCodes;
                    code.BatchPrefix = store.Data.Codes[index].BatchPrefix;
                    store.Data.Codes[index] = code;
                }

                store.CodesChanged();
                store.SaveIfChanged();
            }

            this.Logger.LogInformation("Saved the gift code {Id} ({Code}).", code.Id, code.DisplayCode);
            this.ToastService.ShowSuccess(string.Format(CultureInfo.CurrentCulture, Resources.GiftCodesCodeSaved, code.DisplayCode));
            this._draft = null;
            if (draft.IsNew && draft.IsBatch)
            {
                this.PrepareCsv(code);
            }
        }
        catch (InvalidOperationException ex)
        {
            this._errors.Add(ex.Message);
        }
    }

    private void SetEnabled(GiftCodeDefinition code, bool enabled)
    {
        if (this._engine is not { } engine)
        {
            return;
        }

        lock (engine.Store.Lock)
        {
            code.Enabled = enabled;
            engine.Store.CodesChanged();
            engine.Store.SaveIfChanged();
        }

        this.Logger.LogInformation("Gift code {Id} switched {State}.", code.Id, enabled ? "on" : "off");
    }

    private void Delete(GiftCodeDefinition code)
    {
        if (this._confirmDelete != code.Id)
        {
            this._confirmDelete = code.Id;
            return;
        }

        this._confirmDelete = null;
        if (this._engine is not { } engine)
        {
            return;
        }

        lock (engine.Store.Lock)
        {
            // The uses, claims and the log stay: the references keep counting once.
            engine.Store.Data.Codes.RemoveAll(c => c.Id == code.Id);
            engine.Store.CodesChanged();
            engine.Store.SaveIfChanged();
        }

        this.Logger.LogInformation("Deleted the gift code {Id} ({Code}).", code.Id, code.DisplayCode);
        if (this._draft?.Code.Id == code.Id)
        {
            this._draft = null;
        }
    }

    private void PrepareCsv(GiftCodeDefinition code)
    {
        if (this._engine is not { } engine)
        {
            return;
        }

        var name = (code.IsBatch ? code.BatchPrefix + "-" + code.Id : code.Code).ToLowerInvariant();
        this._csv = ($"gift-codes-{name}.csv", CsvHref(engine.BatchCsv(code)));
    }

    /// <summary>
    /// A code being made or edited, with the batch's settings.
    /// </summary>
    internal sealed class CodeDraft
    {
        /// <summary>Initializes a new instance of the <see cref="CodeDraft"/> class.</summary>
        /// <param name="code">The code (a copy).</param>
        public CodeDraft(GiftCodeDefinition code) => this.Code = code;

        /// <summary>Gets the code.</summary>
        public GiftCodeDefinition Code { get; }

        /// <summary>Gets or sets a value indicating whether it's new.</summary>
        public bool IsNew { get; set; }

        /// <summary>Gets or sets a value indicating whether it's a batch.</summary>
        public bool IsBatch { get; set; }

        /// <summary>Gets or sets the batch's prefix.</summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>Gets or sets how many codes a new batch gets.</summary>
        public int Count { get; set; }

        /// <summary>Gets or sets the random letters of a new batch's codes.</summary>
        public int Length { get; set; } = 8;
    }
}
