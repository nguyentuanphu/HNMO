// <copyright file="MuRummyEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Represents a single Mu Rummy card (color 1..3, rank 1..8).</summary>
/// <param name="Color">Card color (1 = Red, 2 = Blue, 3 = Yellow).</param>
/// <param name="Number">Card face number (1..8).</param>
public readonly record struct MuRummyCard(byte Color, byte Number);

/// <summary>Persisted Mu Rummy mini-game session state for one character.</summary>
public sealed class MuRummyGameState
{
    /// <summary>Gets or sets remaining unopened deck cards.</summary>
    public List<MuRummyCard> Deck { get; set; } = [];

    /// <summary>Gets or sets current hand cards (up to 5).</summary>
    public List<MuRummyCard> Hand { get; set; } = [];

    /// <summary>Gets or sets accumulated score.</summary>
    public int Score { get; set; }
}

/// <summary>
/// Pure helper logic for Mu Rummy deck shuffling, 3-card meld scoring, and tier 1/2 reward resolution
/// matching the Season 6 server suite (<c>IGC_MuRummy.ini</c> &amp; reward bags).
/// </summary>
public static class MuRummyEngine
{
    /// <summary>Builds and shuffles a full 24-card deck (3 colors x ranks 1..8).</summary>
    /// <param name="shuffleNextInt">Shuffle delegate taking inclusive exclusive bound <c>maxExclusive</c>.</param>
    /// <returns>New <see cref="MuRummyGameState"/> with 5 cards dealt to hand.</returns>
    public static MuRummyGameState CreateNewGame(Func<int, int> shuffleNextInt)
    {
        ArgumentNullException.ThrowIfNull(shuffleNextInt);
        var cards = new List<MuRummyCard>(24);
        for (byte color = 1; color <= 3; color++)
        {
            for (byte num = 1; num <= 8; num++)
            {
                cards.Add(new MuRummyCard(color, num));
            }
        }

        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = shuffleNextInt(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        var state = new MuRummyGameState { Deck = cards };
        RefillHand(state);
        return state;
    }

    /// <summary>Tops up hand from deck up to 5 cards.</summary>
    /// <param name="state">Game state to mutate.</param>
    public static void RefillHand(MuRummyGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        while (state.Hand.Count < 5 && state.Deck.Count > 0)
        {
            state.Hand.Add(state.Deck[0]);
            state.Deck.RemoveAt(0);
        }
    }

    /// <summary>Evaluates a 3-card meld. Returns 0 when invalid.</summary>
    /// <param name="a">First card.</param>
    /// <param name="b">Second card.</param>
    /// <param name="c">Third card.</param>
    /// <returns>Meld points (same-color run 10..60 + 20 bonus, three-of-a-kind 20..90, mixed run 10..60, or 0).</returns>
    public static int EvaluateMeldScore(MuRummyCard a, MuRummyCard b, MuRummyCard c)
    {
        var sorted = new[] { a, b, c }.OrderBy(x => x.Number).ToArray();
        var sameNumber = sorted[0].Number == sorted[1].Number && sorted[1].Number == sorted[2].Number;
        var distinctColors = new[] { sorted[0].Color, sorted[1].Color, sorted[2].Color }.Distinct().Count() == 3;
        if (sameNumber && distinctColors)
        {
            return (sorted[0].Number * 10) + 10;
        }

        var sequential = sorted[1].Number == sorted[0].Number + 1 && sorted[2].Number == sorted[1].Number + 1;
        if (!sequential)
        {
            return 0;
        }

        var sameColor = sorted[0].Color == sorted[1].Color && sorted[1].Color == sorted[2].Color;
        var baseScore = sorted[0].Number * 10;
        return sameColor ? baseScore + 30 : baseScore;
    }

    /// <summary>Attempts to match 3 distinct 0-based hand indices into a scoring meld.</summary>
    /// <param name="state">Active game state.</param>
    /// <param name="idx0">First hand index.</param>
    /// <param name="idx1">Second hand index.</param>
    /// <param name="idx2">Third hand index.</param>
    /// <returns>Points awarded (0 if indices invalid or not a meld).</returns>
    public static int TryPlayMeld(MuRummyGameState state, int idx0, int idx1, int idx2)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (idx0 == idx1 || idx0 == idx2 || idx1 == idx2)
        {
            return 0;
        }

        if ((uint)idx0 >= (uint)state.Hand.Count
            || (uint)idx1 >= (uint)state.Hand.Count
            || (uint)idx2 >= (uint)state.Hand.Count)
        {
            return 0;
        }

        var pts = EvaluateMeldScore(state.Hand[idx0], state.Hand[idx1], state.Hand[idx2]);
        if (pts <= 0)
        {
            return 0;
        }

        foreach (var removeIdx in new[] { idx0, idx1, idx2 }.OrderByDescending(x => x))
        {
            state.Hand.RemoveAt(removeIdx);
        }

        state.Score += pts;
        RefillHand(state);
        return pts;
    }

