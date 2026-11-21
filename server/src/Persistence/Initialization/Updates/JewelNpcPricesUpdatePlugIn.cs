// <copyright file="JewelNpcPricesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update sets the NPC prices of the jewels of Bless, Life, Creation and Guardian (and their bundles) to the prices
/// of the Season 6/9 server suites: 3,000,000 / 16,000,000 / 15,000,000 / 15,000,000 (owner's choice, 21 Nov 2026;
/// they were the classic client's 9,000,000 / 45,000,000 / 36,000,000 / 60,000,000). A price the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("4B9E2D71-8C05-4A36-B1F7-6E3D0A92C548")]
public class JewelNpcPricesUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Jewel NPC prices of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Bless 3 M, Life 16 M, Creation 15 M, Guardian 15 M (and their bundles), unless the host changed the price.";

    private static readonly IReadOnlyDictionary<string, string> NewFormulas = new Dictionary<string, string>
    {
        ["9000000"] = "3000000",
        ["45000000"] = "16000000",
        ["36000000"] = "15000000",
        ["60000000"] = "15000000",
        ["(level + 1) * 9000000 * 10"] = "(level + 1) * 3000000 * 10",
        ["(level + 1) * 45000000 * 10"] = "(level + 1) * 16000000 * 10",
        ["(level + 1) * 36000000 * 10"] = "(level + 1) * 15000000 * 10",
        ["(level + 1) * 60000000 * 10"] = "(level + 1) * 15000000 * 10",
    };

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var definition in gameConfiguration.ItemPriceDefinitions)
        {
            if (definition.BasePriceFormula is { } formula
                && NewFormulas.TryGetValue(formula.Trim(), out var newFormula))
            {
                definition.BasePriceFormula = newFormula;
            }
        }

        return ValueTask.CompletedTask;
    }
}
