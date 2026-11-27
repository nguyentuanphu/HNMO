// <copyright file="BugReports.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The players' bug reports (<see cref="BugReportsPlugIn"/>): the server's owner sets their status, answers them,
/// and sends them to the HNMO developer's Discord.
/// </summary>
public partial class BugReports : ComponentBase
{
    private readonly Dictionary<string, string> _replies = [];
    private readonly Dictionary<string, string> _duplicateOf = [];
    private List<BugReport> _reports = [];
    private BugReportsConfiguration _configuration = new();
    private bool _isLoading = true;
    private bool _active;
    private bool _canForward;
    private string _filter = "open";
    private string _tab = "players";
    private List<AutoErrorGroup> _autoGroups = [];
    private string _autoFilter = "open";
    private bool _autoOn = true;
    private int _autoCapAccount = 20;
    private int _autoCapServer = 1000;
    private bool _saving;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IServerProvider ServerProvider { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<BugReports> Logger { get; set; } = null!;

    private string RewardText => this._configuration.RewardConfirmedBugs && this._configuration.RewardAmount > 0
        ? $"{this._configuration.RewardAmount} {this._configuration.RewardCoinType}"
        : L("none", "không");

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string L4(string en, string vi, string de, string zh) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "vi" => vi,
            "de" => de,
            "zh" => zh,
            _ => en,
        };

    private static string AutoStatusLabel(AutoErrorGroup group) => group.Status switch
    {
        AutoErrorStatus.Fixed => L4("Fixed", "Đã sửa", "Behoben", "已修复"),
        AutoErrorStatus.Ignored => L4("Ignored", "Bỏ qua", "Ignoriert", "已忽略"),
        _ when group.Reopened => L4("Back after a fix", "Gặp lại sau khi sửa", "Nach Behebung wieder da", "修复后再次出现"),
        _ => L4("Open", "Đang mở", "Offen", "未处理"),
    };

    private static string AutoStatusClass(AutoErrorGroup group) => group.Status switch
    {
        AutoErrorStatus.Fixed => "bg-success",
        AutoErrorStatus.Ignored => "bg-secondary",
        _ when group.Reopened => "bg-danger",
        _ => "bg-warning text-dark",
    };

    private static string StatusLabel(BugReportStatus status) => status switch
    {
        BugReportStatus.New => L("New", "Mới"),
        BugReportStatus.Confirmed => L("Confirmed", "Đã xác nhận"),
        BugReportStatus.NotABug => L("Not a bug", "Không phải lỗi"),
        BugReportStatus.Fixed => L("Fixed", "Đã sửa"),
        _ => L("Duplicate", "Trùng"),
    };

    private static string StatusClass(BugReportStatus status) => status switch
    {
        BugReportStatus.New => "bg-warning text-dark",
        BugReportStatus.Confirmed => "bg-danger",
        BugReportStatus.Fixed => "bg-success",
        _ => "bg-secondary",
    };

    private static string CategoryLabel(string category) => category switch
    {
        "display" => L("Display", "Hiển thị"),
        "skill" => L("Skill", "Kỹ năng"),
        "item" => L("Item", "Vật phẩm"),
        "quest" => L("Quest / event", "Nhiệm vụ / sự kiện"),
        "connection" => L("Connection", "Kết nối"),
        _ => L("Other", "Khác"),
    };

    private IEnumerable<BugReport> Shown() => this._filter switch
    {
        "all" => this._reports,
        "open" => this._reports.Where(r => r.Status is BugReportStatus.New or BugReportStatus.Confirmed),
        _ => Enum.TryParse<BugReportStatus>(this._filter, out var status) ? this._reports.Where(r => r.Status == status) : this._reports,
    };

    private IEnumerable<AutoErrorGroup> AutoShown() => this._autoFilter switch
    {
        "all" => this._autoGroups,
        "open" => this._autoGroups.Where(g => g.Status == AutoErrorStatus.Open),
        _ => Enum.TryParse<AutoErrorStatus>(this._autoFilter, out var status) ? this._autoGroups.Where(g => g.Status == status) : this._autoGroups,
    };

