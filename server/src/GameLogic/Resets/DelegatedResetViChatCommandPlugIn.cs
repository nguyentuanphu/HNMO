// <copyright file="DelegatedResetViChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Resets;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.PlugIns;

/// <summary>
/// The Vietnamese name of <see cref="DelegatedResetChatCommandPlugIn"/>: <c>/uythac on|off</c>.
/// </summary>
[Guid("7C1F5A38-9E24-4D7B-B6A0-58E2F4C19D73")]
[PlugIn]
[Display(Name = nameof(PlugInResources.DelegatedResetViChatCommandPlugIn_Name), Description = nameof(PlugInResources.DelegatedResetChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp("/uythac", CharacterStatus.Normal)]
public class DelegatedResetViChatCommandPlugIn : DelegatedResetChatCommandPlugIn
{
    /// <inheritdoc />
    public override string Key => "/uythac";
}
