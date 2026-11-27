// <copyright file="HostSettings.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Unified host settings stored in <c>save/hnmo-settings.json</c> and shared by the
/// web bridge, game server, and admin panel.
/// </summary>
public sealed class HostSettings
{
    /// <summary>
    /// Gets or sets the web game HTTP port.
    /// </summary>
    public int WebPort { get; set; } = 8080;

    /// <summary>
    /// Gets or sets the admin panel HTTP port (bound to loopback).
    /// </summary>
    public int AdminPort { get; set; } = 8095;

    /// <summary>
    /// Gets or sets the connect server TCP port.
    /// </summary>
    public int ConnectServerPort { get; set; } = 44406;

    /// <summary>
    /// Gets or sets the PostgreSQL database TCP port.
    /// </summary>
    public int DatabasePort { get; set; } = 55432;

    /// <summary>
    /// Gets or sets the public domain or IP address players use to reach the web game.
    /// </summary>
    public string PublicAddress { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the URLs the web bridge listens on.
    /// </summary>
    public string WebUrls { get; set; } = "http://0.0.0.0:8080";

    /// <summary>
    /// Gets or sets the web client root directory relative to the bridge folder.
    /// </summary>
    public string WebRoot { get; set; } = "wwwroot";

    /// <summary>
    /// Gets or sets the game data directory relative to the bridge folder.
    /// </summary>
    public string DataPath { get; set; } = "../Data";

    /// <summary>
    /// Gets or sets the vendor directory relative to the bridge folder.
    /// </summary>
    public string VendorPath { get; set; } = "vendor";

    /// <summary>
    /// Gets or sets the hosts the WebSocket-to-TCP relay may connect to.
    /// </summary>
    public string[] RelayAllowedHosts { get; set; } = ["127.0.0.1", "localhost"];

    /// <summary>
    /// Gets or sets a value indicating whether loopback relay connections are allowed.
    /// </summary>
    public bool RelayAllowLoopback { get; set; } = true;

    /// <summary>
    /// Gets or sets the relay TCP connect timeout in seconds.
    /// </summary>
    public int RelayConnectTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the bridge starts PostgreSQL and the game server automatically.
    /// </summary>
    public bool LauncherEnabled { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL <c>bin</c> directory relative to the bridge folder.
    /// </summary>
    public string PostgresPath { get; set; } = "../postgres/bin";

    /// <summary>
    /// Gets or sets the save directory relative to the bridge folder.
    /// </summary>
    public string SavePath { get; set; } = "../save";

    /// <summary>
    /// Gets or sets the server directory relative to the bridge folder.
    /// </summary>
    public string ServerPath { get; set; } = "../server";

    /// <summary>
    /// Gets or sets the server executable name.
    /// </summary>
    public string ServerProgram { get; set; } = "HNMO.Startup";

    /// <summary>
    /// Gets or sets the URL opened in the browser when HNMO finishes starting.
    /// </summary>
    public string OpenBrowserUrl { get; set; } = "http://localhost:8080/";

    /// <summary>
    /// Gets or sets the database host name or IP address.
    /// </summary>
    public string DatabaseHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string DatabaseName { get; set; } = "hnmo";

    /// <summary>
    /// Gets or sets the database user name.
    /// </summary>
    public string DatabaseUser { get; set; } = "postgres";

    /// <summary>
    /// Gets or sets the database password.
    /// </summary>
    public string DatabasePassword { get; set; } = "admin";

    /// <summary>
    /// Gets or sets a value indicating whether the launcher also starts a Season 9 game server program next to Season
    /// 6.3's (its own database, ports 100 higher, admin panel on the next port; accounts and coins shared).
    /// </summary>
    public bool SeasonNineEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the launcher also starts the HNMO Arena program: battles across servers
    /// and seasons (its own database, ports 200 higher, admin panel two ports up; accounts shared).
    /// </summary>
    public bool ArenaEnabled { get; set; }

    /// <summary>
    /// Gets or sets the player license file path (relative to the package root or server directory).
    /// </summary>
    public string LicenseFilePath { get; set; } = "license.hnmo";
}
