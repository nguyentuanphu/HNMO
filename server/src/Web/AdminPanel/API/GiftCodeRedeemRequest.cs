// <copyright file="GiftCodeRedeemRequest.cs" company="HNMO">
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
/// A gift code entered on the website.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
/// <param name="Character">The character which gets the zen and items.</param>
/// <param name="Code">The code.</param>
/// <param name="DeviceId">The optional browser UUID; legacy clients leave it unknown.</param>
public sealed record GiftCodeRedeemRequest(string? LoginName, string? Password, string? Character, string? Code, string? DeviceId = null);
