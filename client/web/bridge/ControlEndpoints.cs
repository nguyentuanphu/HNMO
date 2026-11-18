using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HNMO.Interfaces;

internal sealed record ControlAutoStartRequest(bool Enabled);

internal sealed record ControlServiceRequest(string Action);

internal sealed record ControlRestoreRequest(string FileName);

internal sealed record ControlSetupRequest(
    string AdminPassword,
    string GameName,
    string PublicAddress,
    int WebPort,
    int AdminPort,
    int ConnectServerPort,
    int DatabasePort,
    string Language);

internal static class ControlEndpoints
{
    public static void Map(
        WebApplication app,
        GameLauncher? launcher,
        ControlMaintenanceService? maintenance,
        LauncherOptions options,
        string contentRoot,
        string webRoot,
        HttpClient http,
        ILogger logger)
    {
        var saveDir = Path.GetFullPath(Path.Combine(contentRoot, options.SavePath));
        var setupMarker = Path.Combine(saveDir, "setup-complete.json");

        static bool IsLoopback(HttpContext ctx)
        {
            var ip = ctx.Connection.RemoteIpAddress;
            return ip is null || IPAddress.IsLoopback(ip);
        }

        static IResult Forbidden() => Results.StatusCode(StatusCodes.Status403Forbidden);

        app.MapGet("/control", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var path = Path.Combine(webRoot, "control.html");
            if (!File.Exists(path))
            {
                return Results.NotFound("control.html not found.");
            }

            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.SendFileAsync(path, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Empty;
        });

        app.MapGet("/api/control/status", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var dbUp = await ProbeTcpAsync("127.0.0.1", options.DatabasePort, ctx.RequestAborted).ConfigureAwait(false);
            var gameUp = await ProbeTcpAsync("127.0.0.1", options.ConnectServerPort, ctx.RequestAborted).ConfigureAwait(false);
            var adminUp = await ProbeHttpAsync(http, options.AdminUrl, ctx.RequestAborted).ConfigureAwait(false);
            var settings = MaintenanceSettings.Load(saveDir);
            var safeSettings = new MaintenanceSettings
            {
                MaxLogDays = settings.MaxLogDays,
                MaxLogTotalMb = settings.MaxLogTotalMb,
                MaxLogFileMb = settings.MaxLogFileMb,
                DbLogRetentionDays = settings.DbLogRetentionDays,
                AutoBackupEnabled = settings.AutoBackupEnabled,
                AutoBackupHourSydney = settings.AutoBackupHourSydney,
                BackupKeepDaily = settings.BackupKeepDaily,
                BackupKeepWeekly = settings.BackupKeepWeekly,
                BackupKeepMonthly = settings.BackupKeepMonthly,
                VacuumIntervalHours = settings.VacuumIntervalHours,
                LowDiskWarningMb = settings.LowDiskWarningMb,
                AutoRestartOnCrash = settings.AutoRestartOnCrash,
                MaxCrashRestartsPerHour = settings.MaxCrashRestartsPerHour,
                CrashRestartDelaySeconds = settings.CrashRestartDelaySeconds,
                CrashDiscordWebhookUrl = string.Empty,
            };
            var backups = ControlOperations.ListBackups(saveDir);
            var disk = ControlOperations.GetDiskUsage(saveDir, settings.LowDiskWarningMb);
            var incidents = CrashWatchStore.ListIncidents(saveDir);

            return Results.Ok(new
            {
                webGame = true,
                database = dbUp,
                gameServer = gameUp,
                adminPanel = adminUp,
                launcherEnabled = options.Enabled,
                isLaunching = launcher?.IsLaunching ?? false,
                lastError = launcher?.LastError,
                setupNeeded = !File.Exists(setupMarker),
                autoStartEnabled = IsAutoStartEnabled(),
                serviceInstalled = IsServiceInstalled(),
                backups,
                backupTotalBytes = backups.Sum(b => b.SizeBytes),
                disk,
                maintenanceSettings = safeSettings,
                crashWatch = new
                {
                    autoRestartOnCrash = settings.AutoRestartOnCrash,
                    maxRestartsPerHour = settings.MaxCrashRestartsPerHour,
                    delaySeconds = settings.CrashRestartDelaySeconds,
                    hasDiscordWebhook = !string.IsNullOrWhiteSpace(settings.CrashDiscordWebhookUrl),
                    unacknowledgedCount = incidents.Count(i => !i.Acknowledged),
                    incidents = incidents.Take(20).ToList(),
                },
                lastAutoBackupSydney = maintenance?.LastAutoBackupSydney,
                lastVacuumSydney = maintenance?.LastVacuumSydney,
                adminUrl = options.AdminUrl,
                gameUrl = options.OpenBrowserUrl ?? $"http://localhost:{options.WebPort}/",
                os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
                ports = new
                {
                    web = options.WebPort,
                    admin = Uri.TryCreate(options.AdminUrl, UriKind.Absolute, out var adminUri) ? adminUri.Port : 8095,
                    connectServer = options.ConnectServerPort,
                    database = options.DatabasePort,
                },
            });
        });

