// <copyright file="ArenaGuardShopUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// Opens the Arena Guard (239) merchant shop on Arena (map 6) with potions, bolts/arrows, Town Portal Scroll,
/// and Armor of Guardsman (omitting 13-7), when Arena Guard still has no shop configured.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ArenaGuardShopUpdatePlugIn_Name), Description = nameof(PlugInResources.ArenaGuardShopUpdatePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D7C4E910-5A2F-4E3B-914D-8B6A2F0C3E91")]
public class ArenaGuardShopUpdatePlugIn : UpdatePlugInBase
{
    private const short ArenaGuardNumber = 239;

    /// <inheritdoc />
    public override string Name => PlugInResources.ArenaGuardShopUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => PlugInResources.ArenaGuardShopUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 12, 45, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == ArenaGuardNumber) is { MerchantStore: null, NpcWindow: NpcWindow.Undefined } guard)
        {
            guard.NpcWindow = NpcWindow.Merchant;
            guard.MerchantStore = PotionShopStorageBuilder.Build(context, gameConfiguration, ArenaGuardNumber);
        }

        return ValueTask.CompletedTask;
    }
}
