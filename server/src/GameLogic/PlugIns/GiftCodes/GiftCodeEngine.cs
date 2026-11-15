// <copyright file="GiftCodeEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// The result of entering a gift code.
/// </summary>
public enum GiftCodeResult
{
    /// <summary>The reward was given (or waits for room in the bag).</summary>
    Ok,

    /// <summary>No such code.</summary>
    Invalid,

    /// <summary>The account entered too many wrong codes lately.</summary>
    TooManyTries,

    /// <summary>The code is switched off.</summary>
    Disabled,

    /// <summary>The code's start is later.</summary>
    NotStarted,

    /// <summary>The code's end has passed.</summary>
    Ended,

    /// <summary>The code was used as often as it may be.</summary>
    UsedUp,

    /// <summary>The account used the code already (or a code of the same batch).</summary>
    AlreadyUsedAccount,

    /// <summary>The character used the code already (or a code of the same batch).</summary>
    AlreadyUsedCharacter,

    /// <summary>This code of a batch was used already.</summary>
    BatchCodeUsed,

    /// <summary>The character's level is too low.</summary>
    LevelTooLow,

    /// <summary>The character's level is too high.</summary>
    LevelTooHigh,

    /// <summary>The character has too few resets.</summary>
    ResetsTooLow,

    /// <summary>The character has too many resets.</summary>
    ResetsTooHigh,

    /// <summary>The character's class can't use it.</summary>
    WrongClass,
}

/// <summary>
/// Who enters a code: the account and the character which gets the zen and items.
/// </summary>
/// <param name="Login">The account's login name.</param>
/// <param name="CharacterId">The character's id.</param>
/// <param name="CharacterName">The character's name.</param>
/// <param name="Level">The character's level.</param>
/// <param name="Resets">The character's resets.</param>
/// <param name="ClassNumber">The number of the character's class.</param>
public sealed record GiftCodeRedeemer(string Login, string CharacterId, string CharacterName, int Level, int Resets, int ClassNumber);

/// <summary>
/// The outcome of entering a code.
/// </summary>
/// <param name="Result">The result.</param>
/// <param name="Code">The code, when one was found.</param>
/// <param name="Entry">The log entry of a redemption.</param>
/// <param name="Coins">The coins to give now (also kept as pending until they're given).</param>
/// <param name="Owed">The zen and items which wait for the character.</param>
/// <param name="TriesLeft">How many wrong codes the account may still enter in the window.</param>
/// <param name="RetryAtUtc">When the account may try again after <see cref="GiftCodeResult.TooManyTries"/>.</param>
public sealed record GiftCodeOutcome(GiftCodeResult Result, GiftCodeDefinition? Code = null, GiftCodeLogEntry? Entry = null, GiftCodePendingCoins? Coins = null, GiftCodeOwedReward? Owed = null, int TriesLeft = 0, DateTime? RetryAtUtc = null);

/// <summary>
/// HNMO's gift codes, without the game: finding a code (without regard to case), its rules (dates on the Sydney wall
/// clock, uses, once per account or character, level, resets and class), the limit of wrong tries per account, the
/// references which make a reward count once, the rewards which wait for room in the bag, the log and batches of
/// random single-use codes. Every access holds the store's lock.
/// </summary>
public sealed class GiftCodeEngine
{
    /// <summary>The name in the coins history's "granted by" column.</summary>
    public const string GrantedBy = "HNMO gift codes";

    /// <summary>The longest code.</summary>
    public const int MaximumCodeLength = 32;

    /// <summary>The letters of random codes (no 0/O, 1/I/L).</summary>
    public const string RandomAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private static readonly Regex CodePattern = new("^[A-Z0-9_-]{3,32}$", RegexOptions.Compiled);

