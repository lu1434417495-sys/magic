#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

public partial class run_equipment_ability_content_registry_regression : LifecycleTestSceneTree
{
    private const string TraitId = "trait.weapon.flame";
    private const string SkillId = "skill.triggered";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestBuiltInHandlerInventoryIsClosedAndResourceFree();
            TestJsonRebuildProjectsStableIds();
            TestIncompleteContextFailsClosed();
            TestFailedRebuildKeepsLastSuccessfulSnapshot();
            TestRuntimeVocabularyFailsClosed();
            TestWindupSkillContextSurvivesStatusExpansion();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected equipment ability registry exception: {exception}");
        }

        RequestTestExit(_test.Finish("Equipment ability content registry regression"));
    }

    private void TestBuiltInHandlerInventoryIsClosedAndResourceFree()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> conditions =
            registry.GetConditionHandlerSpecsTyped();
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> actions =
            registry.GetActionHandlerSpecsTyped();

        _test.Eq(conditions.Count, 3, "registry exposes the three closed condition handlers");
        _test.Eq(actions.Count, 26, "registry exposes the 26 executable action handlers");
        _test.False(actions.ContainsKey("grant_skill"), "grant_skill has no runtime handler");
        _test.True(conditions.ContainsKey("compare_fact"), "compare_fact remains registered");
        _test.True(actions.ContainsKey("trigger_skill"), "trigger_skill remains registered");
        _test.True(actions.ContainsKey("apply_status"), "apply_status remains registered");
        _test.True(
            typeof(EquipmentAbilityHandlerSpec).GetProperty(
                "PayloadResourceType",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            ) == null,
            "handler metadata no longer retains a Resource payload type"
        );

        foreach ((string kind, EquipmentAbilityPayloadKindSpec payload) in EquipmentAbilityPayloadKindCatalog.Conditions)
        {
            _test.True(conditions.ContainsKey(kind), $"condition handler covers JSON kind {kind}");
            _test.Eq(conditions[kind].PayloadJsonDtoType, payload.JsonDtoType, $"condition DTO matches {kind}");
            _test.Eq(conditions[kind].PayloadImportModelType, payload.ImportModelType, $"condition import matches {kind}");
        }
        foreach ((string kind, EquipmentAbilityPayloadKindSpec payload) in EquipmentAbilityPayloadKindCatalog.Actions)
        {
            _test.True(actions.ContainsKey(kind), $"action handler covers JSON kind {kind}");
            _test.Eq(actions[kind].PayloadJsonDtoType, payload.JsonDtoType, $"action DTO matches {kind}");
            _test.Eq(actions[kind].PayloadImportModelType, payload.ImportModelType, $"action import matches {kind}");
        }

        _test.False(
            Enum.GetNames<EquipmentAbilityTriggerKind>().Contains("OnBattleEnd", StringComparer.Ordinal),
            "ghost OnBattleEnd trigger is absent"
        );
    }

    private void TestJsonRebuildProjectsStableIds()
    {
        EquipmentAbilityContentPackImportModel pack = BuildPack();
        string json = EquipmentAbilityImportCanonicalJson.WriteDocument("registry-test", new[] { pack });
        using var registry = new EquipmentAbilityContentRegistry();
        EquipmentAbilityRegistryBuildResult result = registry.RebuildFromJson(
            "memory://equipment-abilities",
            new FakeSourceReader(new ContentJsonSourceText("pack.json", json)),
            BuildValidationContext()
        );

        _test.True(result.Success, $"plain JSON rebuild succeeds: {Format(result.Errors)}");
        _test.Eq(registry.GetPackDefinitionsTyped().Count, 1, "JSON rebuild publishes one pack");
        _test.Eq(registry.GetBindingDefinitionsTyped().Count, 1, "JSON rebuild publishes one binding");
        EquipmentAbilityBindingDefinition definition =
            registry.GetBindingDefinitionsTyped()["binding.weapon.flame"];
        _test.Eq(definition.BindingId.ToString(), "binding.weapon.flame", "binding id survives JSON projection");
        _test.Eq(definition.TraitId.ToString(), TraitId, "trait id survives JSON projection");
        _test.Eq(definition.Reactions.Count, 1, "reaction survives JSON projection");
        _test.Eq(definition.Reactions[0].Trigger, EquipmentAbilityTriggerKind.OnHit, "trigger projects to typed enum");
        _test.Eq(definition.Reactions[0].Timing, EquipmentAbilityTimingKind.AfterHit, "timing projects to typed enum");
        _test.True(
            definition.Reactions[0].Actions[0].PayloadDefinition
                is AddDamageDiceActionPayloadDefinition,
            "JSON payload projects to the immutable runtime definition"
        );
    }

    private void TestIncompleteContextFailsClosed()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        EquipmentAbilityRegistryBuildResult result = registry.Rebuild(
            Array.Empty<EquipmentAbilityContentPackImportModel>(),
            new EquipmentAbilityContentValidationContext()
        );
        _test.False(result.Success, "missing catalogs fail closed even for an empty import batch");
        AssertError(result.Errors, "EQA_VALIDATION_CONTEXT_INCOMPLETE", "equipment_ability.validation_context");
    }

    private void TestFailedRebuildKeepsLastSuccessfulSnapshot()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        EquipmentAbilityRegistryBuildResult first = registry.Rebuild(
            new[] { BuildPack() },
            BuildValidationContext()
        );
        _test.True(first.Success, $"baseline plain-import rebuild succeeds: {Format(first.Errors)}");
        EquipmentAbilityBindingDefinition snapshot =
            registry.GetBindingDefinitionsTyped()["binding.weapon.flame"];

        EquipmentAbilityRegistryBuildResult invalid = registry.Rebuild(
            new[] { BuildPack(traitId: "trait.missing") },
            BuildValidationContext()
        );
        _test.False(invalid.Success, "unknown trait rejects the replacement batch");
        AssertError(invalid.Errors, "EQA_REFERENCE_MISSING_TRAIT", "binding.weapon.flame");
        _test.Eq(registry.GetBindingDefinitionsTyped().Count, 1, "failed rebuild preserves binding count");
        _test.True(
            ReferenceEquals(registry.GetBindingDefinitionsTyped()["binding.weapon.flame"], snapshot),
            "failed rebuild preserves the last immutable snapshot"
        );
    }

    private void TestRuntimeVocabularyFailsClosed()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        EquipmentAbilityRegistryBuildResult trigger = registry.Rebuild(
            new[] { BuildPack(trigger: "on_future_event") },
            BuildValidationContext()
        );
        _test.False(trigger.Success, "unknown trigger fails closed");
        AssertError(trigger.Errors, "EQA_TRIGGER_UNKNOWN_ID", ".trigger");

        EquipmentAbilityRegistryBuildResult kind = registry.Rebuild(
            new[] { BuildPack(actionKind: "future_handler") },
            BuildValidationContext()
        );
        _test.False(kind.Success, "unknown action kind fails closed");
        AssertError(kind.Errors, "EQA_HANDLER_UNKNOWN_ID", "action.damage");

        EquipmentAbilityConditionGroupImportModel unknownFact = new()
        {
            mode = "all",
            conditions = new[]
            {
                new EquipmentAbilityConditionImportModel
                {
                    condition_id = "condition.future_fact",
                    kind = "compare_fact",
                    payload = new CompareFactConditionPayloadImportModel
                    {
                        left = new EquipmentAbilityFactQueryImportModel
                        {
                            query_kind = "fact",
                            fact_id = "future_fact",
                            subject = "target",
                            value_kind = "int",
                        },
                        compare = "greater_than",
                        right = new EquipmentAbilityFactQueryImportModel
                        {
                            query_kind = "literal",
                            value_kind = "int",
                            int_literal = 0,
                        },
                    },
                },
            },
        };
        EquipmentAbilityRegistryBuildResult fact = registry.Rebuild(
            new[] { BuildPack(conditionGroup: unknownFact) },
            BuildValidationContext()
        );
        _test.False(fact.Success, "unknown fact id fails closed");
        AssertError(fact.Errors, "EQA_FACT_ID_UNKNOWN", ".fact_id");
    }

    private void TestWindupSkillContextSurvivesStatusExpansion()
    {
        EquipmentAbilityActionImportModel action = new()
        {
            action_id = "action.trigger_skill",
            kind = "trigger_skill",
            payload = new TriggerSkillActionPayloadImportModel
            {
                skill_id = SkillId,
                skill_level = 1,
                target_selector = "target",
            },
        };
        using var registry = new EquipmentAbilityContentRegistry();
        EquipmentAbilityRegistryBuildResult result = registry.Rebuild(
            new[] { BuildPack(action: action) },
            BuildValidationContext(windupSkill: true)
        );
        _test.False(result.Success, "automatic trigger_skill rejects a known windup skill");
        AssertError(result.Errors, "EQA_REFERENCE_WINDUP_SKILL_UNSUPPORTED", ".payload.skill_id");
    }

    private static EquipmentAbilityContentPackImportModel BuildPack(
        string traitId = TraitId,
        string trigger = "on_hit",
        string actionKind = "add_damage_dice",
        EquipmentAbilityActionImportModel? action = null,
        EquipmentAbilityConditionGroupImportModel? conditionGroup = null
    )
    {
        EquipmentAbilityActionImportModel resolvedAction = action
            ?? new EquipmentAbilityActionImportModel
        {
            action_id = "action.damage",
            kind = actionKind,
            payload = new AddDamageDiceActionPayloadImportModel
            {
                target_selector = "attack_target",
                damage_type = "physical_slash",
                require_weapon_damage = true,
                dice = new DiceExpressionImportModel
                {
                    terms = new[]
                    {
                        new DiceExpressionTermImportModel
                        {
                            dice_count = 1,
                            dice_sides = 6,
                        },
                    },
                },
            },
        };

        return new EquipmentAbilityContentPackImportModel
        {
            pack_id = "pack.core",
            schema_version = 1,
            load_order = 10,
            bindings = new[]
            {
                new EquipmentAbilityBindingImportModel
                {
                    binding_id = "binding.weapon.flame",
                    trait_id = traitId,
                    override_mode = "add",
                    allowed_source_kinds = new[] { "equipment_fixed" },
                    required_trait_categories = new[] { "weapon_feat" },
                    required_item_tags = new[] { "blade" },
                    supported_equipment_type_ids = new[] { "weapon" },
                    reactions = new[]
                    {
                        new EquipmentAbilityReactionImportModel
                        {
                            reaction_id = "reaction.on_hit",
                            trigger = trigger,
                            timing = "after_hit",
                            condition_group = conditionGroup!,
                            actions = new[] { resolvedAction },
                        },
                    },
                },
            },
        };
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext(
        bool windupSkill = false
    ) => new()
    {
        KnownTraitIds = new HashSet<StringName> { TraitId },
        KnownSkillIds = new HashSet<StringName> { SkillId },
        WindupSkillIds = windupSkill
            ? new HashSet<StringName> { SkillId }
            : new HashSet<StringName>(),
        KnownStatusIds = new HashSet<StringName>(),
    };

    private void AssertError(IReadOnlyList<string> errors, string code, string pathFragment)
    {
        _test.True(
            errors.Any(error =>
                error.Contains(code, StringComparison.Ordinal)
                && error.Contains(pathFragment, StringComparison.Ordinal)
            ),
            $"errors contain {code} at *{pathFragment}: {Format(errors)}"
        );
    }

    private static string Format(IReadOnlyList<string> errors) => string.Join(" | ", errors);

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;
        internal FakeSourceReader(params ContentJsonSourceText[] sources) => _sources = sources;
        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) => _sources;
    }
}
