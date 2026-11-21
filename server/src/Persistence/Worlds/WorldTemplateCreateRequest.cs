// <copyright file="WorldTemplateCreateRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// Parameters to create a new world from an existing template world (ROADMAP 15.7).
/// </summary>
public sealed class WorldTemplateCreateRequest
{
    /// <summary>Gets or sets the source template world ID.</summary>
    public string SourceWorldId { get; set; } = "main";

    /// <summary>Gets or sets the new world slug ID.</summary>
    public string NewWorldId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English display name for the new world.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese display name for the new world.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the new world's season; <c>null</c> takes the template world's season.</summary>
    public WorldSeason? Season { get; set; }

    /// <summary>Gets or sets the dedicated PostgreSQL database name for the new world.</summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional connection string for the new world database.</summary>
    public string DatabaseConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the comma-separated or explicit server IDs assigned to the new world.</summary>
    public List<int> ServerIds { get; set; } = [];

    /// <summary>Gets or sets the template initialization mode (Fresh or CopiedCharacters).</summary>
    public WorldTemplateMode Mode { get; set; } = WorldTemplateMode.Fresh;

    /// <summary>Gets or sets the optional opening date/time in Sydney local format (<c>yyyy-MM-dd HH:mm</c> or ISO UTC).</summary>
    public string? OpeningSydneyText { get; set; }

    /// <summary>Gets or sets an explicit UTC opening timestamp (overrides <see cref="OpeningSydneyText"/> when set).</summary>
    public DateTimeOffset? OpeningDateUtc { get; set; }

    /// <summary>Gets or sets optional rate overrides (when null, copied from template world).</summary>
    public double? ExperienceMultiplier { get; set; }

    /// <summary>Gets or sets optional master EXP override.</summary>
    public double? MasterExperienceMultiplier { get; set; }

    /// <summary>Gets or sets optional drop override.</summary>
    public double? DropMultiplier { get; set; }

    /// <summary>Gets or sets optional zen override.</summary>
    public double? ZenMultiplier { get; set; }

    /// <summary>Gets or sets optional event preset EN override.</summary>
    public string? EventPresetEn { get; set; }

    /// <summary>Gets or sets optional event preset VI override.</summary>
    public string? EventPresetVi { get; set; }

    /// <summary>Gets or sets a value indicating whether one-off transfers into the new world are enabled.</summary>
    public bool TransferEnabled { get; set; }
}
