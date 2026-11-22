// <copyright file="WebsiteServerPageLabels.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Globalization;
using System.Resources;

/// <summary>Four-language captions and validation for host-authored public server information.</summary>
public static class WebsiteServerPageLabels
{
    private static readonly ResourceManager Resources = new("HNMO.GameLogic.Properties.WebsiteServerPage", typeof(WebsiteServerPageLabels).Assembly);

    /// <summary>Gets the localized ClientRulesEn label.</summary>
    public static string ClientRulesEn => Resources.GetString(nameof(ClientRulesEn), CultureInfo.CurrentUICulture) ?? nameof(ClientRulesEn);

    /// <summary>Gets the localized ClientRulesVi label.</summary>
    public static string ClientRulesVi => Resources.GetString(nameof(ClientRulesVi), CultureInfo.CurrentUICulture) ?? nameof(ClientRulesVi);

    /// <summary>Gets the localized NotForSaleEn label.</summary>
    public static string NotForSaleEn => Resources.GetString(nameof(NotForSaleEn), CultureInfo.CurrentUICulture) ?? nameof(NotForSaleEn);

    /// <summary>Gets the localized NotForSaleVi label.</summary>
    public static string NotForSaleVi => Resources.GetString(nameof(NotForSaleVi), CultureInfo.CurrentUICulture) ?? nameof(NotForSaleVi);

    /// <summary>Gets the localized Description label.</summary>
    public static string Description => Resources.GetString(nameof(Description), CultureInfo.CurrentUICulture) ?? nameof(Description);

    /// <summary>Gets the localized MaximumLength label.</summary>
    public static string MaximumLength => Resources.GetString(nameof(MaximumLength), CultureInfo.CurrentUICulture) ?? nameof(MaximumLength);
}
