// <copyright file="HnmoTimetableCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoTimetableCommand"/> packets (<c>F5 32</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Timetable handler", Description = "Handles HNMO event and boss timetable requests.")]
[Guid("C4A3195B-7D28-4C0E-92F1-4E6B81A3901D")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoTimetableCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoTimetableCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var command = new HnmoTimetableCommand(packet);
        if (command.Action != HnmoTimetableCommand.HnmoTimetableActionType.Request)
        {
            return;
        }

        if (TimetablePlugIn.Get(player.GameContext) is { } plugIn)
        {
            await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
            return;
        }

        // Send a clean disabled response so the UI knows the feature is OFF by default.
        var disabledJson = TimetableStateWriter.Write(TimetableEngine.BuildSnapshot(
            new TimetableConfiguration(),
            enabled: false,
            DateTime.UtcNow,
            [],
            []));
        await player.InvokeViewPlugInAsync<IHnmoTimetableViewPlugIn>(p => p.ShowTimetableAsync(disabledJson, requested: true)).ConfigureAwait(false);
    }
}