    private Dictionary<string, (GiftCodeDefinition Code, bool Batch)>? _index;
    private int _indexVersion = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="GiftCodeEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public GiftCodeEngine(GiftCodesConfiguration configuration, GiftCodeStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Clock = new MissionClock(configuration.TimeZone, 0);
    }

    /// <summary>Gets the configuration.</summary>
    public GiftCodesConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public GiftCodeStore Store { get; }

    /// <summary>Gets the clock (its time zone).</summary>
    public MissionClock Clock { get; }

    /// <summary>
    /// Normalizes an entered code: trimmed, without spaces, upper case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The code.</returns>
    public static string Normalize(string? text) =>
        string.Concat((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    /// <summary>
    /// Checks the form of a code: 3 to 32 letters A-Z, digits, '-' or '_'.
    /// </summary>
    /// <param name="code">The normalized code.</param>
    /// <returns>Whether it's valid.</returns>
    public static bool IsValidCode(string code) => CodePattern.IsMatch(code);

    /// <summary>
    /// Gets the reference of a redemption: one per code and account (once per account) or character (once per character).
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="who">Who enters it.</param>
    /// <returns>The reference.</returns>
    public static string Reference(GiftCodeDefinition code, GiftCodeRedeemer who) =>
        code.Limit == GiftCodeLimit.OncePerCharacter
            ? $"giftcode:{code.Id}:c:{who.CharacterId}"
            : $"giftcode:{code.Id}:a:{who.Login.ToLowerInvariant()}";

    /// <summary>
    /// Makes random codes, unique among themselves and the given ones.
    /// </summary>
    /// <param name="prefix">The prefix (normalized; may be empty).</param>
    /// <param name="count">How many.</param>
    /// <param name="length">The number of random letters.</param>
    /// <param name="taken">Codes which exist (normalized).</param>
    /// <returns>The codes.</returns>
    public static List<string> GenerateCodes(string prefix, int count, int length, ISet<string> taken)
    {
        prefix = Normalize(prefix);
        length = Math.Clamp(length, 4, MaximumCodeLength - prefix.Length);
        var result = new List<string>(count);
        var builder = new StringBuilder(prefix.Length + length);
        var attempts = 0;
        while (result.Count < count)
        {
            if (++attempts > (count * 100) + 1000)
            {
                throw new InvalidOperationException("Not enough unique codes could be made; use more letters.");
            }

            builder.Clear().Append(prefix);
            for (var i = 0; i < length; i++)
            {
                builder.Append(RandomAlphabet[RandomNumberGenerator.GetInt32(RandomAlphabet.Length)]);
            }

            var code = builder.ToString();
            if (taken.Add(code))
            {
                result.Add(code);
            }
        }

        return result;
    }

    /// <summary>
    /// Makes a new id for a code.
    /// </summary>
    /// <returns>The id.</returns>
    public static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();

    /// <summary>
    /// Gets the start of a code in UTC.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The moment, or <c>null</c> without a start.</returns>
    public DateTime? StartUtc(GiftCodeDefinition code) =>
        TopRaceEngine.ParseLocal(code.Start) is { } local ? TopRaceEngine.LocalToUtc(local, this.Clock.TimeZone) : null;

    /// <summary>
    /// Gets the end of a code in UTC.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The moment, or <c>null</c> without an end.</returns>
    public DateTime? EndUtc(GiftCodeDefinition code) =>
        TopRaceEngine.ParseLocal(code.End) is { } local ? TopRaceEngine.LocalToUtc(local, this.Clock.TimeZone) : null;

    /// <summary>
    /// Gets how often a code was used.
    /// </summary>
    /// <param name="codeId">The code's id.</param>
    /// <returns>The uses.</returns>
    public int UsesOf(string codeId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Uses.GetValueOrDefault(codeId);
        }
    }

    /// <summary>
    /// Gets every code of the store (normalized): the single codes and the batch codes.
    /// </summary>
    /// <returns>The codes.</returns>
    public HashSet<string> AllCodes()
    {
        lock (this.Store.Lock)
        {
            return new HashSet<string>(this.IndexLocked().Keys, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Finds a code.
    /// </summary>
    /// <param name="text">The entered text.</param>
    /// <returns>The code, and whether it's one of a batch; <c>null</c> when there's none.</returns>
    public (GiftCodeDefinition Code, bool Batch)? Find(string? text)
    {
        lock (this.Store.Lock)
        {
            return this.IndexLocked().TryGetValue(Normalize(text), out var found) ? found : null;
        }
    }

    /// <summary>
    /// Checks a code's rules for a character, without using it.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="who">Who enters it.</param>
    /// <param name="nowUtc">The moment.</param>
    /// <returns>The result; <see cref="GiftCodeResult.Ok"/> when it can be used.</returns>
    public GiftCodeResult Check(GiftCodeDefinition code, GiftCodeRedeemer who, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            return this.CheckLocked(code, null, who, nowUtc);
        }
    }

    /// <summary>
    /// Enters a code: checks the account's wrong tries and the code's rules; on success counts the use, notes who used
    /// it, logs it, keeps the coins as pending (until <see cref="CoinsGiven"/>) and the zen and items as owed to the
    /// character.
    /// </summary>
    /// <param name="text">The entered text.</param>
    /// <param name="who">Who enters it.</param>
    /// <param name="nowUtc">The moment.</param>
    /// <param name="source">Where it was entered: "game" or "website".</param>
    /// <returns>The outcome.</returns>
    public GiftCodeOutcome Redeem(string? text, GiftCodeRedeemer who, DateTime nowUtc, string source)
    {
        var normalized = Normalize(text);
        var login = who.Login.ToLowerInvariant();
        lock (this.Store.Lock)
        {
            var data = this.Store.Data;
            var window = TimeSpan.FromMinutes(Math.Max(1, this.Configuration.WrongTriesWindowMinutes));
            var limit = Math.Max(1, this.Configuration.WrongTriesLimit);
            var tries = data.WrongTries.GetValueOrDefault(login) ?? new List<DateTime>();
            if (tries.RemoveAll(t => nowUtc - t >= window) > 0)
            {
                this.Store.Changed();
            }

            if (tries.Count >= limit)
            {
                return new GiftCodeOutcome(GiftCodeResult.TooManyTries, RetryAtUtc: tries.Min() + window);
            }

            if (normalized.Length > MaximumCodeLength || !this.IndexLocked().TryGetValue(normalized, out var found))
            {
                tries.Add(nowUtc);
                data.WrongTries[login] = tries;
                this.Store.Changed();
                return tries.Count >= limit
                    ? new GiftCodeOutcome(GiftCodeResult.TooManyTries, RetryAtUtc: tries.Min() + window)
                    : new GiftCodeOutcome(GiftCodeResult.Invalid, TriesLeft: limit - tries.Count);
            }

            var code = found.Code;
            var result = this.CheckLocked(code, found.Batch ? normalized : null, who, nowUtc);
            if (result != GiftCodeResult.Ok)
            {
                return new GiftCodeOutcome(result, code, TriesLeft: limit - tries.Count);
            }

            var reference = Reference(code, who);
            data.Uses[code.Id] = data.Uses.GetValueOrDefault(code.Id) + 1;
            if (!data.Claims.TryGetValue(code.Id, out var claims))
            {
                claims = new HashSet<string>(StringComparer.Ordinal);
                data.Claims[code.Id] = claims;
            }

            claims.Add("a:" + login);
            claims.Add("c:" + who.CharacterId);
            if (found.Batch)
            {
                data.UsedBatchCodes[normalized] = reference;
            }

            var entry = new GiftCodeLogEntry
            {
                AtUtc = nowUtc,
                CodeId = code.Id,
                Code = normalized,
                Login = who.Login,
                CharacterId = who.CharacterId,
                CharacterName = who.CharacterName,
                Source = source,
                Reference = reference,
            };
            data.Log.Insert(0, entry);
            var kept = Math.Max(10, this.Configuration.LogKept);
            if (data.Log.Count > kept)
            {
                data.Log.RemoveRange(kept, data.Log.Count - kept);
            }

            GiftCodePendingCoins? coins = null;
            if (code.Reward.Coins > 0 && Enum.IsDefined(code.Reward.CoinType))
            {
                coins = new GiftCodePendingCoins
                {
                    Reference = reference,
                    Login = who.Login,
                    CoinType = code.Reward.CoinType,
                    Amount = code.Reward.Coins,
                    Reason = string.Format(CultureInfo.InvariantCulture, "Gift code {0}", normalized),
                };
                data.PendingCoins.Add(coins);
            }

            GiftCodeOwedReward? owed = null;
            if (code.Reward.HasGoods)
            {
                owed = new GiftCodeOwedReward
                {
                    Reference = reference + ":goods",
                    CodeId = code.Id,
                    Code = normalized,
                    Zen = Math.Max(0, code.Reward.Zen),
                    Items = code.Reward.Items.Where(i => i.Count > 0).Select(Copy).ToList(),
                };
                if (!data.Owed.TryGetValue(who.CharacterId, out var list))
                {
                    list = new List<GiftCodeOwedReward>();
                    data.Owed[who.CharacterId] = list;
                }

                if (list.All(o => o.Reference != owed.Reference))
                {
                    list.Add(owed);
                }
            }

            this.Store.Changed();
            return new GiftCodeOutcome(GiftCodeResult.Ok, code, entry, coins, owed, limit - tries.Count);
        }
    }

    /// <summary>
    /// Notes that pending coins were given (or were given before, by their reference).
    /// </summary>
    /// <param name="reference">The reference.</param>
    public void CoinsGiven(string reference)
    {
        lock (this.Store.Lock)
        {
            if (this.Store.Data.PendingCoins.RemoveAll(c => c.Reference == reference) > 0)
            {
                this.Store.Changed();
            }
        }
    }

    /// <summary>
    /// Gets the coins which weren't given yet.
    /// </summary>
    /// <returns>A copy of the list.</returns>
    public List<GiftCodePendingCoins> PendingCoins()
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.PendingCoins.ToList();
        }
    }

    /// <summary>
    /// Gets the zen and items waiting for a character.
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <returns>A copy of the list.</returns>
    public List<GiftCodeOwedReward> Owed(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Owed.TryGetValue(characterId, out var list) ? list.ToList() : new List<GiftCodeOwedReward>();
        }
    }

    /// <summary>
    /// Takes the zen and items waiting for a character; what isn't given goes back with <see cref="ReturnOwed"/>.
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <returns>The rewards.</returns>
    public List<GiftCodeOwedReward> TakeOwed(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.Remove(characterId, out var list))
            {
                return new List<GiftCodeOwedReward>();
            }

            this.Store.Changed();
            return list;
        }
    }

    /// <summary>
    /// Puts back rewards which weren't given (and keeps rewards which arrived meanwhile).
    /// </summary>
    /// <param name="characterId">The character's id.</param>
    /// <param name="rewards">The rewards.</param>
    public void ReturnOwed(string characterId, IReadOnlyCollection<GiftCodeOwedReward> rewards)
    {
        if (rewards.Count == 0)
        {
            return;
        }

        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.TryGetValue(characterId, out var list))
            {
                list = new List<GiftCodeOwedReward>();
                this.Store.Data.Owed[characterId] = list;
            }

            foreach (var reward in rewards.Reverse())
            {
                if (list.All(o => o.Reference != reward.Reference))
                {
                    list.Insert(0, reward);
                }
            }

            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets the redemptions of an account or a character, newest first.
    /// </summary>
    /// <param name="login">The login name.</param>
    /// <param name="count">How many.</param>
    /// <returns>The entries.</returns>
    public List<GiftCodeLogEntry> LogOf(string login, int count)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Log.Where(e => string.Equals(e.Login, login, StringComparison.OrdinalIgnoreCase)).Take(count).ToList();
        }
    }

    /// <summary>
    /// Gets the codes the website shows: public single codes which are on, not used up and not over.
    /// </summary>
    /// <param name="nowUtc">The moment.</param>
    /// <returns>The codes.</returns>
    public List<GiftCodeDefinition> PublicCodes(DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Codes
                .Where(c => c.Public && c.Enabled && !c.IsBatch && c.Code.Length > 0)
                .Where(c => this.EndUtc(c) is not { } end || end > nowUtc)
                .Where(c => c.MaxUses <= 0 || this.Store.Data.Uses.GetValueOrDefault(c.Id) < c.MaxUses)
                .OrderBy(c => this.StartUtc(c) ?? DateTime.MinValue)
                .ToList();
        }
    }

    /// <summary>
    /// Writes the codes of a batch as CSV: the code, whether it was used, and by whom and when.
    /// </summary>
    /// <param name="code">The batch.</param>
    /// <returns>The CSV text.</returns>
    public string BatchCsv(GiftCodeDefinition code)
    {
        lock (this.Store.Lock)
        {
            var byReference = this.Store.Data.Log.Where(e => e.CodeId == code.Id).GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var builder = new StringBuilder("code,used,account,character,used_at_sydney\r\n");
            var codes = code.IsBatch ? code.BatchCodes : new List<string> { Normalize(code.Code) };
            foreach (var text in codes)
            {
                var used = code.IsBatch ? this.Store.Data.UsedBatchCodes.ContainsKey(text) : this.Store.Data.Uses.GetValueOrDefault(code.Id) > 0;
                byReference.TryGetValue(text, out var entry);
                var at = entry is null ? string.Empty : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entry.AtUtc, DateTimeKind.Utc), this.Clock.TimeZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                builder.Append(Csv(text)).Append(',').Append(used ? "yes" : "no").Append(',')
                    .Append(Csv(code.IsBatch ? entry?.Login : null)).Append(',').Append(Csv(code.IsBatch ? entry?.CharacterName : null)).Append(',').Append(code.IsBatch ? at : string.Empty).Append("\r\n");
            }

            return builder.ToString();
        }
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // A leading =, +, - or @ would be a formula in a spreadsheet.
        var text = value.Length > 0 && "=+-@".Contains(value[0], StringComparison.Ordinal) ? "'" + value : value;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
    }

    private static GiftCodeItem Copy(GiftCodeItem item) => new()
    {
        Group = item.Group,
        Number = item.Number,
        Level = item.Level,
        Count = item.Count,
        Durability = item.Durability,
        Skill = item.Skill,
        Luck = item.Luck,
        OptionLevel = item.OptionLevel,
        ExcellentOptions = item.ExcellentOptions,
    };

    private GiftCodeResult CheckLocked(GiftCodeDefinition code, string? batchCode, GiftCodeRedeemer who, DateTime nowUtc)
    {
        var data = this.Store.Data;
        if (!code.Enabled)
        {
            return GiftCodeResult.Disabled;
        }

        if (this.StartUtc(code) is { } start && nowUtc < start)
        {
            return GiftCodeResult.NotStarted;
        }

        if (this.EndUtc(code) is { } end && nowUtc >= end)
        {
            return GiftCodeResult.Ended;
        }

        if (batchCode is not null && data.UsedBatchCodes.ContainsKey(batchCode))
        {
            return GiftCodeResult.BatchCodeUsed;
        }

        if (code.MaxUses > 0 && data.Uses.GetValueOrDefault(code.Id) >= code.MaxUses)
        {
            return GiftCodeResult.UsedUp;
        }

        if (data.Claims.TryGetValue(code.Id, out var claims))
        {
            if (code.Limit == GiftCodeLimit.OncePerAccount && claims.Contains("a:" + who.Login.ToLowerInvariant()))
            {
                return GiftCodeResult.AlreadyUsedAccount;
            }

            if (code.Limit == GiftCodeLimit.OncePerCharacter && claims.Contains("c:" + who.CharacterId))
            {
                return GiftCodeResult.AlreadyUsedCharacter;
            }
        }

        if (code.MinLevel > 0 && who.Level < code.MinLevel)
        {
            return GiftCodeResult.LevelTooLow;
        }

        if (code.MaxLevel > 0 && who.Level > code.MaxLevel)
        {
            return GiftCodeResult.LevelTooHigh;
        }

        if (code.MinResets > 0 && who.Resets < code.MinResets)
        {
            return GiftCodeResult.ResetsTooLow;
        }

        if (code.MaxResets >= 0 && who.Resets > code.MaxResets)
        {
            return GiftCodeResult.ResetsTooHigh;
        }

        if (code.Classes.Count > 0 && !code.Classes.Contains((TopRaceClass)TopRaceEngine.ClassOf(who.ClassNumber)))
        {
            return GiftCodeResult.WrongClass;
        }

        return GiftCodeResult.Ok;
    }

    private Dictionary<string, (GiftCodeDefinition Code, bool Batch)> IndexLocked()
    {
        if (this._index is { } index && this._indexVersion == this.Store.CodesVersion)
        {
            return index;
        }

        index = new Dictionary<string, (GiftCodeDefinition, bool)>(StringComparer.Ordinal);
        foreach (var code in this.Store.Data.Codes)
        {
            if (!code.IsBatch && Normalize(code.Code) is { Length: > 0 } single)
            {
                index.TryAdd(single, (code, false));
            }

            foreach (var batch in code.BatchCodes)
            {
                index.TryAdd(Normalize(batch), (code, true));
            }
        }

        this._index = index;
        this._indexVersion = this.Store.CodesVersion;
        return index;
    }
}
