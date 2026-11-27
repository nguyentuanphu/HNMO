// <copyright file="PentagramDrops.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>The pentagram and errtel drops of the Season 9 pentagram system (chances are admin settings, off by default).</summary>
public static class PentagramDrops
{
    private static readonly short[] ErrtelNumbers = [221, 231, 241, 251, 261];

    /// <summary>Rolls the drop of one killed monster.</summary>
    /// <param name="gameContext">The game context.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The dropped item, or <c>null</c>.</returns>
    public static TemporaryItem? Roll(IGameContext gameContext, SeasonNineSystemsConfiguration configuration)
    {
        if (configuration.PentagramDropChance > 0 && PentagramActions.NextRandom(10000) < configuration.PentagramDropChance)
        {
            var pentagrams = gameContext.Configuration.Items.Where(Pentagram.IsPentagram).ToList();
            if (pentagrams.Count > 0)
            {
                return new TemporaryItem
                {
                    Definition = pentagrams[PentagramActions.NextRandom(pentagrams.Count)],
                    Durability = 1,
                    PentagramElement = (byte)(1 + PentagramActions.NextRandom(5)),
                };
            }
        }

        if (configuration.ErrtelDropChance > 0 && PentagramActions.NextRandom(10000) < configuration.ErrtelDropChance)
        {
            var number = ErrtelNumbers[PentagramActions.NextRandom(ErrtelNumbers.Length)];
            var definition = gameContext.Configuration.Items.FirstOrDefault(d => d.Group == 12 && d.Number == number);
            var firstRank = PentagramData.Options(configuration).Where(o => o.ErrtelNumber == number && o.Rank == 1).ToList();
            if (definition is not null && firstRank.Count > 0)
            {
                var item = new TemporaryItem { Definition = definition, Durability = 1 };
                var element = (PentagramElement)(1 + PentagramActions.NextRandom(5));
                Pentagram.WriteErrtel(item, new ErrtelInfo(number, element, [(firstRank[PentagramActions.NextRandom(firstRank.Count)].Index, 0)]));
                return item;
            }
        }

        return null;
    }
}
