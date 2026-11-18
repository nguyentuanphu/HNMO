// <copyright file="TeleportDecision.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TeleportNpc;

/// <summary>
/// Result of evaluating a Teleport NPC warp request against configuration and character state.
/// </summary>
/// <param name="Allowed">Whether warping is permitted.</param>
/// <param name="Reason">Denial reason when not allowed.</param>
/// <param name="Destination">Resolved destination config when found.</param>
/// <param name="ZenCost">Zen fee required for this warp.</param>
/// <param name="MessageEn">English message describing approval or denial.</param>
/// <param name="MessageVi">Vietnamese message describing approval or denial.</param>
public sealed record TeleportDecision(
    bool Allowed,
    TeleportDenialReason Reason,
    TeleportDestinationConfig? Destination,
    int ZenCost,
    string MessageEn,
    string MessageVi);
