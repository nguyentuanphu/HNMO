// HNMO updates (owner's choices, 2026-11-21): the host presses "Update" in the admin panel; only the files that changed
// are downloaded (manifest.json lists every file of a version with its SHA-256; the files are stored by hash on the
// update storage); the save game is never touched, the database is backed up first, and when the new version doesn't
// start, the old files and the database come back. The HNMO developer's hub says which version is offered
// (/api/latest) and where its files are.
//
// The steps: this program (the launcher) downloads and checks the changed files into save/updates/<version>, backs up
// the save game, copies itself to save/updates/runner and starts that copy with --apply-update, then stops HNMO.
// The copy waits for HNMO to end, swaps the files (the old ones go to save/updates/<version>/backup), starts HNMO
// again the way the host does (the tray icon on Windows, start-hnmo.sh elsewhere) and waits for the game and the admin
// panel. If they don't come, it puts the old files back, starts the old version and restores the backup.
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.Interfaces;

/// <summary>One file of a version (manifest.json).</summary>
internal sealed record UpdateFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("exec")] bool Exec = false);

/// <summary>A version's manifest: every file of the HNMO folder but the save game.</summary>
internal sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("build")] string? Build,
    [property: JsonPropertyName("files")] List<UpdateFile> Files);

/// <summary>What the copy started with --apply-update does: put these files in place, take those away.</summary>
internal sealed record UpdatePlan(
    string Root,
    string FromVersion,
    string ToVersion,
    string StagingFolder,
    string BackupFolder,
    List<UpdateFile> Copies,
    List<string> Removes,
    string? BackupZip,
    int WebPort,
    int AdminPort,
    string? NewManifest = null);

/// <summary>The state the admin panel shows.</summary>
internal sealed record UpdateStatus(
    string Current,
    string? Latest,
    string? Notes,
    bool Available,
    string State,
    int Percent,
    string? Message,
    long DownloadBytes,
    int ChangedFiles);

