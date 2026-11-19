// <copyright file="WebsiteMissionsInvocation.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Dapr.Common;

using HNMO.Interfaces;

/// <summary>Internal invocation arguments; the player's password is never forwarded.</summary>
/// <param name="ServerId">The addressed server.</param>
/// <param name="Subject">The subject verified by the website API.</param>
/// <param name="Action">The optional claim action.</param>
/// <param name="Argument">The optional mission identifier or invitation code.</param>
public sealed record WebsiteMissionsInvocation(int ServerId, WebsiteMissionSubject Subject, string? Action = null, string? Argument = null);
