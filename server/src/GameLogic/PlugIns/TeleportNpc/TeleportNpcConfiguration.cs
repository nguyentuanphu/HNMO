// <copyright file="TeleportNpcConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TeleportNpc;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="TeleportNpcPlugIn"/> (OFF by default via <see cref="IDisabledByDefault"/>).
/// </summary>
public sealed class TeleportNpcConfiguration
{
    /// <summary>Gets or sets a value indicating whether Teleport NPC handling is active when the plug-in is enabled.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the NPC definition numbers managed by the Teleport NPC plug-in.</summary>
    [Display(Name = "NPC Numbers")]
    public List<short> NpcNumbers { get; set; } = [247, 540];

    /// <summary>Gets or sets a value indicating whether the client menu closes automatically after a successful warp.</summary>
    [Display(Name = "Close Dialog After Warp")]
    public bool CloseAfterWarp { get; set; } = true;

    /// <summary>Gets or sets the admin-defined warp destinations.</summary>
    [Display(Name = "Destinations")]
    public List<TeleportDestinationConfig> Destinations { get; set; } = CreateDefaultDestinations();

    /// <summary>Creates starter Teleport NPC destinations across classic maps with level and Zen gates.</summary>
    /// <returns>The starter destination list.</returns>
    public static List<TeleportDestinationConfig> CreateDefaultDestinations() =>
    [
        new()
        {
            Id = "lorencia",
            NameEn = "Lorencia Safe Zone",
            NameVi = "Thành Lorencia",
            MapNumber = 0,
            TargetX = 130,
            TargetY = 127,
            MinLevel = 1,
            ZenCost = 0,
        },
        new()
        {
            Id = "noria",
            NameEn = "Noria Town",
            NameVi = "Thị Trấn Noria",
            MapNumber = 3,
            TargetX = 174,
            TargetY = 111,
            MinLevel = 10,
            ZenCost = 2000,
        },
        new()
        {
            Id = "devias",
            NameEn = "Devias Outpost",
            NameVi = "Băng Thành Devias",
            MapNumber = 2,
            TargetX = 211,
            TargetY = 41,
            MinLevel = 20,
            ZenCost = 5000,
        },
        new()
        {
            Id = "atlans",
            NameEn = "Atlans Harbor",
            NameVi = "Vùng Biển Atlans",
            MapNumber = 7,
            TargetX = 23,
            TargetY = 19,
            MinLevel = 60,
            ZenCost = 15000,
        },
        new()
        {
            Id = "icarus",
            NameEn = "Icarus Sky Gate",
            NameVi = "Bầu Trời Icarus",
            MapNumber = 10,
            TargetX = 15,
            TargetY = 13,
            MinLevel = 170,
            ZenCost = 50000,
        },
    ];
}
