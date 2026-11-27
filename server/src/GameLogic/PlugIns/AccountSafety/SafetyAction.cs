// <copyright file="SafetyAction.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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
