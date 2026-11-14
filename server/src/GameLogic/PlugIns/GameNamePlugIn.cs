// <copyright file="GameNamePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Holds the game's name which players see (the page title, the login screen, the guide, the item shop).
/// The admin panel changes it in the plug-in's configuration; the web game reads it through the
/// admin panel's public endpoint <c>api/game-name</c>.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.GameNamePlugIn_Name), Description = nameof(Properties.PlugInResources.GameNamePlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("5B7E2C1A-9D43-4F6E-8A21-3C0F7D9B4E62")]
public class GameNamePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<GameNameConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public GameNameConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the game's name of a game context: the configured one, else the default.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The game's name.</returns>
    public static string GetName(IGameContext gameContext)
    {
        var name = gameContext.FeaturePlugIns.GetPlugIn<GameNamePlugIn>()?.Configuration?.Name;
        return string.IsNullOrWhiteSpace(name) ? GameNameConfiguration.DefaultName : name.Trim();
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new GameNameConfiguration();
    }
}
