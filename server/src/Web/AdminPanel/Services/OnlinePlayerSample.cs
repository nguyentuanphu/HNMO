// <copyright file="OnlinePlayerSample.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// A sample of the number of online players at a point in time.
/// </summary>
/// <param name="Timestamp">The point in time (UTC) when the sample was taken.</param>
/// <param name="PlayerCount">The number of online players.</param>
public sealed record OnlinePlayerSample(DateTime Timestamp, int PlayerCount);
