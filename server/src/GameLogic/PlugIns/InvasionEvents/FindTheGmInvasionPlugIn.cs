// <copyright file="FindTheGmInvasionPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.InvasionEvents;

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// Spawns a hidden GameMaster NPC on a scheduled map; the first player(s) to talk to the GM win coins and zen (OFF by default).
/// </summary>
[PlugIn]
[Display(Name = "Find the GM Event", Description = "Spawns a hidden GameMaster NPC on a map on a schedule; finding and talking to it awards coins and zen (OFF by default).")]
[Guid("F18C42A9-65E1-4B3D-A790-8E2D61C4F902")]
public sealed class FindTheGmInvasionPlugIn : BaseInvasionPlugIn<FindTheGmConfiguration>, ISupportDefaultCustomConfiguration, IPlayerTalkToNpcPlugIn, IDisabledByDefault
{
    private readonly ConcurrentDictionary<IGameContext, FindGmRun> _runs = new();

    /// <inheritdoc />
    public object CreateDefaultConfig() => new FindTheGmConfiguration();

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (!this._runs.TryGetValue(player.GameContext, out var run))
        {
            return;
        }

        lock (run.Gate)
        {
            if (run.Finished || !run.Npcs.Contains(npc))
            {
                return;
            }

            eventArgs.HasBeenHandled = true;
        }

        var config = this.Configuration ?? new FindTheGmConfiguration();
        var accountKey = player.Account?.LoginName ?? player.SelectedCharacter?.Name ?? player.Id.ToString();
        var characterName = player.SelectedCharacter?.Name ?? accountKey;
        var mapNameEn = npc.CurrentMap?.Definition.Name.GetTranslation(CultureInfo.GetCultureInfo("en")) ?? "Town";
        var mapNameVi = npc.CurrentMap?.Definition.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")) ?? mapNameEn;
        var claimedNow = false;
        var rank = 0;

        lock (run.Gate)
        {
            if (!run.Finished && run.WinnerKeys.Count < Math.Max(1, config.MaxWinners) && run.WinnerKeys.Add(accountKey))
            {
                claimedNow = true;
                rank = run.WinnerKeys.Count;
                if (run.WinnerKeys.Count >= Math.Max(1, config.MaxWinners))
                {
                    run.Finished = true;
                }
            }
        }

        if (!claimedNow)
        {
            var alreadyMsg = player.Culture.TwoLetterISOLanguageName == "vi"
                ? "Sự kiện Truy tìm GM đã có người chiến thắng rồi!"
                : "You already claimed or the winners limit has been reached!";
            await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(alreadyMsg, MessageType.BlueNormal)).ConfigureAwait(false);
            return;
        }

        if (config.RewardCoins > 0)
        {
            await MissionCoinGranter.GrantAsync(
                player.GameContext,
                new CoinGrant(accountKey, config.RewardCoinType, config.RewardCoins, "Find the GM", $"findgm:{run.RunId}:{rank}:{accountKey}"),
                "HNMO Find the GM").ConfigureAwait(false);
        }

        if (config.RewardZen > 0)
        {
            player.TryAddMoney(config.RewardZen);
        }

        TimetableStore.Open(config.FileName).RecordFindGmWinner(
            characterName,
            mapNameEn,
            config.RewardCoins,
            config.RewardCoinType.ToString(),
            config.RewardZen,
            DateTime.UtcNow);

        await player.GameContext.ForEachPlayerAsync(async online =>
        {
            var msg = online.Culture.TwoLetterISOLanguageName == "vi"
                ? $"[Truy tìm GM] {characterName} đã tìm thấy GM tại {mapNameVi} và nhận thưởng!"
                : $"[Find the GM] {characterName} found the GM in {mapNameEn} and won the reward!";
            await online.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(msg, MessageType.GoldenCenter)).ConfigureAwait(false);
        }).ConfigureAwait(false);

        if (run.Finished)
        {
            await DisposeRunNpcsAsync(run).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask SpawnMobsOnMapsAsync(InvasionGameServerState state)
    {
        var config = this.Configuration ?? new FindTheGmConfiguration();
        var mapIds = state.AnnouncedMapIds.Count > 0
            ? state.AnnouncedMapIds
            : (IReadOnlyList<ushort>)[InvasionMaps.Lorencia];
        var mapId = mapIds[0];
        var gameMap = await state.Context.GetMapAsync(mapId).ConfigureAwait(false);
        if (gameMap is null)
        {
            return;
        }

        var monsterDef = this.ResolveMonsterDefinition(state.Context, InvasionMonsters.GameMaster);
        if (monsterDef is null)
        {
            return;
        }

        var point = gameMap.Terrain.RandomWalkableCoordinate;
        if (point is null)
        {
            return;
        }

        var area = new MonsterSpawnArea
        {
            GameMap = gameMap.Definition,
            MonsterDefinition = monsterDef,
            SpawnTrigger = SpawnTrigger.OnceAtEventStart,
            Quantity = 1,
            X1 = point.Value.X,
            X2 = point.Value.X,
            Y1 = point.Value.Y,
            Y2 = point.Value.Y,
        };

        var npc = new NonPlayerCharacter(area, monsterDef, gameMap);
        npc.Initialize();
        await gameMap.AddAsync(npc).ConfigureAwait(false);

        var run = new FindGmRun(Guid.NewGuid().ToString("N")[..12]);
        run.Npcs.Add(npc);
        this._runs[state.Context] = run;
    }

    /// <inheritdoc />
    protected override async ValueTask OnFinishedAsync(InvasionGameServerState state)
    {
        if (this._runs.TryRemove(state.Context, out var run))
        {
            await DisposeRunNpcsAsync(run).ConfigureAwait(false);
        }

        await base.OnFinishedAsync(state).ConfigureAwait(false);
    }

    private static async ValueTask DisposeRunNpcsAsync(FindGmRun run)
    {
        List<NonPlayerCharacter> snapshot;
        lock (run.Gate)
        {
            run.Finished = true;
            snapshot = run.Npcs.ToList();
            run.Npcs.Clear();
        }

        foreach (var npc in snapshot)
        {
            try
            {
                await npc.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore cleanup races when the map or server is shutting down.
            }
        }
    }

    private sealed class FindGmRun(string runId)
    {
        public string RunId { get; } = runId;

        public object Gate { get; } = new();

        public List<NonPlayerCharacter> Npcs { get; } = [];

        public HashSet<string> WinnerKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Finished { get; set; }
    }
}
