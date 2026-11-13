// <copyright file="AttributeRequirement.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration.Items;

using HNMO.Annotations;
using HNMO.AttributeSystem;

/// <summary>
/// Defines a requirement of an attribute with the specified value.
/// </summary>
[Cloneable]
public partial class AttributeRequirement
{
    /// <summary>
    /// Gets or sets the attribute which is required.
    /// </summary>
    [Required]
    public virtual AttributeDefinition? Attribute { get; set; }

    /// <summary>
    /// Gets or sets the minimum value the attribute needs to have.
    /// </summary>
    public int MinimumValue { get; set; }

    /// <summary>
    /// Gets or sets the minimum item level at which this requirement applies.
    /// </summary>
    public byte? MinimumItemLevel { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{this.Attribute}: {this.MinimumValue}";
    }
}