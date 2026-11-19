// <copyright file="ResetAccountRequest.cs" company="HNMO">
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
/// An account's login for the website's resets.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
public sealed record ResetAccountRequest(string? LoginName, string? Password);
