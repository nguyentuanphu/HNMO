// <copyright file="LegacyServerVersion.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>
/// Detected season era of a legacy server configuration folder.
/// </summary>
public enum LegacyServerVersion
{
    /// <summary>Unable to determine version reliably.</summary>
    Unknown = 0,

    /// <summary>Season 6 / 6.3 era configuration.</summary>
    Season6 = 6,

    /// <summary>Season 9 Episode 2 era configuration (contains elemental, companion, or battle-core additions).</summary>
    Season9 = 9,
}
