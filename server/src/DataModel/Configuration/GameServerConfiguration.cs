// <copyright file="GameServerConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration;

using HNMO.Annotations;

/// <summary>
/// Defines the game server configuration.
/// </summary>
[AggregateRoot]
[Cloneable]
public partial class GameServerConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of players which can connect.
    /// </summary>
    public short MaximumPlayers { get; set; }

    /// <summary>
    /// Gets or sets the maps which should be hosted on the server.
    /// </summary>
    public virtual ICollection<GameMapDefinition> Maps { get; protected set; } = null!;

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(HNMO.DataModel.Properties.Resources.GameServerConfigSummary, this.MaximumPlayers); // TODO Add Description field
    }
}