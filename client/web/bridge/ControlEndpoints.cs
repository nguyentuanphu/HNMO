using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HNMO.Interfaces;

internal sealed record ControlAutoStartRequest(bool Enabled);

internal sealed record ControlServiceRequest(string Action);

internal sealed record ControlRestoreRequest(string FileName);

// AdminPassword: sent by older control pages and ignored; the first admin user is made on the admin panel's /users page.
internal sealed record ControlSetupRequest(
    string? AdminPassword,
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
        ILogger logger,
        PortalOptions? portalOptions = null)
    {
        var saveDir = Path.GetFullPath(Path.Combine(contentRoot, options.SavePath));
        var setupMarker = Path.Combine(saveDir, "setup-complete.json");

        static bool IsLoopback(HttpContext ctx) => HostOnly.IsHostRequest(ctx);

        static IResult Forbidden() => Results.StatusCode(StatusCodes.Status403Forbidden);

        async Task<IResult> SendControlAsync(HttpContext ctx, string fileName = "control.html")
        {
            var path = Path.Combine(webRoot, fileName);
            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            ctx.Response.Headers.CacheControl = "no-store";
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.SendFileAsync(path, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Empty;
        }

        app.MapGet("/control", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            ctx.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            return await SendControlAsync(ctx, options.Enabled && File.Exists(setupMarker) ? "control-recovery.html" : "control.html").ConfigureAwait(false);
        });

        app.MapGet("/control/console", async (HttpContext ctx) =>
        {
            if (!Uri.TryCreate(options.AdminUrl, UriKind.Absolute, out var admin) || !HostOnly.IsAdminFrame(ctx, admin))
            {
                return Forbidden();
            }

            var origin = admin.GetLeftPart(UriPartial.Authority);
            var alternate = new UriBuilder(admin) { Host = admin.Host == "localhost" ? "127.0.0.1" : "localhost" };
            ctx.Response.Headers.ContentSecurityPolicy = $"frame-ancestors {origin} {alternate.Uri.GetLeftPart(UriPartial.Authority)}";
            return await SendControlAsync(ctx).ConfigureAwait(false);
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
            var websiteAdminPath = AdminGateway.Current is { } gateway ? await gateway.PathAsync(ctx.RequestAborted).ConfigureAwait(false) : string.Empty;

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
                websiteAdminPath,
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

        app.MapGet("/api/control/ports", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var admin = Uri.TryCreate(options.AdminUrl, UriKind.Absolute, out var address) ? address.Port : 8095;
            var ports = new[]
            {
                (Service: "web", Port: options.WebPort),
                (Service: "admin", Port: admin),
                (Service: "game", Port: options.ConnectServerPort),
                (Service: "database", Port: options.DatabasePort),
            };
            var listeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners().Select(listener => listener.Port).ToHashSet();
            return Results.Ok(ports.Select(port => new
            {
                port.Service,
                port.Port,
                Listening = listeners.Contains(port.Port),
                Owner = listeners.Contains(port.Port) ? PortOwner.Describe(port.Port) : null,
            }));
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

        // "Stop HNMO" (owner's choice 2026-11-21: no console window to close): stops the game, the database and this program.
        app.MapPost("/api/control/shutdown", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            logger.LogInformation("Stopping HNMO (asked from {Page}).", ctx.Request.Headers.Referer.ToString());
            _ = Task.Run(async () =>
            {
                await Task.Delay(250).ConfigureAwait(false); // let the HTTP response go out first
                launcher?.RequestShutdownStop();
                app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            });
            return Results.Ok(new { ok = true });
        });

        // HNMO updates (owner's choice, 2026-11-21: the host presses "Update"; Updater.cs). The HNMO folder is the one
        // above the web folder; a folder without manifest.json (a developer's checkout) can't update itself.
        var adminPort = Uri.TryCreate(options.AdminUrl, UriKind.Absolute, out var adminUri) ? adminUri.Port : 8095;
        var updater = new HnmoUpdater(Path.GetFullPath(Path.Combine(contentRoot, "..")), saveDir, options.WebPort, adminPort, app.Services.GetRequiredService<IHttpClientFactory>(), logger);
        app.MapGet("/api/control/update", async (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var check = ctx.Request.Query.ContainsKey("check");
            return Results.Ok(await updater.StatusAsync(check, ctx.RequestAborted).ConfigureAwait(false));
        });

        app.MapPost("/api/control/update", (HttpContext ctx) =>
        {
            if (!IsLoopback(ctx))
            {
                return Forbidden();
            }

            var (ok, message) = updater.Start(() =>
            {
                logger.LogInformation("Stopping HNMO for the update.");
                app.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                return Task.CompletedTask;
            });
            return ok ? Results.Ok(new { ok, message }) : Results.BadRequest(new { message });
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
                SyncLiveOptionsFromSavedSettings(saveDir, options, portalOptions);
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
                // The newest release as the HNMO developer published it on the hub (no link to where HNMO is built).
                if (DeveloperHubAddress.LatestRelease is not { Length: > 0 } latestUrl)
                {
                    return Results.BadRequest(new { message = "Update checks are off on this server." });
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, latestUrl);
                request.Headers.UserAgent.ParseAdd("HNMO-Control");
                using var response = await http.SendAsync(request, ctx.RequestAborted).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Results.BadRequest(new { message = response.StatusCode == System.Net.HttpStatusCode.NotFound ? "No release published yet." : $"The update check answered {(int)response.StatusCode}." });
                }

                var json = await response.Content.ReadAsStringAsync(ctx.RequestAborted).ConfigureAwait(false);
                var stampFile = Path.Combine(webRoot, ".hnmo-build");
                var localStamp = File.Exists(stampFile) ? File.ReadAllText(stampFile).Trim() : null;
                return Results.Ok(ControlOperations.ParseRelease(json, localStamp));
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

            int[] ports = [req.WebPort, req.AdminPort, req.ConnectServerPort, req.DatabasePort];
            if (ports.Any(p => p is < 1 or > 65535) || ports.Distinct().Count() != ports.Length)
            {
                return Results.BadRequest(new { message = "Ports must be unique numbers between 1 and 65535." });
            }

            Directory.CreateDirectory(saveDir);
            var publicAddr = string.IsNullOrWhiteSpace(req.PublicAddress) ? "localhost" : req.PublicAddress.Trim();
            // Only the wizard's own fields change: whatever else the host has set before (database password,
            // relay hosts, paths) stays in the file.
            var settingsPath = Path.Combine(saveDir, "hnmo-settings.json");
            System.Text.Json.Nodes.JsonObject settings;
            try
            {
                settings = File.Exists(settingsPath)
                    ? System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(settingsPath, ctx.RequestAborted).ConfigureAwait(false)) as System.Text.Json.Nodes.JsonObject ?? []
                    : [];
            }
            catch (JsonException)
            {
                settings = [];
            }

            settings["WebPort"] = req.WebPort;
            settings["AdminPort"] = req.AdminPort;
            settings["ConnectServerPort"] = req.ConnectServerPort;
            settings["DatabasePort"] = req.DatabasePort;
            settings["PublicAddress"] = publicAddr;
            settings["WebUrls"] = $"http://0.0.0.0:{req.WebPort}";
            settings["OpenBrowserUrl"] = $"http://{publicAddr}:{req.WebPort}/";
            settings["LauncherEnabled"] = true;
            var settingsJson = settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsPath, settingsJson, ctx.RequestAborted).ConfigureAwait(false);
            options.SettingsFilePath = settingsPath;
            options.WebPort = req.WebPort;
            options.AdminUrl = $"http://127.0.0.1:{req.AdminPort}";
            options.ConnectServerPort = req.ConnectServerPort;
            options.DatabasePort = req.DatabasePort;
            options.OpenBrowserUrl = $"http://{publicAddr}:{req.WebPort}/";
            if (portalOptions is not null)
            {
                portalOptions.ConnectServerPort = req.ConnectServerPort;
                portalOptions.ApiBaseUrl = options.AdminUrl;
            }

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

    private static void SyncLiveOptionsFromSavedSettings(string saveDir, LauncherOptions options, PortalOptions? portalOptions)
    {
        var settingsPath = Path.Combine(saveDir, "hnmo-settings.json");
        if (!File.Exists(settingsPath))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = doc.RootElement;
            options.SettingsFilePath = settingsPath;
            if (root.TryGetProperty("WebPort", out var wp) && wp.TryGetInt32(out var webPort) && webPort > 0)
            {
                options.WebPort = webPort;
            }

            if (root.TryGetProperty("AdminPort", out var ap) && ap.TryGetInt32(out var adminPort) && adminPort > 0)
            {
                options.AdminUrl = $"http://127.0.0.1:{adminPort}";
                if (portalOptions is not null)
                {
                    portalOptions.ApiBaseUrl = options.AdminUrl;
                }
            }

            if (root.TryGetProperty("ConnectServerPort", out var cp) && cp.TryGetInt32(out var connectPort) && connectPort > 0)
            {
                options.ConnectServerPort = connectPort;
                if (portalOptions is not null)
                {
                    portalOptions.ConnectServerPort = connectPort;
                }
            }

            if (root.TryGetProperty("DatabasePort", out var dp) && dp.TryGetInt32(out var dbPort) && dbPort > 0)
            {
                options.DatabasePort = dbPort;
            }

            if (root.TryGetProperty("OpenBrowserUrl", out var ob) && ob.GetString() is { Length: > 0 } openUrl)
            {
                options.OpenBrowserUrl = openUrl;
            }
        }
        catch
        {
            // Keep existing in-memory options if a restored file is malformed.
        }
    }

    private static async Task<bool> ProbeHttpAsync(HttpClient http, string url, CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
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
