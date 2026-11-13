// <copyright file="EditConnectionServer.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using HNMO.DataModel.Configuration;
using HNMO.Web.AdminPanel.Auth;

/// <summary>
/// Edit page for the <see cref="HNMO.Web.AdminPanel.Components.ConnectServer.ConnectServerConfiguration"/>.
/// </summary>
[Route("/edit-connectionServer/{id:guid}")]
[Authorize(Policy = AdminPolicies.Administrator)]
public sealed partial class EditConnectionServer : EditBase
{
    /// <inheritdoc />
    protected override Type? Type => typeof(ConnectServerDefinition);
}