// <copyright file="VaultChatCommandArgs.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments for the <c>/vault</c> multi-vault command (index, <c>buy</c>, or empty for status).
/// </summary>
public class VaultChatCommandArgs : ArgumentsBase
{
    /// <summary>Gets or sets optional target vault index (0..19) or <c>buy</c>.</summary>
    [Argument("vault", false)]
    public string? Target { get; set; }
}
