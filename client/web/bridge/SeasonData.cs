using System.Text.RegularExpressions;

/// <summary>
/// What a season's data pack may answer. Only the looks and sounds of the world come from it: maps,
/// models, effects, sounds and music. The texts and tables (Local), the interface and HNMO's own data files
/// always come from the base game, whose formats the web game reads; a pack's files of the same name are in
/// another format and would break the game's windows.
/// </summary>
internal static partial class SeasonData
{
    public static bool IsSeasonFolder(string relativePath)
    {
        var first = relativePath.AsSpan().TrimStart('/');
        var end = first.IndexOfAny('/', '\\');
        return end > 0 && SeasonFolder().IsMatch(first[..end]);
    }

    /// <summary>
    /// Tells whether a Season 6.3 world may take this file from the Season 9 pack: only for the maps that exist in
    /// Season 9 alone (their World and Object folders), which a Season 6.3 world plays when its season settings
    /// enable such a map. It is asked only after the base game had no such file, so a file Season 6.3 has is always
    /// Season 6.3's own.
    /// </summary>
    /// <param name="relativePath">The path below <c>/data/</c>.</param>
    /// <returns><c>true</c> if the file may come from the Season 9 pack.</returns>
    public static bool IsSeasonNineOnlyMapFile(string relativePath)
    {
        var first = relativePath.AsSpan().TrimStart('/');
        var end = first.IndexOfAny('/', '\\');
        return end > 0 && SeasonNineOnlyMapFolder().IsMatch(first[..end]);
    }

    /// <summary>
    /// The second place a Season 9 world object's texture is looked for. Season 9 objects that are weapons and shields
    /// on display (Devias's Object102 and Object105) use item textures that exist only in <c>Item/</c>.
    /// </summary>
    /// <param name="relativePath">The path below <c>/data/s9/</c>.</param>
    /// <returns><c>Item/&lt;file&gt;</c> for a texture directly in an Object folder; otherwise <c>null</c>.</returns>
    public static string? ItemFolderTextureFor(string relativePath)
    {
        var path = relativePath.TrimStart('/').Replace('\\', '/');
        var slash = path.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0 || path.IndexOf('/', slash + 1) >= 0 || !ObjectFolder().IsMatch(path[..slash]))
        {
            return null;
        }

        var file = path[(slash + 1)..];
        return file.Length > 0 && !file.Contains("..", StringComparison.Ordinal) && TextureFile().IsMatch(file) ? "Item/" + file : null;
    }

    [GeneratedRegex(@"^(World\d+|Object\d+|Monster|Item|Skill|Effect|Player|NPC|Sound|Music)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonFolder();

    /// <summary>The folders of the maps only Season 9 has (World/Object 83, 92, 94, 95, 96, 99, 101, 103).</summary>
    [GeneratedRegex(@"^(World|Object)(83|92|94|95|96|99|101|103)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonNineOnlyMapFolder();

    [GeneratedRegex(@"^Object\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ObjectFolder();

    [GeneratedRegex(@"\.(jpg|tga|ozj|ozt|png|ktx2)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TextureFile();
}
