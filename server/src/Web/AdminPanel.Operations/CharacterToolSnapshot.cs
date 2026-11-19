// <copyright file="CharacterToolSnapshot.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>Current character state loaded by the Character Tools admin page.</summary>
/// <param name="CharacterId">The character identifier.</param>
/// <param name="CharacterName">The character name.</param>
/// <param name="AccountLogin">The owning account login name.</param>
/// <param name="IsOnline">Whether the owning account is currently online.</param>
/// <param name="OnlineServerId">The server ID if online.</param>
/// <param name="CharacterStatusName">The character privilege status.</param>
/// <param name="CharacterClassId">The current class identifier.</param>
/// <param name="CharacterClassName">The current class name.</param>
/// <param name="MapNumber">The current map number.</param>
/// <param name="MapName">The current map name.</param>
/// <param name="X">The X coordinate.</param>
/// <param name="Y">The Y coordinate.</param>
/// <param name="Level">The regular level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The reset count.</param>
/// <param name="Strength">Base Strength.</param>
/// <param name="Agility">Base Agility.</param>
/// <param name="Vitality">Base Vitality.</param>
/// <param name="Energy">Base Energy.</param>
/// <param name="Leadership">Base Leadership.</param>
/// <param name="Zen">Inventory Zen.</param>
public sealed record CharacterToolSnapshot(
    Guid CharacterId,
    string CharacterName,
    string AccountLogin,
    bool IsOnline,
    int? OnlineServerId,
    string CharacterStatusName,
    Guid CharacterClassId,
    string CharacterClassName,
    ushort MapNumber,
    string MapName,
    byte X,
    byte Y,
    int Level,
    int MasterLevel,
    int Resets,
    int Strength,
    int Agility,
    int Vitality,
    int Energy,
    int Leadership,
    int Zen);
