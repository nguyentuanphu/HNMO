// -----------------------------------------------------------------------
// <copyright file="ItemUpgradeConsumeHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace HNMO.GameLogic.PlayerActions.ItemConsumeActions;

using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// An item consume handler which upgrades the target item.
/// </summary>
/// <remarks>
/// The upgrade behaviour is configurable, see <see cref="ItemUpgradeConfiguration"/>.
/// A concrete handler defines its default configuration by the one which it passes to its constructor.
/// </remarks>
public abstract class ItemUpgradeConsumeHandlerPlugIn : ItemModifyConsumeHandlerPlugIn, ISupportCustomConfiguration<ItemUpgradeConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly ItemUpgradeConfiguration _defaultConfiguration;

    private ItemUpgradeConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemUpgradeConsumeHandlerPlugIn"/> class.
    /// </summary>
    /// <param name="configuration">The default configuration of the concrete handler.</param>
    internal ItemUpgradeConsumeHandlerPlugIn(ItemUpgradeConfiguration configuration)
    {
        this._defaultConfiguration = configuration;
        this._configuration = configuration;
    }

    /// <summary>
    /// Specifies what should happen with the item, if the upgrading failed randomly.
    /// </summary>
    public enum ItemFailResult
    {
        /// <summary>
        /// Nothing happens.
        /// </summary>
        None,

        /// <summary>
        /// Sets the option to the base level.
        /// </summary>
        SetOptionToBaseLevel,

        /// <summary>
        /// Removes the option.
        /// </summary>
        RemoveOption,
    }

    /// <inheritdoc />
    public ItemUpgradeConfiguration? Configuration
    {
        get => this._configuration;
        set => this._configuration = this.NormalizeConfiguration(value);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => this._defaultConfiguration.Clone();

    /// <summary>
    /// Gets the success chance with the boost of the configuration (<see cref="ItemUpgradeConfiguration.BoostOptionType"/>,
    /// <see cref="ItemUpgradeConfiguration.SuccessChanceBoost"/>), e.g. more chance for an item with luck.
    /// </summary>
    /// <param name="item">The item to upgrade.</param>
    /// <param name="successChance">The success chance without the boost.</param>
    /// <returns>The success chance, at most 1.</returns>
    internal double GetBoostedSuccessChance(Item item, double successChance)
    {
        if (this._configuration.BoostOptionType is not { } boostType || this._configuration.SuccessChanceBoost <= 0)
        {
            return successChance;
        }

        return item.ItemOptions.Any(o => o.ItemOption?.OptionType == boostType)
            ? Math.Min(1.0, successChance + this._configuration.SuccessChanceBoost)
            : successChance;
    }

    /// <inheritdoc/>
    protected override bool ModifyItem(Item item, IContext persistenceContext)
    {
        var (successChance, maxOptionLevel) = this._configuration.OptionType == ItemOptionTypes.Option
            ? PlugIns.GameplayRules.RateTuningPlugIn.GetEffectiveLifeOptionSettings(this.CurrentPlayer, this._configuration.SuccessChance)
            : (this._configuration.SuccessChance, null);
        return this.ModifyItemWithSettings(item, persistenceContext, this.GetBoostedSuccessChance(item, successChance), maxOptionLevel);
    }

    /// <summary>
    /// Checks if an item can have the configured option.
    /// </summary>
    /// <param name="item">The item to upgrade.</param>
    /// <returns>Flag indicating whether the item can have the option.</returns>
    /// <remarks>
    /// Only options with level-dependent values can be added and increased by a jewel.
    /// Other options of the same type, like the ones of the Horn of Dinorant, are fixed.
    /// </remarks>
    protected virtual bool ItemCanHaveOption(Item item)
    {
        return item.Definition?.PossibleItemOptions.Any(o => o.PossibleOptions.Any(this.IsUpgradableOption)) ?? false;
    }

    /// <summary>
    /// Tries to upgrade the item option.
    /// </summary>
    /// <param name="item">The item to upgrade.</param>
    /// <returns>Flag indicating whether the item option was upgraded.</returns>
    protected virtual bool TryUpgradeItemOption(Item item) =>
        this.TryUpgradeItemOptionWithSettings(item, this.GetBoostedSuccessChance(item, this._configuration.SuccessChance), null);

    private ItemUpgradeConfiguration NormalizeConfiguration(ItemUpgradeConfiguration? configuration)
    {
        if (configuration is null)
        {
            return this._defaultConfiguration;
        }

        // A stored configuration of an older shape may not contain the values which the handler needs.
        configuration.OptionType ??= this._defaultConfiguration.OptionType;
        if (!configuration.AddsOption && !configuration.IncreasesOption)
        {
            configuration.AddsOption = this._defaultConfiguration.AddsOption;
            configuration.IncreasesOption = this._defaultConfiguration.IncreasesOption;
        }

        return configuration;
    }

    private void HandleFailedUpgrade(Item item, ItemOptionLink itemOption)
    {
        switch (this._configuration.FailResult)
        {
            case ItemFailResult.RemoveOption:
                item.ItemOptions.Remove(itemOption);
                break;
            case ItemFailResult.SetOptionToBaseLevel:
                itemOption.Level = itemOption.ItemOption?.LevelDependentOptions.Min(ldo => ldo.Level) ?? itemOption.Level;
                break;
            default:
                // do nothing
                break;
        }
    }

    private bool TryAddItemOption(Item item, IContext persistenceContext) =>
        this.TryAddItemOptionWithSettings(item, persistenceContext, this._configuration.SuccessChance);

    private bool ModifyItemWithSettings(Item item, IContext persistenceContext, double successChance, int? maxOptionLevel)
    {
        if (!this.ItemCanHaveOption(item))
        {
            return false;
        }

        if (this.ItemHasOptionAlready(item))
        {
            return this.TryUpgradeItemOptionWithSettings(item, successChance, maxOptionLevel);
        }

        return this.TryAddItemOptionWithSettings(item, persistenceContext, successChance);
    }

    private bool TryUpgradeItemOptionWithSettings(Item item, double successChance, int? maxOptionLevel)
    {
        if (!this._configuration.IncreasesOption)
        {
            return false;
        }

        var itemOption = item.ItemOptions.First(o => o.ItemOption?.OptionType == this._configuration.OptionType);
        var increasableOption = itemOption.ItemOption;
        var higherOptionPossible = increasableOption?.LevelDependentOptions.Any(o =>
            o.Level > itemOption.Level
            && (maxOptionLevel is null || o.Level <= maxOptionLevel.Value)
            && o.RequiredItemLevel <= item.Level) ?? false;
        if (!higherOptionPossible)
        {
            return false;
        }

        if (Rand.NextRandomBool(successChance))
        {
            itemOption.Level++;
        }
        else
        {
            this.HandleFailedUpgrade(item, itemOption);
        }

        return true;
    }

    private bool TryAddItemOptionWithSettings(Item item, IContext persistenceContext, double successChance)
    {
        if (!this._configuration.AddsOption || item.Definition is null)
        {
            return false;
        }

        if (Rand.NextRandomBool(successChance))
        {
            var possibleOptions = item.Definition.PossibleItemOptions.
                SelectMany(o => o.PossibleOptions).
                Where(o => this.IsUpgradableOption(o)
                           && o.LevelDependentOptions.Any(ldo => ldo.RequiredItemLevel <= item.Level)).ToList();
            if (!possibleOptions.Any())
            {
                return false;
            }

            var optionLink = persistenceContext.CreateNew<ItemOptionLink>();
            if (this._configuration.OptionType == ItemOptionTypes.HarmonyOption)
            {
                // Str and agi reduction options are not always applicable, and so should be removed from the pool
                if (!item.Definition.Requirements.Any(r => r.Attribute == Stats.TotalStrengthRequirementValue)
                    && possibleOptions.FirstOrDefault(po => po.LevelDependentOptions
                        .Any(ldo => ldo.PowerUpDefinition?.TargetAttribute == Stats.RequiredStrengthReduction)) is { } strReductOpt)
                {
                    possibleOptions.Remove(strReductOpt);
                }

                if (!item.Definition.Requirements.Any(r => r.Attribute == Stats.TotalAgilityRequirementValue)
                    && possibleOptions.FirstOrDefault(po => po.LevelDependentOptions
                        .Any(ldo => ldo.PowerUpDefinition?.TargetAttribute == Stats.RequiredAgilityReduction)) is { } agiReductOpt)
                {
                    possibleOptions.Remove(agiReductOpt);
                }

                optionLink.ItemOption = possibleOptions.SelectWeightedRandom(possibleOptions.Select(po => (int)po.Weight));
                optionLink.Level = optionLink.ItemOption?.LevelDependentOptions.Select(ldo => ldo.Level).Min() ?? 0;
            }
            else
            {
                // ItemOptionTypes.Option
                optionLink.ItemOption = possibleOptions.SelectRandom();
                optionLink.Level = 1;
            }

            item.ItemOptions.Add(optionLink);
        }

        return true;
    }

    private bool IsUpgradableOption(IncreasableItemOption option)
    {
        return option.OptionType == this._configuration.OptionType && option.LevelDependentOptions.Any();
    }

    private bool ItemHasOptionAlready(Item item)
    {
        return item.ItemOptions.Any(o => o.ItemOption?.OptionType == this._configuration.OptionType);
    }
}
