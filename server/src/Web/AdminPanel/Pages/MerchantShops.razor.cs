// <copyright file="MerchantShops.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>The default merchant list and safe detached shop editor.</summary>
public partial class MerchantShops : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly PaginationState _pagination = new() { ItemsPerPage = 12 };
    private List<MerchantShopRow> _rows = new();
    private List<ItemDefinition> _definitions = new();
    private List<CharacterClass> _classes = new();
    private IContext? _context;
    private MerchantShopRow? _selected;
    private MerchantShopRow? _copySource;
    private Guid _copyTarget;
    private ShopDraft? _draft;
    private bool _help;
    private bool _loading = true;
    private bool _busy;
    private string _search = string.Empty;
    private string _notice = string.Empty;
    private string _error = string.Empty;

    /// <summary>Gets or sets the existing configuration source.</summary>
    [Inject]
    public IDataSource<GameConfiguration> DataSource { get; set; } = null!;

    /// <summary>Gets or sets the authorized, audited save operation.</summary>
    [Inject]
    public ShopEditorOperations Operations { get; set; } = null!;

    /// <summary>Gets or sets browser confirmations.</summary>
    [Inject]
    public IJSRuntime JavaScript { get; set; } = null!;

    /// <summary>Gets or sets the page logger.</summary>
    [Inject]
    public ILogger<MerchantShops> Logger { get; set; } = null!;

    private IQueryable<MerchantShopRow> FilteredRows => this._rows.Where(r => r.Name.Contains(this._search.Trim(), StringComparison.OrdinalIgnoreCase) || r.Locations.Contains(this._search.Trim(), StringComparison.OrdinalIgnoreCase)).AsQueryable();

    /// <inheritdoc />
    public void Dispose()
    {
        this._stop.Cancel();
        this._stop.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync() => await this.LoadAsync().ConfigureAwait(true);

    private static string SummaryText(ShopChangeSummary summary) => string.Format(Resources.ShopEditorSummary, summary.Added, summary.Removed, summary.Moved, summary.Updated);

    private static MerchantShopRow MakeRow(MonsterDefinition merchant, IEnumerable<MonsterSpawnArea> spawns)
    {
        var locations = string.Join("; ", spawns.Select(s => $"{s.GameMap?.Name} ({s.GameMap?.Number}) · {s.X1},{s.Y1}" + (s.X1 != s.X2 || s.Y1 != s.Y2 ? $"–{s.X2},{s.Y2}" : string.Empty)));
        return new MerchantShopRow(merchant, locations.Length > 0 ? locations : Resources.ShopEditorNoSpawn);
    }

    private Task ResetPageAsync() => this._pagination.SetCurrentPageIndexAsync(0);

    private async Task LoadAsync()
    {
        this._loading = true;
        try
        {
            await this.DataSource.GetOwnerAsync(cancellationToken: this._stop.Token).ConfigureAwait(true);
            this._context = await this.DataSource.GetContextAsync(this._stop.Token).ConfigureAwait(true);
            var spawns = this.DataSource.GetAll<MonsterSpawnArea>().Where(s => s.MonsterDefinition is not null).ToLookup(s => s.MonsterDefinition!.GetId());
            this._rows = this.DataSource.GetAll<MonsterDefinition>().Where(m => m is { ObjectKind: NpcObjectKind.PassiveNpc, MerchantStore: not null })
                .Select(m => MakeRow(m, spawns[m.GetId()]))
                .OrderBy(r => r.Name).ToList();
            this._definitions = this.DataSource.GetAll<ItemDefinition>().ToList();
            this._classes = this.DataSource.GetAll<CharacterClass>().ToList();
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException && this._stop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Could not load the merchant shop editor.");
            this._error = Resources.ShopEditorLoadingFailed;
        }
        finally
        {
            this._loading = false;
        }
    }

    private async Task<bool> MayDiscardAsync() => this._draft?.Summary.HasChanges is not true || await this.JavaScript.InvokeAsync<bool>("window.confirm", Resources.ShopEditorUnsaved).ConfigureAwait(true);

    private async Task OpenAsync(MerchantShopRow row)
    {
        if (this._busy || !await this.MayDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        this._notice = string.Empty;
        this._selected = row;
        this._draft = new ShopDraft(row.Merchant.MerchantStore!);
        this._error = string.Empty;
    }

    private async Task BackAsync()
    {
        if (this._busy || !await this.MayDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        this._draft = null;
        this._selected = null;
    }

    private void BeginCopy(MerchantShopRow source)
    {
        this._copySource = source;
        this._copyTarget = Guid.Empty;
    }

    private async Task CopyAsync()
    {
        var target = this._rows.FirstOrDefault(r => r.Merchant.GetId() == this._copyTarget);
        if (this._copySource is not { } source || target is null || !await this.JavaScript.InvokeAsync<bool>("window.confirm", string.Format(Resources.ShopEditorCopyConfirm, target.Name, source.Count, source.Name)).ConfigureAwait(true))
        {
            return;
        }

        await this.OpenAsync(target).ConfigureAwait(true);
        this._draft!.CopyFrom(source.Merchant.MerchantStore!);
    }

    private async Task DiscardAsync()
    {
        if (this._busy || this._selected is null || !await this.MayDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        this._draft = new ShopDraft(this._selected.Merchant.MerchantStore!);
        this._error = string.Empty;
    }

    private async Task ReloadAsync()
    {
        if (this._busy || this._selected is null || !await this.MayDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        var id = this._selected.Merchant.GetId();
        this._error = string.Empty;
        await this.LoadAsync().ConfigureAwait(true);
        this._selected = this._rows.FirstOrDefault(r => r.Merchant.GetId() == id);
        this._draft = this._selected is { } row ? new ShopDraft(row.Merchant.MerchantStore!) : null;
    }

    private async Task SaveAsync()
    {
        if (this._busy || this._selected is not { } selected || this._draft is not { } draft || !draft.Summary.HasChanges)
        {
            return;
        }

        var candidate = draft.Fork();
        this._busy = true;
        this._error = string.Empty;
        try
        {
            if (!await this.JavaScript.InvokeAsync<bool>("window.confirm", string.Format(Resources.ShopEditorSaveConfirm, SummaryText(candidate.Summary))).ConfigureAwait(true))
            {
                return;
            }

            var summary = await this.Operations.SaveAsync(selected.Merchant.GetId(), candidate, this._stop.Token).ConfigureAwait(true);
            this._notice = string.Format(Resources.ShopEditorSaved, SummaryText(summary));
            await this.LoadAsync().ConfigureAwait(true);
            this._selected = this._rows.FirstOrDefault(r => r.Merchant.GetId() == selected.Merchant.GetId());
            this._draft = this._selected is { } row ? new ShopDraft(row.Merchant.MerchantStore!) : null;
        }
        catch (AdminOperationException ex) when (ex.Kind == AdminOperationErrorKind.Conflict)
        {
            this._error = Resources.ShopEditorConflict;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "The merchant shop save could not be confirmed.");
            this._error = Resources.ShopEditorSaveFailed;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task BeforeNavigateAsync(LocationChangingContext context)
    {
        if (this._busy || !await this.MayDiscardAsync().ConfigureAwait(true))
        {
            context.PreventNavigation();
        }
    }
}
