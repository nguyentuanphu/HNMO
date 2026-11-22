// <copyright file="GameNameConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The configuration of the <see cref="GameNamePlugIn"/>.
/// </summary>
public class GameNameConfiguration
{
    /// <summary>
    /// The name players see when nothing else is configured.
    /// </summary>
    public const string DefaultName = "HenryN Mu Online";

    /// <summary>
    /// Gets or sets the game's name which players see.
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_Name_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_Name_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(60, MinimumLength = 1)]
    public string Name { get; set; } = DefaultName;

    /// <summary>
    /// Gets or sets the Zalo link shown to players on the login screen and in the guide (empty: not shown).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_ZaloLink_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_ZaloLink_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string ZaloLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord link shown to players on the login screen and in the guide (empty: not shown).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_DiscordLink_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_DiscordLink_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string DiscordLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Facebook link shown to players on the login screen and in the guide (empty: not shown).
    /// </summary>
    [Display(Name = nameof(Properties.PlugInResources.GameNameConfiguration_FacebookLink_Caption), Description = nameof(Properties.PlugInResources.GameNameConfiguration_FacebookLink_Description), ResourceType = typeof(Properties.PlugInResources))]
    [StringLength(200)]
    public string FacebookLink { get; set; } = string.Empty;

    /// <summary>Gets or sets the host website logo; empty uses the theme default.</summary>
    [Display(Name = nameof(WebsiteBrandLabels.LogoCaption), Description = nameof(WebsiteBrandLabels.LogoDescription), ResourceType = typeof(WebsiteBrandLabels))]
    [StringLength(500, ErrorMessageResourceType = typeof(WebsiteBrandLabels), ErrorMessageResourceName = nameof(WebsiteBrandLabels.LogoLengthValidation))]
    [RegularExpression(@"^$|^/(?!/)[^\s\\<>""']+$|^https://[^\s\\<>""']+$", ErrorMessageResourceType = typeof(WebsiteBrandLabels), ErrorMessageResourceName = nameof(WebsiteBrandLabels.LogoFormatValidation))]
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the host website accent; empty uses the theme default.</summary>
    [Display(Name = nameof(WebsiteBrandLabels.AccentCaption), Description = nameof(WebsiteBrandLabels.AccentDescription), ResourceType = typeof(WebsiteBrandLabels))]
    [RegularExpression("^$|^#[0-9A-Fa-f]{6}$", ErrorMessageResourceType = typeof(WebsiteBrandLabels), ErrorMessageResourceName = nameof(WebsiteBrandLabels.ColorValidation))]
    public string AccentColor { get; set; } = string.Empty;

    /// <summary>Gets or sets the host website background; empty uses the theme default.</summary>
    [Display(Name = nameof(WebsiteBrandLabels.BackgroundCaption), Description = nameof(WebsiteBrandLabels.BackgroundDescription), ResourceType = typeof(WebsiteBrandLabels))]
    [RegularExpression("^$|^#[0-9A-Fa-f]{6}$", ErrorMessageResourceType = typeof(WebsiteBrandLabels), ErrorMessageResourceName = nameof(WebsiteBrandLabels.ColorValidation))]
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>Gets or sets the host website panel; empty uses the theme default.</summary>
    [Display(Name = nameof(WebsiteBrandLabels.PanelCaption), Description = nameof(WebsiteBrandLabels.PanelDescription), ResourceType = typeof(WebsiteBrandLabels))]
    [RegularExpression("^$|^#[0-9A-Fa-f]{6}$", ErrorMessageResourceType = typeof(WebsiteBrandLabels), ErrorMessageResourceName = nameof(WebsiteBrandLabels.ColorValidation))]
    public string PanelColor { get; set; } = string.Empty;

    /// <summary>Gets or sets the host website text; empty uses the theme default.</summary>
    [Display(Name = nameof(WebsiteBrandLabels.TextCaption), Description = nameof(WebsiteBrandLabels.TextDescription), ResourceType = typeof(WebsiteBrandLabels))]
    [RegularExpression("^$|^#[0-9A-Fa-f]{6}$", ErrorMessageResourceType = typeof(WebsiteBrandLabels), ErrorMessageResourceName = nameof(WebsiteBrandLabels.ColorValidation))]
    public string TextColor { get; set; } = string.Empty;
}
