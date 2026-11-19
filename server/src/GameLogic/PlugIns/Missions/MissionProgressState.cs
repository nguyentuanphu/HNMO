// <copyright file="MissionProgressState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
