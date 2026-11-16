// Starts the whole game from one program: a portable PostgreSQL that keeps the save
// game in the HNMO folder, then the game server (with its admin panel), then opens the
// browser. Stopping HNMO (closing its window or Ctrl+C) stops both again.
//
// Only active when "Launcher:Enabled" is true, which the HNMO folder sets; in
// development the server and database are started separately.

using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using HNMO.Interfaces;

internal sealed class LauncherOptions
{
    public bool Enabled { get; set; }

    public string PostgresPath { get; set; } = "../postgres/bin";

    public string SavePath { get; set; } = "../save";

    public int DatabasePort { get; set; } = 55432;

    public string DatabasePassword { get; set; } = "admin";

    public string ServerPath { get; set; } = "../server";

    public string ServerProgram { get; set; } = "HNMO.Startup";

    public string AdminUrl { get; set; } = "http://127.0.0.1:8095";

    public int ConnectServerPort { get; set; } = 44406;

    public string? OpenBrowserUrl { get; set; } = "http://localhost:8080/";

    public string? SettingsFilePath { get; set; }

    public int WebPort { get; set; } = 8080;
}

internal sealed class GameLauncher(LauncherOptions options, string contentRoot, IHostApplicationLifetime lifetime, ILogger<GameLauncher> logger) : IHostedService
{
    private static readonly TimeSpan DatabaseStartTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ServerStartTimeout = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private Process? _server;
    private bool _databaseStarted;
    private int _stopped;
    private PosixSignalRegistration? _closeWindow;

    public bool IsLaunching { get; private set; }

    public string? LastError { get; private set; }

    public string SaveFullPath => this.Full(options.SavePath);

    public string PostgresToolPath(string name) => this.PostgresTool(name);

    // The game server's process of the last start (id and start time), so a server left running by a
    // window that was closed hard can be found and stopped at the next start.
    private string ServerPidFile => Path.Combine(this.Full(options.SavePath), "server.pid");

    private string Full(string path) => Path.GetFullPath(Path.Combine(contentRoot, path));

    private string DatabaseDirectory => Path.Combine(this.Full(options.SavePath), "database");

    private string LogDirectory => Path.Combine(this.Full(options.SavePath), "logs");

    private string? _postgresBin;

    private string PostgresTool(string name) => Path.Combine(this._postgresBin ??= this.FindPostgres(), OperatingSystem.IsWindows() ? name + ".exe" : name);

    // The folder's own PostgreSQL (Windows), else one installed on the computer (Ubuntu: apt install
    // postgresql-16; macOS: brew install postgresql@16 or Postgres.app).
    private string FindPostgres()
    {
        var tool = OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl";
        var candidates = new List<string> { this.Full(options.PostgresPath) };
        if (OperatingSystem.IsLinux())
        {
            foreach (var version in new[] { "17", "16", "15", "14" })
            {
                candidates.Add($"/usr/lib/postgresql/{version}/bin");
            }

            candidates.Add("/usr/pgsql-16/bin");
        }
        else if (OperatingSystem.IsMacOS())
        {
            foreach (var root in new[] { "/opt/homebrew/opt", "/usr/local/opt" })
            {
                foreach (var version in new[] { "postgresql@17", "postgresql@16", "postgresql@15", "postgresql" })
                {
                    candidates.Add($"{root}/{version}/bin");
                }
            }

            candidates.Add("/Applications/Postgres.app/Contents/Versions/latest/bin");
            candidates.Add("/opt/homebrew/bin");
            candidates.Add("/usr/local/bin");
        }

        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        var found = candidates.FirstOrDefault(directory => File.Exists(Path.Combine(directory, tool)));
        if (found is null)
        {
            var hint = OperatingSystem.IsMacOS() ? "brew install postgresql@16 (or Postgres.app)"
                : OperatingSystem.IsLinux() ? "sudo apt install postgresql-16"
                : "the folder's postgres folder is missing";
            throw new InvalidOperationException($"PostgreSQL wasn't found. Install it: {hint}.");
        }

        logger.LogInformation("PostgreSQL: {Path}", found);
        return found;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Task.CompletedTask;
        }

