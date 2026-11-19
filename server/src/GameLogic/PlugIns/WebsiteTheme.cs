// <copyright file="WebsiteTheme.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;

/// <summary>Public website appearances; persisted values must not be renumbered.</summary>
public enum WebsiteTheme
{
    /// <summary>The classic website appearance.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Classic), ResourceType = typeof(WebsiteThemeLabels))]
    Classic = 0,

    /// <summary>The azure website appearance.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Azure), ResourceType = typeof(WebsiteThemeLabels))]
    Azure = 1,

    /// <summary>The ember website appearance.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Ember), ResourceType = typeof(WebsiteThemeLabels))]
    Ember = 2,

    /// <summary>The jade website appearance.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Jade), ResourceType = typeof(WebsiteThemeLabels))]
    Jade = 3,

    /// <summary>The violet website appearance.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Violet), ResourceType = typeof(WebsiteThemeLabels))]
    Violet = 4,

    /// <summary>The parchment website appearance.</summary>
    [Display(Name = nameof(WebsiteThemeLabels.Parchment), ResourceType = typeof(WebsiteThemeLabels))]
    Parchment = 5,
}
