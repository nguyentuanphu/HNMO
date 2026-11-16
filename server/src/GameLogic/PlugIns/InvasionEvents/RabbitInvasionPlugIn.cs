// <copyright file="RabbitInvasionPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Enables the Lunar Rabbit Invasion feature (OFF by default).
/// </summary>
[PlugIn]
[Display(Name = "Lunar Rabbit Invasion", Description = "Spawns Strange Rabbits and Hideous Rabbits on a schedule (OFF by default).")]
[Guid("87D2F4C1-3A65-4E8B-9C14-2B7E50A9D3F1")]
public sealed class RabbitInvasionPlugIn : SimpleInvasionPlugIn, IDisabledByDefault
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitInvasionPlugIn"/> class.
    /// </summary>
    public RabbitInvasionPlugIn()
        : base(() => InvasionConfigurationDefaults.Rabbits)
    {
    }

    /// <inheritdoc />
    protected override ushort? AnnouncedMonsterId => InvasionMonsters.HideousRabbit;
}
