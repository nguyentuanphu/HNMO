// <copyright file="ServerSettingsOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;

/// <summary>
/// Plain-language admin operations for managing game server rates (EXP / Drop / Zen),
/// PvP, player limit, and VIP-only access requirements (<c>Stats.IsVip</c>).
/// </summary>
/// <param name="persistence">Persistence context provider.</param>
/// <param name="servers">Running servers registry.</param>
/// <param name="executor">Authorization and audit executor.</param>
public sealed class ServerSettingsOperations(
    IPersistenceContextProvider persistence,
    IServerProvider servers,
    AdminOperationExecutor executor)
{
    /// <summary>Loads editable settings for all configured game servers.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sorted server settings list.</returns>
    [NotExposedToAgents("Admin panel Servers page UI only.")]
    public async ValueTask<IReadOnlyList<ServerSettingsModel>> LoadAsync(CancellationToken cancellationToken)
    {
        await executor.RequireAsync(AdminRoles.Administrator, cancellationToken).ConfigureAwait(false);
        using var context = persistence.CreateNewContext();
        var definitions = await context.GetAsync<GameServerDefinition>(cancellationToken).ConfigureAwait(false);
        var rateStore = GameServerRateStore.OpenDefault();
        var runningById = servers.Servers
            .OfType<IGameServer>()
            .ToDictionary(s => (byte)s.Id, s => s);

        return definitions
            .OrderBy(d => d.ServerID)
            .Select(d =>
            {
                var (dropRate, zenRate) = rateStore.GetRates(d.ServerID);
                runningById.TryGetValue(d.ServerID, out var running);
                return new ServerSettingsModel
                {
                    ServerId = d.ServerID,
                    Description = d.Description,
                    VipOnly = GameServerRateStore.IsVipOnly(d),
                    ExperienceRate = d.ExperienceRate > 0 ? d.ExperienceRate : 1.0f,
                    DropRate = dropRate,
                    ZenRate = zenRate,
                    PvpEnabled = d.PvpEnabled,
                    MaximumPlayers = d.ServerConfiguration?.MaximumPlayers ?? 100,
                    OnlinePlayers = running?.CurrentConnections ?? 0,
                };
            })
            .ToList();
    }

    /// <summary>Updates a game server's plain settings, VIP-only requirement, and per-server rates.</summary>
    /// <param name="model">Updated settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated persisted snapshot.</returns>
    [NotExposedToAgents("Admin panel Servers page UI only.")]
    public ValueTask<ServerSettingsModel> SaveAsync(ServerSettingsModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        return executor.ExecuteAsync(
            AdminRoles.Administrator,
            "servers.saveSettings",
            new
            {
                serverId = model.ServerId,
                description = model.Description,
                vipOnly = model.VipOnly,
                experienceRate = model.ExperienceRate,
                dropRate = model.DropRate,
                zenRate = model.ZenRate,
                pvpEnabled = model.PvpEnabled,
                maximumPlayers = model.MaximumPlayers,
            },
            async _ =>
            {
                var desc = string.IsNullOrWhiteSpace(model.Description) ? $"Server {model.ServerId + 1}" : model.Description.Trim();
                var exp = float.IsFinite(model.ExperienceRate) ? Math.Clamp(model.ExperienceRate, 0.1f, 1000.0f) : 1.0f;
                var drop = float.IsFinite(model.DropRate) ? Math.Clamp(model.DropRate, 0.1f, 50.0f) : 1.0f;
                var zen = float.IsFinite(model.ZenRate) ? Math.Clamp(model.ZenRate, 0.1f, 50.0f) : 1.0f;
                var maxPlayers = Math.Clamp(model.MaximumPlayers, (short)1, (short)5000);

                using var context = persistence.CreateNewContext();
                var definitions = await context.GetAsync<GameServerDefinition>(cancellationToken).ConfigureAwait(false);
                var def = definitions.FirstOrDefault(d => d.ServerID == model.ServerId)
                    ?? throw new InvalidOperationException($"Game server #{model.ServerId} was not found.");

                def.Description = desc;
                def.ExperienceRate = exp;
                def.PvpEnabled = model.PvpEnabled;
                if (def.ServerConfiguration is not null)
                {
                    def.ServerConfiguration.MaximumPlayers = maxPlayers;
                }

                await ApplyVipRequirementAsync(context, def, model.VipOnly, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync().ConfigureAwait(false);

                var savedRates = GameServerRateStore.OpenDefault().SetRates(model.ServerId, drop, zen);
                return new ServerSettingsModel
                {
                    ServerId = def.ServerID,
                    Description = def.Description,
                    VipOnly = GameServerRateStore.IsVipOnly(def),
                    ExperienceRate = def.ExperienceRate,
                    DropRate = savedRates.DropRate,
                    ZenRate = savedRates.ZenRate,
                    PvpEnabled = def.PvpEnabled,
                    MaximumPlayers = def.ServerConfiguration?.MaximumPlayers ?? maxPlayers,
                    OnlinePlayers = model.OnlinePlayers,
                };
            },
            cancellationToken);
    }

    private static async Task ApplyVipRequirementAsync(
        IContext context,
        GameServerDefinition definition,
        bool vipOnly,
        CancellationToken cancellationToken)
    {
        var existing = definition.JoinRequirements.Where(GameServerRateStore.IsVipRequirement).ToList();
        if (!vipOnly)
        {
            foreach (var item in existing)
            {
                definition.JoinRequirements.Remove(item);
            }

            return;
        }

        if (existing.Count > 0)
        {
            return;
        }

        var attrs = await context.GetAsync<AttributeDefinition>(cancellationToken).ConfigureAwait(false);
        var vipAttr = attrs.FirstOrDefault(a => a.Id == Stats.IsVip.Id);
        if (vipAttr is null)
        {
            var gameConfigs = await context.GetAsync<GameConfiguration>(cancellationToken).ConfigureAwait(false);
            var gc = definition.GameConfiguration ?? gameConfigs.FirstOrDefault();
            if (gc is not null)
            {
                vipAttr = gc.Attributes.FirstOrDefault(a => a.Id == Stats.IsVip.Id);
            }
        }

        if (vipAttr is null)
        {
            vipAttr = context.CreateNew<AttributeDefinition>(Stats.IsVip.Id, Stats.IsVip.Designation, Stats.IsVip.Description);
        }

        var req = context.CreateNew<AttributeRequirement>();
        req.Attribute = vipAttr;
        req.MinimumValue = 1;
        definition.JoinRequirements.Add(req);
    }
}
