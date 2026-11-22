// <copyright file="FenrirSecondClassUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.PlugIns;

/// <summary>
/// Restricts Horn of Fenrir (13, 37) to second class and up, matching the Season 6 and Season 9
/// server suites (removes the four first classes when all four are still assigned).
/// </summary>
[PlugIn]
[Display(
    Name = nameof(Properties.PlugInResources.FenrirSecondClassUpdatePlugIn_Name),
    Description = nameof(Properties.PlugInResources.FenrirSecondClassUpdatePlugIn_Description),
    ResourceType = typeof(Properties.PlugInResources))]
[Guid("D19E6B65-3F35-444A-896F-A7928C271849")]
public class FenrirSecondClassUpdatePlugIn : UpdatePlugInBase
{
    private static readonly byte[] FirstClassNumbers =
    [
        (byte)CharacterClassNumber.DarkWizard,
        (byte)CharacterClassNumber.DarkKnight,
        (byte)CharacterClassNumber.FairyElf,
        (byte)CharacterClassNumber.Summoner,
    ];

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => Properties.PlugInResources.FenrirSecondClassUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => Properties.PlugInResources.FenrirSecondClassUpdatePlugIn_Description;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 6, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Items.FirstOrDefault(i => i.Group == 13 && i.Number == 37) is { } fenrir
            && FirstClassNumbers.All(fc => fenrir.QualifiedCharacters.Any(c => c.Number == fc)))
        {
            var toRemove = fenrir.QualifiedCharacters.Where(c => FirstClassNumbers.Contains(c.Number)).ToList();
            foreach (var characterClass in toRemove)
            {
                fenrir.QualifiedCharacters.Remove(characterClass);
            }
        }

        return ValueTask.CompletedTask;
    }
}
