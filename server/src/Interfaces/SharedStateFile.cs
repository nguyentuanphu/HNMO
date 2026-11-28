// <copyright file="SharedStateFile.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

/// <summary>
/// A state file several programs of one folder change (<c>save/developer-hub.json</c>: the server programs and the
/// launcher). Every writer takes <see cref="Lock"/> before it reads the file and keeps it until the new text is renamed
/// into place (<see cref="Replace"/>), so no writer overwrites a change another made meanwhile.
/// </summary>
/// <remarks>
/// The lock is a file beside the state (<c>&lt;file&gt;.lock</c>) opened without sharing: on Windows the system refuses a
/// second such open, on macOS and Linux .NET takes an exclusive <c>flock</c> on it, for other processes and for other
/// opens in the same process alike. The system lets it go when the program ends, so a crashed writer never leaves the
/// file locked; the lock file itself stays (it is empty). Readers need no lock: the rename is atomic.
/// </remarks>
public static class SharedStateFile
{
    /// <summary>How long a writer waits for another one before it writes without the lock (never stop for it).</summary>
    public static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);

    /// <summary>Gets the lock file of a state file.</summary>
    /// <param name="stateFile">The state file.</param>
    /// <returns>The lock file.</returns>
    public static string LockFileOf(string stateFile) => stateFile + ".lock";

    /// <summary>
    /// Takes the state file's lock, waiting at most <paramref name="wait"/> (<see cref="LockWait"/>) for another writer.
    /// </summary>
    /// <param name="stateFile">The state file.</param>
    /// <param name="wait">How long to wait; none: <see cref="LockWait"/>.</param>
    /// <returns>The lock, to dispose once the file is written; <c>null</c> when it can't be had (the caller writes all the same).</returns>
    public static IDisposable? Lock(string stateFile, TimeSpan? wait = null)
    {
        var lockFile = LockFileOf(stateFile);
        var waited = Stopwatch.StartNew();
        var limit = wait ?? LockWait;
        while (true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockFile))!);
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (waited.Elapsed < limit)
            {
                Thread.Sleep(20);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>Writes the state file's new text beside it and renames it into place.</summary>
    /// <param name="stateFile">The state file.</param>
    /// <param name="text">The new text.</param>
    /// <exception cref="IOException">The file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">This user may not write it.</exception>
    public static void Replace(string stateFile, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(stateFile))!);
        var temporary = $"{stateFile}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, text);
            File.Move(temporary, stateFile, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // left behind; harmless
            }
        }
    }
}
