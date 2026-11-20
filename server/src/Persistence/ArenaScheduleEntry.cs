// <copyright file="ArenaScheduleEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>One of the Arena's scheduled battles as the Arena published it for the home servers.</summary>
/// <param name="Kind">The battle ("royale" or "guildwar").</param>
/// <param name="Days">The days (empty: every day).</param>
/// <param name="Times">The Sydney times.</param>
/// <param name="NextStartUtc">The next start, if one is planned.</param>
/// <param name="UpdatedUtc">When the Arena last published it.</param>
public sealed record ArenaScheduleEntry(string Kind, string Days, string Times, DateTime? NextStartUtc, DateTime UpdatedUtc);
