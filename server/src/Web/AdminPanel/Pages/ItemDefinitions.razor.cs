// <copyright file="ItemDefinitions.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.ItemDefinitions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin panel page for managing item definitions (server <see cref="GameConfiguration.Items"/> + live EN/VI web game sync).
/// </summary>
public partial class ItemDefinitions : ComponentBase
{
    private const int PageSize = 30;

    private GameConfiguration? _gameConfiguration;
    private IContext? _persistenceContext;
    private List<ItemDefinition> _items = [];
    private Dictionary<string, ItemClientMetadata> _overrides = new(StringComparer.Ordinal);
    private ItemClientMetadata? _editing;
    private bool _isNewItem;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _isVi;
    private bool _onlyCustomized;
    private bool _autoSyncDataOnSave;
    private int _selectedGroupFilter = -1;
    private string _searchTerm = string.Empty;
    private int _pageIndex;
    private string? _resolvedDataRoot;
    private string? _statusMessage;
    private bool _statusIsError;

    [Inject]
    private IDataSource<GameConfiguration> DataSource { get; set; } = null!;

    [Inject]
    private IHostSettingsService HostSettingsService { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<ItemDefinitions> Logger { get; set; } = null!;

    private List<ItemDefinition> FilteredItems => this._items
        .Where(item => this._selectedGroupFilter < 0 || item.Group == this._selectedGroupFilter)
        .Where(item => !this._onlyCustomized || this.HasOverride(item.Group, item.Number))
        .Where(this.MatchesSearch)
        .ToList();

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(this.FilteredItems.Count / (double)PageSize));

    private List<ItemDefinition> PagedItems
    {
        get
        {
            var filtered = this.FilteredItems;
            var safePage = Math.Clamp(this._pageIndex, 0, Math.Max(0, this.TotalPages - 1));
            return filtered.Skip(safePage * PageSize).Take(PageSize).ToList();
        }
    }

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        return this.LoadAsync();
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private bool HasOverride(byte group, short number) =>
        this._overrides.ContainsKey(ItemClientMetadata.FormatKey(group, number));

