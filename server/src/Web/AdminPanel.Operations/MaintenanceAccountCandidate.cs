// <copyright file="MaintenanceAccountCandidate.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>
/// Dry-run preview entry for an inactive/empty account eligible for maintenance cleanup.
/// </summary>
/// <param name="AccountId">Account identifier.</param>
/// <param name="LoginName">Account login name.</param>
/// <param name="RegistrationDateUtc">UTC registration timestamp.</param>
/// <param name="CharacterCount">Number of characters on the account.</param>
/// <param name="VaultItemCount">Number of items currently stored in the account vault.</param>
/// <param name="TotalCoins">Sum of WCoinC, WCoinP, and GoblinPoints.</param>
public sealed record MaintenanceAccountCandidate(
    Guid AccountId,
    string LoginName,
    DateTime RegistrationDateUtc,
    int CharacterCount,
    int VaultItemCount,
    int TotalCoins);
