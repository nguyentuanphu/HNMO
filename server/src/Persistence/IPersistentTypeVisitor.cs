// <copyright file="IPersistentTypeVisitor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>
/// A visitor of a <see cref="PersistentType"/>, which gets the persistent type as type argument.
/// It allows to create generic types for a persistent type, without constructing them by reflection.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IPersistentTypeVisitor<out TResult>
{
    /// <summary>
    /// Visits the persistent type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The persistent type.</typeparam>
    /// <returns>The result.</returns>
    TResult Visit<T>()
        where T : class;
}
