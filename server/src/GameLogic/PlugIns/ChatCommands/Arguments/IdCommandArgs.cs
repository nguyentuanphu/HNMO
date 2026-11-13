// <copyright file="IdCommandArgs.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Arguments used by <see cref="RemoveNpcChatCommand"/> and others which just require an id of an object.
/// </summary>
public class IdCommandArgs : ArgumentsBase
{
    /// <summary>
    /// Gets or sets the npc id.
    /// </summary>
    [Argument("id", true)]
    [ValueReference(ChatCommandValueReference.ObjectId)]
    public short Id { get; set; }
}