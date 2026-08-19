#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed record TraitAttributeModifierImportModel(
    string AttributeId,
    string Mode,
    int Value,
    int ValuePerRank,
    string SourceType,
    string SourceId
);

internal sealed record TraitDamageResistanceEntryImportModel(
    string DamageTag,
    string MitigationTier
);

internal sealed record TraitSaveBonusEntryImportModel(string SaveAbility, int Bonus);

internal sealed class TraitPassiveStatusEffectImportModel
{
    internal TraitPassiveStatusEffectImportModel(
        string statusId,
        int power,
        int stacks,
        string displayLabel,
        bool undispellable,
        bool countsAsDebuffOverride,
        bool countsAsDebuff,
        IReadOnlyList<string> saveImmunityTags
    )
    {
        StatusId = RequireString(statusId, nameof(statusId));
        Power = power;
        Stacks = stacks;
        DisplayLabel = RequireString(displayLabel, nameof(displayLabel));
        Undispellable = undispellable;
        CountsAsDebuffOverride = countsAsDebuffOverride;
        CountsAsDebuff = countsAsDebuff;
        SaveImmunityTags = Freeze(saveImmunityTags, nameof(saveImmunityTags));
    }

    internal string StatusId { get; }
    internal int Power { get; }
    internal int Stacks { get; }
    internal string DisplayLabel { get; }
    internal bool Undispellable { get; }
    internal bool CountsAsDebuffOverride { get; }
    internal bool CountsAsDebuff { get; }
    internal IReadOnlyList<string> SaveImmunityTags { get; }

    private static string RequireString(string value, string name) =>
        value ?? throw new ArgumentNullException(name);

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}

internal sealed class TraitRollValueSchemaEntryImportModel
{
    internal TraitRollValueSchemaEntryImportModel(
        string key,
        string valueType,
        int minValue,
        int maxValue,
        IReadOnlyList<string> allowedValues
    )
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        MinValue = minValue;
        MaxValue = maxValue;
        AllowedValues = new ReadOnlyCollection<string>(
            new List<string>(allowedValues ?? throw new ArgumentNullException(nameof(allowedValues)))
        );
    }

    internal string Key { get; }
    internal string ValueType { get; }
    internal int MinValue { get; }
    internal int MaxValue { get; }
    internal IReadOnlyList<string> AllowedValues { get; }
}

internal sealed class TraitImportModel
{
    internal TraitImportModel(
        string traitId,
        string displayName,
        string description,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> allowedSourceKinds,
        string effectType,
        string triggerType,
        string stackPolicy,
        string chargeScope,
        string chargeResetTiming,
        string highestRollCompareKey,
        int visionRange,
        int proficiencyChoiceCount,
        IReadOnlyList<TraitAttributeModifierImportModel> attributeModifiers,
        IReadOnlyList<string> saveAdvantageTags,
        IReadOnlyList<string> saveDisadvantageTags,
        IReadOnlyList<string> saveImmunityTags,
        IReadOnlyList<TraitDamageResistanceEntryImportModel> damageResistanceEntries,
        IReadOnlyList<TraitSaveBonusEntryImportModel> saveBonusEntries,
        IReadOnlyList<TraitPassiveStatusEffectImportModel> passiveStatusEffects,
        IReadOnlyList<TraitRollValueSchemaEntryImportModel> rollValueSchema
    )
    {
        TraitId = RequireString(traitId, nameof(traitId));
        DisplayName = RequireString(displayName, nameof(displayName));
        Description = RequireString(description, nameof(description));
        Categories = Freeze(categories, nameof(categories));
        AllowedSourceKinds = Freeze(allowedSourceKinds, nameof(allowedSourceKinds));
        EffectType = RequireString(effectType, nameof(effectType));
        TriggerType = RequireString(triggerType, nameof(triggerType));
        StackPolicy = RequireString(stackPolicy, nameof(stackPolicy));
        ChargeScope = RequireString(chargeScope, nameof(chargeScope));
        ChargeResetTiming = RequireString(chargeResetTiming, nameof(chargeResetTiming));
        HighestRollCompareKey = RequireString(
            highestRollCompareKey,
            nameof(highestRollCompareKey)
        );
        VisionRange = visionRange;
        ProficiencyChoiceCount = proficiencyChoiceCount;
        AttributeModifiers = Freeze(attributeModifiers, nameof(attributeModifiers));
        SaveAdvantageTags = Freeze(saveAdvantageTags, nameof(saveAdvantageTags));
        SaveDisadvantageTags = Freeze(saveDisadvantageTags, nameof(saveDisadvantageTags));
        SaveImmunityTags = Freeze(saveImmunityTags, nameof(saveImmunityTags));
        DamageResistanceEntries = Freeze(
            damageResistanceEntries,
            nameof(damageResistanceEntries)
        );
        SaveBonusEntries = Freeze(saveBonusEntries, nameof(saveBonusEntries));
        PassiveStatusEffects = Freeze(passiveStatusEffects, nameof(passiveStatusEffects));
        RollValueSchema = Freeze(rollValueSchema, nameof(rollValueSchema));
    }

    internal string TraitId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal IReadOnlyList<string> Categories { get; }
    internal IReadOnlyList<string> AllowedSourceKinds { get; }
    internal string EffectType { get; }
    internal string TriggerType { get; }
    internal string StackPolicy { get; }
    internal string ChargeScope { get; }
    internal string ChargeResetTiming { get; }
    internal string HighestRollCompareKey { get; }
    internal int VisionRange { get; }
    internal int ProficiencyChoiceCount { get; }
    internal IReadOnlyList<TraitAttributeModifierImportModel> AttributeModifiers { get; }
    internal IReadOnlyList<string> SaveAdvantageTags { get; }
    internal IReadOnlyList<string> SaveDisadvantageTags { get; }
    internal IReadOnlyList<string> SaveImmunityTags { get; }
    internal IReadOnlyList<TraitDamageResistanceEntryImportModel> DamageResistanceEntries { get; }
    internal IReadOnlyList<TraitSaveBonusEntryImportModel> SaveBonusEntries { get; }
    internal IReadOnlyList<TraitPassiveStatusEffectImportModel> PassiveStatusEffects { get; }
    internal IReadOnlyList<TraitRollValueSchemaEntryImportModel> RollValueSchema { get; }

    private static string RequireString(string value, string name) =>
        value ?? throw new ArgumentNullException(name);

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}
