// <copyright file="ChatCommandViewItem.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Models;

using HNMO.GameLogic.PlugIns.ChatCommands;

/// <summary>
/// A chat command, as it's shown on the chat commands page.
/// </summary>
public class ChatCommandViewItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCommandViewItem"/> class.
    /// </summary>
    /// <param name="info">The description of the chat command.</param>
    /// <param name="plugIn">The plugin configuration of the command, which is required to activate or deactivate it.</param>
    public ChatCommandViewItem(ChatCommandInfo info, PlugInConfigurationViewItem plugIn)
    {
        this.Info = info;
        this.PlugIn = plugIn;
    }

    /// <summary>
    /// Gets the description of the chat command.
    /// </summary>
    public ChatCommandInfo Info { get; }

    /// <summary>
    /// Gets the plugin configuration of the command.
    /// </summary>
    public PlugInConfigurationViewItem PlugIn { get; }

    /// <summary>
    /// Gets a value indicating whether the command is currently active.
    /// </summary>
    public bool IsActive => this.PlugIn.IsActive;
}
