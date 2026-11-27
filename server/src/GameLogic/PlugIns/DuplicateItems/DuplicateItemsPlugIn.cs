// <copyright file="DuplicateItemsPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.DuplicateItems;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// HNMO's duplicate check (owner's choice, 2026-11-20): every player item gets an origin serial once (in the database
/// every scan, and in the bag and vault of a character entering the game), and every copy of an item keeps it. Every
/// hour the database is scanned for two items with one serial; both are frozen (no trade, sale, drop, vault, store,
/// market, escrow or mix) and the administrator decides on the admin panel's "Duplicate items" page.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.DuplicateItemsPlugIn_Name), Description = nameof(Properties.PlugInResources.DuplicateItemsPlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("3C8E5A71-9D24-4F6B-A1E3-7B52C9D04F18")]
public sealed class DuplicateItemsPlugIn : IPeriodicTaskPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<DuplicateItemsConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly TimeSpan FrozenRefresh = TimeSpan.FromSeconds(30);

    // One scan at a time and one schedule per process, whichever game server's plug-in runs it.
    private static int _scanning;
    private static DateTime _nextScanUtc = DateTime.UtcNow.AddMinutes(2);
    private static volatile HashSet<Guid> _frozen = [];
    private static volatile bool _freeze = true;

    private DateTime _nextRefreshUtc = DateTime.MinValue;

    /// <inheritdoc />
    public DuplicateItemsConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets a value indicating whether an item is frozen: it shares its serial with another item and the administrator
    /// hasn't decided yet.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if frozen.</returns>
    public static bool IsFrozen(Item item) => _freeze && item?.Serial is { } serial && _frozen.Contains(serial);

    /// <summary>
    /// Refuses a use of a frozen item with a message (the trade, sale, drop, store, market, escrow and mix checks of
    /// the account safety, and moves into the vault).
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="item">The item.</param>
    /// <returns><c>true</c>, if the item is frozen.</returns>
    public static async ValueTask<bool> RefuseFrozenAsync(Player player, Item item)
    {
        if (!IsFrozen(item))
        {
            return false;
        }

        var vietnamese = player.Culture.TwoLetterISOLanguageName == "vi";
        await player.ShowBlueMessageAsync(vietnamese
            ? "Vật phẩm này đang bị đóng băng: máy chủ thấy một món khác cùng số serial (có thể là đồ nhân bản). Quản trị viên sẽ kiểm tra; trong lúc đó không thể giao dịch, bán, vứt, cất rương, rao bán hay ép."
            : "This item is frozen: the server found another item with the same serial (a possible duplicate). An administrator will check it; meanwhile it can't be traded, sold, dropped, stored, listed or mixed.").ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Reads the frozen serials of a state file again at once (after the admin panel decided a case, or in tests).
    /// </summary>
    /// <param name="fileName">The state file.</param>
    /// <param name="freeze">Whether found duplicates are frozen.</param>
    public static void Refresh(string? fileName, bool freeze = true)
    {
        _freeze = freeze;
        _frozen = DuplicateItemsStore.Open(fileName).Read(DuplicateItemsEngine.FrozenSerials);
    }

    /// <summary>
    /// Scans the database once: gives serials to the items without one, finds the duplicates and records them.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="fileName">The state file.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The new cases.</returns>
    public static IReadOnlyList<DuplicateItemCase> Scan(IItemSerialDatabase database, string? fileName, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        var given = database.AssignMissing();
        var rows = database.FindDuplicates();
        var store = DuplicateItemsStore.Open(fileName);
        var added = store.Mutate(data =>
        {
            var found = DuplicateItemsEngine.Record(data, rows, DateTime.UtcNow);
            data.LastScanSerialsGiven = given;
            return (true, found);
        });
        _frozen = store.Read(DuplicateItemsEngine.FrozenSerials);
        if (given > 0)
        {
            logger.LogInformation("Duplicate check: {Count} items got their serial.", given);
        }

        foreach (var found in added)
        {
            logger.LogWarning(
                "Duplicate check: {Count} items share the serial {Serial} ({Item}): {Owners}. They're frozen until an administrator decides (admin panel, Duplicate items).",
                found.Items.Count,
                found.Serial,
                found.Items[0].ItemName,
                string.Join(", ", found.Items.Select(i => $"{i.AccountLogin}/{i.CharacterName} {i.Place}")));
        }

        return added;
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new DuplicateItemsConfiguration();

    /// <inheritdoc />
    public ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var cfg = this.Configuration ?? new DuplicateItemsConfiguration();
        var now = DateTime.UtcNow;
        if (now >= this._nextRefreshUtc)
        {
            this._nextRefreshUtc = now + FrozenRefresh;
            try
            {
                Refresh(cfg.FileName, cfg.FreezeDuplicates);
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<DuplicateItemsPlugIn>().LogWarning(ex, "Duplicate check: the frozen items couldn't be read.");
            }
        }

        if (now < _nextScanUtc || ItemSerials.Database is not { } database || Interlocked.Exchange(ref _scanning, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        _nextScanUtc = now.AddMinutes(Math.Max(5, cfg.ScanIntervalMinutes));
        var logger = gameContext.LoggerFactory.CreateLogger<DuplicateItemsPlugIn>();
        _ = Task.Run(() =>
        {
            try
            {
                Scan(database, cfg.FileName, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Duplicate check: the scan failed.");
            }
            finally
            {
                Interlocked.Exchange(ref _scanning, 0);
            }
        });
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        _nextScanUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState == PlayerState.EnteredWorld)
        {
            // Items made since the last scan get their serial now; it's saved with the character.
            GiveSerials(player.Inventory?.Items);
            GiveSerials(player.Vault?.Items);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Gives a serial to every item without one.</summary>
    /// <param name="items">The items.</param>
    /// <returns>How many got one.</returns>
    internal static int GiveSerials(IEnumerable<Item>? items)
    {
        var count = 0;
        foreach (var item in items ?? [])
        {
            if (item.Serial is null)
            {
                item.EnsureSerial();
                count++;
            }
        }

        return count;
    }
}
