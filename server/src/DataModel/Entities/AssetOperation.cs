// <copyright file="AssetOperation.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Entities;

/// <summary>
/// The durable receipt of one operation which moves assets of an account (e.g. jewels between the bag and the Jewel
/// Bank), written in the same database transaction as the change itself.
/// </summary>
/// <remarks>
/// Its identifier is the operation's identifier, chosen by the caller before the first attempt and kept across retries
/// and reconnects: a second attempt with the same identifier and payload finds this receipt and reports the stored
/// outcome; one with the same identifier and another payload is a conflict. An operation with something left to deliver
/// (a website withdrawal waiting for room in the bag) keeps <see cref="PendingCount"/> above zero until it is delivered
/// in the same transaction as the bag which receives it.
/// </remarks>
[AggregateRoot]
public class AssetOperation
{
    /// <summary>Gets or sets the identifier of the operation.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the identifier of the account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the kind, e.g. <c>jewel-deposit</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the hash of the operation's payload, which tells a retry from a conflict.</summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation's details and outcome as JSON, for the history and for support.</summary>
    public string? Detail { get; set; }

    /// <summary>Gets or sets the key of what is still to be delivered (e.g. a jewel key), or <c>null</c>.</summary>
    public string? PendingKey { get; set; }

    /// <summary>Gets or sets the count still to be delivered; zero when nothing is pending.</summary>
    public int PendingCount { get; set; }

    /// <summary>Gets or sets the source (<c>game</c>, <c>website</c>, <c>admin</c>, <c>import</c>).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp at which the operation was committed.</summary>
    public DateTime CreatedAt { get; set; }
}
