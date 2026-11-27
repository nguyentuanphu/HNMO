// <copyright file="NewsPost.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.News;

/// <summary>
/// A bilingual EN/VI announcement or news post managed in AdminPanel and shown on the website,
/// game login screen, and in-game chat notices.
/// </summary>
public sealed class NewsPost
{
    /// <summary>Gets or sets the unique post identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the English headline.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese headline.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English body text.</summary>
    public string BodyEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese body text.</summary>
    public string BodyVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional sanitized image URL (relative <c>/...</c> or <c>http(s)://</c>).</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC publish timestamp (entered and displayed in Sydney time).</summary>
    public DateTime PublishAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets a value indicating whether this post is pinned at the top.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Gets or sets a value indicating whether publishing/entering world should notify online players in chat.</summary>
    public bool NotifyInChat { get; set; } = true;

    /// <summary>Gets or sets the author display name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the UTC last-updated timestamp.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
