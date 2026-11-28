// <copyright file="AutoErrorSample.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BugReports;

/// <summary>
/// One error of the web client as it sent it on its own (HNMO extra; owner 2026-11-27: automatic reports go to the host
/// only, and the host forwards what matters to the developer). Nothing in it names the account.
/// </summary>
public sealed class AutoErrorSample
{
    /// <summary>Gets or sets when the server got it (UTC).</summary>
    public DateTime ReceivedUtc { get; set; }

    /// <summary>Gets or sets the kind: error, rejection, packet, webgl or disconnect.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the error's message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the top lines of the stack.</summary>
    public string Stack { get; set; } = string.Empty;

    /// <summary>Gets or sets the client's version or build stamp.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser.</summary>
    public string Browser { get; set; } = string.Empty;

    /// <summary>Gets or sets the operating system.</summary>
    public string Os { get; set; } = string.Empty;

    /// <summary>Gets or sets the device (screen, touch, pixel ratio).</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Gets or sets the map.</summary>
    public string Map { get; set; } = string.Empty;

    /// <summary>Gets or sets the WebGL renderer.</summary>
    public string Gpu { get; set; } = string.Empty;

    /// <summary>Gets or sets the character (shown to the owner; sent on to the developer only when the owner allows it).</summary>
    public string Character { get; set; } = string.Empty;
}
