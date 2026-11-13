// <copyright file="IgnoreWhenCloningAttribute.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Annotations;

/// <summary>
/// Properties marked with this attribute will not get cloned
/// implemented by a code generator.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreWhenCloningAttribute : Attribute
{
}