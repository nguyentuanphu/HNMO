// <copyright file="FolderIdentity.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// The code of an HNMO folder, which the weekly and the setup report send to the HNMO developer's hub as <c>folder</c>:
/// the first 16 hex digits of the SHA-256 of the folder's install id and its absolute path (<c>installId|path</c>). The
/// install id is random, made once and kept in the folder's save folder (<see cref="InstallIdFileName"/>), so every
/// program of the folder (Season 6.3, Season 9, the Arena) sends the same code, and a copy of the folder elsewhere sends
/// another one.
/// </summary>
public static class FolderIdentity
{
    /// <summary>The file in the save folder which keeps the install id.</summary>
    public const string InstallIdFileName = "install-id";

    /// <summary>The code of a folder.</summary>
    /// <param name="installId">The folder's install id.</param>
    /// <param name="folderPath">The folder's absolute path.</param>
    /// <returns>16 lower-case hex digits.</returns>
    public static string CodeOf(string installId, string folderPath)
    {
        ArgumentNullException.ThrowIfNull(installId);
        ArgumentNullException.ThrowIfNull(folderPath);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(installId + "|" + folderPath)))[..16].ToLowerInvariant();
    }

    /// <summary>
    /// The folder a save folder belongs to (its parent), as an absolute path without a trailing separator. On Windows,
    /// whose paths ignore case, in upper case, so that the programs of a folder started by differently written paths
    /// still agree.
    /// </summary>
    /// <param name="saveDirectory">The save folder.</param>
    /// <returns>The folder's path.</returns>
    public static string FolderOf(string saveDirectory)
    {
        var save = Path.GetFullPath(saveDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folder = Path.GetDirectoryName(save) ?? save;
        folder = folder.Length > 1 ? folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : folder;
        return OperatingSystem.IsWindows() ? folder.ToUpperInvariant() : folder;
    }

    /// <summary>The code of the folder a save folder belongs to; makes the install id on the first call.</summary>
    /// <param name="saveDirectory">The save folder.</param>
    /// <returns>The code.</returns>
    public static string CodeForSaveDirectory(string saveDirectory) => CodeOf(ReadOrCreateInstallId(saveDirectory), FolderOf(saveDirectory));

    /// <summary>
    /// Reads the folder's install id, or makes it (32 random hex digits). Two programs starting at once agree: the file is
    /// written beside and moved into place only when there is none yet, and the loser reads the winner's.
    /// </summary>
    /// <param name="saveDirectory">The save folder.</param>
    /// <returns>The install id.</returns>
    public static string ReadOrCreateInstallId(string saveDirectory)
    {
        var file = Path.Combine(saveDirectory, InstallIdFileName);
        if (TryRead(file) is { } existing)
        {
            return existing;
        }

        Directory.CreateDirectory(saveDirectory);
        var made = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var temporary = $"{file}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, made);
        try
        {
            File.Move(temporary, file, overwrite: File.Exists(file) && TryRead(file) is null);
            return made;
        }
        catch (IOException) when (TryRead(file) is { } other)
        {
            // Another program of the folder made it first.
            return other;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string? TryRead(string file)
    {
        try
        {
            var text = File.Exists(file) ? File.ReadAllText(file).Trim() : null;
            return text is { Length: >= 16 and <= 64 } && text.All(char.IsAsciiHexDigit) ? text : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
