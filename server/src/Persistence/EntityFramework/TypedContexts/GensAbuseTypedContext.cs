// <copyright file="GensAbuseTypedContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.TypedContexts;

/// <summary>
/// The <see cref="TypedContext"/> for <see cref="HNMO.DataModel.Entities.GensAbuse"/>, which uses a compiled model.
/// </summary>
internal sealed class GensAbuseTypedContext : TypedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GensAbuseTypedContext"/> class.
    /// </summary>
    public GensAbuseTypedContext()
        : base(typeof(HNMO.DataModel.Entities.GensAbuse))
    {
    }
}
