// <copyright file="FolderSeatStatus.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>The folder limit as this program sees it now (<see cref="FolderSeat.Status"/>).</summary>
/// <param name="Enforced">Whether the HNMO developer's hub has the folder limit switched on for this folder.</param>
/// <param name="HasSeat">Whether this folder holds a seat (always <c>true</c> while the limit is off).</param>
/// <param name="Allowed">The folders allowed at once on this computer.</param>
/// <param name="MachineId">This computer's id.</param>
/// <param name="OtherHolders">The paths of the other folders which hold a seat.</param>
public sealed record FolderSeatStatus(bool Enforced, bool HasSeat, int Allowed, string MachineId, IReadOnlyList<string> OtherHolders)
{
    /// <summary>Gets the status while the folder limit is off.</summary>
    public static FolderSeatStatus Off { get; } = new(false, true, 0, string.Empty, []);

    /// <summary>Gets a value indicating whether no player may log in to this folder now.</summary>
    public bool IsBlocked => this.Enforced && !this.HasSeat;
}
