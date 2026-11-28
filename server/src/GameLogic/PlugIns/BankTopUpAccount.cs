// <copyright file="BankTopUpAccount.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// One bank account of the host which players transfer to (VietQR, any bank of the NAPAS 247 network).
/// </summary>
public class BankTopUpAccount
{
    /// <summary>
    /// Gets or sets the bank's NAPAS id (6 digits, e.g. 970436 for Vietcombank); <see cref="VietnameseBanks"/> lists the common ones.
    /// </summary>
    [Display(Name = "Bank (NAPAS id)")]
    [RegularExpression("^[0-9]{6}$")]
    public string Bin { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account number.
    /// </summary>
    [Display(Name = "Account number")]
    [StringLength(30)]
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account holder's name as the bank shows it; empty uses <see cref="BankTopUpConfiguration.AccountName"/>.
    /// </summary>
    [Display(Name = "Account holder")]
    [StringLength(80)]
    public string Holder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether players see this account.
    /// </summary>
    [Display(Name = "Shown to players")]
    public bool Active { get; set; } = true;

    /// <summary>Gets a value indicating whether the account can be offered: active, a 6-digit bank id and a number.</summary>
    public bool IsUsable => this.Active && this.Bin.Length == 6 && this.Bin.All(char.IsAsciiDigit) && this.Number.Trim().Length > 0;
}
