// <copyright file="LegacyPasswordMigrationMode.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;

/// <summary>
/// Password migration mode for imported accounts. Plaintext or weak legacy hashes are never stored.
/// </summary>
public enum LegacyPasswordMigrationMode
{
    /// <summary>Replaces every password with a cryptographically random token hashed with BCrypt, requiring players to reset on first login.</summary>
    ForceRandomReset = 0,

    /// <summary>Re-hashes an admin-provided temporary migration passphrase with BCrypt into every imported account.</summary>
    UseTemporaryPassphrase = 1,
}
