// <copyright file="CrossWhisper.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>A whisper sent from one HNMO program to a character playing in another.</summary>
/// <param name="Id">The whisper's id.</param>
/// <param name="From">The sender's character name.</param>
/// <param name="FromSeason">The sender's program.</param>
/// <param name="ToCharacter">The receiver's character name.</param>
/// <param name="Text">The text.</param>
public sealed record CrossWhisper(long Id, string From, string FromSeason, string ToCharacter, string Text);
