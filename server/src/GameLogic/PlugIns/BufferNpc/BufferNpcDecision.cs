// <copyright file="BufferNpcDecision.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

using HNMO.DataModel.Configuration;

/// <summary>
/// Result of evaluating a Buffer NPC buff request.
/// </summary>
/// <param name="Allowed">Whether buffs should be applied.</param>
/// <param name="Reason">Denial reason when <paramref name="Allowed"/> is <c>false</c>.</param>
/// <param name="Effects">Resolved magic effects to apply.</param>
/// <param name="DurationOverrideSeconds">Duration override in seconds, or <c>0</c> to use each effect's own duration.</param>
/// <param name="ZenCost">Zen fee to charge.</param>
public sealed record BufferNpcDecision(
    bool Allowed,
    BufferNpcDenialReason Reason,
    IReadOnlyList<MagicEffectDefinition> Effects,
    int DurationOverrideSeconds,
    int ZenCost);
