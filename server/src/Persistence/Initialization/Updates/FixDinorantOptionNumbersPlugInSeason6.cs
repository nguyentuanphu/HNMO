// <copyright file="FixDinorantOptionNumbersPlugInSeason6.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update gives each Dinorant option a distinct number, which is its bit in the option field the client expects.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixDinorantOptionNumbersPlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.FixDinorantOptionNumbersPlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("F89EC822-9B82-4265-90E0-98D13CDC79F0")]
public class FixDinorantOptionNumbersPlugInSeason6 : FixDinorantOptionNumbersPlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
}
