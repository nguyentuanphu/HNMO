// <copyright file="RelayPortsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Bridge;

/// <summary>The game relay reaches game servers only, never HNMO's other services (audit 2.8).</summary>
[TestFixture]
public class RelayPortsTests
{
    /// <summary>The database, both admin panels and the web server are refused; game ports are not.</summary>
    /// <param name="port">The requested port.</param>
    /// <param name="refused">Whether it's refused.</param>
    [TestCase(55432, true)]
    [TestCase(5432, true)]
    [TestCase(8095, true)]
    [TestCase(8096, true)]
    [TestCase(8080, true)]
    [TestCase(44406, false)]
    [TestCase(44506, false)]
    [TestCase(55901, false)]
    [TestCase(46001, false)]
    public void OnlyGamePortsAreRelayed(int port, bool refused)
    {
        Assert.That(RelayPorts.IsRefused(port, databasePort: 55432, adminPort: 8095, webPort: 8080), Is.EqualTo(refused));
    }
}
