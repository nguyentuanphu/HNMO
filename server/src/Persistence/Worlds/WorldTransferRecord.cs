// <copyright file="WorldTransferRecord.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// Immutable audit record of a completed one-off character transfer between worlds.
/// </summary>
public sealed class WorldTransferRecord
{
    /// <summary>Gets or sets the unique transfer identifier.</summary>
    public Guid TransferId { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the character identifier on the source world.</summary>
    public Guid SourceCharacterId { get; set; }

    /// <summary>Gets or sets the new character identifier created on the target world.</summary>
    public Guid TargetCharacterId { get; set; }

    /// <summary>Gets or sets the character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning account login name.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the source world ID.</summary>
    public string SourceWorldId { get; set; } = string.Empty;

    /// <summary>Gets or sets the destination world ID.</summary>
    public string TargetWorldId { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC completion timestamp.</summary>
    public DateTimeOffset TransferredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the operator who authorized the transfer.</summary>
    public string OperatorName { get; set; } = "system";
}
