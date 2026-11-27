// <copyright file="MissionStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The saved data of the missions and earned coins, in a JSON file in the save folder (no database table):
/// the progress per character and the earned coins per account.
/// </summary>
public sealed class MissionStoreData
{
    /// <summary>Gets or sets the characters by their id.</summary>
    public Dictionary<string, MissionCharacterState> Characters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the accounts by their login name.</summary>
    public Dictionary<string, MissionAccountState> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
