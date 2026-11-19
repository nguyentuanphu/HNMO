// <copyright file="OnboardingStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The saved data of the new-player guide, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class OnboardingStoreData
{
    /// <summary>Gets or sets when the plug-in first ran with this file; characters created after it are new.</summary>
    public DateTime ActivatedAtUtc { get; set; }

    /// <summary>Gets or sets the characters by their id.</summary>
    public Dictionary<string, OnboardingCharacterState> Characters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the accounts by their login name.</summary>
    public Dictionary<string, OnboardingAccountState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the waiting rewards by character id.</summary>
    public Dictionary<string, List<OnboardingOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);
}
