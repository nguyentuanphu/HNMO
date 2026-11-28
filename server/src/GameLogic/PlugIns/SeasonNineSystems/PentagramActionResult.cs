// <copyright file="PentagramActionResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>The outcome of a pentagram action.</summary>
public enum PentagramActionResult
{
    /// <summary>Done.</summary>
    Ok,

    /// <summary>The Season 9 pentagram system is off in this realm.</summary>
    Disabled,

    /// <summary>The item isn't a pentagram (or an errtel), or the slot is wrong.</summary>
    InvalidItem,

    /// <summary>A pentagram is already worn, or the errtel slot is taken.</summary>
    Occupied,

    /// <summary>Nothing is worn, or the errtel slot is empty.</summary>
    Empty,

    /// <summary>The errtel's element (or rank) differs.</summary>
    ElementMismatch,

    /// <summary>The bag has no room.</summary>
    BagFull,

    /// <summary>The change couldn't be saved; nothing changed.</summary>
    SaveFailed,

    /// <summary>Not at the mix NPC (or not in a safe zone).</summary>
    WrongPlace,

    /// <summary>Not enough Zen.</summary>
    NotEnoughZen,

    /// <summary>The errtel is at its highest level or rank.</summary>
    MaximumReached,

    /// <summary>The mix failed: the Zen (and a used-up errtel) are gone, the errtel stays as it was.</summary>
    Failed,

    /// <summary>The errtel broke while being taken out.</summary>
    Broken,
}
