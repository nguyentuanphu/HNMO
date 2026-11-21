// <copyright file="Worlds.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.Persistence.Worlds;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Admin panel page for multi-world database management, templates, Sydney opening locks,
/// and one-off character transfers (ROADMAP 15.7).
/// </summary>
public partial class Worlds
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();

    private readonly WorldTemplateCreateRequest _templateRequest = new()
    {
        SourceWorldId = "main",
        Mode = WorldTemplateMode.Fresh,
    };

    private WorldSeason _templateSeason = WorldSeason.Season6;
    private IReadOnlyList<WorldDefinition> _worlds = [];
    private IReadOnlyList<WorldTransferRecord> _transfers = [];
    private WorldDefinition? _editingWorld;
    private string _editingOpeningSydney = string.Empty;
    private string _editingServerIds = string.Empty;
    private string _templateServerIds = string.Empty;
    private string _transferSourceWorldId = "main";
    private string _transferTargetWorldId = string.Empty;
    private string _transferAccount = string.Empty;
    private string _transferCharacter = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _statusIsError;
    private bool _showHelp;
    private bool _isVi;

    /// <summary>Gets or sets the world operations service.</summary>
    [Inject]
    public WorldsOperations Operations { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this._isVi = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        this.LoadData();
    }

    private static string FormatSydney(DateTimeOffset utc)
    {
        var local = TimeZoneInfo.ConvertTime(utc, SydneyZone);
        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " Sydney";
    }

    private static List<int> ParseServerIds(string text)
    {
        var ids = new List<int>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return ids;
        }

        foreach (var part in text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                ids.Add(id);
            }
        }

        return ids.Distinct().OrderBy(id => id).ToList();
    }

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private string T(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private void LoadData()
    {
        this._worlds = this.Operations.GetWorlds();
        this._transfers = this.Operations.GetTransfers();
        if (this._worlds.Count > 0 && string.IsNullOrWhiteSpace(this._templateRequest.SourceWorldId))
        {
            this._templateRequest.SourceWorldId = this._worlds[0].WorldId;
        }

        if (this._worlds.Count > 1 && string.IsNullOrWhiteSpace(this._transferTargetWorldId))
        {
            this._transferTargetWorldId = this._worlds[1].WorldId;
        }
    }

    private void BeginEditWorld(WorldDefinition world)
    {
        this._editingWorld = new WorldDefinition
        {
            WorldId = world.WorldId,
            NameEn = world.NameEn,
            NameVi = world.NameVi,
            Season = world.Season,
            DatabaseName = world.DatabaseName,
            DatabaseConnectionString = world.DatabaseConnectionString,
            ServerIds = [.. world.ServerIds],
            ExperienceMultiplier = world.ExperienceMultiplier,
            MasterExperienceMultiplier = world.MasterExperienceMultiplier,
            DropMultiplier = world.DropMultiplier,
            ZenMultiplier = world.ZenMultiplier,
            EventPresetEn = world.EventPresetEn,
            EventPresetVi = world.EventPresetVi,
            TemplateSourceWorldId = world.TemplateSourceWorldId,
            TemplateMode = world.TemplateMode,
            OpeningDateUtc = world.OpeningDateUtc,
            TransferEnabled = world.TransferEnabled,
            IsRunning = world.IsRunning,
            IsDefault = world.IsDefault,
        };
        this._editingOpeningSydney = world.OpeningDateUtc is { } utc
            ? TimeZoneInfo.ConvertTime(utc, SydneyZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
        this._editingServerIds = string.Join(",", world.ServerIds);
    }

    private async Task SaveEditedWorldAsync()
    {
        if (this._editingWorld is null)
        {
            return;
        }

        try
        {
            this._editingWorld.OpeningDateUtc = WorldDefinition.ParseSydneyToUtc(this._editingOpeningSydney);
            this._editingWorld.ServerIds = ParseServerIds(this._editingServerIds);
            await this.Operations.SaveWorldAsync(this._editingWorld).ConfigureAwait(false);
            this._editingWorld = null;
            this._statusIsError = false;
            this._statusMessage = this.T("Saved world settings.", "Đã lưu cấu hình thế giới.");
            this.LoadData();
        }
        catch (Exception ex)
        {
            this._statusIsError = true;
            this._statusMessage = ex.Message;
        }
    }

    private async Task ToggleWorldAsync(string worldId, bool start)
    {
        try
        {
            await this.Operations.SetWorldRunningAsync(worldId, start).ConfigureAwait(false);
            this._statusIsError = false;
            this._statusMessage = start
                ? this.T($"Started world '{worldId}'.", $"Đã khởi động thế giới '{worldId}'.")
                : this.T($"Stopped world '{worldId}'.", $"Đã dừng thế giới '{worldId}'.");
            this.LoadData();
        }
        catch (Exception ex)
        {
            this._statusIsError = true;
            this._statusMessage = ex.Message;
        }
    }

    private async Task CreateTemplateWorldAsync()
    {
        try
        {
            this._templateRequest.ServerIds = ParseServerIds(this._templateServerIds);
            this._templateRequest.Season = this._templateSeason;
            await this.Operations.CreateFromTemplateAsync(this._templateRequest).ConfigureAwait(false);
            this._statusIsError = false;
            this._statusMessage = this.T(
                $"Created world '{this._templateRequest.NewWorldId}' from '{this._templateRequest.SourceWorldId}'.",
                $"Đã tạo thế giới '{this._templateRequest.NewWorldId}' từ mẫu '{this._templateRequest.SourceWorldId}'.");
            this._templateRequest.NewWorldId = string.Empty;
            this._templateRequest.NameEn = string.Empty;
            this._templateRequest.NameVi = string.Empty;
            this._templateRequest.DatabaseName = string.Empty;
            this._templateServerIds = string.Empty;
            this.LoadData();
        }
        catch (Exception ex)
        {
            this._statusIsError = true;
            this._statusMessage = ex.Message;
        }
    }

    private async Task TransferCharacterOnceAsync()
    {
        try
        {
            await this.Operations.TransferCharacterOnceAsync(
                this._transferSourceWorldId,
                this._transferTargetWorldId,
                this._transferAccount,
                this._transferCharacter).ConfigureAwait(false);
            this._statusIsError = false;
            this._statusMessage = this.T(
                $"Transferred character '{this._transferCharacter}' once from '{this._transferSourceWorldId}' to '{this._transferTargetWorldId}'.",
                $"Đã chuyển nhân vật '{this._transferCharacter}' (1 lần) từ '{this._transferSourceWorldId}' sang '{this._transferTargetWorldId}'.");
            this._transferCharacter = string.Empty;
            this.LoadData();
        }
        catch (Exception ex)
        {
            this._statusIsError = true;
            this._statusMessage = ex.Message;
        }
    }
}
