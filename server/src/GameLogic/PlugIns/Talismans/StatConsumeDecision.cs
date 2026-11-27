// <copyright file="StatConsumeDecision.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Outcome of evaluating a stat-point consumable against per-character caps.
/// </summary>
/// <param name="IsConfiguredConsumable">Whether the item matched a configured <see cref="StatConsumableDefinitionConfig"/>.</param>
/// <param name="Allowed">Whether consumption is allowed.</param>
/// <param name="DenialReason">Reason key when <paramref name="Allowed"/> is <c>false</c> (<c>cap_reached</c>, <c>no_command</c>, <c>empty</c>).</param>
/// <param name="PointsGranted">Exact points granted after clamping to remaining cap.</param>
/// <param name="EffectiveCap">Effective per-character cap applied.</param>
/// <param name="TargetBucket">Normalized target bucket (<c>levelUpPoints</c>, <c>str</c>, <c>agi</c>, <c>vit</c>, <c>ene</c>, <c>cmd</c>).</param>
/// <param name="DisplayNameEn">Configured English item label.</param>
/// <param name="DisplayNameVi">Configured Vietnamese item label.</param>
public readonly record struct StatConsumeDecision(
    bool IsConfiguredConsumable,
    bool Allowed,
    string? DenialReason,
    int PointsGranted,
    int EffectiveCap,
    string TargetBucket,
    string DisplayNameEn,
    string DisplayNameVi);
