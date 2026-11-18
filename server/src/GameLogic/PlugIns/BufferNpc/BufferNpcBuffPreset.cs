// <copyright file="BufferNpcBuffPreset.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

/// <summary>
/// A documented buff choice for the Buffer NPC admin panel.
/// </summary>
/// <param name="EffectNumber">The magic effect definition number.</param>
/// <param name="NameEn">English label.</param>
/// <param name="NameVi">Vietnamese label.</param>
public sealed record BufferNpcBuffPreset(short EffectNumber, string NameEn, string NameVi);
