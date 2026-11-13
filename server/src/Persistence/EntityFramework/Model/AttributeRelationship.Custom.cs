// <copyright file="AttributeRelationship.Custom.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.Model;

using HNMO.DataModel;

/// <summary>
/// The Entity Framework Core implementation of <see cref="HNMO.AttributeSystem.AttributeRelationship"/>.
/// </summary>
internal partial class AttributeRelationship :
    IAssignable,
    IAssignable<HNMO.AttributeSystem.AttributeRelationship>,
    ICloneable<HNMO.AttributeSystem.AttributeRelationship>
{
    /// <inheritdoc />
    public virtual AttributeSystem.AttributeRelationship Clone(DataModel.Configuration.GameConfiguration gameConfiguration)
    {
        var clone = new AttributeRelationship();
        clone.AssignValuesOf(this, gameConfiguration);
        return clone;
    }

    /// <inheritdoc />
    public virtual void AssignValuesOf(object other, DataModel.Configuration.GameConfiguration gameConfiguration)
    {
        if (other is AttributeSystem.AttributeRelationship typedOther)
        {
            this.AssignValuesOf(typedOther, gameConfiguration);
        }
    }

    /// <inheritdoc />
    public virtual void AssignValuesOf(AttributeSystem.AttributeRelationship other, DataModel.Configuration.GameConfiguration gameConfiguration)
    {
        this.InputAttribute = gameConfiguration.Attributes.FirstOrDefault(a => a.Id == other.InputAttribute?.Id) ?? other.InputAttribute;
        this.OperandAttribute = gameConfiguration.Attributes.FirstOrDefault(a => a.Id == other.OperandAttribute?.Id) ?? other.OperandAttribute;
        this.TargetAttribute = gameConfiguration.Attributes.FirstOrDefault(a => a.Id == other.TargetAttribute?.Id) ?? other.TargetAttribute;
        this.InputOperand = other.InputOperand;
        this.InputOperator = other.InputOperator;
        this.AggregateType = other.AggregateType;
    }
}