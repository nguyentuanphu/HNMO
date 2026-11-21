// <copyright file="DarkWizardTeleportSoulBarrierUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// This update lets the Dark Wizard (first class) use Teleport and Soul Barrier, as in the Season 6/9 server suites
/// (skill list: Dark Wizard rank 1 for both). Only the Soul Master and the Grand Master could use them, although the
/// Dark Wizard can read their scrolls.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7D3F2A61-C845-4E09-B1A7-5F8E2C94D013")]
public class DarkWizardTeleportSoulBarrierUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Teleport and Soul Barrier for the Dark Wizard";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The Dark Wizard can use Teleport and Soul Barrier, as in the Season 6/9 server suites, not only the Soul Master and Grand Master.";

    /// <summary>Gets the skills which the Dark Wizard gets.</summary>
    internal static IReadOnlyList<SkillNumber> Skills { get; } = [SkillNumber.Teleport, SkillNumber.SoulBarrier];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 13, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.CharacterClasses.FirstOrDefault(c => c.Number == (byte)CharacterClassNumber.DarkWizard) is not { } darkWizard)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var number in Skills)
        {
            if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)number) is { } skill
                && !skill.QualifiedCharacters.Contains(darkWizard))
            {
                skill.QualifiedCharacters.Add(darkWizard);
            }
        }

        return ValueTask.CompletedTask;
    }
}
