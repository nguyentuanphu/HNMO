// <copyright file="EscrowContracts.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Escrow;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// Admin panel management page for HNMO Escrow Contracts (ROADMAP 16.1 + 16.3).
/// </summary>
public partial class EscrowContracts : ComponentBase
{
    private EscrowConfiguration? _configuration;
    private List<EscrowContract> _contracts = [];
    private List<EscrowReputationEntry> _reputations = [];
    private string _bannedItemsText = string.Empty;
    private string _adminReason = string.Empty;
    private bool _banLosingAccount;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showHelp;

    [Inject]
    private IPersistenceContextProvider PersistenceContextProvider { get; set; } = null!;

    [Inject]
    private IDataSource<GameConfiguration> GameConfigurationSource { get; set; } = null!;

    [Inject]
    private IServerProvider ServerProvider { get; set; } = null!;

    [Inject]
    private PluginWriteOperations PluginOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<EscrowContracts> Logger { get; set; } = null!;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => this.LoadAsync();

    private static string L(string en, string vi) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase) ? vi : en;

    private bool IsCurrencyAllowed(EscrowCurrency cur) =>
        this._configuration?.AllowedCurrencies?.Contains(cur) == true;

    private void ToggleCurrency(EscrowCurrency cur, bool enabled)
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        cfg.AllowedCurrencies ??= [];
        if (enabled && !cfg.AllowedCurrencies.Contains(cur))
        {
            cfg.AllowedCurrencies.Add(cur);
        }
        else if (!enabled)
        {
            cfg.AllowedCurrencies.Remove(cur);
        }
    }

    private EscrowEngine ResolveEngine()
    {
        var live = this.ServerProvider.Servers
            .OfType<IGameServerContextProvider>()
            .Select(s => EscrowContractsPlugIn.Get(s.Context))
            .FirstOrDefault(p => p is not null);
        if (live is not null)
        {
            return live.GetEngine();
        }

        var cfg = this._configuration ?? new EscrowConfiguration();
        return new EscrowEngine(cfg, EscrowStore.Open(cfg.FileName));
    }

    private async Task LoadAsync()
    {
        this._isLoading = true;
        try
        {
            var (_, _, saved) = await PlugInSettings.ReadStoredAsync<EscrowConfiguration>(
                this.PersistenceContextProvider,
                typeof(EscrowContractsPlugIn),
                default).ConfigureAwait(true);

            var cfg = saved ?? new EscrowConfiguration();
            this._configuration = JsonSerializer.Deserialize<EscrowConfiguration>(JsonSerializer.Serialize(cfg))
                ?? new EscrowConfiguration();
            this._bannedItemsText = string.Join(", ", this._configuration.BannedItems ?? []);

            var engine = this.ResolveEngine();
            engine.ExpireDueContracts(DateTime.UtcNow);
            this._contracts = engine.Store.Read(d => d.Contracts.OrderByDescending(x => x.CreatedAtUtc).Take(100).ToList());
            this._reputations = engine.Store.Read(d => d.Reputations.Values.OrderByDescending(r => r.Score).ThenBy(r => r.LoginName).Take(50).ToList());
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error loading Escrow Contracts admin page.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (this._configuration is not { } cfg)
        {
            return;
        }

        this._isSaving = true;
        try
        {
            if (cfg.DirectVndEnabled && !cfg.DirectVndComplianceAcknowledged)
            {
                cfg.DirectVndEnabled = false;
                this.ToastService.ShowError(L(
                    "Please tick 'I understand' under the Vietnamese law notice before enabling Direct P2P VND.",
                    "Vui lòng đánh dấu 'Tôi hiểu' tại cảnh báo pháp lý Việt Nam trước khi bật VND P2P trực tiếp."));
                return;
            }

            cfg.BannedItems = this._bannedItemsText
                .Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var serialized = new PlugInConfiguration();
            serialized.SetConfiguration(cfg, null);
            var json = serialized.CustomConfiguration ?? "{}";
            var id = typeof(EscrowContractsPlugIn).GUID;
            var preview = await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, false, null, default).ConfigureAwait(true);
            if (preview["confirmationToken"]?.GetValue<string>() is { } token)
            {
                await this.PluginOperations.ReplaceFromPanelAsync(id, cfg, json, true, token, default).ConfigureAwait(true);
            }

            this.ToastService.ShowSuccess(L("Saved escrow settings.", "Đã lưu cấu hình hợp đồng ký gửi."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error saving Escrow Contracts settings.");
            this.ToastService.ShowError(ex.Message);
        }
        finally
        {
            this._isSaving = false;
        }
    }

    private async Task FreezeAsync(string contractId, bool freeze)
    {
        var engine = this.ResolveEngine();
        var note = string.IsNullOrWhiteSpace(this._adminReason)
            ? (freeze ? "Frozen by admin" : "Unfrozen by admin")
            : this._adminReason.Trim();
        if (engine.SetFrozen(contractId, freeze, note))
        {
            ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
            {
                Id = $"esc-adm-frz-{Guid.NewGuid():N}",
                TimestampUtc = DateTime.UtcNow,
                Kind = ActivityKind.Trade,
                Account = "ADMIN",
                ItemSummary = $"{(freeze ? "Froze" : "Unfroze")} escrow {contractId}",
                Details = note,
            });
            this.ToastService.ShowSuccess(freeze
                ? L("Contract frozen.", "Đã đóng băng hợp đồng.")
                : L("Contract unfrozen.", "Đã mở khóa hợp đồng."));
            await this.LoadAsync().ConfigureAwait(true);
        }
    }

    private async Task AdminCancelAsync(string contractId)
    {
        var engine = this.ResolveEngine();
        var note = string.IsNullOrWhiteSpace(this._adminReason) ? "Admin force cancel" : this._adminReason.Trim();
        var (err, _) = engine.CancelContract("ADMIN", contractId, isAdmin: true, note, DateTime.UtcNow);
        if (err == "Ok")
        {
            ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
            {
                Id = $"esc-adm-can-{Guid.NewGuid():N}",
                TimestampUtc = DateTime.UtcNow,
                Kind = ActivityKind.Trade,
                Account = "ADMIN",
                ItemSummary = $"Admin cancelled escrow {contractId}",
                Details = note,
            });
            this.ToastService.ShowSuccess(L("Contract cancelled and goods returned to owed deliveries.", "Đã hủy hợp đồng và hoàn hàng về hộp quà."));
            await this.LoadAsync().ConfigureAwait(true);
        }
        else
        {
            this.ToastService.ShowError(L($"Cannot cancel ({err}).", $"Không thể hủy ({err})."));
        }
    }

    private async Task ResolveDisputeAsync(string contractId, bool awardToBuyer)
    {
        var cfg = this._configuration ?? new EscrowConfiguration();
        var engine = this.ResolveEngine();
        var direct = new EscrowDirectVndEngine(cfg, engine.Store);
        var note = string.IsNullOrWhiteSpace(this._adminReason)
            ? (awardToBuyer ? "Evidence accepted for buyer" : "Returned to seller")
            : this._adminReason.Trim();
        var (err, _, loser) = direct.ResolveDispute(contractId, awardToBuyer, note, DateTime.UtcNow);
        if (err != "Ok")
        {
            this.ToastService.ShowError(L($"Cannot resolve dispute ({err}).", $"Không thể xử lý tranh chấp ({err})."));
            return;
        }

        if (this._banLosingAccount && !string.IsNullOrWhiteSpace(loser))
        {
            var gameCfg = await this.GameConfigurationSource.GetOwnerAsync(Guid.Empty, default).ConfigureAwait(true);
            using var ctx = this.PersistenceContextProvider.CreateNewPlayerContext(gameCfg);
            if (await ctx.GetAccountByLoginNameAsync(loser, default).ConfigureAwait(true) is { } acc)
            {
                acc.State = AccountState.Banned;
                await ctx.SaveChangesAsync(default).ConfigureAwait(true);
            }
        }

        ActivityLogPlugIn.RecordExternal(new ActivityLogEntry
        {
            Id = $"esc-res-{Guid.NewGuid():N}",
            TimestampUtc = DateTime.UtcNow,
            Kind = ActivityKind.Trade,
            Account = "ADMIN",
            PartnerAccount = loser,
            Details = $"Resolved dispute {contractId} (awardToBuyer={awardToBuyer}, banLoser={this._banLosingAccount}): {note}",
        });

        this.ToastService.ShowSuccess(awardToBuyer
            ? L("Dispute resolved for buyer (seller bond forfeited).", "Đã xử lý giao hàng cho người mua (tịch thu cọc người bán).")
            : L("Dispute resolved for seller (goods & bond returned).", "Đã hoàn hàng & cọc cho người bán."));
        await this.LoadAsync().ConfigureAwait(true);
    }
}
