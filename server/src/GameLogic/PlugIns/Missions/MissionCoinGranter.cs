// <copyright file="MissionCoinGranter.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using HNMO.GameLogic.CashShop;

/// <summary>
/// Gives the coins of missions and earned-coin sources as cash shop coin grants, so every grant is in the coins
/// history with its reason, and its reference makes it count once. The game server applies the grant at once
/// when the account is in this game; otherwise when the player opens the cash shop next time.
/// </summary>
public static class MissionCoinGranter
{
    /// <summary>
    /// The name in the coins history's "granted by" column.
    /// </summary>
    public const string GrantedBy = "HNMO missions";

    /// <summary>
    /// Gives coins earned in the game (missions, world bosses, the new-player guide, community, guild levels, lucky
    /// number, top races, events, bug reports): nothing while this season's cash shop is off (owner 2026-11-28: a classic
    /// season gives no free coins; only the coins players top up, for VIP, remain). Coins which only change hands (market,
    /// escrow) or come from a top-up use <see cref="GrantAsync"/>.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="grant">The grant.</param>
    /// <param name="grantedBy">The name in the coins history's "granted by" column; <see cref="GrantedBy"/> by default.</param>
    /// <returns><c>true</c>, if it was given now; <c>false</c>, if not (the cash shop is off, the reference was given already or the account doesn't exist).</returns>
    public static ValueTask<bool> GrantRewardAsync(IGameContext gameContext, CoinGrant grant, string? grantedBy = null)
    {
        return CoinRewardsAllowed(gameContext) ? GrantAsync(gameContext, grant, grantedBy) : ValueTask.FromResult(false);
    }

    /// <summary>Whether coins earned in the game are given and shown on this season (its cash shop is on).</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns><c>true</c>, if they are.</returns>
    public static bool CoinRewardsAllowed(IGameContext? gameContext) =>
        gameContext is not null && CashShopFeaturePlugIn.GetSettings(gameContext) is not null;

    /// <summary>
    /// Gives a grant.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="grant">The grant.</param>
    /// <param name="grantedBy">The name in the coins history's "granted by" column; <see cref="GrantedBy"/> by default.</param>
    /// <returns><c>true</c>, if it was given now; <c>false</c>, if its reference was given already or the account doesn't exist.</returns>
    public static async ValueTask<bool> GrantAsync(IGameContext gameContext, CoinGrant grant, string? grantedBy = null)
    {
        if (grant.Amount <= 0 || !Enum.IsDefined(grant.CoinType))
        {
            return false;
        }

        using (var context = gameContext.PersistenceContextProvider.CreateNewPlayerContext(gameContext.Configuration))
        {
            if (await context.GetCashShopCoinGrantByReferenceAsync(grant.Reference).ConfigureAwait(false) is not null)
            {
                return false;
            }

            if (await context.GetAccountIdByLoginNameAsync(grant.LoginName).ConfigureAwait(false) is not { } accountId)
            {
                return false;
            }

            var entry = context.CreateNew<CashShopCoinGrant>();
            entry.AccountId = accountId;
            entry.CoinType = grant.CoinType;
            entry.Amount = grant.Amount;
            entry.Reason = grant.Reason;
            entry.Reference = grant.Reference;
            entry.GrantedBy = grantedBy ?? GrantedBy;
            entry.CreatedAt = DateTime.UtcNow;
            try
            {
                if (!await context.SaveChangesAsync().ConfigureAwait(false))
                {
                    gameContext.LoggerFactory.CreateLogger(typeof(MissionCoinGranter)).LogWarning("The coin grant {Reference} for {Login} wasn't saved.", grant.Reference, grant.LoginName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // The unique reference refuses a second grant which raced this one.
                gameContext.LoggerFactory.CreateLogger(typeof(MissionCoinGranter)).LogWarning(ex, "The coin grant {Reference} for {Login} wasn't saved.", grant.Reference, grant.LoginName);
                return false;
            }
        }

        var player = (await gameContext.GetPlayersAsync().ConfigureAwait(false))
            .FirstOrDefault(p => string.Equals(p.Account?.LoginName, grant.LoginName, StringComparison.OrdinalIgnoreCase));
        if (player is not null)
        {
            await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        }

        return true;
    }
}
