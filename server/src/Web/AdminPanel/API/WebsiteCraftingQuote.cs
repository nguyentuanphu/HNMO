// <copyright file="WebsiteCraftingQuote.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Reflection;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;

/// <summary>Reads built-in handler metadata without instantiating handlers or touching player inventories.</summary>
internal static class WebsiteCraftingQuote
{
    private static readonly Type SimpleType = typeof(SimpleItemCraftingHandler);

    /// <summary>Checks whether the configured built-in handler inherits the settings-based price and chance paths.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>Whether its base/cap and price formula may be published as settings-backed facts.</returns>
    internal static bool UsesSettings(ItemCrafting recipe)
    {
        if (recipe.SimpleCraftingSettings is null || Resolve(recipe) is not { } type || !SimpleType.IsAssignableFrom(type))
        {
            return false;
        }

        var required = type.GetMethod(nameof(SimpleItemCraftingHandler.TryGetRequiredItems));
        var price = type.GetMethod("GetPrice", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(byte), typeof(IList<CraftingRequiredItemLink>)], null);
        return required?.DeclaringType == SimpleType && price?.DeclaringType == SimpleType;
    }

    /// <summary>Checks that an inherited excellent roll really reads the simple settings.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>Whether the roll may be published; specialized rolls remain unknown.</returns>
    internal static bool UsesSettingsExcellentRoll(ItemCrafting recipe)
        => Resolve(recipe)?.GetMethod("AddRandomExcellentOptions", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(Item), typeof(Player)], null)?.DeclaringType == SimpleType;

    private static Type? Resolve(ItemCrafting recipe)
        => string.IsNullOrWhiteSpace(recipe.ItemCraftingHandlerClassName) ? SimpleType : SimpleType.Assembly.GetType(recipe.ItemCraftingHandlerClassName, false);
}
