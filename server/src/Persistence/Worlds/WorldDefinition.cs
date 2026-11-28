// <copyright file="WorldDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

using System.Globalization;

/// <summary>
/// Configuration and runtime state for a game world backed by its own database (ROADMAP 15.7).
/// </summary>
public sealed class WorldDefinition
{
    private static readonly TimeZoneInfo SydneyZone = ResolveSydneyZone();

    /// <summary>Gets or sets the unique slug identifier of this world (for example <c>s1</c> or <c>season-2</c>).</summary>
    public string WorldId { get; set; } = "main";

    /// <summary>Gets or sets the English display name.</summary>
    public string NameEn { get; set; } = "World 1";

    /// <summary>Gets or sets the Vietnamese display name.</summary>
    public string NameVi { get; set; } = "Thế giới 1";

    /// <summary>
    /// Gets or sets the season this world plays. A world saved before worlds had a season is Season 6.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorldSeason>))]
    public WorldSeason Season { get; set; } = WorldSeason.Season6;

    /// <summary>Gets or sets the dedicated PostgreSQL database name for this world.</summary>
    public string DatabaseName { get; set; } = "hnmo_world_1";

    /// <summary>Gets or sets the optional connection string (redacted from public snapshots).</summary>
    public string DatabaseConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the game server IDs belonging to this world.</summary>
    public List<int> ServerIds { get; set; } = [];

    /// <summary>Gets or sets the EXP multiplier for this world.</summary>
    public double ExperienceMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the master EXP multiplier for this world.</summary>
    public double MasterExperienceMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the item drop multiplier for this world.</summary>
    public double DropMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the zen drop multiplier for this world.</summary>
    public double ZenMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the English active event preset summary for this world.</summary>
    public string EventPresetEn { get; set; } = "Standard schedule";

    /// <summary>Gets or sets the Vietnamese active event preset summary for this world.</summary>
    public string EventPresetVi { get; set; } = "Lịch chuẩn";

    /// <summary>
    /// Gets or sets the English text the game's server choice shows under the worlds for this world (the host's own
    /// words, like the desktop's per-group description). Empty: the game shows its own text.
    /// </summary>
    public string ServerChoiceTextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text of the server choice for this world (see <see cref="ServerChoiceTextEn"/>).</summary>
    public string ServerChoiceTextVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the template source world ID when created from a template.</summary>
    public string? TemplateSourceWorldId { get; set; }

    /// <summary>Gets or sets how template characters were initialized.</summary>
    public WorldTemplateMode TemplateMode { get; set; } = WorldTemplateMode.Fresh;

    /// <summary>Gets or sets the scheduled opening timestamp (UTC). Before this time the world is locked for play.</summary>
    public DateTimeOffset? OpeningDateUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether one-off character transfers into this world are allowed.</summary>
    public bool TransferEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether this world is running.</summary>
    public bool IsRunning { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether this is the default primary world.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets the cached or external player count for this world.</summary>
    public int CachedOnlinePlayers { get; set; }

    /// <summary>Gets or sets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Converts a Sydney local date/time input (<c>yyyy-MM-ddTHH:mm</c> or <c>yyyy-MM-dd HH:mm</c>) to UTC.</summary>
    /// <param name="sydneyText">Sydney local date/time text.</param>
    /// <returns>Converted UTC timestamp, or <c>null</c> when empty.</returns>
    public static DateTimeOffset? ParseSydneyToUtc(string? sydneyText)
    {
        if (string.IsNullOrWhiteSpace(sydneyText))
        {
            return null;
        }

        var normalized = sydneyText.Trim();
        string[] formats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "o"];
        if (DateTimeOffset.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedOffset)
            && (normalized.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || normalized.Contains('+', StringComparison.Ordinal)))
        {
            return parsedOffset.ToUniversalTime();
        }

        if (!DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            throw new FormatException("Expected Sydney time in yyyy-MM-dd HH:mm or yyyy-MM-ddTHH:mm format.");
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, SydneyZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    /// <summary>Determines whether the world is open at <paramref name="utcNow"/>.</summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <returns><c>true</c> if open (no future opening date or opening date has passed).</returns>
    public bool IsOpenAt(DateTimeOffset utcNow) =>
        this.OpeningDateUtc is not { } opens || utcNow >= opens;

    /// <summary>Formats <see cref="OpeningDateUtc"/> in Sydney time (<c>yyyy-MM-dd HH:mm</c>).</summary>
    /// <returns>Sydney formatted timestamp or empty string.</returns>
    public string FormatOpeningSydney()
    {
        if (this.OpeningDateUtc is not { } opens)
        {
            return string.Empty;
        }

        var local = TimeZoneInfo.ConvertTime(opens, SydneyZone);
        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " Sydney";
    }

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }
}
