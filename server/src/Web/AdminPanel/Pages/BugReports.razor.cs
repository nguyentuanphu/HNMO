// <copyright file="BugReports.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.BugReports;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.API;
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
            this._canForward = BugReportForwarder.IsDiscordWebhook(this._configuration.DeveloperWebhookUrl);
            this._reports = this.PlugIn().All();
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

    private async Task ForwardAsync(string id)
    {
        var (ok, error) = await this.PlugIn().ForwardAsync(id, LicensedPlayerLimit.Licensee).ConfigureAwait(true);
        if (ok)
        {
            this.ToastService.ShowSuccess(L("Sent to the HNMO developer.", "Đã gửi cho nhà phát triển HNMO."));
        }
        else
        {
            this.ToastService.ShowError(L($"Not sent: {error}", $"Chưa gửi được: {error}"));
        }

        await this.LoadAsync().ConfigureAwait(true);
    }
}