        // Closing the window (Windows) or the terminal (SIGHUP) ends this program at once, without the
        // usual shutdown: stop the game server and the database right here, or they stay behind.
        this._closeWindow = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ => this.StopNow());
        AppDomain.CurrentDomain.ProcessExit += (_, _) => this.StopNow();

        // Start in the background, so the web part answers right away and shows the game/control page while the server comes up.
        _ = Task.Run(() => this.LaunchAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StartGameAsync(CancellationToken cancellationToken = default)
    {
        await this._lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._server is { HasExited: false })
            {
                return;
            }

            Interlocked.Exchange(ref this._stopped, 0);
            await this.LaunchAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this._lifecycleLock.Release();
        }
    }

    public async Task StopGameAsync(CancellationToken cancellationToken = default)
    {
        await this._lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.StopNow();
        }
        finally
        {
            this._lifecycleLock.Release();
        }
    }

    public async Task RestartGameAsync(CancellationToken cancellationToken = default)
    {
        await this.StopGameAsync(cancellationToken).ConfigureAwait(false);
        await this.StartGameAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.StopNow();
        return Task.CompletedTask;
    }

    // Stops the game server and the database (once; also from the window-close handler, which has only
    // a few seconds).
    private void StopNow()
    {
        if (Interlocked.Exchange(ref this._stopped, 1) == 1)
        {
            return;
        }

        try
        {
            if (this._server is { HasExited: false })
            {
                logger.LogInformation("Stopping the game server...");
                this._server.Kill(entireProcessTree: true);
                this._server.WaitForExit(5000);
            }

            File.Delete(this.ServerPidFile);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(ex, "The game server didn't stop cleanly.");
        }

        if (this._databaseStarted)
        {
            logger.LogInformation("Stopping the database...");
            this.StopDatabase("fast");
        }

        this._closeWindow?.Dispose();
    }

    private void StopDatabase(string mode)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(this.PostgresTool("pg_ctl"), ["stop", "-D", this.DatabaseDirectory, "-m", mode, "-w", "-t", "10"])
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            });
            process?.WaitForExit(10_000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(ex, "The database didn't stop cleanly.");
        }
    }

    // A game server of this folder that's still running from an earlier start (its window was closed
    // hard, or the computer slept): stopped, so this start can go on.
    private async Task StopLeftoverServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(this.ServerPidFile))
            {
                return;
            }

            var parts = (await File.ReadAllTextAsync(this.ServerPidFile, cancellationToken).ConfigureAwait(false)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            File.Delete(this.ServerPidFile);
            if (parts.Length < 2 || !int.TryParse(parts[0], out var id) || !long.TryParse(parts[1], out var started))
            {
                return;
            }

            using var process = Process.GetProcessById(id);
            // The start time as another process reads it can differ by a little (rounding): a few seconds' slack.
            if (process.HasExited || Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) > TimeSpan.TicksPerSecond * 5)
            {
                return; // the id belongs to another program now
            }

            logger.LogWarning("The game server of an earlier start is still running (process {Id}); stopping it.", id);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < 15 && await IsPortOpenAsync(options.ConnectServerPort, cancellationToken).ConfigureAwait(false); i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            // Not running any more, or not ours: the port check below says what's left.
            logger.LogInformation("No game server left from an earlier start ({Reason}).", ex.Message);
        }
    }

    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.IsLaunching = true;
            this.LastError = null;
            Directory.CreateDirectory(this.LogDirectory);
            await this.StopLeftoverServerAsync(cancellationToken).ConfigureAwait(false);
            if (await IsPortOpenAsync(options.ConnectServerPort, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"Another program uses port {options.ConnectServerPort} (another HNMO folder, or a game server started by hand). "
                    + "Close it (Task Manager: \".NET Host\" / \"dotnet\" / \"HNMO\"), or restart the computer, and start HNMO again.");
            }

            await this.StartDatabaseAsync(cancellationToken).ConfigureAwait(false);
            this.StartServer();
            logger.LogInformation("The game server is starting. The first start sets up the save game and takes a few minutes.");
            await WaitForPortAsync(options.ConnectServerPort, ServerStartTimeout, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("HNMO is ready. Play: {Play}  Control: http://localhost:{WebPort}/control  Admin panel: {Admin} (this computer only)", options.OpenBrowserUrl, options.WebPort, options.AdminUrl);
            var controlUrl = $"http://localhost:{options.WebPort}/control";
            OpenBrowser(File.Exists(Path.Combine(this.SaveFullPath, "setup-complete.json")) ? options.OpenBrowserUrl : controlUrl);
        }
        catch (Exception ex)
        {
            this.LastError = ex.Message;
            logger.LogError(ex, "HNMO couldn't start. See the logs in {LogDirectory}.", this.LogDirectory);
        }
        finally
        {
            this.IsLaunching = false;
        }
    }

    private async Task StartDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(this.DatabaseDirectory, "PG_VERSION")))
        {
            logger.LogInformation("Creating the save game database in {Directory}...", this.DatabaseDirectory);
            Directory.CreateDirectory(this.DatabaseDirectory);
            var passwordFile = Path.Combine(this.Full(options.SavePath), "db-password.tmp");
            await File.WriteAllTextAsync(passwordFile, options.DatabasePassword, cancellationToken).ConfigureAwait(false);
            try
            {
                await RunAsync(this.PostgresTool("initdb"), ["-D", this.DatabaseDirectory, "-U", "postgres", "--pwfile", passwordFile, "-A", "scram-sha-256", "-E", "UTF8", "--locale=C"], cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                File.Delete(passwordFile);
            }
        }

        if (File.Exists(Path.Combine(this.DatabaseDirectory, "postmaster.pid")) && await this.DatabaseRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            // Still running from an earlier start whose window was closed hard: use it.
            logger.LogInformation("The database is still running from an earlier start; using it.");
            this._databaseStarted = true;
            return;
        }

        logger.LogInformation("Starting the database on port {Port}...", options.DatabasePort);
        var logFile = Path.Combine(this.LogDirectory, "database.log");
        // Only reachable from this computer.
        var settings = $"-p {options.DatabasePort} -c listen_addresses=127.0.0.1";
        if (!OperatingSystem.IsWindows())
        {
            // Linux and macOS: keep the database's socket file in the save folder, not in a system folder.
            settings += $" -c unix_socket_directories='{this.DatabaseDirectory}'";
        }
        await RunAsync(this.PostgresTool("pg_ctl"), ["start", "-D", this.DatabaseDirectory, "-l", logFile, "-o", settings, "-w", "-t", ((int)DatabaseStartTimeout.TotalSeconds).ToString()], cancellationToken).ConfigureAwait(false);
        this._databaseStarted = true;
    }

    private async Task<bool> DatabaseRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(this.PostgresTool("pg_ctl"), ["status", "-D", this.DatabaseDirectory], cancellationToken).ConfigureAwait(false);
            return true; // exit code 0: running
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void StartServer()
    {
        var serverDirectory = this.Full(options.ServerPath);
        var program = Path.Combine(serverDirectory, OperatingSystem.IsWindows() ? options.ServerProgram + ".exe" : options.ServerProgram);
        string[] arguments = ["-autostart", "-resolveIP:loopback", "-deamon"];
        if (!File.Exists(program) && File.Exists(Path.Combine(serverDirectory, options.ServerProgram + ".dll")))
        {
            // The folder for every system: the server is a portable .NET program, started with the same
            // .NET runtime this web server runs on (the folder's runtime for this system).
            arguments = [Path.Combine(serverDirectory, options.ServerProgram + ".dll"), .. arguments];
            program = Environment.ProcessPath ?? "dotnet";
        }

        var start = new ProcessStartInfo(program, arguments)
        {
            WorkingDirectory = serverDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.Environment["ASPNETCORE_URLS"] = options.AdminUrl;
        start.Environment["DB_ADMIN_PW"] = options.DatabasePassword;
        if (!string.IsNullOrWhiteSpace(options.SettingsFilePath))
        {
            start.Environment[HostSettingsStore.EnvironmentVariableName] = options.SettingsFilePath;
        }

        var log = new StreamWriter(Path.Combine(this.LogDirectory, "server.log"), append: true) { AutoFlush = true };
        this._server = Process.Start(start) ?? throw new InvalidOperationException("The game server didn't start.");
        File.WriteAllText(this.ServerPidFile, $"{this._server.Id} {this._server.StartTime.ToUniversalTime().Ticks}");
        this._server.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (log) { log.WriteLine(e.Data); } } };
        this._server.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (log) { log.WriteLine(e.Data); } } };
        this._server.BeginOutputReadLine();
        this._server.BeginErrorReadLine();
        this._server.EnableRaisingEvents = true;
        this._server.Exited += (_, _) =>
        {
            var exitCode = this._server.ExitCode;
            if (exitCode == HostSettingsStore.RestartExitCode && Volatile.Read(ref this._stopped) == 0)
            {
                logger.LogInformation("Restarting HNMO...");
                this.RestartSelf();
                return;
            }

            logger.LogWarning("The game server stopped. See {Log}.", Path.Combine(this.LogDirectory, "server.log"));
        };
    }

    private void RestartSelf()
    {
        this.StopNow();
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            var rawArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
            var psi = new ProcessStartInfo(exe)
            {
                WorkingDirectory = contentRoot,
                UseShellExecute = false,
            };
            foreach (var arg in rawArgs)
            {
                psi.ArgumentList.Add(arg);
            }

            Process.Start(psi);
        }

        lifetime.StopApplication();
    }

    private static async Task RunAsync(string program, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(program, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{program} didn't start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(program)} failed: {await error.ConfigureAwait(false)}{await output.ConfigureAwait(false)}");
        }
    }

    private static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task WaitForPortAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"The game server didn't open port {port} in time.");
    }

    private static void OpenBrowser(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception)
        {
            // No browser to open (e.g. on a server): the address is in the log.
        }
    }
}
