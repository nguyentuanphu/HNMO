// <copyright file="BuildCalculatorInput.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BuildCalculator;

using System.Collections.Generic;

/// <summary>
/// Input build parameters for server-authoritative stat calculation (ROADMAP 14.21).
/// </summary>
public sealed class BuildCalculatorInput
{
    /// <summary>Gets or sets <see cref="HNMO.DataModel.Configuration.CharacterClass.Number"/>.</summary>
    public byte ClassNumber { get; set; }

    /// <summary>Gets or sets character level (1..400).</summary>
    public int Level { get; set; } = 1;

    /// <summary>Gets or sets master level (0..400).</summary>
    public int MasterLevel { get; set; }

    /// <summary>Gets or sets base Strength points.</summary>
    public int Strength { get; set; } = 28;

    /// <summary>Gets or sets base Agility points.</summary>
    public int Agility { get; set; } = 20;

    /// <summary>Gets or sets base Vitality points.</summary>
    public int Vitality { get; set; } = 25;

    /// <summary>Gets or sets base Energy points.</summary>
    public int Energy { get; set; } = 10;

    /// <summary>Gets or sets base Leadership points (Command).</summary>
    public int Leadership { get; set; }

    /// <summary>Gets or sets equipped item specifications.</summary>
    public List<BuildCalculatorItemInput> Items { get; set; } = [];
}
