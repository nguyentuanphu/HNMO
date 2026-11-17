// <copyright file="MaintenanceExperienceIssue.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>
/// Dry-run preview entry for a character whose stored experience falls outside its level band.
/// </summary>
/// <param name="CharacterId">Character identifier.</param>
/// <param name="AccountLogin">Owning account login name.</param>
/// <param name="CharacterName">Character name.</param>
/// <param name="Level">Current character level.</param>
/// <param name="CurrentExperience">Stored experience value.</param>
/// <param name="FixedExperience">Clamped experience value that will be written on apply.</param>
/// <param name="Reason">Description of the anomaly.</param>
public sealed record MaintenanceExperienceIssue(
    Guid CharacterId,
    string AccountLogin,
    string CharacterName,
    int Level,
    long CurrentExperience,
    long FixedExperience,
    string Reason);
