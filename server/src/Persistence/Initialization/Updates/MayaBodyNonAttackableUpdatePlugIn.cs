// <copyright file="MayaBodyNonAttackableUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Mandatory update that sets Maya's body (monster #364) from <see cref="NpcObjectKind.Monster"/>
/// to <see cref="NpcObjectKind.PassiveNpc"/> (and zero drops when still at the old 7-drop value)
/// so it cannot be hit during the Kanturu Event, matching the classic behavior.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MayaBodyNonAttackableUpdatePlugIn_Name), Description = nameof(PlugInResources.MayaBodyNonAttackableUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D6C4A19E-7B22-4F81-9E3A-364B7F1E2026")]
public class MayaBodyNonAttackableUpdatePlugIn : UpdatePlugInBase
{
    private const short MayaBodyNumber = 364;
    private const int PreviousItemDrops = 7;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInResources.MayaBodyNonAttackableUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.MayaBodyNonAttackableUpdatePlugIn_Description;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 5, 15, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var maya = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == MayaBodyNumber);
        if (maya is { ObjectKind: NpcObjectKind.Monster })
        {
            maya.ObjectKind = NpcObjectKind.PassiveNpc;
            if (maya.NumberOfMaximumItemDrops == PreviousItemDrops)
            {
                maya.NumberOfMaximumItemDrops = 0;
            }
        }

        return ValueTask.CompletedTask;
    }
}
