// <copyright file="HnmoBugReportCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoBugReportCommand"/> (<c>F5 54</c>): the bug report window's state, and sending a report.
/// </summary>
[PlugIn]
[Display(Name = "HNMO bug report handler", Description = "Takes the players' bug reports for the server's owner.")]
[Guid("C8B24F61-7A3E-4D95-B1C0-2E6F9D4A8B37")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoBugReportCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoBugReportCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || packet.Length < 6)
        {
            return;
        }

        var vi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        if (BugReportsPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            var off = JsonSerializer.Serialize(new { off = true, message = vi ? "Báo lỗi đang tắt trên server này." : "Bug reports are off on this server." });
            await player.InvokeViewPlugInAsync<IHnmoBugReportViewPlugIn>(p => p.ShowBugReportsAsync(off)).ConfigureAwait(false);
            return;
        }

        var request = new HnmoBugReportCommand(packet);
        string? message = null;
        var sent = false;
        if (request.Action == 1)
        {
            string? category = null, text = null, info = null;
            try
            {
                using var document = JsonDocument.Parse(packet.Length > 6 ? request.Json : "{}");
                var root = document.RootElement;
                category = Read(root, "category");
                text = Read(root, "text");
                info = Read(root, "info");
            }
            catch (JsonException)
            {
                // a broken message is refused like an empty one
            }

            var (en, viText, ok) = plugIn.Submit(player, category, text, info);
            message = vi ? viText : en;
            sent = ok;
        }

        var json = JsonSerializer.Serialize(new { state = plugIn.StateOf(player), message, sent });
        await player.InvokeViewPlugInAsync<IHnmoBugReportViewPlugIn>(p => p.ShowBugReportsAsync(json)).ConfigureAwait(false);

        static string? Read(JsonElement root, string name) =>
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
