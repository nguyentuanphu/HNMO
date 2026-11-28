// <copyright file="BankTopUpConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>
/// The configuration of the <see cref="BankTopUpPlugIn"/>.
/// </summary>
public class BankTopUpConfiguration
{
    /// <summary>
    /// Gets or sets the account holder's name as the bank shows it (e.g. NGUYEN VAN A).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_AccountName_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_AccountName_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(80)]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the host's bank accounts players transfer to, at any bank of the VietQR network.
    /// </summary>
    [Display(Name = "Bank accounts", Description = "The accounts players see on the top-up page (any VietQR bank).")]
    public List<BankTopUpAccount> Accounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the Techcombank account number of the first version (kept so older saved settings still work;
    /// <see cref="UsableAccounts"/> turns it into an account while <see cref="Accounts"/> is empty).
    /// </summary>
    [ScaffoldColumn(false)]
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_TechcombankAccount_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_TechcombankAccount_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(30)]
    public string TechcombankAccount { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Vietcombank account number of the first version (see <see cref="TechcombankAccount"/>).
    /// </summary>
    [ScaffoldColumn(false)]
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_VietcombankAccount_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_VietcombankAccount_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(30)]
    public string VietcombankAccount { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many VND one coin costs.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_VndPerCoin_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_VndPerCoin_Description), ResourceType = typeof(Properties.PlugInResources))]
    [Range(1, 10_000_000)]
    public int VndPerCoin { get; set; } = 100;

    /// <summary>
    /// Gets or sets the smallest transfer in VND which is turned into coins.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_MinimumVnd_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_MinimumVnd_Description), ResourceType = typeof(Properties.PlugInResources))]
    [Range(1000, 100_000_000)]
    public int MinimumVnd { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets which coins a transfer buys.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_CoinType_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_CoinType_Description), ResourceType = typeof(Properties.PlugInResources))]
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>
    /// Gets or sets the word before the login name in the transfer message (e.g. "HNMO test2").
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_TransferPrefix_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_TransferPrefix_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(10, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9]+$")]
    public string TransferPrefix { get; set; } = "HNMO";

    /// <summary>
    /// Gets or sets the security key (secure-token) set in Casso's webhook settings; empty turns Casso off.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_CassoSecureToken_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_CassoSecureToken_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string CassoSecureToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key set in SePay's webhook settings (authentication "API Key"); empty turns SePay off.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.BankTopUpConfiguration_SepayApiKey_Caption), Description = nameof(Properties.PlugInResources.BankTopUpConfiguration_SepayApiKey_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string SepayApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The accounts players can be offered. Settings saved before the list existed have only the two fixed
    /// Techcombank and Vietcombank fields: they count as those two banks until the list is filled.
    /// </summary>
    /// <returns>The usable accounts, in order.</returns>
    public List<BankTopUpAccount> UsableAccounts()
    {
        if (this.Accounts.Count > 0)
        {
            return this.Accounts.Where(a => a.IsUsable).ToList();
        }

        var legacy = new List<BankTopUpAccount>();
        if (this.TechcombankAccount.Trim().Length > 0)
        {
            legacy.Add(new BankTopUpAccount { Bin = "970407", Number = this.TechcombankAccount.Trim() });
        }

        if (this.VietcombankAccount.Trim().Length > 0)
        {
            legacy.Add(new BankTopUpAccount { Bin = "970436", Number = this.VietcombankAccount.Trim() });
        }

        return legacy;
    }

    /// <summary>
    /// Moves the two fixed fields of the first version into <see cref="Accounts"/> (when the admin panel saves).
    /// </summary>
    public void MigrateLegacyAccounts()
    {
        if (this.Accounts.Count == 0)
        {
            this.Accounts = this.UsableAccounts();
        }

        this.TechcombankAccount = string.Empty;
        this.VietcombankAccount = string.Empty;
    }

    /// <summary>The holder's name of an account: its own, else <see cref="AccountName"/>.</summary>
    /// <param name="account">The account.</param>
    /// <returns>The name.</returns>
    public string HolderOf(BankTopUpAccount account) =>
        account.Holder.Trim().Length > 0 ? account.Holder.Trim() : this.AccountName.Trim();
}
