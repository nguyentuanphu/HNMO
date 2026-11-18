// <copyright file="GiftCodeIdentityLimitTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;

/// <summary>Checks cross-account network/device limits and persistence without retaining raw identities.</summary>
[TestFixture]
public class GiftCodeIdentityLimitTest
{
    private const string Network = "203.0.113.12";
    private const string Device = "53046daa-2908-4655-b22e-61dd3cdd826a";
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Existing numeric result values remain readable when identity results are appended.</summary>
    [Test]
    public void LegacyNumericResultsKeepTheirMeaning()
    {
        var legacy = JsonSerializer.Deserialize<GiftCodeResult>("14");
        Assert.That(legacy, Is.EqualTo(GiftCodeResult.WrongClass));
        Assert.That((int)GiftCodeResult.AlreadyUsedNetwork, Is.GreaterThan((int)legacy));
        Assert.That((int)GiftCodeResult.AlreadyUsedDevice, Is.GreaterThan((int)legacy));
        Assert.That((int)GiftCodeResult.NetworkAddressUnknown, Is.GreaterThan((int)legacy));
    }

    /// <summary>Each enabled identity limit applies across accounts and both entry points.</summary>
    /// <param name="network">Whether the network limit is enabled.</param>
    /// <param name="device">Whether the device limit is enabled.</param>
    /// <param name="expected">The second account's result.</param>
    [TestCase(true, false, GiftCodeResult.AlreadyUsedNetwork)]
    [TestCase(false, true, GiftCodeResult.AlreadyUsedDevice)]
    [TestCase(false, false, GiftCodeResult.Ok)]
    [TestCase(true, true, GiftCodeResult.AlreadyUsedNetwork)]
    public void LimitsApplyAcrossAccountsAndSources(bool network, bool device, GiftCodeResult expected)
    {
        var engine = Create(network, device);
        Assert.That(engine.Redeem("GIFT", Who("ann"), Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("GIFT", Who("bob"), Now, "website").Result, Is.EqualTo(expected));
        Assert.That(engine.Redeem("GIFT", Who("ann"), Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedAccount));
    }

    /// <summary>Both limits independently reject a reused identity, and refusals never reserve a new identity.</summary>
    [Test]
    public void BothLimitsAreIndependentAndRefusalsDoNotReserveClaims()
    {
        var engine = Create(true, true);
        engine.Redeem("GIFT", Who("ann"), Now, "game");
        var otherDevice = Guid.NewGuid().ToString();
        Assert.That(engine.Redeem("GIFT", Who("bob") with { DeviceId = otherDevice }, Now, "website").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedNetwork));
        Assert.That(engine.Redeem("GIFT", Who("bob") with { NetworkAddress = "203.0.113.13" }, Now, "website").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedDevice));
        Assert.That(engine.Redeem("GIFT", Who("bob") with { NetworkAddress = "203.0.113.13", DeviceId = otherDevice }, Now, "website").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.IdentityUsesOf("gift"), Is.EqualTo((2, 2)));
    }

    /// <summary>Legacy clients with no device id remain eligible, while network checks still apply.</summary>
    /// <param name="deviceId">The unknown device value.</param>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-uuid")]
    public void UnknownDevicesAreNotDeviceLimited(string? deviceId)
    {
        var engine = Create(false, true);
        Assert.That(engine.Redeem("GIFT", Who("ann") with { DeviceId = deviceId }, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("GIFT", Who("bob") with { DeviceId = deviceId }, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        engine.Store.Data.Codes[0].OncePerNetwork = true;
        Assert.That(engine.Redeem("GIFT", Who("cat") with { DeviceId = deviceId }, Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedNetwork));
        Assert.That(engine.IdentityUsesOf("gift"), Is.EqualTo((1, 0)));
    }

    /// <summary>Alternative textual representations cannot bypass address/device limits.</summary>
    [Test]
    public void IdentityRepresentationsAreCanonicalized()
    {
        var engine = Create(true, true);
        engine.Redeem("GIFT", Who("ann"), Now, "game");
        Assert.That(engine.Redeem("GIFT", Who("bob") with { NetworkAddress = "::ffff:203.0.113.12", DeviceId = Guid.NewGuid().ToString() }, Now, "website").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedNetwork));
        Assert.That(engine.Redeem("GIFT", Who("bob") with { NetworkAddress = "203.0.113.13", DeviceId = Device.ToUpperInvariant() }, Now, "website").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedDevice));
    }

    /// <summary>Missing addresses fail closed only when the network limit is on.</summary>
    [Test]
    public void MissingNetworkDoesNotBypassAnEnabledLimit()
    {
        var engine = Create(true, false);
        Assert.That(engine.Redeem("GIFT", Who("ann") with { NetworkAddress = null }, Now, "game").Result, Is.EqualTo(GiftCodeResult.NetworkAddressUnknown));
        Assert.That(engine.UsesOf("gift"), Is.Zero);
        engine.Store.Data.Codes[0].OncePerNetwork = false;
        Assert.That(engine.Redeem("GIFT", Who("ann") with { NetworkAddress = null }, Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
    }

    /// <summary>Claims are scoped to one code definition, including the existing batch-wide rules.</summary>
    [Test]
    public void DifferentCodesHaveSeparateLimits()
    {
        var engine = Create(true, true);
        engine.Store.Data.Codes.Add(new GiftCodeDefinition { Id = "other", Code = "OTHER", OncePerNetwork = true, OncePerDevice = true });
        engine.Store.CodesChanged();
        Assert.That(engine.Redeem("GIFT", Who("ann"), Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
        Assert.That(engine.Redeem("OTHER", Who("bob"), Now, "website").Result, Is.EqualTo(GiftCodeResult.Ok));
    }

    /// <summary>Competing claims for one network have exactly one winner.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ConcurrentClaimsAreAtomicAsync()
    {
        var engine = Create(true, true);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() =>
            engine.Redeem("GIFT", Who("account" + i) with { DeviceId = Guid.NewGuid().ToString() }, Now, i % 2 == 0 ? "game" : "website").Result))).ConfigureAwait(false);
        Assert.That(results.Count(r => r == GiftCodeResult.Ok), Is.EqualTo(1));
        Assert.That(results.Count(r => r == GiftCodeResult.AlreadyUsedNetwork), Is.EqualTo(15));
        Assert.That(engine.IdentityUsesOf("gift"), Is.EqualTo((1, 1)));
    }

    /// <summary>Old saves gain stable salt and optional fields without losing old uses or exposing raw identities.</summary>
    [Test]
    public void OldSaveFilesUpgradeAndPersistOnlyHashedIdentities()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-identities-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(file, "{\"Codes\":[{\"Id\":\"gift\",\"Code\":\"GIFT\"}],\"Uses\":{\"gift\":1},\"Claims\":{\"gift\":[\"a:old\",\"c:old\"]}}");
            var store = new GiftCodeStore(file);
            var engine = new GiftCodeEngine(new GiftCodesConfiguration(), store);
            Assert.That((store.Data.Codes[0].OncePerNetwork, store.Data.Codes[0].OncePerDevice), Is.EqualTo((false, false)));
            Assert.That(engine.UsesOf("gift"), Is.EqualTo(1));
            Assert.That(engine.IdentityUsesOf("gift"), Is.EqualTo((0, 0)));
            engine.Redeem("GIFT", Who("ann"), Now, "website");
            lock (store.Lock)
            {
                store.SaveIfChanged();
            }

            var saved = File.ReadAllText(file);
            Assert.That(saved, Does.Not.Contain(Network).And.Not.Contain(Device));
            var rows = HnmoPlayerStateRowStoreRegistry.GetBackend(file).LoadAll("giftCodes.claims");
            Assert.That(string.Join(string.Empty, rows.Select(r => r.PayloadJson)), Does.Not.Contain(Network).And.Not.Contain(Device));
            var reopened = new GiftCodeStore(file);
            Assert.That(reopened.Data.IdentitySalt, Has.Length.EqualTo(64).And.EqualTo(store.Data.IdentitySalt));
            Assert.That(reopened.Data.Claims["gift"], Does.Contain("a:old"));
            reopened.Data.Codes[0].OncePerNetwork = true;
            Assert.That(new GiftCodeEngine(new GiftCodesConfiguration(), reopened).Redeem("GIFT", Who("bob"), Now, "game").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedNetwork));
            Assert.That(new HnmoStateStore(file, "giftCodes").ReadSection<GiftCodeStoreData>("giftCodes", JsonSerializerOptions.Default)!.Uses["gift"], Is.EqualTo(2));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Separate installations use unrelated salted claims for the same identities.</summary>
    [Test]
    public void EachStoreHasItsOwnRandomSalt()
    {
        var first = Create(true, true);
        var second = Create(true, true);
        first.Redeem("GIFT", Who("ann"), Now, "game");
        second.Redeem("GIFT", Who("ann"), Now, "game");
        Assert.That(first.Store.Data.IdentitySalt, Is.Not.EqualTo(second.Store.Data.IdentitySalt));
        Assert.That(first.Store.Data.Claims["gift"].Single(c => c.StartsWith("n:", StringComparison.Ordinal)), Is.Not.EqualTo(second.Store.Data.Claims["gift"].Single(c => c.StartsWith("n:", StringComparison.Ordinal))));
    }

    /// <summary>Concurrent first opens create one shared store and one persistent salt.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ConcurrentFirstOpensShareThePersistentSaltAsync()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-first-open-{Guid.NewGuid():N}.json");
        try
        {
            var stores = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => GiftCodeStore.Open(file)))).ConfigureAwait(false);
            Assert.That(stores.All(s => ReferenceEquals(s, stores[0])), Is.True);
            Assert.That(new GiftCodeStore(file).Data.IdentitySalt, Is.EqualTo(stores[0].Data.IdentitySalt));
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Legacy and unified file names resolve to one live store even on concurrent first opens.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task UnifiedAndLegacyNamesShareOneStoreAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hnmo-gift-aliases-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var legacy = Path.Combine(directory, "gift-codes.json");
            var unified = Path.Combine(directory, HnmoStateStore.StateFileName);
            var stores = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() => GiftCodeStore.Open(i % 2 == 0 ? legacy : unified)))).ConfigureAwait(false);
            Assert.That(stores.All(s => ReferenceEquals(s, stores[0])), Is.True);
            Assert.That(new HnmoStateStore(unified).ReadSection<GiftCodeStoreData>("giftCodes", JsonSerializerOptions.Default)!.IdentitySalt, Is.EqualTo(stores[0].Data.IdentitySalt));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Migrating existing hashed claims preserves their original salt and network refusal after reopening.</summary>
    [Test]
    public void MigratesExistingSaltWithHashedClaims()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hnmo-gift-salt-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var original = Create(true, true);
            Assert.That(original.Redeem("GIFT", Who("ann"), Now, "game").Result, Is.EqualTo(GiftCodeResult.Ok));
            var legacy = Path.Combine(directory, "gift-codes.json");
            File.WriteAllText(legacy, JsonSerializer.Serialize(original.Store.Data));
            var migrated = new GiftCodeStore(legacy);
            var reopened = new GiftCodeStore(Path.Combine(directory, HnmoStateStore.StateFileName));
            Assert.That((migrated.Data.IdentitySalt, reopened.Data.IdentitySalt), Is.EqualTo((original.Store.Data.IdentitySalt, original.Store.Data.IdentitySalt)));
            Assert.That(new GiftCodeEngine(new GiftCodesConfiguration(), reopened).Redeem("GIFT", Who("bob"), Now, "website").Result, Is.EqualTo(GiftCodeResult.AlreadyUsedNetwork));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Existing identity hashes without a salt are refused instead of silently bypassing their limits.</summary>
    [Test]
    public void RefusesHashedClaimsWithoutTheirSalt()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hnmo-gift-missing-salt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(file, "{\"Claims\":{\"gift\":[\"n:old-hash\"]}}");
            Assert.Throws<InvalidDataException>(() => new GiftCodeStore(file));
        }
        finally
        {
            File.Delete(file);
            File.Delete(file + ".bak");
        }
    }

    /// <summary>A salt which cannot be persisted must not be used to accept new redemptions.</summary>
    [Test]
    public void RefusesAnUnwritableSaltFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hnmo-gift-no-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "blocked.json");
            Directory.CreateDirectory(file);
            Assert.Throws<IOException>(() => new GiftCodeStore(file));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static GiftCodeEngine Create(bool network, bool device)
    {
        var store = new GiftCodeStore();
        store.Data.Codes.Add(new GiftCodeDefinition { Id = "gift", Code = "GIFT", OncePerNetwork = network, OncePerDevice = device });
        store.CodesChanged();
        return new GiftCodeEngine(new GiftCodesConfiguration(), store);
    }

    private static GiftCodeRedeemer Who(string login) => new(login, "character-" + login, login, 50, 0, 0, Network, Device);
}
