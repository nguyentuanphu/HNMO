// <copyright file="JewelBankCommandPacketTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameServer.MessageHandler;
using HNMO.Network.Packets.ClientToServer;

/// <summary>
/// The Jewel Bank's request as the web game sends it when the window opens: no key, ten bytes.
/// The server used to drop it (it wanted eleven), so the window stayed on "Loading" for good.
/// </summary>
[TestFixture]
public class JewelBankCommandPacketTest
{
    /// <summary>A command without a key is complete and is read with an empty key.</summary>
    [Test]
    public void AStateRequestWithoutAKeyIsAccepted()
    {
        // C1, length 10, F5 3A, action 1 (request), slot 0, amount 0: the bytes the web game sent.
        byte[] bytes = [0xC1, 0x0A, 0xF5, 0x3A, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];
        HnmoJewelBankCommand command = bytes.AsMemory();

        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(HnmoJewelBankCommandHandlerPlugIn.MinimumLength));
        Assert.That(command.Action, Is.EqualTo(1));
        Assert.That(command.Key, Is.Empty);
    }

    /// <summary>The state sent while the plug-in is off says so, so the window can tell the player.</summary>
    [Test]
    public void TheTurnedOffStateSaysDisabled()
    {
        using var json = System.Text.Json.JsonDocument.Parse(HNMO.GameLogic.PlugIns.JewelBank.JewelBankStateWriter.DisabledState);
        Assert.That(json.RootElement.GetProperty("enabled").GetBoolean(), Is.False);
        Assert.That(json.RootElement.GetProperty("jewels").GetArrayLength(), Is.Zero);
    }
}
