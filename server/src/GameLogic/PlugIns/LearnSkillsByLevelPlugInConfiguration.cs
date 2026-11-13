// <copyright file="LearnSkillsByLevelPlugInConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using HNMO.DataModel.Composition;

/// <summary>
/// Configuration for the <see cref="LearnSkillsByLevelPlugIn"/>.
/// </summary>
public class LearnSkillsByLevelPlugInConfiguration
{
    /// <summary>
    /// Gets or sets the skills which are learned by level.
    /// </summary>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LearnSkillsByLevelPlugInConfiguration_Skills_Name))]
    public ICollection<SkillOfLevel> Skills { get; set; } = new List<SkillOfLevel>();

    /// <summary>
    /// Creates the default configuration: the A, B and C skills of the HNMO skills (700 to 727, four per class),
    /// learned at the levels 80, 150 and 220. The D skills are learned by the trial quests.
    /// </summary>
    /// <returns>The default configuration.</returns>
    public static LearnSkillsByLevelPlugInConfiguration CreateDefault()
    {
        var configuration = new LearnSkillsByLevelPlugInConfiguration();
        for (short classIndex = 0; classIndex < 7; classIndex++)
        {
            var skillA = (short)(700 + (4 * classIndex));
            configuration.Skills.Add(new SkillOfLevel(skillA, 80));
            configuration.Skills.Add(new SkillOfLevel((short)(skillA + 1), 150));
            configuration.Skills.Add(new SkillOfLevel((short)(skillA + 2), 220));
        }

        return configuration;
    }

    /// <summary>
    /// A skill which is learned from a minimum level on.
    /// </summary>
    public class SkillOfLevel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SkillOfLevel"/> class.
        /// </summary>
        public SkillOfLevel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillOfLevel"/> class.
        /// </summary>
        /// <param name="skillNumber">The skill number.</param>
        /// <param name="minimumLevel">The minimum level.</param>
        public SkillOfLevel(short skillNumber, int minimumLevel)
        {
            this.SkillNumber = skillNumber;
            this.MinimumLevel = minimumLevel;
        }

        /// <summary>
        /// Gets or sets the number of the skill. Only characters of a class which qualifies for the skill learn it.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LearnSkillsByLevelPlugInConfiguration_SkillNumber_Name))]
        public short SkillNumber { get; set; }

        /// <summary>
        /// Gets or sets the level from which the skill is learned.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.LearnSkillsByLevelPlugInConfiguration_MinimumLevel_Name))]
        public int MinimumLevel { get; set; }
    }
}
