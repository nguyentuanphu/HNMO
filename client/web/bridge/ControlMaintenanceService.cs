using HNMO.Interfaces;

internal sealed class ControlMaintenanceService(
    GameLauncher? launcher,
    LauncherOptions options,
    string contentRoot,
    ILogger<ControlMaintenanceService> logger) : BackgroundService
{
    private DateOnly _lastBackupSydneyDay;
    private DateTimeOffset _lastVacuumUtc = DateTimeOffset.MinValue;

    public string? LastAutoBackupSydney { get; private set; }

    public string? LastVacuumSydney { get; private set; }

    public Task<object> RunMaintenanceCycleAsync(bool forceBackup, bool forceVacuum, CancellationToken cancellationToken)
    {
        var saveDir = Path.GetFullPath(Path.Combine(contentRoot, options.SavePath));
        Directory.CreateDirectory(saveDir);
        var cfg = MaintenanceSettings.Load(saveDir);

        launcher?.RotateLogsIfNeeded();
        var logDir = Path.Combine(saveDir, "logs");
        RollingLogWriter.RotateExternalLogIfNeeded(
            Path.Combine(logDir, "database.log"),
            Math.Max(1L, cfg.MaxLogFileMb) * 1024L * 1024L,
            cfg.MaxLogDays,
            Math.Max(10L, cfg.MaxLogTotalMb) * 1024L * 1024L);
        RollingLogWriter.EnforceRetention(logDir, cfg.MaxLogDays, Math.Max(10L, cfg.MaxLogTotalMb) * 1024L * 1024L);

        return this.RunAsyncWork(saveDir, cfg, forceBackup, forceVacuum, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken).ConfigureAwait(false);
                await this.RunMaintenanceCycleAsync(forceBackup: false, forceVacuum: false, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scheduled maintenance cycle failed.");
            }
        }
    }

    private async Task<object> RunAsyncWork(
        string saveDir,
        MaintenanceSettings cfg,
        bool forceBackup,
        bool forceVacuum,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sydneyStr = ControlOperations.FormatSydneyTime(now);
        var todaySydney = DateOnly.ParseExact(sydneyStr[..10], "yyyy-MM-dd");
        var currentHourSydney = int.Parse(sydneyStr[11..13]);

        BackupInfo? createdBackup = null;
        if (forceBackup || (cfg.AutoBackupEnabled && todaySydney > this._lastBackupSydneyDay && currentHourSydney >= cfg.AutoBackupHourSydney))
        {
            var dumpPath = Path.Combine(saveDir, "auto-backup-db.sql.tmp");
            try
            {
                if (launcher is not null)
                {
                    try
                    {
                        var pgDump = launcher.PostgresToolPath("pg_dump");
                        await ControlEndpoints.RunPgDumpAsync(pgDump, options.DatabasePort, options.DatabasePassword, dumpPath, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Skipping pg_dump during scheduled backup.");
                    }
                }

                createdBackup = ControlOperations.CreateBackupZip(saveDir, File.Exists(dumpPath) ? dumpPath : null, now);
                this._lastBackupSydneyDay = todaySydney;
                this.LastAutoBackupSydney = sydneyStr;
            }
            finally
            {
                if (File.Exists(dumpPath))
                {
                    File.Delete(dumpPath);
                }
            }
        }

        var retention = ControlOperations.ApplyBackupRetention(saveDir, cfg.BackupKeepDaily, cfg.BackupKeepWeekly, cfg.BackupKeepMonthly);

        var vacuumRan = false;
        if (launcher is not null && (forceVacuum || now - this._lastVacuumUtc >= TimeSpan.FromHours(cfg.VacuumIntervalHours)))
        {
            try
            {
                await launcher.RunVacuumAnalyzeAsync(cancellationToken).ConfigureAwait(false);
                this._lastVacuumUtc = now;
                this.LastVacuumSydney = ControlOperations.FormatSydneyTime(now);
                vacuumRan = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PostgreSQL VACUUM ANALYZE skipped.");
            }
        }

        return new
        {
            createdBackup,
            retention,
            vacuumRan,
            lastAutoBackupSydney = this.LastAutoBackupSydney,
            lastVacuumSydney = this.LastVacuumSydney,
        };
    }
}
