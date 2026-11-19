// <copyright file="OnboardingEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The rules of the new-player guide and the starter kit, without the game around them (unit-tested): which character is
/// new, the current step and its count, the steps the server checks by looking, the rewards (kept as waiting rewards with
/// unique references until they're handed over), skipping, and who gets the starter kit.
/// </summary>
public sealed class OnboardingEngine
{
    /// <summary>
    /// The name of the guide's coin grants in the coins history's "granted by" column.
    /// </summary>
    public const string GrantedBy = "HNMO onboarding";

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingEngine"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="store">The store.</param>
    public OnboardingEngine(OnboardingConfiguration configuration, OnboardingStore store)
    {
        this.Configuration = configuration;
        this.Store = store;
    }

    /// <summary>Gets the configuration.</summary>
    public OnboardingConfiguration Configuration { get; }

    /// <summary>Gets the store.</summary>
    public OnboardingStore Store { get; }

    /// <summary>
    /// Whether a step's kind is counted by events (the plug-in reports them) rather than by looking at the character.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>true</c>, for counted kinds.</returns>
    public static bool IsCounted(OnboardingStepKind kind) =>
        kind is OnboardingStepKind.TalkToNpc or OnboardingStepKind.KillMonsters or OnboardingStepKind.UseItem or OnboardingStepKind.UseSkill or OnboardingStepKind.OpenMissions;

