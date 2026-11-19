// <copyright file="BankTransfer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Text.RegularExpressions;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.TopUpMilestones;
using Microsoft.Extensions.Logging;

/// <summary>
/// An incoming bank transfer as a payment service (Casso, SePay) reported it.
/// </summary>
/// <param name="Provider">The service which reported it ("casso", "sepay").</param>
/// <param name="ProviderId">The service's own id of the transaction.</param>
/// <param name="BankReference">The bank's reference of the transaction (the same for every service), if known.</param>
/// <param name="AmountVnd">The amount in VND.</param>
/// <param name="Message">The transfer message.</param>
public sealed record BankTransfer(string Provider, string ProviderId, string? BankReference, long AmountVnd, string Message);
