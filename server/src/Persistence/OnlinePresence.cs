// <copyright file="OnlinePresence.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>An account playing in one of the HNMO programs (Season 6.3, Season 9 or the Arena), as that program published it.</summary>
/// <param name="Login">The account's login name (the same in every program; the account ids differ).</param>
/// <param name="Character">The character being played.</param>
/// <param name="Season">The program: "s6", "s9" or "arena".</param>
/// <param name="ServerId">The game server.</param>
public sealed record OnlinePresence(string Login, string Character, string Season, int ServerId);
