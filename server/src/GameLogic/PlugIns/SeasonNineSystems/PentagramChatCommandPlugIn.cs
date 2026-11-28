// <copyright file="PentagramChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/pentagram</c> displaying active Season 9 Pentagram and Errtel settings.</summary>
[Guid("7C2E9D14-5B08-4F61-8A32-1E9B4C7D8E07")]
[PlugIn]
[Display(Name = nameof(PlugInResources.PentagramChatCommandPlugIn_Name), Description = nameof(PlugInResources.PentagramChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class PentagramChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/pentagram";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public ValueTask HandleCommandAsync(Player player, string command) =>
        SeasonNineSystemsPlugIn.ExecuteStatusCommandAsync(player, "pentagram");
}
