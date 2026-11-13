// <copyright file="SystemConfigurationTypedContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.TypedContexts;

/// <summary>
/// The <see cref="TypedContext"/> for <see cref="HNMO.DataModel.Configuration.SystemConfiguration"/>, which uses a compiled model.
/// </summary>
internal sealed class SystemConfigurationTypedContext : TypedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemConfigurationTypedContext"/> class.
    /// </summary>
    public SystemConfigurationTypedContext()
        : base(typeof(HNMO.DataModel.Configuration.SystemConfiguration))
    {
    }
}
