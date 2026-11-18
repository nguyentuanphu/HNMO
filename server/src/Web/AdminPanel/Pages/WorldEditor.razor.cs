// <copyright file="WorldEditor.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.TerrainEditor;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// Interactive Three.js 3D World Editor in the Admin Panel (ROADMAP 14.24 Part A):
/// inspect maps as players see them, paint terrain attributes, place/edit monster spawns and gates,
/// and persist simultaneously to the server database and web client data directory.
/// </summary>
public partial class WorldEditor : IAsyncDisposable
{
    private const string EditorScriptPath = "./_content/HNMO.Web.AdminPanel/js/world-editor.js";

    private ElementReference _viewportRef;
    private IContext? _context;
    private GameConfiguration? _config;
    private List<GameMapDefinition> _maps = new();
    private List<MonsterDefinition> _monsters = new();
    private List<ExitGate> _allExitGates = new();
    private GameMapDefinition? _selectedMap;

    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _editorHandle;
    private DotNetObjectReference<WorldEditor>? _dotNetRef;
    private CancellationTokenSource? _disposeCts;

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private bool _needsMapPush = true;
    private bool _canUndo;
    private bool _canRedo;
    private bool _showOverlay = true;
    private bool _showSpawns = true;
    private bool _showGates = true;
    private bool _statusError;

    private string _activeTool = "inspect";
    private string _cameraMode = "orbit";
    private string _activeTab = "spawns";
    private string _renderModeLabel = "Three.js";
    private string? _statusMessage;

    private int _brushRadius = 2;
    private int _cursorX;
    private int _cursorY;
    private int _cursorHeight;
    private int _cursorFlags;
    private int _jumpX = 128;
    private int _jumpY = 128;
    private short _defaultMonsterNumber;

