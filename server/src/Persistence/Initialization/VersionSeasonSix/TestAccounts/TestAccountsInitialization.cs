// <copyright file="TestAccountsInitialization.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.TestAccounts;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;

/// <summary>
/// Initializes the test accounts.
/// </summary>
public class TestAccountsInitialization : InitializerBase
{
    /// <summary>
    /// The login name (and password) of the guild master of the social browser test's own pair of accounts.
    /// </summary>
    public const string SocialMasterName = "socmas";

    /// <summary>
    /// The login name (and password) of the guild member of the social browser test's own pair of accounts.
    /// </summary>
    public const string SocialMemberName = "socmem";

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAccountsInitialization"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public TestAccountsInitialization(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <summary>
    /// Adds the social browser test's own pair of accounts to a demo database: a level 300 guild master and a level 41
    /// member, like <c>test300</c> and <c>test4</c>. They are established characters (created long ago), so they get no
    /// first-time rewards when they enter, and no other test uses them, so the guild, friend and letter state of one
    /// run can't reach another test.
    /// </summary>
    /// <param name="provider">The persistence provider of the demo database (in memory).</param>
    /// <returns>The task.</returns>
    public static async Task SeedSocialPairAsync(HNMO.Persistence.InMemory.InMemoryPersistenceContextProvider provider)
    {
        using var context = provider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        new Level300(context, gameConfiguration, SocialMasterName).Initialize();
        new LowLevel(context, gameConfiguration, SocialMemberName, 41).Initialize();

        var accounts = await context.GetAsync<Account>().ConfigureAwait(false);
        foreach (var character in accounts.Where(account => account.LoginName is SocialMasterName or SocialMemberName).SelectMany(account => account.Characters))
        {
            character.CreateDate = DateTime.UnixEpoch;
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        for (int i = 0; i < 10; i++)
        {
            var level = (i * 10) + 1;
            new TestAccounts.LowLevel(this.Context, this.GameConfiguration, "test" + i, level).Initialize();
        }

        new TestAccounts.Level300(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Level400(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Ancient(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Socket(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Quest150(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Quest220(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Quest400(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.GameMaster(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.GameMaster2(this.Context, this.GameConfiguration).Initialize();
        new TestAccounts.Unlocked(this.Context, this.GameConfiguration).Initialize();
    }
}