// <copyright file="Leaderboards.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Pages;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// The page which shows the leaderboards of the mini games.
/// </summary>
public sealed partial class Leaderboards : ComponentBase, IDisposable
{
    private MiniGameType? _selectedType;
    private LeaderboardPeriod _period = LeaderboardPeriod.AllTime;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;

    /// <summary>
    /// The period of the finished games which are counted.
    /// </summary>
    internal enum LeaderboardPeriod
    {
        /// <summary>
        /// All games are counted.
        /// </summary>
        AllTime,

        /// <summary>
        /// The games of the last 30 days are counted.
        /// </summary>
        Last30Days,

        /// <summary>
        /// The games of the last 7 days are counted.
        /// </summary>
        Last7Days,
    }

    /// <summary>
    /// Gets the mini game types which have a leaderboard.
    /// </summary>
    internal IReadOnlyList<MiniGameType> MiniGameTypes { get; private set; } = [];

    /// <summary>
    /// Gets the entries of the shown leaderboard.
    /// </summary>
    internal IReadOnlyList<MiniGameLeaderboardEntry> Entries { get; private set; } = [];

    /// <summary>
    /// Gets a value indicating whether a leaderboard is loading.
    /// </summary>
    internal bool IsLoading { get; private set; } = true;

    [Inject]
    private MiniGameReadOperations ReadOperations { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;

    [Inject]
    private ILogger<Leaderboards> Logger { get; set; } = null!;

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Increment(ref this._loadGeneration);
        var previous = Interlocked.Exchange(ref this._loadCancellation, null);
        previous?.Cancel();
        previous?.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(true);
        await this.LoadAsync().ConfigureAwait(true);
    }

    private static int? GetDays(LeaderboardPeriod period)
    {
        return period switch
        {
            LeaderboardPeriod.Last7Days => 7,
            LeaderboardPeriod.Last30Days => 30,
            _ => null,
        };
    }

    private async Task SelectTypeAsync(MiniGameType miniGameType)
    {
        if (this._selectedType != miniGameType)
        {
            this._selectedType = miniGameType;
            await this.LoadAsync().ConfigureAwait(true);
        }
    }

    private async Task OnPeriodChangedAsync(ChangeEventArgs args)
    {
        if (Enum.TryParse<LeaderboardPeriod>(args.Value?.ToString(), out var period) && period != this._period)
        {
            this._period = period;
            await this.LoadAsync().ConfigureAwait(true);
        }
    }

    private async Task LoadAsync()
    {
        // Atomically supersede any in-flight load before awaiting, so rapid selection
        // changes cannot interleave on CancelAsync or let a slow earlier load overwrite
        // the current leaderboard.
        var generation = Interlocked.Increment(ref this._loadGeneration);
        var cancellation = new CancellationTokenSource();
        var previousCancellation = Interlocked.Exchange(ref this._loadCancellation, cancellation);
        if (previousCancellation is not null)
        {
            await previousCancellation.CancelAsync().ConfigureAwait(true);
            previousCancellation.Dispose();
        }

        var miniGameType = this._selectedType;
        var period = this._period;

        this.IsLoading = true;
        this.Entries = [];
        try
        {
            var leaderboard = await this.ReadOperations.LeaderboardAsync(miniGameType, GetDays(period), cancellation.Token).ConfigureAwait(true);
            if (cancellation.IsCancellationRequested || generation != Volatile.Read(ref this._loadGeneration))
            {
                return;
            }

            this.MiniGameTypes = leaderboard.AvailableTypes;
            this._selectedType = leaderboard.MiniGameType;
            this.Entries = leaderboard.Entries;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer selection is loading.
        }
        catch (Exception ex)
        {
            if (generation == Volatile.Read(ref this._loadGeneration))
            {
                this.Logger.LogError(ex, "Error loading the leaderboard of {MiniGameType}.", miniGameType);
                this.ToastService.ShowError(string.Format(Resources.UnexpectedErrorOccurred, ex.Message));
            }
        }
        finally
        {
            if (!cancellation.IsCancellationRequested && generation == Volatile.Read(ref this._loadGeneration))
            {
                this.IsLoading = false;
            }
        }
    }
}
