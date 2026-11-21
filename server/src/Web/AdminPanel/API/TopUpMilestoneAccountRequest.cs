// <copyright file="TopUpMilestoneAccountRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>Request to inspect an account's top-up milestone progress.</summary>
/// <param name="LoginName">The account login name.</param>
/// <param name="Password">The account password.</param>
public sealed record TopUpMilestoneAccountRequest(string? LoginName, string? Password);