    /// <summary>Gets or sets the optionally routed map identifier.</summary>
    [Parameter]
    public Guid SelectedMapId { get; set; }

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private WorldEditorDataService DataService { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private ILogger<WorldEditor> Logger { get; set; } = null!;

    /// <summary>Receives cursor coordinates and tile flags from the Three.js viewport.</summary>
    /// <param name="x">Tile X (0-255).</param>
    /// <param name="y">Tile Y (0-255).</param>
    /// <param name="flags">Terrain attribute mask.</param>
    /// <param name="height">Terrain height value.</param>
    /// <returns>Completed task.</returns>
    [JSInvokable]
    public Task OnCursorMovedAsync(int x, int y, int flags, int height)
    {
        this._cursorX = x;
        this._cursorY = y;
        this._cursorFlags = flags;
        this._cursorHeight = height;
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Updates undo/redo button availability from the JS editor history stack.</summary>
    /// <param name="canUndo">Whether undo is available.</param>
    /// <param name="canRedo">Whether redo is available.</param>
    /// <returns>Completed task.</returns>
    [JSInvokable]
    public Task OnHistoryChangedAsync(bool canUndo, bool canRedo)
    {
        this._canUndo = canUndo;
        this._canRedo = canRedo;
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Handles clicking on the 3D terrain to place a spawn, enter gate, or exit gate.</summary>
    /// <param name="x">Tile X (0-255).</param>
    /// <param name="y">Tile Y (0-255).</param>
    /// <param name="tool">Active placement tool identifier.</param>
    /// <returns>Asynchronous task.</returns>
    [JSInvokable]
    public async Task OnTerrainPlaceRequestedAsync(int x, int y, string tool)
    {
        if (this._context is null || this._selectedMap is null)
        {
            return;
        }

        var bx = (byte)Math.Clamp(x, 0, 255);
        var by = (byte)Math.Clamp(y, 0, 255);
        if (tool == "spawn")
        {
            var monster = this._monsters.FirstOrDefault(m => m.Number == this._defaultMonsterNumber)
                ?? this._monsters.FirstOrDefault();
            if (monster is null)
            {
                return;
            }

            var area = this._context.CreateNew<MonsterSpawnArea>();
            area.GameMap = this._selectedMap;
            area.MonsterDefinition = monster;
            area.X1 = bx;
            area.Y1 = by;
            area.X2 = (byte)Math.Min(255, bx + 4);
            area.Y2 = (byte)Math.Min(255, by + 4);
            area.Quantity = 3;
            area.SpawnTrigger = SpawnTrigger.Automatic;
            this._selectedMap.MonsterSpawns.Add(area);
            this._activeTab = "spawns";
        }
        else if (tool == "enterGate")
        {
            var gate = this._context.CreateNew<EnterGate>();
            gate.X1 = bx;
            gate.Y1 = by;
            gate.X2 = (byte)Math.Min(255, bx + 2);
            gate.Y2 = (byte)Math.Min(255, by + 2);
            gate.Number = (short)(this._selectedMap.EnterGates.Select(g => g.Number).DefaultIfEmpty((short)0).Max() + 1);
            gate.LevelRequirement = 1;
            gate.TargetGate = this._allExitGates.FirstOrDefault();
            this._selectedMap.EnterGates.Add(gate);
            this._activeTab = "enterGates";
        }
        else if (tool == "exitGate")
        {
            var gate = this._context.CreateNew<ExitGate>();
            gate.Map = this._selectedMap;
            gate.X1 = bx;
            gate.Y1 = by;
            gate.X2 = (byte)Math.Min(255, bx + 2);
            gate.Y2 = (byte)Math.Min(255, by + 2);
            gate.Direction = Direction.South;
            this._selectedMap.ExitGates.Add(gate);
            this._allExitGates.Add(gate);
            this._activeTab = "exitGates";
        }

        await this.PushEntitiesToViewportAsync().ConfigureAwait(true);
        this.StateHasChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this._disposeCts is not null)
        {
            await this._disposeCts.CancelAsync().ConfigureAwait(false);
            this._disposeCts.Dispose();
            this._disposeCts = null;
        }

        if (this._editorHandle is not null)
        {
            try
            {
                await this._editorHandle.InvokeVoidAsync("dispose").ConfigureAwait(false);
                await this._editorHandle.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Circuit closed before cleanup.
            }
        }

        if (this._jsModule is not null)
        {
            try
            {
                await this._jsModule.DisposeAsync().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // Ignore disconnect on teardown.
            }
        }

        this._dotNetRef?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._disposeCts = new CancellationTokenSource();
        this._context = await this.GameConfigurationSource.GetContextAsync(this._disposeCts.Token).ConfigureAwait(true);
        this._config = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, this._disposeCts.Token).ConfigureAwait(true);
        this._maps = this._config.Maps.OrderBy(m => m.Number).ToList();
        this._monsters = this._config.Monsters.OrderBy(m => m.Number).ToList();
        this._defaultMonsterNumber = this._monsters.FirstOrDefault()?.Number ?? 0;
        this._allExitGates = this._maps.SelectMany(m => m.ExitGates).ToList();
        this._selectedMap = this._maps.FirstOrDefault(m => m.GetId() == this.SelectedMapId) ?? this._maps.FirstOrDefault();
        this._isLoading = false;
        this._needsMapPush = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (this._selectedMap is null || this._isLoading)
        {
            return;
        }

        if (this._jsModule is null)
        {
            this._jsModule = await this.JavaScript.InvokeAsync<IJSObjectReference>("import", EditorScriptPath).ConfigureAwait(true);
            this._dotNetRef = DotNetObjectReference.Create(this);
            var apiBase = this.NavigationManager.BaseUri.TrimEnd('/') + "/api/world-editor";
            this._editorHandle = await this._jsModule.InvokeAsync<IJSObjectReference>(
                "createWorldEditor",
                this._viewportRef,
                this._dotNetRef,
                new { dataBaseUrl = apiBase }).ConfigureAwait(true);
            this._needsMapPush = true;
        }

        if (this._needsMapPush && this._editorHandle is not null)
        {
            this._needsMapPush = false;
            await this.LoadSelectedMapIntoViewportAsync().ConfigureAwait(true);
        }
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string FormatFlags(int flags)
    {
        if (flags == 0)
        {
            return L("Walkable", "Đi được");
        }

        var parts = new List<string>(4);
        if ((flags & 0x01) != 0)
        {
            parts.Add(L("Safe", "An toàn"));
        }

        if ((flags & 0x04) != 0)
        {
            parts.Add(L("Blocked", "Cấm đi"));
        }

        if ((flags & 0x08) != 0)
        {
            parts.Add(L("NoGround", "Vực"));
        }

        if ((flags & 0x10) != 0)
        {
            parts.Add(L("Water", "Nước"));
        }

        return parts.Count > 0 ? string.Join("+", parts) : $"0x{flags:X2}";
    }

    private static List<object> ProjectSpawns(GameMapDefinition map) =>
        map.MonsterSpawns.Select(s => (object)new
        {
            id = s.GetId().ToString(),
            monsterNumber = s.MonsterDefinition?.Number ?? 0,
            monsterName = s.MonsterDefinition?.Designation ?? string.Empty,
            x1 = (int)s.X1,
            y1 = (int)s.Y1,
            x2 = (int)s.X2,
            y2 = (int)s.Y2,
            quantity = (int)s.Quantity,
            direction = (int)s.Direction,
            spawnTrigger = (int)s.SpawnTrigger,
        }).ToList();

    private static List<object> ProjectEnterGates(GameMapDefinition map) =>
        map.EnterGates.Select(g => (object)new
        {
            id = g.GetId().ToString(),
            number = (int)g.Number,
            levelRequirement = (int)g.LevelRequirement,
            x1 = (int)g.X1,
            y1 = (int)g.Y1,
            x2 = (int)g.X2,
            y2 = (int)g.Y2,
            targetGateId = g.TargetGate?.GetId().ToString(),
            targetGateLabel = g.TargetGate?.ToString() ?? string.Empty,
        }).ToList();

    private static List<object> ProjectExitGates(GameMapDefinition map) =>
        map.ExitGates.Select(g => (object)new
        {
            id = g.GetId().ToString(),
            x1 = (int)g.X1,
            y1 = (int)g.Y1,
            x2 = (int)g.X2,
            y2 = (int)g.Y2,
            direction = (int)g.Direction,
            isSpawnGate = g.IsSpawnGate,
        }).ToList();

    private async Task LoadSelectedMapIntoViewportAsync()
    {
        if (this._selectedMap is null || this._editorHandle is null)
        {
            return;
        }

        var attributes = TerrainFileConverter.GetTileAttributes(this._selectedMap.TerrainData);
        var b64 = Convert.ToBase64String(attributes);
        var spawns = ProjectSpawns(this._selectedMap);
        var enterGates = ProjectEnterGates(this._selectedMap);
        var exitGates = ProjectExitGates(this._selectedMap);

        var info = await this._editorHandle.InvokeAsync<ViewportLoadInfo>(
            "setMap",
            this._selectedMap.Number,
            b64,
            spawns,
            enterGates,
            exitGates).ConfigureAwait(true);

        this._renderModeLabel = info.Mode == "full-3d-world"
            ? $"{L("Full 3D World", "Thế giới 3D đầy đủ")} ({info.ObjectCount})"
            : L("3D Terrain Grid", "Lưới địa hình 3D");
        this.StateHasChanged();
    }

    private async Task PushEntitiesToViewportAsync()
    {
        if (this._selectedMap is null || this._editorHandle is null)
        {
            return;
        }

        await this._editorHandle.InvokeVoidAsync(
            "updateEntities",
            ProjectSpawns(this._selectedMap),
            ProjectEnterGates(this._selectedMap),
            ProjectExitGates(this._selectedMap)).ConfigureAwait(true);
    }

    private async Task OnMapChangedAsync(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id))
        {
            this._selectedMap = this._maps.FirstOrDefault(m => m.GetId() == id) ?? this._selectedMap;
            this._statusMessage = null;
            await this.LoadSelectedMapIntoViewportAsync().ConfigureAwait(true);
        }
    }

