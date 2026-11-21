// <copyright file="GiftBox.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Operations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>Admin Panel page for composing, reviewing, and queueing bilingual Gift Box / Mail letters (ROADMAP 14.18).</summary>
public partial class GiftBox
{
    private readonly List<DraftItemRow> _items = new();
    private IReadOnlyList<ItemDefinition> _itemDefinitions = Array.Empty<ItemDefinition>();

    private Guid _sendId = Guid.NewGuid();
    private GiftBoxAudienceKind _audience = GiftBoxAudienceKind.Account;
    private string _logins = string.Empty;
    private bool _useActiveDays;
    private int _activeDays = 7;
    private string _titleEn = string.Empty;
    private string _titleVi = string.Empty;
    private string _messageEn = string.Empty;
    private string _messageVi = string.Empty;
    private int _coins;
    private CashShopCoinType _coinType = CashShopCoinType.WCoinC;
    private int _zen;
    private bool _hasExpiry;
    private DateTime _expiresSydneyLocal = DateTime.UtcNow.AddDays(7);
    private string _reason = string.Empty;

    private bool _busy;
    private string? _statusMessage;
    private bool _statusError;
    private ReviewSnapshot? _review;
    private string? _confirmationToken;

    [Inject]
    private GiftBoxAdminOperations Operations { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private ILogger<GiftBox> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var cfg = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, default).ConfigureAwait(true);
            this._itemDefinitions = cfg.Items.Where(i => i.IsActive).OrderBy(i => i.Group).ThenBy(i => i.Number).ToList();
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Could not load item definitions for Gift Box page.");
        }

        this._expiresSydneyLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddDays(7), ResolveSydneyZone());
    }

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private static TimeZoneInfo ResolveSydneyZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
        }
    }

    private void AddItemFromPicker(ItemDefinition definition)
    {
        if (this._review is not null)
        {
            return;
        }

        if (this._items.Sum(i => i.Item.Count) >= 50)
        {
            this._statusError = true;
            this._statusMessage = L("A single gift batch supports at most 50 item pieces.", "Mỗi lượt gửi chỉ hỗ trợ tối đa 50 vật phẩm.");
            return;
        }

        var label = definition.Name.GetTranslation(CultureInfo.CurrentUICulture) ?? definition.Name.ValueInNeutralLanguage;
        this._items.Add(new DraftItemRow(
            label,
            new GiftCodeItem
            {
                Group = definition.Group,
                Number = definition.Number,
                Level = 0,
                Count = 1,
                Durability = definition.Durability,
            }));
    }

    private GiftBoxSendDraft BuildDraft()
    {
        DateTime? expiresUtc = null;
        if (this._hasExpiry)
        {
            var unspecified = DateTime.SpecifyKind(this._expiresSydneyLocal, DateTimeKind.Unspecified);
            expiresUtc = TimeZoneInfo.ConvertTimeToUtc(unspecified, ResolveSydneyZone());
        }

        var reward = new GiftCodeReward
        {
            Coins = this._coins,
            CoinType = this._coinType,
            Zen = this._zen,
            Items = this._items.Select(i => new GiftCodeItem
            {
                Group = i.Item.Group,
                Number = i.Item.Number,
                Level = i.Item.Level,
                Count = i.Item.Count,
                Durability = i.Item.Durability,
                Skill = i.Item.Skill,
                Luck = i.Item.Luck,
                OptionLevel = i.Item.OptionLevel,
                ExcellentOptions = i.Item.ExcellentOptions,
            }).ToList(),
        };

        return new GiftBoxSendDraft(
            this._sendId,
            this._audience,
            this._audience == GiftBoxAudienceKind.Everyone ? string.Empty : this._logins,
            this._useActiveDays ? this._activeDays : null,
            this._titleEn.Trim(),
            this._titleVi.Trim(),
            this._messageEn.Trim(),
            this._messageVi.Trim(),
            reward,
            expiresUtc);
    }

    private async Task PreviewAsync()
    {
        if (this._busy)
        {
            return;
        }

        this._busy = true;
        this._statusMessage = null;
        this._statusError = false;
        try
        {
            var preview = await this.Operations.SendAsync(this.BuildDraft(), this._reason, false, null, default).ConfigureAwait(true);
            this._confirmationToken = preview["confirmationToken"]?.GetValue<string>();
            var change = preview["change"];
            var count = change?["count"]?.GetValue<int>() ?? 0;
            var digest = change?["digest"]?.GetValue<string>() ?? string.Empty;
            this._review = new ReviewSnapshot(count, digest);
        }
        catch (AdminOperationException ex)
        {
            this._statusError = true;
            this._statusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Gift box preview failed.");
            this._statusError = true;
            this._statusMessage = L("Preview failed; verify inputs and retry.", "Không thể kiểm tra danh sách nhận; vui lòng kiểm tra lại dữ liệu.");
        }
        finally
        {
            this._busy = false;
        }
    }

    private async Task ConfirmAsync()
    {
        if (this._busy || this._review is null)
        {
            return;
        }

        this._busy = true;
        this._statusMessage = null;
        this._statusError = false;
        try
        {
            var result = await this.Operations.SendAsync(this.BuildDraft(), this._reason, true, this._confirmationToken, default).ConfigureAwait(true);
            var count = result["count"]?.GetValue<int>() ?? 0;
            this._statusMessage = L(
                $"Queued gift letter to {count} recipient(s) (SendId: {this._sendId:D}).",
                $"Đã gửi thư quà cho {count} tài khoản (Mã gửi: {this._sendId:D}).");
            this._review = null;
            this._confirmationToken = null;
            this._sendId = Guid.NewGuid();
            this._items.Clear();
        }
        catch (AdminOperationException ex)
        {
            this._statusError = true;
            this._statusMessage = ex.Message;
            if (ex.Kind != AdminOperationErrorKind.Uncertain)
            {
                this._review = null;
                this._confirmationToken = null;
            }
            else
            {
                // Preserve exact SendId and re-request a confirmation token so retry reconciles safely.
                this._confirmationToken = null;
                try
                {
                    var preview = await this.Operations.SendAsync(this.BuildDraft(), this._reason, false, null, default).ConfigureAwait(true);
                    this._confirmationToken = preview["confirmationToken"]?.GetValue<string>();
                }
                catch
                {
                    // Keep Retry button available with preserved SendId.
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Gift box send confirmation failed.");
            this._statusError = true;
            this._statusMessage = L("Send failed; retry carefully with the same Send ID.", "Gửi thất bại; hãy thử lại với cùng mã gửi.");
        }
        finally
        {
            this._busy = false;
        }
    }

    private void CancelReview()
    {
        if (this._busy)
        {
            return;
        }

        this._review = null;
        this._confirmationToken = null;
    }

    private sealed record DraftItemRow(string Label, GiftCodeItem Item);

    private sealed record ReviewSnapshot(int Count, string Digest);
}
