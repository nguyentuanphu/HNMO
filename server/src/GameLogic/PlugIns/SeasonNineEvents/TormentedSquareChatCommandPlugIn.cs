// <copyright file="TormentedSquareChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/tsquare</c> for entering or checking Tormented Square.</summary>
[Guid("1B7E4D10-3C92-48A5-8E41-52A6F90D1104")]
[PlugIn]
[Display(Name = nameof(PlugInResources.TormentedSquareChatCommandPlugIn_Name), Description = nameof(PlugInResources.TormentedSquareChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class TormentedSquareChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/tsquare";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public ValueTask HandleCommandAsync(Player player, string command) =>
        SeasonNineEventsPlugIn.ExecuteChatCommandAsync(player, "tsquare", "Tormented Square is disabled on this server.", "Quảng Trường Đau Khổ đang tắt.");
}
