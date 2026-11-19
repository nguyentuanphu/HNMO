/// <summary>
/// Ports the game relay (/relay) never connects to (audit 2.8): HNMO's own services that aren't game servers. The
/// relay frames game packets only, but a visitor shouldn't reach the database or the admin panels through it.
/// </summary>
internal static class RelayPorts
{
    /// <summary>PostgreSQL's default port, refused even when HNMO's database runs on another one.</summary>
    public const int PostgresDefaultPort = 5432;

    /// <summary>Whether the relay refuses a port.</summary>
    /// <param name="port">The requested port.</param>
    /// <param name="databasePort">HNMO's database port.</param>
    /// <param name="adminPort">Season 6.3's admin panel port (Season 9's is the next one).</param>
    /// <param name="webPort">This web server's port.</param>
    /// <returns><c>true</c> if the port is refused.</returns>
    public static bool IsRefused(int port, int databasePort, int adminPort, int webPort) =>
        port == PostgresDefaultPort
        || port == databasePort
        || port == adminPort
        || port == adminPort + 1
        || port == webPort;
}
