// <copyright file="Metrics.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Network;

/// <summary>
/// Provides information about the available metrics of this project.
/// </summary>
public static class Metrics
{
    /// <summary>
    /// Gets all available meters of this project.
    /// </summary>
    public static IEnumerable<string> Meters
    {
        get { yield return Connection.MeterName; }
    }
}