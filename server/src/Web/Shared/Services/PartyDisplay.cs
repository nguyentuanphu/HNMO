// <copyright file="PartyDisplay.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Services;

using HNMO.GameLogic;

/// <summary>
/// Normalizes party display data: a row only counts as partied when a party master is known.
/// A <see cref="Party"/> whose master is momentarily <c>null</c> sorts and renders as solo.
/// </summary>
public static class PartyDisplay
{
    /// <summary>
    /// Extracts the display data of the given party.
    /// </summary>
    /// <param name="party">The party, if the player is in one.</param>
    /// <returns>The party master name and member count; both empty when not partied.</returns>
    public static (string? Master, int Size) From(Party? party)
    {
        var master = party?.PartyMaster?.Name;
        return master is null ? (null, 0) : (master, party!.PartyList.Count);
    }
}
