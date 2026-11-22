// <copyright file="MossMerchantShopUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.VersionSeasonSix.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds the five Moss gamble vouchers (13-71..75) at 2,000,000 Zen and equips Moss the Merchant (492)
/// with his gamble shop when his store is still unset, preserving existing admin customizations.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.MossMerchantShopUpdatePlugIn_Name), Description = nameof(Properties.PlugInResources.MossMerchantShopUpdatePlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("B7A4F12E-903D-48C6-A519-4F8C32E19B07")]
public class MossMerchantShopUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => Properties.PlugInResources.MossMerchantShopUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => Properties.PlugInResources.MossMerchantShopUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 12, 48, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        MossGambleItemCatalog.CreateItems(context, gameConfiguration, ensurePriceDefinition: true);

        var moss = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == 492);
        if (moss is not null && moss.MerchantStore is null)
        {
            moss.NpcWindow = NpcWindow.Merchant;
            moss.MerchantStore = MossGambleItemCatalog.CreateMerchantStore(context, gameConfiguration);
        }

        return ValueTask.CompletedTask;
    }
}
