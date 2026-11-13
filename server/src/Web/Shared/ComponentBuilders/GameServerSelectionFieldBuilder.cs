// <copyright file="GameServerSelectionFieldBuilder.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.ComponentBuilders;

using System.Reflection;
using Microsoft.AspNetCore.Components.Rendering;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.Web.Shared.Components.Form;
using HNMO.Web.Shared.Services;

/// <summary>
/// A <see cref="IComponentBuilder"/> for the server scope of periodic task configurations.
/// </summary>
/// <remarks>
/// It matches <see cref="IList{T}"/> of <see cref="byte"/> declared within the
/// <see cref="PeriodicTaskConfiguration"/> hierarchy instead of matching by property
/// name, so renames can't silently degrade the editor to a number table, and other
/// byte lists (e.g. character class numbers) keep their generic editor.
/// </remarks>
public class GameServerSelectionFieldBuilder : BaseComponentBuilder, IComponentBuilder
{
    /// <inheritdoc/>
    public int BuildComponent(object model, PropertyInfo propertyInfo, RenderTreeBuilder builder, int currentIndex, IChangeNotificationService notificationService)
        => this.BuildField<IList<byte>>(model, typeof(GameServerSelectionField), propertyInfo, builder, currentIndex, notificationService);

    /// <inheritdoc/>
    public bool CanBuildComponent(PropertyInfo propertyInfo)
        => propertyInfo.PropertyType == typeof(IList<byte>)
        && propertyInfo.DeclaringType is { } declaringType
        && typeof(PeriodicTaskConfiguration).IsAssignableFrom(declaringType);
}
