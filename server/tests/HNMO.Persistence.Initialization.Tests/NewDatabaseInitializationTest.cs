// <copyright file="NewDatabaseInitializationTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;

/// <summary>
/// Initializes a new Season 6 database with an update of a plugin which runs on a new database
/// (<see cref="IConfigurationUpdatePlugIn.RunsOnNewDatabase"/>): it's applied, while the updates of HNMO itself are only
/// marked as installed.
/// </summary>
[TestFixture]
[NonParallelizable]
internal class NewDatabaseInitializationTest
{
    /// <summary>
    /// The plugin's update adds its item and is installed; HNMO's own Rena update is installed without adding the
    /// Rena item a second time.
    /// </summary>
    [Test]
    public async Task PlugInUpdateIsAppliedOnANewDatabaseAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        NewDatabaseTestUpdatePlugIn.IsActive = true;
        try
        {
            var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
            await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        }
        finally
        {
            NewDatabaseTestUpdatePlugIn.IsActive = false;
        }

        using var context = contextProvider.CreateNewContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var updates = (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).ToList();

        Assert.That(gameConfiguration.Items.Count(item => item.Group == NewDatabaseTestUpdatePlugIn.ItemGroup && item.Number == NewDatabaseTestUpdatePlugIn.ItemNumber), Is.EqualTo(1));
        Assert.That(updates.Single(update => update.Key == typeof(NewDatabaseTestUpdatePlugIn).GUID).InstalledAt, Is.Not.Null);

        Assert.That(gameConfiguration.Items.Count(item => item.Group == 14 && item.Number == 21), Is.EqualTo(1), "the Rena item, once");
        Assert.That(updates.Single(update => update.Key == typeof(AddRenaItemUpdatePlugIn).GUID).InstalledAt, Is.Not.Null);
    }
}

/// <summary>
/// An update of a plugin, which adds an item the initialization doesn't contain. It's discovered by every Season 6
/// initialization of these tests, so it only adds the item while <see cref="IsActive"/>.
/// </summary>
[PlugIn]
[Display(Name = "New database test update", Description = "Adds a test item to a new database.")]
[Guid("C3B1E7A4-6F28-4D95-9A0E-2D8B5F14C6E3")]
internal sealed class NewDatabaseTestUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The group of the item the update adds.
    /// </summary>
    public const byte ItemGroup = 15;

    /// <summary>
    /// The number of the item the update adds, which the initialization doesn't use.
    /// </summary>
    public const short ItemNumber = 500;

    /// <summary>
    /// Gets or sets a value indicating whether the update adds its item.
    /// </summary>
    public static bool IsActive { get; set; }

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "New database test update";

    /// <inheritdoc />
    public override string Description => "Adds a test item to a new database.";

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override bool RunsOnNewDatabase => true;

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (IsActive)
        {
            var item = context.CreateNew<ItemDefinition>();
            item.Name = "New database test item";
            item.Group = ItemGroup;
            item.Number = ItemNumber;
            gameConfiguration.Items.Add(item);
        }

        return ValueTask.CompletedTask;
    }
}
