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

    /// <summary>Gets the localized caption for the English roadmap field.</summary>
    public static string ContentRoadmapEn => Resources.GetString(nameof(ContentRoadmapEn), CultureInfo.CurrentUICulture) ?? nameof(ContentRoadmapEn);

    /// <summary>Gets the localized caption for the Vietnamese roadmap field.</summary>
    public static string ContentRoadmapVi => Resources.GetString(nameof(ContentRoadmapVi), CultureInfo.CurrentUICulture) ?? nameof(ContentRoadmapVi);

    /// <summary>Gets the localized caption for the English about page field.</summary>
    public static string ContentAboutEn => Resources.GetString(nameof(ContentAboutEn), CultureInfo.CurrentUICulture) ?? nameof(ContentAboutEn);

    /// <summary>Gets the localized caption for the Vietnamese about page field.</summary>
    public static string ContentAboutVi => Resources.GetString(nameof(ContentAboutVi), CultureInfo.CurrentUICulture) ?? nameof(ContentAboutVi);

    /// <summary>Gets the localized caption for the English terms of use field.</summary>
    public static string ContentTermsEn => Resources.GetString(nameof(ContentTermsEn), CultureInfo.CurrentUICulture) ?? nameof(ContentTermsEn);

    /// <summary>Gets the localized caption for the Vietnamese terms of use field.</summary>
    public static string ContentTermsVi => Resources.GetString(nameof(ContentTermsVi), CultureInfo.CurrentUICulture) ?? nameof(ContentTermsVi);

    /// <summary>Gets the localized caption for the English privacy policy field.</summary>
    public static string ContentPrivacyEn => Resources.GetString(nameof(ContentPrivacyEn), CultureInfo.CurrentUICulture) ?? nameof(ContentPrivacyEn);

    /// <summary>Gets the localized caption for the Vietnamese privacy policy field.</summary>
    public static string ContentPrivacyVi => Resources.GetString(nameof(ContentPrivacyVi), CultureInfo.CurrentUICulture) ?? nameof(ContentPrivacyVi);

    /// <summary>Gets the localized public content editing instructions.</summary>
    public static string ContentDescription => Resources.GetString(nameof(ContentDescription), CultureInfo.CurrentUICulture) ?? nameof(ContentDescription);
}
