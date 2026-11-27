// <copyright file="MarriageChatCommandArgs.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>Arguments for the <c>/marry</c> chat command.</summary>
public sealed class MarriageChatCommandArgs : ArgumentsBase
{
    /// <summary>Gets or sets the subcommand (<c>accept</c>, <c>decline</c>, <c>divorce</c>, <c>warp</c>, <c>title</c>) or partner name.</summary>
    [Argument("target", false)]
    public string? Target { get; set; }

    /// <summary>Gets or sets the optional title text when using <c>/marry title &lt;text&gt;</c>.</summary>
    [Argument("value", false)]
    public string? Value { get; set; }
}
