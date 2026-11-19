// <copyright file="GiftCodeAccountRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// An account's login for the website's gift codes.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
public sealed record GiftCodeAccountRequest(string? LoginName, string? Password);
