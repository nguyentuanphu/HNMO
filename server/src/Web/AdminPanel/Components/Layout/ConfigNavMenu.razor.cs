// <copyright file="ConfigNavMenu.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using HNMO.Web.Shared.Services;

/// <summary>
/// Navigation menu of the admin panel.
/// </summary>
public partial class ConfigNavMenu
{
    private bool _expandGameConfig;

    /// <summary>
    /// Gets or sets the game configuration identifier.
    /// </summary>
    [Parameter]
    [Required]
    public Guid GameConfigurationId { get; set; }

    [Inject]
    private NavigationHistory NavigationHistory { get; set; } = null!;

    private void ToggleGameConfig()
    {
        this._expandGameConfig = !this._expandGameConfig;
    }
}