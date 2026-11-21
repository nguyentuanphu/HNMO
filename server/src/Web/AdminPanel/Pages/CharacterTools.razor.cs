// <copyright file="CharacterTools.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Components;

/// <summary>Code-behind for <see cref="CharacterTools"/> (ROADMAP 14.15).</summary>
public partial class CharacterTools
{
    private readonly List<GameMapDefinition> _towns = new();
    private readonly List<CharacterClass> _classes = new();
    private readonly List<MagicEffectDefinition> _buffs = new();

    private string _characterName = string.Empty;
    private string _selectedTownMap = string.Empty;
    private string _moveReason = string.Empty;
    private short _selectedBuffNumber;
    private int _buffDurationSeconds = 1800;
    private string _buffReason = string.Empty;
    private string _selectedClassIdText = string.Empty;
    private CharacterEditRequest _edit = new();
    private CharacterToolSnapshot? _snapshot;
    private bool _showHelp;
    private bool _isVi;
    private bool _busy;
    private string _error = string.Empty;
    private string _status = string.Empty;

    [Inject]
    private CharacterToolsOperations Operations { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this._isVi = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        try
        {
            var (towns, classes, buffs) = await this.Operations.LoadCatalogAsync(CancellationToken.None).ConfigureAwait(true);
            this._towns.Clear();
            this._towns.AddRange(towns);
            this._classes.Clear();
            this._classes.AddRange(classes);
            this._buffs.Clear();
            this._buffs.AddRange(buffs);
            if (this._buffs.Count > 0)
            {
                this._selectedBuffNumber = this._buffs[0].Number;
            }
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
    }

    private string L(string en, string vi) => this._isVi ? vi : en;

    private void SetLanguage(bool vi) => this._isVi = vi;

    private async Task LoadCharacterAsync()
    {
        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            var loaded = await this.Operations.LoadCharacterAsync(this._characterName, CancellationToken.None).ConfigureAwait(true);
            this.BindSnapshot(loaded);
        }
        catch (Exception ex)
        {
            this._snapshot = null;
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task MoveToTownAsync()
    {
        if (this._snapshot is null)
        {
            return;
        }

        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            ushort? mapNum = ushort.TryParse(this._selectedTownMap, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
            var result = await this.Operations.MoveToTownAsync(this._snapshot.CharacterName, mapNum, this._moveReason, CancellationToken.None).ConfigureAwait(true);
            var reloaded = await this.Operations.LoadCharacterAsync(this._snapshot.CharacterName, CancellationToken.None).ConfigureAwait(true);
            this.BindSnapshot(reloaded);
            this._status = this.L(
                $"Moved {reloaded.CharacterName} to {result["mapName"]} ({result["x"]}, {result["y"]}).",
                $"Đã đưa {reloaded.CharacterName} về {result["mapName"]} ({result["x"]}, {result["y"]}).");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task ApplyBuffAsync()
    {
        if (this._snapshot is null)
        {
            return;
        }

        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            var result = await this.Operations.ApplyBuffAsync(this._snapshot.CharacterName, this._selectedBuffNumber, this._buffDurationSeconds, this._buffReason, CancellationToken.None).ConfigureAwait(true);
            this._status = this.L(
                $"Applied buff {result["effectName"]} for {this._buffDurationSeconds}s.",
                $"Đã ban tặng bùa {result["effectName"]} trong {this._buffDurationSeconds} giây.");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task ClearBuffsAsync()
    {
        if (this._snapshot is null)
        {
            return;
        }

        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            await this.Operations.ClearBuffsAsync(this._snapshot.CharacterName, this._buffReason, CancellationToken.None).ConfigureAwait(true);
            this._status = this.L("Cleared active timed buffs.", "Đã xóa các bùa hỗ trợ đang có hiệu lực.");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task SaveOfflineEditsAsync()
    {
        if (this._snapshot is null)
        {
            return;
        }

        this._error = string.Empty;
        this._status = string.Empty;
        this._busy = true;
        try
        {
            this._edit.CharacterClassId = Guid.TryParse(this._selectedClassIdText, out var parsedClass) && parsedClass != Guid.Empty
                ? parsedClass
                : null;
            await this.Operations.EditOfflineCharacterAsync(this._snapshot.CharacterName, this._edit, CancellationToken.None).ConfigureAwait(true);
            var reloaded = await this.Operations.LoadCharacterAsync(this._snapshot.CharacterName, CancellationToken.None).ConfigureAwait(true);
            this.BindSnapshot(reloaded);
            this._status = this.L("Saved offline character changes and recorded audit log.", "Đã lưu chỉnh sửa ngoại tuyến và ghi nhật ký kiểm toán.");
        }
        catch (Exception ex)
        {
            this._error = ex.Message;
        }
        finally
        {
            this._busy = false;
        }
    }

    private void BindSnapshot(CharacterToolSnapshot loaded)
    {
        this._snapshot = loaded;
        this._selectedClassIdText = loaded.CharacterClassId == Guid.Empty ? string.Empty : loaded.CharacterClassId.ToString();
        this._edit = new CharacterEditRequest
        {
            Level = loaded.Level,
            MasterLevel = loaded.MasterLevel,
            Resets = loaded.Resets,
            Strength = loaded.Strength,
            Agility = loaded.Agility,
            Vitality = loaded.Vitality,
            Energy = loaded.Energy,
            Leadership = loaded.Leadership,
            Zen = loaded.Zen,
            CharacterClassId = loaded.CharacterClassId,
            Reason = this._edit.Reason,
        };
    }
}
