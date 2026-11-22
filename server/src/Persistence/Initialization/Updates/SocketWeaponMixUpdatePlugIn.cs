// <copyright file="SocketWeaponMixUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Properties;
using HNMO.Persistence.Initialization.VersionSeasonSix.Items;
using HNMO.PlugIns;

/// <summary>
/// Mandatory update that adds any missing Creation Notes (14-191..203), their matching 5-socket
/// weapons (reusing the Season 9 item catalog definitions), and the Chaos Machine socket weapon mix (#57)
/// for existing Season 6 and Season 9 configurations without modifying existing customizations.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SocketWeaponMixUpdatePlugIn_Name), Description = nameof(PlugInResources.SocketWeaponMixUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E7A4D8B2-3C19-4E5F-9A01-8F2D6B4C7E57")]
public class SocketWeaponMixUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInResources.SocketWeaponMixUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.SocketWeaponMixUpdatePlugIn_Description;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 4, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(AddItemRuleFlagsPlugIn).GUID];

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new SocketWeaponMixItems(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
