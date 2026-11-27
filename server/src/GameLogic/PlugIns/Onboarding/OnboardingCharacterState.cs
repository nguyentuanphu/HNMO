// <copyright file="OnboardingCharacterState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The guide of a character.
/// </summary>
public sealed class OnboardingCharacterState
{
    /// <summary>Gets or sets the character name (for the logs).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the login name of its account.</summary>
    public string? Account { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public OnboardingStatus Status { get; set; }

    /// <summary>Gets or sets when it was first seen.</summary>
    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Gets or sets when the guide ended (done or skipped).</summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>Gets or sets the ids of the steps which are done (or skipped, when optional).</summary>
    public List<string> Completed { get; set; } = new();

    /// <summary>Gets or sets the ids of the optional steps the player skipped (no reward).</summary>
    public List<string> SkippedSteps { get; set; } = new();

    /// <summary>Gets or sets the id of the step whose <see cref="Value"/> and <see cref="Baseline"/> these are.</summary>
    public string? StepId { get; set; }

    /// <summary>Gets or sets the count of the current step.</summary>
    public int Value { get; set; }

    /// <summary>Gets or sets the stat points or skills when the current step started (for the steps which count new ones).</summary>
    public int? Baseline { get; set; }

    /// <summary>Gets or sets a value indicating whether the character was created while the plug-in ran (it gets the kit's stat points then).</summary>
    public bool CreatedHere { get; set; }

    /// <summary>Gets or sets a value indicating whether the starter kit is this character's (given, or waiting for the first login).</summary>
    public bool KitReserved { get; set; }

    /// <summary>Gets or sets a value indicating whether the starter kit was handed over (it may still wait for room in the bag).</summary>
    public bool KitGiven { get; set; }

    /// <summary>Gets or sets a value indicating whether the player was told about the kit.</summary>
    public bool KitShown { get; set; }
}