    private IEnumerable<IGameContext> Contexts() =>
        this.ServerProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context);

    private BugReportsPlugIn PlugIn() =>
        this.Contexts().Select(BugReportsPlugIn.Get).FirstOrDefault(p => p is not null)
        ?? new BugReportsPlugIn { Configuration = this._configuration };

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, active, saved) = await PlugInSettings.ReadStoredAsync<BugReportsConfiguration>(this.PersistenceContextProvider, typeof(BugReportsPlugIn), default).ConfigureAwait(true);
            this._configuration = saved ?? new BugReportsConfiguration();
            this._active = active;
            this._canForward = DeveloperHub.Url.Length > 0 || BugReportForwarder.IsDiscordWebhook(this._configuration.DeveloperWebhookUrl);
            this._autoOn = this._configuration.AutomaticReports;
            this._autoCapAccount = this._configuration.AutomaticDailyLimitPerAccount;
            this._autoCapServer = this._configuration.AutomaticDailyLimitPerServer;
            this._reports = this.PlugIn().All();
            this._autoGroups = this.PlugIn().AutoGroups();
            foreach (var r in this._reports)
            {
                this._replies.TryAdd(r.Id, string.Empty);
                this._duplicateOf.TryAdd(r.Id, string.Empty);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "The bug reports couldn't be read.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SetStatusAsync(string id, BugReportStatus status)
    {
        var contexts = this.Contexts().ToList();
        if (contexts.Count == 0)
        {
            this.ToastService.ShowError(L("No game server is running.", "Không có máy chủ game nào đang chạy."));
            return;
        }

        var duplicateOf = this._duplicateOf.TryGetValue(id, out var d) ? d : null;
        if (await this.PlugIn().SetStatusAsync(contexts, id, status, duplicateOf).ConfigureAwait(true))
        {
            this.ToastService.ShowSuccess(L($"{id}: {StatusLabel(status)}.", $"{id}: {StatusLabel(status)}."));
        }
        else
        {
            this.ToastService.ShowError(status == BugReportStatus.Duplicate
                ? L("Write the id of the report it repeats (e.g. B3).", "Ghi mã báo lỗi bị trùng (vd. B3).")
                : L("The report wasn't found.", "Không tìm thấy báo lỗi."));
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task ReplyAsync(string id)
    {
        var text = this._replies.TryGetValue(id, out var t) ? t : string.Empty;
        var contexts = this.Contexts().ToList();
        if (string.IsNullOrWhiteSpace(text) || contexts.Count == 0)
        {
            return;
        }

        if (await this.PlugIn().ReplyAsync(contexts, id, text).ConfigureAwait(true))
        {
            this._replies[id] = string.Empty;
            this.ToastService.ShowSuccess(L("Answered: the player sees it in the game.", "Đã trả lời: người chơi thấy trong game."));
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task SetAutoStatusAsync(string id, AutoErrorStatus status)
    {
        if (this.PlugIn().SetAutoStatus(id, status))
        {
            this.ToastService.ShowSuccess($"{id}: {AutoStatusLabel(new AutoErrorGroup { Status = status })}.");
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task ForwardAutoAsync(string id)
    {
        var (ok, error) = this.PlugIn().ForwardAuto(id);
        if (ok)
        {
            this.ToastService.ShowSuccess(L4(
                "It goes to the HNMO developer with this server's weekly report.",
                "Lỗi sẽ gửi tới nhà phát triển HNMO cùng báo cáo hằng tuần của server.",
                "Er geht mit dem Wochenbericht dieses Servers an den HNMO-Entwickler.",
                "它将随本服务器的周报发给 HNMO 开发者。"));
        }
        else
        {
            this.ToastService.ShowError(error ?? string.Empty);
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task SaveAutoSettingsAsync()
    {
        this._saving = true;
        try
        {
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<BugReportsConfiguration>(this.PersistenceContextProvider, typeof(BugReportsPlugIn), default).ConfigureAwait(true);
            var configuration = saved ?? new BugReportsConfiguration();
            configuration.AutomaticReports = this._autoOn;
            configuration.AutomaticDailyLimitPerAccount = Math.Max(0, this._autoCapAccount);
            configuration.AutomaticDailyLimitPerServer = Math.Max(0, this._autoCapServer);

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(BugReportsPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, configuration, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L4("Saved.", "Đã lưu.", "Gespeichert.", "已保存。"));
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "The automatic error report settings couldn't be saved.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._saving = false;
        }

        await this.LoadAsync().ConfigureAwait(true);
    }

    private async Task ForwardAsync(string id)
    {
        var (ok, error) = await this.PlugIn().ForwardAsync(id, LicensedPlayerLimit.Licensee).ConfigureAwait(true);
        if (ok)
        {
            this.ToastService.ShowSuccess(L("It goes to the HNMO developer with this server's weekly report.", "Báo lỗi sẽ gửi tới nhà phát triển HNMO cùng báo cáo hằng tuần của server."));
        }
        else
        {
            this.ToastService.ShowError(L($"Not queued: {error}", $"Chưa xếp vào hàng gửi: {error}"));
        }

        await this.LoadAsync().ConfigureAwait(true);
    }
}
