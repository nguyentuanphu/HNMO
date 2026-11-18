// <copyright file="FixedItemOptionsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.FixedItemOptions;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.PlugIns;

/// <summary>
/// Fixed Item Options plug-in (OFF by default): pins chosen options (Skill, Luck, Level, Option,
/// or Excellent options) onto matching items generated from drops, event boxes, or mini-game rewards.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Fixed Item Options", Description = "Lets admins pin Skill, Luck, minimum upgrade/option levels, or Excellent options on chosen items (OFF by default).")]
[Guid("7C2E9D14-5B8A-43F1-90E6-2A7D4F1B8C35")]
public sealed class FixedItemOptionsPlugIn :
    IFeaturePlugIn,
    ISupportCustomConfiguration<FixedItemOptionsConfiguration>,
    ISupportDefaultCustomConfiguration,
    IDisabledByDefault
{
    /// <inheritdoc />
    public FixedItemOptionsConfiguration? Configuration { get; set; }

    /// <summary>Gets the active <see cref="FixedItemOptionsPlugIn"/> for <paramref name="gameContext"/>, or <c>null</c> when disabled.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The active plug-in, or <c>null</c>.</returns>
    public static FixedItemOptionsPlugIn? Get(IGameContext gameContext) =>
        gameContext.FeaturePlugIns.GetPlugIn<FixedItemOptionsPlugIn>();

    /// <summary>Applies active fixed item option rules to every item in <paramref name="items"/>.</summary>
    /// <param name="gameContext">Optional game context; no-op when null or plug-in inactive.</param>
    /// <param name="items">Generated items.</param>
    /// <param name="source">Origin source (<c>"drop"</c> or <c>"box_event"</c>).</param>
    /// <param name="mapNumber">Optional current map number.</param>
    /// <param name="randomizer">Optional randomizer for extra excellent options.</param>
    public static void ApplyToItems(
        IGameContext? gameContext,
        IEnumerable<Item> items,
        string source,
        short? mapNumber,
        IRandomizer? randomizer = null)
    {
        if (gameContext is null || Get(gameContext) is not { } plugIn)
        {
            return;
        }

        var cfg = plugIn.Configuration ?? new FixedItemOptionsConfiguration();
        foreach (var item in items)
        {
            FixedItemOptionsEngine.ApplyRules(item, cfg, plugInActive: true, source, mapNumber, randomizer);
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new FixedItemOptionsConfiguration();
}
