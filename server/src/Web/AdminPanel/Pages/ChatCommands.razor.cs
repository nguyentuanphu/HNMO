// <copyright file="ChatCommands.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Components;
using HNMO.DataModel.Entities;
using HNMO.Web.Shared.Services;

/// <summary>
/// Code-behind for the <see cref="ChatCommands"/> page.
/// </summary>
public partial class ChatCommands
{
    [Inject]
    private ChatCommandController ChatCommandController { get; set; } = null!;

    private void OnStatusSelected(ChangeEventArgs args)
    {
        this.ChatCommandController.StatusFilter = args.Value is string value && Enum.TryParse<CharacterStatus>(value, out var status)
            ? status
            : null;
    }
}
