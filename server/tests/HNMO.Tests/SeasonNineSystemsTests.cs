// <copyright file="SeasonNineSystemsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>
/// Unit tests for <see cref="SeasonNineSystemsCatalog"/> and <see cref="SeasonNineSystemsEngine"/>.
/// </summary>
[TestFixture]
public class SeasonNineSystemsTests
{
    /// <summary>Verifies exported Muun, Pentagram, and Errtel catalog counts match the Season 9 server suite.</summary>
    [Test]
    public void CatalogCountsMatchSeasonNineSuite()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SeasonNineSystemsCatalog.Muuns, Has.Count.EqualTo(76));
            Assert.That(SeasonNineSystemsCatalog.MuunOptions, Has.Count.EqualTo(37));
            Assert.That(SeasonNineSystemsCatalog.Pentagrams, Has.Count.EqualTo(15));
            Assert.That(SeasonNineSystemsCatalog.ErrtelOptions, Has.Count.GreaterThanOrEqualTo(60));
            Assert.That(SeasonNineSystemsCatalog.ErrtelExtractionRates, Has.Count.EqualTo(55));
        });
    }

    /// <summary>Verifies Muun companion condition checking and option scaling.</summary>
    [Test]
    public void EvaluatesMuunBonusAndConditions()
    {
        var luki = SeasonNineSystemsEngine.FindMuunByItem(13, 212)!.Value;
        var insideHours = SeasonNineSystemsEngine.EvaluateMuunBonus(
            luki,
            muunLevel: 1,
            evolved: false,
            mapNumber: 0,
            totalLevel: 100,
            localNow: new DateTime(2026, 11, 26, 20, 0, 0),
            strictConditions: true);
        var outsideHours = SeasonNineSystemsEngine.EvaluateMuunBonus(
            luki,
            muunLevel: 1,
            evolved: false,
            mapNumber: 0,
            totalLevel: 100,
            localNow: new DateTime(2026, 11, 26, 10, 0, 0),
            strictConditions: true);
        var evolvedNonStrict = SeasonNineSystemsEngine.EvaluateMuunBonus(
            luki,
            muunLevel: 1,
            evolved: true,
            mapNumber: 0,
            totalLevel: 100,
            localNow: new DateTime(2026, 11, 26, 10, 0, 0),
            strictConditions: false);

        Assert.Multiple(() =>
        {
            Assert.That(insideHours, Is.EqualTo(44));
            Assert.That(outsideHours, Is.EqualTo(0));
            Assert.That(evolvedNonStrict, Is.EqualTo(76));
            Assert.That(SeasonNineSystemsEngine.CalculateMuunEnergyConversionPoints(1, 0, 1.0), Is.EqualTo(5));
        });
    }

    /// <summary>Verifies Pentagram elemental triangle, Errtel extraction rate bonus, and Shield-Burn damage.</summary>
    [Test]
    public void ComputesPentagramElementalMatrixErrtelAndShieldBurn()
    {
        var cfg = new SeasonNineSystemsConfiguration();
        Assert.Multiple(() =>
        {
            Assert.That(cfg.Enabled, Is.False);
            Assert.That(cfg.MasterLevelMaxCap, Is.EqualTo(330));
            Assert.That(SeasonNineSystemsEngine.CalculateElementalMultiplier(PentagramElement.Fire, PentagramElement.Darkness, 15, 1.0), Is.EqualTo(1.15).Within(0.001));
            Assert.That(SeasonNineSystemsEngine.CalculateElementalMultiplier(PentagramElement.Darkness, PentagramElement.Fire, 15, 1.0), Is.EqualTo(0.925).Within(0.001));
            Assert.That(SeasonNineSystemsEngine.CalculateErrtelExtractionRate(1, 0, 0), Is.EqualTo(10000));
            Assert.That(SeasonNineSystemsEngine.CalculateErrtelExtractionRate(1, 3, 5), Is.EqualTo(10000));
            Assert.That(SeasonNineSystemsEngine.CalculateShieldBurnReduction(1000, 25), Is.EqualTo(250));
        });
    }
}
