// <copyright file="DuplicateItemsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using HNMO.GameLogic.PlugIns.DuplicateItems;
using HNMO.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// HNMO's duplicate check: serials follow copies, a scan turns two items with one serial into a frozen case, and an
/// administrator's decision unfreezes them.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class DuplicateItemsTest
{
    private string _file = null!;

    /// <summary>A state file of its own.</summary>
    [SetUp]
    public void SetUp()
    {
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-duplicates-{Guid.NewGuid():N}.json");
        DuplicateItemsPlugIn.Refresh(this._file);
    }

    /// <summary>Removes the state file and the frozen serials.</summary>
    [TearDown]
    public void TearDown()
    {
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }

        DuplicateItemsPlugIn.Refresh(this._file);
    }

    /// <summary>A copy of an item keeps its serial; an item gets one only once.</summary>
    [Test]
    public void ACopyKeepsTheSerial()
    {
        var item = new HNMO.Persistence.BasicModel.Item();
        Assert.That(DuplicateItemsPlugIn.GiveSerials([item]), Is.EqualTo(1));
        var serial = item.Serial;
        Assert.That(serial, Is.Not.Null);
        Assert.That(DuplicateItemsPlugIn.GiveSerials([item]), Is.EqualTo(0), "only once");
        Assert.That(item.Serial, Is.EqualTo(serial));

        var copy = new HNMO.Persistence.BasicModel.Item();
        copy.AssignValues(item);
        Assert.That(copy.Serial, Is.EqualTo(serial));
    }

    /// <summary>
    /// A scan which finds two items with one serial records a case and freezes both; another scan doesn't add it
    /// again; the decision unfreezes them and a later scan leaves the decided case alone.
    /// </summary>
    [Test]
    public void AScanFreezesDuplicatesUntilDecided()
    {
        var serial = Guid.NewGuid();
        var rows = new[]
        {
            new DuplicateItemRow(serial, Guid.NewGuid(), Guid.NewGuid(), "inventory", "Seller", "seller", "Sword of Test", 9),
            new DuplicateItemRow(serial, Guid.NewGuid(), Guid.NewGuid(), "vault", null, "buyer", "Sword of Test", 9),
        };
        var database = new FakeDatabase(rows);
        var item = new HNMO.Persistence.BasicModel.Item { Serial = serial };
        var other = new HNMO.Persistence.BasicModel.Item { Serial = Guid.NewGuid() };

        var added = DuplicateItemsPlugIn.Scan(database, this._file, NullLogger.Instance);
        Assert.That(added, Has.Count.EqualTo(1));
        Assert.That(added[0].Items.Select(i => i.AccountLogin), Is.EquivalentTo(new[] { "seller", "buyer" }));
        Assert.That(database.Assigned, Is.EqualTo(1), "the scan gives the missing serials first");
        Assert.That(DuplicateItemsPlugIn.IsFrozen(item), Is.True);
        Assert.That(DuplicateItemsPlugIn.IsFrozen(other), Is.False);

        Assert.That(DuplicateItemsPlugIn.Scan(database, this._file, NullLogger.Instance), Is.Empty, "known already");

        var decided = DuplicateItemsStore.Open(this._file).Mutate(data =>
        {
            var ok = DuplicateItemsEngine.Resolve(data, serial, "admin", "kept the older one", DateTime.UtcNow);
            return (ok, ok);
        });
        Assert.That(decided, Is.True);
        DuplicateItemsPlugIn.Refresh(this._file);
        Assert.That(DuplicateItemsPlugIn.IsFrozen(item), Is.False);

        Assert.That(DuplicateItemsPlugIn.Scan(database, this._file, NullLogger.Instance), Is.Empty);
        Assert.That(DuplicateItemsPlugIn.IsFrozen(item), Is.False, "a decided case stays decided");
    }

    /// <summary>With freezing switched off, the cases are recorded but nothing is frozen.</summary>
    [Test]
    public void FreezingCanBeSwitchedOff()
    {
        var serial = Guid.NewGuid();
        var database = new FakeDatabase(
        [
            new DuplicateItemRow(serial, Guid.NewGuid(), null, "held", null, null, "Ring", 0),
            new DuplicateItemRow(serial, Guid.NewGuid(), null, "held", null, null, "Ring", 0),
        ]);
        DuplicateItemsPlugIn.Scan(database, this._file, NullLogger.Instance);
        DuplicateItemsPlugIn.Refresh(this._file, freeze: false);
        Assert.That(DuplicateItemsPlugIn.IsFrozen(new HNMO.Persistence.BasicModel.Item { Serial = serial }), Is.False);
    }

    private sealed class FakeDatabase(IReadOnlyList<DuplicateItemRow> rows) : IItemSerialDatabase
    {
        public int Assigned { get; private set; }

        public int AssignMissing()
        {
            this.Assigned++;
            return 0;
        }

        public IReadOnlyList<DuplicateItemRow> FindDuplicates() => rows;
    }
}
