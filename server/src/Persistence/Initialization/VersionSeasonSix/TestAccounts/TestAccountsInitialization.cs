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
    /// Initializes a new instance of the <see cref="TestAccountsInitialization"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public TestAccountsInitialization(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <summary>
    /// Seeds equivalent accounts reserved for the social browser test.
    /// </summary>
    /// <param name="provider">The isolated demo persistence provider.</param>
    public static async Task SeedSocialAccountsAsync(InMemory.InMemoryPersistenceContextProvider provider)
    {
        using var context = provider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        const string master = "socmas";
        const string member = "socmem";
        new Level300(context, gameConfiguration, master).Initialize();
        new LowLevel(context, gameConfiguration, member, 41).Initialize();

        // These are established characters. Keep first-time onboarding rewards out of the warp fixture.
        var accounts = await context.GetAsync<Account>().ConfigureAwait(false);
        foreach (var character in accounts.Where(account => account.LoginName is master or member).SelectMany(account => account.Characters))
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