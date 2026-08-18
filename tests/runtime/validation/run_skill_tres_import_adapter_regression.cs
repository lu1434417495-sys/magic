#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public partial class run_skill_tres_import_adapter_regression : LifecycleTestSceneTree
{
    private const string SkillDirectory = "res://data/configs/skills";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestExportInventoriesMatchDtoSnapshots();
            TestRepresentativePayloadAndRecursiveNestedProjection();
            TestInvalidLegacyPayloadFailsWithoutPartialModel();
            TestRecursiveAndCanonicalKeyFailuresAreClosed();
            TestResourceLevelOverrideResetAndBusinessValuesProject();
            TestDescriptionConfigRawShapesAreStrict();
            TestOwnerSpecificRawStringKeysAreStrict();
            TestPlainGraphHasNoGodotOrDynamicCarrier();
            TestEveryFormalSkillResourceAdapts();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected skill tres adapter regression exception: {exception}");
        }
        RequestTestExit(_test.Finish("Skill tres import adapter regression"));
    }

    private void TestRepresentativePayloadAndRecursiveNestedProjection()
    {
        var repeatParams = new Godot.Collections.Dictionary
        {
            ["base_attack_bonus"] = 7,
            ["follow_up_cost_multiplier"] = 1.5,
            ["same_target_only"] = true,
            ["penalty_free_stages_by_level"] = new Godot.Collections.Dictionary { [1] = 2 },
        };
        var nestedStatus = new CombatEffectDef
        {
            effect_type = "status", status_id = "nested_status", power = 3,
            @params = new Godot.Collections.Dictionary { ["source_skill_id"] = "synthetic_adapter" },
        };
        var parent = new CombatEffectDef
        {
            effect_type = "damage", dice_count = 2, dice_sides = 6,
            save_failure_status_outcomes = new Godot.Collections.Array<CombatWeightedStatusOutcomeDef>
            {
                new() { outcome_id = "nested", weight = 4, status_effect = nestedStatus },
            },
        };
        var repeat = new CombatEffectDef { effect_type = "repeat_attack_until_fail", @params = repeatParams };
        var combat = new CombatSkillDef
        {
            skill_id = "synthetic_adapter",
            effect_defs = new Godot.Collections.Array<CombatEffectDef> { repeat, parent },
        };
        var bindingArray = new Godot.Collections.Array { "alpha", new StringName("beta") };
        var bindings = new Godot.Collections.Dictionary
        {
            ["bool_value"] = true,
            ["int_value"] = 5L,
            ["float_value"] = 1.25,
            ["string_value"] = "value",
            ["string_array"] = bindingArray,
        };
        var skill = new SkillDef
        {
            skill_id = "synthetic_adapter", display_name = "adapter", combat_profile = combat,
            contingency_automation_profile = new ContingencyAutomationDef
            {
                allowed_parameter_bindings = bindings,
            },
        };
        JsonContentEntryContext context = new("skills", "synthetic_adapter", "synthetic", "/entries/0");
        ContentImportStageResult<SkillImportModel> result = SkillTresImportAdapter.TryAdapt(context, skill);
        _test.True(result.HasValue, "representative Resource graph should adapt: " + Format(result));
        if (!result.HasValue) return;

        CombatSkillImportModel profile = result.Value.CombatProfile!;
        var repeatPayload = (RepeatAttackUntilFailEffectPayloadImportModel)profile.EffectDefs[0].Payload;
        _test.Eq(repeatPayload.BaseAttackBonus, 7, "repeat base attack bonus should copy");
        _test.Eq(repeatPayload.FollowUpCostMultiplier, 1.5, "repeat double should copy");
        _test.True(repeatPayload.SameTargetOnly, "repeat bool should copy");
        _test.Eq(repeatPayload.PenaltyFreeStagesByLevel[1], 2, "repeat level map should copy");
        CombatWeightedStatusOutcomeImportModel outcome = profile.EffectDefs[1].SaveFailureStatusOutcomes[0];
        _test.Eq(outcome.OutcomeId.Value, "nested", "weighted outcome id should copy");
        _test.Eq(outcome.Weight, 4, "weighted outcome weight should copy");
        _test.Eq(outcome.StatusEffect.StatusId.Value, "nested_status", "recursive nested status should copy");
        _test.Eq(((StatusEffectPayloadImportModel)outcome.StatusEffect.Payload).SourceSkillId.Value, "synthetic_adapter", "recursive typed payload should copy");
        IReadOnlyDictionary<SkillImportIdentifier, ContingencyParameterBindingImportValue> projectedBindings = result.Value.ContingencyAutomationProfile!.AllowedParameterBindings;
        _test.True(projectedBindings.Values.Any(static value => value is ContingencyBoolBindingImportValue), "bool binding should stay typed");
        _test.True(projectedBindings.Values.Any(static value => value is ContingencyIntBindingImportValue), "int64 binding should stay typed");
        _test.True(projectedBindings.Values.Any(static value => value is ContingencyFloatBindingImportValue), "finite double binding should stay typed");
        _test.True(projectedBindings.Values.Any(static value => value is ContingencyStringBindingImportValue), "string binding should stay typed");
        _test.True(projectedBindings.Values.Any(static value => value is ContingencyStringListBindingImportValue), "string array binding should stay typed");

        repeatParams["base_attack_bonus"] = 99;
        nestedStatus.status_id = "mutated";
        combat.effect_defs.Clear();
        _test.Eq(repeatPayload.BaseAttackBonus, 7, "model must not borrow params dictionary");
        _test.Eq(outcome.StatusEffect.StatusId.Value, "nested_status", "model must not borrow nested Resource");
        _test.Eq(profile.EffectDefs.Count, 2, "model must not borrow effect array");
    }

    private void TestInvalidLegacyPayloadFailsWithoutPartialModel()
    {
        var skill = new SkillDef
        {
            skill_id = "invalid_adapter", display_name = "invalid",
            combat_profile = new CombatSkillDef
            {
                skill_id = "invalid_adapter",
                effect_defs = new Godot.Collections.Array<CombatEffectDef>
                {
                    new() { effect_type = "damage", @params = new Godot.Collections.Dictionary { ["unknown"] = 1 } },
                },
            },
        };
        ContentImportStageResult<SkillImportModel> result = SkillTresImportAdapter.TryAdapt(
            new JsonContentEntryContext("skills", "invalid_adapter", "synthetic", "/entries/4"), skill
        );
        _test.True(!result.HasValue, "unknown legacy param must fail closed");
        _test.Eq(result.Diagnostics.Count, 1, "unknown legacy param should produce one stable diagnostic");
        if (result.Diagnostics.Count == 1)
        {
            _test.Eq(result.Diagnostics[0].RuleId, "skill.tres.invalid_resource", "legacy param rule should be stable");
            _test.Eq(result.Diagnostics[0].JsonPointer, "/entries/4/combat_profile/effect_defs/0/payload/unknown", "legacy param pointer should be entry-absolute");
        }
    }

    private void TestRecursiveAndCanonicalKeyFailuresAreClosed()
    {
        var cyclic = new CombatEffectDef { effect_type = "damage" };
        cyclic.save_failure_status_outcomes = new Godot.Collections.Array<CombatWeightedStatusOutcomeDef>
        {
            new() { outcome_id = "cycle", status_effect = cyclic },
        };
        ContentImportStageResult<SkillImportModel> cycle = AdaptEffect("cycle_adapter", cyclic, "/entries/5");
        _test.True(!cycle.HasValue, "recursive weighted effect cycle must fail closed");
        _test.True(cycle.Diagnostics.Any(static diagnostic => diagnostic.JsonPointer == "/entries/5/combat_profile/effect_defs/0/save_failure_status_outcomes/0/status_effect"), "cycle diagnostic should be entry-absolute");

        ContentImportStageResult<SkillImportModel> unknown = AdaptEffect(
            "unknown_effect",
            new CombatEffectDef { effect_type = "legacy_unknown" },
            "/entries/7"
        );
        _test.True(!unknown.HasValue, "unknown Resource effect kind must fail closed");
        _test.True(unknown.Diagnostics.Any(static diagnostic => diagnostic.JsonPointer == "/entries/7/combat_profile/effect_defs/0/effect_type"), "unknown effect diagnostic should be entry-absolute");
    }

    private void TestResourceLevelOverrideResetAndBusinessValuesProject()
    {
        var overrides = new Godot.Collections.Dictionary
        {
            [1] = new Godot.Collections.Dictionary
            {
                ["attack_resolution_mode"] = "fate_attack",
                ["attack_defense_mode"] = "touch",
            },
            [2] = new Godot.Collections.Dictionary
            {
                ["attack_resolution_mode"] = "",
                ["attack_defense_mode"] = "",
                ["ap_cost"] = -4,
                ["max_target_count"] = 0,
            },
            [3] = new Godot.Collections.Dictionary(),
        };
        var effect = new CombatEffectDef
        {
            effect_type = "damage", min_skill_level = -2, max_skill_level = -3,
            power = -5, duration_tu = -7,
        };
        var skill = new SkillDef
        {
            skill_id = "resource_business_values", display_name = "resource", max_level = -1,
            combat_profile = new CombatSkillDef
            {
                skill_id = "different_but_representable", range_value = -1, ap_cost = -2,
                mp_cost = -3, cooldown_tu = -4, level_overrides = overrides,
                effect_defs = new Godot.Collections.Array<CombatEffectDef> { effect },
            },
        };
        ContentImportStageResult<SkillImportModel> result = SkillTresImportAdapter.TryAdapt(
            new JsonContentEntryContext("skills", "resource_business_values", "synthetic", "/entries/8"), skill
        );
        _test.True(result.HasValue, "adapter should preserve validator-owned business values: " + Format(result));
        if (!result.HasValue) return;
        CombatSkillImportModel combat = result.Value.CombatProfile!;
        _test.Eq(result.Value.MaxLevel, -1, "negative max level should remain representable for validator");
        _test.Eq(combat.SkillId.Value, "different_but_representable", "mismatched nested skill ID should remain representable for validator");
        _test.Eq(combat.RangeValue, -1, "negative range should remain representable for validator");
        _test.Eq(combat.LevelOverrides[2].AttackResolutionMode, CombatSkillLevelOverrideAttackResolutionMode.Auto, "empty Resource attack resolution should reset to Auto");
        _test.Eq(combat.LevelOverrides[2].AttackDefenseMode, CombatSkillLevelOverrideAttackDefenseMode.Normal, "empty Resource attack defense should reset to Normal");
        _test.Eq(combat.LevelOverrides[2].ApCost, -4, "negative override should remain representable for validator");
        _test.Eq(combat.LevelOverrides[2].MaxTargetCount, 0, "zero override target count should remain representable for validator");
        _test.True(combat.LevelOverrides.ContainsKey(3), "empty Resource override should remain represented instead of being dropped");
        _test.True(combat.LevelOverrides[3].ApCost == null && combat.LevelOverrides[3].AttackResolutionMode == null, "empty Resource override should project as an all-null plain entry");
        _test.Eq(combat.EffectDefs[0].Power, -5, "negative effect power should remain representable for validator");
        _test.Eq(combat.EffectDefs[0].MaxSkillLevel, -3, "effect level window should remain representable for validator");
    }

    private void TestDescriptionConfigRawShapesAreStrict()
    {
        AssertDescriptionConfigFailure(
            new Godot.Collections.Dictionary { [1] = new Godot.Collections.Dictionary { ["value"] = "1" } },
            "/entries/9/level_description_configs",
            "/entries/9"
        );
        AssertDescriptionConfigFailure(
            new Godot.Collections.Dictionary { [new StringName("1")] = new Godot.Collections.Dictionary { ["value"] = "1" } },
            "/entries/10/level_description_configs",
            "/entries/10"
        );
        AssertDescriptionConfigFailure(
            new Godot.Collections.Dictionary { ["1"] = new Godot.Collections.Dictionary { ["value"] = new StringName("1") } },
            "/entries/11/level_description_configs/1/value",
            "/entries/11"
        );
    }

    private void AssertDescriptionConfigFailure(Godot.Collections.Dictionary configs, string expectedPointer, string entryPointer)
    {
        var skill = new SkillDef
        {
            skill_id = "invalid_description_shape", display_name = "invalid",
            level_description_configs = configs,
        };
        ContentImportStageResult<SkillImportModel> result = SkillTresImportAdapter.TryAdapt(
            new JsonContentEntryContext("skills", "invalid_description_shape", "synthetic", entryPointer), skill
        );
        _test.True(!result.HasValue, "invalid description raw shape must fail closed");
        _test.True(result.Diagnostics.Any(diagnostic => diagnostic.JsonPointer == expectedPointer), $"description raw shape diagnostic should point to {expectedPointer}");
    }

    private void TestOwnerSpecificRawStringKeysAreStrict()
    {
        var invalidGrowth = new SkillDef
        {
            skill_id = "invalid_growth_key",
            display_name = "invalid",
            attribute_growth_progress = new Godot.Collections.Dictionary
            {
                [new StringName("strength")] = 1,
            },
        };
        ContentImportStageResult<SkillImportModel> growthResult = SkillTresImportAdapter.TryAdapt(
            new JsonContentEntryContext("skills", "invalid_growth_key", "synthetic", "/entries/12"),
            invalidGrowth
        );
        _test.True(!growthResult.HasValue, "attribute growth StringName key must fail closed");
        _test.True(
            growthResult.Diagnostics.Any(static diagnostic => diagnostic.JsonPointer == "/entries/12/attribute_growth_progress/strength"),
            "attribute growth StringName key diagnostic should use its exact pointer"
        );

        var invalidOverride = new SkillDef
        {
            skill_id = "invalid_override_key",
            display_name = "invalid",
            combat_profile = new CombatSkillDef
            {
                skill_id = "invalid_override_key",
                level_overrides = new Godot.Collections.Dictionary
                {
                    [1] = new Godot.Collections.Dictionary
                    {
                        [new StringName("ap_cost")] = 1,
                    },
                },
            },
        };
        ContentImportStageResult<SkillImportModel> overrideResult = SkillTresImportAdapter.TryAdapt(
            new JsonContentEntryContext("skills", "invalid_override_key", "synthetic", "/entries/13"),
            invalidOverride
        );
        _test.True(!overrideResult.HasValue, "level override StringName member must fail closed");
        _test.True(
            overrideResult.Diagnostics.Any(static diagnostic => diagnostic.JsonPointer == "/entries/13/combat_profile/level_overrides/1/ap_cost"),
            "level override StringName member diagnostic should use its exact pointer"
        );

        AssertDescriptionConfigFailure(
            new Godot.Collections.Dictionary
            {
                ["1"] = new Godot.Collections.Dictionary
                {
                    [new StringName("value")] = "1",
                },
            },
            "/entries/14/level_description_configs/1/value",
            "/entries/14"
        );
    }

    private static ContentImportStageResult<SkillImportModel> AdaptEffect(string id, CombatEffectDef effect, string pointer)
    {
        var skill = new SkillDef
        {
            skill_id = id, display_name = id,
            combat_profile = new CombatSkillDef
            {
                skill_id = id,
                effect_defs = new Godot.Collections.Array<CombatEffectDef> { effect },
            },
        };
        return SkillTresImportAdapter.TryAdapt(new JsonContentEntryContext("skills", id, "synthetic", pointer), skill);
    }

    private void TestEveryFormalSkillResourceAdapts()
    {
        string[] files = DirAccess.GetFilesAt(SkillDirectory)
            .Where(static file => file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToArray();
        _test.True(files.Length > 0, "formal skill directory should contain resources");
        var models = new List<SkillImportModel>(files.Length);
        using (var loader = new TestContentResourceLoader())
        {
            for (int index = 0; index < files.Length; index++)
            {
                string path = $"{SkillDirectory}/{files[index]}";
                SkillDef skill = loader.LoadCanonical<SkillDef>(path);
                string id = skill.skill_id.ToString();
                ContentImportStageResult<SkillImportModel> result = SkillTresImportAdapter.TryAdapt(
                    new JsonContentEntryContext("skills", id, path, $"/entries/{index}"), skill
                );
                if (!result.HasValue)
                {
                    _test.Fail($"formal skill {path} must adapt: {Format(result)}");
                    continue;
                }
                _test.Eq(result.Value.SkillId.Value, id, $"formal skill ID should match for {path}");
                models.Add(result.Value);
            }
        }
        _test.Eq(models.Count, files.Length, "every discovered formal skill should publish exactly one model");
        foreach (SkillImportModel model in models)
            _test.True(model.SkillId.Value.Length > 0, "plain models should remain usable after loader disposal");
    }

    private void TestPlainGraphHasNoGodotOrDynamicCarrier()
    {
        Type root = typeof(SkillImportModel);
        var pending = new Stack<Type>();
        var seen = new HashSet<Type>();
        pending.Push(root);
        foreach (Type payload in root.Assembly.GetTypes().Where(static type => typeof(ICombatEffectPayloadImportModel).IsAssignableFrom(type)))
            pending.Push(payload);
        foreach (Type binding in root.Assembly.GetTypes().Where(static type => typeof(ContingencyParameterBindingImportValue).IsAssignableFrom(type)))
            pending.Push(binding);
        while (pending.Count > 0)
        {
            Type type = Unwrap(pending.Pop());
            if (!seen.Add(type) || IsLeaf(type)) continue;
            _test.True(!typeof(GodotObject).IsAssignableFrom(type), $"plain graph must not contain Godot type {type}");
            _test.True(type != typeof(Variant) && type != typeof(JsonElement) && type != typeof(object), $"plain graph must not contain dynamic carrier {type}");
            _test.True(!type.Name.Contains("ResourcePath", StringComparison.OrdinalIgnoreCase) && !type.Name.Equals("Uid", StringComparison.OrdinalIgnoreCase), $"plain graph type must not expose resource path/UID: {type}");
            if (type.IsGenericType)
                foreach (Type argument in type.GetGenericArguments()) pending.Push(argument);
            if (type.Assembly != root.Assembly) continue;
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                _test.True(!property.Name.Contains("ResourcePath", StringComparison.OrdinalIgnoreCase) && !property.Name.Equals("Uid", StringComparison.OrdinalIgnoreCase), $"plain graph property must not expose resource path/UID: {type.Name}.{property.Name}");
                pending.Push(property.PropertyType);
            }
        }
    }

    private void TestExportInventoriesMatchDtoSnapshots()
    {
        AssertExportMap(typeof(SkillDef), typeof(SkillJsonDto));
        AssertExportMap(typeof(CombatSkillDef), typeof(CombatSkillJsonDto));
        AssertExportMap(typeof(AttributeModifier), typeof(AttributeModifierJsonDto));
        AssertExportMap(typeof(ContingencyAutomationDef), typeof(ContingencyAutomationJsonDto));
        AssertExportMap(typeof(CombatWindupDef), typeof(CombatWindupJsonDto));
        AssertExportMap(typeof(CombatDirectionalPiercingDef), typeof(CombatDirectionalPiercingJsonDto));
        AssertExportMap(typeof(CombatApproachAttackDef), typeof(CombatApproachAttackJsonDto));
        AssertExportMap(typeof(CombatLineThroughAttackDef), typeof(CombatLineThroughAttackJsonDto));
        AssertExportMap(typeof(CombatSequentialLineHitDef), typeof(CombatSequentialLineHitJsonDto));
        AssertExportMap(typeof(CombatSpellReactionDef), typeof(CombatSpellReactionJsonDto));
        AssertExportMap(typeof(CombatRangedWeaponReactionDef), typeof(CombatRangedWeaponReactionJsonDto));
        AssertExportMap(typeof(CombatCastVariantDef), typeof(CombatCastVariantJsonDto), replaceParamsWithPayload: true);
        AssertExportMap(typeof(CombatEffectDef), typeof(CombatEffectJsonDto), replaceParamsWithPayload: true);
        AssertExportMap(typeof(CombatEffectSlotWeightDef), typeof(CombatEffectSlotWeightJsonDto));
        AssertExportMap(typeof(CombatDamageSegmentDef), typeof(CombatDamageSegmentJsonDto));
        AssertExportMap(typeof(CombatTargetDamageMultiplierRuleDef), typeof(CombatTargetDamageMultiplierRuleJsonDto));
        AssertExportMap(typeof(CombatWeightedStatusOutcomeDef), typeof(CombatWeightedStatusOutcomeJsonDto));
    }

    private void AssertExportMap(Type resourceType, Type dtoType, bool replaceParamsWithPayload = false)
    {
        var exports = resourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetCustomAttribute<ExportAttribute>() != null)
            .Select(static property => property.Name == "params" ? "params" : property.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (replaceParamsWithPayload && exports.Remove("params")) exports.Add("payload");
        var json = dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(static name => name != null)
            .Select(static name => name!)
            .ToHashSet(StringComparer.Ordinal);
        _test.Eq(string.Join("\n", exports.OrderBy(static x => x, StringComparer.Ordinal)), string.Join("\n", json.OrderBy(static x => x, StringComparer.Ordinal)), $"{resourceType.Name} exports must exactly match {dtoType.Name}");
    }

    private static Type Unwrap(Type type)
    {
        if (type.IsArray) return type.GetElementType()!;
        return Nullable.GetUnderlyingType(type) ?? type;
    }
    private static bool IsLeaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(SkillImportIdentifier) || type == typeof(SkillImportStringName) || type == typeof(SkillImportAssetId) || type == typeof(SkillImportStatId);
    private static string Format(ContentImportStageResult<SkillImportModel> result) => string.Join(" | ", result.Diagnostics.Select(static diagnostic => $"{diagnostic.RuleId}@{diagnostic.JsonPointer}:{diagnostic.Message}"));
}
