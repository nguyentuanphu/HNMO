// <copyright file="SafetyPinRequest.cs" company="HNMO">
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
/// A change of the second password.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The account's password.</param>
/// <param name="OldPin">The current second password (needed when there is one).</param>
/// <param name="NewPin">The new second password (4 digits); empty clears it.</param>
/// <param name="Address">The visitor's address.</param>
public sealed record SafetyPinRequest(string? LoginName, string? Password, string? OldPin, string? NewPin, string? Address = null);
