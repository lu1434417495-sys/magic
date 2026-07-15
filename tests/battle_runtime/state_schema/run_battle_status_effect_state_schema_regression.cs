using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_status_effect_state_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<GodotProjectionLease<GDictionary>> _payloadLeases = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestValidRoundtripWithoutDuration();
        TestValidRoundtripWithDurationTickAndSkip();
        TestMissingRequiredFieldReturnsNull();
        TestExtraLegacyFieldReturnsNull();
        TestWrongTypesReturnNull();
        TestStringNumbersAndBoolsReturnNull();
        TestEmptyStatusIdReturnsNull();
        TestNegativeDurationReturnsNull();
        TestZeroTickOptionalReturnsNull();
        TestSkipFalseOptionalReturnsNull();
        TestDuplicateStateStillWorks();
        TestRuntimeDamageMultiplierFieldsRoundTripThroughParamsBoundary();
        TestTypedMultiplierFieldsRoundTripThroughParamsBoundary();
        TestMitigationTierRoundTripThroughParamsBoundary();
        TestSaveBonusRoundTripThroughParamsBoundary();
        TestSaveTagListsRoundTripThroughParamsBoundary();
        TestFixedMitigationFieldsRoundTripThroughParamsBoundary();
        TestLockDodgeBonusRoundTripAsTypedField();
        TestBodySizeOverrideRoundTripThroughParamsBoundary();
        TestSelfSaveFieldsRoundTripThroughParamsBoundary();
        TestBarrierSourceFieldsRoundTripThroughParamsBoundary();
        TestStatusSourceSkillFieldsRoundTripThroughParamsBoundary();
        TestStatusStackFieldsRoundTripThroughParamsBoundary();
        TestStatusRangeBonusRoundTripThroughParamsBoundary();
        TestTemporalTagFieldsRoundTripThroughParamsBoundary();

        DisposePayloadLeases();
        RequestTestExit(_test.Finish("Battle status effect state schema regression"));
    }

    private GDictionary Project(BattleStatusEffectState effect)
    {
        GodotProjectionLease<GDictionary> lease = effect.ToDictionaryLease();
        _payloadLeases.Add(lease);
        return lease.Value;
    }

    private GDictionary ProjectParams(BattleStatusEffectState effect)
    {
        GodotProjectionLease<GDictionary> lease = RuntimePlainPayload.ProjectDictionaryLease(
            effect?.ParamsSnapshotPlain
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            "battle-status-effect-state-schema.params",
            LifetimeDomain.Request,
            "battle-status-effect-state-schema.params"
        );
        _payloadLeases.Add(lease);
        return lease.Value;
    }

    private void DisposePayloadLeases()
    {
        for (int index = _payloadLeases.Count - 1; index >= 0; index--)
            _payloadLeases[index].Dispose();
        _payloadLeases.Clear();
    }

    private void TestValidRoundtripWithoutDuration()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "guarded",
            source_unit_id = "",
            power = 2,
            @params = new GDictionary { ["damage_tag"] = "holy" },
            stacks = 0,
        };

        GDictionary payload = Project(effect);
        _test.False(payload.ContainsKey("duration"), "无 duration 状态的 to_dict 不应写 duration。");
        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "无 duration 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.status_id, new StringName("guarded"), "roundtrip 应保留 status_id。");
        _test.Eq(restored.source_unit_id, new StringName(""), "roundtrip 应允许空 source_unit_id。");
        _test.Eq(restored.power, 2, "roundtrip 应保留 power。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary(),
            "roundtrip 后 owner 内部 @params 只应保留 residual params。"
        );
        _test.Eq(restored.stacks, 0, "roundtrip 应允许新建状态写出的 0 stacks。");
        _test.Eq(restored.duration, -1, "缺失 duration 应恢复为 -1。");
        _test.Eq(restored.tick_interval_tu, 0, "缺失 tick_interval_tu 应恢复为 0。");
        _test.Eq(restored.next_tick_at_tu, 0, "缺失 next_tick_at_tu 应恢复为 0。");
        _test.False(restored.skip_next_turn_end_decay, "缺失 skip_next_turn_end_decay 应恢复为 false。");
    }

    private void TestValidRoundtripWithDurationTickAndSkip()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "burning",
            source_unit_id = "caster",
            power = 3,
            @params = new GDictionary
            {
                ["damage_tag"] = "fire",
                ["nested"] = new GDictionary { ["value"] = 1 },
            },
            stacks = 2,
            duration = 20,
            tick_interval_tu = 10,
            next_tick_at_tu = 15,
            skip_next_turn_end_decay = true,
            lock_dodge_bonus = true,
            lock_crit = true,
        };

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(Project(effect));
        _test.True(restored != null, "带 duration/tick/skip 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.status_id, new StringName("burning"), "roundtrip 应保留 status_id。");
        _test.Eq(restored.source_unit_id, new StringName("caster"), "roundtrip 应保留 source_unit_id。");
        _test.Eq(restored.power, 3, "roundtrip 应保留 power。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["nested"] = new GDictionary { ["value"] = 1 },
            },
            "roundtrip 后 owner 内部 @params 只应保留 residual params。"
        );
        _test.Eq(restored.stacks, 2, "roundtrip 应保留 stacks。");
        _test.Eq(restored.duration, 20, "roundtrip 应保留 duration。");
        _test.Eq(restored.tick_interval_tu, 10, "roundtrip 应保留 tick_interval_tu。");
        _test.Eq(restored.next_tick_at_tu, 15, "roundtrip 应保留 next_tick_at_tu。");
        _test.True(restored.skip_next_turn_end_decay, "roundtrip 应保留 skip_next_turn_end_decay。");
        _test.True(restored.lock_dodge_bonus, "roundtrip 应保留 lock_dodge_bonus typed 字段。");
        _test.True(restored.lock_crit, "roundtrip 应保留 lock_crit typed 字段。");
    }

    private void TestMissingRequiredFieldReturnsNull()
    {
        foreach (string field in new[] { "status_id", "source_unit_id", "power", "params", "stacks" })
        {
            GDictionary payload = ValidPayload();
            payload.Remove(field);
            _test.True(BattleStatusEffectState.FromDictionary(payload) == null, $"缺少必需字段 {field} 应返回 null。");
        }
    }

    private void TestExtraLegacyFieldReturnsNull()
    {
        GDictionary payload = ValidPayload();
        payload["remaining_turns"] = 2;
        _test.True(BattleStatusEffectState.FromDictionary(payload) == null, "额外旧字段应返回 null。");
    }

    private void TestWrongTypesReturnNull()
    {
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("status_id", 7)) == null, "status_id 必须是 String/StringName。");
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("source_unit_id", 7)) == null,
            "source_unit_id 必须是 String/StringName。"
        );
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("power", 1.5)) == null, "power 必须是 int。");
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("params", new GArray())) == null,
            "params 必须是 Dictionary。"
        );
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("stacks", 1.0)) == null, "stacks 必须是 int。");
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("duration", 1.0)) == null, "duration 必须是 int。");
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("tick_interval_tu", 1.0)) == null,
            "tick_interval_tu 必须是 int。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("next_tick_at_tu", 1.0)) == null,
            "next_tick_at_tu 必须是 int。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("skip_next_turn_end_decay", 1)) == null,
            "skip_next_turn_end_decay 必须是 bool true。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("lock_crit", 1)) == null,
            "lock_crit 必须是 bool true。"
        );

        GDictionary stringNamePayload = ValidPayload();
        stringNamePayload["status_id"] = new StringName("slow");
        stringNamePayload["source_unit_id"] = new StringName("caster");
        _test.True(
            BattleStatusEffectState.FromDictionary(stringNamePayload) != null,
            "StringName status_id/source_unit_id 应继续可用。"
        );
    }

    private void TestStringNumbersAndBoolsReturnNull()
    {
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("power", "3")) == null, "power 不接受字符串数字。");
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("stacks", "1")) == null, "stacks 不接受字符串数字。");
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("duration", "10")) == null, "duration 不接受字符串数字。");
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("tick_interval_tu", "10")) == null,
            "tick_interval_tu 不接受字符串数字。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("next_tick_at_tu", "15")) == null,
            "next_tick_at_tu 不接受字符串数字。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("skip_next_turn_end_decay", "true")) == null,
            "skip_next_turn_end_decay 不接受字符串 bool。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("lock_crit", "true")) == null,
            "lock_crit 不接受字符串 bool。"
        );
    }

    private void TestEmptyStatusIdReturnsNull()
    {
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("status_id", "")) == null, "空 String status_id 应返回 null。");
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("status_id", new StringName(""))) == null,
            "空 StringName status_id 应返回 null。"
        );
    }

    private void TestNegativeDurationReturnsNull()
    {
        _test.True(BattleStatusEffectState.FromDictionary(PayloadWith("duration", -1)) == null, "显式负 duration 应返回 null。");
    }

    private void TestZeroTickOptionalReturnsNull()
    {
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("tick_interval_tu", 0)) == null,
            "显式 0 tick_interval_tu 应返回 null。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("next_tick_at_tu", 0)) == null,
            "显式 0 next_tick_at_tu 应返回 null。"
        );
    }

    private void TestSkipFalseOptionalReturnsNull()
    {
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("skip_next_turn_end_decay", false)) == null,
            "显式 false skip_next_turn_end_decay 应返回 null。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("lock_crit", false)) == null,
            "显式 false lock_crit 应返回 null。"
        );
        _test.True(
            BattleStatusEffectState.FromDictionary(PayloadWith("lock_dodge_bonus", false)) == null,
            "显式 false lock_dodge_bonus 应返回 null。"
        );
    }

    private void TestDuplicateStateStillWorks()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "slow",
            source_unit_id = "caster",
            power = 1,
            @params = new GDictionary { ["move_cost_delta"] = 1 },
            stacks = 1,
            duration = 15,
            lock_dodge_bonus = true,
            lock_crit = true,
        };

        BattleStatusEffectState duplicate = effect.DuplicateState();
        _test.True(duplicate != null, "duplicate_state 应继续返回有效对象。");
        if (duplicate == null)
        {
            return;
        }

        _test.True(duplicate != effect, "duplicate_state 应返回新对象。");
        _test.Eq(duplicate.status_id, new StringName("slow"), "duplicate_state 应保留 status_id。");
        _test.Eq(duplicate.source_unit_id, new StringName("caster"), "duplicate_state 应保留 source_unit_id。");
        _test.Eq(duplicate.power, 1, "duplicate_state 应保留 power。");
        AssertDictionaryEq(
            ProjectParams(duplicate),
            new GDictionary { ["move_cost_delta"] = 1 },
            "duplicate_state 应保留 params。"
        );
        _test.Eq(duplicate.stacks, 1, "duplicate_state 应保留 stacks。");
        _test.Eq(duplicate.duration, 15, "duplicate_state 应保留 duration。");
        _test.True(duplicate.lock_dodge_bonus, "duplicate_state 应保留 lock_dodge_bonus。");
        _test.True(duplicate.lock_crit, "duplicate_state 应保留 lock_crit。");
    }

    private void TestRuntimeDamageMultiplierFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "reverse_fate",
            source_unit_id = "caster",
            power = 1,
            incoming_damage_multiplier = 1.25,
            outgoing_damage_multiplier = 0.75,
            @params = new GDictionary { ["damage_tag"] = "chaos" },
            stacks = 1,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["incoming_damage_multiplier"].AsDouble(),
            1.25,
            "typed incoming_damage_multiplier 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["outgoing_damage_multiplier"].AsDouble(),
            0.75,
            "typed outgoing_damage_multiplier 应在 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "runtime damage multiplier fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(
            restored.incoming_damage_multiplier ?? -1.0,
            1.25,
            "roundtrip 应保留 incoming_damage_multiplier typed 字段。"
        );
        _test.Eq(
            restored.outgoing_damage_multiplier ?? -1.0,
            0.75,
            "roundtrip 应保留 outgoing_damage_multiplier typed 字段。"
        );
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary(),
            "runtime damage multiplier formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestTypedMultiplierFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "soul_fracture",
            source_unit_id = "caster",
            power = 1,
            heal_multiplier_percent = 50,
            shield_gain_multiplier_percent = 40,
            attack_roll_penalty = 6,
            undispellable = true,
            dispellable_magic = false,
            dispellable_harmful_magic = true,
            dispellable_beneficial_magic = false,
            @params = new GDictionary { ["damage_tag"] = "negative-energy" },
            stacks = 1,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["heal_multiplier_percent"].AsInt32(),
            50,
            "typed heal_multiplier_percent 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["shield_gain_multiplier_percent"].AsInt32(),
            40,
            "typed shield_gain_multiplier_percent 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["attack_roll_penalty"].AsInt32(),
            6,
            "typed attack_roll_penalty 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["undispellable"].AsBool(),
            true,
            "typed undispellable 应在 params 边界投影。"
        );
        _test.False(
            projectedParams.ContainsKey("dispellable_magic"),
            "typed false dispellable_magic 不应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["dispellable_harmful_magic"].AsBool(),
            true,
            "typed dispellable_harmful_magic 应在 params 边界投影。"
        );
        _test.False(
            projectedParams.ContainsKey("dispellable_beneficial_magic"),
            "typed false dispellable_beneficial_magic 不应在 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "typed multiplier fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.heal_multiplier_percent ?? -1, 50, "roundtrip 应保留 typed heal multiplier。");
        _test.Eq(
            restored.shield_gain_multiplier_percent ?? -1,
            40,
            "roundtrip 应保留 typed shield multiplier。"
        );
        _test.Eq(restored.attack_roll_penalty, 6, "roundtrip 应保留 typed attack_roll_penalty。");
        _test.Eq(restored.undispellable, true, "roundtrip 应保留 typed undispellable。");
        _test.Eq(restored.dispellable_magic, false, "roundtrip 应保留 typed dispellable_magic。");
        _test.Eq(
            restored.dispellable_harmful_magic,
            true,
            "roundtrip 应保留 typed dispellable_harmful_magic。"
        );
        _test.Eq(
            restored.dispellable_beneficial_magic,
            false,
            "roundtrip 应保留 typed dispellable_beneficial_magic。"
        );
    }

    private void TestMitigationTierRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "magic_shield",
            source_unit_id = "caster",
            power = 1,
            damage_tag = "negative-energy",
            damage_tags = new List<StringName> { "fire", "freeze" },
            damage_category = "magic",
            mitigation_tier = "half",
            stacks = 1,
        };

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(Project(effect));
        _test.True(restored != null, "mitigation_tier 应通过 params 边界 roundtrip。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.mitigation_tier, new StringName("half"), "roundtrip 应保留 mitigation_tier typed 字段。");
        _test.Eq(restored.damage_tag, new StringName("negative-energy"), "roundtrip 应保留 damage_tag typed 字段。");
        _test.Eq(restored.damage_tags.Count, 2, "roundtrip 应保留 damage_tags typed 字段。");
        _test.Eq(restored.damage_tags[0], new StringName("fire"), "roundtrip 应保留 damage_tags[0]。");
        _test.Eq(restored.damage_tags[1], new StringName("freeze"), "roundtrip 应保留 damage_tags[1]。");
        _test.Eq(restored.damage_category, new StringName("magic"), "roundtrip 应保留 damage_category typed 字段。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary(),
            "damage filter / mitigation formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestSaveBonusRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "willpower_save_bonus_up",
            source_unit_id = "caster",
            power = 1,
            control_save_bonus = 2,
            save_bonus = 1,
            stacks = 1,
        };

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(Project(effect));
        _test.True(restored != null, "save_bonus/control_save_bonus 应通过 params 边界 roundtrip。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.save_bonus, 1, "roundtrip 应保留 save_bonus typed 字段。");
        _test.Eq(restored.control_save_bonus, 2, "roundtrip 应保留 control_save_bonus typed 字段。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary(),
            "save bonus formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestSaveTagListsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "save_tag_state",
            source_unit_id = "caster",
            power = 1,
            stacks = 1,
            save_advantage_tags = new List<StringName> { "poison" },
            save_disadvantage_tags = new List<StringName> { "fear" },
            save_immunity_tags = new List<StringName> { "sleep" },
            save_tags = new List<StringName> { "charm" },
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.True(projectedParams.ContainsKey("save_advantage_tags"), "save_advantage_tags 应投影到 params 边界。");
        _test.True(
            projectedParams.ContainsKey("save_disadvantage_tags"),
            "save_disadvantage_tags 应投影到 params 边界。"
        );
        _test.True(projectedParams.ContainsKey("save_immunity_tags"), "save_immunity_tags 应投影到 params 边界。");
        _test.True(projectedParams.ContainsKey("save_tags"), "save_tags 应投影到 params 边界。");

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "save tag lists 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.save_advantage_tags.Count, 1, "roundtrip 应保留 save_advantage_tags。");
        _test.Eq(restored.save_advantage_tags[0], new StringName("poison"), "roundtrip 应保留 save_advantage_tags 内容。");
        _test.Eq(restored.save_disadvantage_tags.Count, 1, "roundtrip 应保留 save_disadvantage_tags。");
        _test.Eq(restored.save_disadvantage_tags[0], new StringName("fear"), "roundtrip 应保留 save_disadvantage_tags 内容。");
        _test.Eq(restored.save_immunity_tags.Count, 1, "roundtrip 应保留 save_immunity_tags。");
        _test.Eq(restored.save_immunity_tags[0], new StringName("sleep"), "roundtrip 应保留 save_immunity_tags 内容。");
        _test.Eq(restored.save_tags.Count, 1, "roundtrip 应保留 save_tags。");
        _test.Eq(restored.save_tags[0], new StringName("charm"), "roundtrip 应保留 save_tags 内容。");
    }

    private void TestFixedMitigationFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "vajra_guard",
            source_unit_id = "caster",
            power = 1,
            @params = new GDictionary { ["damage_tag"] = "physical_slash" },
            stacks = 1,
            dr_bypass_tag = "armor_pierce",
            passive_reduction = 3,
            content_dr = 4,
            guard_block = 5,
        };

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(Project(effect));
        _test.True(restored != null, "fixed mitigation typed 字段 roundtrip 后不应丢失。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.passive_reduction, 3, "roundtrip 应保留 passive_reduction typed 字段。");
        _test.Eq(restored.content_dr, 4, "roundtrip 应保留 content_dr typed 字段。");
        _test.Eq(restored.guard_block, 5, "roundtrip 应保留 guard_block typed 字段。");
        _test.Eq(restored.dr_bypass_tag, new StringName("armor_pierce"), "roundtrip 应保留 dr_bypass_tag typed 字段。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary(),
            "fixed mitigation formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestLockDodgeBonusRoundTripAsTypedField()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "blind",
            source_unit_id = "caster",
            power = 1,
            @params = new GDictionary { ["breaks_barrier_layer"] = "indigo" },
            stacks = 1,
            lock_dodge_bonus = true,
        };

        GDictionary payload = Project(effect);
        _test.True(
            payload.ContainsKey("lock_dodge_bonus"),
            "typed lock_dodge_bonus 应通过正式状态字段序列化。"
        );
        _test.False(
            payload["params"].AsGodotDictionary().ContainsKey("lock_dodge_bonus"),
            "typed lock_dodge_bonus 不应回退到 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "typed lock_dodge_bonus roundtrip 后应恢复对象。");
        if (restored == null)
        {
            return;
        }

        _test.True(restored.lock_dodge_bonus, "roundtrip 应保留 typed lock_dodge_bonus。");
    }

    private void TestBodySizeOverrideRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "giant_form",
            source_unit_id = "caster",
            power = 1,
            body_size_category_override = BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
            previous_body_size_category = BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
            @params = new GDictionary { ["display_name"] = "巨神化" },
            stacks = 1,
            duration = 80,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["body_size_category_override"].AsStringName(),
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
            "typed body_size_category_override 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["previous_body_size_category"].AsStringName(),
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
            "typed previous_body_size_category 应在 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "body size override typed fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(
            restored.body_size_category_override,
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
            "roundtrip 应保留 body_size_category_override typed 字段。"
        );
        _test.Eq(
            restored.previous_body_size_category,
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
            "roundtrip 应保留 previous_body_size_category typed 字段。"
        );
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["display_name"] = "巨神化",
            },
            "body size override formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestSelfSaveFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "petrified",
            source_unit_id = "caster",
            power = 1,
            self_save_dc = 15,
            self_save_ability = "constitution",
            self_save_tag = "constitution",
            self_save_roll_override = 20,
            @params = new GDictionary { ["display_name"] = "石化" },
            stacks = 1,
            duration = -1,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["self_save_dc"].AsInt32(),
            15,
            "typed self_save_dc 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["self_save_ability"].AsStringName(),
            new StringName("constitution"),
            "typed self_save_ability 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["self_save_tag"].AsStringName(),
            new StringName("constitution"),
            "typed self_save_tag 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["self_save_roll_override"].AsInt32(),
            20,
            "typed self_save_roll_override 应在 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "self save typed fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.self_save_dc, 15, "roundtrip 应保留 self_save_dc typed 字段。");
        _test.Eq(
            restored.self_save_ability,
            new StringName("constitution"),
            "roundtrip 应保留 self_save_ability typed 字段。"
        );
        _test.Eq(
            restored.self_save_tag,
            new StringName("constitution"),
            "roundtrip 应保留 self_save_tag typed 字段。"
        );
        _test.Eq(
            restored.self_save_roll_override ?? -1,
            20,
            "roundtrip 应保留 self_save_roll_override typed 字段。"
        );
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["display_name"] = "石化",
            },
            "self save formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestBarrierSourceFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "barrier_burn",
            source_unit_id = "barrier_caster",
            source_profile_id = "prismatic_sphere",
            source_layer_id = "green",
            power = 1,
            @params = new GDictionary { ["display_name"] = "屏障灼烧" },
            stacks = 1,
            counts_as_debuff_override = true,
            counts_as_debuff = true,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["source"].AsStringName(),
            new StringName("prismatic_sphere"),
            "typed source_profile_id 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["layer_id"].AsStringName(),
            new StringName("green"),
            "typed source_layer_id 应在 params 边界投影。"
        );
        _test.Eq(
            payload["counts_as_debuff_override"].AsBool(),
            true,
            "typed counts_as_debuff_override 应继续走正式 schema 字段。"
        );
        _test.Eq(
            payload["counts_as_debuff"].AsBool(),
            true,
            "typed counts_as_debuff 应继续走正式 schema 字段。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "barrier source typed fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(
            restored.source_profile_id,
            new StringName("prismatic_sphere"),
            "roundtrip 应保留 source_profile_id typed 字段。"
        );
        _test.Eq(
            restored.source_layer_id,
            new StringName("green"),
            "roundtrip 应保留 source_layer_id typed 字段。"
        );
        _test.True(
            restored.counts_as_debuff_override && restored.counts_as_debuff,
            "roundtrip 应保留 typed counts_as_debuff override。"
        );
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["display_name"] = "屏障灼烧",
            },
            "barrier source formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestStatusSourceSkillFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "death_ward",
            source_unit_id = "caster",
            source_skill_id = "warrior_last_stand",
            source_skill_level = 7,
            power = 1,
            @params = new GDictionary { ["display_name"] = "不屈护命" },
            stacks = 1,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["source_skill_id"].AsStringName(),
            new StringName("warrior_last_stand"),
            "typed source_skill_id 应在 params 边界投影。"
        );
        _test.Eq(
            projectedParams["skill_level"].AsInt32(),
            7,
            "typed source_skill_level 应在 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "status source skill typed fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(
            restored.source_skill_id,
            new StringName("warrior_last_stand"),
            "roundtrip 应保留 source_skill_id typed 字段。"
        );
        _test.Eq(
            restored.source_skill_level ?? -1,
            7,
            "roundtrip 应保留 source_skill_level typed 字段。"
        );
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["display_name"] = "不屈护命",
            },
            "status source skill formal keys 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestStatusStackFieldsRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "armor_break",
            source_unit_id = "source",
            stack_behavior = "add",
            stack_limit = 20,
            power = 2,
            @params = new GDictionary { ["residual"] = "kept" },
            stacks = 2,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);

        _test.True(restored != null, "status stack typed fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.stack_behavior, new StringName("add"), "roundtrip 应保留 stack_behavior typed 字段。");
        _test.Eq(restored.stack_limit, 20, "roundtrip 应保留 stack_limit typed 字段。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary { ["residual"] = "kept" },
            "status stack formal keys 不应继续滞留在 owner 内部 @params。"
        );
        _test.Eq(
            projectedParams["stack_behavior"].AsStringName(),
            new StringName("add"),
            "typed stack_behavior 应在 params 边界投影。"
        );
        _test.Eq(projectedParams["stack_limit"].AsInt32(), 20, "typed stack_limit 应在 params 边界投影。");
    }

    private void TestStatusRangeBonusRoundTripThroughParamsBoundary()
    {
        BattleStatusEffectState effect = new()
        {
            status_id = "archer_shooting_specialization",
            source_unit_id = "archer",
            power = 1,
            range_bonus = 1,
            @params = new GDictionary { ["display_name"] = "射击专精" },
            stacks = 1,
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.Eq(
            projectedParams["range_bonus"].AsInt32(),
            1,
            "typed range_bonus 应在 params 边界投影。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "status range_bonus typed field 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.range_bonus, 1, "roundtrip 应保留 range_bonus typed 字段。");
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["display_name"] = "射击专精",
            },
            "range_bonus formal key 不应继续滞留在 owner 内部 @params。"
        );
    }

    private void TestTemporalTagFieldsRoundTripThroughParamsBoundary()
    {
        StringName temporalTag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Temporal);
        BattleStatusEffectState effect = new()
        {
            status_id = "time_reverberation",
            source_unit_id = "temporal_caster",
            power = 1,
            @params = new GDictionary { ["display_name"] = "时之余波" },
            stacks = 1,
            duration = 60,
            status_tags = new List<StringName> { temporalTag },
            save_bonus_by_tag = new Dictionary<StringName, int> { [temporalTag] = 5 },
        };

        GDictionary payload = Project(effect);
        GDictionary projectedParams = payload["params"].AsGodotDictionary();
        _test.True(projectedParams.ContainsKey("status_tags"), "status_tags 应投影到 params 边界。");
        _test.True(
            projectedParams.ContainsKey("save_bonus_by_tag"),
            "save_bonus_by_tag 应投影到 params 边界。"
        );
        GDictionary projectedBonusByTag =
            projectedParams["save_bonus_by_tag"].AsGodotDictionary();
        _test.True(
            projectedBonusByTag.ContainsKey(temporalTag)
            && projectedBonusByTag[temporalTag].AsInt32() == 5,
            "save_bonus_by_tag 应保留 StringName key 与 int value 的正式 payload 语义。"
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(restored != null, "temporal tag typed fields 通过 params 边界后应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.status_tags.Count, 1, "roundtrip 应保留 status_tags。");
        _test.Eq(restored.status_tags[0], temporalTag, "roundtrip 应保留 status_tags 内容。");
        _test.Eq(restored.save_bonus_by_tag.Count, 1, "roundtrip 应保留 save_bonus_by_tag。");
        _test.Eq(
            restored.save_bonus_by_tag.GetValueOrDefault(temporalTag, 0),
            5,
            "roundtrip 应保留 save_bonus_by_tag 内容。"
        );
        AssertDictionaryEq(
            ProjectParams(restored),
            new GDictionary
            {
                ["display_name"] = "时之余波",
            },
            "temporal tag formal keys 不应继续滞留在 owner 内部 @params。"
        );

        BattleStatusEffectState duplicate = restored.DuplicateState();
        _test.Eq(duplicate.status_tags.Count, 1, "duplicate_state 应保留 status_tags。");
        _test.Eq(
            duplicate.save_bonus_by_tag.GetValueOrDefault(temporalTag, 0),
            5,
            "duplicate_state 应保留 save_bonus_by_tag。"
        );

        // string key 不应通过 fallback 恢复进 typed map（无旧 payload fallback）。
        GDictionary stringKeyPayload = ValidPayload();
        GDictionary stringKeyParams = stringKeyPayload["params"].AsGodotDictionary();
        stringKeyParams["save_bonus_by_tag"] = new GDictionary { ["temporal"] = 5 };
        BattleStatusEffectState stringKeyRestored = BattleStatusEffectState.FromDictionary(
            stringKeyPayload
        );
        _test.True(stringKeyRestored != null, "string-key 探针 payload 应能恢复对象。");
        _test.Eq(
            stringKeyRestored?.save_bonus_by_tag.Count ?? -1,
            0,
            "save_bonus_by_tag 的 string key 不应通过 fallback 恢复进 typed map。"
        );
    }

    private static GDictionary ValidPayload()
    {
        return new GDictionary
        {
            ["status_id"] = "burning",
            ["source_unit_id"] = "caster",
            ["power"] = 3,
            ["params"] = new GDictionary { ["damage_tag"] = "fire" },
            ["stacks"] = 1,
        };
    }

    private static GDictionary PayloadWith(string fieldName, Variant value)
    {
        GDictionary payload = ValidPayload();
        payload[fieldName] = value;
        return payload;
    }

    private void AssertDictionaryEq(GDictionary actual, GDictionary expected, string message)
    {
        if (!DictionaryEquals(actual, expected))
        {
            _test.Fail($"{message} | actual={Variant.From(actual)} expected={Variant.From(expected)}");
        }
    }

    private static bool DictionaryEquals(GDictionary actual, GDictionary expected)
    {
        if (actual == null || expected == null || actual.Count != expected.Count)
        {
            return false;
        }
        foreach (Variant key in expected.Keys)
        {
            string keyText = key.AsString();
            if (
                !TryGetDictionaryValue(expected, keyText, out Variant expectedValue)
                || !TryGetDictionaryValue(actual, keyText, out Variant actualValue)
            )
            {
                return false;
            }
            if (!VariantEquals(actualValue, expectedValue))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, string key, out Variant value)
    {
        foreach (Variant candidateKey in dictionary.Keys)
        {
            if (candidateKey.AsString() == key)
            {
                value = dictionary[candidateKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool VariantEquals(Variant actual, Variant expected)
    {
        if (actual.VariantType == Variant.Type.Dictionary && expected.VariantType == Variant.Type.Dictionary)
        {
            return DictionaryEquals(actual.AsGodotDictionary(), expected.AsGodotDictionary());
        }
        if (IsStringLike(actual) && IsStringLike(expected))
        {
            return actual.AsString() == expected.AsString();
        }
        if (actual.VariantType == Variant.Type.Int && expected.VariantType == Variant.Type.Int)
        {
            return actual.AsInt64() == expected.AsInt64();
        }
        if (actual.VariantType == Variant.Type.Bool && expected.VariantType == Variant.Type.Bool)
        {
            return actual.AsBool() == expected.AsBool();
        }
        if (actual.VariantType == Variant.Type.Float && expected.VariantType == Variant.Type.Float)
        {
            return Mathf.IsEqualApprox(actual.AsDouble(), expected.AsDouble());
        }
        return actual.VariantType == expected.VariantType && actual.AsString() == expected.AsString();
    }

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType is Variant.Type.String or Variant.Type.StringName;
    }
}
