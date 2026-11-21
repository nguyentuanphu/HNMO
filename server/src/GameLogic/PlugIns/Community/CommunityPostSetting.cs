// <copyright file="CommunityPostSetting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The setting of one kind of automatic post. The texts take placeholders in braces: {player}, {item}, {monster},
/// {map}, {boss}, {guild}, {level}, {rank}, {points}.
/// </summary>
public class CommunityPostSetting
{
    /// <summary>Gets or sets a value indicating whether this kind is posted at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it's posted to Discord (when the Discord bridge is set up).</summary>
    public bool ToDiscord { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether it's shown in the website's recent highlights.</summary>
    public bool ToWebsite { get; set; } = true;

    /// <summary>Gets or sets the English text.</summary>
    [StringLength(300)]
    public string TextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text.</summary>
    [StringLength(300)]
    public string TextVi { get; set; } = string.Empty;
}
