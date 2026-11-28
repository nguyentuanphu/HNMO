// <copyright file="AcheronChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/acheron</c> for entering or checking Acheron Guardian.</summary>
[Guid("1B7E4D10-3C92-48A5-8E41-52A6F90D1101")]
[PlugIn]
[Display(Name = nameof(PlugInResources.AcheronChatCommandPlugIn_Name), Description = nameof(PlugInResources.AcheronChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class AcheronChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/acheron";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public ValueTask HandleCommandAsync(Player player, string command) =>
        SeasonNineEventsPlugIn.ExecuteChatCommandAsync(player, "acheron", "Acheron Guardian is disabled on this server.", "Sự kiện Hộ Vệ Acheron đang tắt.");
}
