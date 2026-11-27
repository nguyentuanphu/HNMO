// <copyright file="ActivityItemState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ActivityLog;

using HNMO.GameLogic.PlugIns.AccountSafety;

/// <summary>Immutable item details captured at one side of an activity.</summary>
/// <param name="Serial">The item's origin serial.</param>
/// <param name="Group">The configured item group.</param>
/// <param name="Number">The configured item number.</param>
/// <param name="Level">The instance's upgrade level.</param>
/// <param name="Quantity">The stack size, or one for non-stackable items.</param>
/// <param name="Durability">The instance's durability.</param>
/// <param name="Sockets">The number of socket slots.</param>
/// <param name="HasSkill">Whether the instance supplies its skill.</param>
/// <param name="Options">Snapshot of the instance's option types, numbers and levels.</param>
/// <param name="Description">The existing private item description.</param>
public sealed record ActivityItemState(Guid Serial, byte Group, short Number, byte Level, double Quantity, double Durability, int Sockets, bool HasSkill, string Options, string Description)
{
    internal static ActivityItemState? Capture(Item? item)
    {
        if (item?.Definition is not { } definition)
        {
            return null;
        }

        var options = item.ItemOptions is not { Count: > 0 } ? string.Empty : string.Join(",", item.ItemOptions
            .Select(option => $"{option.ItemOption?.OptionType?.Name.ValueInNeutralLanguage}:{option.ItemOption?.Number}:{option.Level}")
            .Order(StringComparer.Ordinal));
        return new(item.EnsureSerial(), definition.Group, definition.Number, item.Level, item.IsStackable() ? item.Durability : 1,
            item.Durability, item.SocketCount, item.HasSkill, options, AccountSafetyPlugIn.Describe(item));
    }
}
