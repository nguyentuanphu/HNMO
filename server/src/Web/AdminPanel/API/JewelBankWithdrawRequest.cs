// <copyright file="JewelBankWithdrawRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Website jewel withdrawal request.</summary>
/// <param name="LoginName">Login name.</param>
/// <param name="Password">Password.</param>
/// <param name="JewelKey">Jewel key.</param>
/// <param name="Quantity">Quantity to withdraw.</param>
public sealed record JewelBankWithdrawRequest(string? LoginName, string? Password, string? JewelKey, int Quantity);
