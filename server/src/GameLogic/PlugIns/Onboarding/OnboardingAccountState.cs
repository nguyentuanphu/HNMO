// <copyright file="OnboardingAccountState.cs" company="HNMO">
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
/// The starter kit of an account (when it's given once per account).
/// </summary>
public sealed class OnboardingAccountState
{
    /// <summary>Gets or sets the id of the character which got the account's kit.</summary>
    public string? KitCharacter { get; set; }
}
