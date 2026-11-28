// Coin rewards in the game's windows (missions, guild levels, community, the new-player guide, top races, lucky number):
// a season whose host turned the cash shop off gives no free coins (owner 2026-11-28, classic play), so its windows
// don't promise them either. The page knows once the world is in: without the cash shop's script version the body
// gets hnmo-no-cash-shop (main.ts, ui/cashShop.ts). Before that, coins show.

/** Whether this season's windows show coin rewards (its cash shop is on). */
export function coinRewardsShown(): boolean {
  return typeof document === 'undefined' || !document.body.classList.contains('hnmo-no-cash-shop');
}
