// <copyright file="GiftBoxLetter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>An account-owned message whose goods remain owed until delivery is proved.</summary>
/// <param name="Id">The stable host send identifier.</param>
/// <param name="AccountId">The recipient account, never supplied by a player request.</param>
/// <param name="TitleEn">The English title.</param>
/// <param name="TitleVi">The Vietnamese title.</param>
/// <param name="MessageEn">The English message.</param>
/// <param name="MessageVi">The Vietnamese message.</param>
/// <param name="Reward">A detached snapshot of promised items, Zen and coins.</param>
/// <param name="SentAtUtc">The original UTC send instant.</param>
/// <param name="ExpiresAtUtc">The optional UTC collection deadline.</param>
public sealed record GiftBoxLetter(Guid Id, Guid AccountId, string TitleEn, string TitleVi, string MessageEn, string MessageVi, GiftCodeReward Reward, DateTime SentAtUtc, DateTime? ExpiresAtUtc)
{
    /// <summary>Checks expiry without deleting or consuming the owed reward.</summary>
    /// <param name="nowUtc">The authoritative UTC instant.</param>
    /// <returns>Whether collection is past its optional deadline.</returns>
    public bool IsExpired(DateTime nowUtc) => this.ExpiresAtUtc is { } expiry && nowUtc >= expiry;
}
