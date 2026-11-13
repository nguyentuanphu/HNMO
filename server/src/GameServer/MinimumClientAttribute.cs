// <copyright file="MinimumClientAttribute.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer;

using HNMO.GameLogic.Views;
using HNMO.Network.PlugIns;

/// <summary>
/// Attribute to mark an implemented <see cref="IViewPlugIn"/> with a specific (inclusive) minimum client version.
/// </summary>
public class MinimumClientAttribute : ClientAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MinimumClientAttribute"/> class.
    /// </summary>
    /// <param name="season">The minimum season of compatible clients.</param>
    /// <param name="episode">The minimum episode of compatible clients.</param>
    /// <param name="language">The language of compatible clients.</param>
    public MinimumClientAttribute(byte season, byte episode, ClientLanguage language)
        : base(season, episode, language)
    {
    }
}