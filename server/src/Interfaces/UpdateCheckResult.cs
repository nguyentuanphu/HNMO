// <copyright file="UpdateCheckResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Result of checking the developer's hub for a newer release.
/// </summary>
/// <param name="LatestTag">Latest release tag name.</param>
/// <param name="PublishedAtSydney">Release timestamp in Sydney time.</param>
/// <param name="ReleaseNotes">Release notes markdown/text.</param>
/// <param name="DownloadUrl">HTML or asset download URL.</param>
/// <param name="IsUpdateAvailable">Whether a newer release appears available.</param>
public sealed record UpdateCheckResult(
    string LatestTag,
    string PublishedAtSydney,
    string ReleaseNotes,
    string DownloadUrl,
    bool IsUpdateAvailable);
