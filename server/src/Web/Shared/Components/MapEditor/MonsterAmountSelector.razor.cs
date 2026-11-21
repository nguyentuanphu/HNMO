// <copyright file="MonsterAmountSelector.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Components.MapEditor;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.Shared.Properties;
using Microsoft.AspNetCore.Components;

/// <summary>
/// Sets the monster amount multiplier (×0.5 to ×5) of a map, in the configuration of the <see cref="MonsterAmountPlugIn"/>.
/// The stored spawn quantities stay as they are; the running game servers apply a change at once.
/// </summary>
public partial class MonsterAmountSelector
{
    private GameMapDefinition? _loadedMap;
    private double _current = 1;
    private double _selected = 1;
    private bool _isSaving;
    private string? _status;
    private bool _statusIsError;

    /// <summary>
    /// Gets or sets the map.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public GameMapDefinition Map { get; set; } = null!;

    /// <summary>
    /// Gets or sets the persistence context of the game configuration, which the map editor uses.
    /// </summary>
    [CascadingParameter]
    public IContext? PersistenceContext { get; set; }

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    private string SelectedText
    {
        get => Format(this._selected);
        set
        {
            this._selected = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier)
                ? MonsterAmountConfiguration.Normalize(multiplier)
                : 1;
            this._status = null;
        }
    }

    /// <summary>
    /// Formats a multiplier like the choices (0.5, 1, 2 ...).
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <returns>The text.</returns>
    internal static string Format(double multiplier) => multiplier.ToString("0.##", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync().ConfigureAwait(true);
        if (ReferenceEquals(this._loadedMap, this.Map))
        {
            return;
        }

        this._loadedMap = this.Map;
        this._status = null;
        var gameConfiguration = await this.GameConfigurationSource.GetOwnerAsync().ConfigureAwait(true);
        this._current = ReadConfiguration(gameConfiguration, out _)?.GetMultiplier(this.Map.Number) ?? 1;
        this._selected = this._current;
    }

    private static MonsterAmountConfiguration? ReadConfiguration(GameConfiguration gameConfiguration, out PlugInConfiguration? plugInConfiguration, bool onlyActive = true)
    {
        plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeof(MonsterAmountPlugIn).GUID);
        if (plugInConfiguration is null || (onlyActive && !plugInConfiguration.IsActive))
        {
            return null;
        }

        try
        {
            return plugInConfiguration.GetConfiguration<MonsterAmountConfiguration>(null);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task SaveAsync()
    {
        var context = this.PersistenceContext ?? await this.GameConfigurationSource.GetContextAsync().ConfigureAwait(true);
        if (context.HasChanges)
        {
            // Saving now would save the unsaved changes of the map editor as well.
            this.SetStatus(Resources.MonsterAmountSaveMapFirst, true);
            return;
        }

        this._isSaving = true;
        try
        {
            var gameConfiguration = await this.GameConfigurationSource.GetOwnerAsync().ConfigureAwait(true);
            var amounts = ReadConfiguration(gameConfiguration, out var plugInConfiguration, onlyActive: false) ?? new MonsterAmountConfiguration();
            if (plugInConfiguration is null)
            {
                plugInConfiguration = context.CreateNew<PlugInConfiguration>();
                plugInConfiguration.TypeId = typeof(MonsterAmountPlugIn).GUID;
                gameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
            }

            amounts.SetMultiplier(this.Map.Number, this._selected);
            plugInConfiguration.IsActive = true;
            plugInConfiguration.SetConfiguration(amounts, null);
            if (await context.SaveChangesAsync().ConfigureAwait(true))
            {
                this._current = this._selected;
                this.SetStatus(Resources.MonsterAmountSaved, false);
            }
            else
            {
                this.SetStatus(Resources.MonsterAmountSaveFailed, true);
            }
        }
        catch (Exception ex)
        {
            this.SetStatus(Resources.MonsterAmountSaveFailed + " " + ex.Message, true);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private void SetStatus(string text, bool isError)
    {
        this._status = text;
        this._statusIsError = isError;
    }
}
