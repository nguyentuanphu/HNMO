// <copyright file="MayaBodyNonAttackableDataTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Updates;

/// <summary>
/// Tests the configuration data and mandatory update for Maya's non-attackable body (#364).
/// </summary>
[TestFixture]
internal class MayaBodyNonAttackableDataTest
{
    private const short MayaBodyNumber = 364;

    /// <summary>
    /// Verifies that a newly initialized Season 6 database defines Maya (#364) as <see cref="NpcObjectKind.PassiveNpc"/> with 0 drops.
    /// </summary>
    [Test]
    public async Task NewDatabaseDefinesMayaAsPassiveNpcAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var maya = config.Monsters.Single(m => m.Number == MayaBodyNumber);
        Assert.That(maya.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
        Assert.That(maya.NumberOfMaximumItemDrops, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that <see cref="MayaBodyNonAttackableUpdatePlugIn"/> updates legacy <see cref="NpcObjectKind.Monster"/>
    /// definitions with 7 drops while preserving customized values.
    /// </summary>
    [Test]
    public async Task UpdateMigratesOldDefaultAndPreservesCustomizationsAsync()
    {
        var (context, config) = await TestGameConfiguration.CreateSeason6Async().ConfigureAwait(false);
        using var _ = context;

        var maya = config.Monsters.Single(m => m.Number == MayaBodyNumber);
        maya.ObjectKind = NpcObjectKind.Monster;
        maya.NumberOfMaximumItemDrops = 7;

        var plugin = new MayaBodyNonAttackableUpdatePlugIn();
        Assert.That(plugin.IsMandatory, Is.True);
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);

        Assert.That(maya.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
        Assert.That(maya.NumberOfMaximumItemDrops, Is.EqualTo(0));

        // Customized values must not be overwritten.
        maya.ObjectKind = NpcObjectKind.Statue;
        maya.NumberOfMaximumItemDrops = 3;
        await plugin.ApplyUpdateAsync(context, config).ConfigureAwait(false);
        Assert.That(maya.ObjectKind, Is.EqualTo(NpcObjectKind.Statue));
        Assert.That(maya.NumberOfMaximumItemDrops, Is.EqualTo(3));
    }

    /// <summary>
    /// Verifies update captions exist in en, vi, de, and zh-CN.
    /// </summary>
    [Test]
    public void CaptionsExistInAllFourLanguages()
    {
        var plugin = new MayaBodyNonAttackableUpdatePlugIn();
        var prev = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "en", "vi", "de", "zh-CN" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.That(plugin.Name, Is.Not.Null.And.Not.Empty, culture);
                Assert.That(plugin.Description, Is.Not.Null.And.Not.Empty, culture);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }
    }
}
