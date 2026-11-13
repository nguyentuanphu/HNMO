// <copyright file="InvasionSpawnTableFieldBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder" /> for <see cref="IList{InvasionSpawnConfiguration}"/> fields.
/// </summary>
public class InvasionSpawnTableFieldBuilder : BaseComponentBuilder, IComponentBuilder
{
    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
        => this.BuildField<IList<InvasionSpawnConfiguration>>(model, typeof(InvasionSpawnTable), propertyInfo, builder, currentIndex, notificationService);

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo)
        => propertyInfo.PropertyType == typeof(IList<InvasionSpawnConfiguration>);
}
