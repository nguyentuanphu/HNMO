// <copyright file="GiftBoxItemPickerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using Bunit;
using HNMO.DataModel.Configuration.Items;
using HNMO.Interfaces;
using HNMO.Web.Shared.Components.ItemPicker;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Resources = HNMO.Web.Shared.Properties.Resources;

/// <summary>Reuses the real catalogue while removing misleading shop controls from gift selection.</summary>
[TestFixture]
[NonParallelizable]
public class GiftBoxItemPickerTests
{
    /// <summary>The gift variant searches bilingual names, selects real definitions and has truthful localized hints.</summary>
    /// <param name="language">The administrator language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public async Task GiftModeUsesLocalizedHintsAndSelectsDefinitionsAsync(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        var previousResource = Resources.Culture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Resources.Culture = CultureInfo.CurrentUICulture;
            using var ui = new BunitContext();
            ui.JSInterop.Mode = JSRuntimeMode.Loose;
            var definition = Definition();
            var chosen = new List<ItemDefinition>();
            Action<ItemDefinition> choose = chosen.Add;
            var cut = ui.Render<ItemPicker>(parameters => parameters.Add(component => component.ShopMode, false)
                .Add(component => component.Definitions, new[] { definition }).Add(component => component.Add, choose));
            cut.WaitForAssertion(() => Assert.That(cut.FindAll(".picker-add"), Has.Count.EqualTo(1)));
            Assert.That(cut.FindAll(".picker-missing"), Is.Empty);
            Assert.That(cut.Find(".picker-entry").GetAttribute("draggable"), Is.EqualTo("false"));
            Assert.That(cut.Find(".picker-entry").GetAttribute("title"), Is.EqualTo(language == "vi" ? "Chọn vật phẩm để thêm vào quà này." : "Choose an item to add to this gift."));
            Assert.That(cut.Find(".picker-add").GetAttribute("title"), Is.EqualTo(language == "vi" ? "Thêm Sword vào quà này." : "Add Sword to this gift."));
            await cut.Find(".picker-search").InputAsync(new ChangeEventArgs { Value = "kiếm" }).ConfigureAwait(false);
            cut.WaitForAssertion(() => Assert.That(cut.Instance.Matches.Single(), Is.SameAs(definition)));
            await cut.Find(".picker-add").ClickAsync(new MouseEventArgs()).ConfigureAwait(false);
            Assert.That(chosen, Is.EqualTo(new[] { definition }));
            Assert.That(definition.Group, Is.Zero);
            Assert.That(definition.Number, Is.Zero);
        }
        finally
        {
            Resources.Culture = previousResource;
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Existing shop callers keep the stock filter and grid dragging unless they explicitly choose gift mode.</summary>
    [Test]
    public void DefaultModeRetainsShopControls()
    {
        using var ui = new BunitContext();
        ui.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = ui.Render<ItemPicker>(parameters => parameters.Add(component => component.Definitions, new[] { Definition() }));
        cut.WaitForAssertion(() => Assert.That(cut.FindAll(".picker-add"), Has.Count.EqualTo(1)));
        Assert.That(cut.FindAll(".picker-missing"), Has.Count.EqualTo(1));
        Assert.That(cut.Find(".picker-entry").GetAttribute("draggable"), Is.EqualTo("true"));
    }

    private static ItemDefinition Definition() => new HNMO.Persistence.BasicModel.ItemDefinition
    {
        Group = 0,
        Number = 0,
        Width = 1,
        Height = 1,
        MaximumItemLevel = 15,
        IsActive = true,
        Name = new LocalizedString("Sword").WithTranslation(CultureInfo.GetCultureInfo("vi"), "Kiếm"),
    };
}
