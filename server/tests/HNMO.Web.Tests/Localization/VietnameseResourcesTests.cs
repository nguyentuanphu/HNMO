// <copyright file="VietnameseResourcesTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Localization;

using System.Collections;
using System.Globalization;
using System.Resources;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization;

/// <summary>Checks that built satellite assemblies contain the Vietnamese resource coverage.</summary>
[TestFixture]
public sealed class VietnameseResourcesTests
{
    /// <summary>Every nonempty text resource is available directly in the Vietnamese satellite.</summary>
    /// <param name="representativeType">A type in the assembly whose resources are checked.</param>
    [TestCase(typeof(GameConfiguration))]
    [TestCase(typeof(WebsiteConfiguration))]
    [TestCase(typeof(LocalizedString))]
    [TestCase(typeof(DataInitializationBase))]
    public void NonemptyResourceTextsHaveVietnameseEntries(Type representativeType)
    {
        var assembly = representativeType.Assembly;
        var culture = CultureInfo.GetCultureInfo("vi");
        var count = 0;
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".resources", StringComparison.Ordinal)))
        {
            var manager = new ResourceManager(name[..^".resources".Length], assembly);
            var english = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
            var vietnamese = manager.GetResourceSet(culture, true, false);
            Assert.That(english, Is.Not.Null, name);
            Assert.That(vietnamese, Is.Not.Null, name);
            foreach (DictionaryEntry entry in english!)
            {
                if (entry.Value is not string text || string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                count++;
                Assert.That(vietnamese!.GetString((string)entry.Key), Is.Not.Null.And.Not.Empty, $"{name}: {entry.Key}");
            }

            manager.ReleaseAllResources();
        }

        Assert.That(count, Is.Positive);
    }
}
