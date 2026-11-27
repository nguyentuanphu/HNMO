// <copyright file="BufferNpcPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Configurable Buffer NPC plug-in (OFF by default) granting a curated set of buffs to characters up to a configured level.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Buffer NPC", Description = "Gives a configurable set of buffs to characters under an admin-configured level (OFF by default).")]
[Guid("3E41F8B0-7D22-4C9E-A510-1B8A2C6D4E01")]
public sealed class BufferNpcPlugIn :
    IPlayerTalkToNpcPlugIn,
    ISupportCustomConfiguration<BufferNpcConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <inheritdoc />
    public BufferNpcConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the active <see cref="BufferNpcPlugIn"/> for the specified game context, or <c>null</c> when disabled.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in instance, or <c>null</c>.</returns>
    public static BufferNpcPlugIn? Get(IGameContext gameContext) =>
        gameContext.PlugInManager.GetActivePlugInsOf<IPlayerTalkToNpcPlugIn>().OfType<BufferNpcPlugIn>().FirstOrDefault();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new BufferNpcConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (eventArgs.HasBeenHandled)
        {
            return;
        }

        if (await this.TryHandleBuffRequestAsync(player, npc).ConfigureAwait(false))
        {
            eventArgs.HasBeenHandled = true;
            eventArgs.LeavesDialogOpen = false;
        }
    }

    /// <summary>
    /// Handles a buff request for <paramref name="npc"/> if this NPC is configured in <see cref="BufferNpcConfiguration.NpcNumbers"/>.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="npc">The opened or clicked NPC.</param>
    /// <returns><c>true</c> if the NPC is managed by the Buffer NPC plug-in (whether granted or refused with a message); otherwise, <c>false</c>.</returns>
    public async ValueTask<bool> TryHandleBuffRequestAsync(Player player, NonPlayerCharacter? npc)
    {
        if (npc?.Definition is null || player.SelectedCharacter is null)
        {
            return false;
        }

        var cfg = this.Configuration ?? new BufferNpcConfiguration();
        if (cfg.NpcNumbers is null || !cfg.NpcNumbers.Contains(npc.Definition.Number))
        {
            return false;
        }

        var effectsByNumber = player.GameContext.Configuration.MagicEffects
            .GroupBy(e => e.Number)
            .ToDictionary(g => g.Key, g => g.First());
        var resets = (int)(player.Attributes?[Stats.Resets] ?? 0f);
        var decision = BufferNpcEngine.Evaluate(
            cfg,
            true,
            npc.Definition.Number,
            player.Level,
            resets,
            player.Money,
            effectsByNumber);

        var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        if (!decision.Allowed)
        {
            var reasonText = decision.Reason switch
            {
                BufferNpcDenialReason.LevelTooHigh => isVi
                    ? $"Cấp độ vượt quá giới hạn nhận hỗ trợ của NPC Buffer (tối đa cấp {Math.Max(1, cfg.MaxLevel)})."
                    : $"Your level exceeds the Buffer NPC limit (max level {Math.Max(1, cfg.MaxLevel)}).",
                BufferNpcDenialReason.ResetsTooHigh => isVi
                    ? $"Số lần Reset vượt quá giới hạn NPC Buffer (tối đa {cfg.MaxResets} Reset)."
                    : $"Your reset count exceeds the Buffer NPC limit (max {cfg.MaxResets} resets).",
                BufferNpcDenialReason.NotEnoughZen => isVi
                    ? $"Cần {decision.ZenCost:N0} Zen để nhận gói hỗ trợ từ NPC Buffer."
                    : $"You need {decision.ZenCost:N0} Zen to receive Buffer NPC buffs.",
                BufferNpcDenialReason.NoValidEffects => isVi
                    ? "NPC Buffer hiện chưa được cấu hình bùa hỗ trợ hợp lệ."
                    : "The Buffer NPC has no valid buffs configured.",
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(reasonText))
            {
                await player.ShowBlueMessageAsync(reasonText).ConfigureAwait(false);
            }

            return true;
        }

        if (decision.ZenCost > 0 && !player.TryRemoveMoney(decision.ZenCost))
        {
            await player.ShowBlueMessageAsync(isVi
                ? $"Cần {decision.ZenCost:N0} Zen để nhận hỗ trợ."
                : $"You need {decision.ZenCost:N0} Zen for Buffer NPC buffs.").ConfigureAwait(false);
            return true;
        }

        var appliedCount = 0;
        var appliedSeconds = 0;
        foreach (var effectDef in decision.Effects)
        {
            var durationSeconds = decision.DurationOverrideSeconds > 0
                ? decision.DurationOverrideSeconds
                : (int)(effectDef.Duration?.ConstantValue?.Value ?? 0);
            if (durationSeconds <= 0)
            {
                continue;
            }

            var boosts = effectDef.PowerUpDefinitions
                .Where(def => def.Boost is not null && def.TargetAttribute is not null)
                .Select(def => new MagicEffect.ElementWithTarget(player.Attributes!.CreateElement(def), def.TargetAttribute!))
                .ToArray();
            if (boosts.Length == 0)
            {
                continue;
            }

            var effect = new MagicEffect(TimeSpan.FromSeconds(durationSeconds), effectDef, boosts);
            await player.MagicEffectList.AddEffectAsync(effect).ConfigureAwait(false);
            appliedCount++;
            appliedSeconds = Math.Max(appliedSeconds, durationSeconds);
        }

        if (appliedCount > 0)
        {
            var minutes = Math.Max(1, (int)Math.Round(appliedSeconds / 60.0));
            var msg = decision.ZenCost > 0
                ? (isVi
                    ? $"NPC Buffer đã ban tặng {appliedCount} bùa hỗ trợ ({minutes} phút, phí {decision.ZenCost:N0} Zen)."
                    : $"Buffer NPC granted {appliedCount} buff(s) for {minutes} min ({decision.ZenCost:N0} Zen).")
                : (isVi
                    ? $"NPC Buffer đã ban tặng {appliedCount} bùa hỗ trợ ({minutes} phút)."
                    : $"Buffer NPC granted {appliedCount} buff(s) for {minutes} min.");
            await player.ShowBlueMessageAsync(msg).ConfigureAwait(false);
        }

        return true;
    }
}
