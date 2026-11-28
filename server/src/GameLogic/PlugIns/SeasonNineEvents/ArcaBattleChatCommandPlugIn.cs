// <copyright file="ArcaBattleChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Properties;
using HNMO.PlugIns;

/// <summary>Chat command <c>/arcabattle</c> for entering or checking Arca Battle.</summary>
[Guid("1B7E4D10-3C92-48A5-8E41-52A6F90D1102")]
[PlugIn]
[Display(Name = nameof(PlugInResources.ArcaBattleChatCommandPlugIn_Name), Description = nameof(PlugInResources.ArcaBattleChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class ArcaBattleChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/arcabattle";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public ValueTask HandleCommandAsync(Player player, string command) =>
        SeasonNineEventsPlugIn.ExecuteChatCommandAsync(player, "arca", "Arca Battle is disabled on this server.", "Sự kiện Chiến Trường Arca đang tắt.");
}
