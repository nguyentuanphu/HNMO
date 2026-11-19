// <copyright file="PartyOptionsStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.IO;
using System.Text.Json;

/// <summary>Persistent state stored under section <c>partyOptions</c> in <see cref="HnmoStateStore"/>.</summary>
public sealed class PartyOptionsStoreData
{
    /// <summary>Gets or sets latest public spot advertisements snapshot.</summary>
    public List<PartyAdSnapshot> ActiveAds { get; set; } = [];
}
