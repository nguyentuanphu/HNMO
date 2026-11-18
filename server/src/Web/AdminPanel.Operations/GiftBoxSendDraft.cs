// <copyright file="GiftBoxSendDraft.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>The reviewed host input; authenticated actor identity is never accepted in this draft.</summary>
/// <param name="SendId">The stable operation identifier retained across uncertain retries.</param>
/// <param name="Audience">The explicit recipient mode.</param>
/// <param name="Logins">The exact login, or one exact login per pasted line.</param>
/// <param name="ActiveDays">An optional observed recent-activity filter.</param>
/// <param name="TitleEn">The English player title.</param>
/// <param name="TitleVi">The Vietnamese player title.</param>
/// <param name="MessageEn">The English player message.</param>
/// <param name="MessageVi">The Vietnamese player message.</param>
/// <param name="Reward">The exact configured reward options.</param>
/// <param name="ExpiresAtUtc">The optional UTC deadline parsed from Sydney time by the host page.</param>
public sealed record GiftBoxSendDraft(Guid SendId, GiftBoxAudienceKind Audience, string Logins, int? ActiveDays, string TitleEn, string TitleVi, string MessageEn, string MessageVi, GiftCodeReward Reward, DateTime? ExpiresAtUtc);
