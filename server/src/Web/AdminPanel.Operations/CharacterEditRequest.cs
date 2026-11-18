// <copyright file="CharacterEditRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

/// <summary>Requested offline character modifications requiring an operator reason.</summary>
public sealed class CharacterEditRequest
{
    /// <summary>Gets or sets the target regular level.</summary>
    public int? Level { get; set; }

    /// <summary>Gets or sets the target master level.</summary>
    public int? MasterLevel { get; set; }

    /// <summary>Gets or sets the target resets count.</summary>
    public int? Resets { get; set; }

    /// <summary>Gets or sets the base Strength stat.</summary>
    public int? Strength { get; set; }

    /// <summary>Gets or sets the base Agility stat.</summary>
    public int? Agility { get; set; }

    /// <summary>Gets or sets the base Vitality stat.</summary>
    public int? Vitality { get; set; }

    /// <summary>Gets or sets the base Energy stat.</summary>
    public int? Energy { get; set; }

    /// <summary>Gets or sets the base Leadership stat.</summary>
    public int? Leadership { get; set; }

    /// <summary>Gets or sets the character inventory Zen.</summary>
    public int? Zen { get; set; }

    /// <summary>Gets or sets the target character class identifier.</summary>
    public Guid? CharacterClassId { get; set; }

    /// <summary>Gets or sets the required operator reason for audit and activity logs.</summary>
    public string Reason { get; set; } = string.Empty;
}
