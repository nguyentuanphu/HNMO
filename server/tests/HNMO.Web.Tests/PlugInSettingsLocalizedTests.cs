// <copyright file="PlugInSettingsLocalizedTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;

/// <summary>
/// Settings which hold a <see cref="HNMO.Interfaces.LocalizedString"/> must be readable by <see cref="PlugInSettings"/>.
/// </summary>
[TestFixture]
public class PlugInSettingsLocalizedTests
{
    /// <summary>Reads an invasion configuration whose start message is stored as a plain JSON string.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task ReadsStoredLocalizedStringAsPlainString()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        using (var context = persistence.CreateNewTypedContext(typeof(PlugInConfiguration), true))
        {
            var row = context.CreateNew<PlugInConfiguration>();
            row.TypeId = typeof(GoldenInvasionPlugIn).GUID;
            row.IsActive = true;
            row.CustomConfiguration = "{\"StartMessage\":\"Hello||vi=Xin chao\"}";
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var (exists, isActive, settings) = await PlugInSettings.ReadStoredAsync<PeriodicInvasionConfiguration>(
            persistence,
            typeof(GoldenInvasionPlugIn),
            default).ConfigureAwait(false);

        Assert.That(exists, Is.True);
        Assert.That(isActive, Is.True);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.StartMessage.Value, Is.EqualTo("Hello||vi=Xin chao"));

        var active = await PlugInSettings.ReadActiveAsync<PeriodicInvasionConfiguration>(persistence, typeof(GoldenInvasionPlugIn), default).ConfigureAwait(false);
        Assert.That(active, Is.Not.Null);
    }
}
