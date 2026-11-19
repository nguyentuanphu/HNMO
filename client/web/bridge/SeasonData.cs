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

    [GeneratedRegex(@"^(World\d+|Object\d+|Monster|Item|Skill|Effect|Player|NPC|Sound|Music)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeasonFolder();
}
