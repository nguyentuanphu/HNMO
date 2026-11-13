// <copyright file="ILookupController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Services;

using HNMO.Persistence;

/// <summary>
/// Interface for a lookup controller which provides methods to find objects by some text and type.
/// </summary>
public interface ILookupController
{
    /// <summary>
    /// Looks up objects with a specific text and type.
    /// </summary>
    /// <typeparam name="T">The type of the searched object.</typeparam>
    /// <param name="text">The search text.</param>
    /// <param name="persistenceContext">The persistence context.</param>
    /// <returns>
    /// All objects which meet the criteria.
    /// </returns>
    Task<IEnumerable<T>> GetSuggestionsAsync<T>(string text, IContext? persistenceContext)
        where T : class;
}