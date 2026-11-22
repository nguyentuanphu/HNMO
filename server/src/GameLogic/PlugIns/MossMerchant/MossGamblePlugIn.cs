// <copyright file="MossGamblePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MossMerchant;

using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Properties;
using HNMO.PlugIns;

/// <summary>
/// Implements Moss the Merchant (492) random-weapon gambling in Elbeland when a player buys gamble vouchers (13-71..75).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MossGamblePlugIn_Name), Description = nameof(PlugInResources.MossGamblePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B2D84E71-9C43-4F65-8A12-5D9C3B4E7092")]
public sealed class MossGamblePlugIn :
    INpcItemPurchasePlugIn,
    ISupportCustomConfiguration<MossGambleConfiguration>,
    ISupportDefaultCustomConfiguration
{
    private const short MossNpcNumber = 492;

    /// <inheritdoc />
    public MossGambleConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MossGambleConfiguration();

    /// <inheritdoc />
    public async ValueTask BeforePurchaseAsync(Player player, NonPlayerCharacter merchant, NpcItemPurchaseArgs eventArgs)
    {
        if (merchant.Definition?.Number != MossNpcNumber
            || eventArgs.StoreItem.Definition is not { Group: 13, Number: >= 71 and <= 75 } voucherDef)
        {
            return;
        }

        var config = this.Configuration ?? new MossGambleConfiguration();
        if (!config.Enabled)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MossGambleDisabled)).ConfigureAwait(false);
            eventArgs.Cancel = true;
            return;
        }

        var rolled = MossGambleCatalog.RollReward(player.GameContext.Configuration, voucherDef.Number, Rand.GetRandomizer());
        if (rolled is null)
        {
            eventArgs.Cancel = true;
            return;
        }

        eventArgs.ReplacementItem = rolled;
        eventArgs.Price = Math.Max(0, config.PriceZen);
    }

    /// <inheritdoc />
    public async ValueTask AfterPurchaseAsync(Player player, NonPlayerCharacter merchant, NpcItemPurchaseArgs eventArgs, Item boughtItem)
    {
        if (merchant.Definition?.Number != MossNpcNumber || eventArgs.ReplacementItem is null)
        {
            return;
        }

        var itemName = boughtItem.Definition?.Name.GetTranslation(player.Culture)
            ?? boughtItem.Definition?.Name.ValueInNeutralLanguage
            ?? boughtItem.Definition?.ToString()
            ?? string.Empty;
        var displayName = boughtItem.Level > 0 ? $"{itemName} +{boughtItem.Level}" : itemName;
        await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MossGambleWon), displayName).ConfigureAwait(false);
    }
}
