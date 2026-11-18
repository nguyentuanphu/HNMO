// <copyright file="VipAdminSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using HNMO.GameLogic.PlugIns.Vip;

/// <summary>The authorized editor settings and bounded list of proved subscriptions.</summary>
/// <param name="Configuration">The current saved settings or disabled defaults.</param>
/// <param name="IsActive">Whether the plug-in is registered as active.</param>
/// <param name="Accounts">At most fifty proved subscriptions; searching an exact login also rebuilds a missing index entry.</param>
/// <param name="HasMore">Whether the shared index has more accounts than this bounded page.</param>
public sealed record VipAdminSnapshot(VipConfiguration Configuration, bool IsActive, IReadOnlyList<VipAdminAccount> Accounts, bool HasMore);
