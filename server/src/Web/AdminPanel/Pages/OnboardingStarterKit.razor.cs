// <copyright file="OnboardingStarterKit.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// The page "Onboarding and starter kit": the steps of the new-player guide with their rewards, the final reward and the
/// starter kit of the <see cref="OnboardingPlugIn"/>, in one form. It saves the plug-in's configuration like the plug-in
/// editor does, so the running game servers use it at once.
/// </summary>
public partial class OnboardingStarterKit : ComponentBase
{
    private readonly List<StepRow> _rows = new();
    private readonly List<StarterKitItem> _kitItems = new();
    private readonly List<string> _errors = new();
    private readonly List<(int Number, string Name)> _classes = new();
    private OnboardingConfiguration? _configuration;
    private MissionRewardText _finalReward = new();
    private bool _isActive;
    private bool _isLoading = true;
    private bool _isSaving;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<OnboardingStarterKit> Logger { get; set; } = null!;

    /// <summary>
    /// Checks the edited configuration and puts the edited lists into it.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="steps">The steps with their reward item texts, in their order.</param>
    /// <param name="finalItems">The items text of the final reward.</param>
    /// <param name="kitItems">The starter kit's items.</param>
    /// <returns>The errors; none when it's valid.</returns>
    internal static List<string> Apply(OnboardingConfiguration configuration, IReadOnlyList<(OnboardingStep Step, string ItemsText)> steps, string finalItems, IReadOnlyList<StarterKitItem> kitItems)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (step, itemsText) in steps)
        {
            step.Id = step.Id.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(step.Id, "^[A-Za-z0-9_-]{1,40}$"))
            {
                errors.Add(string.Format(Resources.OnboardingErrorId, step.Id));
            }
            else if (!ids.Add(step.Id))
            {
                errors.Add(string.Format(Resources.OnboardingErrorDuplicateId, step.Id));
            }

            if (step.Target < 1)
            {
                errors.Add(string.Format(Resources.OnboardingErrorTarget, step.Id));
            }

            if (MissionConfigurationText.TryParseItems(itemsText, out var items))
            {
                step.Reward.Items = items;
            }
            else
            {
                errors.Add(string.Format(Resources.MissionsErrorItems, step.Id));
            }

            step.Reward.Zen = Math.Max(0, step.Reward.Zen);
            step.Reward.Coins = Math.Max(0, step.Reward.Coins);
            step.MapNumber = (short)Math.Clamp((int)step.MapNumber, -1, 255);
            step.ItemGroup = (short)Math.Clamp((int)step.ItemGroup, -1, 15);
            step.ItemNumber = (short)Math.Clamp((int)step.ItemNumber, -1, 511);
            step.NpcNumber = Math.Max((short)0, step.NpcNumber);
            step.MonsterNumber = Math.Max((short)0, step.MonsterNumber);
            step.SkillNumber = Math.Max((short)0, step.SkillNumber);
            step.Radius = Math.Min(step.Radius, (byte)50);
        }

        configuration.Steps = steps.Select(s => s.Step).ToList();
        if (MissionConfigurationText.TryParseItems(finalItems, out var final))
        {
            configuration.FinalReward.Items = final;
        }
        else
        {
            errors.Add(string.Format(Resources.MissionsErrorItems, Resources.OnboardingFinalReward));
        }

        configuration.FinalReward.Zen = Math.Max(0, configuration.FinalReward.Zen);
        configuration.FinalReward.Coins = Math.Max(0, configuration.FinalReward.Coins);

        var number = 0;
        foreach (var item in kitItems)
        {
            number++;
            if (item.Group is < 0 or > 15 || item.Number is < 0 or > 511 || item.Level > 15 || item.Count is < 1 or > 50)
            {
                errors.Add(string.Format(Resources.OnboardingErrorKitItem, number));
            }

            item.OptionLevel = Math.Min(item.OptionLevel, (byte)7);
            item.ExcellentOptions = Math.Min(item.ExcellentOptions, (byte)63);
        }

        var kit = configuration.StarterKit;
        kit.Items = kitItems.ToList();
        kit.Zen = Math.Max(0, kit.Zen);
        kit.Coins = Math.Max(0, kit.Coins);
        kit.LevelUpPoints = Math.Clamp(kit.LevelUpPoints, 0, 10_000);
        kit.ExperienceBuff.Minutes = Math.Clamp(kit.ExperienceBuff.Minutes, 1, 10080);
        configuration.NewCharacterMaxLevel = Math.Clamp(configuration.NewCharacterMaxLevel, 0, 1000);
        return errors;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await this.LoadAsync().ConfigureAwait(true);
        await base.OnInitializedAsync().ConfigureAwait(true);
    }

    private static string KindCaption(OnboardingStepKind kind) => kind switch
    {
        OnboardingStepKind.TalkToNpc => Resources.OnboardingKindTalkToNpc,
        OnboardingStepKind.EnterMap => Resources.OnboardingKindEnterMap,
        OnboardingStepKind.WalkTo => Resources.OnboardingKindWalkTo,
        OnboardingStepKind.KillMonsters => Resources.OnboardingKindKillMonsters,
        OnboardingStepKind.UseItem => Resources.OnboardingKindUseItem,
        OnboardingStepKind.UseSkill => Resources.OnboardingKindUseSkill,
        OnboardingStepKind.LearnSkill => Resources.OnboardingKindLearnSkill,
        OnboardingStepKind.ReachLevel => Resources.OnboardingKindReachLevel,
        OnboardingStepKind.JoinParty => Resources.OnboardingKindJoinParty,
        OnboardingStepKind.AddStatPoints => Resources.OnboardingKindAddStatPoints,
        OnboardingStepKind.OpenCashShop => Resources.OnboardingKindOpenCashShop,
        _ => Resources.OnboardingKindOpenMissions,
    };

    private static bool HasCount(OnboardingStepKind kind) =>
        kind is OnboardingStepKind.KillMonsters or OnboardingStepKind.UseItem or OnboardingStepKind.UseSkill or OnboardingStepKind.LearnSkill
            or OnboardingStepKind.ReachLevel or OnboardingStepKind.AddStatPoints or OnboardingStepKind.TalkToNpc;

    private static bool HasMap(OnboardingStepKind kind) => kind is OnboardingStepKind.EnterMap or OnboardingStepKind.WalkTo or OnboardingStepKind.KillMonsters;

    private async Task LoadAsync()
    {
        this._isLoading = true;
        this._errors.Clear();
        try
        {
            // Straight from the database: the panel's cached game configuration can miss the row the game
            // server added when it first started.
            var (exists, isActive, saved) = await PlugInSettings.ReadStoredAsync<OnboardingConfiguration>(this.PersistenceContextProvider, typeof(OnboardingPlugIn), default).ConfigureAwait(true);
            if (!exists)
            {
                this._configuration = null;
                return;
            }

            this._isActive = isActive;
            var configuration = saved ?? new OnboardingConfiguration();

            // A copy, so that an unsaved edit doesn't change a cached configuration.
            configuration = JsonSerializer.Deserialize<OnboardingConfiguration>(JsonSerializer.Serialize(configuration)) ?? new OnboardingConfiguration();
            this._configuration = configuration;
            this._rows.Clear();
            this._rows.AddRange(configuration.Steps.Select(s => new StepRow(s)));
            this._kitItems.Clear();
            this._kitItems.AddRange(configuration.StarterKit.Items);
            this._finalReward = new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(configuration.FinalReward.Items) };
            await this.LoadClassesAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading the onboarding configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task LoadClassesAsync()
    {
        this._classes.Clear();
        try
        {
            var gameConfiguration = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, default).ConfigureAwait(true);
            this._classes.AddRange(gameConfiguration.CharacterClasses
                .Where(c => c.CanGetCreated)
                .OrderBy(c => c.Number)
                .Select(c => ((int)c.Number, c.Name.ValueInNeutralLanguage ?? c.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
        catch (Exception ex)
        {
            this.Logger.LogDebug(ex, "The character classes weren't read.");
        }

        // Classes the items name but the game data doesn't list (or wasn't read) stay selectable.
        foreach (var number in this._kitItems.Select(i => (int)i.ClassNumber).Where(n => n >= 0).Distinct())
        {
            if (this._classes.All(c => c.Number != number))
            {
                this._classes.Add((number, $"#{number}"));
            }
        }
    }

    private void AddStep()
    {
        var number = this._rows.Count + 1;
        while (this._rows.Any(r => r.Step.Id == $"step-{number}"))
        {
            number++;
        }

        this._rows.Add(new StepRow(new OnboardingStep { Id = $"step-{number}", Kind = OnboardingStepKind.KillMonsters, Target = 10, TitleEn = "New step", TitleVi = "Bước mới" }));
    }

    private void RemoveStep(StepRow row) => this._rows.Remove(row);

    private void MoveStep(StepRow row, int by)
    {
        var index = this._rows.IndexOf(row);
        var target = index + by;
        if (index < 0 || target < 0 || target >= this._rows.Count)
        {
            return;
        }

        this._rows.RemoveAt(index);
        this._rows.Insert(target, row);
    }

    private void AddKitItem() => this._kitItems.Add(new StarterKitItem { Group = 14, Number = 1, Count = 1 });

    private void RemoveKitItem(StarterKitItem item) => this._kitItems.Remove(item);

    private async Task SaveAsync()
    {
        if (this._configuration is not { } configuration)
        {
            return;
        }

        this._errors.Clear();
        this._errors.AddRange(Apply(configuration, this._rows.Select(r => (r.Step, r.Reward.ItemsText)).ToList(), this._finalReward.ItemsText, this._kitItems));
        if (this._errors.Count > 0)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(OnboardingPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            var (_, active, _) = await PlugInSettings.ReadStoredAsync<OnboardingConfiguration>(this.PersistenceContextProvider, typeof(OnboardingPlugIn), default).ConfigureAwait(true);
            if (active != this._isActive)
            {
                var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
                if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
                {
                    await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
                }
            }

            this.Logger.LogInformation("Saved the onboarding configuration: {Count} steps, {Items} kit items, active: {Active}.", configuration.Steps.Count, configuration.StarterKit.Items.Count, this._isActive);
            this.ToastService.ShowSuccess(Resources.OnboardingSaved);
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (AdminOperationException ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving the onboarding configuration.");
            this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
        }
        finally
        {
            this._isSaving = false;
        }
    }

    /// <summary>
    /// A row of the steps table.
    /// </summary>
    private sealed class StepRow
    {
        public StepRow(OnboardingStep step)
        {
            this.Step = step;
            this.Reward = new MissionRewardText { ItemsText = MissionConfigurationText.FormatItems(step.Reward.Items) };
        }

        public OnboardingStep Step { get; }

        public MissionRewardText Reward { get; }
    }
}
