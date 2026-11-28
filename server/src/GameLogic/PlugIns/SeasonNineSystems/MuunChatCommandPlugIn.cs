// <copyright file="MuunChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.Properties;
using HNMO.PlugIns;

/// <summary>Chat command <c>/muun</c> displaying active Season 9 Muun pet settings and count.</summary>
[Guid("7C2E9D14-5B08-4F61-8A32-1E9B4C7D8E06")]
[PlugIn]
[Display(Name = nameof(PlugInResources.MuunChatCommandPlugIn_Name), Description = nameof(PlugInResources.MuunChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class MuunChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/muun";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public ValueTask HandleCommandAsync(Player player, string command) =>
        SeasonNineSystemsPlugIn.ExecuteStatusCommandAsync(player, "muun");
}
