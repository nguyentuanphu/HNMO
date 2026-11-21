// <copyright file="ShopWorkspace.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components;

using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.ItemEditor;
using HNMO.Web.Shared.Components.ItemPicker;
using HNMO.Web.Shared.Components.Shops;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

/// <summary>The graphical catalogue, placement and property controls for an isolated merchant draft.</summary>
public partial class ShopWorkspace
{
    private readonly InventoryPlacementService _placement = new(StorageType.Merchant, 0);
    private readonly ItemPriceCalculator _prices = new();
    private ItemPicker? _picker;
    private ShopDraftContext? _draftContext;
    private ShopItemDraft? _selected;
    private Item? _dragged;
    private bool _catalogueDrag;
    private byte _columnOffset;
    private byte _rowOffset;
    private string _message = string.Empty;
    private bool _error;

    /// <summary>Gets or sets the detached draft.</summary>
    [Parameter]
    [EditorRequired]
    public ShopDraft Draft { get; set; } = null!;

    /// <summary>Gets or sets the complete catalogue.</summary>
    [Parameter]
    public IReadOnlyList<ItemDefinition> Definitions { get; set; } = [];

    /// <summary>Gets or sets the character classes.</summary>
    [Parameter]
    public IReadOnlyList<CharacterClass> Classes { get; set; } = [];

    /// <summary>Gets or sets the page-owned lookup context.</summary>
    [Parameter]
    [EditorRequired]
    public IContext LookupContext { get; set; } = null!;

    /// <summary>Gets or sets a value indicating whether save is in progress.</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Gets or sets the summary/state notification.</summary>
    [Parameter]
    public EventCallback Changed { get; set; }

    /// <summary>Gets or sets browser confirmation.</summary>
    [Inject]
    public IJSRuntime JavaScript { get; set; } = null!;

    private ISet<(byte Group, short Number)> Stocked => this.Draft.Items.Where(i => i.Definition is not null).Select(i => (i.Definition!.Group, i.Definition.Number)).ToHashSet();
    private int CellsUsed => this.Draft.Items.Sum(i => (i.Definition?.Width ?? 1) * (i.Definition?.Height ?? 1));

    /// <inheritdoc />
    protected override void OnParametersSet() => this._draftContext ??= new ShopDraftContext(this.LookupContext);

    private static ShopItemDraft NewItem(ItemDefinition definition) => new() { Definition = definition, Durability = definition.Durability };

    private string PriceText(Item item)
    {
        if (item.Definition is null)
        {
            return Resources.ShopEditorPriceUnavailable;
        }

        try
        {
            return string.Format(Resources.ShopEditorPrice, this._prices.CalculateFinalBuyingPrice(item));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            return Resources.ShopEditorPriceUnavailable;
        }
    }

    private bool TryAdd(ItemDefinition definition, byte? slot = null)
    {
        var item = NewItem(definition);
        if (definition.Width == 0 || definition.Height == 0)
        {
            return false;
        }

        var destination = slot ?? this._placement.FindFreeSlot(item, this.Draft.Storage.Items);
        if (destination is null || !this._placement.CanMoveItem(item, destination.Value, this.Draft.Storage.Items, 0, InventoryConstants.WarehouseRows))
        {
            return false;
        }

        item.ItemSlot = destination.Value;
        this.Draft.Storage.Items.Add(item);
        this._selected = item;
        return true;
    }

    private async Task AddAsync(ItemDefinition definition)
    {
        if (this.Busy)
        {
            return;
        }

        this._error = !this.TryAdd(definition);
        this._message = this._error ? Resources.ShopEditorDoesNotFit : string.Empty;
        await this.EditedAsync().ConfigureAwait(true);
    }

    private async Task AddSelectedAsync()
    {
        if (this._picker is { } picker)
        {
            await this.AddManyAsync(picker.Selected.ToArray()).ConfigureAwait(true);
        }
    }

    private async Task AddCategoryAsync()
    {
        if (this.Busy || this._picker is not { Filter.Category: >= 0 } picker)
        {
            return;
        }

        var definitions = picker.Matches.ToArray();
        var preview = this.Draft.Items.Select(i => (Item)ShopItemDraft.Copy(i)).ToList();
        var fits = 0;
        foreach (var definition in definitions)
        {
            var item = NewItem(definition);
            if (definition.Width == 0 || definition.Height == 0)
            {
                continue;
            }

            if (this._placement.FindFreeSlot(item, preview) is { } slot && this._placement.CanMoveItem(item, slot, preview, 0, InventoryConstants.WarehouseRows))
            {
                item.ItemSlot = slot;
                preview.Add(item);
                fits++;
            }
        }

        if (await this.JavaScript.InvokeAsync<bool>("window.confirm", string.Format(Resources.ShopEditorBulkConfirm, definitions.Length, fits, definitions.Length - fits)).ConfigureAwait(true))
        {
            await this.AddManyAsync(definitions).ConfigureAwait(true);
        }
    }

