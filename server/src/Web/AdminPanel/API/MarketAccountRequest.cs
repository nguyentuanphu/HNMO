// <copyright file="MarketAccountRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ItemMarket;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Account market lookup request.</summary>
/// <param name="LoginName">Account login name.</param>
/// <param name="Password">Account password.</param>
public sealed record MarketAccountRequest(string? LoginName, string? Password);
