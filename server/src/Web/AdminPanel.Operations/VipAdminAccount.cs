// <copyright file="VipAdminAccount.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>A proved current subscription for the authorized host list.</summary>
/// <param name="LoginName">The account login.</param>
/// <param name="LevelId">The current paid or granted level.</param>
/// <param name="NameEn">The English level name.</param>
/// <param name="NameVi">The Vietnamese level name.</param>
/// <param name="UntilUtc">The exact timed-grant expiry.</param>
public sealed record VipAdminAccount(string LoginName, int LevelId, string NameEn, string NameVi, DateTime UntilUtc);
