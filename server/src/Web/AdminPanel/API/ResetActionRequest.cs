// <copyright file="ResetActionRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// An action of the website's resets.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
/// <param name="Character">The character.</param>
/// <param name="Action">"reset", "relife", "add-stats", "clear-stats", "delegated-on" or "delegated-off".</param>
/// <param name="Str">The points added to strength.</param>
/// <param name="Agi">The points added to agility.</param>
/// <param name="Vit">The points added to vitality.</param>
/// <param name="Ene">The points added to energy.</param>
/// <param name="Cmd">The points added to command.</param>
public sealed record ResetActionRequest(string? LoginName, string? Password, string? Character, string? Action, int? Str = null, int? Agi = null, int? Vit = null, int? Ene = null, int? Cmd = null);
