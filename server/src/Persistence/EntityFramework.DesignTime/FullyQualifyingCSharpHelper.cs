// <copyright file="FullyQualifyingCSharpHelper.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.DesignTime;

using Microsoft.EntityFrameworkCore.Storage;

#pragma warning disable EF1001 // Internal EF Core API usage. There is no public API to influence how types are referenced in generated code.

/// <summary>
/// A <see cref="Microsoft.EntityFrameworkCore.Design.ICSharpHelper"/> which references the types of HNMO
/// with their full name in generated code.
/// </summary>
/// <remarks>
/// The entity types of this project have the same names as their base types of the data model, e.g.
/// <see cref="Persistence.EntityFramework.Model.Account"/> and <see cref="DataModel.Entities.Account"/>. The generated code of the
/// compiled models imports both namespaces and references both types, which would be ambiguous with short names.
/// </remarks>
internal class FullyQualifyingCSharpHelper : Microsoft.EntityFrameworkCore.Design.Internal.CSharpHelper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullyQualifyingCSharpHelper"/> class.
    /// </summary>
    /// <param name="typeMappingSource">The type mapping source.</param>
    public FullyQualifyingCSharpHelper(ITypeMappingSource typeMappingSource)
        : base(typeMappingSource)
    {
    }

    /// <inheritdoc />
    public override string Reference(Type type, bool? fullName = null)
    {
        return base.Reference(type, fullName ?? ContainsHnmoType(type));
    }

    private static bool ContainsHnmoType(Type type)
    {
        if (type.Namespace?.StartsWith("HNMO", StringComparison.Ordinal) is true)
        {
            return true;
        }

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            return ContainsHnmoType(elementType);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsHnmoType);
    }
}