    private async Task SetToolAsync(string tool)
    {
        this._activeTool = tool;
        if (this._editorHandle is not null)
        {
            await this._editorHandle.InvokeVoidAsync("setTool", tool, this._brushRadius).ConfigureAwait(true);
        }
    }

    private async Task OnBrushRadiusChangedAsync(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var val))
        {
            this._brushRadius = Math.Clamp(val, 1, 12);
            if (this._editorHandle is not null)
            {
                await this._editorHandle.InvokeVoidAsync("setTool", this._activeTool, this._brushRadius).ConfigureAwait(true);
            }
        }
    }

    private async Task SetCameraModeAsync(string mode)
    {
        this._cameraMode = mode;
        if (this._editorHandle is not null)
        {
            await this._editorHandle.InvokeVoidAsync("setCameraMode", mode, this._jumpX, this._jumpY).ConfigureAwait(true);
        }
    }

    private async Task FocusCoordinatesAsync()
    {
        if (this._editorHandle is not null)
        {
            await this._editorHandle.InvokeVoidAsync("focusTile", this._jumpX, this._jumpY).ConfigureAwait(true);
        }
    }

    private async Task FocusTileAsync(int x, int y)
    {
        this._jumpX = x;
        this._jumpY = y;
        await this.FocusCoordinatesAsync().ConfigureAwait(true);
    }

    private async Task ToggleOverlayAsync(ChangeEventArgs args)
    {
        this._showOverlay = args.Value is bool b ? b : string.Equals(args.Value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        await this.SyncOverlayOptionsAsync().ConfigureAwait(true);
    }

    private async Task ToggleSpawnsAsync(ChangeEventArgs args)
    {
        this._showSpawns = args.Value is bool b ? b : string.Equals(args.Value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        await this.SyncOverlayOptionsAsync().ConfigureAwait(true);
    }

    private async Task ToggleGatesAsync(ChangeEventArgs args)
    {
        this._showGates = args.Value is bool b ? b : string.Equals(args.Value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        await this.SyncOverlayOptionsAsync().ConfigureAwait(true);
    }

    private async Task SyncOverlayOptionsAsync()
    {
        if (this._editorHandle is not null)
        {
            await this._editorHandle.InvokeVoidAsync("setOverlayOptions", this._showOverlay, 0.52, this._showSpawns, this._showGates).ConfigureAwait(true);
        }
    }

    private async Task UndoAsync()
    {
        if (this._editorHandle is not null)
        {
            await this._editorHandle.InvokeAsync<bool>("undo").ConfigureAwait(true);
        }
    }

    private async Task RedoAsync()
    {
        if (this._editorHandle is not null)
        {
            await this._editorHandle.InvokeAsync<bool>("redo").ConfigureAwait(true);
        }
    }

    private async Task RemoveSpawnAsync(MonsterSpawnArea spawn)
    {
        if (this._selectedMap is null)
        {
            return;
        }

        this._selectedMap.MonsterSpawns.Remove(spawn);
        await this.PushEntitiesToViewportAsync().ConfigureAwait(true);
    }

    private async Task UpdateSpawnCoordAsync(MonsterSpawnArea spawn, int field, ChangeEventArgs args)
    {
        if (!byte.TryParse(args.Value?.ToString(), out var v))
        {
            return;
        }

        switch (field)
        {
            case 1:
                spawn.X1 = v;
                break;
            case 2:
                spawn.Y1 = v;
                break;
            case 3:
                spawn.X2 = v;
                break;
            case 4:
                spawn.Y2 = v;
                break;
        }

        await this.PushEntitiesToViewportAsync().ConfigureAwait(true);
    }

    private void UpdateEnterGateTarget(EnterGate gate, ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var id))
        {
            gate.TargetGate = this._allExitGates.FirstOrDefault(g => g.GetId() == id);
        }
    }

    private async Task RemoveEnterGateAsync(EnterGate gate)
    {
        this._selectedMap?.EnterGates.Remove(gate);
        await this.PushEntitiesToViewportAsync().ConfigureAwait(true);
    }

    private async Task RemoveExitGateAsync(ExitGate gate)
    {
        this._selectedMap?.ExitGates.Remove(gate);
        this._allExitGates.Remove(gate);
        await this.PushEntitiesToViewportAsync().ConfigureAwait(true);
    }

    private async Task SaveAllAsync()
    {
        if (this._isSaving || this._selectedMap is null || this._context is null || this._editorHandle is null)
        {
            return;
        }

        this._isSaving = true;
        this._statusMessage = null;
        this._statusError = false;
        try
        {
            var b64 = await this._editorHandle.InvokeAsync<string>("getTileFlagsBase64").ConfigureAwait(true);
            var attributes = Convert.FromBase64String(b64);
            this._selectedMap.TerrainData = TerrainFileConverter.ToServerFile(attributes);
            await this._context.SaveChangesAsync().ConfigureAwait(true);
            var written = this.DataService.WriteClientAttributes(this._selectedMap.Number, attributes);
            var targetSummary = written.Count > 0 ? string.Join(", ", written) : L("database only (data directory not present)", "chỉ cơ sở dữ liệu (chưa có thư mục Data)");
            this._statusMessage = L(
                $"Saved terrain, spawns, and gates for {this._selectedMap.Name} ({targetSummary}).",
                $"Đã lưu địa hình, bãi quái và cổng cho {this._selectedMap.Name} ({targetSummary}).");
            this.ToastService.ShowSuccess(this._statusMessage);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to save map {MapNumber} in WorldEditor", this._selectedMap.Number);
            this._statusError = true;
            this._statusMessage = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
            this.StateHasChanged();
        }
    }

    private sealed class ViewportLoadInfo
    {
        public string Mode { get; set; } = string.Empty;

        public int ObjectCount { get; set; }
    }
}
