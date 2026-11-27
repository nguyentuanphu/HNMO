// Tests of the zen the move window (M) shows: the map's price, or, to an outlaw, what /move takes from one.

#include "stdafx.h"

#include "doctest.h"

#include "GameLogic/Commands/MoveCost.h"

using GameLogic::Commands::ShownMoveCost;

TEST_CASE("A player who isn't an outlaw sees the map's price [gamelogic][move]")
{
    CHECK(ShownMoveCost(15'000, false, false) == 15'000);
    CHECK(ShownMoveCost(15'000, true, false) == 15'000);
    CHECK(ShownMoveCost(0, false, false) == 0);
}

TEST_CASE("An outlaw sees what /move takes: twice into a Gens battle zone, 50 times elsewhere [gamelogic][move]")
{
    // Vulcanus (15,000) is a Gens battle zone; Lorencia (2,000 here) is not.
    CHECK(ShownMoveCost(15'000, true, true) == 30'000);
    CHECK(ShownMoveCost(2'000, false, true) == 100'000);
    CHECK(ShownMoveCost(0, false, true) == 0);
}

TEST_CASE("An outlaw's price above what an int holds is shown in full [gamelogic][move]")
{
    CHECK(ShownMoveCost(100'000'000, false, true) == 5'000'000'000LL);
}
