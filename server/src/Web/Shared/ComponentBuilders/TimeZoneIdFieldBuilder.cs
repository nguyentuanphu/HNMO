// <copyright file="TimeZoneIdFieldBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using HNMO.DataModel.Configuration;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder"/> for the <see cref="SystemConfiguration.TimeZoneId"/>, which offers the
/// available system time zones as suggestions while still allowing a free-text value.
/// </summary>
public class TimeZoneIdFieldBuilder : BaseComponentBuilder, IComponentBuilder
{
    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
    {
        return this.BuildField<string, TimeZoneIdField>(model, propertyInfo, builder, currentIndex, notificationService);
    }

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo) => propertyInfo.PropertyType == typeof(string) && propertyInfo.Name == nameof(SystemConfiguration.TimeZoneId);
}
