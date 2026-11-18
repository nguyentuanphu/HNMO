// <copyright file="TopUpMilestoneEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Outcome codes when claiming a top-up milestone step.
/// </summary>
public enum TopUpClaimResult
{
    /// <summary>The step reward was granted.</summary>
    Ok,

    /// <summary>The milestone feature is currently turned off.</summary>
    Disabled,

    /// <summary>The requested step id does not exist.</summary>
    StepNotFound,

    /// <summary>The account's cumulative VND is below the step threshold.</summary>
    NotReached,

    /// <summary>The step was already claimed by this account.</summary>
    AlreadyClaimed,
}

/// <summary>
/// View of a single milestone step for a specific account.
/// </summary>
/// <param name="Step">The step configuration.</param>
/// <param name="Reached">Whether the account has reached the threshold.</param>
/// <param name="Claimed">Whether the account has already claimed the step.</param>
/// <param name="Claimable">Whether the account can claim the step right now.</param>
public sealed record TopUpStepProgress(
    TopUpMilestoneStep Step,
    bool Reached,
    bool Claimed,
    bool Claimable);

/// <summary>
/// Outcome of recording a top-up VND credit for an account.
/// </summary>
/// <param name="Recorded">False when the reference was already counted or amount was non-positive.</param>
/// <param name="NewTotalVnd">The account's cumulative VND total.</param>
/// <param name="NewlyUnlockedSteps">Milestone steps newly crossed by this top-up.</param>
public sealed record TopUpCreditOutcome(
    bool Recorded,
    long NewTotalVnd,
    IReadOnlyList<TopUpMilestoneStep> NewlyUnlockedSteps);

/// <summary>
/// Outcome of claiming a milestone step.
/// </summary>
/// <param name="Result">The outcome code.</param>
/// <param name="Step">The targeted step, if found.</param>
/// <param name="CoinGrant">The coin grant to apply (with unique reference), if any.</param>
/// <param name="OwedReward">The bag goods (zen/items) to deliver or queue, if any.</param>
public sealed record TopUpClaimOutcome(
    TopUpClaimResult Result,
    TopUpMilestoneStep? Step,
    CoinGrant? CoinGrant,
    GiftCodeOwedReward? OwedReward);

/// <summary>
/// Pure, unit-tested rules engine for top-up milestones.
/// </summary>
public sealed class TopUpMilestoneEngine
{
    /// <summary>Name written in CashShopCoinGrant.GrantedBy.</summary>
    public const string GrantedBy = "HNMO top-up milestones";

    /// <summary>Maximum history entries retained per account.</summary>
    public const int MaxHistoryPerAccount = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopUpMilestoneEngine"/> class.
    /// </summary>
    /// <param name="configuration">The plug-in configuration.</param>
    /// <param name="store">The state store.</param>
    /// <param name="enabled">Whether the plug-in is currently enabled.</param>
    public TopUpMilestoneEngine(TopUpMilestonesConfiguration configuration, TopUpMilestoneStore store, bool enabled = true)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets the plug-in configuration.</summary>
    public TopUpMilestonesConfiguration Configuration { get; }

    /// <summary>Gets the backing store.</summary>
    public TopUpMilestoneStore Store { get; }

    /// <summary>Gets a value indicating whether the feature is active.</summary>
    public bool Enabled { get; }

    /// <summary>Gets the display timezone (Sydney by default).</summary>
    public TimeZoneInfo TimeZone => HnmoStateStore.ResolveTimeZone(this.Configuration.TimeZone);

