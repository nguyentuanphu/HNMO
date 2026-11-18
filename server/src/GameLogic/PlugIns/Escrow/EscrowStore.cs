// <copyright file="EscrowStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Thread-safe, crash-safe persistence wrapper over <see cref="HnmoStateStore"/> (<c>escrow</c> section).
/// Every state transition runs inside a single serialized lock and flushes atomically; if a mutator throws
/// or rolls back mid-operation, persisted state remains untouched.
/// </summary>
public sealed class EscrowStore
{
    private const string SectionKey = "escrow";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _store;

    private EscrowStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the escrow store backed by <paramref name="filePath"/> (or a temp file in tests).</summary>
    /// <param name="filePath">Target state file path.</param>
    /// <returns>Store instance.</returns>
    public static EscrowStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-escrow-{Guid.NewGuid():N}.json"));
        return new(store);
    }

    /// <summary>Reads a deep projection under the store lock.</summary>
    /// <typeparam name="TResult">Projected result type.</typeparam>
    /// <param name="reader">Projection delegate.</param>
    /// <returns>Projected value.</returns>
    public TResult Read<TResult>(Func<EscrowStoreData, TResult> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (this._gate)
        {
            var data = this.LoadLocked();
            return reader(data);
        }
    }

    /// <summary>Executes an all-or-nothing atomic state mutation under lock.</summary>
    /// <typeparam name="TResult">Mutation outcome type.</typeparam>
    /// <param name="mutator">Mutator returning whether changes occurred and the result.</param>
    /// <returns>Mutation outcome.</returns>
    public TResult Mutate<TResult>(Func<EscrowStoreData, (bool Changed, TResult Result)> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        lock (this._gate)
        {
            var data = this.LoadLocked();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }

    private EscrowStoreData LoadLocked() =>
        this._store.ReadSection<EscrowStoreData>(SectionKey, JsonOptions) ?? new EscrowStoreData();
}
