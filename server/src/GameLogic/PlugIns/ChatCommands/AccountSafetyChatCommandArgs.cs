// <copyright file="AccountSafetyChatCommandArgs.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.PlugIns;

/// <summary>
/// Arguments of the account safety chat commands (they read the raw text; this is for the help).
/// </summary>
public class AccountSafetyChatCommandArgs : ArgumentsBase
{
    /// <summary>Gets or sets the first value (a PIN or a bag cell).</summary>
    [Argument("value", false)]
    public string? Value { get; set; }

    /// <summary>Gets or sets the second value (the PIN of /unlockitem).</summary>
    [Argument("pin", false)]
    public string? Pin { get; set; }
}
