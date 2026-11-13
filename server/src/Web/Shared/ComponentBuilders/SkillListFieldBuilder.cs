// <copyright file="SkillListFieldBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using HNMO.DataModel.Entities;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder"/> for <see cref="ICollection{T}"/> of <see cref="SkillEntry"/> fields.
/// It shows a skill list including a master skill tree editor.
/// </summary>
public class SkillListFieldBuilder : BaseComponentBuilder, IComponentBuilder
{
    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
        => this.BuildField<ICollection<SkillEntry>>(model, typeof(SkillListField), propertyInfo, builder, currentIndex, notificationService);

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo)
        => propertyInfo.PropertyType == typeof(ICollection<SkillEntry>);
}
