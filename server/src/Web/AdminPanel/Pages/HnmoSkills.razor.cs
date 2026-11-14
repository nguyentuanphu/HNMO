// <copyright file="HnmoSkills.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.Persistence.AdminAuth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page which shows, grants and removes the HNMO skills (700 to 727) of the characters of an account.
/// </summary>
public partial class HnmoSkills : ComponentBase
{
    private IReadOnlyList<HnmoCharacterSkills>? _characters;
    private bool _isLoading = true;
    private bool _isChanging;
    private (bool Success, string Text)? _lastResult;

    /// <summary>
    /// Gets or sets the login name of the account.
    /// </summary>
    [Parameter]
    public string LoginName { get; set; } = string.Empty;

    [Inject]
    private HnmoSkillService SkillService { get; set; } = null!;

    [Inject]
    private AdminOperationExecutor Executor { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<HnmoSkills> Logger { get; set; } = null!;

    /// <summary>
    /// Creates the text which tells the administrator what changed.
    /// </summary>
    /// <param name="result">The result of the change.</param>
    /// <param name="characterName">The name of the character.</param>
    /// <param name="skillName">The name of the skill.</param>
    /// <returns>Whether it changed something, and the text.</returns>
    internal static (bool Success, string Text) Describe(HnmoSkillChangeResult result, string characterName, string skillName)
    {
        var reached = result.Online ? Resources.HnmoSkillsReachedGame : Resources.HnmoSkillsReachedStored;
        return result.Status switch
        {
            HnmoSkillChangeStatus.Learned => (true, $"{string.Format(Resources.HnmoSkillsGranted, characterName, skillName)} {reached}"),
            HnmoSkillChangeStatus.Removed => (true, $"{string.Format(Resources.HnmoSkillsRemoved, characterName, skillName)} {reached}"),
            HnmoSkillChangeStatus.Unchanged => (true, string.Format(Resources.HnmoSkillsUnchanged, characterName, skillName)),
            HnmoSkillChangeStatus.CharacterNotFound => (false, Resources.HnmoSkillsCharacterNotFound),
            HnmoSkillChangeStatus.SkillNotOfClass => (false, Resources.HnmoSkillsSkillNotOfClass),
            HnmoSkillChangeStatus.AccountBusy => (false, Resources.HnmoSkillsAccountBusy),
            HnmoSkillChangeStatus.NotInGame => (false, string.Format(Resources.HnmoSkillsNotInGame, result.Detail)),
            _ => (false, Resources.HnmoSkillsServerUnavailable),
        };
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnParametersSetAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            this._characters = await this.SkillService.GetCharactersAsync(this.LoginName).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the HNMO skills of account {LoginName}.", this.LoginName);
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task OnChangeAsync(string characterName, HnmoSkillState skill, bool learn)
    {
        this._isChanging = true;
        try
        {
            var result = await this.Executor.ExecuteAsync(
                AdminRoles.Operator,
                "hnmo_skills_set",
                new { this.LoginName, characterName, skill.Number, learn },
                _ => this.SkillService.SetSkillAsync(characterName, skill.Number, learn),
                default,
                value => value.Status is HnmoSkillChangeStatus.Learned or HnmoSkillChangeStatus.Removed or HnmoSkillChangeStatus.Unchanged).ConfigureAwait(true);
            this._lastResult = Describe(result, characterName, $"{skill.Name} ({skill.Number})");
            if (this._lastResult.Value.Success)
            {
                this.Logger.LogInformation("Admin {Change} the HNMO skill {Skill} of {Character}: {Status}, online: {Online}.", learn ? "granted" : "removed", skill.Number, characterName, result.Status, result.Online);
                this.ToastService.ShowSuccess(this._lastResult.Value.Text);
            }
            else
            {
                this.ToastService.ShowWarning(this._lastResult.Value.Text);
            }

            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error changing the HNMO skill {Skill} of {Character}.", skill.Number, characterName);
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isChanging = false;
        }
    }
}
