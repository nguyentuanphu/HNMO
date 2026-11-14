// <copyright file="BankTopUpPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Top-ups of cash shop coins by bank transfer (VietQR): the bank accounts and the coin rate players see
/// on the web game's top-up page, and the keys of the services (Casso, SePay) which report incoming
/// transfers to the admin panel, which then adds the coins to the account named in the transfer message.
/// Deactivated by default: it does nothing until the owner fills it in and activates it.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.BankTopUpPlugIn_Name), Description = nameof(Properties.PlugInResources.BankTopUpPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("C4F1A7D2-3B8E-4E59-9A16-7D2E5B3C8F41")]
public class BankTopUpPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<BankTopUpConfiguration>, ISupportDefaultCustomConfiguration, IDisabledByDefault
{
    /// <inheritdoc />
    public BankTopUpConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new BankTopUpConfiguration();
    }
}
