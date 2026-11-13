// <copyright file="MemberOfAggregateAttribute.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Composition;

/// <summary>
/// Marks a property as a member of an aggregate.
/// The declaring type owns the objects of the marked property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MemberOfAggregateAttribute : Attribute
{
}