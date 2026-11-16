// <copyright file="BufferNpcDenialReason.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

/// <summary>
/// Outcome reason for evaluating a Buffer NPC request.
/// </summary>
public enum BufferNpcDenialReason
{
    /// <summary>Eligible to receive buffs.</summary>
    None = 0,

    /// <summary>The plug-in configuration disables the Buffer NPC.</summary>
    Disabled = 1,

    /// <summary>The interacted NPC is not in the configured NPC list.</summary>
    NpcNotConfigured = 2,

    /// <summary>The character exceeds <see cref="BufferNpcConfiguration.MaxLevel"/>.</summary>
    LevelTooHigh = 3,

    /// <summary>The character exceeds <see cref="BufferNpcConfiguration.MaxResets"/>.</summary>
    ResetsTooHigh = 4,

    /// <summary>The character lacks <see cref="BufferNpcConfiguration.ZenCost"/> zen.</summary>
    NotEnoughZen = 5,

    /// <summary>None of the configured magic effects exist or carry power-ups.</summary>
    NoValidEffects = 6,
}
