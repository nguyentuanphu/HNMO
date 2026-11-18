// <copyright file="TopUpMilestonesPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// HNMO top-up milestones ("thưởng mốc nạp"): rewards accounts once when their cumulative confirmed
/// VND top-up passes configured steps. OFF by default until activated in the admin panel.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Top-up milestones", Description = "Rewards accounts once per milestone step when cumulative VND top-ups pass thresholds (OFF by default).")]
[Guid("8F32B710-4D6E-49B2-A517-3C8F90D14E55")]
public class TopUpMilestonesPlugIn :
    IPlayerStateChangedPlugIn,
    ISupportCustomConfiguration<TopUpMilestonesConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    private static readonly ConcurrentDictionary<byte, IGameContext> Contexts = new();

    private (TopUpMilestonesConfiguration Configuration, TopUpMilestoneEngine Engine)? _engine;

    /// <inheritdoc />
    public TopUpMilestonesConfiguration? Configuration { get; set; }

    /// <summary>Gets the active plug-in for a game context, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static TopUpMilestonesPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerStateChangedPlugIn>().OfType<TopUpMilestonesPlugIn>().FirstOrDefault();

    /// <summary>Gets the active game contexts running this plug-in.</summary>
    /// <returns>The active game contexts.</returns>
    public static IReadOnlyCollection<IGameContext> KnownContexts() => Contexts.Values.Distinct().ToList();

    /// <summary>Lookup helper for neutral item names.</summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>The name resolver function.</returns>
    public static Func<short, short, string?> ItemNames(GameConfiguration configuration) =>
        (group, number) => configuration.Items.FirstOrDefault(i => i.Group == group && i.Number == number)?.Name.ValueInNeutralLanguage;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new TopUpMilestonesConfiguration();

    /// <summary>Gets or creates the engine for the current configuration.</summary>
    /// <returns>The engine.</returns>
    public TopUpMilestoneEngine GetEngine()
    {
        var config = this.Configuration ??= new TopUpMilestonesConfiguration();
        if (this._engine is { } cached && ReferenceEquals(cached.Configuration, config))
        {
            return cached.Engine;
        }

        var engine = new TopUpMilestoneEngine(config, TopUpMilestoneStore.Open(config.FileName), enabled: true);
        this._engine = (config, engine);
        return engine;
    }

    /// <summary>Sends the milestone state to an online player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="requested">Whether the client explicitly requested it.</param>
    /// <param name="actionResult">Optional outcome code.</param>
    /// <param name="messageEn">Optional English notice.</param>
    /// <param name="messageVi">Optional Vietnamese notice.</param>
    /// <returns>A task representing the send.</returns>
    public async ValueTask SendStateAsync(
        Player player,
        bool requested,
        string? actionResult = null,
        string? messageEn = null,
        string? messageVi = null)
    {
        if (AccountLoginOf(player) is not { } login)
        {
            return;
        }

        var json = TopUpMilestoneStateWriter.WriteAccountState(
            this.GetEngine(),
            login,
            ItemNames(player.GameContext.Configuration),
            actionResult,
            messageEn,
            messageVi);
        await player.InvokeViewPlugInAsync<IHnmoTopUpMilestonesViewPlugIn>(v => v.ShowTopUpMilestonesAsync(json, requested)).ConfigureAwait(false);
    }

    /// <summary>Claims a milestone step from the in-game window.</summary>
    /// <param name="player">The player.</param>
    /// <param name="stepId">The milestone step id.</param>
    /// <returns>The claim outcome.</returns>
    public async ValueTask<TopUpClaimOutcome?> ClaimAsync(Player player, string stepId)
    {
        if (AccountLoginOf(player) is not { } login)
        {
            return null;
        }

        var engine = this.GetEngine();
        var outcome = engine.Claim(login, stepId, DateTime.UtcNow);
        if (outcome.Result == TopUpClaimResult.Ok)
        {
            player.Logger.LogInformation("Top-up milestones: {Player} claimed step {StepId}.", player, outcome.Step?.Id);
            if (outcome.CoinGrant is { } grant)
            {
                await MissionCoinGranter.GrantAsync(player.GameContext, grant, TopUpMilestoneEngine.GrantedBy).ConfigureAwait(false);
            }

            await this.DeliverOwedAsync(player).ConfigureAwait(false);
            this.Save();
        }

        var (en, vi) = DescribeOutcome(outcome, engine.SnapshotFor(login).Owed.Count > 0);
        await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
        await this.SendStateAsync(player, true, outcome.Result.ToString(), en, vi).ConfigureAwait(false);
        return outcome;
    }

    /// <summary>Delivers queued bag items/zen waiting for room.</summary>
    /// <param name="player">The player.</param>
    /// <returns>A task representing the delivery.</returns>
    public async ValueTask TakeOwedAsync(Player player)
    {
        if (AccountLoginOf(player) is not { } login)
        {
            return;
        }

        var (given, left) = await this.DeliverOwedAsync(player).ConfigureAwait(false);
        this.Save();
        var en = left > 0 ? "Your bag is full; remaining items are still waiting." : given > 0 ? "Top-up milestone items delivered to your bag." : "No waiting items.";
        var vi = left > 0 ? "Túi đã đầy; vật phẩm còn lại đang chờ chỗ trống." : given > 0 ? "Đã nhận vật phẩm mốc nạp vào túi." : "Không có vật phẩm đang chờ.";
        await player.ShowBlueMessageAsync(IsVietnamese(player) ? vi : en).ConfigureAwait(false);
        await this.SendStateAsync(player, true, left > 0 ? "BagFull" : "Ok", en, vi).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        var serverId = (player.GameContext as IGameServerContext)?.Id ?? 0;
        Contexts[serverId] = player.GameContext;
        if (currentState != PlayerState.EnteredWorld || previousState != PlayerState.CharacterSelection || AccountLoginOf(player) is null)
        {
            return ValueTask.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var (given, left) = await this.DeliverOwedAsync(player).ConfigureAwait(false);
                if (given > 0)
                {
                    await player.ShowBlueMessageAsync(IsVietnamese(player)
                        ? "Đã giao vật phẩm mốc nạp đang chờ vào túi."
                        : "Delivered your waiting top-up milestone items.").ConfigureAwait(false);
                }

                if (given > 0 || left > 0)
                {
                    this.Save();
                    await this.SendStateAsync(player, false).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Top-up milestones: waiting rewards of {Player} could not be delivered.", player);
            }
        });

        return ValueTask.CompletedTask;
    }

    private static bool IsVietnamese(Player player) => player.Culture.TwoLetterISOLanguageName == "vi";

    private static string? AccountLoginOf(Player player) =>
        player is OfflinePlayer || player.IsTemplatePlayer || player.Account is not { IsBot: false, LoginName: { Length: > 0 } login }
            ? null
            : login;

    private static (string En, string Vi) DescribeOutcome(TopUpClaimOutcome outcome, bool hasWaitingBag) => outcome.Result switch
    {
        TopUpClaimResult.Ok when hasWaitingBag => (
            "Milestone claimed! Coins granted; items wait for bag room.",
            "Đã nhận mốc nạp! Coin đã cộng; vật phẩm đang chờ túi trống."),
        TopUpClaimResult.Ok => (
            $"Milestone claimed: {outcome.Step?.TitleEn}!",
            $"Đã nhận thưởng mốc nạp: {outcome.Step?.TitleVi}!"),
        TopUpClaimResult.AlreadyClaimed => ("You already claimed this milestone.", "Bạn đã nhận mốc nạp này rồi."),
        TopUpClaimResult.NotReached => ("Your total top-up has not reached this milestone yet.", "Tổng nạp của bạn chưa đạt mốc này."),
        TopUpClaimResult.Disabled => ("Top-up milestones are currently disabled.", "Thưởng mốc nạp hiện đang tắt."),
        _ => ("Milestone step not found.", "Không tìm thấy mốc nạp."),
    };

    private async ValueTask<(int Given, int Left)> DeliverOwedAsync(Player player)
    {
        if (AccountLoginOf(player) is not { } login || player.SelectedCharacter is null)
        {
            return (0, 0);
        }

        var engine = this.GetEngine();
        var keep = new List<GiftCodeOwedReward>();
        var given = 0;
        foreach (var reward in engine.TakeOwed(login))
        {
            try
            {
                if (await GiftCodeRewards.GiveGoodsAsync(player, reward).ConfigureAwait(false))
                {
                    given++;
                }
                else
                {
                    keep.Add(reward);
                }
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Top-up milestones: failed delivering owed reward {Reference} to {Player}.", reward.Reference, player);
                keep.Add(reward);
            }
        }

        engine.ReturnOwed(login, keep);
        return (given, keep.Count);
    }

    private void Save()
    {
        var store = this.GetEngine().Store;
        lock (store.Lock)
        {
            store.SaveIfChanged();
        }
    }
}
