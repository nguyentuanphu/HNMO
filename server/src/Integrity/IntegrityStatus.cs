// <copyright file="IntegrityStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Integrity;

/// <summary>
/// The outcome of an HNMO build integrity check.
/// </summary>
public enum IntegrityStatus
{
    /// <summary>
    /// A signed manifest was present, its signature verified and every listed file matched.
    /// </summary>
    Ok,

    /// <summary>
    /// No manifest or no signature was found. This is an unsigned (e.g. development) build.
    /// </summary>
    Unsigned,

    /// <summary>
    /// A manifest and a signature were present, but the signature did not verify against the
    /// public key (the manifest or the signature was altered).
    /// </summary>
    SignatureInvalid,

    /// <summary>
    /// The signature verified, but the manifest could not be parsed.
    /// </summary>
    ManifestInvalid,

    /// <summary>
    /// The signature verified, but one or more listed files are missing or have changed.
    /// </summary>
    FileMismatch,
}
