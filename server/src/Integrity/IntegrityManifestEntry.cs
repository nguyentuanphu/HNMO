// <copyright file="IntegrityManifestEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Integrity;

/// <summary>
/// One entry of the HNMO integrity manifest: a shipped file and the SHA-256 hash it must have.
/// </summary>
/// <param name="Path">The file path, relative to the HNMO folder root, using forward slashes.</param>
/// <param name="Sha256">The expected lower-case hex SHA-256 of the file.</param>
public sealed record IntegrityManifestEntry(string Path, string Sha256);
