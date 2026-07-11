using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_terrain_effect_state_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestParamsLifetimePolicyRoundtrip();
        TestMoveCostDeltaAndLifetimePolicyTypedFieldsRoundtrip();
        TestAppliedStatusMetadataRoundtrip();
        TestOverlayAndDisplayMetadataRoundtrip();
        TestAccuracyModifierSpecRoundtrip();
        TestNonstackingStatusMetadataRoundtrip();
        TestTopLevelLifetimePolicyIsRejected();
        TestInvalidTargetTeamFilterIsRejected();

        RequestTestExit(_test.Finish("Battle terrain effect state schema regression"));
    }

    private void TestParamsLifetimePolicyRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        BattleTerrainEffectState restored = BattleTerrainEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restored != null, "terrain effect state roundtrip 应恢复对象。");
        _test.Eq(
            restored != null ? DictString(restored.@params, "lifetime_policy", "") : "",
            "",
            "terrain owner 内部 @params 不应保留 formal lifetime_policy。"
        );
        _test.Eq(
            restored != null ? restored.remaining_tu : -1,
            0,
            "battle lifetime terrain effect 应允许 remaining_tu=0。"
        );
        _test.Eq(
            restored != null ? restored.tick_interval_tu : -1,
            0,
            "battle lifetime terrain effect 应允许 tick_interval_tu=0。"
        );
    }

    private void TestTopLevelLifetimePolicyIsRejected()
    {
        GDictionary payload = BuildEffect().ToDictionary();
        payload["lifetime_policy"] = "battle";
        _test.True(
            BattleTerrainEffectState.FromDictionary(payload) == null,
            "terrain effect strict schema 应拒绝顶层 lifetime_policy 字段。"
        );
    }

    private void TestAppliedStatusMetadataRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        effect.effect_type = "status";
        effect.applied_status_id = "burning";
        effect.applied_status_duration_tu = 40;

        BattleTerrainEffectState restored = BattleTerrainEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restored != null, "terrain applied status metadata roundtrip 应恢复对象。");
        _test.Eq(
            restored != null ? restored.applied_status_id : new StringName(""),
            new StringName("burning"),
            "terrain tick status 应通过正式 applied_status_id 字段 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.applied_status_duration_tu : -1,
            40,
            "terrain tick status duration 应通过正式 applied_status_duration_tu 字段 roundtrip。"
        );
    }

    private void TestMoveCostDeltaAndLifetimePolicyTypedFieldsRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        BattleTerrainEffectState restored = BattleTerrainEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restored != null, "terrain move-cost metadata roundtrip 应恢复对象。");
        _test.Eq(
            restored != null ? restored.lifetime_policy : new StringName(""),
            new StringName("battle"),
            "terrain effect lifetime_policy 应通过正式字段 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.move_cost_delta : -1,
            3,
            "terrain effect move_cost_delta 应通过正式字段 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.@params.Count : -1,
            0,
            "terrain owner 内部 @params roundtrip 后只应保留 residual params。"
        );
    }

    private void TestOverlayAndDisplayMetadataRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        BattleTerrainEffectState restored = BattleTerrainEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restored != null, "terrain overlay/display metadata roundtrip 应恢复对象。");
        _test.Eq(
            restored != null ? restored.render_overlay_id : new StringName(""),
            new StringName("meteor_crater_core"),
            "terrain effect render_overlay_id 应通过正式字段 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.overlay_priority : -1,
            4,
            "terrain effect overlay_priority 应通过正式字段 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.display_name : "",
            "陨坑核心",
            "terrain effect display_name 应通过正式字段/边界投影 roundtrip。"
        );
    }

    private void TestAccuracyModifierSpecRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        BattleTerrainEffectState restored = BattleTerrainEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restored != null, "terrain accuracy modifier spec roundtrip 应恢复对象。");
        _test.True(
            restored?.accuracy_modifier_spec != null,
            "terrain effect accuracy_modifier_spec 应通过正式字段/边界投影 roundtrip。"
        );
        _test.Eq(
            restored?.accuracy_modifier_spec?.modifier_delta ?? 0,
            -2,
            "terrain effect accuracy_modifier_spec.modifier_delta 应稳定 roundtrip。"
        );
        _test.Eq(
            restored?.accuracy_modifier_spec?.stack_key ?? new StringName(""),
            new StringName("dust_attack_roll_penalty"),
            "terrain effect accuracy_modifier_spec.stack_key 应稳定 roundtrip。"
        );
    }

    private void TestNonstackingStatusMetadataRoundtrip()
    {
        BattleTerrainEffectState effect = BuildEffect();
        BattleTerrainEffectState restored = BattleTerrainEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restored != null, "terrain nonstacking-status metadata roundtrip 应恢复对象。");
        _test.Eq(
            restored != null ? restored.does_not_stack_with_status_id : new StringName(""),
            new StringName("slow"),
            "terrain effect does_not_stack_with_status_id 应通过正式字段/边界投影 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.does_not_stack_with_status_ids.Count : -1,
            2,
            "terrain effect does_not_stack_with_status_ids 应通过正式字段/边界投影 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.does_not_stack_with_status_ids[0] : new StringName(""),
            new StringName("slow"),
            "terrain effect does_not_stack_with_status_ids[0] 应稳定 roundtrip。"
        );
        _test.Eq(
            restored != null ? restored.does_not_stack_with_status_ids[1] : new StringName(""),
            new StringName("rooted"),
            "terrain effect does_not_stack_with_status_ids[1] 应稳定 roundtrip。"
        );
    }

    private void TestInvalidTargetTeamFilterIsRejected()
    {
        GDictionary payload = BuildEffect().ToDictionary();
        payload["target_team_filter"] = "hostile";
        _test.True(
            BattleTerrainEffectState.FromDictionary(payload) == null,
            "terrain effect state 不应接受 hostile 作为 target_team_filter。"
        );
    }

    private static BattleTerrainEffectState BuildEffect()
    {
        return new BattleTerrainEffectState
        {
            field_instance_id = "meteor_crater_core_1",
            effect_id = "meteor_swarm_crater_core",
            effect_type = "none",
            lifetime_policy = "battle",
            move_cost_delta = 3,
            render_overlay_id = "meteor_crater_core",
            overlay_priority = 4,
            display_name = "陨坑核心",
            accuracy_modifier_spec = new BattleAttackRollModifierSpec
            {
                source_domain = "terrain",
                label = "尘土",
                modifier_delta = -2,
                stack_key = "dust_attack_roll_penalty",
                stack_mode = "max",
                roll_kind_filter = "spell_attack",
                endpoint_mode = "either",
                distance_min_exclusive = 1,
                distance_max_inclusive = -1,
                target_team_filter = "any",
                footprint_mode = "any_cell",
                applies_to = "attack_roll",
            },
            does_not_stack_with_status_id = "slow",
            does_not_stack_with_status_ids = new List<StringName> { "slow", "rooted" },
            source_unit_id = "caster",
            source_skill_id = "mage_meteor_swarm",
            target_team_filter = "any",
            power = 0,
            damage_tag = "",
            remaining_tu = 0,
            tick_interval_tu = 0,
            next_tick_at_tu = 0,
            stack_behavior = "refresh",
            @params = new GDictionary
            {
                ["lifetime_policy"] = "battle",
                ["move_cost_delta"] = 3,
            },
        };
    }

    private static string DictString(GDictionary dictionary, string key, string defaultValue)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return defaultValue;
        }
        Variant value = dictionary[key];
        return value.VariantType is Variant.Type.String or Variant.Type.StringName
            ? value.AsString()
            : defaultValue;
    }
}
