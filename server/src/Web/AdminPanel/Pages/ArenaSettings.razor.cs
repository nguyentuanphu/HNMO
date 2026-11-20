// <copyright file="ArenaSettings.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Text;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Arena;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The page "Arena characters &amp; battles" (owner's wish, 2026-11-20): the arena character of every class (level, stats,
/// gear, class balance) and the main settings of the team battles and the battle royale. It saves the Arena plug-in's
/// configuration like the plug-in editor does; the templates only apply in the Arena program, so this page belongs in
/// the Arena's admin panel.
/// </summary>
public partial class ArenaSettings : ComponentBase
{
    private readonly List<string> _errors = new();
    private readonly List<TemplateRow> _rows = new();
    private ArenaConfiguration? _configuration;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<ArenaSettings> Logger { get; set; } = null!;

    private static bool IsVietnamese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    private static bool InArena => HnmoSeasonInstance.IsArena;

    /// <summary>The gear of a template as text, one item a line: slot, group, number, level, skill, luck, option, excellent, durability.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The text.</returns>
    internal static string ItemsText(IEnumerable<ArenaTemplateItem> items)
    {
        var text = new StringBuilder();
        foreach (var i in items)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{i.Slot}, {i.Group}, {i.Number}, {i.Level}, {(i.Skill ? 1 : 0)}, {(i.Luck ? 1 : 0)}, {i.OptionLevel}, {i.ExcellentOptions}, {i.Durability}"));
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>Reads gear text back (see <see cref="ItemsText"/>); the last fields may be left out.</summary>
    /// <param name="text">The text.</param>
    /// <param name="items">The items.</param>
    /// <param name="error">The first wrong line, if any.</param>
    /// <returns><c>true</c>, if every line is right.</returns>
    internal static bool TryParseItems(string? text, out List<ArenaTemplateItem> items, out string? error)
    {
        items = [];
        error = null;
        var lineNumber = 0;
        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            var numbers = new int[9];
            if (parts.Length < 3 || parts.Length > 9 || parts.Select((p, i) => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i])).Any(ok => !ok))
            {
                error = string.Create(CultureInfo.InvariantCulture, $"line {lineNumber}: \"{trimmed}\"");
                return false;
            }

            if (numbers[0] is < 0 or > 255 || numbers[1] is < 0 or > 15 || numbers[2] is < 0 or > 511 || numbers[3] is < 0 or > 15
                || numbers[6] is < 0 or > 7 || numbers[7] is < 0 or > 63 || numbers[8] is < 0 or > 255)
            {
                error = string.Create(CultureInfo.InvariantCulture, $"line {lineNumber}: \"{trimmed}\" (out of range)");
                return false;
            }

            items.Add(new ArenaTemplateItem
            {
                Slot = (byte)numbers[0],
                Group = (byte)numbers[1],
                Number = (short)numbers[2],
                Level = (byte)numbers[3],
                Skill = numbers[4] != 0,
                Luck = numbers[5] != 0,
                OptionLevel = (byte)numbers[6],
                ExcellentOptions = (byte)numbers[7],
                Durability = (byte)numbers[8],
            });
        }

        return true;
    }

    /// <summary>Checks the edits and puts them into the configuration.</summary>
    /// <param name="configuration">The configuration (already holding the battle settings).</param>
    /// <param name="rows">The template rows with their gear text.</param>
    /// <returns>The errors (EN/VI by the current culture); none when it's valid.</returns>
    internal static List<string> Apply(ArenaConfiguration configuration, IReadOnlyList<(ArenaClassTemplate Template, string ItemsText)> rows)
    {
        var errors = new List<string>();
        foreach (var (template, itemsText) in rows)
        {
            if (template.Level is < 1 or > 1000 || new[] { template.Strength, template.Agility, template.Vitality, template.Energy, template.Leadership }.Any(v => v is < 0 or > 32767))
            {
                errors.Add(L($"{template.Name}: level 1-1000 and stats 0-32767.", $"{template.Name}: cấp 1-1000 và chỉ số 0-32767."));
            }

            if (template.DamagePercent is < 10 or > 500 || template.DefensePercent is < 10 or > 500)
            {
                errors.Add(L($"{template.Name}: damage and toughness 10-500 %.", $"{template.Name}: sát thương và độ chịu đòn 10-500 %."));
            }

            if (!TryParseItems(itemsText, out var items, out var error))
            {
                errors.Add(L($"{template.Name}: a gear line is wrong ({error}).", $"{template.Name}: một dòng trang bị bị sai ({error})."));
            }
            else
            {
                template.Items = items;
            }
        }

        if (!WorldBossSchedule.TryParseTimes(configuration.RoyaleTimes, out _))
        {
            errors.Add(L("Battle royale times: write them as HH:mm, separated by commas (Sydney time).", "Giờ đấu sinh tồn: ghi dạng HH:mm, cách nhau bằng dấu phẩy (giờ Sydney)."));
        }

        if (configuration.TeamMinSize > configuration.TeamSize || configuration.RoyaleMinPlayers > configuration.RoyaleMaxPlayers)
        {
            errors.Add(L("The fewest players can't be more than the most.", "Số người tối thiểu không được lớn hơn tối đa."));
        }

        if (errors.Count == 0)
        {
            configuration.Templates = rows.Select(r => r.Template).ToList();
        }

        return errors;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
    }

    private static string L(string en, string vi) => IsVietnamese ? vi : en;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            var (exists, _, saved) = await PlugInSettings.ReadStoredAsync<ArenaConfiguration>(this.PersistenceContextProvider, typeof(ArenaPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            // A copy, so that an unsaved edit doesn't change a cached configuration.
            var configuration = JsonSerializer.Deserialize<ArenaConfiguration>(JsonSerializer.Serialize(saved ?? new ArenaConfiguration())) ?? new ArenaConfiguration();
            if (configuration.Templates.Count == 0)
            {
                configuration.Templates = ArenaConfiguration.DefaultTemplates();
            }

            this._configuration = configuration;
            this._rows.Clear();
            this._rows.AddRange(configuration.Templates.OrderBy(t => t.ClassFamily).Select(t => new TemplateRow(t, ItemsText(t.Items))));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the Arena configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void ResetTemplate(TemplateRow row)
    {
        if (ArenaConfiguration.DefaultTemplates().FirstOrDefault(t => t.ClassFamily == row.Template.ClassFamily) is { } template)
        {
            row.Template = template;
            row.ItemsText = ItemsText(template.Items);
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Apply(configuration, this._rows.Select(r => (r.Template, r.ItemsText)).ToList()));
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
            var id = typeof(ArenaPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            this.Logger.LogInformation("Saved the Arena configuration: {Templates} class templates.", configuration.Templates.Count);
            this.ToastService.ShowSuccess(L("Saved. New arena characters use it from their next entry.", "Đã lưu. Nhân vật Đấu trường dùng cài đặt mới từ lần vào tiếp theo."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the Arena configuration.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private sealed class TemplateRow(ArenaClassTemplate template, string itemsText)
    {
        public ArenaClassTemplate Template { get; set; } = template;

        public string ItemsText { get; set; } = itemsText;
    }
}
