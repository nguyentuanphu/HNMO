// <copyright file="ShopEditorOperations.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Operations;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using Microsoft.Extensions.Logging;

/// <summary>Saves graphical merchant drafts through the existing configuration context and audited authorization.</summary>
/// <param name="source">The circuit's configuration source.</param>
/// <param name="executor">The caller and audit boundary.</param>
/// <param name="confirmation">Serializes administrative writes.</param>
/// <param name="logger">The save log.</param>
public sealed class ShopEditorOperations(IDataSource<GameConfiguration> source, AdminOperationExecutor executor, AdminChangeConfirmation confirmation, ILogger<ShopEditorOperations> logger)
{
    /// <summary>Validates a detached draft, checks for concurrent changes and persists only its item differences.</summary>
    /// <param name="merchantId">The merchant definition.</param>
    /// <param name="draft">The confirmed graphical draft.</param>
    /// <param name="cancellationToken">Cancels before persistence.</param>
    /// <returns>The applied summary.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    [NotExposedToAgents("A graphical shop draft with local undo and native confirmation has no stateless agent equivalent.")]
    public ValueTask<ShopChangeSummary> SaveAsync(Guid merchantId, ShopDraft draft, CancellationToken cancellationToken = default)
    {
        draft = draft.Fork();
        return executor.ExecuteAsync(
            AdminRoles.Administrator,
            "merchant_shop_save",
            new
            {
                merchantId,
                summary = draft.Summary,
            },
            async actor =>
        {
            if (actor.Frontend != "panel")
            {
                throw new AdminOperationException("This graphical operation is available only to the panel.", AdminOperationErrorKind.Forbidden);
            }

            using var gate = await confirmation.Gate.LockAsync(cancellationToken).ConfigureAwait(false);
            await source.GetOwnerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var context = await source.GetContextAsync(cancellationToken).ConfigureAwait(false);
            var merchant = source.GetAll<MonsterDefinition>().SingleOrDefault(m => m.GetId() == merchantId);
            if (merchant?.MerchantStore is not { } storage || context.HasChanges || !draft.MatchesSource(storage))
            {
                throw new AdminOperationException("The shop or configuration changed. Reload before saving.", AdminOperationErrorKind.Conflict);
            }

            if (!draft.IsValid())
            {
                throw new AdminOperationException("The shop has invalid items, overlaps or out-of-bounds rectangles.", AdminOperationErrorKind.InvalidInput);
            }

            var summary = draft.Summary;
            if (!summary.HasChanges)
            {
                return summary;
            }

            var items = draft.Items.Select(i => ShopItemDraft.Copy(i)).ToArray();
            foreach (var item in items)
            {
                await ResolveAsync(context, item, cancellationToken).ConfigureAwait(false);
            }

            var resolvedStorage = new ShopDraftStorage();
            foreach (var item in items)
            {
                resolvedStorage.Items.Add(item);
            }

            if (!new ShopDraft(resolvedStorage).IsValid())
            {
                throw new AdminOperationException("Item definitions changed and no longer fit this draft.", AdminOperationErrorKind.Conflict);
            }

            try
            {
                await ApplyAsync(context, storage, items).ConfigureAwait(false);
                if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new AdminOperationException("The configuration save did not complete.", AdminOperationErrorKind.Unavailable);
                }
            }
            catch
            {
                await source.ForceDiscardChangesAsync().ConfigureAwait(false);
                throw;
            }

            logger.LogInformation("Merchant shop saved by {Actor} ({ActorId}): {Merchant} ({MerchantId}), added {Added}, removed {Removed}, moved {Moved}, updated {Updated}.", actor.Name, actor.Id, merchant.Designation.ValueInNeutralLanguage, merchantId, summary.Added, summary.Removed, summary.Moved, summary.Updated);
            return summary;
        },
            cancellationToken);
    }

    private static async ValueTask ResolveAsync(IContext context, ShopItemDraft item, CancellationToken token)
    {
        item.Definition = await context.GetByIdAsync<ItemDefinition>(item.Definition!.GetId(), token).ConfigureAwait(false)
            ?? throw new AdminOperationException("The item definition no longer exists.", AdminOperationErrorKind.Conflict);
        if (item.StorePriceItemDefinition is { } currency)
        {
            item.StorePriceItemDefinition = await context.GetByIdAsync<ItemDefinition>(currency.GetId(), token).ConfigureAwait(false)
                ?? throw new AdminOperationException("The currency definition no longer exists.", AdminOperationErrorKind.Conflict);
        }

        foreach (var option in item.ItemOptions)
        {
            option.ItemOption = await context.GetByIdAsync<IncreasableItemOption>(option.ItemOption!.GetId(), token).ConfigureAwait(false)
                ?? throw new AdminOperationException("An item option no longer exists.", AdminOperationErrorKind.Conflict);
        }

        var sets = item.ItemSetGroups.ToArray();
        item.ItemSetGroups.Clear();
        foreach (var set in sets)
        {
            item.ItemSetGroups.Add(await context.GetByIdAsync<ItemOfItemSet>(set.GetId(), token).ConfigureAwait(false)
                ?? throw new AdminOperationException("An item set no longer exists.", AdminOperationErrorKind.Conflict));
        }
    }

    private static async ValueTask ApplyAsync(IContext context, ItemStorage storage, IReadOnlyList<ShopItemDraft> items)
    {
        var desired = items.Select(i => i.Key).ToHashSet();
        foreach (var removed in storage.Items.Where(i => !desired.Contains(i.GetId())).ToArray())
        {
            storage.Items.Remove(removed);
            await context.DeleteAsync(removed).ConfigureAwait(false);
        }

        var existing = storage.Items.ToDictionary(i => i.GetId());
        foreach (var draft in items)
        {
            if (!existing.TryGetValue(draft.Key, out var target))
            {
                target = context.CreateNew<Item>();
                storage.Items.Add(target);
            }
            else if (ShopItemDraft.Copy(target).Signature() == draft.Signature())
            {
                continue;
            }

            foreach (var oldOption in target.ItemOptions.ToArray())
            {
                await context.DeleteAsync(oldOption).ConfigureAwait(false);
            }

            ShopItemDraft.CopyValues(draft, target, () => context.CreateNew<ItemOptionLink>());
        }
    }
}
