// <copyright file="GiftBoxStateWriterTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Checks owned, bilingual, packet-bounded inbox projections and confirmed collection status.</summary>
[TestFixture]
public class GiftBoxStateWriterTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);
    private IPlayerContext _context = null!;
    private GameConfiguration _game = null!;
    private Account _account = null!;
    private Character _character = null!;
    private GiftBoxDispatch _box = null!;

    /// <summary>Creates an isolated owned mailbox and real account entities.</summary>
    [SetUp]
    public void SetUp()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        this._game.MaximumInventoryMoney = 2000000000;
        this._context = provider.CreateNewPlayerContext(this._game);
        this._account = this._context.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.PasswordHash = "private-password-hash";
        this._character = this._context.CreateNew<Character>();
        this._character.Name = "AnnHero";
        this._character.Inventory = this._context.CreateNew<ItemStorage>();
        this._account.Characters.Add(this._character);
        this._box = new(new InMemoryHnmoPlayerStateRowBackend());
    }

    /// <summary>Disposes the owned context.</summary>
    [TearDown]
    public void TearDown() => this._context.Dispose();

    /// <summary>Only the authenticated account's selected language is returned; private host fields stay absent.</summary>
    /// <param name="language">The supported display language.</param>
    /// <param name="title">The expected localized title.</param>
    /// <returns>The test task.</returns>
    [TestCase("en", "Gift")]
    [TestCase("vi", "Quà")]
    public async Task ProjectionIsOwnedAndLocalizedAsync(string language, string title)
    {
        var letter = this.Send(this._account.GetId());
        var foreign = this.Send(Guid.NewGuid());
        var json = await this.WriteAsync(new("state", LetterId: letter, Language: language)).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.GetProperty("letters").GetArrayLength(), Is.EqualTo(1));
        Assert.That(document.RootElement.GetProperty("detail").GetProperty("title").GetString(), Is.EqualTo(title));
        Assert.That(json, Does.Not.Contain("private-host").And.Not.Contain("private-reason").And.Not.Contain("private-password-hash").And.Not.Contain(this._account.GetId().ToString()));
        using var other = JsonDocument.Parse(await this.WriteAsync(new("state", LetterId: foreign)).ConfigureAwait(false));
        Assert.That(other.RootElement.GetProperty("detail").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    /// <summary>Inbox pages contain20 compact summaries and one bounded selected detail even at maximum message sizes.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task WorstCaseTextAndOptionsFitPacketBudgetAsync()
    {
        using var setup = new InMemoryPersistenceContextProvider().CreateNewContext();
        var definition = setup.CreateNew<ItemDefinition>();
        definition.Group = 0;
        definition.Number = 1;
        definition.Name = new string('界', 200);
        this._game.Items.Add(definition);
        Guid selected = default;
        for (var index = 0; index < 21; index++)
        {
            selected = Guid.NewGuid();
            this._box.Send(selected, "private-host", "private-reason", [this._account.GetId()], new string('界', 100), new string('界', 100), new string('界', 2000), new string('界', 2000), new()
            {
                Coins = 1000000,
                Items = Enumerable.Range(0, 50).Select(_ => new GiftCodeItem { Group = 0, Number = 1, Count = 1, Level = 15, ExcellentOptions = 63, OptionLevel = 7 }).ToList(),
            }, Now);
        }

        var json = await this.WriteAsync(new("state", LetterId: selected, Language: "vi")).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.GetProperty("letters").GetArrayLength(), Is.EqualTo(20));
        Assert.That(document.RootElement.GetProperty("hasMore").GetBoolean(), Is.True);
        Assert.That(document.RootElement.GetProperty("detail").GetProperty("items").GetArrayLength(), Is.EqualTo(50));
        Assert.That(Encoding.UTF8.GetByteCount(json), Is.LessThan(60000));
        using var next = JsonDocument.Parse(await this.WriteAsync(new("state", Offset: 20)).ConfigureAwait(false));
        Assert.That(next.RootElement.GetProperty("letters").GetArrayLength(), Is.EqualTo(1));
        Assert.That(next.RootElement.GetProperty("hasMore").GetBoolean(), Is.False);
    }

    /// <summary>Staged proof after a failed save is never presented as confirmed collection.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task UncertainReceiptIsNotShownAsCollectedAsync()
    {
        var letter = this.Send(this._account.GetId());
        var collection = new GiftBoxCollection(this._box, this._game, NullLogger.Instance);
        var result = await collection.CollectAsync(this._context, this._account, letter, this._character.GetId(), Now, () => ValueTask.FromResult(false)).ConfigureAwait(false);
        Assert.That(result.Status, Is.EqualTo("Pending"));
        using var pending = JsonDocument.Parse(await this.WriteAsync(new("state", LetterId: letter)).ConfigureAwait(false));
        Assert.That(pending.RootElement.GetProperty("detail").GetProperty("collected").GetBoolean(), Is.False);
        Assert.That(pending.RootElement.GetProperty("status").GetString(), Is.EqualTo("Pending"));
        await collection.CollectAsync(this._context, this._account, letter, this._character.GetId(), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        using var confirmed = JsonDocument.Parse(await this.WriteAsync(new("state", LetterId: letter)).ConfigureAwait(false));
        Assert.That(confirmed.RootElement.GetProperty("detail").GetProperty("collected").GetBoolean(), Is.True);
        Assert.That(confirmed.RootElement.GetProperty("detail").GetProperty("collectedBy").GetString(), Is.EqualTo("AnnHero"));
    }

    /// <summary>Unsupported actions, languages, offsets and empty identifiers are refused before reading mail.</summary>
    [Test]
    public void RequestValidationIsBounded()
    {
        Assert.That(new GiftBoxRequest("state").IsValid, Is.True);
        Assert.That(new GiftBoxRequest("collect").IsValid, Is.False);
        Assert.That(new GiftBoxRequest("send").IsValid, Is.False);
        Assert.That(new GiftBoxRequest("state", Offset: -1).IsValid, Is.False);
        Assert.That(new GiftBoxRequest("state", Offset: 100001).IsValid, Is.False);
        Assert.That(new GiftBoxRequest("state", Language: "other").IsValid, Is.False);
        Assert.That(new GiftBoxRequest("state", LetterId: Guid.Empty).IsValid, Is.False);
    }

    private Guid Send(Guid account)
    {
        var id = Guid.NewGuid();
        this._box.Send(id, "private-host", "private-reason", [account], "Gift", "Quà", "Thank you.", "Cảm ơn bạn.", new() { Coins = 10 }, Now);
        return id;
    }

    private ValueTask<string> WriteAsync(GiftBoxRequest request) => GiftBoxStateWriter.WriteAsync(this._box, this._game, this._context, this._account, request, Now);
}
