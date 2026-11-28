// <copyright file="SocialTestAccountsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Entities;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Initialization.VersionSeasonSix.TestAccounts;

/// <summary>
/// The social browser test's own pair of accounts: no other test uses them, and they are established characters, so
/// the guild, friend and letter state of one run can't reach another test and no first-time reward disturbs the warp.
/// </summary>
[TestFixture]
internal class SocialTestAccountsTest
{
    /// <summary>The pair is added next to the usual test accounts, with characters that were created long ago.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PairIsAddedWithEstablishedCharactersAsync()
    {
        var provider = await CreateDemoDatabaseAsync().ConfigureAwait(false);

        await TestAccountsInitialization.SeedSocialPairAsync(provider).ConfigureAwait(false);

        using var context = provider.CreateNewContext();
        var accounts = (await context.GetAsync<Account>().ConfigureAwait(false)).ToList();
        var master = accounts.Single(account => account.LoginName == TestAccountsInitialization.SocialMasterName);
        var member = accounts.Single(account => account.LoginName == TestAccountsInitialization.SocialMemberName);
        Assert.Multiple(() =>
        {
            Assert.That(TestAccountsInitialization.SocialMasterName, Is.EqualTo("socmas"));
            Assert.That(TestAccountsInitialization.SocialMemberName, Is.EqualTo("socmem"));
            Assert.That(master.Characters, Is.Not.Empty);
            Assert.That(member.Characters, Is.Not.Empty);
            Assert.That(master.Characters.Concat(member.Characters).Select(character => character.CreateDate), Has.All.EqualTo(DateTime.UnixEpoch), "no first-time rewards");
            Assert.That(accounts.Select(account => account.LoginName), Does.Contain("test300").And.Contain("test4"), "the usual test accounts stay");
        });
    }

    /// <summary>The usual test accounts keep their characters and creation dates: only the pair is changed.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UsualTestAccountsAreNotChangedAsync()
    {
        var provider = await CreateDemoDatabaseAsync().ConfigureAwait(false);
        var before = await CreateDatesAsync(provider).ConfigureAwait(false);

        await TestAccountsInitialization.SeedSocialPairAsync(provider).ConfigureAwait(false);

        var after = await CreateDatesAsync(provider).ConfigureAwait(false);
        Assert.That(after["test300"], Is.EqualTo(before["test300"]));
        Assert.That(after["test4"], Is.EqualTo(before["test4"]));
        Assert.That(after["test300"], Has.None.EqualTo(DateTime.UnixEpoch), "test300 is not made an established account");
    }

    private static async Task<InMemoryPersistenceContextProvider> CreateDemoDatabaseAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        return provider;
    }

    private static async Task<Dictionary<string, List<DateTime>>> CreateDatesAsync(InMemoryPersistenceContextProvider provider)
    {
        using var context = provider.CreateNewContext();
        var accounts = await context.GetAsync<Account>().ConfigureAwait(false);
        return accounts
            .Where(account => account.LoginName is "test300" or "test4")
            .ToDictionary(account => account.LoginName, account => account.Characters.Select(character => character.CreateDate).ToList());
    }
}
