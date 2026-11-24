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

    /// <summary>Gets the localized class guide setting.</summary>
    public static string ClassGuide => Resources.GetString(nameof(ClassGuide), CultureInfo.CurrentUICulture) ?? nameof(ClassGuide);

    /// <summary>Gets the localized class guide setting description.</summary>
    public static string ClassGuideDescription => Resources.GetString(nameof(ClassGuideDescription), CultureInfo.CurrentUICulture) ?? nameof(ClassGuideDescription);
    /// <summary>Gets the localized combat guide switch label.</summary>
    public static string CombatGuide => Resources.GetString(nameof(CombatGuide), CultureInfo.CurrentUICulture) ?? nameof(CombatGuide);

    /// <summary>Gets the localized combat guide switch description.</summary>
    public static string CombatGuideDescription => Resources.GetString(nameof(CombatGuideDescription), CultureInfo.CurrentUICulture) ?? nameof(CombatGuideDescription);

    /// <summary>Gets the localized ClientRulesEn label.</summary>
    public static string ClientRulesEn => Resources.GetString(nameof(ClientRulesEn), CultureInfo.CurrentUICulture) ?? nameof(ClientRulesEn);

    /// <summary>Gets the localized ClientRulesVi label.</summary>
    public static string ClientRulesVi => Resources.GetString(nameof(ClientRulesVi), CultureInfo.CurrentUICulture) ?? nameof(ClientRulesVi);

    /// <summary>Gets the localized NotForSaleEn label.</summary>
    public static string NotForSaleEn => Resources.GetString(nameof(NotForSaleEn), CultureInfo.CurrentUICulture) ?? nameof(NotForSaleEn);

    /// <summary>Gets the localized NotForSaleVi label.</summary>
    public static string NotForSaleVi => Resources.GetString(nameof(NotForSaleVi), CultureInfo.CurrentUICulture) ?? nameof(NotForSaleVi);

    /// <summary>Gets the localized item and mix guide setting.</summary>
    public static string ItemGuide => Resources.GetString(nameof(ItemGuide), CultureInfo.CurrentUICulture) ?? nameof(ItemGuide);

    /// <summary>Gets the localized item and mix guide setting description.</summary>
    public static string ItemGuideDescription => Resources.GetString(nameof(ItemGuideDescription), CultureInfo.CurrentUICulture) ?? nameof(ItemGuideDescription);

    /// <summary>Gets the localized Description label.</summary>
    public static string Description => Resources.GetString(nameof(Description), CultureInfo.CurrentUICulture) ?? nameof(Description);

    /// <summary>Gets the localized MaximumLength label.</summary>
    public static string MaximumLength => Resources.GetString(nameof(MaximumLength), CultureInfo.CurrentUICulture) ?? nameof(MaximumLength);

    /// <summary>Gets the public tracker switch caption.</summary>
    public static string TrackerEnabled => Get(nameof(TrackerEnabled));

    /// <summary>Gets the public tracker switch description.</summary>
    public static string TrackerEnabledDescription => Get(nameof(TrackerEnabledDescription));

    /// <summary>Gets the tracker character-name switch caption.</summary>
    public static string TrackerNames => Get(nameof(TrackerNames));

    /// <summary>Gets the tracker character-name switch description.</summary>
    public static string TrackerNamesDescription => Get(nameof(TrackerNamesDescription));

    private static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    /// <summary>Gets the localized event and quest guide setting.</summary>
    public static string EventGuide => Resources.GetString(nameof(EventGuide), CultureInfo.CurrentUICulture) ?? nameof(EventGuide);

    /// <summary>Gets the localized event and quest guide setting description.</summary>
    public static string EventGuideDescription => Resources.GetString(nameof(EventGuideDescription), CultureInfo.CurrentUICulture) ?? nameof(EventGuideDescription);

    /// <summary>Gets the public character bans caption.</summary>
    public static string BannedCharacters => Resources.GetString(nameof(BannedCharacters), CultureInfo.CurrentUICulture) ?? nameof(BannedCharacters);

    /// <summary>Gets the public character bans description.</summary>
    public static string BannedCharactersDescription => Resources.GetString(nameof(BannedCharactersDescription), CultureInfo.CurrentUICulture) ?? nameof(BannedCharactersDescription);
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
