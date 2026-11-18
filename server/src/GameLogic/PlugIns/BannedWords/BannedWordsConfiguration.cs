// <copyright file="BannedWordsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BannedWords;

/// <summary>
/// Configuration for <see cref="BannedWordsPlugIn"/> (disabled until the host activates it).
/// </summary>
public sealed class BannedWordsConfiguration
{
    /// <summary>
    /// Gets the starter English banned words and phrases.
    /// </summary>
    public static readonly string[] DefaultEnglishWords =
    [
        "fuck",
        "fucking",
        "fucker",
        "shit",
        "bitch",
        "cunt",
        "dick",
        "pussy",
        "asshole",
        "bastard",
        "motherfucker",
        "nigger",
        "retard",
        "whore",
        "slut",
    ];

    /// <summary>
    /// Gets the starter Vietnamese banned words, accented profanity, and common unaccented slang.
    /// </summary>
    public static readonly string[] DefaultVietnameseWords =
    [
        "địt",
        "địt mẹ",
        "đụ",
        "đụ má",
        "đéo",
        "cặc",
        "lồn",
        "buồi",
        "đĩ",
        "óc chó",
        "mẹ mày",
        "con mẹ mày",
        "thằng chó",
        "chó chết",
        "súc vật",
        "đm",
        "dm",
        "đmm",
        "dmm",
        "đcm",
        "dcm",
        "đcmm",
        "dcmm",
        "vcl",
        "vkl",
        "clm",
        "cmm",
        "đkm",
        "dkm",
    ];

    /// <summary>
    /// Gets or sets the English banned words and phrases.
    /// </summary>
    public List<string> EnglishWords { get; set; } = [.. DefaultEnglishWords];

    /// <summary>
    /// Gets or sets the Vietnamese banned words and phrases.
    /// </summary>
    public List<string> VietnameseWords { get; set; } = [.. DefaultVietnameseWords];

    /// <summary>
    /// Gets or sets how matching chat messages are handled (<see cref="BannedWordChatAction.Censor"/> or <see cref="BannedWordChatAction.Block"/>).
    /// </summary>
    public BannedWordChatAction ChatAction { get; set; } = BannedWordChatAction.Censor;

    /// <summary>
    /// Gets or sets a value indicating whether chat messages (normal, whisper, party, guild, alliance, gens, /post, /world) are filtered.
    /// </summary>
    public bool FilterChat { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether new character names are checked on creation.
    /// </summary>
    public bool FilterCharacterNames { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether new guild names are checked on creation.
    /// </summary>
    public bool FilterGuildNames { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Vietnamese diacritics are stripped during comparison.
    /// Keep <c>false</c> to avoid flagging everyday words like <c>các</c> or <c>lớn</c>.
    /// </summary>
    public bool IgnoreVietnameseDiacritics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CamelCase/digit tokens and substrings of length 4+ are checked inside character and guild names.
    /// </summary>
    public bool CheckNameSubstrings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Game Masters bypass the chat and name filter.
    /// </summary>
    public bool ExemptGameMasters { get; set; } = true;
}
