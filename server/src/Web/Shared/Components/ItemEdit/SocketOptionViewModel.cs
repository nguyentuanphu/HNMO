// <copyright file="SocketOptionViewModel.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Shared.Components.ItemEdit;

using System.Text.RegularExpressions;
using HNMO.DataModel.Configuration.Items;

/// <summary>
/// A view model for a socket option.
/// </summary>
public class SocketOptionViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SocketOptionViewModel"/> class.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="option">The option.</param>
    public SocketOptionViewModel(ItemOptionDefinition definition, IncreasableItemOption option)
    {
        this.Definition = definition;
        this.Option = option;
    }

    /// <summary>
    /// Gets the definition.
    /// </summary>
    public ItemOptionDefinition Definition { get; }

    /// <summary>
    /// Gets the option.
    /// </summary>
    public IncreasableItemOption Option { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var elementPrefix = this.Definition.Name.ToString() ?? string.Empty;
        if (Regex.Match(elementPrefix, @".\((\w+)\)") is { Success: true } match)
        {
            elementPrefix = match.Groups[1].Value;
        }

        return $"{elementPrefix}: {this.Option.LevelDependentOptions.FirstOrDefault()?.PowerUpDefinition?.TargetAttribute?.Designation?.ToString()}";
    }
}