/// <summary>Checks for, downloads and starts the installation of HNMO updates (the launcher's side).</summary>
internal sealed class HnmoUpdater
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _root;
    private readonly string _saveDir;
    private readonly int _webPort;
    private readonly int _adminPort;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly Func<string>? _machineIdProvider;
    private readonly Func<string>? _legacyMachineIdProvider;
    private string? _latest;
    private string? _notes;
    private string? _filesUrl;
    private string? _updateToken;
    private List<UpdateFile>? _authorizedFiles;
    private string _state = "idle";
    private int _percent;
    private string? _message;
    private long _downloadBytes;
    private int _changedFiles;

    /// <summary>Initializes a new instance of the <see cref="HnmoUpdater"/> class.</summary>
    /// <param name="root">The HNMO folder (the one with version.json and manifest.json).</param>
    /// <param name="saveDir">The save folder.</param>
    /// <param name="webPort">The game's web port.</param>
    /// <param name="adminPort">The admin panel's port.</param>
    /// <param name="clients">The HTTP clients.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="machineIdProvider">Optional override for tests.</param>
    /// <param name="legacyMachineIdProvider">Optional override for tests.</param>
    public HnmoUpdater(
        string root,
        string saveDir,
        int webPort,
        int adminPort,
        IHttpClientFactory clients,
        ILogger logger,
        Func<string>? machineIdProvider = null,
        Func<string>? legacyMachineIdProvider = null)
    {
        this._root = root;
        this._saveDir = saveDir;
        this._webPort = webPort;
        this._adminPort = adminPort;
        this._clients = clients;
        this._logger = logger;
        this._machineIdProvider = machineIdProvider;
        this._legacyMachineIdProvider = legacyMachineIdProvider;
        this.ReadLastResult();
    }

    /// <summary>Gets the version of this HNMO folder (version.json), or the program's own.</summary>
    public string CurrentVersion => ReadVersion(this._root) ?? typeof(HnmoUpdater).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Gets a value indicating whether this is an HNMO folder that can update itself (it has a manifest).</summary>
    public bool CanUpdate => File.Exists(Path.Combine(this._root, "manifest.json"));

    /// <summary>The version of an HNMO folder (version.json), if it has one.</summary>
    /// <param name="root">The folder.</param>
    /// <returns>The version.</returns>
    public static string? ReadVersion(string root)
    {
        try
        {
            var file = Path.Combine(root, "version.json");
            if (!File.Exists(file))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            return document.RootElement.TryGetProperty("version", out var version) ? version.GetString() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether a version is newer than another (1.0.10 &gt; 1.0.9).</summary>
    /// <param name="candidate">The offered version.</param>
    /// <param name="current">The running one.</param>
    /// <returns><c>true</c>, if newer.</returns>
    public static bool IsNewer(string? candidate, string current) =>
        Version.TryParse(candidate, out var a) && Version.TryParse(current, out var b) && a > b;

    /// <summary>Works out what changes from one manifest to the next.</summary>
    /// <param name="installed">The installed version's manifest.</param>
    /// <param name="next">The new version's manifest.</param>
    /// <returns>The files to put in place and the paths to take away.</returns>
    public static (List<UpdateFile> Copies, List<string> Removes) Diff(UpdateManifest installed, UpdateManifest next)
    {
        var have = installed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var copies = next.Files.Where(f => !have.TryGetValue(f.Path, out var old) || !string.Equals(old.Sha256, f.Sha256, StringComparison.OrdinalIgnoreCase)).ToList();
        var wanted = next.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var removes = installed.Files.Select(f => f.Path).Where(p => !wanted.Contains(p)).ToList();
        return (copies, removes);
    }

    /// <summary>A path of a manifest is inside the HNMO folder and not in its save folder.</summary>
    /// <param name="root">The HNMO folder.</param>
    /// <param name="relative">The manifest's path.</param>
    /// <returns>The full path, or <c>null</c> for one that would leave the folder or touch the save game.</returns>
    public static string? SafePath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\0', StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var save = rootFull + "save" + Path.DirectorySeparatorChar;
        return full.StartsWith(rootFull, StringComparison.Ordinal) && !full.StartsWith(save, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>The state for the admin panel; asks the hub for the offered version when asked to.</summary>
    /// <param name="check">Whether to ask the hub now.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    /// <returns>The state.</returns>
    public async Task<UpdateStatus> StatusAsync(bool check, CancellationToken cancellationToken)
    {
        if (check && this._state is "idle" or "failed" or "done" or "rolled-back")
        {
            await this.CheckAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (this._gate)
        {
            return new UpdateStatus(this.CurrentVersion, this._latest, this._notes, IsNewer(this._latest, this.CurrentVersion), this._state, this._percent, this._message, this._downloadBytes, this._changedFiles);
        }
    }

    /// <summary>Starts the update in the background (download, backup, then the copy installs it).</summary>
    /// <param name="stopHnmo">Stops HNMO (the game, the database, this program) once the copy waits for it.</param>
    /// <returns>Whether it started, and why not.</returns>
    public (bool Ok, string Message) Start(Func<Task> stopHnmo)
    {
        lock (this._gate)
        {
            if (!this.CanUpdate)
            {
                return (false, "This HNMO folder can't update itself (no manifest.json): download the whole folder.");
            }

            if (this._state is "downloading" or "installing")
            {
                return (false, "An update is already running.");
            }

            if (!IsNewer(this._latest, this.CurrentVersion)
                || this._authorizedFiles is not { Count: > 0 }
                || this._filesUrl is null
                || string.IsNullOrEmpty(this._updateToken))
            {
                return (false, "No newer version is offered.");
            }

            this._state = "downloading";
            this._percent = 0;
            this._message = null;
        }

        _ = Task.Run(() => this.RunAsync(stopHnmo));
        return (true, "The update started.");
    }

    private void ClearAuthorizedOffer()
    {
        lock (this._gate)
        {
            this._latest = null;
            this._notes = null;
            this._filesUrl = null;
            this._updateToken = null;
            this._authorizedFiles = null;
        }
    }

    private string? ReadLicenceText()
    {
        var serverDir = Path.Combine(this._root, "server");
        var settingsPath = HostSettingsStore.ResolveDefaultPath(Directory.Exists(serverDir) ? serverDir : this._root);
        var hostSettings = HostSettingsStore.TryLoad(settingsPath);
        var configured = hostSettings?.LicenseFilePath;
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Path.IsPathRooted(configured))
            {
                candidates.Add(configured);
            }
            else
            {
                candidates.Add(Path.Combine(this._root, configured));
                candidates.Add(Path.Combine(serverDir, configured));
            }
        }

        candidates.Add(Path.Combine(this._root, "license.hnmo"));
        candidates.Add(Path.Combine(serverDir, "license.hnmo"));
        var found = candidates.FirstOrDefault(File.Exists);
        return found is null ? null : File.ReadAllText(found);
    }

    private string ReadBuildStamp()
    {
        try
        {
            var manifest = Path.Combine(this._root, "manifest.json");
            if (File.Exists(manifest))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("build", out var b) && b.ValueKind == JsonValueKind.String && b.GetString() is { Length: > 0 } stamp)
                {
                    return stamp;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // fall back to .hnmo-build
        }

        var marker = Path.Combine(this._root, "web", "wwwroot", ".hnmo-build");
        return File.Exists(marker) ? File.ReadAllText(marker).Trim() : string.Empty;
    }

    private static string ReadLicenceId(string licenceText)
    {
        try
        {
            using var outer = JsonDocument.Parse(licenceText);
            var payload = Convert.FromBase64String(outer.RootElement.GetProperty("payload").GetString() ?? string.Empty);
            using var body = JsonDocument.Parse(payload);
            return body.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() ?? string.Empty : string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var url = DeveloperHubAddress.UpdateCheck;
        if (url.Length == 0)
        {
            this.ClearAuthorizedOffer();
            return;
        }

        var licenceText = this.ReadLicenceText();
        if (string.IsNullOrWhiteSpace(licenceText))
        {
            this.ClearAuthorizedOffer();
            this._logger.LogInformation("HNMO update check: no licence file present; staying on current files.");
            return;
        }

        var machineId = (this._machineIdProvider ?? (() => HostMachineIdentity.MachineId))();
        var legacyMachineId = (this._legacyMachineIdProvider ?? (() => HostMachineIdentity.LegacyMachineId))();
        var requestBody = new
        {
            licenceId = ReadLicenceId(licenceText),
            machineId,
            legacyMachineId,
            buildStamp = this.ReadBuildStamp(),
            licence = Convert.ToBase64String(Encoding.UTF8.GetBytes(licenceText)),
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await this._clients.CreateClient().PostAsJsonAsync(url, requestBody, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                this.ClearAuthorizedOffer();
                return;
            }

            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            var root = document.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var manifestVersion = Text("manifestVersion");
            var downloadBase = Text("downloadBase");
            var token = Text("token");
            if (string.IsNullOrWhiteSpace(manifestVersion)
                || string.IsNullOrWhiteSpace(downloadBase)
                || string.IsNullOrWhiteSpace(token)
                || !root.TryGetProperty("files", out var filesEl)
                || filesEl.ValueKind != JsonValueKind.Array)
            {
                this.ClearAuthorizedOffer();
                return;
            }

            var files = new List<UpdateFile>();
            foreach (var item in filesEl.EnumerateArray())
            {
                var path = item.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                var sha = item.TryGetProperty("sha256", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sha) || SafePath(this._root, path) is null)
                {
                    this.ClearAuthorizedOffer();
                    return;
                }

                var size = item.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number ? sz.GetInt64() : 0;
                var exec = item.TryGetProperty("exec", out var ex) && ex.ValueKind == JsonValueKind.True;
                files.Add(new UpdateFile(path, size, sha, exec));
            }

            if (files.Count == 0)
            {
                this.ClearAuthorizedOffer();
                return;
            }

            var normalizedBase = downloadBase.TrimEnd('/');
            lock (this._gate)
            {
                this._latest = manifestVersion;
                this._notes = Text("notes");
                this._filesUrl = normalizedBase;
                this._updateToken = token;
                this._authorizedFiles = files;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            this.ClearAuthorizedOffer();
            this._logger.LogInformation("HNMO update check: the hub can't be reached ({Message}); staying on current files.", ex.Message);
        }
    }

    internal async Task<UpdatePlan?> StageAuthorizedUpdateForTestAsync(CancellationToken cancellationToken = default)
    {
        List<UpdateFile>? files;
        string? latest;
        string? filesUrl;
        string? token;
        lock (this._gate)
        {
            files = this._authorizedFiles?.ToList();
            latest = this._latest;
            filesUrl = this._filesUrl;
            token = this._updateToken;
        }

        if (files is not { Count: > 0 } || string.IsNullOrWhiteSpace(latest) || string.IsNullOrWhiteSpace(filesUrl) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var http = this._clients.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(30);
        return await this.StagePlanAsync(http, new UpdateManifest(latest, this.ReadBuildStamp(), files), filesUrl, token, skipBackup: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UpdatePlan> StagePlanAsync(
        HttpClient http,
        UpdateManifest next,
        string filesUrl,
        string bearerToken,
        bool skipBackup,
        CancellationToken cancellationToken)
    {
        var installed = JsonSerializer.Deserialize<UpdateManifest>(await File.ReadAllTextAsync(Path.Combine(this._root, "manifest.json"), cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidDataException("This folder's manifest.json can't be read.");
        if (next.Files.Any(f => SafePath(this._root, f.Path) is null) || installed.Files.Any(f => SafePath(this._root, f.Path) is null))
        {
            throw new InvalidDataException("A manifest names a file outside the HNMO folder or in the save folder.");
        }

        var (copies, removes) = Diff(installed, next);
        var staging = Path.Combine(this._saveDir, "updates", next.Version, "files");
        Directory.CreateDirectory(staging);
        var total = Math.Max(1, copies.Sum(c => c.Size));
        long done = 0;
        lock (this._gate)
        {
            this._changedFiles = copies.Count;
            this._downloadBytes = copies.Sum(c => c.Size);
        }

        this._logger.LogInformation("HNMO update {From} → {To}: {Files} files to download ({Mb} MB), {Removed} to take away.", installed.Version, next.Version, copies.Count, total / (1 << 20), removes.Count);
        var machineId = (this._machineIdProvider ?? (() => HostMachineIdentity.MachineId))();
        foreach (var file in copies.DistinctBy(c => c.Sha256, StringComparer.OrdinalIgnoreCase))
        {
            var target = Path.Combine(staging, file.Sha256.ToLowerInvariant());
            if (!File.Exists(target) || !string.Equals(await HashAsync(target).ConfigureAwait(false), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                var fileUrl = filesUrl.EndsWith("/api/update-file", StringComparison.OrdinalIgnoreCase)
                    ? $"{filesUrl}?path={Uri.EscapeDataString(file.Path.Replace('\\', '/'))}"
                    : $"{filesUrl}/{file.Sha256.ToLowerInvariant()}";
                await DownloadAsync(http, fileUrl, target, file.Sha256, bearerToken, machineId, cancellationToken).ConfigureAwait(false);
            }

            done += file.Size;
            lock (this._gate)
            {
                this._percent = (int)(done * 90 / total);
            }
        }

        string? backupZip = null;
        if (!skipBackup)
        {
            // The save game first (the database too): the copy restores it when the new version doesn't start.
            lock (this._gate)
            {
                this._message = "Backing up the save game";
            }

            backupZip = await this.BackupAsync(http).ConfigureAwait(false);
        }

        var updateFolder = Path.Combine(this._saveDir, "updates", next.Version);
        var newManifest = Path.Combine(updateFolder, "manifest.json");
        Directory.CreateDirectory(updateFolder);
        await File.WriteAllTextAsync(newManifest, JsonSerializer.Serialize(next), cancellationToken).ConfigureAwait(false);
        var plan = new UpdatePlan(
            this._root,
            installed.Version,
            next.Version,
            staging,
            Path.Combine(updateFolder, "backup"),
            copies,
            removes,
            backupZip,
            this._webPort,
            this._adminPort,
            newManifest);
        var planFile = Path.Combine(updateFolder, "plan.json");
        await File.WriteAllTextAsync(planFile, JsonSerializer.Serialize(plan, Json), cancellationToken).ConfigureAwait(false);
        return plan;
    }

    private async Task RunAsync(Func<Task> stopHnmo)
    {
        try
        {
            List<UpdateFile>? files;
            string? latest;
            string? filesUrl;
            string? token;
            lock (this._gate)
            {
                files = this._authorizedFiles?.ToList();
                latest = this._latest;
                filesUrl = this._filesUrl;
                token = this._updateToken;
            }

            if (files is not { Count: > 0 } || string.IsNullOrWhiteSpace(latest) || string.IsNullOrWhiteSpace(filesUrl) || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("No authorized update from the hub.");
            }

            var http = this._clients.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(30);
            var next = new UpdateManifest(latest, this.ReadBuildStamp(), files);
            var plan = await this.StagePlanAsync(http, next, filesUrl, token, skipBackup: false, CancellationToken.None).ConfigureAwait(false);
            var planFile = Path.Combine(this._saveDir, "updates", next.Version, "plan.json");

            // The installer: a copy of this program, which isn't replaced while it runs.
            var runner = Path.Combine(this._saveDir, "updates", "runner");
            if (Directory.Exists(runner))
            {
                Directory.Delete(runner, true);
            }

            CopyFolder(AppContext.BaseDirectory, runner);
            lock (this._gate)
            {
                this._state = "installing";
                this._percent = 95;
                this._message = "Installing: HNMO restarts in a moment";
            }

            StartRunner(runner, planFile, Environment.ProcessId);
            this._logger.LogInformation("HNMO update {To}: installing; HNMO stops and starts again.", next.Version);
            await stopHnmo().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "HNMO update failed before installing; nothing was changed.");
            lock (this._gate)
            {
                this._state = "failed";
                this._message = ex.Message;
            }
        }
    }

    private async Task<string?> BackupAsync(HttpClient http)
    {
        try
        {
            using var response = await http.PostAsync($"http://127.0.0.1:{this._webPort}/api/control/backup", null).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"The save game couldn't be backed up: {text}");
            }

            using var document = JsonDocument.Parse(text);
            return document.RootElement.TryGetProperty("backup", out var backup) && backup.TryGetProperty("fileName", out var name) ? name.GetString() : null;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"The save game couldn't be backed up: {ex.Message}", ex);
        }
    }

    private static async Task DownloadAsync(HttpClient http, string url, string target, string sha256, string? bearerToken, string machineId, CancellationToken cancellationToken)
    {
        var part = target + ".part";
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(bearerToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                if (!string.IsNullOrWhiteSpace(machineId))
                {
                    request.Headers.TryAddWithoutValidation("X-HNMO-Machine", machineId);
                }

                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using var destination = File.Create(part);
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                if (!string.Equals(await HashAsync(part).ConfigureAwait(false), sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"A downloaded file doesn't match its hash ({sha256}).");
                }

                File.Move(part, target, true);
                return;
            }
            catch (Exception) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The SHA-256 of a file, as lower-case hex.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The hash.</returns>
    public static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (relative.StartsWith("wwwroot", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("vendor", StringComparison.OrdinalIgnoreCase))
            {
                continue; // the installer needs the program only, not the web game's pages
            }

            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void StartRunner(string runner, string planFile, int processId)
    {
        var host = Environment.ProcessPath ?? "dotnet";
        var isDotnetHost = Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        var start = isDotnetHost
            ? new ProcessStartInfo(host) { ArgumentList = { Path.Combine(runner, "HNMO.dll") } }
            : new ProcessStartInfo(Path.Combine(runner, Path.GetFileName(host)));
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add(planFile);
        start.ArgumentList.Add("--wait-pid");
        start.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.WorkingDirectory = runner;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        Process.Start(start);
    }

    private void ReadLastResult()
    {
        try
        {
            var file = Path.Combine(this._saveDir, "updates", "last.json");
            if (!File.Exists(file))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            this._state = root.TryGetProperty("state", out var state) ? state.GetString() ?? "idle" : "idle";
            this._message = root.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // no last result
        }
    }
}

/// <summary>The installer: run from a copy of the launcher with --apply-update while HNMO is stopped.</summary>
internal static class UpdateApplier
{
    // How long the new version may take to start (the first start of a big save game can take a few minutes);
    // HNMO_UPDATE_START_MINUTES changes it (tests).
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(
        int.TryParse(Environment.GetEnvironmentVariable("HNMO_UPDATE_START_MINUTES"), out var minutes) && minutes > 0 ? minutes : 10);

    /// <summary>Installs the planned update, starts HNMO and goes back to the old version if it doesn't start.</summary>
    /// <param name="planFile">The plan.</param>
    /// <param name="waitForProcess">The launcher's process, which ends first.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string planFile, int waitForProcess)
    {
        var plan = JsonSerializer.Deserialize<UpdatePlan>(await File.ReadAllTextAsync(planFile).ConfigureAwait(false))
            ?? throw new InvalidDataException("The update plan can't be read.");
        var saveDir = Path.Combine(plan.Root, "save");
        var log = Path.Combine(saveDir, "logs", "update.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        void Log(string text) => File.AppendAllText(log, $"{ControlOperations.FormatSydneyTime(DateTimeOffset.UtcNow)} {text}{Environment.NewLine}");

        Log($"Update {plan.FromVersion} → {plan.ToVersion}: waiting for HNMO to stop.");
        await WaitForExitAsync(waitForProcess).ConfigureAwait(false);
        await WaitForPortsFreeAsync(plan.WebPort, plan.AdminPort).ConfigureAwait(false);

        var moved = new List<(string Target, string? Backup)>();
        try
        {
            foreach (var file in plan.Copies)
            {
                var target = HnmoUpdater.SafePath(plan.Root, file.Path) ?? throw new InvalidDataException($"Unsafe path {file.Path}.");
                var backup = MoveToBackup(plan, file.Path, target);
                moved.Add((target, backup));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(plan.StagingFolder, file.Sha256.ToLowerInvariant()), target, true);
                if (file.Exec && !OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(target, File.GetUnixFileMode(target) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }

            foreach (var path in plan.Removes)
            {
                var target = HnmoUpdater.SafePath(plan.Root, path);
                if (target is not null && File.Exists(target))
                {
                    moved.Add((target, MoveToBackup(plan, path, target)));
                }
            }

            if (plan.NewManifest is { } newManifest && File.Exists(newManifest))
            {
                var target = Path.Combine(plan.Root, "manifest.json");
                moved.Add((target, MoveToBackup(plan, "manifest.json", target)));
                File.Copy(newManifest, target, true);
            }

            Log($"Installed {plan.Copies.Count} files, took away {plan.Removes.Count}. Starting HNMO {plan.ToVersion}.");
        }
        catch (Exception ex)
        {
            Log($"Installing failed ({ex.Message}): putting the old files back.");
            PutBack(moved);
            StartHnmo(plan.Root);
            WriteResult(saveDir, "failed", $"The update to {plan.ToVersion} couldn't be installed ({ex.Message}); HNMO {plan.FromVersion} runs as before.");
            return 1;
        }

        StartHnmo(plan.Root);
        if (await WaitForHnmoAsync(plan.WebPort, plan.AdminPort).ConfigureAwait(false))
        {
            Log($"HNMO {plan.ToVersion} runs.");
            WriteResult(saveDir, "done", $"Updated to HNMO {plan.ToVersion}.");
            return 0;
        }

        // The new version didn't start: the old files, the old version, then the save game from before.
        Log($"HNMO {plan.ToVersion} didn't start within {StartTimeout.TotalMinutes} minutes: going back to {plan.FromVersion}.");
        await StopHnmoAsync(plan.WebPort).ConfigureAwait(false);
        PutBack(moved);
        StartHnmo(plan.Root);
        var back = await WaitForHnmoAsync(plan.WebPort, plan.AdminPort).ConfigureAwait(false);
        if (back && plan.BackupZip is { Length: > 0 } zip)
        {
            var restored = await RestoreAsync(plan.WebPort, zip).ConfigureAwait(false);
            Log(restored ? $"The save game from before ({zip}) is back." : $"The save game from before ({zip}) couldn't be restored by itself: restore it on the control page.");
        }

        WriteResult(saveDir, "rolled-back", $"HNMO {plan.ToVersion} didn't start, so HNMO {plan.FromVersion} and the save game from before the update are back. See save/logs/update.log.");
        return 2;
    }

    private static string? MoveToBackup(UpdatePlan plan, string relative, string target)
    {
        if (!File.Exists(target))
        {
            return null;
        }

        var backup = Path.Combine(plan.BackupFolder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Move(target, backup, true);
        return backup;
    }

    private static void PutBack(List<(string Target, string? Backup)> moved)
    {
        foreach (var (target, backup) in Enumerable.Reverse(moved))
        {
            if (backup is not null && File.Exists(backup))
            {
                File.Move(backup, target, true);
            }
            else if (backup is null && File.Exists(target))
            {
                File.Delete(target); // a file the update added
            }
        }
    }

    private static void StartHnmo(string root)
    {
        ProcessStartInfo start;
        if (OperatingSystem.IsWindows())
        {
            // As a double-click on "Start HNMO.bat": the tray icon, no window.
            start = new ProcessStartInfo("wscript.exe") { ArgumentList = { Path.Combine(root, "launcher", "start-hidden.vbs") } };
        }
        else
        {
            start = new ProcessStartInfo(Path.Combine(root, "start-hnmo.sh"));
        }

        start.WorkingDirectory = root;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        Process.Start(start);
    }

    private static async Task WaitForExitAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OperationCanceledException)
        {
            // already gone, or it took too long: the port check follows
        }
    }

    private static async Task WaitForPortsFreeAsync(params int[] ports)
    {
        var until = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < until && ports.Any(IsListening))
        {
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }

    private static bool IsListening(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            return client.ConnectAsync(IPAddress.Loopback, port).Wait(300) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForHnmoAsync(int webPort, int adminPort)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var until = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < until)
        {
            await Task.Delay(3000).ConfigureAwait(false);
            try
            {
                var game = (await http.GetAsync($"http://127.0.0.1:{webPort}/health").ConfigureAwait(false)).IsSuccessStatusCode;
                var admin = (int)(await http.GetAsync($"http://127.0.0.1:{adminPort}/").ConfigureAwait(false)).StatusCode < 500;
                if (game && admin)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // not yet
            }
        }

        return false;
    }

    private static async Task StopHnmoAsync(int webPort)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            await http.PostAsync($"http://127.0.0.1:{webPort}/api/control/shutdown", null).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // not running
        }

        await WaitForPortsFreeAsync(webPort).ConfigureAwait(false);
    }

    private static async Task<bool> RestoreAsync(int webPort, string zip)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        try
        {
            using var restore = await http.PostAsJsonAsync($"http://127.0.0.1:{webPort}/api/control/restore", new { FileName = zip }).ConfigureAwait(false);
            if (!restore.IsSuccessStatusCode)
            {
                return false;
            }

            using var start = await http.PostAsync($"http://127.0.0.1:{webPort}/api/control/start", null).ConfigureAwait(false);
            return start.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static void WriteResult(string saveDir, string state, string message)
    {
        var folder = Path.Combine(saveDir, "updates");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "last.json"), JsonSerializer.Serialize(new { state, message, at = DateTimeOffset.UtcNow }));
    }
}
