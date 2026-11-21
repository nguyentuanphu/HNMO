// <copyright file="SafetyItemRequest.cs" company="HNMO">
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
/// A lock or unlock of an item.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The account's password.</param>
/// <param name="ItemId">The item's id.</param>
/// <param name="Unlock">Whether to ask the unlock (else lock).</param>
/// <param name="Pin">The second password (for an unlock).</param>
/// <param name="Address">The visitor's address.</param>
public sealed record SafetyItemRequest(string? LoginName, string? Password, Guid ItemId, bool Unlock, string? Pin, string? Address = null);
