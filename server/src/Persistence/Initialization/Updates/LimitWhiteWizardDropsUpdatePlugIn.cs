// <copyright file="LimitWhiteWizardDropsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.PlugIns;

/// <summary>
/// This update limits the Wizard's Ring to one per character for existing databases.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.LimitWhiteWizardDropsUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.LimitWhiteWizardDropsUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A7B3E5F1-8C2D-4E6F-9A1B-3D5C7E8F2A4B")]
public class LimitWhiteWizardDropsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "Limit White Wizard drops";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "Limits the Wizard's Ring to one per character.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 07, 05, 2, 20, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var wizardsRing = gameConfiguration.Items.FirstOrDefault(item =>
            item.Number == ItemConstants.WizardsRing.Number && item.Group == ItemConstants.WizardsRing.Group);

        if (wizardsRing is not null)
        {
            wizardsRing.StorageLimitPerCharacter = 1;
        }
    }
}
