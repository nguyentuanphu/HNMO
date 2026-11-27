// <copyright file="HnmoAutoErrorReportHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoAutoErrorReport"/> (<c>F5 60</c>): an error the web client met and reported on its own. It is
/// grouped for the server's owner (admin panel, Bug reports, Automatic); the client gets no answer.
/// </summary>
[PlugIn]
[Display(Name = "HNMO automatic error report handler", Description = "Keeps the web client's automatic error reports for the server's owner.")]
[Guid("3B7E5D29-9C41-4A6F-8E12-D0F4A7C35B68")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoAutoErrorReportHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoAutoErrorReport.SubCode;

    /// <inheritdoc />
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 6 || BugReportsPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return ValueTask.CompletedTask;
        }

        string? fingerprint;
        AutoErrorSample sample;
        try
        {
            using var document = JsonDocument.Parse(new HnmoAutoErrorReport(packet).Json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ValueTask.CompletedTask;
            }

            fingerprint = Read(root, "fingerprint");
            sample = new AutoErrorSample
            {
                Kind = Read(root, "kind") ?? "error",
                Message = Read(root, "message") ?? string.Empty,
                Stack = Read(root, "stack") ?? string.Empty,
                Version = Read(root, "version") ?? string.Empty,
                Browser = Read(root, "browser") ?? string.Empty,
                Os = Read(root, "os") ?? string.Empty,
                Device = Read(root, "device") ?? string.Empty,
                Map = Read(root, "map") ?? string.Empty,
                Gpu = Read(root, "gpu") ?? string.Empty,
            };
        }
        catch (JsonException)
        {
            // a broken report is dropped
            return ValueTask.CompletedTask;
        }

        plugIn.SubmitAutomatic(player, fingerprint, sample);
        return ValueTask.CompletedTask;

        static string? Read(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
