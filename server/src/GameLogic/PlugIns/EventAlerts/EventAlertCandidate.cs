// <copyright file="EventAlertCandidate.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.EventAlerts;

/// <summary>
/// Evaluated event/boss alert ready for deduplicated Web Push and Discord delivery.
/// </summary>
/// <param name="SentKey">Unique deduplication key for this occurrence and phase.</param>
/// <param name="CategoryKey">Normalized category key for subscriber filtering (e.g. <c>blood_castle</c>, <c>world_boss</c>).</param>
/// <param name="TitleEn">Localized English alert title.</param>
/// <param name="TitleVi">Localized Vietnamese alert title.</param>
/// <param name="BodyEn">Localized English alert body (with Sydney time).</param>
/// <param name="BodyVi">Localized Vietnamese alert body (with Sydney time).</param>
public sealed record EventAlertCandidate(
    string SentKey,
    string CategoryKey,
    string TitleEn,
    string TitleVi,
    string BodyEn,
    string BodyVi);
