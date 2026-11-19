// <copyright file="GiftCodeRewards.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Interfaces;
using HNMO.Persistence;

/// <summary>
/// The result of giving a redemption's coins.
/// </summary>
public enum GiftCodeCoinsResult
{
    /// <summary>Given now.</summary>
    Given,

    /// <summary>Its reference was given before (nothing more to do).</summary>
    AlreadyGiven,

    /// <summary>The account doesn't exist (nothing more to do).</summary>
    NoAccount,

    /// <summary>Not saved; it's tried again later.</summary>
    Failed,
}

/// <summary>
/// Gives the rewards of gift codes: the coins as cash shop coin grants with the redemption's reference (so they count
/// once and are in the coins history), the zen and items into a character's bag, all or nothing.
/// </summary>
public static class GiftCodeRewards
{
    /// <summary>
    /// Saves the coins of a redemption as a cash shop coin grant. A player in the game gets it applied by the caller
    /// (<see cref="CashShopCoinExtensions.ApplyPendingCashShopCoinGrantsAsync"/>); otherwise it's applied when the
    /// player opens the cash shop.
    /// </summary>
    /// <param name="persistence">The persistence context provider.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="coins">The coins.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The result.</returns>
    public static async ValueTask<GiftCodeCoinsResult> GrantCoinsAsync(IPersistenceContextProvider persistence, GameConfiguration configuration, GiftCodePendingCoins coins, ILogger logger)
    {
        if (coins.Amount <= 0 || !Enum.IsDefined(coins.CoinType))
        {
            return GiftCodeCoinsResult.AlreadyGiven;
        }

        try
        {
            using var context = persistence.CreateNewPlayerContext(configuration);
            if (await context.GetCashShopCoinGrantByReferenceAsync(coins.Reference).ConfigureAwait(false) is not null)
            {
                return GiftCodeCoinsResult.AlreadyGiven;
            }

            if (await context.GetAccountIdByLoginNameAsync(coins.Login).ConfigureAwait(false) is not { } accountId)
            {
                return GiftCodeCoinsResult.NoAccount;
            }

            var entry = context.CreateNew<CashShopCoinGrant>();
            entry.AccountId = accountId;
            entry.CoinType = coins.CoinType;
            entry.Amount = coins.Amount;
            entry.Reason = coins.Reason;
            entry.Reference = coins.Reference;
            entry.GrantedBy = GiftCodeEngine.GrantedBy;
            entry.CreatedAt = DateTime.UtcNow;

            // A save that reports failure keeps the coins waiting (tried again), instead of dropping them as given.
            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                logger.LogWarning("Gift codes: the coins {Reference} for {Login} weren't saved yet.", coins.Reference, coins.Login);
                return GiftCodeCoinsResult.Failed;
            }

            return GiftCodeCoinsResult.Given;
        }
        catch (Exception ex)
        {
            // The unique reference refuses a second grant which raced this one; anything else is tried again.
            logger.LogWarning(ex, "Gift codes: the coins {Reference} for {Login} weren't saved yet.", coins.Reference, coins.Login);
            return GiftCodeCoinsResult.Failed;
        }
    }

    /// <summary>
    /// Gives the zen and items of a reward to a player, all or nothing.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="reward">The reward.</param>
    /// <returns><c>true</c>, if it was given; <c>false</c>, if it didn't fit (nothing was given).</returns>
    public static async ValueTask<bool> GiveGoodsAsync(Player player, GiftCodeOwedReward reward)
    {
        if (player.Inventory is not { } inventory)
        {
            return false;
        }

        var added = new List<Item>();
        async ValueTask TakeBackAsync()
        {
            foreach (var back in added)
            {
                await inventory.RemoveItemAsync(back).ConfigureAwait(false);
                await player.PersistenceContext.DeleteAsync(back).ConfigureAwait(false);
            }
        }

        foreach (var entry in reward.Items.Where(i => i.Count > 0))
        {
            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == entry.Group && d.Number == entry.Number);
            if (definition is null)
            {
                player.Logger.LogWarning("Gift codes: the reward item {Group} {Number} doesn't exist; it's left out.", entry.Group, entry.Number);
                continue;
            }

            var level = Math.Min(entry.Level, definition.MaximumItemLevel);
            var specification = new ItemSpecification((byte)entry.Group, entry.Number, level, entry.Skill, entry.Luck, entry.OptionLevel, entry.ExcellentOptions);
            if (!GameMasterItemFactory.IsValid(definition, specification))
            {
                player.Logger.LogWarning("Gift codes: the options of the item {Group} {Number} don't fit it; it's given without them.", entry.Group, entry.Number);
                specification = new ItemSpecification((byte)entry.Group, entry.Number, level, entry.Skill && definition.Skill is not null);
            }

            for (var i = 0; i < Math.Min(entry.Count, 50); i++)
            {
                var temporary = GameMasterItemFactory.CreateItem(definition, specification);
                if (temporary.IsStackable())
                {
                    temporary.Durability = Math.Clamp(entry.Durability == 0 ? definition.Durability : entry.Durability, 1, Math.Max(1, (int)definition.Durability));
                }
                else
                {
                    var maximum = temporary.GetMaximumDurabilityOfOnePiece();
                    temporary.Durability = entry.Durability == 0 ? maximum : Math.Min(entry.Durability, maximum);
                }

                if (inventory.CheckInvSpace(temporary) is not { } slot)
                {
                    await TakeBackAsync().ConfigureAwait(false);
                    return false;
                }

                var item = temporary.MakePersistent(player.PersistenceContext);
                if (!await inventory.AddItemAsync(slot, item).ConfigureAwait(false))
                {
                    player.PersistenceContext.Detach(item);
                    await TakeBackAsync().ConfigureAwait(false);
                    return false;
                }

                added.Add(item);
            }
        }

        if (reward.Zen > 0 && !player.TryAddMoney(reward.Zen))
        {
            await TakeBackAsync().ConfigureAwait(false);
            return false;
        }

        foreach (var item in added)
        {
            await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
        }

        if (reward.Zen > 0)
        {
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        }

        return true;
    }
}