    private bool MatchesSearch(ItemDefinition item)
    {
        if (string.IsNullOrWhiteSpace(this._searchTerm))
        {
            return true;
        }

        var term = this._searchTerm.Trim();
        var key = ItemClientMetadata.FormatKey(item.Group, item.Number);
        if (key.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var row = this.BuildPreviewRow(item);
        return row.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase)
            || row.NameVi.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private ItemClientMetadata BuildPreviewRow(ItemDefinition item)
    {
        var key = ItemClientMetadata.FormatKey(item.Group, item.Number);
        this._overrides.TryGetValue(key, out var existing);
        return ItemDefinitionStateStore.FromDefinition(item, existing);
    }

    private string FormatRequirements(ItemClientMetadata meta)
    {
        var parts = new List<string>();
        if (meta.LevelRequirement > 0)
        {
            parts.Add($"Lv{meta.LevelRequirement}");
        }

        if (meta.StrengthRequirement > 0)
        {
            parts.Add($"Str{meta.StrengthRequirement}");
        }

        if (meta.AgilityRequirement > 0)
        {
            parts.Add($"Agi{meta.AgilityRequirement}");
        }

        if (meta.VitalityRequirement > 0)
        {
            parts.Add($"Vit{meta.VitalityRequirement}");
        }

        if (meta.EnergyRequirement > 0)
        {
            parts.Add($"Ene{meta.EnergyRequirement}");
        }

        if (meta.LeadershipRequirement > 0)
        {
            parts.Add($"Cmd{meta.LeadershipRequirement}");
        }

        return parts.Count == 0 ? "-" : string.Join(" / ", parts);
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var settings = this.HostSettingsService.Load();
            this._resolvedDataRoot = ItemClientDataSync.TryResolveDataRoot(settings.DataPath);
            this._persistenceContext = await this.DataSource.GetContextAsync(default).ConfigureAwait(true);
            this._gameConfiguration = await this.DataSource.GetOwnerAsync(cancellationToken: default).ConfigureAwait(true);
            this._items = this._gameConfiguration.Items
                .OrderBy(i => i.Group)
                .ThenBy(i => i.Number)
                .ToList();
            this._overrides = ItemDefinitionStateStore.GetAll()
                .ToDictionary(x => x.Key, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed loading Item Definitions.");
            this._statusMessage = ex.Message;
            this._statusIsError = true;
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void StartNewItem()
    {
        var defaultGroup = (byte)Math.Clamp(this._selectedGroupFilter >= 0 ? this._selectedGroupFilter : 0, 0, 15);
        var usedNumbers = this._items.Where(i => i.Group == defaultGroup).Select(i => (int)i.Number).ToHashSet();
        short nextNumber = 50;
        while (usedNumbers.Contains(nextNumber) && nextNumber < 511)
        {
            nextNumber++;
        }

        this._isNewItem = true;
        this._editing = new ItemClientMetadata
        {
            Group = defaultGroup,
            Number = nextNumber,
            NameEn = "Custom HNMO Item",
            NameVi = "Vật Phẩm Tùy Chỉnh",
            Width = 1,
            Height = 2,
            DropLevel = 10,
            MaximumItemLevel = 15,
            Durability = 30,
            BuyPrice = 10000,
            DropsFromMonsters = false,
            GlowColor = "gold",
            ModelSourceGroup = defaultGroup,
            ModelSourceNumber = 0,
        };
        this._statusMessage = null;
    }

    private void StartEditItem(ItemDefinition item)
    {
        this._isNewItem = false;
        this._editing = this.BuildPreviewRow(item);
        this._statusMessage = null;
    }

    private void CancelEdit()
    {
        this._editing = null;
        this._isNewItem = false;
    }

    private async Task SaveEditingItemAsync()
    {
        if (this._editing is not { } model || this._gameConfiguration is null || this._persistenceContext is null)
        {
            return;
        }

        this._statusMessage = null;
        this._statusIsError = false;

        if (string.IsNullOrWhiteSpace(model.NameEn))
        {
            this._statusIsError = true;
            this._statusMessage = this.L("English item name is required.", "Bắt buộc nhập tên tiếng Anh của vật phẩm.");
            return;
        }

        if (model.Width < 1 || model.Width > 8 || model.Height < 1 || model.Height > 8)
        {
            this._statusIsError = true;
            this._statusMessage = this.L("Width and Height must be between 1 and 8 cells.", "Chiều rộng và chiều cao phải từ 1 đến 8 ô.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(model.GlowColor) && string.IsNullOrEmpty(ItemClientDataSync.NormalizeGlowColor(model.GlowColor)))
        {
            this._statusIsError = true;
            this._statusMessage = this.L("Glow color must match a preset name or #rrggbb hex code.", "Màu phát sáng phải là tên chuẩn hoặc mã hex #rrggbb.");
            return;
        }

        this._isSaving = true;
        try
        {
            var existing = this._gameConfiguration.Items.FirstOrDefault(i => i.Group == model.Group && i.Number == model.Number);
            if (this._isNewItem)
            {
                if (existing is not null)
                {
                    this._statusIsError = true;
                    this._statusMessage = this.L(
                        $"Item {model.Group}:{model.Number} already exists. Pick a free number or edit the existing row.",
                        $"Vật phẩm {model.Group}:{model.Number} đã tồn tại. Hãy chọn số hiệu khác hoặc sửa dòng sẵn có.");
                    return;
                }

                existing = this._persistenceContext.CreateNew<ItemDefinition>();
                existing.Group = model.Group;
                existing.Number = model.Number;
                this._gameConfiguration.Items.Add(existing);
            }
            else if (existing is null)
            {
                this._statusIsError = true;
                this._statusMessage = this.L("Selected item no longer exists in configuration.", "Vật phẩm đã chọn không còn trong cấu hình.");
                return;
            }

            ItemDefinitionStateStore.ApplyToDefinition(existing, model, this._gameConfiguration, this._persistenceContext);
            await this._persistenceContext.SaveChangesAsync().ConfigureAwait(true);

            var snapshot = ItemDefinitionStateStore.Upsert(model);
            this._overrides = snapshot.ToDictionary(x => x.Key, StringComparer.Ordinal);
            this._items = this._gameConfiguration.Items.OrderBy(i => i.Group).ThenBy(i => i.Number).ToList();

            var syncedFilesText = string.Empty;
            if (this._autoSyncDataOnSave && this._resolvedDataRoot is not null)
            {
                var written = ItemClientDataSync.ApplyOverridesToDataDirectory(this._resolvedDataRoot, snapshot);
                syncedFilesText = this.L($" Synced {written.Count} JSON file(s).", $" Đã đồng bộ {written.Count} file JSON.");
            }

            this._statusIsError = false;
            this._statusMessage = this.L(
                $"Saved item {model.Group}:{model.Number} ({model.NameEn}). Web feed updated.{syncedFilesText}",
                $"Đã lưu vật phẩm {model.Group}:{model.Number} ({model.NameEn}). Dữ liệu web đã cập nhật.{syncedFilesText}");
            this.ToastService.ShowSuccess(this._statusMessage);
            this._isNewItem = false;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving item definition {Group}:{Number}.", model.Group, model.Number);
            this._statusIsError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void ResetOverride()
    {
        if (this._editing is not { } model)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            ItemDefinitionStateStore.Remove(model.Group, model.Number);
            this._overrides.Remove(ItemClientMetadata.FormatKey(model.Group, model.Number));
            this._editing = null;
            this._statusIsError = false;
            this._statusMessage = this.L("Client override removed.", "Đã xóa tùy biến phía client.");
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void SyncDataFolder()
    {
        if (this._resolvedDataRoot is null)
        {
            return;
        }

        try
        {
            var written = ItemClientDataSync.ApplyOverridesToDataDirectory(this._resolvedDataRoot, ItemDefinitionStateStore.GetAll());
            this._statusIsError = false;
            this._statusMessage = this.L(
                $"Synchronized {written.Count} file(s) in {this._resolvedDataRoot}.",
                $"Đã đồng bộ {written.Count} tệp trong {this._resolvedDataRoot}.");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed syncing item data files.");
            this._statusIsError = true;
            this._statusMessage = ex.Message;
        }
    }
}
