// <copyright file="WebsiteConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the <see cref="WebsitePlugIn"/>.
/// </summary>
public class WebsiteConfiguration
{
    /// <summary>
    /// The admin path when nothing else is set.
    /// </summary>
    public const string DefaultAdminPath = "adminpanel";

    /// <summary>
    /// Makes a hard-to-guess admin path ("admin-" and 16 random letters and digits) for a new HNMO folder.
    /// </summary>
    /// <returns>The new path.</returns>
    public static string NewRandomAdminPath()
    {
        const string letters = "abcdefghijkmnpqrstuvwxyz23456789";
        return "admin-" + System.Security.Cryptography.RandomNumberGenerator.GetString(letters, 16);
    }

    /// <summary>
    /// Gets or sets the path under which the website opens the admin panel (e.g. "adminpanel" for
    /// https://your-site/adminpanel). Choose something hard to guess; empty turns the website's
    /// admin access off (the admin panel then only opens on the host computer).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.WebsiteConfiguration_AdminPath_Caption), Description = nameof(Properties.PlugInResources.WebsiteConfiguration_AdminPath_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(40)]
    [RegularExpression("^$|^[A-Za-z0-9][A-Za-z0-9_-]{2,39}$")]
    public string AdminPath { get; set; } = DefaultAdminPath;

    /// <summary>Gets or sets the public website appearance. Existing installations keep Classic.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Caption), Description = nameof(WebsiteThemeLabels.GlobalDescription), ResourceType = typeof(WebsiteThemeLabels))]
    [EnumDataType(typeof(WebsiteTheme))]
    public WebsiteTheme Theme { get; set; } = WebsiteTheme.Classic;

    /// <summary>
    /// Gets or sets the OAuth client id of a Google Cloud "Web application" client; empty hides "Sign in with Google".
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.WebsiteConfiguration_GoogleClientId_Caption), Description = nameof(Properties.PlugInResources.WebsiteConfiguration_GoogleClientId_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string GoogleClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the website publishes the live item and mix guide.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.ItemGuide), Description = nameof(WebsiteServerPageLabels.ItemGuideDescription), ResourceType = typeof(WebsiteServerPageLabels))]
    public bool ItemGuideEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the web game hides its own password form and requires
    /// players to sign in on the website first.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.WebsiteConfiguration_RequireWebsiteLogin_Caption), Description = nameof(Properties.PlugInResources.WebsiteConfiguration_RequireWebsiteLogin_Description), ResourceType = typeof(Properties.PlugInResources))]
    public bool RequireWebsiteLogin { get; set; }

    /// <summary>Gets or sets whether the host publishes a public tracker feed.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.TrackerEnabled), Description = nameof(WebsiteServerPageLabels.TrackerEnabledDescription), ResourceType = typeof(WebsiteServerPageLabels))]
    public bool TrackerEnabled { get; set; }

    /// <summary>Gets or sets whether the tracker may display eligible character names.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.TrackerNames), Description = nameof(WebsiteServerPageLabels.TrackerNamesDescription), ResourceType = typeof(WebsiteServerPageLabels))]
    public bool TrackerShowCharacterNames { get; set; }
    /// <summary>Gets or sets a value indicating whether the public guide shows live class configuration.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.ClassGuide), Description = nameof(WebsiteServerPageLabels.ClassGuideDescription), ResourceType = typeof(WebsiteServerPageLabels))]
    public bool ClassGuideEnabled { get; set; } = true;

    /// <summary>Gets or sets the public ClientRulesEn lines; an empty value publishes no claim.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.ClientRulesEn), Description = nameof(WebsiteServerPageLabels.Description), ResourceType = typeof(WebsiteServerPageLabels))]
    [StringLength(4000, ErrorMessageResourceType = typeof(WebsiteServerPageLabels), ErrorMessageResourceName = nameof(WebsiteServerPageLabels.MaximumLength))]
    [DataType(DataType.MultilineText)]
    public string ServerClientRulesEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the public ClientRulesVi lines; an empty value publishes no claim.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.ClientRulesVi), Description = nameof(WebsiteServerPageLabels.Description), ResourceType = typeof(WebsiteServerPageLabels))]
    [StringLength(4000, ErrorMessageResourceType = typeof(WebsiteServerPageLabels), ErrorMessageResourceName = nameof(WebsiteServerPageLabels.MaximumLength))]
    [DataType(DataType.MultilineText)]
    public string ServerClientRulesVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the public NotForSaleEn lines; an empty value publishes no claim.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.NotForSaleEn), Description = nameof(WebsiteServerPageLabels.Description), ResourceType = typeof(WebsiteServerPageLabels))]
    [StringLength(4000, ErrorMessageResourceType = typeof(WebsiteServerPageLabels), ErrorMessageResourceName = nameof(WebsiteServerPageLabels.MaximumLength))]
    [DataType(DataType.MultilineText)]
    public string ServerNotForSaleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the public NotForSaleVi lines; an empty value publishes no claim.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.NotForSaleVi), Description = nameof(WebsiteServerPageLabels.Description), ResourceType = typeof(WebsiteServerPageLabels))]
    [StringLength(4000, ErrorMessageResourceType = typeof(WebsiteServerPageLabels), ErrorMessageResourceName = nameof(WebsiteServerPageLabels.MaximumLength))]
    [DataType(DataType.MultilineText)]
    public string ServerNotForSaleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the public website shows the live event and quest guide.</summary>
    [Display(Name = nameof(WebsiteServerPageLabels.EventGuide), Description = nameof(WebsiteServerPageLabels.EventGuideDescription), ResourceType = typeof(WebsiteServerPageLabels))]
    public bool EventGuideEnabled { get; set; } = true;
}
