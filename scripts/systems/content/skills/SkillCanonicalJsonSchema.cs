#nullable enable

using System;
using System.Collections.Generic;

internal static partial class SkillCanonicalJsonSchema
{
    private enum ContingencyBindingShape
    {
        Boolean,
        Integer,
        FloatingPoint,
        String,
        StringArray,
    }

    private static readonly ContentCanonicalJsonValueSchema<string> Text =
        ContentCanonicalJsonValue.Text;
    private static readonly ContentCanonicalJsonValueSchema<SkillImportIdentifier> Identifier =
        ContentCanonicalJsonValue.StableBusinessString<SkillImportIdentifier>(
            static value => value.Value
        );
    private static readonly ContentCanonicalJsonValueSchema<SkillImportStringName> StringName =
        ContentCanonicalJsonValue.StableBusinessString<SkillImportStringName>(
            static value => value.Value
        );
    private static readonly ContentCanonicalJsonValueSchema<SkillImportAssetId> AssetId =
        ContentCanonicalJsonValue.StableBusinessString<SkillImportAssetId>(
            static value => value.Value
        );
    private static readonly ContentCanonicalJsonValueSchema<SkillImportStatId> StatId =
        ContentCanonicalJsonValue.StableBusinessString<SkillImportStatId>(
            static value => value.Value
        );
    private static readonly SkillImportAssetId EmptyAssetId = SkillImportAssetId.FromRaw("");
    private static readonly SkillImportStatId EmptyStatId = CreateStatId("");

    private static readonly ContentCanonicalJsonValueSchema<
        IReadOnlyDictionary<string, string>
    > StringMap = ContentCanonicalJsonValue.OrderedObjectMap<
        IReadOnlyDictionary<string, string>,
        string,
        string
    >(
        static values => values,
        static key => key,
        StringComparer.Ordinal,
        Text
    );

    private static readonly ContentCanonicalJsonValueSchema<SkillDescriptionVariables>
        DescriptionVariables = ContentCanonicalJsonValue.Project<
            SkillDescriptionVariables,
            IReadOnlyDictionary<string, string>
        >(static value => value, StringMap);

