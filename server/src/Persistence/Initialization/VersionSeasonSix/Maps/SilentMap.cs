// <copyright file="SilentMap.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Maps;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;

/// <summary>
/// The initialization for the Silent map.
/// </summary>
internal class SilentMap : BaseMapInitializer
{
    /// <summary>
    /// The Number of the Map.
    /// </summary>
    internal const byte Number = 40;

    /// <summary>
    /// The Name of the Map.
    /// </summary>
    internal const string Name = "Silent Map?";

    /// <summary>
    /// Initializes a new instance of the <see cref="SilentMap"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public SilentMap(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc/>
    protected override byte MapNumber => Number;

    /// <inheritdoc/>
    protected override LocalizedString MapName => LocalizedString.FromResource(() => MapNames.SilentMap);
}