using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_unit_state_schema_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<GodotProjectionLease<GDictionary>> _payloadLeases = new();

    public override void _Initialize()
    {
        TestValidRoundtripPreservesCurrentPayload();
        TestClonePreservesEphemeralChargeState();
        TestEffectiveTraitPayloadRoundtripAndClone();
        TestEquipmentAbilitySourcePayloadRoundtripAndClone();
        TestClonePreservesPendingCastRuntimeStateWithoutSerialization();
        TestTypedChargeAndFumbleHelpers();
        TestExtendedBodySizeCategoriesRoundtrip();
        TestRejectsEmptyMissingAndExtraFields();
        TestRejectsWrongTopLevelTypes();
        TestRejectsStringNumericValues();
        TestRejectsBadStringNameArrays();
        TestRejectsBadIdentityProjectionFields();
        TestRejectsBadEffectiveTraitPayloads();
        TestRejectsBadEquipmentAbilitySourcePayloads();
        TestRejectsBadCombatResourceUnlocks();
        TestRejectsBadStatusEffectEntries();
        TestOwnerInternalStatusMapIgnoresMalformedRawKeys();
        TestRejectsEquipmentViewBadPayload();
        TestRejectsBadWeaponDicePayloads();
        TestBodySizeRulesWrapperIsRemoved();

        DisposePayloadLeases();
        RequestTestExit(_test.Finish("Battle unit state schema regression"));
    }

    private void TestValidRoundtripPreservesCurrentPayload()
    {
        BattleUnitState unit = BuildUnit();
        _test.True(unit != null, "BuildUnit 应返回单位。");
        GDictionary payload = Project(unit);
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "当前 to_dict payload 应可由 from_dict 恢复。");
        _test.Eq(restored?.current_move_points ?? -1, 5, "current_move_points 应保留大于默认值的 int。");
        _test.Eq(
            restored?.body_size_category.ToString() ?? "",
            "large",
            "body_size_category 应随 body_size round-trip。"
        );
        AssertVariantEq(
            restored?.vision_tags,
            new GStringNameArray { "darkvision" },
            "vision_tags 应 round-trip。"
        );
        _test.Eq(
            restored?.damage_resistances.Get("fire").ToString() ?? "",
            "half",
            "damage_resistances 应 round-trip。"
        );
        using GDictionary cooldownPayload = payload["cooldowns"].AsGodotDictionary();
        _test.True(
            HasKeyWithType(cooldownPayload, "slash", Variant.Type.StringName),
            "cooldowns projection 应保持正式 StringName key shape。"
        );
        AssertVariantEq(
            Project(restored),
            payload,
            "BattleUnitState 应保持 to_dict/from_dict round-trip。"
        );
    }

    private void TestClonePreservesEphemeralChargeState()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.current_move_points = 5;
        unit.per_battle_charges.Put("dragon_breath", 1);
        unit.per_turn_charges.Put("nimble_escape", 1);
        unit.per_turn_charge_limits.Put("nimble_escape", 1);

        BattleUnitState cloned = unit.clone();
        _test.True(cloned != null, "BattleUnitState.clone() 应返回可用副本。");
        if (cloned == null)
            return;

        AssertVariantEq(Project(cloned), Project(unit), "clone 应保留序列化字段。");
        _test.Eq(cloned.per_battle_charges.Get("dragon_breath", -1), 1, "clone 应深拷贝 per_battle_charges。");
        _test.Eq(cloned.per_turn_charges.Get("nimble_escape", -1), 1, "clone 应深拷贝 per_turn_charges。");
        _test.Eq(cloned.per_turn_charge_limits.Get("nimble_escape", -1), 1, "clone 应深拷贝 per_turn_charge_limits。");

        cloned.per_battle_charges["dragon_breath"] = 0;
        cloned.per_turn_charges["nimble_escape"] = 0;
        cloned.per_turn_charge_limits["nimble_escape"] = 0;
        _test.Eq(unit.per_battle_charges.Get("dragon_breath", -1), 1, "clone 不应共享 per_battle_charges 字典。");
        _test.Eq(unit.per_turn_charges.Get("nimble_escape", -1), 1, "clone 不应共享 per_turn_charges 字典。");
        _test.Eq(unit.per_turn_charge_limits.Get("nimble_escape", -1), 1, "clone 不应共享 per_turn_charge_limits 字典。");
    }

    private void TestEffectiveTraitPayloadRoundtripAndClone()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.effective_trait_instances = TraitTestData.EffectiveTraits(
            TraitTestData.EffectiveTrait(
                "halfling_luck",
                "hero_trait_001",
                "on_natural_one",
                "per_turn",
                "turn_start",
                effectType: "halfling_luck",
                sourceType: "character",
                sourceId: "hero"
            ),
            TraitTestData.EffectiveTrait(
                "savage_attacks",
                "eq_000001_t01",
                "on_crit",
                "none",
                "none",
                effectType: "savage_attacks",
                sourceType: "equipment_roll",
                sourceId: "eq_000001",
                rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 4))
            )
        );
        unit.effective_trait_ids = new GStringNameArray { "halfling_luck", "savage_attacks" };

        GDictionary payload = Project(unit);
        using GArray traitPayloads = payload["effective_trait_instances"].AsGodotArray();
        using GDictionary savageAttacksPayload = traitPayloads[1].AsGodotDictionary();
        using GDictionary rollValuesPayload = savageAttacksPayload["roll_values"]
            .AsGodotDictionary();
        _test.True(
            HasKeyWithType(rollValuesPayload, "amount", Variant.Type.StringName),
            "effective trait roll_values projection 应保持正式 StringName key shape。"
        );
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "effective trait payload 应可 round-trip。");
        _test.Eq(restored?.effective_trait_instances.Count ?? -1, 2, "effective trait payload 数量应保留。");
        _test.Eq(restored?.effective_trait_ids.Count ?? -1, 2, "effective_trait_ids 派生投影应保留。");

        BattleUnitState cloned = unit.clone();
        _test.Eq(cloned.effective_trait_instances.Count, 2, "clone 应保留 effective trait payload。");
        cloned.effective_trait_instances[0].trait_id = "mutated";
        _test.Eq(
            unit.effective_trait_instances[0].trait_id,
            new StringName("halfling_luck"),
            "clone 不应共享 effective trait state。"
        );
    }

    private void TestEquipmentAbilitySourcePayloadRoundtripAndClone()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.creature_type_tags = new GStringNameArray { "undead", "construct" };
        unit.equipment_ability_sources = new List<BattleEquipmentAbilitySourceState>
        {
            new()
            {
                EffectiveInstanceKey = "equipment_fixed::eq_000001::trait.weapon.flame",
                EquipmentDefId = "test_blade",
                SourceEquipmentInstanceId = "eq_000001",
                SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                AbilityIds = new List<StringName> { "binding.weapon.flame" },
            },
        };

        GDictionary payload = Project(unit);
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "equipment ability source payload 应可 round-trip。");
        _test.Eq(
            restored?.equipment_ability_sources.Count ?? -1,
            1,
            "equipment ability source 数量应保留。"
        );
        BattleEquipmentAbilitySourceState restoredSource =
            restored?.equipment_ability_sources.Count > 0
                ? restored.equipment_ability_sources[0]
                : null;
        _test.Eq(
            restoredSource?.EffectiveInstanceKey ?? "",
            "equipment_fixed::eq_000001::trait.weapon.flame",
            "equipment ability source 应保留 effective instance key。"
        );
        _test.Eq(
            restoredSource?.SourceEquipmentInstanceId ?? "",
            "eq_000001",
            "player equipment ability source 应保留持久装备 instance id。"
        );
        _test.Eq(
            restoredSource?.SourceKind ?? EquipmentAbilitySourceKind.Unknown,
            EquipmentAbilitySourceKind.PlayerPersistentEquipment,
            "player equipment ability source 应保留 source kind。"
        );
        _test.True(
            restoredSource != null && restoredSource.AbilityIds.Contains("binding.weapon.flame"),
            "equipment ability source 应保留 ability/binding id。"
        );
        _test.True(
            restored != null && restored.creature_type_tags.Contains("undead"),
            "creature_type_tags 应随 unit payload round-trip。"
        );

        BattleUnitState cloned = unit.clone();
        _test.Eq(
            cloned.equipment_ability_sources.Count,
            1,
            "clone 应保留 equipment ability source payload。"
        );
        cloned.equipment_ability_sources[0].AbilityIds[0] = "mutated.binding";
        cloned.creature_type_tags.Add("mutated_creature");
        _test.Eq(
            unit.equipment_ability_sources[0].AbilityIds[0],
            new StringName("binding.weapon.flame"),
            "clone 不应共享 equipment ability source ability id list。"
        );
        _test.True(
            !unit.creature_type_tags.Contains("mutated_creature"),
            "clone 不应共享 creature_type_tags。"
        );
    }

    private void TestClonePreservesPendingCastRuntimeStateWithoutSerialization()
    {
        BattleUnitState unit = BuildMinimalUnit();
        var pendingCast = new BattlePendingCastState
        {
            SourceUnitId = unit.unit_id,
            SkillId = "slow_bolt",
            VariantId = "",
            TargetMode = BattleTargetMode.Unit,
            BindingMode = PendingCastBindingModeKind.SoftAnchor,
            StartedCoord = new Vector2I(1, 2),
            StartedTu = 20,
            BaseCastingTimeTu = 30,
            RemainingCastProgress = 15,
            LastMaintenanceCheckpointHp = 40,
            CastSequence = 3,
            CostTransaction = new SkillCostTransaction
            {
                SkillId = "slow_bolt",
                SkillLevel = 2,
                ApCost = 1,
                MpCost = 10,
                StaminaCost = 4,
                AuraCost = 2,
                CooldownTurns = 20,
            },
        };
        pendingCast.SetTargetUnitIds(new[] { new StringName("target_a") });
        pendingCast.SetTargetCoords(new[] { new Vector2I(3, 4) });
        unit.SetPendingCast(pendingCast);
        unit.turn_casting_exhausted = true;

        GDictionary payload = Project(unit);
        _test.True(!payload.ContainsKey("pending_cast"), "pending_cast 是 runtime-only 字段，不应进入 unit payload。");
        _test.True(!payload.ContainsKey("turn_casting_exhausted"), "turn_casting_exhausted 是 runtime-only 字段，不应进入 unit payload。");
        _test.True(
            BattleUnitState.FromDictionary(payload)?.pending_cast == null,
            "从 payload 恢复时不应恢复 pending_cast runtime 状态。"
        );

        BattleUnitState cloned = unit.clone();
        _test.True(cloned?.pending_cast != null, "clone 应保留 pending_cast runtime 状态。");
        _test.True(cloned?.turn_casting_exhausted == true, "clone 应保留 turn_casting_exhausted runtime 状态。");
        _test.Eq(cloned?.pending_cast?.SkillId.ToString() ?? "", "slow_bolt", "clone 应保留 pending cast skill id。");
        _test.Eq(cloned?.pending_cast?.TargetUnitIds.Count ?? -1, 1, "clone 应保留 pending cast 目标。");
        _test.Eq(cloned?.pending_cast?.CostTransaction?.MpCost ?? -1, 10, "clone 应保留 pending cast 成本事务。");

        cloned?.pending_cast?.RemoveTargetUnitId("target_a");
        if (cloned?.pending_cast?.CostTransaction != null)
            cloned.pending_cast.CostTransaction.MpCost = 1;
        _test.Eq(unit.pending_cast?.TargetUnitIds.Count ?? -1, 1, "pending cast clone 不应共享目标列表。");
        _test.Eq(unit.pending_cast?.CostTransaction?.MpCost ?? -1, 10, "pending cast clone 不应共享成本事务。");
    }

    private void TestTypedChargeAndFumbleHelpers()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.SetPerBattleChargeTyped("dragon_breath", 2);
        unit.SetPerTurnChargeLimitTyped("nimble_escape", 3);
        unit.SetPerTurnChargeTyped("nimble_escape", 1);
        unit.SetFumbleProtectionUsedTyped("mage_fireball", 1);

        _test.True(unit.HasPerBattleChargeTyped("dragon_breath"), "typed per-battle charge helper 应标记已初始化 charge。");
        _test.Eq(unit.GetPerBattleChargeTyped("dragon_breath", -1), 2, "typed per-battle charge helper 应返回当前次数。");
        _test.True(unit.HasPerTurnChargeLimitTyped("nimble_escape"), "typed per-turn charge limit helper 应标记已初始化 limit。");
        _test.Eq(unit.GetPerTurnChargeLimitTyped("nimble_escape", -1), 3, "typed per-turn charge limit helper 应返回当前上限。");
        _test.Eq(unit.GetPerTurnChargeTyped("nimble_escape", -1), 1, "typed per-turn charge helper 应返回当前次数。");
        _test.Eq(unit.GetFumbleProtectionUsedTyped("mage_fireball", -1), 1, "typed fumble helper 应返回当前保护消耗次数。");

        unit.ResetPerTurnCharges();
        _test.Eq(unit.GetPerTurnChargeTyped("nimble_escape", -1), 3, "reset_per_turn_charges 应通过 typed limit helper 回满 per-turn charges。");
    }

    private void TestExtendedBodySizeCategoriesRoundtrip()
    {
        BattleUnitState tiny = BuildMinimalUnit();
        _test.True(tiny != null, "body size fixture 应可构建。");
        _test.True(tiny.SetBodySizeCategory("tiny"), "tiny category 应可设置。");
        GDictionary tinyPayload = Project(tiny);
        _test.Eq(DictString(tinyPayload, "body_size_category"), "tiny", "to_dict 应保留 tiny category。");
        _test.Eq(DictInt(tinyPayload, "body_size"), BodySizeContentRules.ToBodySize(BodySizeCategoryKind.Tiny), "tiny 应映射到 typed body-size int。");
        _test.Eq(DictVector2I(tinyPayload, "footprint_size"), Vector2I.One, "tiny footprint 应为 1x1。");
        _test.True(BattleUnitState.FromDictionary(tinyPayload) != null, "tiny payload 应可 round-trip。");

        BattleUnitState gargantuan = BuildMinimalUnit();
        _test.True(
            gargantuan.SetBodySizeCategory(BodySizeContentRules.ToStringName(BodySizeCategoryKind.Gargantuan)),
            "gargantuan category 应可设置。"
        );
        GDictionary gargantuanPayload = Project(gargantuan);
        _test.Eq(
            DictInt(gargantuanPayload, "body_size"),
            BodySizeContentRules.ToBodySize(BodySizeCategoryKind.Gargantuan),
            "gargantuan 应映射到 typed body-size int。"
        );
        _test.Eq(
            DictVector2I(gargantuanPayload, "footprint_size"),
            new Vector2I(4, 4),
            "gargantuan footprint 应为 4x4。"
        );
        _test.Eq(
            DictArray(gargantuanPayload, "occupied_coords").Count,
            16,
            "gargantuan 应占 16 格。"
        );
        _test.True(BattleUnitState.FromDictionary(gargantuanPayload) != null, "gargantuan payload 应可 round-trip。");

        BattleUnitState boss = BuildMinimalUnit();
        _test.True(boss.SetBodySizeCategory(BodySizeContentRules.ToStringName(BodySizeCategoryKind.Boss)), "boss category 应可设置。");
        GDictionary bossPayload = Project(boss);
        _test.Eq(DictInt(bossPayload, "body_size"), BodySizeContentRules.ToBodySize(BodySizeCategoryKind.Boss), "boss 应映射到 typed body-size int。");
        _test.Eq(DictVector2I(bossPayload, "footprint_size"), new Vector2I(5, 5), "boss footprint 应为 5x5。");
        _test.True(BattleUnitState.FromDictionary(bossPayload) != null, "boss payload 应可 round-trip。");
    }

    private void TestRejectsEmptyMissingAndExtraFields()
    {
        _test.True(BattleUnitState.FromDictionary(new GDictionary()) == null, "空 Dictionary payload 应拒绝。");

        GDictionary missing = Payload();
        missing.Remove("footprint_size");
        AssertRejected(missing, "缺少当前 to_dict 字段应拒绝。");

        GDictionary extra = Payload();
        extra["legacy_body_size"] = 1;
        AssertRejected(extra, "包含额外旧字段应拒绝。");
    }

    private void TestRejectsWrongTopLevelTypes()
    {
        GDictionary badCoord = Payload();
        badCoord["coord"] = "0,0";
        AssertRejected(badCoord, "coord 非 Vector2i 应拒绝。");

        GDictionary badFootprint = Payload();
        badFootprint["footprint_size"] = Vector2I.One;
        AssertRejected(badFootprint, "footprint_size 与 body_size 刷新结果不一致应拒绝。");

        GDictionary badOccupied = Payload();
        badOccupied["occupied_coords"] = new GArray { new Vector2I(9, 9) };
        AssertRejected(badOccupied, "occupied_coords 与 coord/body_size 刷新结果不一致应拒绝。");

        GDictionary badBool = Payload();
        badBool["is_alive"] = "true";
        AssertRejected(badBool, "bool 字段使用字符串应拒绝。");

        GDictionary badRequiredId = Payload();
        badRequiredId["unit_id"] = "";
        AssertRejected(badRequiredId, "必填 String/StringName 为空应拒绝。");

        GDictionary badWeaponFamily = Payload();
        badWeaponFamily["weapon_family"] = 7;
        AssertRejected(badWeaponFamily, "weapon_family 非 String/StringName 应拒绝。");
    }

    private void TestRejectsStringNumericValues()
    {
        foreach (string fieldName in new[] { "current_hp", "current_ap", "aura_max", "weapon_attack_range", "last_turn_tu" })
        {
            GDictionary payload = Payload();
            payload[fieldName] = "7";
            AssertRejected(payload, $"{fieldName} 使用字符串数字应拒绝。");
        }

        GDictionary badMovePoints = Payload();
        badMovePoints["current_move_points"] = -1;
        AssertRejected(badMovePoints, "current_move_points 负数应拒绝。");

        GDictionary badAttribute = Payload();
        DictDictionary(badAttribute, "attribute_snapshot")["strength"] = "3";
        AssertRejected(badAttribute, "attribute_snapshot value 非 int 应拒绝。");

        GDictionary badSkillLevel = Payload();
        DictDictionary(badSkillLevel, "known_skill_level_map")["slash"] = "2";
        AssertRejected(badSkillLevel, "known_skill_level_map value 非 int 应拒绝。");
    }

    private void TestRejectsBadStringNameArrays()
    {
        GDictionary emptySkillId = Payload();
        emptySkillId["known_active_skill_ids"] = new GArray { "slash", "" };
        AssertRejected(emptySkillId, "known_active_skill_ids 空元素应拒绝。");

        GDictionary duplicateSkillId = Payload();
        duplicateSkillId["known_active_skill_ids"] = new GArray { "slash", "slash" };
        AssertRejected(duplicateSkillId, "known_active_skill_ids 重复元素应拒绝。");

        GDictionary badMovementTag = Payload();
        badMovementTag["movement_tags"] = new GArray { "grounded", 3 };
        AssertRejected(badMovementTag, "movement_tags 非 String/StringName 元素应拒绝。");

        GDictionary oldTraitField = Payload();
        oldTraitField["race_trait_ids"] = new GArray();
        AssertRejected(oldTraitField, "旧 race_trait_ids 字段应作为 extra field 拒绝。");

        GDictionary badSaveAdvantageTag = Payload();
        badSaveAdvantageTag["save_advantage_tags"] = new GArray { "charm", "" };
        AssertRejected(badSaveAdvantageTag, "save_advantage_tags 空元素应拒绝。");
    }

    private void TestRejectsBadIdentityProjectionFields()
    {
        GDictionary categoryMismatch = Payload();
        categoryMismatch["body_size_category"] = "medium";
        AssertRejected(categoryMismatch, "body_size_category 与 body_size 不一致应拒绝。");

        GDictionary invalidCategory = Payload();
        invalidCategory["body_size_category"] = "colossal";
        AssertRejected(invalidCategory, "非法 body_size_category 应拒绝。");

        GDictionary badDamageKey = Payload();
        DictDictionary(badDamageKey, "damage_resistances")[3] = "half";
        AssertRejected(badDamageKey, "damage_resistances 非字符串 key 应拒绝。");

        GDictionary badDamageValue = Payload();
        DictDictionary(badDamageValue, "damage_resistances")["fire"] = "quarter";
        AssertRejected(badDamageValue, "damage_resistances 非法 mitigation tier 应拒绝。");
    }

    private void TestRejectsBadEffectiveTraitPayloads()
    {
        GDictionary duplicateKey = Payload();
        duplicateKey["effective_trait_instances"] = new GArray
        {
            EffectiveTraitPayload(
                "halfling_luck",
                "halfling_luck",
                "dup_key",
                "character",
                "hero",
                "on_natural_one",
                "per_turn",
                "turn_start"
            ),
            EffectiveTraitPayload(
                "halfling_luck",
                "halfling_luck",
                "dup_key",
                "character",
                "hero",
                "on_natural_one",
                "per_turn",
                "turn_start"
            ),
        };
        duplicateKey["effective_trait_ids"] = new GArray { "halfling_luck" };
        AssertRejected(duplicateKey, "effective_trait_instances 重复 effective_instance_key 应拒绝。");

        GDictionary mismatchedIds = Payload();
        mismatchedIds["effective_trait_instances"] = new GArray
        {
            EffectiveTraitPayload(
                "halfling_luck",
                "halfling_luck",
                "hero_trait_001",
                "character",
                "hero",
                "on_natural_one",
                "per_turn",
                "turn_start"
            ),
        };
        mismatchedIds["effective_trait_ids"] = new GArray { "savage_attacks" };
        AssertRejected(mismatchedIds, "effective_trait_ids 与 payload 派生集合不一致应拒绝。");

        GDictionary invalidEffect = Payload();
        GDictionary invalidEntry = EffectiveTraitPayload(
            "halfling_luck",
            "unsupported_effect",
            "hero_trait_001",
            "character",
            "hero",
            "on_natural_one",
            "per_turn",
            "turn_start"
        );
        invalidEffect["effective_trait_instances"] = new GArray { invalidEntry };
        invalidEffect["effective_trait_ids"] = new GArray { "halfling_luck" };
        AssertRejected(invalidEffect, "effective trait payload 非法 effect_type 应拒绝。");

        GDictionary extraField = Payload();
        GDictionary extraEntry = EffectiveTraitPayload(
            "halfling_luck",
            "halfling_luck",
            "hero_trait_001",
            "character",
            "hero",
            "on_natural_one",
            "per_turn",
            "turn_start"
        );
        extraEntry["legacy_trait_id"] = "halfling_luck";
        extraField["effective_trait_instances"] = new GArray { extraEntry };
        extraField["effective_trait_ids"] = new GArray { "halfling_luck" };
        AssertRejected(extraField, "effective trait payload entry 额外字段应拒绝。");
    }

    private void TestRejectsBadEquipmentAbilitySourcePayloads()
    {
        GDictionary badSourceKind = Payload();
        badSourceKind["equipment_ability_sources"] = new GArray
        {
            EquipmentAbilitySourcePayload(
                "equipment_fixed::eq_000001::trait.weapon.flame",
                "test_blade",
                "eq_000001",
                "legacy_source",
                new GArray { "binding.weapon.flame" }
            ),
        };
        AssertRejected(badSourceKind, "equipment ability source 非法 source_kind 应拒绝。");

        GDictionary missingPlayerInstance = Payload();
        missingPlayerInstance["equipment_ability_sources"] = new GArray
        {
            EquipmentAbilitySourcePayload(
                "equipment_fixed::eq_000001::trait.weapon.flame",
                "test_blade",
                "",
                "player_persistent_equipment",
                new GArray { "binding.weapon.flame" }
            ),
        };
        AssertRejected(missingPlayerInstance, "player equipment ability source 缺 instance id 应拒绝。");

        GDictionary enemyWithPersistentInstance = Payload();
        enemyWithPersistentInstance["equipment_ability_sources"] = new GArray
        {
            EquipmentAbilitySourcePayload(
                "enemy_battle_only_equipment::enemy_01::test_blade::trait.weapon.flame",
                "test_blade",
                "eq_000001",
                "enemy_battle_only_equipment",
                new GArray { "binding.weapon.flame" }
            ),
        };
        AssertRejected(enemyWithPersistentInstance, "enemy battle-only equipment source 不应携带持久 instance id。");

        GDictionary duplicateAbilityIds = Payload();
        duplicateAbilityIds["equipment_ability_sources"] = new GArray
        {
            EquipmentAbilitySourcePayload(
                "equipment_fixed::eq_000001::trait.weapon.flame",
                "test_blade",
                "eq_000001",
                "player_persistent_equipment",
                new GArray { "binding.weapon.flame", "binding.weapon.flame" }
            ),
        };
        AssertRejected(duplicateAbilityIds, "equipment ability source ability_ids 重复应拒绝。");

        GDictionary badCreatureTag = Payload();
        badCreatureTag["creature_type_tags"] = new GArray { "undead", "" };
        AssertRejected(badCreatureTag, "creature_type_tags 空元素应拒绝。");
    }

    private void TestRejectsBadCombatResourceUnlocks()
    {
        GDictionary missingHp = Payload();
        missingHp["unlocked_combat_resource_ids"] = new GArray { "stamina" };
        AssertRejected(missingHp, "unlocked_combat_resource_ids 缺 hp 应拒绝。");

        GDictionary missingStamina = Payload();
        missingStamina["unlocked_combat_resource_ids"] = new GArray { "hp" };
        AssertRejected(missingStamina, "unlocked_combat_resource_ids 缺 stamina 应拒绝。");

        GDictionary illegalResource = Payload();
        illegalResource["unlocked_combat_resource_ids"] = new GArray { "hp", "stamina", "rage" };
        AssertRejected(illegalResource, "unlocked_combat_resource_ids 含非法资源应拒绝。");

        GDictionary duplicateResource = Payload();
        duplicateResource["unlocked_combat_resource_ids"] = new GArray { "hp", "stamina", "hp" };
        AssertRejected(duplicateResource, "unlocked_combat_resource_ids 重复资源应拒绝。");
    }

    private void TestRejectsBadStatusEffectEntries()
    {
        GDictionary badEntry = Payload();
        DictDictionary(badEntry, "status_effects")["burning"] = "bad";
        AssertRejected(badEntry, "status_effects 坏 entry 应拒绝整份 unit payload。");

        GDictionary keyMismatch = Payload();
        DictDictionary(DictDictionary(keyMismatch, "status_effects"), "burning")["status_id"] = "slow";
        AssertRejected(keyMismatch, "status_effects key 与 payload.status_id 不一致应拒绝。");

        GDictionary emptyKey = Payload();
        GDictionary statusEffects = DictDictionary(emptyKey, "status_effects");
        statusEffects[""] = statusEffects["burning"];
        statusEffects.Remove("burning");
        AssertRejected(emptyKey, "status_effects 空 key 应拒绝。");
    }

    private void TestOwnerInternalStatusMapIgnoresMalformedRawKeys()
    {
        BattleUnitState unit = BuildMinimalUnit();
        using GDictionary projectedStatusEffects =
            Project(unit)["status_effects"].AsGodotDictionary();
        projectedStatusEffects[3] = Project(
            new BattleStatusEffectState
            {
                status_id = "burning",
                source_unit_id = "malformed",
                power = 1,
                stacks = 1,
            }
        );

        _test.True(
            unit.GetStatusEffect("burning") == null,
            "BattleUnitState owner 内部不应通过坏 raw key 命中 status_effects。"
        );
        _test.Eq(
            unit.GetSortedStatusEffectIdsTyped().Count,
            0,
            "坏 raw key status entry 不应继续出现在 typed status id 枚举里。"
        );
    }

    private void TestRejectsEquipmentViewBadPayload()
    {
        GDictionary payload = Payload();
        DictDictionary(payload, "equipment_view").Remove("equipped_slots");
        AssertRejected(payload, "equipment_view 无法由 EquipmentState.from_dict 恢复时应拒绝整份 payload。");
    }

    private void TestRejectsBadWeaponDicePayloads()
    {
        GDictionary stringDice = Payload();
        DictDictionary(stringDice, "weapon_one_handed_dice")["dice_count"] = "1";
        AssertRejected(stringDice, "weapon dice 字符串数字应拒绝。");

        GDictionary missingDiceField = Payload();
        DictDictionary(missingDiceField, "weapon_one_handed_dice").Remove("flat_bonus");
        AssertRejected(missingDiceField, "weapon dice 缺字段应拒绝。");

        GDictionary extraDiceField = Payload();
        DictDictionary(extraDiceField, "weapon_one_handed_dice")["legacy_bonus"] = 1;
        AssertRejected(extraDiceField, "weapon dice 旧额外字段应拒绝。");

        GDictionary invalidSides = Payload();
        DictDictionary(invalidSides, "weapon_two_handed_dice")["dice_sides"] = 0;
        AssertRejected(invalidSides, "weapon dice dice_sides <= 0 应拒绝。");

        GDictionary invalidKind = Payload();
        invalidKind["weapon_profile_kind"] = "legacy_weapon";
        AssertRejected(invalidKind, "非法 weapon_profile_kind 应拒绝。");

        GDictionary invalidGrip = Payload();
        invalidGrip["weapon_current_grip"] = "legacy_grip";
        AssertRejected(invalidGrip, "非法 weapon_current_grip 应拒绝。");
    }

    private void TestBodySizeRulesWrapperIsRemoved()
    {
        _test.True(
            FindLoadedType("BodySizeRules") == null,
            "BodySizeRules Godot wrapper 应删除，测试和生产路径应直接使用 BodySizeContentRules 或本地 helper。"
        );
    }

    private static BattleUnitState BuildUnit()
    {
        BattleUnitState unit = new()
        {
            unit_id = "schema_unit",
            source_member_id = "member_1",
            display_name = "Schema Unit",
            faction_id = "player",
            control_mode = "manual",
            coord = new Vector2I(3, 4),
            body_size = 3,
            body_size_category = "large",
            current_hp = 21,
            current_mp = 4,
            current_stamina = 13,
            current_aura = 2,
            current_ap = 1,
            current_move_points = 5,
            unlocked_combat_resource_ids = new GStringNameArray { "hp", "stamina", "aura" },
            stamina_recovery_progress = 7,
            current_shield_hp = 4,
            shield_max_hp = 8,
            shield_duration = 30,
            shield_family = "ward",
            shield_source_unit_id = "schema_unit",
            shield_source_skill_id = "ward_skill",
            action_progress = 20,
            action_threshold = 140,
            known_active_skill_ids = new GStringNameArray { "slash" },
            movement_tags = new GStringNameArray { "grounded" },
            vision_tags = new GStringNameArray { "darkvision" },
            proficiency_tags = new GStringNameArray { "light_armor" },
            save_advantage_tags = new GStringNameArray { "charm" },
            versatility_pick = "strength",
            last_turn_tu = 50,
        };
        unit.SetKnownSkillLevelsTyped(new Dictionary<StringName, int> { ["slash"] = 2 });
        unit.damage_resistances.Put("fire", "half");
        unit.SetCooldownsTyped(new Dictionary<StringName, int> { ["slash"] = 12 });
        unit.attribute_snapshot.SetValue("strength", 3);
        unit.attribute_snapshot.SetValue("aura_max", 6);
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "training_longsword",
                weapon_profile_type_id = "longsword",
                weapon_family = "sword",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 2,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 0,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 10,
                    flat_bonus = 1,
                },
                weapon_is_versatile = true,
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
        BattleStatusEffectState effect = new()
        {
            status_id = "burning",
            source_unit_id = "source",
            power = 3,
            @params = new GDictionary { ["element"] = "fire" },
            stacks = 2,
            duration = 20,
        };
        unit.SetStatusEffect(effect);
        return unit;
    }

    private GDictionary Payload() => Project(BuildUnit());

    private GDictionary Project(BattleUnitState unit)
    {
        if (unit == null)
            return null;
        GodotProjectionLease<GDictionary> lease = unit.ToDictionaryLease(
            LifetimeDomain.Request,
            "battle-unit-state-schema-regression"
        );
        _payloadLeases.Add(lease);
        return lease.Value;
    }

    private GDictionary Project(BattleStatusEffectState effect)
    {
        GodotProjectionLease<GDictionary> lease = effect.ToDictionaryLease();
        _payloadLeases.Add(lease);
        return lease.Value;
    }

    private void DisposePayloadLeases()
    {
        for (int index = _payloadLeases.Count - 1; index >= 0; index--)
            _payloadLeases[index].Dispose();
        _payloadLeases.Clear();
    }

    private static GDictionary EffectiveTraitPayload(
        StringName traitId,
        StringName effectType,
        StringName effectiveInstanceKey,
        StringName sourceType,
        StringName sourceId,
        StringName triggerType,
        StringName chargeScope,
        StringName chargeResetTiming,
        GDictionary rollValues = null
    ) =>
        new()
        {
            ["trait_id"] = traitId.ToString(),
            ["effective_instance_key"] = effectiveInstanceKey.ToString(),
            ["source_type"] = sourceType.ToString(),
            ["source_id"] = sourceId.ToString(),
            ["effect_type"] = effectType.ToString(),
            ["trigger_type"] = triggerType.ToString(),
            ["charge_scope"] = chargeScope.ToString(),
            ["charge_reset_timing"] = chargeResetTiming.ToString(),
            ["rank"] = 1,
            ["stacks"] = 1,
            ["roll_values"] = rollValues ?? new GDictionary(),
        };

    private static GDictionary EquipmentAbilitySourcePayload(
        StringName effectiveInstanceKey,
        StringName equipmentDefId,
        StringName sourceEquipmentInstanceId,
        StringName sourceKind,
        GArray abilityIds
    ) =>
        new()
        {
            ["effective_instance_key"] = effectiveInstanceKey.ToString(),
            ["equipment_def_id"] = equipmentDefId.ToString(),
            ["source_equipment_instance_id"] = sourceEquipmentInstanceId.ToString(),
            ["source_kind"] = sourceKind.ToString(),
            ["ability_ids"] = abilityIds ?? new GArray(),
        };

    private static BattleUnitState BuildMinimalUnit() =>
        new()
        {
            unit_id = "body_size_unit",
            display_name = "Body Size Unit",
            faction_id = "player",
            control_mode = "manual",
        };

    private void AssertRejected(GDictionary payload, string message)
    {
        _test.True(BattleUnitState.FromDictionary(payload) == null, message);
    }

    private static GDictionary DictDictionary(GDictionary data, string key)
    {
        return data[key].AsGodotDictionary();
    }

    private static bool HasKeyWithType(
        GDictionary dictionary,
        string expectedText,
        Variant.Type expectedType
    )
    {
        if (dictionary == null)
            return false;
        foreach (Variant key in dictionary.Keys)
        {
            if (key.VariantType == expectedType && key.AsString() == expectedText)
                return true;
        }
        return false;
    }

    private static GArray DictArray(GDictionary data, string key)
    {
        return data[key].AsGodotArray();
    }

    private static int DictInt(GDictionary data, string key, int defaultValue = 0)
    {
        return data.ContainsKey(key) ? data[key].AsInt32() : defaultValue;
    }

    private static string DictString(GDictionary data, string key)
    {
        return data.ContainsKey(key) ? ProgressionDataUtils.to_string_name(data[key]).ToString() : "";
    }

    private static Vector2I DictVector2I(GDictionary data, string key)
    {
        return data.ContainsKey(key) ? data[key].AsVector2I() : Vector2I.Zero;
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? ProgressionDataUtils.to_string_name(data[key]) : "";
    }

    private void AssertVariantEq(object actual, object expected, string message)
    {
        string actualText = StableVariantText(actual);
        string expectedText = StableVariantText(expected);
        if (actualText != expectedText)
            _test.Fail($"{message} | actual={actualText} expected={expectedText}");
    }

    private static string StableVariantText(object value)
    {
        if (value == null)
            return "<null>";
        if (value is Variant variant)
            return StableVariantText(variant);
        if (value is GDictionary dictionary)
            return StableDictionaryText(dictionary);
        if (value is GArray array)
            return StableArrayText(array);
        if (value is GStringNameArray stringNameArray)
            return StableStringNameArrayText(stringNameArray);
        if (value is IEnumerable<StringName> stringNameValues)
            return StableStringNameEnumerableText(stringNameValues);
        if (value is StringName stringName)
            return stringName.ToString();
        if (value is Vector2I vector)
            return StableVector2IText(vector);
        return value.ToString() ?? "";
    }

    private static string StableVariantText(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => "<nil>",
            Variant.Type.Bool => value.AsBool() ? "true" : "false",
            Variant.Type.Int => value.AsInt64().ToString(),
            Variant.Type.Float => value.AsDouble().ToString("R"),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            Variant.Type.Vector2I => StableVector2IText(value.AsVector2I()),
            Variant.Type.Dictionary => StableDictionaryText(value.AsGodotDictionary()),
            Variant.Type.Array => StableArrayText(value.AsGodotArray()),
            _ => value.ToString(),
        };
    }

    private static string StableDictionaryText(GDictionary dictionary)
    {
        var parts = new List<string>();
        foreach (Variant key in dictionary.Keys)
            parts.Add($"{StableVariantText(key)}:{StableVariantText(dictionary[key])}");
        parts.Sort(StringComparer.Ordinal);
        return "{" + string.Join(",", parts) + "}";
    }

    private static string StableArrayText(GArray array)
    {
        var parts = new List<string>();
        foreach (Variant value in array)
            parts.Add(StableVariantText(value));
        return "[" + string.Join(",", parts) + "]";
    }

    private static string StableStringNameArrayText(GStringNameArray array)
    {
        var parts = new List<string>();
        foreach (StringName value in array)
            parts.Add(value.ToString());
        return "[" + string.Join(",", parts) + "]";
    }

    private static string StableStringNameEnumerableText(IEnumerable<StringName> values)
    {
        var parts = new List<string>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
            parts.Add(value.ToString());
        return "[" + string.Join(",", parts) + "]";
    }

    private static string StableVector2IText(Vector2I vector)
    {
        return $"Vector2I({vector.X},{vector.Y})";
    }

    private static Type FindLoadedType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }
        return null;
    }
}