    /// <summary>
    /// Notes a character created while the plug-in runs: it's new, it may reserve the starter kit, and the kit's stat
    /// points are given at once (the character isn't saved yet).
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="login">The login name.</param>
    /// <param name="name">The character name.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The stat points to add to the new character.</returns>
    public int CharacterCreated(string characterId, string login, string? name, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var state = this.GetOrCreate(characterId, login, name, nowUtc, true);
            state.CreatedHere = true;
            var kit = this.Configuration.StarterKit;
            if (!kit.Enabled || !this.Reserve(state, characterId, login))
            {
                return 0;
            }

            this.Store.Changed();
            return kit.PointsEnabled ? Math.Max(0, kit.LevelUpPoints) : 0;
        }
    }

    /// <summary>
    /// Gets the state of a character in the game; the first time decides whether it's new (created after the plug-in was
    /// turned on, or at most <see cref="OnboardingConfiguration.NewCharacterMaxLevel"/>).
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="login">The login name.</param>
    /// <param name="name">The character name.</param>
    /// <param name="createdUtc">When the character was created.</param>
    /// <param name="level">Its level.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>A copy of the state's status.</returns>
    public OnboardingStatus See(string characterId, string login, string? name, DateTime createdUtc, int level, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            var isNew = createdUtc >= this.Store.Data.ActivatedAtUtc
                || (this.Configuration.NewCharacterMaxLevel > 0 && level <= this.Configuration.NewCharacterMaxLevel);
            var state = this.GetOrCreate(characterId, login, name, nowUtc, isNew);
            if (state.Name != name)
            {
                state.Name = name;
                this.Store.Changed();
            }

            return state.Status;
        }
    }

    /// <summary>
    /// Gets the status of a character; <see cref="OnboardingStatus.NotEligible"/> when it wasn't seen.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The status.</returns>
    public OnboardingStatus StatusOf(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Characters.TryGetValue(characterId, out var state) ? state.Status : OnboardingStatus.NotEligible;
        }
    }

    /// <summary>
    /// Gets the current step of a character: the first step of the guide which isn't done; <c>null</c> when the guide
    /// doesn't run for it (or is switched off).
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The step.</returns>
    public OnboardingStep? CurrentStep(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Characters.TryGetValue(characterId, out var state) ? this.CurrentOf(state) : null;
        }
    }

    /// <summary>
    /// Gets the progress of the current step.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="facts">What's known of the character, for the steps checked by looking.</param>
    /// <returns>The step, the count so far and the count to reach; <c>null</c> without a current step.</returns>
    public (OnboardingStep Step, int Value, int Target)? Progress(string characterId, OnboardingFacts? facts)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var state) || this.CurrentOf(state) is not { } step)
            {
                return null;
            }

            var target = Math.Max(1, step.Target);
            var value = step.Kind switch
            {
                OnboardingStepKind.ReachLevel => facts?.Level ?? 0,
                OnboardingStepKind.AddStatPoints => facts is null || state.StepId != step.Id || state.Baseline is null ? 0 : facts.StatPoints - state.Baseline.Value,
                OnboardingStepKind.LearnSkill => facts is null || state.StepId != step.Id || state.Baseline is null ? 0 : facts.LearnedSkills - state.Baseline.Value,
                OnboardingStepKind.EnterMap or OnboardingStepKind.WalkTo or OnboardingStepKind.JoinParty or OnboardingStepKind.OpenCashShop => 0,
                _ => state.StepId == step.Id ? state.Value : 0,
            };
            if (step.Kind is OnboardingStepKind.EnterMap or OnboardingStepKind.WalkTo or OnboardingStepKind.JoinParty or OnboardingStepKind.OpenCashShop)
            {
                target = 1;
            }

            return (step, Math.Clamp(value, 0, target), target);
        }
    }

    /// <summary>
    /// Counts an event for the current step, when it's of that kind and matches.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="kind">The kind of the event.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="matches">Whether the step counts this event (null: every step of the kind).</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>What was done.</returns>
    public OnboardingAdvance Report(string characterId, OnboardingStepKind kind, int amount, Func<OnboardingStep, bool>? matches, DateTime nowUtc)
    {
        if (amount <= 0)
        {
            return OnboardingAdvance.None;
        }

        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var state)
                || this.CurrentOf(state) is not { } step
                || step.Kind != kind
                || (matches is not null && !matches(step)))
            {
                return OnboardingAdvance.None;
            }

            this.Follow(state, step);
            state.Value = (int)Math.Min((long)state.Value + amount, int.MaxValue);
            this.Store.Changed();
            if (state.Value < Math.Max(1, step.Target))
            {
                return OnboardingAdvance.None;
            }

            var completed = new List<OnboardingStep>();
            var finished = this.Complete(state, characterId, step, nowUtc, completed, true);
            return new OnboardingAdvance(completed, finished);
        }
    }

    /// <summary>
    /// Checks the steps which are done by being somewhere or having something (level, map, spot, party, stat points,
    /// skills, the cash shop); several may be done at once.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="facts">What's known of the character.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>What was done.</returns>
    public OnboardingAdvance Check(string characterId, OnboardingFacts facts, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var state))
            {
                return OnboardingAdvance.None;
            }

            var completed = new List<OnboardingStep>();
            var finished = false;
            for (var guard = 0; guard < 200 && this.CurrentOf(state) is { } step; guard++)
            {
                this.Follow(state, step);
                if (step.Kind is OnboardingStepKind.AddStatPoints or OnboardingStepKind.LearnSkill && state.Baseline is null)
                {
                    state.Baseline = step.Kind == OnboardingStepKind.AddStatPoints ? facts.StatPoints : facts.LearnedSkills;
                    this.Store.Changed();
                }

                if (!IsDone(step, state, facts))
                {
                    break;
                }

                finished = this.Complete(state, characterId, step, nowUtc, completed, true);
                if (finished)
                {
                    break;
                }
            }

            return new OnboardingAdvance(completed, finished);
        }
    }

    /// <summary>
    /// Skips the current step, when it's optional (it gives no reward).
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>What was done (the guide may be finished by it).</returns>
    public OnboardingAdvance SkipStep(string characterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var state) || this.CurrentOf(state) is not { Optional: true } step)
            {
                return OnboardingAdvance.None;
            }

            state.SkippedSteps.Add(step.Id);
            var completed = new List<OnboardingStep>();
            var finished = this.Complete(state, characterId, step, nowUtc, completed, false);
            return new OnboardingAdvance([], finished);
        }
    }

    /// <summary>
    /// Ends the guide of a character (the player confirmed it). Rewards earned before still come.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns><c>true</c>, if it ran.</returns>
    public bool Skip(string characterId, DateTime nowUtc)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var state) || state.Status != OnboardingStatus.Active)
            {
                return false;
            }

            state.Status = OnboardingStatus.Skipped;
            state.EndedUtc = nowUtc;
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>
    /// Takes the starter kit of a character at its login, when it's new, the kit is on and it wasn't handed over (per
    /// character, or per account to the first new character). The kit becomes a waiting reward.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="login">The login name.</param>
    /// <param name="classNumber">The character class number (for class items).</param>
    /// <param name="nowUtc">The time.</param>
    /// <returns>The kit; <c>null</c>, if there's none for it.</returns>
    public OnboardingOwedReward? TakeStarterKit(string characterId, string login, int classNumber, DateTime nowUtc)
    {
        var kit = this.Configuration.StarterKit;
        lock (this.Store.Lock)
        {
            if (!kit.Enabled
                || !this.Store.Data.Characters.TryGetValue(characterId, out var state)
                || state.Status == OnboardingStatus.NotEligible
                || state.KitGiven
                || !this.Reserve(state, characterId, login))
            {
                return null;
            }

            state.KitGiven = true;
            var reward = new OnboardingOwedReward
            {
                Reference = $"onboarding:{characterId}:kit",
                Kind = "kit",
                TitleEn = "Starter kit",
                TitleVi = "Quà tân thủ",
                Reason = "Starter kit",
                Login = login,
                Zen = kit.ZenEnabled ? Math.Max(0, kit.Zen) : 0,
                Coins = kit.CoinsEnabled ? Math.Max(0, kit.Coins) : 0,
                CoinType = kit.CoinType,
                Items = kit.ItemsEnabled ? KitItemsOf(kit, classNumber) : new List<StarterKitItem>(),
                BuffMinutes = kit.ExperienceBuff.Enabled ? Math.Max(0, kit.ExperienceBuff.Minutes) : 0,
                BuffItemGroup = kit.ExperienceBuff.ItemGroup,
                BuffItemNumber = kit.ExperienceBuff.ItemNumber,
                EarnedAtUtc = nowUtc,
            };
            this.Store.Changed();
            if (reward.Coins <= 0 && !reward.HasGoods)
            {
                return null;
            }

            this.AddOwed(characterId, reward);
            return reward;
        }
    }

    /// <summary>
    /// Takes the not-yet-told starter kit of a character, so the client is told once.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns><c>true</c>, if the kit was handed over and the player wasn't told yet.</returns>
    public bool TakeKitNotice(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Characters.TryGetValue(characterId, out var state) || !state.KitGiven || state.KitShown)
            {
                return false;
            }

            state.KitShown = true;
            this.Store.Changed();
            return true;
        }
    }

    /// <summary>
    /// Gets the items of the kit for a class.
    /// </summary>
    /// <param name="kit">The kit.</param>
    /// <param name="classNumber">The class number.</param>
    /// <returns>Copies of the items.</returns>
    public static List<StarterKitItem> KitItemsOf(StarterKitConfiguration kit, int classNumber) =>
        kit.Items.Where(i => i.Count > 0 && (i.ClassNumber < 0 || i.ClassNumber == classNumber)).Select(Copy).ToList();

    /// <summary>
    /// Gets the waiting rewards of a character.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>Copies of the rewards.</returns>
    public IReadOnlyList<OnboardingOwedReward> Owed(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Owed.TryGetValue(characterId, out var list) ? list.ToList() : [];
        }
    }

    /// <summary>
    /// Takes the waiting rewards of a character out of the store; give back what wasn't given with <see cref="ReturnOwed"/>.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The rewards.</returns>
    public List<OnboardingOwedReward> TakeOwed(string characterId)
    {
        lock (this.Store.Lock)
        {
            if (!this.Store.Data.Owed.Remove(characterId, out var list))
            {
                return [];
            }

            this.Store.Changed();
            return list;
        }
    }

    /// <summary>
    /// Puts rewards which weren't given back.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <param name="rewards">The rewards.</param>
    public void ReturnOwed(string characterId, IEnumerable<OnboardingOwedReward> rewards)
    {
        lock (this.Store.Lock)
        {
            foreach (var reward in rewards)
            {
                this.AddOwed(characterId, reward);
            }

            this.Store.Changed();
        }
    }

    /// <summary>
    /// Gets the ids of the steps of a character which are done and which were skipped.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The ids.</returns>
    public (IReadOnlyList<string> Completed, IReadOnlyList<string> Skipped) StepsOf(string characterId)
    {
        lock (this.Store.Lock)
        {
            return this.Store.Data.Characters.TryGetValue(characterId, out var state)
                ? (state.Completed.ToList(), state.SkippedSteps.ToList())
                : ([], []);
        }
    }

    private static StarterKitItem Copy(StarterKitItem i) => new()
    {
        Group = i.Group, Number = i.Number, Level = i.Level, Count = i.Count, Durability = i.Durability, Skill = i.Skill,
        Luck = i.Luck, OptionLevel = i.OptionLevel, ExcellentOptions = i.ExcellentOptions, ClassNumber = i.ClassNumber,
    };

    private static List<StarterKitItem> ItemsOf(MissionReward reward) =>
        reward.Items.Where(i => i.Count > 0).Select(i => new StarterKitItem { Group = i.Group, Number = i.Number, Level = i.Level, Count = i.Count }).ToList();

    private static bool IsDone(OnboardingStep step, OnboardingCharacterState state, OnboardingFacts facts) => step.Kind switch
    {
        OnboardingStepKind.ReachLevel => facts.Level >= Math.Max(1, step.Target),
        OnboardingStepKind.EnterMap => step.MapNumber < 0 || facts.Map == step.MapNumber,
        OnboardingStepKind.WalkTo => (step.MapNumber < 0 || facts.Map == step.MapNumber)
            && Math.Max(Math.Abs(facts.X - step.X), Math.Abs(facts.Y - step.Y)) <= step.Radius,
        OnboardingStepKind.JoinParty => facts.InParty,
        OnboardingStepKind.OpenCashShop => facts.CashShopOpen,
        OnboardingStepKind.AddStatPoints => state.Baseline is { } points && facts.StatPoints - points >= Math.Max(1, step.Target),
        OnboardingStepKind.LearnSkill => state.Baseline is { } skills && facts.LearnedSkills - skills >= Math.Max(1, step.Target),
        _ => false,
    };

    private OnboardingCharacterState GetOrCreate(string characterId, string login, string? name, DateTime nowUtc, bool isNew)
    {
        if (!this.Store.Data.Characters.TryGetValue(characterId, out var state))
        {
            state = new OnboardingCharacterState
            {
                Name = name,
                Account = login,
                FirstSeenUtc = nowUtc,
                Status = isNew ? OnboardingStatus.Active : OnboardingStatus.NotEligible,
            };
            this.Store.Data.Characters[characterId] = state;
            this.Store.Changed();
        }

        return state;
    }

    /// <summary>
    /// Reserves the kit for the character: per character always, per account only for the account's first new character.
    /// </summary>
    private bool Reserve(OnboardingCharacterState state, string characterId, string login)
    {
        if (state.KitReserved)
        {
            return true;
        }

        if (this.Configuration.StarterKit.PerAccount)
        {
            if (!this.Store.Data.Accounts.TryGetValue(login, out var account))
            {
                account = new OnboardingAccountState();
                this.Store.Data.Accounts[login] = account;
            }

            if (account.KitCharacter is not null && account.KitCharacter != characterId)
            {
                return false;
            }

            account.KitCharacter = characterId;
        }

        state.KitReserved = true;
        this.Store.Changed();
        return true;
    }

    private OnboardingStep? CurrentOf(OnboardingCharacterState state)
    {
        if (state.Status != OnboardingStatus.Active || !this.Configuration.GuideEnabled)
        {
            return null;
        }

        return this.Configuration.EnabledSteps().FirstOrDefault(s => !state.Completed.Contains(s.Id, StringComparer.Ordinal));
    }

    /// <summary>Starts counting a step which just became the current one.</summary>
    private void Follow(OnboardingCharacterState state, OnboardingStep step)
    {
        if (state.StepId == step.Id)
        {
            return;
        }

        state.StepId = step.Id;
        state.Value = 0;
        state.Baseline = null;
        this.Store.Changed();
    }

    /// <summary>
    /// Marks a step done (with its reward waiting), and the guide finished (with the final reward) when it was the last.
    /// </summary>
    /// <returns><c>true</c>, if the guide was finished.</returns>
    private bool Complete(OnboardingCharacterState state, string characterId, OnboardingStep step, DateTime nowUtc, List<OnboardingStep> completed, bool rewarded)
    {
        state.Completed.Add(step.Id);
        state.StepId = null;
        state.Value = 0;
        state.Baseline = null;
        this.Store.Changed();
        if (rewarded)
        {
            completed.Add(step);
            if (!step.Reward.IsEmpty)
            {
                this.AddOwed(characterId, new OnboardingOwedReward
                {
                    Reference = $"onboarding:{characterId}:{step.Id}",
                    Kind = "step",
                    StepId = step.Id,
                    TitleEn = step.TitleEn,
                    TitleVi = step.TitleVi,
                    Reason = $"New-player guide: \"{step.TitleEn}\"",
                    Login = state.Account ?? string.Empty,
                    Zen = Math.Max(0, step.Reward.Zen),
                    Coins = Math.Max(0, step.Reward.Coins),
                    CoinType = step.Reward.CoinType,
                    Items = ItemsOf(step.Reward),
                    EarnedAtUtc = nowUtc,
                });
            }
        }

        if (this.CurrentOf(state) is not null)
        {
            return false;
        }

        state.Status = OnboardingStatus.Done;
        state.EndedUtc = nowUtc;
        var final = this.Configuration.FinalReward;
        if (!final.IsEmpty)
        {
            this.AddOwed(characterId, new OnboardingOwedReward
            {
                Reference = $"onboarding:{characterId}:final",
                Kind = "final",
                TitleEn = "New-player guide finished",
                TitleVi = "Hoàn thành hướng dẫn tân thủ",
                Reason = "New-player guide finished",
                Login = state.Account ?? string.Empty,
                Zen = Math.Max(0, final.Zen),
                Coins = Math.Max(0, final.Coins),
                CoinType = final.CoinType,
                Items = ItemsOf(final),
                EarnedAtUtc = nowUtc,
            });
        }

        return true;
    }

    private void AddOwed(string characterId, OnboardingOwedReward reward)
    {
        if (!this.Store.Data.Owed.TryGetValue(characterId, out var list))
        {
            list = new List<OnboardingOwedReward>();
            this.Store.Data.Owed[characterId] = list;
        }

        if (list.All(r => r.Reference != reward.Reference))
        {
            list.Add(reward);
        }

        this.Store.Changed();
    }
}
