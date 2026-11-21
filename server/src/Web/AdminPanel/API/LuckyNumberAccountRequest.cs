// <copyright file="LuckyNumberAccountRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.LuckyNumber;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Request to inspect an account's lucky number picks.</summary>
/// <param name="LoginName">Account login name.</param>
/// <param name="Password">Account password.</param>
public sealed record LuckyNumberAccountRequest(string? LoginName, string? Password);
