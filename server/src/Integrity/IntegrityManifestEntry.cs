// <copyright file="IntegrityManifestEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Integrity;

/// <summary>
/// One entry of the HNMO integrity manifest: a shipped file and the SHA-256 hash it must have.
/// </summary>
/// <param name="Path">The file path, relative to the HNMO folder root, using forward slashes.</param>
/// <param name="Sha256">The expected lower-case hex SHA-256 of the file.</param>
public sealed record IntegrityManifestEntry(string Path, string Sha256);
