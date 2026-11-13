// <copyright file="CastleSiegeGuildRegistrationTypedContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.TypedContexts;

/// <summary>
/// The <see cref="TypedContext"/> for <see cref="HNMO.DataModel.Entities.CastleSiegeGuildRegistration"/>, which uses a compiled model.
/// </summary>
internal sealed class CastleSiegeGuildRegistrationTypedContext : TypedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeGuildRegistrationTypedContext"/> class.
    /// </summary>
    public CastleSiegeGuildRegistrationTypedContext()
        : base(typeof(HNMO.DataModel.Entities.CastleSiegeGuildRegistration))
    {
    }
}
