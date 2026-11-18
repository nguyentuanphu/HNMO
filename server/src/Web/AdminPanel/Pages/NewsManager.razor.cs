// <copyright file="NewsManager.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.News;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;

/// <summary>
/// Admin manager page (<c>/news</c>) for creating bilingual EN/VI announcements, scheduling in Sydney time,
/// pinning posts, and pushing in-game chat notices.
/// </summary>
public partial class NewsManager : ComponentBase
{
    private NewsConfiguration _configuration = new();
    private List<NewsPost> _posts = [];
    private bool _isActive = true;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;
    private string? _formError;

    private string _editingId = string.Empty;
    private string _titleEn = string.Empty;
    private string _titleVi = string.Empty;
    private string _bodyEn = string.Empty;
    private string _bodyVi = string.Empty;
    private string _imageUrl = string.Empty;
    private string _publishSydneyLocal = FormatSydneyLocalInput(DateTime.UtcNow);
    private bool _isPinned;
    private bool _notifyInChat = true;

    [Inject]
    private NewsAdminOperations NewsOperations { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<NewsManager> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static string FormatSydneyLocalInput(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), NewsStore.ResolveSydneyZone())
            .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (isActive, cfg, posts) = await this.NewsOperations.LoadAsync(default).ConfigureAwait(true);
            this._isActive = isActive;
            this._configuration = cfg;
            this._posts = [.. posts];
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to load news posts.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private void OnPublishTimeChanged(ChangeEventArgs args)
    {
        var raw = args.Value?.ToString();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            this._publishSydneyLocal = raw.Trim();
        }
    }

    private void StartEdit(NewsPost post)
    {
        this._formError = null;
        this._editingId = post.Id;
        this._titleEn = post.TitleEn;
        this._titleVi = post.TitleVi;
        this._bodyEn = post.BodyEn;
        this._bodyVi = post.BodyVi;
        this._imageUrl = post.ImageUrl;
        this._publishSydneyLocal = FormatSydneyLocalInput(post.PublishAtUtc);
        this._isPinned = post.IsPinned;
        this._notifyInChat = post.NotifyInChat;
    }

    private void ResetForm()
    {
        this._formError = null;
        this._editingId = string.Empty;
        this._titleEn = string.Empty;
        this._titleVi = string.Empty;
        this._bodyEn = string.Empty;
        this._bodyVi = string.Empty;
        this._imageUrl = string.Empty;
        this._publishSydneyLocal = FormatSydneyLocalInput(DateTime.UtcNow);
        this._isPinned = false;
        this._notifyInChat = true;
    }

    private async Task SaveConfigAsync()
    {
        this._isSaving = true;
        try
        {
            this._configuration.MaxVisiblePosts = Math.Clamp(this._configuration.MaxVisiblePosts, 1, 100);
            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(this._configuration, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(NewsPlugIn).GUID;

            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, this._configuration, json, true, token, default).ConfigureAwait(true);
            }

            var activePreview = await this.PluginOperations.SetActiveAsync(id, this._isActive, false, null, default).ConfigureAwait(true);
            if (activePreview["confirmationToken"]?.GetValue<string>() is { } activeToken)
            {
                await this.PluginOperations.SetActiveAsync(id, this._isActive, true, activeToken, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved news settings.", "Đã lưu cài đặt tin tức."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving news settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task SavePostAsync()
    {
        this._formError = null;
        if (string.IsNullOrWhiteSpace(this._titleEn) && string.IsNullOrWhiteSpace(this._titleVi))
        {
            this._formError = L("Please enter an English or Vietnamese title.", "Vui lòng nhập tiêu đề tiếng Anh hoặc tiếng Việt.");
            return;
        }

        this._isSaving = true;
        try
        {
            var post = new NewsPost
            {
                Id = this._editingId,
                TitleEn = this._titleEn,
                TitleVi = this._titleVi,
                BodyEn = this._bodyEn,
                BodyVi = this._bodyVi,
                ImageUrl = this._imageUrl,
                IsPinned = this._isPinned,
                NotifyInChat = this._notifyInChat,
            };

            var (saved, broadcastCount) = await this.NewsOperations
                .SavePostAsync(post, this._publishSydneyLocal, default)
                .ConfigureAwait(true);
            var msg = broadcastCount > 0
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    L("Published '{0}' and notified {1} server(s).", "Đã đăng '{0}' và thông báo cho {1} máy chủ."),
                    saved.TitleEn,
                    broadcastCount)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    L("Saved post '{0}'.", "Đã lưu bài viết '{0}'."),
                    saved.TitleEn);
            this.ToastService.ShowSuccess(msg);
            this.ResetForm();
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving news post.");
            this._formError = ex.Message;
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task BroadcastAsync(string id)
    {
        this._isSaving = true;
        try
        {
            var (found, serversCount) = await this.NewsOperations.BroadcastPostAsync(id, default).ConfigureAwait(true);
            if (!found)
            {
                this.ToastService.ShowError(L("Post not found.", "Không tìm thấy bài viết."));
                return;
            }

            this.ToastService.ShowSuccess(string.Format(
                CultureInfo.InvariantCulture,
                L("Sent chat notice to {0} server(s).", "Đã gửi thông báo chat tới {0} máy chủ."),
                serversCount));
        }
        catch (Exception ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task DeleteAsync(string id)
    {
        this._isSaving = true;
        try
        {
            await this.NewsOperations.DeletePostAsync(id, default).ConfigureAwait(true);
            if (string.Equals(this._editingId, id, StringComparison.Ordinal))
            {
                this.ResetForm();
            }

            this.ToastService.ShowSuccess(L("Deleted post.", "Đã xóa bài viết."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }
}
