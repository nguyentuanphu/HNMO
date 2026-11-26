// <copyright file="PentagramData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>
/// The pentagram and errtel tables in use: the admin's (<see cref="SeasonNineSystemsConfiguration"/>) when given,
/// otherwise HNMO's built-in defaults (<see cref="SeasonNineSystemsCatalog"/>), which wait for the Season 9 pack's values.
/// </summary>
public static class PentagramData
{
    private static readonly List<ErrtelOptionSetting> DefaultOptions = SeasonNineSystemsCatalog.ErrtelOptions
        .Select(o => new ErrtelOptionSetting
        {
            Index = o.Index,
            ErrtelNumber = o.ItemNumber,
            Rank = o.Rank,
            Rate = o.OptionRate,
            ValueLv0 = o.ValueLv0,
            ValueLv5 = o.ValueLv5,
            ValueLv10 = o.ValueLv10,
            Name = o.Name,
        })
        .ToList();

    /// <summary>Gets the errtel options in use.</summary>
    /// <param name="configuration">The configuration, or <c>null</c> for the defaults.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyList<ErrtelOptionSetting> Options(SeasonNineSystemsConfiguration? configuration)
        => configuration?.ErrtelOptions is { Count: > 0 } options ? options : DefaultOptions;

    /// <summary>Gets an option by its index.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="index">The option index.</param>
    /// <returns>The option, or <c>null</c>.</returns>
    public static ErrtelOptionSetting? Option(SeasonNineSystemsConfiguration? configuration, int index)
        => Options(configuration).FirstOrDefault(o => o.Index == index);

    /// <summary>Gets the grade of a pentagram (1 for an unknown one).</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="pentagramNumber">The pentagram's item number.</param>
    /// <returns>The grade.</returns>
    public static byte Grade(SeasonNineSystemsConfiguration? configuration, short pentagramNumber)
    {
        if (configuration?.PentagramGrades is { Count: > 0 } grades)
        {
            return grades.FirstOrDefault(g => g.PentagramNumber == pentagramNumber)?.Grade ?? 1;
        }

        return SeasonNineSystemsCatalog.Pentagrams.FirstOrDefault(p => p.ItemNumber == pentagramNumber) is { Name: not null } p ? p.Grade : (byte)1;
    }

    /// <summary>Gets the chance (n/10000) to take an errtel of a rank and level out intact, with the admin bonus.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="rank">The errtel's rank.</param>
    /// <param name="level">The errtel's highest option level.</param>
    /// <returns>The chance out of 10000.</returns>
    public static int ExtractionRate(SeasonNineSystemsConfiguration? configuration, byte rank, byte level)
    {
        var bonus = configuration?.PentagramErrtelExtractBonusPct ?? 0;
        if (configuration?.ErrtelExtractionRates is { Count: > 0 } rates)
        {
            var rate = rates.FirstOrDefault(r => r.Rank == rank && r.Level == level)?.Rate ?? 7000;
            return Math.Clamp(rate + (bonus * 100), 0, 10000);
        }

        return SeasonNineSystemsEngine.CalculateErrtelExtractionRate(rank, level, bonus);
    }

    /// <summary>Gets the element of a monster (none unless the admin set one).</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="monsterNumber">The monster definition number.</param>
    /// <returns>The element.</returns>
    public static PentagramElement MonsterElement(SeasonNineSystemsConfiguration? configuration, int monsterNumber)
        => configuration?.MonsterElements?.FirstOrDefault(m => m.MonsterNumber == monsterNumber)?.Element ?? PentagramElement.Undefined;
}
