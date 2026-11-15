// <copyright file="GiftCodeEngineTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// Tests for HNMO's gift codes: codes without regard to case, the limits (uses, once per account or character, level,
/// resets, class), dates on the Sydney wall clock with daylight saving, the limit of wrong tries, references which make
/// a reward count once, the rewards waiting for room in the bag, batches of unique codes and the saved file.
/// </summary>
[TestFixture]
public class GiftCodeEngineTest
{
    private static readonly DateTime Now = Utc(2026, 11, 15, 5, 0);
    private static readonly GiftCodeRedeemer AnnKnight = new("ann", "c-ann-1", "AnnKnight", 50, 0, 4);
    private static readonly GiftCodeRedeemer AnnElf = new("ann", "c-ann-2", "AnnElf", 50, 0, 8);
    private static readonly GiftCodeRedeemer Bob = new("Bob", "c-bob", "Bobby", 50, 0, 0);

    /// <summary>
    /// A code is found without regard to case and spaces; the reward's coins become pending with a unique reference and
    /// the zen and items wait for the character; the log tells who used it.
    /// </summary>
    [Test]
    public void ACodeIsRedeemedWithoutRegardToCase()
    {
        var engine = Engine(out _, Code("WELCOME2026", reward: new GiftCodeReward { Coins = 100, Zen = 5000, Items = [new GiftCodeItem { Group = 14, Number = 13, Count = 2 }] }));
        var outcome = engine.Redeem("  welcome 2026 ", AnnKnight, Now, "game");
        Assert.That(outcome.Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(outcome.Entry!.Code, Is.EqualTo("WELCOME2026"));
        Assert.That(outcome.Coins!.Reference, Is.EqualTo("giftcode:w:a:ann"));
        Assert.That((outcome.Coins.Login, outcome.Coins.Amount, outcome.Coins.CoinType), Is.EqualTo(("ann", 100, CashShopCoinType.GoblinPoints)));
        Assert.That(engine.PendingCoins().Single().Reference, Is.EqualTo("giftcode:w:a:ann"));
        var owed = engine.Owed("c-ann-1").Single();
        Assert.That((owed.Reference, owed.Zen, owed.Items.Single().Count), Is.EqualTo(("giftcode:w:a:ann:goods", 5000, 2)));
        Assert.That(engine.LogOf("ANN", 5).Single().CharacterName, Is.EqualTo("AnnKnight"));
        Assert.That(engine.UsesOf("w"), Is.EqualTo(1));

        engine.CoinsGiven("giftcode:w:a:ann");
        Assert.That(engine.PendingCoins(), Is.Empty);
    }

    /// <summary>
    /// Once per account: no other character of the account can use it again, another account can. Once per character:
    /// each character once, with its own reference.
    /// </summary>
    [Test]
    public void OncePerAccountOrCharacter()
    {
        var engine = Engine(out _, Code("ACC"), Code("CHAR", id: "c", limit: GiftCodeLimit.OncePerCharacter));
        Assert.That(engine.Redeem("acc", AnnKnight, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("acc", AnnElf, Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedAccount));
        Assert.That(engine.Redeem("ACC", AnnKnight with { Login = "ANN" }, Now, "website").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedAccount), "The login name is compared without case.");
        Assert.That(engine.Redeem("acc", Bob, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));

        var first = engine.Redeem("char", AnnKnight, Now, "game");
        Assert.That(first.Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("char", AnnKnight, Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedCharacter));
        var second = engine.Redeem("char", AnnElf, Now, "game");
        Assert.That(second.Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(first.Entry!.Reference, Is.Not.EqualTo(second.Entry!.Reference));
        Assert.That(second.Entry.Reference, Is.EqualTo("giftcode:c:c:c-ann-2"));
    }

    /// <summary>
    /// The total number of uses, level, reset and class limits and a code that's off.
    /// </summary>
    [Test]
    public void LimitsAreChecked()
    {
        var limited = Code("TWO", id: "two");
        limited.MaxUses = 2;
        var rules = Code("RULES", id: "rules");
        rules.MinLevel = 40;
        rules.MaxLevel = 60;
        rules.MinResets = 1;
        rules.MaxResets = 3;
        rules.Classes = [TopRaceClass.DarkKnight];
        var off = Code("OFF", id: "off");
        off.Enabled = false;
        var engine = Engine(out _, limited, rules, off);

        Assert.That(engine.Redeem("two", AnnKnight, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("two", Bob, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("two", new GiftCodeRedeemer("cat", "c-cat", "Cat", 1, 0, 0), Now, "game").Result, Is.EqualTo(GiftCodeResult.UsedUp));

        GiftCodeResult Try(int level, int resets, int classNumber) => engine.Redeem("rules", new GiftCodeRedeemer("x" + level + resets + classNumber, "c", "X", level, resets, classNumber), Now, "game").Result;
        Assert.That(Try(39, 1, 4), Is.EqualTo(GiftCodeResult.LevelTooLow));
        Assert.That(Try(61, 1, 4), Is.EqualTo(GiftCodeResult.LevelTooHigh));
        Assert.That(Try(50, 0, 4), Is.EqualTo(GiftCodeResult.ResetsTooLow));
        Assert.That(Try(50, 4, 4), Is.EqualTo(GiftCodeResult.ResetsTooHigh));
        Assert.That(Try(50, 1, 0), Is.EqualTo(GiftCodeResult.WrongClass));
        Assert.That(Try(50, 1, 6), Is.EqualTo(GiftCodeResult.Ok), "A Blade Master (6) is a Dark Knight.");
        Assert.That(engine.Redeem("off", AnnKnight, Now, "game").Result, Is.EqualTo(GiftCodeResult.Disabled));

        rules.MaxResets = 0;
        rules.MinResets = 0;
        Assert.That(Try(50, 1, 5), Is.EqualTo(GiftCodeResult.ResetsTooHigh), "A maximum of 0 resets is for characters which never reset.");
    }

    /// <summary>
    /// Start and end are Sydney wall-clock times: a start in the skipped hour (clocks forward) works from the first valid
    /// minute, an end in the repeated hour (clocks back) is the first of the two.
    /// </summary>
    [Test]
    public void DatesFollowTheSydneyWallClockWithDaylightSaving()
    {
        var code = Code("DST");
        code.Start = "2026-10-04 02:30"; // doesn't exist: 02:00 -> 03:00 AEDT
        code.End = "2027-04-04 02:30"; // exists twice: 03:00 AEDT -> 02:00 AEST
        var engine = Engine(out _, code);
        Assert.That(engine.StartUtc(code), Is.EqualTo(Utc(2026, 10, 3, 16, 0)), "03:00 AEDT.");
        Assert.That(engine.EndUtc(code), Is.EqualTo(Utc(2027, 4, 3, 15, 30)), "The first 02:30 (still AEDT, +11).");
        Assert.That(engine.Redeem("dst", AnnKnight, Utc(2026, 10, 3, 15, 59), "game").Result, Is.EqualTo(GiftCodeResult.NotStarted));
        Assert.That(engine.Redeem("dst", Bob, Utc(2027, 4, 3, 15, 30), "game").Result, Is.EqualTo(GiftCodeResult.Ended));
        Assert.That(engine.Redeem("dst", AnnKnight, Utc(2026, 10, 3, 16, 0), "game").Result, Is.EqualTo(GiftCodeResult.Ok));

        var winter = Code("WINTER", id: "winter");
        winter.Start = "2026-07-01 09:00";
        Assert.That(new GiftCodeEngine(new GiftCodesConfiguration { FileName = string.Empty }, new GiftCodeStore()).StartUtc(winter), Is.EqualTo(Utc(2026, 6, 30, 23, 0)), "AEST is +10.");
    }

    /// <summary>
    /// Wrong codes are limited per account in a window; known codes don't count; when the window passes, the account
    /// may try again.
    /// </summary>
    [Test]
    public void WrongTriesAreLimitedPerAccount()
    {
        var engine = Engine(out _, Code("REAL"));
        for (var i = 0; i < 4; i++)
        {
            var wrong = engine.Redeem("nope" + i, AnnKnight, Now.AddMinutes(i), "game");
            Assert.That((wrong.Result, wrong.TriesLeft), Is.EqualTo((GiftCodeResult.Invalid, 4 - i)));
        }

        Assert.That(engine.Redeem("off-limits", AnnElf, Now.AddMinutes(5), "website").Result, Is.EqualTo(GiftCodeResult.TooManyTries), "The fifth wrong code of the account (any character).");
        var blocked = engine.Redeem("real", AnnKnight, Now.AddMinutes(6), "game");
        Assert.That(blocked.Result, Is.EqualTo(GiftCodeResult.TooManyTries), "Even a right code waits.");
        Assert.That(blocked.RetryAtUtc, Is.EqualTo(Now.AddMinutes(60)));
        Assert.That(engine.Redeem("real", Bob, Now.AddMinutes(6), "game").Result, Is.EqualTo(GiftCodeResult.Ok), "Another account isn't blocked.");
        Assert.That(engine.Redeem("real", AnnKnight, Now.AddMinutes(60), "game").Result, Is.EqualTo(GiftCodeResult.Ok), "The first wrong try left the window.");
    }

    /// <summary>
    /// A batch's codes are unique (also against the other codes); each can be used once, and the campaign's limit per
    /// account covers all its codes; the CSV lists who used which.
    /// </summary>
    [Test]
    public void BatchesAreUniqueAndSingleUse()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "XMASAAAA" };
        var codes = GiftCodeEngine.GenerateCodes("xmas", 500, 6, taken);
        Assert.That(codes, Has.Count.EqualTo(500));
        Assert.That(codes.Distinct().Count(), Is.EqualTo(500));
        Assert.That(codes, Has.All.Match("^XMAS[A-Z2-9]{6}$"));
        Assert.That(codes, Has.None.Contains("O").And.None.Contains("0").And.None.Contains("I"));
        Assert.That(taken, Has.Count.EqualTo(501));

        var batch = new GiftCodeDefinition { Id = "b", BatchCodes = codes.Take(3).ToList(), BatchPrefix = "XMAS", Reward = new GiftCodeReward { Coins = 5 } };
        var engine = Engine(out _, batch);
        Assert.That(engine.Redeem(codes[0].ToLowerInvariant(), AnnKnight, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem(codes[0], Bob, Now, "game").Result, Is.EqualTo(GiftCodeResult.BatchCodeUsed));
        Assert.That(engine.Redeem(codes[1], AnnElf, Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedAccount), "Once per account covers the whole batch.");
        Assert.That(engine.Redeem(codes[1], Bob, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        var csv = engine.BatchCsv(batch).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.That(csv[0], Is.EqualTo("code,used,account,character,used_at_sydney"));
        Assert.That(csv[1], Is.EqualTo($"{codes[0]},yes,ann,AnnKnight,2026-11-15 16:00"));
        Assert.That(csv[3], Is.EqualTo($"{codes[2]},no,,,"));
    }

    /// <summary>
    /// The state is saved in the file and read back (a restart keeps uses, claims and waiting rewards), and the rewards
    /// taken and returned keep their references once.
    /// </summary>
    [Test]
    public void TheStateOutlastsARestart()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-codes-{Guid.NewGuid():N}.json");
        try
        {
            var store = new GiftCodeStore(file);
            store.Data.Codes.Add(Code("SAVE", reward: new GiftCodeReward { Zen = 10 }));
            var engine = new GiftCodeEngine(new GiftCodesConfiguration(), store);
            Assert.That(engine.Redeem("save", AnnKnight, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
            var taken = engine.TakeOwed("c-ann-1");
            engine.ReturnOwed("c-ann-1", taken);
            engine.ReturnOwed("c-ann-1", taken);
            Assert.That(engine.Owed("c-ann-1"), Has.Count.EqualTo(1), "A returned reward is kept once.");
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var again = new GiftCodeEngine(new GiftCodesConfiguration(), new GiftCodeStore(file));
            Assert.That(again.Redeem("SAVE", AnnElf, Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedAccount));
            Assert.That(again.Owed("c-ann-1").Single().Zen, Is.EqualTo(10));
            Assert.That(JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("Codes").GetArrayLength(), Is.EqualTo(1));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The website lists public single codes which are on, not used up and not over; the client state is plain ASCII.
    /// </summary>
    [Test]
    public void PublicCodesAndAsciiState()
    {
        var shown = Code("PUBLIC", id: "p");
        shown.Public = true;
        shown.Note = "Chào mừng";
        var over = Code("OVER", id: "o");
        over.Public = true;
        over.End = "2026-11-01 00:00";
        var hidden = Code("HIDDEN", id: "h");
        var engine = Engine(out _, shown, over, hidden);
        Assert.That(engine.PublicCodes(Now).Select(c => c.Code), Is.EqualTo(new[] { "PUBLIC" }));
        var json = GiftCodeStateWriter.WritePublic(engine, Now);
        Assert.That(json, Does.Contain("\"code\":\"PUBLIC\"").And.Not.Contain("HIDDEN"));
        Assert.That(json.All(c => c < 128), Is.True);

        var outcome = engine.Redeem("public", AnnKnight, Now, "game");
        var state = GiftCodeStateWriter.WriteState(engine, "ann", "c-ann-1", outcome, "redeem", null);
        using var document = JsonDocument.Parse(state);
        Assert.That(document.RootElement.GetProperty("event").GetProperty("result").GetString(), Is.EqualTo("Ok"));
        Assert.That(document.RootElement.GetProperty("recent")[0].GetProperty("code").GetString(), Is.EqualTo("PUBLIC"));
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static GiftCodeDefinition Code(string code, string? id = null, GiftCodeLimit limit = GiftCodeLimit.OncePerAccount, GiftCodeReward? reward = null) => new()
    {
        Id = id ?? code[..1].ToLowerInvariant(),
        Code = code,
        Limit = limit,
        Reward = reward ?? new GiftCodeReward { Coins = 10 },
    };

    private static GiftCodeEngine Engine(out GiftCodeStore store, params GiftCodeDefinition[] codes)
    {
        store = new GiftCodeStore();
        store.Data.Codes.AddRange(codes);
        return new GiftCodeEngine(new GiftCodesConfiguration { FileName = string.Empty }, store);
    }
}
