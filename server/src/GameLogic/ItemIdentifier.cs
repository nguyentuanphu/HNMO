// <copyright file="ItemIdentifier.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>
/// The identifier for an item, including number and group.
/// </summary>
public record struct ItemIdentifier(short? Number, byte Group);