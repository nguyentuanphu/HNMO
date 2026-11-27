// <copyright file="BufferNpcBuffPreset.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BufferNpc;

/// <summary>
/// A documented buff choice for the Buffer NPC admin panel.
/// </summary>
/// <param name="EffectNumber">The magic effect definition number.</param>
/// <param name="NameEn">English label.</param>
/// <param name="NameVi">Vietnamese label.</param>
public sealed record BufferNpcBuffPreset(short EffectNumber, string NameEn, string NameVi);
