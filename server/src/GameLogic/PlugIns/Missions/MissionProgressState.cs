// <copyright file="MissionProgressState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The progress of a mission in its current period.
/// </summary>
public sealed class MissionProgressState
{
    /// <summary>Gets or sets the count so far (at most the target).</summary>
    public int Value { get; set; }

    /// <summary>Gets or sets a value indicating whether the reward was claimed.</summary>
    public bool Claimed { get; set; }
}
