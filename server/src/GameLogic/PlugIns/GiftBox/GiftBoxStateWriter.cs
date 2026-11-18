// <copyright file="GiftBoxStateWriter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>Projects a compact owned inbox and one selected letter without private host or delivery-proof metadata.</summary>
public static class GiftBoxStateWriter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Reads safe mailbox display fields without collecting or changing an account.</summary>
    /// <param name="dispatch">The immutable mailbox.</param>
    /// <param name="game">The canonical item definitions.</param>
    /// <param name="context">The authenticated account's context.</param>
    /// <param name="account">The authenticated account; never selected by a client-supplied identifier.</param>
    /// <param name="request">The validated bounded selection.</param>
    /// <param name="nowUtc">The authoritative UTC instant.</param>
    /// <param name="status">The collection outcome, if any.</param>
    /// <returns>Bounded JSON suitable for both the website and a C2 game packet.</returns>
    public static async ValueTask<string> WriteAsync(GiftBoxDispatch dispatch, GameConfiguration game, IPlayerContext context, Account account, GiftBoxRequest request, DateTime nowUtc, string? status = null)
    {
        if (!request.IsValid || account.GetId() == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The gift box display selection is invalid.");
        }

        var vietnamese = request.Language == "vi";
        var page = dispatch.ReadPage(account.GetId(), request.Offset, 21);
        var summaries = new List<object>();
        foreach (var letter in page.Take(20))
        {
            var proof = await ReadProofAsync(context, account, letter.Id).ConfigureAwait(false);
            summaries.Add(new
            {
                letter.Id,
                title = Clip(vietnamese ? letter.TitleVi : letter.TitleEn, 100),
                letter.SentAtUtc,
                letter.ExpiresAtUtc,
                collected = proof is not null,
                expired = letter.IsExpired(nowUtc),
                pieces = letter.Reward.Items.Sum(item => item.Count),
                letter.Reward.Coins,
                letter.Reward.Zen,
                coinType = letter.Reward.CoinType.ToString(),
            });
        }

        object? detail = null;
        if (request.LetterId is { } selected && dispatch.ReadLetter(account.GetId(), selected) is { } found)
        {
            var proof = await ReadProofAsync(context, account, selected).ConfigureAwait(false);
            var culture = CultureInfo.GetCultureInfo(request.Language);
            detail = new
            {
                found.Id,
                title = Clip(vietnamese ? found.TitleVi : found.TitleEn, 100),
                message = Clip(vietnamese ? found.MessageVi : found.MessageEn, 2000),
                found.SentAtUtc,
                found.ExpiresAtUtc,
                collected = proof is not null,
                expired = found.IsExpired(nowUtc),
                collectedBy = proof is null ? null : account.Characters.FirstOrDefault(character => character.GetId() == proof.CharacterId) is { } receiver ? Clip(receiver.Name, 10) : null,
                found.Reward.Coins,
                found.Reward.Zen,
                coinType = found.Reward.CoinType.ToString(),
                items = found.Reward.Items.Select(item =>
                {
                    var definition = game.Items.FirstOrDefault(value => value.Group == item.Group && value.Number == item.Number);
                    return new
                    {
                        item.Group,
                        item.Number,
                        name = Clip(definition?.Name.GetTranslation(culture) ?? $"{item.Group}:{item.Number}", 60),
                        item.Level,
                        item.Count,
                        item.Durability,
                        item.Skill,
                        item.Luck,
                        item.OptionLevel,
                        item.ExcellentOptions,
                    };
                }).ToArray(),
            };
        }

        var json = JsonSerializer.Serialize(new
        {
            accountKey = account.LoginName,
            request.Offset,
            hasMore = page.Count > 20,
            letters = summaries,
            detail,
            characters = account.Characters.OrderBy(character => character.CharacterSlot).Select(character => new
            {
                id = character.GetId(),
                name = Clip(character.Name, 10),
            }).ToArray(),
            status = GiftBoxCollection.HasUnconfirmed(context) ? "Pending" : status,
        }, Options);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 60000)
        {
            throw new InvalidOperationException("The gift box display exceeded its packet budget.");
        }

        return json;
    }

    private static string Clip(string text, int length) => text.Length <= length ? text : text[..length];

    private static async ValueTask<GiftBoxReceipt?> ReadProofAsync(IPlayerContext context, Account account, Guid letter)
    {
        if (GiftBoxCollection.IsUnconfirmed(context, letter))
        {
            return null;
        }

        var row = await context.GetCashShopCoinGrantByReferenceAsync($"mail:{account.GetId():N}:{letter:N}").ConfigureAwait(false);
        return row is not null && GiftBoxCollection.ReadReceipt(row) is { } proof && proof.AccountId == account.GetId() && proof.LetterId == letter ? proof : null;
    }
}
