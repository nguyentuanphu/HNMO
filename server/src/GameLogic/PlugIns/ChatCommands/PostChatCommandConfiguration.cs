// <copyright file="PostChatCommandConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

/// <summary>
/// The configuration of the /post command (a message to every player of the server).
/// The defaults are those of the Season 6/9 server suites.
/// </summary>
public sealed class PostChatCommandConfiguration
{
    /// <summary>Gets or sets the zen which a post costs (0: free).</summary>
    public int ZenCost { get; set; } = 1_000;

    /// <summary>Gets or sets the seconds a player has to wait between two posts (0: no wait).</summary>
    public int CooldownSeconds { get; set; } = 5;
}
