// <copyright file="ItemPicker.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Components.ItemPicker;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Web.Shared.Properties;
using Microsoft.AspNetCore.Components;

/// <summary>A virtualized, reusable item catalogue which owns selection but never creates persisted items.</summary>
public partial class ItemPicker
{
    private readonly string _id = "item-picker-" + Guid.NewGuid().ToString("N");
    private readonly HashSet<ItemDefinition> _selected = new();
    private List<ItemDefinition> _matches = new();

    /// <summary>Gets or sets a value indicating whether shop stock and grid dragging controls are available; disable for gift selection.</summary>
    [Parameter]
    public bool ShopMode { get; set; } = true;

    /// <summary>Gets or sets all definitions to search.</summary>
    [Parameter]
    public IReadOnlyList<ItemDefinition> Definitions { get; set; } = [];

    /// <summary>Gets or sets available character classes.</summary>
    [Parameter]
    public IReadOnlyList<CharacterClass> Classes { get; set; } = [];

    /// <summary>Gets or sets the definition keys already stocked.</summary>
    [Parameter]
    public ISet<(byte Group, short Number)> Stocked { get; set; } = new HashSet<(byte, short)>();

    /// <summary>Gets or sets the single-click addition callback.</summary>
    [Parameter]
    public EventCallback<ItemDefinition> Add { get; set; }

    /// <summary>Gets or sets the catalogue drag callback.</summary>
    [Parameter]
    public EventCallback<ItemDefinition> DragStarted { get; set; }

    /// <summary>Gets or sets the drag-end callback.</summary>
    [Parameter]
    public EventCallback DragEnded { get; set; }

    /// <summary>Gets or sets the filter/selection notification.</summary>
    [Parameter]
    public EventCallback Changed { get; set; }

    /// <summary>Gets the active filter, reusable by bulk previews.</summary>
    public ItemCatalogueFilter Filter { get; } = new();

    /// <summary>Gets the matching definitions in display order.</summary>
    public IReadOnlyList<ItemDefinition> Matches => this._matches;

    /// <summary>Gets every selected definition, including selections hidden by a later filter.</summary>
    public IReadOnlyList<ItemDefinition> Selected => this.Definitions.Where(this._selected.Contains).ToArray();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        this._selected.RemoveWhere(d => !this.Definitions.Contains(d));
        if (!this.ShopMode)
        {
            this.Filter.OnlyMissing = false;
        }

        this._matches = this.Filter.Apply(this.Definitions, this.Stocked);
    }

    private static string GroupName(int group) => Resources.ResourceManager.GetString("ItemPickerGroup" + group.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentUICulture) ?? group.ToString(CultureInfo.InvariantCulture);

    private async Task FilterChangedAsync()
    {
        if (!this.ShopMode)
        {
            this.Filter.OnlyMissing = false;
        }

        this._matches = this.Filter.Apply(this.Definitions, this.Stocked);
        await this.Changed.InvokeAsync().ConfigureAwait(true);
    }

    private async Task SelectAsync(ItemDefinition definition, bool selected)
    {
        if (selected)
        {
            this._selected.Add(definition);
        }
        else
        {
            this._selected.Remove(definition);
        }

        await this.Changed.InvokeAsync().ConfigureAwait(true);
    }

    private async Task ClearAsync()
    {
        this._selected.Clear();
        await this.Changed.InvokeAsync().ConfigureAwait(true);
    }
}
