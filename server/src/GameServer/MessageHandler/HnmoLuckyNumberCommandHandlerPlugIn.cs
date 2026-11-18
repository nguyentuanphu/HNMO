// <copyright file="HnmoLuckyNumberCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.LuckyNumber;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoLuckyNumberCommand"/> packets (<c>F5 34</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Lucky number handler", Description = "Handles daily lucky number requests, picks, and prize claims.")]
[Guid("47C9E1B5-6D32-4F8A-A14E-2B9C58F0D4A3")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoLuckyNumberCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoLuckyNumberCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var command = new HnmoLuckyNumberCommand(packet);
        var plugIn = LuckyNumberPlugIn.Get(player.GameContext);
        if (plugIn is null)
        {
            if (command.Action == HnmoLuckyNumberCommand.HnmoLuckyNumberActionType.Request)
            {
                var offEngine = new LuckyNumberEngine(new LuckyNumberConfiguration(), LuckyNumberStore.Open(null), enabled: false);
                var offJson = LuckyNumberStateWriter.WritePublic(offEngine, DateTime.UtcNow);
                await player.InvokeViewPlugInAsync<IHnmoLuckyNumberViewPlugIn>(p => p.ShowLuckyNumberAsync(offJson, requested: true)).ConfigureAwait(false);
            }

            return;
        }

        switch (command.Action)
        {
            case HnmoLuckyNumberCommand.HnmoLuckyNumberActionType.Request:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
            case HnmoLuckyNumberCommand.HnmoLuckyNumberActionType.Pick:
                await plugIn.PickAsync(player, unchecked((int)command.Number)).ConfigureAwait(false);
                break;
            case HnmoLuckyNumberCommand.HnmoLuckyNumberActionType.ClaimOwed:
                await plugIn.ClaimOwedAsync(player).ConfigureAwait(false);
                break;
        }
    }
}