        app.MapPost("/api/control/start", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            if (launcher is null)
            {
                return Results.BadRequest(new { message = "Launcher is disabled." });
            }

            _ = Task.Run(() => launcher.StartGameAsync(CancellationToken.None));
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/control/stop", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            if (launcher is null)
            {
                return Results.BadRequest(new { message = "Launcher is disabled." });
            }

            await launcher.StopGameAsync(ctx.RequestAborted).ConfigureAwait(false);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/control/restart", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            if (launcher is null)
            {
                return Results.BadRequest(new { message = "Launcher is disabled." });
            }

            _ = Task.Run(() => launcher.RestartGameAsync(CancellationToken.None));
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/control/logs", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var logDir = Path.Combine(saveDir, "logs");
            var file = ctx.Request.Query["file"].ToString();
            var q = ctx.Request.Query["q"].ToString();
            var maxLines = int.TryParse(ctx.Request.Query["lines"].ToString(), out var parsed) ? parsed : 200;
            var files = RollingLogWriter.ListLogFiles(logDir);
            if (!string.IsNullOrWhiteSpace(file) || !string.IsNullOrWhiteSpace(q))
            {
                var matched = RollingLogWriter.SearchLines(logDir, string.IsNullOrWhiteSpace(file) ? "all" : file, q, maxLines);
                return Results.Ok(new { files, lines = matched, server = Array.Empty<string>(), database = Array.Empty<string>() });
            }

            var serverLines = ControlOperations.ReadTailLines(Path.Combine(logDir, "server.log"), 120);
            var dbLines = ControlOperations.ReadTailLines(Path.Combine(logDir, "database.log"), 60);
            return Results.Ok(new { files, lines = serverLines.Concat(dbLines).ToArray(), server = serverLines, database = dbLines });
        });

        app.MapGet("/api/control/logs/download", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var fileName = ctx.Request.Query["file"].ToString();
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..", StringComparison.Ordinal))
            {
                return Results.BadRequest(new { message = "Invalid log file name." });
            }

            var path = Path.Combine(saveDir, "logs", fileName);
            if (!File.Exists(path))
            {
                return Results.NotFound(new { message = "Log file not found." });
            }

            var contentType = fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? "application/gzip" : "text/plain; charset=utf-8";
            return Results.File(path, contentType, fileName);
        });

        app.MapGet("/api/control/maintenance-settings", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            return Results.Ok(MaintenanceSettings.Load(saveDir));
        });

        app.MapPost("/api/control/maintenance-settings", (HttpContext ctx, MaintenanceSettings req) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            if (string.Equals(req.CrashDiscordWebhookUrl, "__KEEP__", StringComparison.Ordinal))
            {
                req.CrashDiscordWebhookUrl = MaintenanceSettings.Load(saveDir).CrashDiscordWebhookUrl;
            }

            MaintenanceSettings.Save(saveDir, req);
            launcher?.RotateLogsIfNeeded();
            var cfg = MaintenanceSettings.Load(saveDir);
            ControlOperations.ApplyBackupRetention(saveDir, cfg.BackupKeepDaily, cfg.BackupKeepWeekly, cfg.BackupKeepMonthly);
            return Results.Ok(new
            {
                cfg.MaxLogDays,
                cfg.MaxLogTotalMb,
                cfg.MaxLogFileMb,
                cfg.DbLogRetentionDays,
                cfg.AutoBackupEnabled,
                cfg.AutoBackupHourSydney,
                cfg.BackupKeepDaily,
                cfg.BackupKeepWeekly,
                cfg.BackupKeepMonthly,
                cfg.VacuumIntervalHours,
                cfg.LowDiskWarningMb,
                cfg.AutoRestartOnCrash,
                cfg.MaxCrashRestartsPerHour,
                cfg.CrashRestartDelaySeconds,
                hasDiscordWebhook = !string.IsNullOrWhiteSpace(cfg.CrashDiscordWebhookUrl),
            });
        });

        app.MapPost("/api/control/crash-watch/ack", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var cleared = CrashWatchStore.AcknowledgeAll(saveDir);
            return Results.Ok(new { ok = true, cleared });
        });

        app.MapPost("/api/control/vacuum", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            if (maintenance is null)
            {
                return Results.BadRequest(new { message = "Maintenance service is unavailable." });
            }

            var result = await maintenance.RunMaintenanceCycleAsync(forceBackup: false, forceVacuum: true, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Ok(result);
        });

        app.MapPost("/api/control/autostart", (HttpContext ctx, ControlAutoStartRequest req) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            try
            {
                SetAutoStart(req.Enabled, contentRoot);
                return Results.Ok(new { enabled = IsAutoStartEnabled() });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not update auto-start setting.");
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/control/service", (HttpContext ctx, ControlServiceRequest req) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            try
            {
                ConfigureService(string.Equals(req.Action, "install", StringComparison.OrdinalIgnoreCase), contentRoot);
                return Results.Ok(new { installed = IsServiceInstalled() });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not configure system service.");
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/control/backup", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            Directory.CreateDirectory(saveDir);
            var dumpPath = Path.Combine(saveDir, "backup-db.sql.tmp");
            try
            {
                if (launcher is not null && await ProbeTcpAsync("127.0.0.1", options.DatabasePort, ctx.RequestAborted).ConfigureAwait(false))
                {
                    try
                    {
                        var pgDump = launcher.PostgresToolPath("pg_dump");
                        await RunPgDumpAsync(pgDump, options.DatabasePort, options.DatabasePassword, dumpPath, ctx.RequestAborted).ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogWarning(ex, "Skipping pg_dump during backup.");
                    }
                }

                var cfg = MaintenanceSettings.Load(saveDir);
                var backup = ControlOperations.CreateBackupZip(saveDir, File.Exists(dumpPath) ? dumpPath : null);
                var summary = ControlOperations.ApplyBackupRetention(saveDir, cfg.BackupKeepDaily, cfg.BackupKeepWeekly, cfg.BackupKeepMonthly);
                return Results.Ok(new { backup, summary });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backup failed.");
                return Results.BadRequest(new { message = ex.Message });
            }
            finally
            {
                if (File.Exists(dumpPath))
                {
                    File.Delete(dumpPath);
                }
            }
        });

        app.MapPost("/api/control/restore", async (HttpContext ctx, ControlRestoreRequest req) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            try
            {
                if (launcher is not null)
                {
                    await launcher.StopGameAsync(ctx.RequestAborted).ConfigureAwait(false);
                }

                var sqlPath = ControlOperations.RestoreBackupZip(saveDir, req.FileName);
                var dbRestored = false;
                if (sqlPath is not null && File.Exists(sqlPath))
                {
                    try
                    {
                        if (launcher is not null && options.Enabled)
                        {
                            await launcher.RestoreDatabaseSqlAsync(sqlPath, ctx.RequestAborted).ConfigureAwait(false);
                            dbRestored = true;
                        }
                    }
                    finally
                    {
                        File.Delete(sqlPath);
                    }
                }

                return Results.Ok(new { ok = true, dbRestored, message = dbRestored ? "Backup and database restored. Start HNMO to play." : "Save files restored. Start HNMO to use the restored save." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Restore failed.");
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapGet("/api/control/updates", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/nguyentuanphu/HNMO/releases/latest");
                request.Headers.UserAgent.ParseAdd("HNMO-Control");
                using var response = await http.SendAsync(request, ctx.RequestAborted).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Results.BadRequest(new { message = $"GitHub returned {(int)response.StatusCode}." });
                }

                var json = await response.Content.ReadAsStringAsync(ctx.RequestAborted).ConfigureAwait(false);
                var stampFile = Path.Combine(webRoot, ".hnmo-build");
                var localStamp = File.Exists(stampFile) ? File.ReadAllText(stampFile).Trim() : null;
                return Results.Ok(ControlOperations.ParseGitHubRelease(json, localStamp));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        app.MapPost("/api/control/setup", async (HttpContext ctx, ControlSetupRequest req) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            if (string.IsNullOrWhiteSpace(req.AdminPassword) || req.AdminPassword.Length < 4)
            {
                return Results.BadRequest(new { message = "Admin password must be at least 4 characters." });
            }

            int[] ports = [req.WebPort, req.AdminPort, req.ConnectServerPort, req.DatabasePort];
            if (ports.Any(p => p is < 1 or > 65535) || ports.Distinct().Count() != ports.Length)
            {
                return Results.BadRequest(new { message = "Ports must be unique numbers between 1 and 65535." });
            }

            Directory.CreateDirectory(saveDir);
            var publicAddr = string.IsNullOrWhiteSpace(req.PublicAddress) ? "localhost" : req.PublicAddress.Trim();
            var settingsJson = JsonSerializer.Serialize(new
            {
                WebPort = req.WebPort,
                AdminPort = req.AdminPort,
                ConnectServerPort = req.ConnectServerPort,
                DatabasePort = req.DatabasePort,
                PublicAddress = publicAddr,
                WebUrls = $"http://0.0.0.0:{req.WebPort}",
                OpenBrowserUrl = $"http://{publicAddr}:{req.WebPort}/",
                LauncherEnabled = true,
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(saveDir, "hnmo-settings.json"), settingsJson, ctx.RequestAborted).ConfigureAwait(false);
            var setupPayload = JsonSerializer.Serialize(new
            {
                gameName = string.IsNullOrWhiteSpace(req.GameName) ? "HNMO" : req.GameName.Trim(),
                language = req.Language is "vi" ? "vi" : "en",
                completedUtc = DateTimeOffset.UtcNow,
            });
            await File.WriteAllTextAsync(setupMarker, setupPayload, ctx.RequestAborted).ConfigureAwait(false);

            if (launcher is not null)
            {
                _ = Task.Run(() => launcher.StartGameAsync(CancellationToken.None));
            }

            return Results.Ok(new { ok = true });
        });
    }

    private static async Task<bool> ProbeTcpAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken).AsTask().WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ProbeHttpAsync(HttpClient http, string url, CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).WaitAsync(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
            return (int)res.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    internal static async Task RunPgDumpAsync(string pgDumpPath, int port, string password, string outFile, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(pgDumpPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-h");
        psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(port.ToString());
        psi.ArgumentList.Add("-U");
        psi.ArgumentList.Add("postgres");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(outFile);
        psi.ArgumentList.Add("hnmo");
        psi.Environment["PGPASSWORD"] = password;
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("pg_dump could not start.");
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"pg_dump failed: {err}");
        }
    }

    private static bool IsAutoStartEnabled()
    {
        if (OperatingSystem.IsLinux())
        {
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "hnmo.desktop"));
        }

        if (OperatingSystem.IsMacOS())
        {
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "com.hnmo.launcher.plist"));
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo("reg.exe", "query \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v HNMO")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                proc?.WaitForExit(3000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static void SetAutoStart(bool enabled, string contentRoot)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(contentRoot, "HNMO");
        if (OperatingSystem.IsWindows())
        {
            var args = enabled
                ? $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v HNMO /t REG_SZ /d \"\\\"{exe}\\\"\" /f"
                : "delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v HNMO /f";
            RunSystemTool("reg.exe", args);
        }
        else if (OperatingSystem.IsLinux())
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
            var desktop = Path.Combine(dir, "hnmo.desktop");
            if (enabled)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(desktop, $"[Desktop Entry]\nType=Application\nName=HNMO\nExec=\"{exe}\"\nPath={contentRoot}\nTerminal=false\n");
            }
            else if (File.Exists(desktop))
            {
                File.Delete(desktop);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
            var plist = Path.Combine(dir, "com.hnmo.launcher.plist");
            if (enabled)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(plist, $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n<plist version=\"1.0\"><dict><key>Label</key><string>com.hnmo.launcher</string><key>ProgramArguments</key><array><string>{exe}</string></array><key>WorkingDirectory</key><string>{contentRoot}</string><key>RunAtLoad</key><true/></dict></plist>\n");
            }
            else if (File.Exists(plist))
            {
                File.Delete(plist);
            }
        }
    }

    private static bool IsServiceInstalled()
    {
        if (OperatingSystem.IsLinux())
        {
            return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user", "hnmo.service"));
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo("sc.exe", "query HNMO")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                proc?.WaitForExit(3000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static void ConfigureService(bool install, string contentRoot)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(contentRoot, "HNMO");
        if (OperatingSystem.IsWindows())
        {
            if (install)
            {
                RunSystemTool("sc.exe", $"create HNMO binPath= \"\\\"{exe}\\\"\" start= auto");
            }
            else
            {
                RunSystemTool("sc.exe", "delete HNMO");
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user");
            var unit = Path.Combine(dir, "hnmo.service");
            if (install)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(unit, $"[Unit]\nDescription=HNMO Game Server & Web Bridge\nAfter=network.target\n\n[Service]\nWorkingDirectory={contentRoot}\nExecStart=\"{exe}\"\nRestart=on-failure\n\n[Install]\nWantedBy=default.target\n");
                RunSystemTool("systemctl", "--user daemon-reload");
                RunSystemTool("systemctl", "--user enable hnmo.service");
            }
            else
            {
                if (File.Exists(unit))
                {
                    RunSystemTool("systemctl", "--user disable hnmo.service");
                    File.Delete(unit);
                    RunSystemTool("systemctl", "--user daemon-reload");
                }
            }
        }
        else
        {
            throw new PlatformNotSupportedException("System service installation is supported on Windows and Linux.");
        }
    }

    private static void RunSystemTool(string fileName, string arguments)
    {
        using var proc = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException($"{fileName} could not start.");
        proc.WaitForExit(10_000);
        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            var output = proc.StandardOutput.ReadToEnd();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(err) ? output : err);
        }
    }
}
