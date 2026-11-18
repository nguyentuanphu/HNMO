// <copyright file="WebsiteMissionsReply.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>A bounded mission-state response without credentials or network addresses.</summary>
/// <param name="Status">The machine-readable availability/action status.</param>
/// <param name="BoardJson">The mission board, when available.</param>
/// <param name="CharacterOnline">Whether the selected character is connected and can collect through the existing game rules.</param>
public sealed record WebsiteMissionsReply(string Status, string? BoardJson, bool CharacterOnline);
