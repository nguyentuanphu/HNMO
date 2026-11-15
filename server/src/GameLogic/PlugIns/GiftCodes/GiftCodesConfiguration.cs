// <copyright file="GiftCodesConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// Who can use a gift code once.
/// </summary>
public enum GiftCodeLimit
{
    /// <summary>Once per account (any of its characters).</summary>
    OncePerAccount,

    /// <summary>Once per character.</summary>
    OncePerCharacter,
}

/// <summary>
/// An item of a gift code's reward.
/// </summary>
public class GiftCodeItem
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; }

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>Gets or sets how many of the item are given (separate pieces).</summary>
    [Range(1, 50)]
    public int Count { get; set; } = 1;

    /// <summary>Gets or sets the durability of a piece; for stackable items (potions, arrows) the stack size. 0 is full.</summary>
    [Range(0, 255)]
    public byte Durability { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has its skill (when it has one).</summary>
    public bool Skill { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has luck (when it can).</summary>
    public bool Luck { get; set; }

    /// <summary>Gets or sets the level of the item's additional option (0-4, when it can have one).</summary>
    [Range(0, 7)]
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets the excellent options as bits (1-63; 0 is none).</summary>
    [Range(0, 63)]
    public byte ExcellentOptions { get; set; }
}

/// <summary>
/// The reward of a gift code: coins to the account, zen and items to the character.
/// </summary>
public class GiftCodeReward
{
    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the zen.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<GiftCodeItem> Items { get; set; } = new();

    /// <summary>Gets a value indicating whether the reward gives nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this.Coins <= 0 && !this.HasGoods;

    /// <summary>Gets a value indicating whether the reward has zen or items (which go to a character's bag).</summary>
    [JsonIgnore]
    public bool HasGoods => this.Zen > 0 || this.Items.Any(i => i.Count > 0);
}

/// <summary>
/// A gift code, or a batch of single-use codes which share one reward and its rules. The codes are kept in the gift
/// codes' file in the save folder (not in the database), with their uses and the redemption log.
/// </summary>
public class GiftCodeDefinition
{
    /// <summary>Gets or sets the identifier (made when it's created; references and the log use it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the code players enter (empty for a batch). Matched without regard to case.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the codes of a batch; each one can be used once (by anybody, within the other rules).</summary>
    public List<string> BatchCodes { get; set; } = new();

    /// <summary>Gets or sets the prefix the batch's codes were made with.</summary>
    public string BatchPrefix { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the code works.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the code is shown on the website (only a single code, not a batch).</summary>
    public bool Public { get; set; }

    /// <summary>Gets or sets the admin's note (also the title shown with a public code).</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward.</summary>
    public GiftCodeReward Reward { get; set; } = new();

    /// <summary>Gets or sets how many times the code (all codes of a batch together) can be used; 0 is no limit.</summary>
    [Range(0, 10_000_000)]
    public int MaxUses { get; set; }

    /// <summary>Gets or sets who can use it once.</summary>
    public GiftCodeLimit Limit { get; set; } = GiftCodeLimit.OncePerAccount;

    /// <summary>Gets or sets the start, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney); empty is no start.</summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>Gets or sets the end, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney); empty is no end.</summary>
    public string End { get; set; } = string.Empty;

    /// <summary>Gets or sets the lowest character level; 0 is no limit.</summary>
    [Range(0, 1000)]
    public int MinLevel { get; set; }

    /// <summary>Gets or sets the highest character level; 0 is no limit.</summary>
    [Range(0, 1000)]
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets the fewest resets; 0 is no limit.</summary>
    [Range(0, 100_000)]
    public int MinResets { get; set; }

    /// <summary>Gets or sets the most resets; -1 is no limit (0 means characters which never reset).</summary>
    [Range(-1, 100_000)]
    public int MaxResets { get; set; } = -1;

    /// <summary>Gets or sets the classes (with their later evolutions) which can use it; empty is every class.</summary>
    public List<TopRaceClass> Classes { get; set; } = new();

    /// <summary>Gets or sets when it was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets a value indicating whether it's a batch of single-use codes.</summary>
    [JsonIgnore]
    public bool IsBatch => this.BatchCodes.Count > 0;

    /// <summary>Gets the text which names it in lists: the code, or the batch's prefix and size.</summary>
    [JsonIgnore]
    public string DisplayCode => this.IsBatch ? $"{this.BatchPrefix}… ({this.BatchCodes.Count})" : this.Code;
}

/// <summary>
/// The configuration of HNMO's gift codes. The codes themselves are kept in the file (<see cref="FileName"/>) and are
/// made on the admin panel's Gift codes page.
/// </summary>
public class GiftCodesConfiguration
{
    /// <summary>Gets or sets the time zone of the start and end dates, an IANA name.</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets how many wrong codes an account may enter in <see cref="WrongTriesWindowMinutes"/>.</summary>
    [Range(1, 1000)]
    public int WrongTriesLimit { get; set; } = 5;

    /// <summary>Gets or sets the window of <see cref="WrongTriesLimit"/>, in minutes.</summary>
    [Range(1, 10_080)]
    public int WrongTriesWindowMinutes { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether codes can be entered on the website's account page.</summary>
    public bool AllowWebsite { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the website shows the codes marked public.</summary>
    public bool ShowPublicCodes { get; set; } = true;

    /// <summary>Gets or sets how many redemptions the log keeps (the newest).</summary>
    [Range(10, 100_000)]
    public int LogKept { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the file which keeps the codes, their uses, the log and the rewards waiting for room in the bag,
    /// relative to the server's folder; empty keeps it in memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/gift-codes.json";
}
