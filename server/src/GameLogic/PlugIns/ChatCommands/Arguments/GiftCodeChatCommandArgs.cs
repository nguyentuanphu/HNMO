// <copyright file="GiftCodeChatCommandArgs.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments for the gift code chat command.
/// </summary>
public class GiftCodeChatCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the gift code.
    /// </summary>
    [Argument("code", false)]
    public string? Code { get; set; }
}
