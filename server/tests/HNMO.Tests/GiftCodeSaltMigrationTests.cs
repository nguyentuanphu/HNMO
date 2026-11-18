// <copyright file="GiftCodeSaltMigrationTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;

/// <summary>Protects the original identity salt through both metadata projections on the integration foundation.</summary>
[TestFixture]
public class GiftCodeSaltMigrationTests
{
    private const string OriginalSalt = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private DirectoryInfo _directory = null!;

    /// <summary>Creates an isolated save directory.</summary>
    [SetUp]
    public void SetUp() => this._directory = Directory.CreateTempSubdirectory("hnmo-gift-salt-");

    /// <summary>Removes only this test's files.</summary>
    [TearDown]
    public void TearDown() => this._directory.Delete(true);

    /// <summary>Both migration-time and ordinary server-only writes preserve the original salt and existing claims.</summary>
    /// <param name="hasClaims">Whether per-player claims must be migrated to rows.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void RetainsOriginalSaltDuringMigrationAndSubsequentSaves(bool hasClaims)
    {
        var legacy = Path.Combine(this._directory.FullName, "gift-codes.json");
        var seed = new GiftCodeStoreData { IdentitySalt = OriginalSalt };
        seed.Uses["gift"] = 2;
        if (hasClaims)
        {
            seed.Claims["gift"] = new HashSet<string> { "a:ann", "n:existing-network-hash", "d:existing-device-hash" };
        }

        File.WriteAllText(legacy, JsonSerializer.Serialize(seed));
        var store = new GiftCodeStore(legacy);
        Assert.That(store.Data.IdentitySalt, Is.EqualTo(OriginalSalt));
        Assert.That(this.ReadDisk().IdentitySalt, Is.EqualTo(OriginalSalt), "Migration metadata must retain the salt.");
        Assert.That(store.Data.Uses["gift"], Is.EqualTo(2));
        store.Data.Uses["gift"] = 3;
        store.Changed();
        store.SaveIfChanged();
        Assert.That(this.ReadDisk().IdentitySalt, Is.EqualTo(OriginalSalt), "Normal server-only saves must retain the salt.");
        Assert.That(this.ReadDisk().Uses["gift"], Is.EqualTo(3));
        var reopened = new GiftCodeStore(Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName));
        Assert.That(reopened.Data.IdentitySalt, Is.EqualTo(OriginalSalt));
        if (hasClaims)
        {
            Assert.That(reopened.Data.Claims["gift"], Is.EquivalentTo(seed.Claims["gift"]));
            var backend = HnmoPlayerStateRowStoreRegistry.GetBackend(Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName));
            Assert.That(backend.LoadAll("giftCodes.claims").Single().PayloadJson, Does.Contain("existing-network-hash").And.Contain("existing-device-hash"));
        }
    }

    /// <summary>An old account-only save receives one persistent salt without losing its old claims.</summary>
    [Test]
    public void UpgradesAnOldSaveWithoutChangingExistingClaims()
    {
        var file = Path.Combine(this._directory.FullName, "gift-codes.json");
        File.WriteAllText(file, "{\"Uses\":{\"gift\":1},\"Claims\":{\"gift\":[\"a:ann\"]}}");
        var store = new GiftCodeStore(file);
        Assert.That(Convert.FromHexString(store.Data.IdentitySalt), Has.Length.EqualTo(32));
        Assert.That(this.ReadDisk().IdentitySalt, Is.EqualTo(store.Data.IdentitySalt));
        var reopened = new GiftCodeStore(Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName));
        Assert.That(reopened.Data.IdentitySalt, Is.EqualTo(store.Data.IdentitySalt));
        Assert.That(reopened.Data.Claims["gift"], Is.EquivalentTo(new[] { "a:ann" }));
    }

    /// <summary>Concurrent opens through legacy and unified names share one initialized salt and store.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConcurrentAliasesShareOneSaltAsync()
    {
        var legacy = Path.Combine(this._directory.FullName, "gift-codes.json");
        var unified = Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName);
        var stores = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() => GiftCodeStore.Open(i % 2 == 0 ? legacy : unified)))).ConfigureAwait(false);
        Assert.That(stores.All(s => ReferenceEquals(s, stores[0])), Is.True);
        Assert.That(this.ReadDisk().IdentitySalt, Is.EqualTo(stores[0].Data.IdentitySalt));
    }

    /// <summary>Lost salts must not silently reset existing identity restrictions.</summary>
    /// <param name="prefix">The persisted identity kind.</param>
    [TestCase("n:")]
    [TestCase("d:")]
    public void RefusesHashedClaimsWithoutTheirOriginalSalt(string prefix)
    {
        var file = Path.Combine(this._directory.FullName, "gift-codes.json");
        var seed = new GiftCodeStoreData();
        seed.Claims["gift"] = new HashSet<string> { prefix + "existing-hash" };
        File.WriteAllText(file, JsonSerializer.Serialize(seed));
        Assert.Throws<InvalidDataException>(() => new GiftCodeStore(file));
    }

    /// <summary>A salt which cannot be saved must never be used for accepting redemptions.</summary>
    [Test]
    public void RefusesUnwritableInitialSalt()
    {
        var file = Path.Combine(this._directory.FullName, "blocked.json");
        Directory.CreateDirectory(file);
        Assert.Throws<IOException>(() => new GiftCodeStore(file));
    }

    private GiftCodeStoreData ReadDisk() => new HnmoStateStore(Path.Combine(this._directory.FullName, HnmoStateStore.StateFileName))
        .ReadSection<GiftCodeStoreData>("giftCodes", JsonSerializerOptions.Default)!;
}
