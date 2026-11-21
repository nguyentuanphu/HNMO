// <copyright file="TopRaceParticipant.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Who did something: a character (or a guild for a guild race).
/// </summary>
/// <param name="Key">The character id, or <see cref="TopRaceEngine.GuildKey"/> of the guild.</param>
/// <param name="Name">The name.</param>
/// <param name="Login">The account's login name; empty for a guild.</param>
/// <param name="Class">The class (<see cref="TopRaceClass"/>); -1 for a guild.</param>
public sealed record TopRaceParticipant(string Key, string Name, string Login, int Class);
