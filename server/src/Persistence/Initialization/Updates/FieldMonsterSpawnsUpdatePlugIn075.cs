// <copyright file="FieldMonsterSpawnsUpdatePlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Removes the duplicate monster spawns of Dungeon and Lost Tower of version 0.75.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FieldMonsterSpawnsUpdatePlugInBase_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FieldMonsterSpawnsUpdatePlugInBase_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("FA5F8867-A3E3-48AA-9453-9D756049AECE")]
public class FieldMonsterSpawnsUpdatePlugIn075 : FieldMonsterSpawnsUpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => Version075.DataInitialization.Id;
}
