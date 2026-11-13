// <copyright file="IllusionTempleEnterResultMappingTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.GameLogic.PlayerActions.MiniGames;
using HNMO.GameServer.RemoteView.MiniGames;

/// <summary>
/// Tests the admission result bytes which select the Illusion Temple client's messages.
/// </summary>
[TestFixture]
public class IllusionTempleEnterResultMappingTests
{
    /// <summary>
    /// Verifies that each admission outcome uses the corresponding client result code.
    /// </summary>
    /// <param name="result">The admission outcome.</param>
    /// <param name="expected">The result byte understood by the client.</param>
    [TestCase(EnterResult.Success, (byte)0)]
    [TestCase(EnterResult.Failed, (byte)3)]
    [TestCase(EnterResult.NotOpen, (byte)1)]
    [TestCase(EnterResult.CharacterLevelTooHigh, (byte)3)]
    [TestCase(EnterResult.CharacterLevelTooLow, (byte)3)]
    [TestCase(EnterResult.Full, (byte)4)]
    [TestCase(EnterResult.NotEnoughMoney, (byte)3)]
    [TestCase(EnterResult.PlayerKillerCantEnter, (byte)7)]
    public void EnterResultUsesClientAdmissionCode(EnterResult result, byte expected)
    {
        Assert.That(result.ToIllusionTempleEnterResult(), Is.EqualTo(expected));
    }
}
