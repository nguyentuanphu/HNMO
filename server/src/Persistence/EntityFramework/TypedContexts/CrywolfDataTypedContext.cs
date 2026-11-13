// <copyright file="CrywolfDataTypedContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.TypedContexts;

/// <summary>
/// The <see cref="TypedContext"/> for <see cref="HNMO.DataModel.Entities.CrywolfData"/>, which uses a compiled model.
/// </summary>
internal sealed class CrywolfDataTypedContext : TypedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrywolfDataTypedContext"/> class.
    /// </summary>
    public CrywolfDataTypedContext()
        : base(typeof(HNMO.DataModel.Entities.CrywolfData))
    {
    }
}
