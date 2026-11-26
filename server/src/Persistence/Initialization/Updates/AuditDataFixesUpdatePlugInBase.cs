// <copyright file="AuditDataFixesUpdatePlugInBase.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using HNMO.DataModel.Configuration;

/// <summary>
/// Brings the data fixes of the 25 Nov 2026 audit (commit 1fc60d72, which only changed the initialization) to existing
/// databases: Hanzo the Blacksmith's store had two weapons in slot 73 and a Morning Star where the Mace belongs.
/// </summary>
/// <remarks>
/// Cumulative and idempotent: it sets the final state (slots and definitions) and changes nothing on a second run.
/// </remarks>
public abstract class AuditDataFixesUpdatePlugInBase : UpdatePlugInBase
{
    /// <summary>The number of Hanzo the Blacksmith.</summary>
    internal const short HanzoNumber = 251;

    private const byte SwordsGroup = 0;

    private const byte ScepterGroup = 2;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 20, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        FixHanzoStore(gameConfiguration);
        this.ApplyVersionFixes(gameConfiguration);
        return ValueTask.CompletedTask;
    }

    /// <summary>Applies the fixes of one game version.</summary>
    /// <param name="gameConfiguration">The game configuration.</param>
    protected virtual void ApplyVersionFixes(GameConfiguration gameConfiguration)
    {
    }

    private static void FixHanzoStore(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == HanzoNumber)?.MerchantStore is not { } store)
        {
            return;
        }

        // Falchion, Serpent Sword and Blade one slot further: 73 is the Gladius'.
        foreach (var (number, slot) in new[] { (7, 74), (8, 75), (5, 76) })
        {
            if (store.Items.FirstOrDefault(i => i.Definition is { Group: SwordsGroup } d && d.Number == number) is { } item)
            {
                item.ItemSlot = (byte)slot;
            }
        }

        // Slot 50 is the Mace (scepter 0); the Morning Star (scepter 1) has its own slot, 54.
        if (store.Items.FirstOrDefault(i => i.ItemSlot == 50 && i.Definition is { Group: ScepterGroup, Number: 1 }) is { } wrong
            && gameConfiguration.Items.FirstOrDefault(d => d.Group == ScepterGroup && d.Number == 0) is { } mace)
        {
            wrong.Definition = mace;
        }
    }
}
