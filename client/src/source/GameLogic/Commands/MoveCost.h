#pragma once

// The zen the move window (M) shows for a map.
//
// An outlaw (PK level 2 and up, PVP_MURDERER1 and above) can't use the window: its rows stay locked, as on the
// desktop. Such a player moves with /move, and the server takes twice the map's price into a Gens battle zone (the
// window's strife maps) and 50 times the price anywhere else (owner, 26 Nov 2026). The window shows that amount to
// an outlaw, so the price it shows is the one the server takes.
namespace GameLogic::Commands
{
inline constexpr int OutlawMoveCostInBattleZone = 2;
inline constexpr int OutlawMoveCostElsewhere = 50;

// reqZen: the map's price; battleZone: a Gens battle zone (the window's strife mark); outlaw: PK level 2 and up.
[[nodiscard]] constexpr long long ShownMoveCost(int reqZen, bool battleZone, bool outlaw)
{
    if (!outlaw)
    {
        return reqZen;
    }

    return static_cast<long long>(reqZen) * (battleZone ? OutlawMoveCostInBattleZone : OutlawMoveCostElsewhere);
}
} // namespace GameLogic::Commands