    private async Task AddManyAsync(IReadOnlyList<ItemDefinition> definitions)
    {
        if (this.Busy)
        {
            return;
        }

        var added = definitions.Count(d => this.TryAdd(d));
        this._error = added < definitions.Count;
        this._message = string.Format(Resources.ShopEditorAdded, added, definitions.Count - added);
        await this.EditedAsync().ConfigureAwait(true);
    }

    private async Task ArrangeAsync()
    {
        if (this.Busy)
        {
            return;
        }

        var packed = new List<Item>();
        var copies = this.Draft.Items.Select(i => ShopItemDraft.Copy(i)).OrderByDescending(i => (i.Definition?.Width ?? 1) * (i.Definition?.Height ?? 1)).ThenByDescending(i => i.Definition?.Height).ToArray();
        foreach (var item in copies)
        {
            if (item.Definition is not { Width: > 0, Height: > 0 } || this._placement.FindFreeSlot(item, packed) is not { } slot)
            {
                this._message = Resources.ShopEditorArrangeFailed;
                this._error = true;
                return;
            }

            item.ItemSlot = slot;
            packed.Add(item);
        }

        foreach (var original in this.Draft.Items)
        {
            original.ItemSlot = copies.Single(c => c.Key == original.Key).ItemSlot;
        }

        this._message = string.Empty;
        this._error = false;
        await this.EditedAsync().ConfigureAwait(true);
    }

    private async Task ClearAsync()
    {
        if (this.Busy || !await this.JavaScript.InvokeAsync<bool>("window.confirm", string.Format(Resources.ShopEditorClearConfirm, this.Draft.Storage.Items.Count)).ConfigureAwait(true))
        {
            return;
        }

        this.Draft.Storage.Items.Clear();
        this._selected = null;
        await this.EditedAsync().ConfigureAwait(true);
    }

    private async Task DeleteAsync()
    {
        if (this.Busy || this._selected is null)
        {
            return;
        }

        this.Draft.Storage.Items.Remove(this._selected);
        this._selected = null;
        await this.EditedAsync().ConfigureAwait(true);
    }

    private async Task SelectAsync(Item? item)
    {
        if (this.Busy)
        {
            return;
        }

        this._selected = item as ShopItemDraft;
        await this.EditedAsync().ConfigureAwait(true);
    }

    private async Task UndoAsync()
    {
        this.Draft.Undo();
        await this.RestoredAsync().ConfigureAwait(true);
    }

    private async Task RedoAsync()
    {
        this.Draft.Redo();
        await this.RestoredAsync().ConfigureAwait(true);
    }

    private async Task RestoredAsync()
    {
        this._selected = this.Draft.Items.FirstOrDefault(i => i.Key == this._selected?.Key);
        this.EndDrag();
        this._message = string.Empty;
        this._error = false;
        await this.NotifyAsync().ConfigureAwait(true);
    }

    private async Task EditedAsync()
    {
        this.Draft.Record();
        await this.NotifyAsync().ConfigureAwait(true);
    }

    private Task NotifyAsync() => this.Changed.InvokeAsync();
    private void DragDefinition(ItemDefinition definition)
    {
        if (this.Busy)
        {
            return;
        }

        this._dragged = NewItem(definition);
        this._catalogueDrag = true;
        this._columnOffset = 0;
        this._rowOffset = 0;
    }

    private void DragItem((Item Item, byte ColumnOffset, byte RowOffset) drag)
    {
        if (this.Busy)
        {
            return;
        }

        this._dragged = drag.Item;
        this._catalogueDrag = false;
        this._columnOffset = drag.ColumnOffset;
        this._rowOffset = drag.RowOffset;
    }

    private void EndDrag()
    {
        this._dragged = null;
        this._catalogueDrag = false;
    }

    private async Task DropAsync((byte Slot, int StartIndex, int Rows, bool IsEquipmentSlot) target)
    {
        if (this.Busy || this._dragged is not { } dragged)
        {
            return;
        }

        var slot = target.Slot - (this._rowOffset * InventoryConstants.RowSize) - this._columnOffset;
        var placed = slot >= 0 && slot < InventoryConstants.WarehouseSize && (this._catalogueDrag
            ? dragged.Definition is { } definition && this.TryAdd(definition, (byte)slot)
            : this._placement.TryMoveItem(dragged, (byte)slot, this.Draft.Storage.Items, 0, InventoryConstants.WarehouseRows));
        this._error = !placed;
        this._message = placed ? string.Empty : Resources.ShopEditorDoesNotFit;
        this.EndDrag();
        await this.EditedAsync().ConfigureAwait(true);
    }
}
