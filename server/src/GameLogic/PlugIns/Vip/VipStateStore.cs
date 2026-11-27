// <copyright file="VipStateStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

/// <summary>Stores VIP only in the shared HNMO state document, preserving other plug-in sections.</summary>
public sealed class VipStateStore
{
    private const string SectionName = "vip";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly ConcurrentDictionary<HnmoStateStore, VipStateStore> Stores = new();
    private readonly HnmoStateStore? _backend;

    private VipStateStore(HnmoStateStore? backend)
    {
        this._backend = backend;
        this.Data = backend?.ReadSection<VipState>(SectionName, JsonOptions) ?? new();
        if (backend?.HasSection(SectionName) == true && backend.ReadSection<VipState>(SectionName, JsonOptions) is null)
        {
            throw new InvalidDataException("The VIP section could not be read; preserve the state file and investigate before allowing purchases.");
        }
    }

    /// <summary>Gets the synchronization object for VIP rule changes in this process.</summary>
    internal object SyncRoot { get; } = new();

    /// <summary>Gets the current VIP section.</summary>
    internal VipState Data { get; }

    /// <summary>Opens the VIP section of an already shared state store.</summary>
    /// <param name="backend">The store returned by HnmoStateStore.Open for the host.</param>
    /// <returns>The shared VIP view of that store.</returns>
    public static VipStateStore Open(HnmoStateStore backend) => Stores.GetOrAdd(backend, value => new VipStateStore(value));

    /// <summary>Opens the vip section in the host's shared save document.</summary>
    /// <returns>The shared VIP store for this host.</returns>
    public static VipStateStore OpenHost() => Open(HnmoStateStore.Open(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName))!);

    /// <summary>Creates isolated in-memory rules state for tests.</summary>
    /// <returns>An in-memory store which writes no file.</returns>
    public static VipStateStore CreateInMemory() => new(null);

    /// <summary>Copies known active account identifiers for a host list; callers must verify their database proof.</summary>
    /// <param name="nowUtc">The current UTC instant.</param>
    /// <returns>A bounded-source index ordered by canonical login; identifiers preserve case-sensitive account lookup.</returns>
    public IReadOnlyList<Guid> KnownAccountIds(DateTime nowUtc)
    {
        lock (this.SyncRoot)
        {
            var accounts = new List<Guid>();
            foreach (var membership in this.Data.Memberships.Values.Where(value => value.UntilUtc > nowUtc).OrderBy(value => value.LoginName, StringComparer.Ordinal))
            {
                if (this.Data.WalletReferences.TryGetValue(membership.LoginName, out var reference)
                    && reference.Split(':') is ["vip", var account, var operation]
                    && Guid.TryParseExact(account, "N", out var id) && Guid.TryParseExact(operation, "N", out _))
                {
                    accounts.Add(id);
                }
            }

            return accounts.Distinct().ToArray();
        }
    }

    /// <summary>Atomically writes the VIP section while retaining all other sections.</summary>
    internal void Save() => this._backend?.WriteSection(SectionName, this.Data, JsonOptions);
}