    /// <summary>Resolves the reward tier item matching final <paramref name="score"/>.</summary>
    /// <param name="score">Final score.</param>
    /// <param name="firstScore">First prize threshold (default 250).</param>
    /// <param name="secondScore">Second prize threshold (default 400).</param>
    /// <param name="randomFraction">Uniform random in [0, 1).</param>
    /// <returns>Owed reward specification.</returns>
    public static S6EventsOwedReward ResolveReward(int score, int firstScore, int secondScore, double randomFraction)
    {
        var clamped = Math.Clamp(randomFraction, 0.0, 0.999999999);
        if (score >= Math.Max(firstScore + 1, secondScore))
        {
            IReadOnlyList<S6ExchangeRewardEntry> tier2 =
            [
                new() { Weight = 2500, NameEn = "Bundle of Jewel of Soul +2", NameVi = "Cụm Ngọc Tâm Linh +2", ItemGroup = 12, ItemNumber = 31, ItemLevel = 2, ItemCount = 1 },
                new() { Weight = 2000, NameEn = "Bundle of Jewel of Creation +2", NameVi = "Cụm Ngọc Sáng Tạo +2", ItemGroup = 12, ItemNumber = 137, ItemLevel = 2, ItemCount = 1 },
                new() { Weight = 1500, NameEn = "Bundle of Jewel of Life +2", NameVi = "Cụm Ngọc Sinh Mệnh +2", ItemGroup = 12, ItemNumber = 136, ItemLevel = 2, ItemCount = 1 },
                new() { Weight = 1250, NameEn = "Bundle of Jewel of Bless +2", NameVi = "Cụm Ngọc Ước Nguyện +2", ItemGroup = 12, ItemNumber = 30, ItemLevel = 2, ItemCount = 1 },
                new() { Weight = 1000, NameEn = "Horn of Fenrir", NameVi = "Sói Tinh Fenrir", ItemGroup = 13, ItemNumber = 37, ItemLevel = 0, ItemCount = 1 },
                new() { Weight = 250, NameEn = "Golden Panda Pet", NameVi = "Thú Cưng Gấu Trúc", ItemGroup = 13, ItemNumber = 80, ItemLevel = 0, ItemCount = 1 },
            ];
            var picked = S6EventsEngine.PickWeightedReward(tier2, clamped)!;
            return ToReward(picked);
        }

        if (score >= firstScore)
        {
            IReadOnlyList<S6ExchangeRewardEntry> tier1 =
            [
                new() { Weight = 2000, NameEn = "Jewel of Chaos", NameVi = "Ngọc Hỗn Nguyên", ItemGroup = 12, ItemNumber = 15, ItemLevel = 0, ItemCount = 1 },
                new() { Weight = 1300, NameEn = "Jewel of Soul", NameVi = "Ngọc Tâm Linh", ItemGroup = 14, ItemNumber = 14, ItemLevel = 0, ItemCount = 1 },
                new() { Weight = 900, NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", ItemGroup = 14, ItemNumber = 13, ItemLevel = 0, ItemCount = 1 },
                new() { Weight = 900, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh", ItemGroup = 14, ItemNumber = 16, ItemLevel = 0, ItemCount = 1 },
                new() { Weight = 1500, NameEn = "Lost Map +4", NameVi = "Bản Đồ Mất Tích +4", ItemGroup = 14, ItemNumber = 28, ItemLevel = 4, ItemCount = 1 },
            ];
            var picked = S6EventsEngine.PickWeightedReward(tier1, clamped)!;
            return ToReward(picked);
        }

        return new S6EventsOwedReward
        {
            Zen = 500_000,
            ItemGroup = -1,
            ReasonEn = "Mu Rummy Consolation (500,000 Zen)",
            ReasonVi = "Khuyến khích Mu Rummy (500.000 Zen)",
        };
    }

    private static S6EventsOwedReward ToReward(S6ExchangeRewardEntry picked) => new()
    {
        Zen = picked.Zen,
        ItemGroup = picked.ItemGroup,
        ItemNumber = picked.ItemNumber,
        ItemLevel = picked.ItemLevel,
        ItemCount = picked.ItemCount,
        ReasonEn = picked.NameEn,
        ReasonVi = picked.NameVi,
    };
}
