// <copyright file="StoreSafetyTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.GameLogic.PlugIns.JewelBank;

/// <summary>
/// Tests that the shared state file can't lose or double a player's goods: an owed escrow delivery is handed
/// over once, and changes made through different wrappers of the same file don't overwrite each other.
/// </summary>
[TestFixture]
public class StoreSafetyTests
{
    private string _tempFile = string.Empty;

    /// <summary>Picks a fresh state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempFile = Path.Combine(Path.GetTempPath(), $"hnmo-store-safety-{Guid.NewGuid():N}.json");
    }

    /// <summary>Removes the state file and its backup.</summary>
    [TearDown]
    public void TearDown()
    {
        foreach (var file in new[] { this._tempFile, this._tempFile + ".bak" }.Where(File.Exists))
        {
            File.Delete(file);
        }
    }

    /// <summary>An owed delivery can be taken once; a second claim finds nothing, so its jewels are paid once.</summary>
    [Test]
    public void OwedDeliveryIsTakenOnlyOnce()
    {
        var store = EscrowStore.Open(this._tempFile);
        var engine = new EscrowEngine(new EscrowConfiguration(), store);
        store.Mutate(data =>
        {
            data.OwedDeliveries.Add(new EscrowOwedDelivery
            {
                DeliveryId = "d1",
                RecipientLogin = "seller",
                Jewels = [new EscrowJewelLine { JewelKey = "bless", Quantity = 30 }],
            });
            return (true, true);
        });

        var first = engine.TakeOwedDelivery("d1");
        var second = engine.TakeOwedDelivery("d1");

        Assert.That(first, Is.Not.Null);
        Assert.That(first!.Jewels.Single().Quantity, Is.EqualTo(30));
        Assert.That(second, Is.Null);
        Assert.That(engine.PeekOwedDeliveries("seller"), Is.Empty);
    }

    /// <summary>A delivery whose goods didn't fit goes back whole, jewels included, and isn't doubled by a second restore.</summary>
    [Test]
    public void RestoredDeliveryKeepsItsJewelsAndIsNotDoubled()
    {
        var store = EscrowStore.Open(this._tempFile);
        var engine = new EscrowEngine(new EscrowConfiguration(), store);
        var delivery = new EscrowOwedDelivery
        {
            DeliveryId = "d2",
            RecipientLogin = "seller",
            Jewels = [new EscrowJewelLine { JewelKey = "soul", Quantity = 7 }],
        };

        engine.RestoreOwedDelivery(delivery);
        engine.RestoreOwedDelivery(delivery);

        var owed = engine.PeekOwedDeliveries("seller");
        Assert.That(owed, Has.Count.EqualTo(1));
        Assert.That(owed[0].Jewels.Single().Quantity, Is.EqualTo(7));
    }

    /// <summary>
    /// Every game server, website request and admin page opens its own wrapper of the same file. Their
    /// read-change-write steps must not interleave, or a deposit made in between is lost.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ChangesThroughDifferentWrappersAreNotLostAsync()
    {
        const int wrappers = 8;
        const int depositsPerWrapper = 50;
        var tasks = Enumerable.Range(0, wrappers).Select(_ => Task.Run(() =>
        {
            var store = JewelBankStore.Open(this._tempFile);
            for (var i = 0; i < depositsPerWrapper; i++)
            {
                store.Mutate(data =>
                {
                    if (!data.Accounts.TryGetValue("player", out var balance))
                    {
                        balance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        data.Accounts["player"] = balance;
                    }

                    balance.TryGetValue("bless", out var have);
                    balance["bless"] = have + 1;
                    return (true, true);
                });
            }
        })).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var total = JewelBankStore.Open(this._tempFile).Read(data => data.Accounts["player"]["bless"]);
        Assert.That(total, Is.EqualTo(wrappers * depositsPerWrapper));
    }
}
