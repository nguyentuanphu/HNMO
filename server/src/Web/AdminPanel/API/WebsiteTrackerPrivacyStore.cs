// <copyright file="WebsiteTrackerPrivacyStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;

/// <summary>Persists account-wide tracker choices independently of the activity log's storage location.</summary>
/// <param name="backend">The host's row backend.</param>
public sealed class WebsiteTrackerPrivacyStore(IHnmoPlayerStateRowBackend backend)
{
    private const string Feature = "websiteTracker.optOut";

    /// <summary>Opens the preference store using the host's shared state backend.</summary>
    /// <returns>The preference store.</returns>
    internal static WebsiteTrackerPrivacyStore Open() => new(HnmoPlayerStateRowStoreRegistry.GetBackend(
        HnmoStateStore.ResolveStateFilePath(Path.Combine(AppContext.BaseDirectory, "save", HnmoStateStore.StateFileName))!));

    /// <summary>Checks the current choice for a set of accounts inside one request.</summary>
    /// <param name="logins">Account keys from the bounded raw activity page.</param>
    /// <returns>Private hashed account keys with an active opt-out.</returns>
    internal IReadOnlySet<string> ReadOptOuts(IEnumerable<string> logins)
    {
        var keys = logins.Select(Key).ToHashSet(StringComparer.Ordinal);
        if (backend is IHnmoPlayerStateRowQueryBackend exact)
        {
            return keys.Where(key => exact.FindRow(Feature, key) is not null).ToHashSet(StringComparer.Ordinal);
        }

        return backend.LoadAll(Feature).Where(row => keys.Contains(row.ScopeId)).Select(row => row.ScopeId).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Writes the authenticated account's choice.</summary>
    /// <param name="login">The verified canonical login.</param>
    /// <param name="optedOut">Whether the account should be excluded.</param>
    /// <param name="utcNow">Current UTC time.</param>
    public void Write(string login, bool optedOut, DateTime utcNow)
    {
        var key = Key(login);
        if (optedOut)
        {
            backend.UpsertBatch([new(Feature, key, "true", utcNow)], []);
        }
        else
        {
            backend.UpsertBatch([], [(Feature, key)]);
        }
    }

    internal static string Key(string login) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.Trim().ToUpperInvariant())));
}
