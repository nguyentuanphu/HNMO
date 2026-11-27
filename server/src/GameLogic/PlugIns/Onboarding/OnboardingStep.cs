// <copyright file="OnboardingStep.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A step of the new-player guide.
/// </summary>
public class OnboardingStep
{
    /// <summary>Gets or sets the identifier; the progress is kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the step is part of the guide.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the player may skip this step alone (it gives no reward then).</summary>
    public bool Optional { get; set; }

    /// <summary>Gets or sets what the step asks for.</summary>
    public OnboardingStepKind Kind { get; set; }

    /// <summary>Gets or sets the count, level or points to reach.</summary>
    [Range(1, 2_000_000_000)]
    public int Target { get; set; } = 1;

    /// <summary>Gets or sets the NPC number of <see cref="OnboardingStepKind.TalkToNpc"/>; 0 is any NPC.</summary>
    [Range(0, 10000)]
    public short NpcNumber { get; set; }

    /// <summary>Gets or sets the map of <see cref="OnboardingStepKind.EnterMap"/> and <see cref="OnboardingStepKind.WalkTo"/>,
    /// or where kills count for <see cref="OnboardingStepKind.KillMonsters"/>; -1 is every map.</summary>
    [Range(-1, 255)]
    public short MapNumber { get; set; } = -1;

    /// <summary>Gets or sets the x coordinate of <see cref="OnboardingStepKind.WalkTo"/>.</summary>
    [Range(0, 255)]
    public byte X { get; set; }

    /// <summary>Gets or sets the y coordinate of <see cref="OnboardingStepKind.WalkTo"/>.</summary>
    [Range(0, 255)]
    public byte Y { get; set; }

    /// <summary>Gets or sets how near to the spot counts for <see cref="OnboardingStepKind.WalkTo"/>.</summary>
    [Range(0, 50)]
    public byte Radius { get; set; } = 4;

    /// <summary>Gets or sets the monster number which counts for <see cref="OnboardingStepKind.KillMonsters"/>; 0 counts every monster.</summary>
    [Range(0, 10000)]
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets the item group of <see cref="OnboardingStepKind.UseItem"/>; -1 is any group.</summary>
    [Range(-1, 15)]
    public short ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets the item number of <see cref="OnboardingStepKind.UseItem"/>; -1 is any number of the group.</summary>
    [Range(-1, 511)]
    public short ItemNumber { get; set; } = -1;

    /// <summary>Gets or sets the skill number of <see cref="OnboardingStepKind.UseSkill"/>; 0 is any skill.</summary>
    [Range(0, 1000)]
    public short SkillNumber { get; set; }

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(120)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English hint (how to do it).</summary>
    [StringLength(300)]
    public string HintEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese hint.</summary>
    [StringLength(300)]
    public string HintVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward of the step.</summary>
    public MissionReward Reward { get; set; } = new();
}
