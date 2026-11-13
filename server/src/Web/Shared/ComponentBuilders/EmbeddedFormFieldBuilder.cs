// <copyright file="EmbeddedFormFieldBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using HNMO.DataModel.Composition;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder"/> for properties which are marked with the <see cref="MemberOfAggregateAttribute"/>.
/// </summary>
public class EmbeddedFormFieldBuilder : BaseComponentBuilder, IComponentBuilder
{
    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
    {
        return this.BuildGenericField(model, typeof(MemberOfAggregateField<>), builder, propertyInfo, currentIndex, notificationService);
    }

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo) =>
        propertyInfo.PropertyType.IsClass
        && !propertyInfo.PropertyType.IsArray
        && propertyInfo.GetCustomAttribute<MemberOfAggregateAttribute>() is { };
}