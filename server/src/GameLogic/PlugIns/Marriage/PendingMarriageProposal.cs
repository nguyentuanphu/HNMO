// <copyright file="PendingMarriageProposal.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.Collections.Concurrent;

/// <summary>Pending in-memory marriage proposal with expiry.</summary>
/// <param name="ProposerName">Proposing character name.</param>
/// <param name="ProposerLogin">Proposer account login.</param>
/// <param name="TargetName">Proposed target character name.</param>
/// <param name="ExpiresAtUtc">Expiry timestamp.</param>
public sealed record PendingMarriageProposal(string ProposerName, string ProposerLogin, string TargetName, DateTime ExpiresAtUtc);
