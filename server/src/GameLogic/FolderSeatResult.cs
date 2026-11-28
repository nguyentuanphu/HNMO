// <copyright file="FolderSeatResult.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>What a folder's heartbeat found (<see cref="FolderSeats.Refresh"/>).</summary>
/// <param name="HasSeat">Whether this folder holds a seat.</param>
/// <param name="Allowed">The folders allowed at once on this computer.</param>
/// <param name="Place">This folder's place in the line of live folders (0: first).</param>
/// <param name="OtherHolders">The paths of the other folders which hold a seat.</param>
public sealed record FolderSeatResult(bool HasSeat, int Allowed, int Place, IReadOnlyList<string> OtherHolders);