    private static readonly ContentCanonicalJsonValueSchema<AttributeModifierImportModel>
        AttributeModifier = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<AttributeModifierImportModel>(
                ContentCanonicalJsonProperty<AttributeModifierImportModel>.Optional(
                    "attribute_id", static value => value.AttributeId, CreateName(""),
                    StringName
                ),
                ContentCanonicalJsonProperty<AttributeModifierImportModel>.Optional(
                    "mode", static value => value.Mode, AttributeModifierImportMode.Flat,
                    ContentCanonicalJsonValue.StableBusinessString<AttributeModifierImportMode>(
                        SkillRootCombatImportValueRules.GetWireValue
                    )
                ),
                ContentCanonicalJsonProperty<AttributeModifierImportModel>.Optional(
                    "value", static value => value.Value, 0, ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<AttributeModifierImportModel>.Optional(
                    "value_per_rank", static value => value.ValuePerRank, 0,
                    ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<AttributeModifierImportModel>.Optional(
                    "source_type", static value => value.SourceType, CreateName(""), StringName
                ),
                ContentCanonicalJsonProperty<AttributeModifierImportModel>.Optional(
                    "source_id", static value => value.SourceId, CreateName(""), StringName
                )
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<
        ContingencyParameterBindingImportValue
    > ContingencyBinding = ContentCanonicalJsonValue.ClosedUnion<
        ContingencyParameterBindingImportValue,
        ContingencyBindingShape
    >(
        GetContingencyBindingShape,
        ContentCanonicalJsonUnionCase<
            ContingencyParameterBindingImportValue,
            ContingencyBindingShape
        >.Create<ContingencyBoolBindingImportValue>(
            ContingencyBindingShape.Boolean,
            ContentCanonicalJsonValue.Project<ContingencyBoolBindingImportValue, bool>(
                static value => value.Value,
                ContentCanonicalJsonValue.Boolean
            )
        ),
        ContentCanonicalJsonUnionCase<
            ContingencyParameterBindingImportValue,
            ContingencyBindingShape
        >.Create<ContingencyIntBindingImportValue>(
            ContingencyBindingShape.Integer,
            ContentCanonicalJsonValue.Project<ContingencyIntBindingImportValue, long>(
                static value => value.Value,
                ContentCanonicalJsonValue.Int64
            )
        ),
        ContentCanonicalJsonUnionCase<
            ContingencyParameterBindingImportValue,
            ContingencyBindingShape
        >.Create<ContingencyFloatBindingImportValue>(
            ContingencyBindingShape.FloatingPoint,
            ContentCanonicalJsonValue.Project<ContingencyFloatBindingImportValue, double>(
                static value => value.Value,
                ContentCanonicalJsonValue.FloatingPointDouble
            )
        ),
        ContentCanonicalJsonUnionCase<
            ContingencyParameterBindingImportValue,
            ContingencyBindingShape
        >.Create<ContingencyStringBindingImportValue>(
            ContingencyBindingShape.String,
            ContentCanonicalJsonValue.Project<ContingencyStringBindingImportValue, string>(
                static value => value.Value,
                Text
            )
        ),
        ContentCanonicalJsonUnionCase<
            ContingencyParameterBindingImportValue,
            ContingencyBindingShape
        >.Create<ContingencyStringListBindingImportValue>(
            ContingencyBindingShape.StringArray,
            ContentCanonicalJsonValue.Project<
                ContingencyStringListBindingImportValue,
                IReadOnlyList<string>
            >(static value => value.Values, ContentCanonicalJsonValue.Array(Text))
        )
    );

    private static readonly ContentCanonicalJsonValueSchema<ContingencyAutomationImportModel>
        ContingencyAutomation = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ContingencyAutomationImportModel>(
                ContentCanonicalJsonProperty<ContingencyAutomationImportModel>.Optional(
                    "can_be_stored_in_contingency",
                    static value => value.CanBeStoredInContingency,
                    false,
                    ContentCanonicalJsonValue.Boolean
                ),
                ContentCanonicalJsonProperty<ContingencyAutomationImportModel>.Optional(
                    "min_contingency_skill_level",
                    static value => value.MinContingencySkillLevel,
                    1,
                    ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ContingencyAutomationImportModel>.Optional(
                    "effect_category", static value => value.EffectCategory, CreateName(""),
                    StringName
                ),
                OptionalList<ContingencyAutomationImportModel, SkillImportStringName>(
                    "tags", static value => value.Tags, StringName
                ),
                ContentCanonicalJsonProperty<ContingencyAutomationImportModel>.Optional(
                    "contingency_load_override",
                    static value => value.ContingencyLoadOverride,
                    0,
                    ContentCanonicalJsonValue.Int32
                ),
                OptionalList<ContingencyAutomationImportModel, SkillImportStringName>(
                    "allowed_target_resolvers",
                    static value => value.AllowedTargetResolvers,
                    StringName
                ),
                ContentCanonicalJsonProperty<ContingencyAutomationImportModel>.Optional(
                    "requires_manual_targeting",
                    static value => value.RequiresManualTargeting,
                    false,
                    ContentCanonicalJsonValue.Boolean
                ),
                OptionalMap<
                    ContingencyAutomationImportModel,
                    IReadOnlyDictionary<SkillImportIdentifier, ContingencyParameterBindingImportValue>,
                    SkillImportIdentifier,
                    ContingencyParameterBindingImportValue
                >(
                    "allowed_parameter_bindings",
                    static value => value.AllowedParameterBindings,
                    static values => values,
                    static key => key.Value,
                    StringComparer.Ordinal,
                    ContingencyBinding
                )
            )
        );

    internal static ContentCanonicalJsonObjectSchema<SkillImportModel> EntrySchema { get; } =
        new(
            ContentCanonicalJsonProperty<SkillImportModel>.Required(
                "skill_id", static value => value.SkillId, Identifier
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Required(
                "display_name", static value => value.DisplayName, Text
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "description", static value => value.Description, "", Text
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "skill_type", static value => value.SkillType, SkillImportType.Active,
                ContentCanonicalJsonValue.StableBusinessString<SkillImportType>(
                    SkillJsonImportValueRules.GetWireValue
                )
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "max_level", static value => value.MaxLevel, 1,
                ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "learn_source", static value => value.LearnSource, SkillImportLearnSource.Book,
                ContentCanonicalJsonValue.StableBusinessString<SkillImportLearnSource>(
                    SkillJsonImportValueRules.GetWireValue
                )
            ),
            OptionalList<SkillImportModel, SkillImportIdentifier>(
                "tags", static value => value.Tags, Identifier
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "level_description_template",
                static value => value.LevelDescriptionTemplate,
                "",
                Text
            ),
            OptionalMap<
                SkillImportModel,
                IReadOnlyDictionary<int, SkillDescriptionVariables>,
                int,
                SkillDescriptionVariables
            >(
                "level_description_configs",
                static value => value.LevelDescriptionConfigs,
                static values => values,
                ContentCanonicalJsonKey.InvariantInt32,
                ContentCanonicalJsonKey.InvariantInt32Order,
                DescriptionVariables
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional<CombatSkillImportModel>(
                "combat_profile",
                static value => value.CombatProfile!,
                null!,
                Combat
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "icon_id", static value => value.IconId, EmptyAssetId, AssetId
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "non_core_max_level", static value => value.NonCoreMaxLevel, 0,
                ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "dynamic_max_level_stat_id", static value => value.DynamicMaxLevelStatId,
                EmptyStatId, StatId
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "dynamic_max_level_base", static value => value.DynamicMaxLevelBase, 0,
                ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "dynamic_max_level_per_stat", static value => value.DynamicMaxLevelPerStat, 0,
                ContentCanonicalJsonValue.Int32
            ),
            OptionalList<SkillImportModel, int>(
                "mastery_curve", static value => value.MasteryCurve,
                ContentCanonicalJsonValue.Int32
            ),
            OptionalList<SkillImportModel, SkillImportIdentifier>(
                "learn_requirements", static value => value.LearnRequirements, Identifier
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "unlock_mode", static value => value.UnlockMode, SkillImportUnlockMode.Standard,
                ContentCanonicalJsonValue.StableBusinessString<SkillImportUnlockMode>(
                    SkillRootCombatImportValueRules.GetWireValue
                )
            ),
            OptionalList<SkillImportModel, SkillImportIdentifier>(
                "knowledge_requirements", static value => value.KnowledgeRequirements, Identifier
            ),
            IdentifierIntMap("skill_level_requirements", static value => value.SkillLevelRequirements),
            IdentifierIntMap("attribute_requirements", static value => value.AttributeRequirements),
            OptionalList<SkillImportModel, SkillImportIdentifier>(
                "achievement_requirements", static value => value.AchievementRequirements, Identifier
            ),
            OptionalList<SkillImportModel, SkillImportIdentifier>(
                "upgrade_source_skill_ids", static value => value.UpgradeSourceSkillIds, Identifier
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "retain_source_skills_on_unlock",
                static value => value.RetainSourceSkillsOnUnlock,
                true,
                ContentCanonicalJsonValue.Boolean
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional(
                "core_skill_transition_mode",
                static value => value.CoreSkillTransitionMode,
                SkillImportCoreSkillTransitionMode.Inherit,
                ContentCanonicalJsonValue.StableBusinessString<SkillImportCoreSkillTransitionMode>(
                    SkillRootCombatImportValueRules.GetWireValue
                )
            ),
            OptionalList<SkillImportModel, SkillImportIdentifier>(
                "mastery_sources", static value => value.MasterySources, Identifier
            ),
            OptionalOmittedEnum<SkillImportModel, SkillImportProgressionTier>(
                "growth_tier", static value => value.GrowthTier,
                SkillImportProgressionTier.None,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            IdentifierIntMap("attribute_growth_progress", static value => value.AttributeGrowthProgress),
            OptionalOmittedEnum<SkillImportModel, SkillImportProgressionTier>(
                "practice_tier", static value => value.PracticeTier,
                SkillImportProgressionTier.None,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            OptionalList<SkillImportModel, AttributeModifierImportModel>(
                "attribute_modifiers", static value => value.AttributeModifiers,
                AttributeModifier
            ),
            ContentCanonicalJsonProperty<SkillImportModel>.Optional<ContingencyAutomationImportModel>(
                "contingency_automation_profile",
                static value => value.ContingencyAutomationProfile!,
                null!,
                ContingencyAutomation
            )
        );

    private static ContingencyBindingShape GetContingencyBindingShape(
        ContingencyParameterBindingImportValue value
    ) => value switch
    {
        ContingencyBoolBindingImportValue => ContingencyBindingShape.Boolean,
        ContingencyIntBindingImportValue => ContingencyBindingShape.Integer,
        ContingencyFloatBindingImportValue => ContingencyBindingShape.FloatingPoint,
        ContingencyStringBindingImportValue => ContingencyBindingShape.String,
        ContingencyStringListBindingImportValue => ContingencyBindingShape.StringArray,
        _ => throw new ArgumentOutOfRangeException(
            nameof(value), value, "Unregistered contingency binding shape."
        ),
    };

    private static ContentCanonicalJsonProperty<SkillImportModel> IdentifierIntMap(
        string name,
        Func<SkillImportModel, IReadOnlyDictionary<SkillImportIdentifier, int>> getter
    ) => OptionalMap<
        SkillImportModel,
        IReadOnlyDictionary<SkillImportIdentifier, int>,
        SkillImportIdentifier,
        int
    >(
        name, getter, static values => values, static key => key.Value,
        StringComparer.Ordinal, ContentCanonicalJsonValue.Int32
    );

    private static ContentCanonicalJsonProperty<TObject> OptionalOmittedEnum<TObject, TEnum>(
        string name,
        Func<TObject, TEnum> getter,
        TEnum defaultValue,
        Func<TEnum, string> wire
    ) where TEnum : struct => ContentCanonicalJsonProperty<TObject>.Optional(
        name, getter, defaultValue,
        ContentCanonicalJsonValue.StableBusinessString<TEnum>(wire)
    );

    private static ContentCanonicalJsonProperty<TObject> OptionalList<TObject, TElement>(
        string name,
        Func<TObject, IReadOnlyList<TElement>> getter,
        ContentCanonicalJsonValueSchema<TElement> elementSchema
    ) => ContentCanonicalJsonProperty<TObject>.Optional(
        name,
        getter,
        Array.Empty<TElement>(),
        ContentCanonicalJsonValue.Array(elementSchema),
        EmptyReadOnlyListComparer<TElement>.Instance
    );

    private static ContentCanonicalJsonProperty<TObject> OptionalMap<
        TObject,
        TMap,
        TKey,
        TValue
    >(
        string name,
        Func<TObject, TMap> getter,
        Func<TMap, IEnumerable<KeyValuePair<TKey, TValue>>> entries,
        Func<TKey, string> key,
        IComparer<string> comparer,
        ContentCanonicalJsonValueSchema<TValue> valueSchema
    ) where TMap : class => ContentCanonicalJsonProperty<TObject>.Optional<TMap>(
        name,
        getter,
        null!,
        ContentCanonicalJsonValue.OrderedObjectMap<TMap, TKey, TValue>(
            entries, key, comparer, valueSchema
        ),
        EmptyReadOnlyMapComparer<TMap, TKey, TValue>.Instance
    );

    private sealed class EmptyReadOnlyListComparer<T> : IEqualityComparer<IReadOnlyList<T>>
    {
        internal static EmptyReadOnlyListComparer<T> Instance { get; } = new();
        public bool Equals(IReadOnlyList<T>? left, IReadOnlyList<T>? right) =>
            left != null && right != null && left.Count == 0 && right.Count == 0;
        public int GetHashCode(IReadOnlyList<T> value) => value?.Count ?? -1;
    }

    private sealed class ReadOnlyListSequenceComparer<T> : IEqualityComparer<IReadOnlyList<T>>
    {
        internal static ReadOnlyListSequenceComparer<T> Instance { get; } = new();

        public bool Equals(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;
            var comparer = EqualityComparer<T>.Default;
            for (int index = 0; index < left.Count; index += 1)
            {
                if (!comparer.Equals(left[index], right[index]))
                    return false;
            }
            return true;
        }

        public int GetHashCode(IReadOnlyList<T> value) => value?.Count ?? -1;
    }

    private sealed class EmptyReadOnlyMapComparer<TMap, TKey, TValue>
        : IEqualityComparer<TMap> where TMap : class
    {
        internal static EmptyReadOnlyMapComparer<TMap, TKey, TValue> Instance { get; } = new();

        public bool Equals(TMap? left, TMap? right)
        {
            if (left == null || right != null)
                return false;
            return left is IReadOnlyCollection<KeyValuePair<TKey, TValue>> collection
                && collection.Count == 0;
        }

        public int GetHashCode(TMap value) => value?.GetHashCode() ?? -1;
    }

    private static SkillImportStatId CreateStatId(string value)
    {
        if (!SkillImportStatId.TryCreate(value, out SkillImportStatId result))
            throw new InvalidOperationException($"Invalid canonical default stat ID '{value}'.");
        return result;
    }
}
