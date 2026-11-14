// <copyright file="CashShopCoinGrantsAdminOperationPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Administration;

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.Views.CashShop;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// The live administration operation which applies the pending cash shop coin grants of an account which is in the
/// game at once, instead of the next time the player opens the cash shop.
/// </summary>
/// <remarks>
/// The admin panel calls it after it granted coins to an account which is online. When the cash shop of the player is
/// open, the new balance is shown right away.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.CashShopCoinGrantsAdminOperationPlugIn_Name), Description = nameof(PlugInResources.CashShopCoinGrantsAdminOperationPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5C8E3A61-0B7F-4D92-A4E6-19F2C7B83D05")]
public sealed class CashShopCoinGrantsAdminOperationPlugIn : IAdminOperationPlugIn
{
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public const string OperationName = "cash_shop_apply_grants";

    /// <inheritdoc />
    public string Key => OperationName;

    /// <inheritdoc />
    public AdminPluginOperationInfo Operation => new(
        typeof(CashShopCoinGrantsAdminOperationPlugIn).GUID,
        OperationName,
        "Applies the pending cash shop coin grants of an account which is in the game, so that its balance changes at once.",
        "Operator",
        ReadOnly: false,
        Destructive: false,
        Idempotent: true,
        new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("loginName"),
            ["properties"] = new JsonObject
            {
                ["loginName"] = new JsonObject { ["type"] = "string", ["maxLength"] = 10 },
            },
        });

    /// <inheritdoc />
    public async ValueTask<JsonObject> PreviewAsync(IGameContext context, JsonObject arguments, CancellationToken cancellationToken)
    {
        var player = await FindPlayerAsync(context, GetLoginName(arguments)).ConfigureAwait(false);
        return new JsonObject
        {
            ["loginName"] = player.Account!.LoginName,
            ["characterName"] = player.SelectedCharacter?.Name,
        };
    }

    /// <inheritdoc />
    public async ValueTask<JsonObject> ExecuteAsync(IGameContext context, JsonObject arguments, JsonObject? expectedPreview, CancellationToken cancellationToken)
    {
        var player = await FindPlayerAsync(context, GetLoginName(arguments)).ConfigureAwait(false);
        await player.ApplyPendingCashShopCoinGrantsAsync().ConfigureAwait(false);
        var account = player.Account!;
        if (player.IsCashShopOpen)
        {
            await player.InvokeViewPlugInAsync<ICashShopViewPlugIn>(p => p.ShowPointsAsync(account)).ConfigureAwait(false);
        }

        return new JsonObject
        {
            ["loginName"] = account.LoginName,
            ["wCoinC"] = account.WCoinC,
            ["wCoinP"] = account.WCoinP,
            ["goblinPoints"] = account.GoblinPoints,
        };
    }

    private static string GetLoginName(JsonObject arguments)
    {
        try
        {
            return arguments["loginName"]?.GetValue<string>() is { Length: > 0 and <= 10 } loginName
                ? loginName
                : throw new ArgumentException("Give the loginName of the account.", nameof(arguments));
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException("Give the loginName of the account.", nameof(arguments), ex);
        }
    }

    private static async ValueTask<Player> FindPlayerAsync(IGameContext context, string loginName)
    {
        var players = await context.GetPlayersAsync().ConfigureAwait(false);
        return players.FirstOrDefault(p => p is not OfflinePlayer
                                           && p.IsConnected
                                           && p.PlayerState.CurrentState == PlayerState.EnteredWorld
                                           && string.Equals(p.Account?.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException("The account is not in the game on this server.", nameof(loginName));
    }
}
