// <copyright file="ShopDraft.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using HNMO.DataModel;
using HNMO.DataModel.Entities;

/// <summary>Owns isolated shop state, undo/redo and optimistic source comparisons.</summary>
public sealed class ShopDraft
{
    private const int HistoryLimit = 100;
    private readonly List<ShopItemDraft> _baseline;
    private readonly Stack<List<ShopItemDraft>> _undo = new();
    private readonly Stack<List<ShopItemDraft>> _redo = new();
    private List<ShopItemDraft> _checkpoint;

    /// <summary>Initializes a new instance of the <see cref="ShopDraft"/> class.</summary>
    /// <param name="source">The saved shop.</param>
    public ShopDraft(ItemStorage source)
    {
        this._baseline = source.Items.Select(i => ShopItemDraft.Copy(i)).ToList();
        this.Set(this._baseline);
        this._checkpoint = this.Snapshot();
    }

    /// <summary>Gets the draft storage.</summary>
    public ShopDraftStorage Storage { get; } = new();

    /// <summary>Gets the editable entries.</summary>
    public IEnumerable<ShopItemDraft> Items => this.Storage.Items.Cast<ShopItemDraft>();

    /// <summary>Gets a value indicating whether undo is available.</summary>
    public bool CanUndo => this._undo.Count > 0;

    /// <summary>Gets a value indicating whether redo is available.</summary>
    public bool CanRedo => this._redo.Count > 0;

    /// <summary>Gets the current change counts.</summary>
    public ShopChangeSummary Summary
    {
        get
        {
            var current = this.Items.ToDictionary(i => i.Key);
            var original = this._baseline.ToDictionary(i => i.Key);
            return new(
                current.Keys.Except(original.Keys).Count(),
                original.Keys.Except(current.Keys).Count(),
                current.Count(p => original.TryGetValue(p.Key, out var old) && old.ItemSlot != p.Value.ItemSlot),
                current.Count(p => original.TryGetValue(p.Key, out var old) && old.Signature(false) != p.Value.Signature(false)));
        }
    }

    /// <summary>Records an atomic edit after a control or bulk action changes the detached items.</summary>
    public void Record()
    {
        var next = this.Snapshot();
        if (Same(this._checkpoint, next))
        {
            return;
        }

        if (this._undo.Count >= HistoryLimit)
        {
            var retained = this._undo.Take(HistoryLimit - 1).Reverse().ToArray();
            this._undo.Clear();
            foreach (var state in retained)
            {
                this._undo.Push(state);
            }
        }

        this._undo.Push(this._checkpoint);
        this._redo.Clear();
        this._checkpoint = next;
    }

    /// <summary>Restores the preceding edit.</summary>
    public void Undo() => this.Restore(this._undo, this._redo);

    /// <summary>Restores an undone edit.</summary>
    public void Redo() => this.Restore(this._redo, this._undo);

    /// <summary>Discards every unsaved edit.</summary>
    public void Discard()
    {
        this.Set(this._baseline);
        this._undo.Clear();
        this._redo.Clear();
        this._checkpoint = this.Snapshot();
    }

    /// <summary>Replaces the draft by independent entries from another merchant.</summary>
    /// <param name="source">The source shop.</param>
    public void CopyFrom(ItemStorage source)
    {
        this.Storage.Items.Clear();
        foreach (var item in source.Items)
        {
            this.Storage.Items.Add(ShopItemDraft.Copy(item, true));
        }

        this.Record();
    }

    /// <summary>Freezes a save candidate so subsequent graphical events cannot alter a confirmed request.</summary>
    /// <returns>An independent draft with the same baseline.</returns>
    public ShopDraft Fork()
    {
        var baseline = new ShopDraftStorage();
        foreach (var item in this._baseline)
        {
            baseline.Items.Add(ShopItemDraft.Copy(item));
        }

        var fork = new ShopDraft(baseline);
        fork.Set(this.Items);
        fork.Record();
        return fork;
    }

    /// <summary>Checks that the saved source still equals the baseline.</summary>
    /// <param name="source">The current shop.</param>
    /// <returns>Whether it is unchanged.</returns>
    public bool MatchesSource(ItemStorage source) => Same(this._baseline, source.Items.Select(i => ShopItemDraft.Copy(i)).ToList());

    /// <summary>Checks rectangles, bounds and the supported item property ranges before persistence.</summary>
    /// <returns>Whether the draft can be safely stored.</returns>
    public bool IsValid()
    {
        var cells = new HashSet<int>();
        foreach (var item in this.Items)
        {
            if (item.Definition is not { Width: > 0, Height: > 0 } definition || item.Level > definition.MaximumItemLevel
                || !double.IsFinite(item.Durability) || item.Durability < 0 || item.SocketCount < 0 || item.SocketCount > definition.MaximumSockets
                || item.ItemOptions.Any(o => o.ItemOption is null || o.Level < 0))
            {
                return false;
            }

            var column = item.ItemSlot % InventoryConstants.RowSize;
            var row = item.ItemSlot / InventoryConstants.RowSize;
            if (column + definition.Width > InventoryConstants.RowSize || row + definition.Height > InventoryConstants.WarehouseRows)
            {
                return false;
            }

            for (var y = 0; y < definition.Height; y++)
            {
                for (var x = 0; x < definition.Width; x++)
                {
                    if (!cells.Add(item.ItemSlot + (y * InventoryConstants.RowSize) + x))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool Same(IReadOnlyList<ShopItemDraft> left, IReadOnlyList<ShopItemDraft> right) =>
        left.Count == right.Count && left.OrderBy(i => i.Key).Zip(right.OrderBy(i => i.Key)).All(p => p.First.Key == p.Second.Key && p.First.Signature() == p.Second.Signature());

    private List<ShopItemDraft> Snapshot() => this.Items.Select(i => ShopItemDraft.Copy(i)).ToList();

    private void Set(IEnumerable<ShopItemDraft> items)
    {
        this.Storage.Items.Clear();
        foreach (var item in items)
        {
            this.Storage.Items.Add(ShopItemDraft.Copy(item));
        }
    }

    private void Restore(Stack<List<ShopItemDraft>> from, Stack<List<ShopItemDraft>> to)
    {
        if (!from.TryPop(out var state))
        {
            return;
        }

        to.Push(this.Snapshot());
        this.Set(state);
        this._checkpoint = this.Snapshot();
    }
}
