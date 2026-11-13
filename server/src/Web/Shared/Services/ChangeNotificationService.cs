// <copyright file="ChangeNotificationService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Services;

using System.ComponentModel;

/// <summary>
/// A simple implementation of the <see cref="IChangeNotificationService"/>.
/// </summary>
public class ChangeNotificationService : IChangeNotificationService
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public void NotifyChange(object? sender, string? propertyName)
    {
        this.PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
    }
}