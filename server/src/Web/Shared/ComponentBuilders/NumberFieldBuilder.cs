// <copyright file="NumberFieldBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder" /> for number fields.
/// </summary>
/// <typeparam name="TNumber">The type of the number.</typeparam>
public class NumberFieldBuilder<TNumber> : BaseComponentBuilder, IComponentBuilder
    where TNumber : struct
{
    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
    {
        if (propertyInfo.PropertyType == typeof(TNumber))
        {
            return this.BuildField<TNumber, NumberField<TNumber>>(model, propertyInfo, builder, currentIndex, notificationService);
        }

        return this.BuildField<TNumber?, NullableNumberField<TNumber>>(model, propertyInfo, builder, currentIndex, notificationService);
    }

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo) => propertyInfo.PropertyType == typeof(TNumber) || propertyInfo.PropertyType == typeof(TNumber?);
}