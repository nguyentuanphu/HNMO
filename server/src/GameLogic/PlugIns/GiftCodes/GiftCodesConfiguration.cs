// <copyright file="GiftCodesConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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
