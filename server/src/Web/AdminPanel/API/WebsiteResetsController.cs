// <copyright file="WebsiteResetsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Security.Cryptography;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.PlugIns.ResetOptions;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Web.AdminPanel.Auth;

/// <summary>
/// The website's resets: the public rankings (resets of the month, relifes) and rules, and, for the account page through
/// the web bridge (which holds the API key), an account's characters with their resets, relifes and today's limits, and
/// the reset, relife, stat points (add, clear) and delegated reset switch. Every call checks the account's password; the
/// changes of a character need the account offline (not in the game, not offline leveling), which the login server's
/// reservation makes sure of while the change is saved.
/// </summary>
[ApiController]
[Route("api/website/resets")]
public sealed class WebsiteResetsController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILoginServer loginServer,
    IServerProvider serverProvider,
    ILogger<WebsiteResetsController> logger) : ControllerBase
{
    /// <summary>Gets the public rankings and rules.</summary>
    /// <returns>The JSON; "enabled": false when the plug-in is off.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        if (await this.EngineAsync(this.HttpContext.RequestAborted).ConfigureAwait(false) is not { } engine)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        return this.Content(ResetStateWriter.WritePublic(engine, DateTime.UtcNow), "application/json");
    }

    /// <summary>Gets the characters of an account with their reset state, after checking its password.</summary>
    /// <param name="request">The login name and password.</param>
    /// <returns>The characters; 401 when the name or password is wrong; 403 when the plug-in is off.</returns>
    [HttpPost("account")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> AccountAsync([FromBody] ResetAccountRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewPlayerContext(configuration);
        if (await AuthenticateAsync(context, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return this.Unauthorized(new { error = "login" });
        }

        var busy = await this.BusyAsync(account.LoginName).ConfigureAwait(false);
        var names = GiftCodesPlugIn.ItemNames(configuration);
        return this.Content(ResetStateWriter.Serialize(new
        {
            v = 1,
            loginName = account.LoginName,
            busy,
            options = Options(engine.Configuration),
            characters = account.Characters.OrderBy(c => c.CharacterSlot).Select(c => Character(engine, account, c, busy, names)).ToList(),
        }), "application/json");
    }

    /// <summary>Does an action for a character of an account, after checking its password.</summary>
    /// <param name="request">The login name, password, character, action and stat points.</param>
    /// <returns>The result and the character's new state; 401 when the name or password is wrong.</returns>
    [HttpPost("action")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedOperator)]
    public async Task<IActionResult> ActionAsync([FromBody] ResetActionRequest request)
    {
        var cancellationToken = this.HttpContext.RequestAborted;
        if (await this.EngineAsync(cancellationToken).ConfigureAwait(false) is not { } engine)
        {
            return this.StatusCode(403, new { error = "off" });
        }

        var options = engine.Configuration.Website;
        var action = request.Action?.Trim().ToLowerInvariant() ?? string.Empty;
        var allowed = action switch
        {
            "reset" => options.AllowReset,
            "relife" => options.AllowRelife && engine.Configuration.Relife.Enabled,
            "add-stats" => options.AllowAddStats,
            "clear-stats" => options.AllowClearStats,
            "delegated-on" or "delegated-off" => options.AllowDelegatedSwitch && engine.Configuration.Delegated.Enabled,
            _ => (bool?)null,
        };
        if (allowed is null)
        {
            return this.BadRequest(new { error = "action" });
        }

        var configuration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using (var check = persistence.CreateNewPlayerContext(configuration))
        {
            if (await AuthenticateAsync(check, request.LoginName, request.Password, cancellationToken).ConfigureAwait(false) is not { } checkedAccount)
            {
                return this.Unauthorized(new { error = "login" });
            }

            if (checkedAccount.Characters.All(c => !string.Equals(c.Name, request.Character, StringComparison.Ordinal)))
            {
                return this.BadRequest(new { error = "character" });
            }
        }

        var login = request.LoginName!.Trim();
        var names = GiftCodesPlugIn.ItemNames(configuration);
        if (allowed is false)
        {
            return await this.AnswerAsync(engine, configuration, login, request.Character!, nameof(ResetResult.NotAllowed), null, names, cancellationToken).ConfigureAwait(false);
        }

        if (action is "delegated-on" or "delegated-off")
        {
            using var context = persistence.CreateNewPlayerContext(configuration);
            var account = await context.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false);
            var character = account?.Characters.FirstOrDefault(c => c.Name == request.Character);
            if (account is null || character is null)
            {
                return this.BadRequest(new { error = "character" });
            }

            engine.SetDelegated(SubjectOf(account, character), action == "delegated-on");
            Save(engine);
            logger.LogInformation("Resets: {Login} switched the delegated reset of {Character} {State} on the website.", login, character.Name, action == "delegated-on" ? "on" : "off");
            return await this.AnswerAsync(engine, configuration, login, character.Name, nameof(ResetResult.Ok), null, names, cancellationToken).ConfigureAwait(false);
        }

        if (await this.BusyAsync(login).ConfigureAwait(false) is not null)
        {
            return await this.AnswerAsync(engine, configuration, login, request.Character!, nameof(ResetResult.Online), null, names, cancellationToken).ConfigureAwait(false);
        }

        var reservation = RandomNumberGenerator.GetInt32(byte.MaxValue + 1, int.MaxValue);
        string result;
        object? plan = null;
        try
        {
            if (!await loginServer.TryReserveAccountAsync(login, reservation).ConfigureAwait(false))
            {
                result = nameof(ResetResult.Online);
            }
            else
            {
                // Read again after the reservation: a game session may have saved just before it.
                using var context = persistence.CreateNewPlayerContext(configuration);
                var account = await context.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false);
                var character = account?.Characters.FirstOrDefault(c => c.Name == request.Character);
                if (account is null || character is null)
                {
                    return this.BadRequest(new { error = "character" });
                }

                (result, plan) = action switch
                {
                    "reset" or "relife" => await this.ResetAsync(engine, context, account, character, action == "relife", names).ConfigureAwait(false),
                    "add-stats" => (AddStats(character, request, (a, v) => Set(context, character, a, v)), null),
                    _ => (ClearStatsPaid(engine.Configuration.Website, context, account, character), null),
                };
                if (result == nameof(ResetResult.Ok) && !await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
                {
                    result = "Failed";
                }

                if (result == nameof(ResetResult.Ok))
                {
                    logger.LogInformation("Resets: {Login} did {Action} for {Character} on the website.", login, action, character.Name);
                }
            }
        }
        finally
        {
            await loginServer.ReleaseAccountReservationAsync(login, reservation).ConfigureAwait(false);
        }

        if (result == nameof(ResetResult.Ok))
        {
            Save(engine);
        }

        return await this.AnswerAsync(engine, configuration, login, request.Character!, result, plan, names, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds free points to the stats of a character: only the class's stats, not more than its free points and the
    /// stats' maximum.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="request">The points per stat.</param>
    /// <param name="set">Sets an attribute's value.</param>
    /// <returns>The result: "Ok", "NothingToAdd", "NotEnoughPoints" or "StatMaximum".</returns>
    internal static string AddStats(Character character, ResetActionRequest request, Action<AttributeDefinition, float> set)
    {
        var wanted = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["str"] = request.Str ?? 0,
            ["agi"] = request.Agi ?? 0,
            ["vit"] = request.Vit ?? 0,
            ["ene"] = request.Ene ?? 0,
            ["cmd"] = request.Cmd ?? 0,
        };
        if (wanted.Values.Any(v => v < 0) || wanted.Values.Sum(v => (long)v) <= 0)
        {
            return "NothingToAdd";
        }

        var stats = ResetApplier.IncreasableStats(character).ToDictionary(s => ResetApplier.StatKey(s.Attribute!) ?? string.Empty, s => s, StringComparer.Ordinal);
        if (wanted.Any(p => p.Value > 0 && !stats.ContainsKey(p.Key)))
        {
            return "NothingToAdd";
        }

        if (wanted.Values.Sum(v => (long)v) > character.LevelUpPoints)
        {
            return "NotEnoughPoints";
        }

        var changes = new List<(AttributeDefinition Attribute, float Value)>();
        foreach (var (key, points) in wanted.Where(p => p.Value > 0))
        {
            var attribute = stats[key].Attribute!;
            var value = Get(character, attribute) + points;
            if (attribute.MaximumValue is { } maximum && value > maximum)
            {
                return "StatMaximum";
            }

            changes.Add((attribute, value));
        }

        foreach (var (attribute, value) in changes)
        {
            set(attribute, value);
        }

        character.LevelUpPoints -= (int)wanted.Values.Sum(v => (long)v);
        return nameof(ResetResult.Ok);
    }

    private static object Options(ResetOptionsConfiguration configuration) => new
    {
        allowReset = configuration.Website.AllowReset,
        allowRelife = configuration.Website.AllowRelife && configuration.Relife.Enabled,
        allowAddStats = configuration.Website.AllowAddStats,
        allowClearStats = configuration.Website.AllowClearStats,
        clearStatsZen = configuration.Website.ClearStatsZen,
        clearStatsCoins = configuration.Website.ClearStatsCoins,
        clearStatsCoinType = configuration.Website.ClearStatsCoinType,
        allowDelegated = configuration.Website.AllowDelegatedSwitch && configuration.Delegated.Enabled,
    };

    private static object Character(ResetEngine engine, Account account, Character character, string? busy, Func<short, short, string?> names) => new
    {
        cls = character.CharacterClass?.Number ?? 0,
        className = character.CharacterClass?.Name.ValueInNeutralLanguage ?? string.Empty,
        freePoints = character.LevelUpPoints,
        zen = character.Inventory?.Money ?? 0,
        stats = ResetApplier.IncreasableStats(character).Where(s => ResetApplier.StatKey(s.Attribute!) is not null)
            .ToDictionary(s => ResetApplier.StatKey(s.Attribute!)!, s => (int)Get(character, s.Attribute!)),
        busy,
        state = ResetStateWriter.State(engine, SubjectOf(account, character), DateTime.UtcNow, names),
    };

    private static ResetSubject SubjectOf(Account account, Character character)
    {
        var items = character.Inventory?.Items.ToList() ?? new List<Item>();
        return new ResetSubject
        {
            CharacterId = character.Id.ToString(),
            Name = character.Name,
            Login = account.LoginName,
            Class = character.CharacterClass?.Number ?? 0,
            Level = (int)Get(character, Stats.Level),
            Resets = (int)Get(character, Stats.Resets),
            Zen = character.Inventory?.Money ?? 0,
            Coins = ResetApplier.CoinsOf(account),
            VipLevel = (int)account.GetAccountAttributeValue(Stats.IsVip, DateTime.UtcNow),
            CountItems = cost => ResetApplier.Count(items, cost),
        };
    }

    private static float Get(Character character, AttributeDefinition attribute) =>
        character.Attributes.FirstOrDefault(a => a.Definition?.Id == attribute.Id)?.Value ?? 0;

    private static void Set(IContext context, Character character, AttributeDefinition attribute, float value)
    {
        if (character.Attributes.FirstOrDefault(a => a.Definition?.Id == attribute.Id) is { } existing)
        {
            existing.Value = value;
        }
        else
        {
            character.Attributes.Add(context.CreateNew<StatAttribute>(attribute, value));
        }
    }

    private static string ClearStatsPaid(WebsiteResetOptions options, IContext context, Account account, Character character)
    {
        if (ResetApplier.IncreasableStats(character).All(s => Get(character, s.Attribute!) <= s.BaseValue))
        {
            return "NothingToClear";
        }

        var zen = Math.Max(0, options.ClearStatsZen);
        if ((character.Inventory?.Money ?? 0) < zen)
        {
            return nameof(ResetResult.NotEnoughZen);
        }

        var plan = new ResetPlan();
        if (options.ClearStatsCoins > 0)
        {
            plan.Coins.Add(new ResetCoinCost(options.ClearStatsCoinType, options.ClearStatsCoins));
        }

        if (!ResetApplier.TryTakeCoins(account, plan))
        {
            return nameof(ResetResult.NotEnoughCoins);
        }

        if (zen > 0)
        {
            character.Inventory!.Money -= zen;
        }

        ResetApplier.ClearStats(character, a => Get(character, a), (a, v) => Set(context, character, a, v));
        return nameof(ResetResult.Ok);
    }

    private static async Task<Account?> AuthenticateAsync(IPlayerContext context, string? loginName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrEmpty(password) || loginName.Length > 10 || password.Length > 20)
        {
            return null;
        }

        var account = await context.GetAccountByLoginNameAsync(loginName.Trim(), password, cancellationToken).ConfigureAwait(false);
        return account is null || account.IsBot || account.State is AccountState.Banned or AccountState.TemporarilyBanned ? null : account;
    }

    private static void Save(ResetEngine engine)
    {
        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }
    }

    private async Task<(string Result, object? Plan)> ResetAsync(ResetEngine engine, IContext context, Account account, Character character, bool relife, Func<short, short, string?> names)
    {
        var subject = SubjectOf(account, character);
        var now = DateTime.UtcNow;
        var plan = relife ? engine.PlanRelife(subject, now) : engine.PlanReset(subject, now, ResetSource.Website);
        if (plan.Result != ResetResult.Ok)
        {
            return (plan.Result.ToString(), ResetStateWriter.Plan(plan, names));
        }

        var items = ResetApplier.PickItems(character.Inventory?.Items ?? Enumerable.Empty<Item>(), plan);
        if (items is null)
        {
            return (nameof(ResetResult.NotEnoughItems), ResetStateWriter.Plan(plan, names));
        }

        if (plan.Zen > (character.Inventory?.Money ?? 0))
        {
            return (nameof(ResetResult.NotEnoughZen), ResetStateWriter.Plan(plan, names));
        }

        if (!ResetApplier.TryTakeCoins(account, plan))
        {
            return (nameof(ResetResult.NotEnoughCoins), ResetStateWriter.Plan(plan, names));
        }

        if (plan.Zen > 0)
        {
            character.Inventory!.Money -= (int)plan.Zen;
        }

        foreach (var item in items)
        {
            character.Inventory!.Items.Remove(item);
            await context.DeleteAsync(item).ConfigureAwait(false);
        }

        TopRacesPlugIn.ObserveCharacter(null, subject.CharacterId, subject.Name, subject.Login, subject.Class, subject.Level, subject.Resets);
        ResetApplier.Apply(character, plan, false, a => Get(character, a), (a, v) => Set(context, character, a, v));
        if (engine.Configuration.MoveHome && character.CharacterClass?.HomeMap is { } home && home.ExitGates.FirstOrDefault(g => g.IsSpawnGate) is { } gate)
        {
            character.CurrentMap = home;
            character.PositionX = (byte)((gate.X1 + gate.X2) / 2);
            character.PositionY = (byte)((gate.Y1 + gate.Y2) / 2);
        }

        engine.Record(subject, plan, now, ResetSource.Website);
        TopRacesPlugIn.ObserveCharacter(null, subject.CharacterId, subject.Name, subject.Login, subject.Class, plan.LevelAfter, plan.ResetsAfter);
        return (nameof(ResetResult.Ok), ResetStateWriter.Plan(plan, names));
    }

    private async Task<IActionResult> AnswerAsync(ResetEngine engine, GameConfiguration configuration, string login, string characterName, string result, object? plan, Func<short, short, string?> names, CancellationToken cancellationToken)
    {
        using var context = persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(login, cancellationToken).ConfigureAwait(false);
        var character = account?.Characters.FirstOrDefault(c => c.Name == characterName);
        var busy = await this.BusyAsync(login).ConfigureAwait(false);
        return this.Content(ResetStateWriter.Serialize(new
        {
            v = 1,
            result,
            plan,
            character = account is null || character is null ? null : Character(engine, account, character, busy, names),
        }), "application/json");
    }

    /// <summary>
    /// Whether the account is busy: "online" (in the game) or "offlineLeveling"; <c>null</c> when it's offline.
    /// </summary>
    private async Task<string?> BusyAsync(string login)
    {
        try
        {
            var snapshot = await loginServer.GetSnapshotAsync().ConfigureAwait(false);
            if (snapshot.Keys.Any(k => string.Equals(k, login, StringComparison.OrdinalIgnoreCase)))
            {
                return "online";
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The login server's snapshot wasn't read.");
        }

        foreach (var context in serverProvider.Servers.OfType<IGameServerContextProvider>().Select(s => s.Context).Concat(ResetOptionsPlugIn.KnownContexts()).Distinct())
        {
            if (context.OfflinePlayerManager.IsActive(login))
            {
                return "offlineLeveling";
            }

            var players = await context.GetPlayersAsync().ConfigureAwait(false);
            if (players.Any(p => p is not OfflinePlayer && !p.IsTemplatePlayer && string.Equals(p.Account?.LoginName, login, StringComparison.OrdinalIgnoreCase)))
            {
                return "online";
            }
        }

        return null;
    }

    private async Task<ResetEngine?> EngineAsync(CancellationToken cancellationToken)
    {
        var configuration = await PlugInSettings.ReadActiveAsync<ResetOptionsConfiguration>(persistence, typeof(ResetOptionsPlugIn), () => new ResetOptionsConfiguration(), cancellationToken).ConfigureAwait(false);
        return configuration is null ? null : new ResetEngine(configuration, ResetStore.Open(configuration.FileName));
    }
}

/// <summary>
/// An account's login for the website's resets.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
public sealed record ResetAccountRequest(string? LoginName, string? Password);

/// <summary>
/// An action of the website's resets.
/// </summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Password">The password.</param>
/// <param name="Character">The character.</param>
/// <param name="Action">"reset", "relife", "add-stats", "clear-stats", "delegated-on" or "delegated-off".</param>
/// <param name="Str">The points added to strength.</param>
/// <param name="Agi">The points added to agility.</param>
/// <param name="Vit">The points added to vitality.</param>
/// <param name="Ene">The points added to energy.</param>
/// <param name="Cmd">The points added to command.</param>
public sealed record ResetActionRequest(string? LoginName, string? Password, string? Character, string? Action, int? Str = null, int? Agi = null, int? Vit = null, int? Ene = null, int? Cmd = null);
