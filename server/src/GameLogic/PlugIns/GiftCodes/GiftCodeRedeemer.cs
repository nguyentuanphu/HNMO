// <copyright file="GiftCodeRedeemer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// Who enters a code: the account and the character which gets the zen and items.
/// </summary>
/// <param name="Login">The account's login name.</param>
/// <param name="CharacterId">The character's id.</param>
/// <param name="CharacterName">The character's name.</param>
/// <param name="Level">The character's level.</param>
/// <param name="Resets">The character's resets.</param>
/// <param name="ClassNumber">The number of the character's class.</param>
/// <param name="NetworkAddress">The connection address, used transiently and never persisted in raw form.</param>
/// <param name="DeviceId">The optional browser-generated device id, used transiently and never persisted in raw form.</param>
public sealed record GiftCodeRedeemer(string Login, string CharacterId, string CharacterName, int Level, int Resets, int ClassNumber, string? NetworkAddress = null, string? DeviceId = null);
