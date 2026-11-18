// <copyright file="WorldRegistryState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// Persisted multi-world registry and one-off character transfer history.
/// </summary>
public sealed class WorldRegistryState
{
    /// <summary>Gets or sets all configured worlds.</summary>
    public List<WorldDefinition> Worlds { get; set; } = [];

    /// <summary>Gets or sets completed one-off character transfers.</summary>
    public List<WorldTransferRecord> Transfers { get; set; } = [];
}
