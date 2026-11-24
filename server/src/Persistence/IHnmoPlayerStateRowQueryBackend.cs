// <copyright file="IHnmoPlayerStateRowQueryBackend.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>Optional bounded row reads for account-facing plug-ins; existing backends remain compatible.</summary>
public interface IHnmoPlayerStateRowQueryBackend : IHnmoPlayerStateRowBackend
{
    /// <summary>Reads one exact active feature/scope key without loading unrelated player payloads.</summary>
    /// <param name="feature">The feature key.</param>
    /// <param name="scopeId">The exact scope identifier.</param>
    /// <returns>The existing row, or null.</returns>
    HnmoFeatureRow? FindRow(string feature, string scopeId);

    /// <summary>Reads a bounded literal scope prefix, newest first with a stable scope-key tie-break.</summary>
    /// <param name="feature">The feature key.</param>
    /// <param name="scopePrefix">The literal scope prefix. Use an empty string to query the whole feature.</param>
    /// <param name="offset">The nonnegative number of matching rows to skip.</param>
    /// <param name="limit">The maximum rows, between 1 and 101.</param>
    /// <returns>Only the bounded matching page.</returns>
    IReadOnlyList<HnmoFeatureRow> LoadScopePage(string feature, string scopePrefix, int offset, int limit);

    /// <summary>Reads the active log rows containing an exact origin serial, using the same activity store.</summary>
    /// <param name="serial">The origin serial, maintained by item persistence.</param>
    /// <param name="fromUtc">The inclusive retention cutoff.</param>
    /// <param name="limit">The maximum number of matches, between 1 and 101.</param>
    /// <returns>The bounded results, or null if this backend does not support indexed serial reads.</returns>
    IReadOnlyList<HnmoFeatureRow>? LoadActivityItemPage(Guid serial, DateTime fromUtc, int limit) => null;
}
