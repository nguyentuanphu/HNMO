// <copyright file="IHnmoTimetableViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in interface for sending the HNMO event and boss timetable to the client.
/// </summary>
public interface IHnmoTimetableViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the serialized timetable state JSON to the client.</summary>
    /// <param name="json">The timetable state JSON.</param>
    /// <param name="requested">Whether the client explicitly requested the snapshot.</param>
    ValueTask ShowTimetableAsync(string json, bool requested);
}
