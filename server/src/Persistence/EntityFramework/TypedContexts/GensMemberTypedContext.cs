// <copyright file="GensMemberTypedContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.TypedContexts;

/// <summary>
/// The <see cref="TypedContext"/> for <see cref="HNMO.DataModel.Entities.GensMember"/>, which uses a compiled model.
/// </summary>
internal sealed class GensMemberTypedContext : TypedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GensMemberTypedContext"/> class.
    /// </summary>
    public GensMemberTypedContext()
        : base(typeof(HNMO.DataModel.Entities.GensMember))
    {
    }
}
