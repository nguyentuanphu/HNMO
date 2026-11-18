// <copyright file="NewsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.News;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="NewsPlugIn"/> controlling website display, login panel visibility, and in-game chat notices.
/// </summary>
public sealed class NewsConfiguration
{
    /// <summary>Gets or sets a value indicating whether published posts appear on the public website.</summary>
    [Display(Name = "Show news on website home", Description = "Publishes active news posts at GET /api/website/news and the #news section of the homepage.")]
    public bool AllowWebsite { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether published posts appear in the login &amp; character-select news panel.</summary>
    [Display(Name = "Show news panel on login screen", Description = "Shows latest/pinned news inside the web client login & character selection card.")]
    public bool ShowLoginPanel { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether entering the world sends a chat notice for the newest post.</summary>
    [Display(Name = "Notify in chat on login", Description = "Sends a bilingual chat notice for the latest published news post when a player enters the world.")]
    public bool ChatNoticeOnEnterWorld { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether saving a published post broadcasts a live chat notice.</summary>
    [Display(Name = "Broadcast in chat when published", Description = "Broadcasts a bilingual chat notice immediately to all online players when a post is published.")]
    public bool ChatNoticeOnPublish { get; set; } = true;

    /// <summary>Gets or sets the maximum number of published posts exposed on the website and login screen.</summary>
    [Range(1, 100)]
    [Display(Name = "Max visible posts", Description = "Maximum number of published posts returned to players.")]
    public int MaxVisiblePosts { get; set; } = 20;

    /// <summary>Gets or sets the state file or database key.</summary>
    [Display(Name = "State key", Description = "Unified state store file/key for persisted news posts.")]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;
}
