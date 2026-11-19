// <copyright file="SafetyAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Globalization;
using System.Net;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The actions which can need the second password.
/// </summary>
public enum SafetyAction
{
    /// <summary>Taking items or zen out of the vault.</summary>
    Vault,

    /// <summary>Starting or accepting a trade.</summary>
    Trade,

    /// <summary>Opening the personal store.</summary>
    PersonalStore,

    /// <summary>Dropping or selling a valuable item.</summary>
    ValuableItem,
}
