// <copyright file="EditMap.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using HNMO.DataModel.Configuration;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.MapEditor;

/// <summary>
/// A page, which shows an <see cref="MapEditor"/> for all <see cref="GameConfiguration.Maps"/>.
/// </summary>
[Route("/map-editor")]
[Route("/map-editor/{SelectedMapId:guid}")]
[Authorize(Policy = AdminPolicies.Administrator)]
public sealed class EditMap : MapEditorPageBase<MapEditor>
{
    /// <inheritdoc />
    protected override string Caption => Resources.MapEditor;
}
