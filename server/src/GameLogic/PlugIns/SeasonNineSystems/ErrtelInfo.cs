// <copyright file="ErrtelInfo.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>One errtel: its item number (12/221..261), element and options, one per rank.</summary>
/// <param name="Number">The errtel's item number.</param>
/// <param name="Element">The errtel's element.</param>
/// <param name="Options">The options by rank: the option index of <see cref="SeasonNineSystemsCatalog.ErrtelOptions"/> and its level (0..10).</param>
public sealed record ErrtelInfo(short Number, PentagramElement Element, IReadOnlyList<(int Index, byte Level)> Options);
