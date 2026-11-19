// <copyright file="SafetyLoginRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// An account's login for the account page's safety panel.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
/// <param name="Address">The visitor's address (told by the web bridge).</param>
public sealed record SafetyLoginRequest(string? LoginName, string? Password, string? Address = null);