    /// <summary>Gets the configured steps sorted ascending by <see cref="TopUpMilestoneStep.ThresholdVnd"/>.</summary>
    /// <returns>The ordered steps.</returns>
    public IReadOnlyList<TopUpMilestoneStep> OrderedSteps() =>
        this.Configuration.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Id) && s.ThresholdVnd > 0)
            .OrderBy(s => s.ThresholdVnd)
            .ToList();

    /// <summary>
    /// Records an approved bank top-up VND amount idempotently by <paramref name="reference"/>.
    /// Always records total VND even if milestones are later enabled by the host.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <param name="amountVnd">The top-up amount in VND.</param>
    /// <param name="reference">A unique reference (e.g. bank reference) preventing double-counting.</param>
    /// <param name="note">An optional note for history.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <returns>The credit outcome.</returns>
    public TopUpCreditOutcome RecordTopUp(string loginName, long amountVnd, string reference, string? note, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(loginName) || amountVnd <= 0 || string.IsNullOrWhiteSpace(reference))
        {
            return new TopUpCreditOutcome(false, 0, []);
        }

        var key = loginName.Trim().ToLowerInvariant();
        var cleanRef = reference.Trim();

        lock (this.Store.Lock)
        {
            var account = this.GetOrCreateAccountLocked(key);
            if (!account.CreditedReferences.Add(cleanRef))
            {
                return new TopUpCreditOutcome(false, account.TotalVnd, []);
            }

            var previous = account.TotalVnd;
            var next = previous + amountVnd;
            account.TotalVnd = next;
            account.History.Insert(0, new TopUpHistoryEntry
            {
                AtUtc = nowUtc,
                Kind = "topup",
                AmountVnd = amountVnd,
                Detail = string.IsNullOrWhiteSpace(note) ? cleanRef : note.Trim(),
            });
            TrimHistory(account);

            var unlocked = this.OrderedSteps()
                .Where(s => s.ThresholdVnd > previous && s.ThresholdVnd <= next && !account.ClaimedStepIds.Contains(s.Id))
                .ToList();

            this.Store.Changed();
            return new TopUpCreditOutcome(true, next, unlocked);
        }
    }

    /// <summary>
    /// Claims a milestone step once for <paramref name="loginName"/>.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <param name="stepId">The milestone step id.</param>
    /// <param name="nowUtc">The current UTC timestamp.</param>
    /// <returns>The claim outcome.</returns>
    public TopUpClaimOutcome Claim(string loginName, string stepId, DateTime nowUtc)
    {
        if (!this.Enabled)
        {
            return new TopUpClaimOutcome(TopUpClaimResult.Disabled, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(stepId))
        {
            return new TopUpClaimOutcome(TopUpClaimResult.StepNotFound, null, null, null);
        }

        var step = this.OrderedSteps().FirstOrDefault(s => string.Equals(s.Id, stepId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            return new TopUpClaimOutcome(TopUpClaimResult.StepNotFound, null, null, null);
        }

        var key = loginName.Trim().ToLowerInvariant();
        lock (this.Store.Lock)
        {
            var account = this.GetOrCreateAccountLocked(key);
            if (account.TotalVnd < step.ThresholdVnd)
            {
                return new TopUpClaimOutcome(TopUpClaimResult.NotReached, step, null, null);
            }

            if (!account.ClaimedStepIds.Add(step.Id))
            {
                return new TopUpClaimOutcome(TopUpClaimResult.AlreadyClaimed, step, null, null);
            }

            var grantRef = $"topup-milestone:{key}:{step.Id}";
            CoinGrant? coinGrant = step.Coins > 0
                ? new CoinGrant(key, step.CoinType, step.Coins, $"Top-up milestone {step.ThresholdVnd:N0} VND ({step.Id})", grantRef)
                : null;

            GiftCodeOwedReward? owed = step.Zen > 0 || step.Items.Count > 0
                ? new GiftCodeOwedReward
                {
                    Reference = grantRef,
                    Code = step.Id,
                    Zen = Math.Max(0, step.Zen),
                    Items = step.Items
                        .Where(i => i.Quantity > 0)
                        .Select(i => new GiftCodeItem
                        {
                            Group = i.Group,
                            Number = i.Number,
                            Level = i.Level,
                            OptionLevel = i.OptionLevel,
                            Luck = i.WithLuck,
                            Skill = i.WithSkill,
                            Count = i.Quantity,
                        })
                        .ToList(),
                }
                : null;

            if (owed is not null)
            {
                account.Owed.Add(owed);
            }

            account.History.Insert(0, new TopUpHistoryEntry
            {
                AtUtc = nowUtc,
                Kind = "claim",
                AmountVnd = 0,
                Detail = step.Id,
            });
            TrimHistory(account);

            this.Store.Changed();
            return new TopUpClaimOutcome(TopUpClaimResult.Ok, step, coinGrant, owed);
        }
    }

    /// <summary>
    /// Gets the progress snapshot for an account.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <returns>The total VND, next threshold, step list, owed count, and history.</returns>
    public (long TotalVnd, long? NextThresholdVnd, IReadOnlyList<TopUpStepProgress> Steps, IReadOnlyList<GiftCodeOwedReward> Owed, IReadOnlyList<TopUpHistoryEntry> History) SnapshotFor(string loginName)
    {
        var key = loginName.Trim().ToLowerInvariant();
        var steps = this.OrderedSteps();
        lock (this.Store.Lock)
        {
            var account = this.Store.Data.Accounts.TryGetValue(key, out var found) ? found : new TopUpAccountState();
            var total = account.TotalVnd;
            var progress = steps
                .Select(s =>
                {
                    var reached = total >= s.ThresholdVnd;
                    var claimed = account.ClaimedStepIds.Contains(s.Id);
                    return new TopUpStepProgress(s, reached, claimed, this.Enabled && reached && !claimed);
                })
                .ToList();

            var next = progress.FirstOrDefault(p => !p.Reached)?.Step.ThresholdVnd;
            return (total, next, progress, account.Owed.ToList(), account.History.ToList());
        }
    }

    /// <summary>
    /// Takes and clears waiting bag rewards for an account so they can be delivered.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The queued rewards.</returns>
    public List<GiftCodeOwedReward> TakeOwed(string loginName)
    {
        var key = loginName.Trim().ToLowerInvariant();
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Accounts.TryGetValue(key, out var account) || account.Owed.Count == 0)
            {
                return [];
            }

            var list = account.Owed.ToList();
            account.Owed.Clear();
            this.Store.Changed();
            return list;
        }
    }

    /// <summary>
    /// Returns undelivered rewards back to the account queue (when bag had no room).
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="rewards">The remaining rewards.</param>
    public void ReturnOwed(string loginName, IReadOnlyCollection<GiftCodeOwedReward> rewards)
    {
        if (rewards.Count == 0)
        {
            return;
        }

        var key = loginName.Trim().ToLowerInvariant();
        lock (this.Store.Lock)
        {
            var account = this.GetOrCreateAccountLocked(key);
            var seen = account.Owed.Select(o => o.Reference).ToHashSet(StringComparer.Ordinal);
            foreach (var r in rewards)
            {
                if (seen.Add(r.Reference))
                {
                    account.Owed.Add(r);
                }
            }

            this.Store.Changed();
        }
    }

    private static void TrimHistory(TopUpAccountState account)
    {
        if (account.History.Count > MaxHistoryPerAccount)
        {
            account.History.RemoveRange(MaxHistoryPerAccount, account.History.Count - MaxHistoryPerAccount);
        }
    }

    private TopUpAccountState GetOrCreateAccountLocked(string lowerLogin)
    {
        if (!this.Store.Data.Accounts.TryGetValue(lowerLogin, out var account))
        {
            account = new TopUpAccountState();
            this.Store.Data.Accounts[lowerLogin] = account;
            this.Store.Changed();
        }

        return account;
    }
}
