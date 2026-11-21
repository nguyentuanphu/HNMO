// <copyright file="IntegrityResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Integrity;

using System.Collections.Generic;

/// <summary>
/// The result of an HNMO build integrity check.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Message">A human-readable, HNMO-branded summary suitable for logging.</param>
/// <param name="MismatchedFiles">The listed files that were missing or changed (empty unless <see cref="IntegrityStatus.FileMismatch"/>).</param>
public sealed record IntegrityResult(IntegrityStatus Status, string Message, IReadOnlyList<string> MismatchedFiles)
{
    /// <summary>
    /// Gets a value indicating whether the server should refuse to start for this result. A present-but-invalid
    /// signature always blocks startup; a changed file in a validly-signed build (a tampered release) blocks startup
    /// too, unless the host opted out with <c>Integrity:OnMismatch</c> = <c>warn</c> (<paramref name="allowOnMismatch"/>).
    /// </summary>
    /// <param name="allowOnMismatch">Whether the host chose to only warn on a changed file instead of refusing.</param>
    /// <returns><c>true</c> if startup should be refused.</returns>
    public bool ShouldRefuseStartup(bool allowOnMismatch) => this.Status switch
    {
        IntegrityStatus.SignatureInvalid => true,
        IntegrityStatus.ManifestInvalid => true,
        IntegrityStatus.FileMismatch => !allowOnMismatch,
        _ => false,
    };
}
