// <copyright file="AccountSafetyConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

/// <summary>
/// The configuration of HNMO's account safety: the second password (the vault PIN of the game, 4 digits) and which
/// actions need it, the wrong-try limits, the item lock with its unlock wait, and how long the login and trade history
/// is kept. Edited on the admin panel's "Account safety" page.
/// </summary>
public class AccountSafetyConfiguration
{
    /// <summary>Gets or sets a value indicating whether taking items or zen out of the vault needs the second password.</summary>
    public bool RequireForVault { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether starting or accepting a trade needs the second password.</summary>
    public bool RequireForTrade { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether opening the personal store needs the second password.</summary>
    public bool RequireForPersonalStore { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether dropping or selling a valuable item (excellent, ancient, or of
    /// <see cref="ValuableMinimumLevel"/> and above) needs the second password.
    /// </summary>
    public bool RequireForValuableItems { get; set; }

    /// <summary>Gets or sets the item level from which an item counts as valuable.</summary>
    [Range(0, 15)]
    public int ValuableMinimumLevel { get; set; } = 9;

    /// <summary>Gets or sets how long an entered second password stays valid, in minutes; 0 is until the player logs out.</summary>
    [Range(0, 1440)]
    public int UnlockedMinutes { get; set; }

    /// <summary>Gets or sets how many wrong second passwords an account may enter before it's locked out for a while.</summary>
    [Range(1, 100)]
    public int MaxWrongTries { get; set; } = 5;

    /// <summary>Gets or sets how long an account is locked out after too many wrong second passwords, in minutes.</summary>
    [Range(1, 10_080)]
    public int LockoutMinutes { get; set; } = 30;

    /// <summary>Gets or sets a value indicating whether players can set, change and clear the second password on the website.</summary>
    public bool AllowWebsitePinChange { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether players can lock their items.</summary>
    public bool ItemLockEnabled { get; set; } = true;

    /// <summary>Gets or sets how long an unlock of an item takes, in hours (0 unlocks at once).</summary>
    [Range(0, 720)]
    public int UnlockWaitHours { get; set; } = 24;

    /// <summary>Gets or sets how many items an account may lock.</summary>
    [Range(1, 1000)]
    public int MaxLockedItems { get; set; } = 100;

    /// <summary>Gets or sets how many days the login history is kept.</summary>
    [Range(1, 3650)]
    public int LoginHistoryDays { get; set; } = 30;

    /// <summary>Gets or sets how many logins the website's account page shows.</summary>
    [Range(1, 500)]
    public int LoginsShown { get; set; } = 20;

    /// <summary>Gets or sets a value indicating whether the game tells the player when the last login came from another address.</summary>
    public bool NotifyNewAddress { get; set; } = true;

    /// <summary>Gets or sets how many days the trade history is kept.</summary>
    [Range(1, 3650)]
    public int TradeHistoryDays { get; set; } = 90;

    /// <summary>Gets or sets how many trades the website's account page shows.</summary>
    [Range(1, 500)]
    public int TradesShown { get; set; } = 20;

    /// <summary>Gets or sets the time zone of the times on the website and in the game, an IANA name.</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>
    /// Gets or sets the file which keeps the histories, the item locks and the wrong tries, relative to the server's
    /// folder; empty keeps it in memory only. The second password itself is the account's vault password in the database.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/account-safety.json";
}
