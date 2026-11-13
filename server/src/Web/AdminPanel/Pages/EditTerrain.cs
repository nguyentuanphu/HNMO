// <copyright file="EditTerrain.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using HNMO.DataModel.Configuration;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.TerrainEditor;

/// <summary>
/// A page, which shows a <see cref="TerrainEditor"/> for the terrains of all <see cref="GameConfiguration.Maps"/>.
/// </summary>
[Route("/terrain-editor")]
[Route("/terrain-editor/{SelectedMapId:guid}")]
[Authorize(Policy = AdminPolicies.Administrator)]
public sealed class EditTerrain : MapEditorPageBase<TerrainEditor>
{
    /// <inheritdoc />
    protected override string Caption => Resources.TerrainEditor;
}
