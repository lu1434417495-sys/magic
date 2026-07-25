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
        TestCombatResourceStrictLoadAndClonePreserveRaw();
        TestDetachedOwnerSnapshotsPreserveFlatValues();
        TestClonePreservesEphemeralRuntimeState();
        TestClonePreservesAiBlackboardOneShotMarkers();
        TestEffectiveTraitPayloadRoundtripAndClone();
        TestEquipmentAbilitySourcePayloadRoundtripAndClone();
        TestClonePreservesPendingCastRuntimeStateWithoutSerialization();
        TestTypedChargeAndFumbleHelpers();
        TestExtendedBodySizeCategoriesRoundtrip();
        TestGeometryOwnerMaintainsDerivedProjection();
        TestGeometryFormalBoundariesRejectInvalidExactState();
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
        TestWeaponProjectionStrictLoadPreservesRawUntilCanonicalBoundary();
        TestBodySizeRulesWrapperIsRemoved();

        DisposePayloadLeases();
        RequestTestExit(_test.Finish("Battle unit state schema regression"));
    }

    private void TestGeometryOwnerMaintainsDerivedProjection()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.SetAnchorCoord(new Vector2I(3, 4));
        BattleUnitGeometryReadView initial =
            unit.GetGeometryReadViewTyped();
        _test.Eq(
            initial.OccupiedCoords.Count,
            1,
            "单格体型应派生一个 occupied coord。"
        );
        _test.Eq(
            initial.OccupiedCoords[0],
            new Vector2I(3, 4),
            "occupied coords 应从 authoritative anchor 派生。"
        );

        unit.SetAnchorCoord(new Vector2I(4, 4));
        BattleUnitGeometryReadView moved =
            unit.GetGeometryReadViewTyped();
        _test.Eq(
            initial.OccupiedCoords[0],
            new Vector2I(3, 4),
            "先前取得的 geometry read view 应保持稳定。"
        );
        _test.Eq(
            moved.OccupiedCoords[0],
            new Vector2I(4, 4),
            "anchor 写入口应同步刷新 occupied coords。"
        );

        _test.True(unit.SetBodySizeProjection(BattleUnitState.BodySizeLarge), "large body size 应合法。");
        BattleUnitGeometryReadView enlarged =
            unit.GetGeometryReadViewTyped();
        _test.Eq(
            enlarged.BodySizeCategory,
            new StringName("large"),
            "body size 写入口应同步 category。"
        );
        _test.Eq(
            enlarged.FootprintSize,
            new Vector2I(2, 2),
            "body size 写入口应同步 footprint。"
        );
        _test.Eq(
            enlarged.OccupiedCoords.Count,
            4,
            "2x2 footprint 应同步派生四个 occupied coords。"
        );
    }

    private void TestGeometryFormalBoundariesRejectInvalidExactState()
    {
        BattleUnitState missingOwner = BuildMinimalUnit();
        missingOwner.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.MissingOwner
        );
        _test.True(
            ThrowsInvalidOperation(
                () =>
                {
                    using GodotProjectionLease<GDictionary> lease =
                        missingOwner.ToDictionaryLease(
                            LifetimeDomain.Request,
                            "geometry-missing-owner-schema-probe"
                        );
                }
            ),
            "正式 payload 投影应 fail-fast 拒绝 missing geometry owner。"
        );

        BattleUnitState invalidIdentity = BuildMinimalUnit();
        invalidIdentity.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.Present(
                Vector2I.Zero,
                -4,
                "raw_invalid_category",
                Vector2I.One,
                new Vector2IList { Vector2I.Zero }
            )
        );
        _test.True(
            ThrowsInvalidOperation(() => invalidIdentity.clone()),
            "clone canonical 边界应 fail-fast 拒绝非法 body-size identity。"
        );

        BattleUnitState staleProjection = BuildMinimalUnit();
        staleProjection.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.Present(
                new Vector2I(2, 3),
                BattleUnitState.BodySizeLarge,
                "large",
                Vector2I.One,
                new Vector2IList { new Vector2I(2, 3) }
            )
        );
        _test.True(
            ThrowsInvalidOperation(
                () =>
                {
                    using GodotProjectionLease<GDictionary> lease =
                        staleProjection.ToDictionaryLease(
                            LifetimeDomain.Request,
                            "geometry-stale-projection-schema-probe"
                        );
                }
            ),
            "正式 payload 投影应 fail-fast 拒绝 stale footprint/occupied projection。"
        );
    }

    private void TestValidRoundtripPreservesCurrentPayload()
    {
        BattleUnitState unit = BuildUnit();
        _test.True(unit != null, "BuildUnit 应返回单位。");
        BattleUnitActionClockSnapshot actionClock =
            unit.GetActionClockStateTyped();
        unit.RestoreActionClockForMutationSnapshotExact(
            BattleUnitActionClockSnapshot.Present(
                actionClock.ActionProgress,
                actionClock.ActionThreshold,
                75
            )
        );
        GDictionary payload = Project(unit);
        _test.False(
            payload.ContainsKey("action_progress_rate_remainder"),
            "runtime-only action progress 余数不应进入正式 payload。"
        );
        _test.False(
            payload.ContainsKey("save_modifier_state"),
            "save-modifier owner 不应新增 nested payload key。"
        );
        var payloadKeys = new List<string>();
        foreach (Variant key in payload.Keys)
            payloadKeys.Add(key.AsString());
        int saveKeyStart = payloadKeys.IndexOf("save_advantage_tags");
        _test.True(
            saveKeyStart >= 0
            && saveKeyStart + 4 < payloadKeys.Count
            && payloadKeys[saveKeyStart + 1] == "save_disadvantage_tags"
            && payloadKeys[saveKeyStart + 2] == "save_immunity_tags"
            && payloadKeys[saveKeyStart + 3] == "damage_resistances"
            && payloadKeys[saveKeyStart + 4] == "save_bonus_by_ability",
            "save-modifier owner 投影应保持既有 flat key 与字段顺序。"
        );
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "当前 to_dict payload 应可由 from_dict 恢复。");
        _test.Eq(restored?.GetCurrentMovePoints() ?? -1, 5, "current_move_points 应保留大于默认值的 int。");
        _test.Eq(
            restored?.GetBodySizeCategory().ToString() ?? "",
            "large",
            "body_size_category 应随 body_size round-trip。"
        );
        _test.Eq(
            restored?.encounter_actor_id.ToString() ?? "",
            "schema_actor",
            "encounter_actor_id 应随当前 payload round-trip。"
        );
        BattleUnitVisionProficiencyReadView restoredIdentity =
            restored?.GetVisionProficiencyReadViewTyped()
            ?? BattleUnitVisionProficiencyReadView.MissingOwner;
        _test.True(
            restoredIdentity.VisionTags.Contains("darkvision"),
            "vision_tags 应通过原 flat key round-trip。"
        );
        _test.True(
            restoredIdentity.ProficiencyTags.Contains("light_armor"),
            "proficiency_tags 应通过原 flat key round-trip。"
        );
        BattleUnitSaveModifierReadView restoredSaveModifiers =
            restored?.GetSaveModifiersReadViewTyped()
            ?? BattleUnitSaveModifierReadView.MissingOwner;
        _test.True(
            restoredSaveModifiers.AdvantageTags.Contains("charm"),
            "save_advantage_tags 应通过原 flat key round-trip。"
        );
        _test.True(
            restoredSaveModifiers.DisadvantageTags.Contains("fear"),
            "save_disadvantage_tags 应通过原 flat key round-trip。"
        );
        _test.True(
            restoredSaveModifiers.ImmunityTags.Contains("poison"),
            "save_immunity_tags 应通过原 flat key round-trip。"
        );
        _test.Eq(
            restoredSaveModifiers.BonusByAbility.Get("wisdom", -1),
            3,
            "save_bonus_by_ability 应通过原 flat key round-trip。"
        );
        _test.True(
            restoredSaveModifiers.BonusByAbility.TryGetValue(
                "constitution",
                out int explicitZeroBonus
            )
            && explicitZeroBonus == 0,
            "save_bonus_by_ability round-trip 应保留显式 0。"
        );
        _test.Eq(
            restored?.GetDamageResistanceTyped("fire").ToString() ?? "",
            "half",
            "damage_resistances 应 round-trip。"
        );
        _test.True(
            restored?.IsRestingTyped() == true,
            "is_resting 应继续通过原 flat key round-trip。"
        );
        _test.Eq(
            restored?.GetActionProgressTyped() ?? -1,
            20,
            "action progress 应继续通过原 flat key round-trip。"
        );
        _test.Eq(
            restored?.GetActionThresholdTyped() ?? -1,
            140,
            "action threshold 应继续通过原 flat key round-trip。"
        );
        _test.Eq(
            restored?.GetActionProgressRateRemainderTyped() ?? -1,
            0,
            "runtime-only action progress 余数不应从 payload 恢复。"
        );
        _test.Eq(
            restored?.GetKnownActiveSkillIdsTyped()[0] ?? new StringName(""),
            new StringName("slash"),
            "known active skill 顺序应通过原 flat key round-trip。"
        );
        _test.Eq(
            restored?.GetKnownSkillLevelTyped("slash", -1) ?? -1,
            2,
            "known skill level 应通过原 flat map round-trip。"
        );
        _test.Eq(
            restored?.GetKnownSkillLockHitBonusTyped("slash", -1) ?? -1,
            3,
            "known skill lock-hit bonus 应通过原 flat map round-trip。"
        );

        GDictionary rawKnownSkillPayload = Payload();
        DictDictionary(rawKnownSkillPayload, "known_skill_level_map")[
            "raw_negative"
        ] = -2;
        DictDictionary(rawKnownSkillPayload, "known_skill_lock_hit_bonus_map")[
            "raw_zero"
        ] = 0;
        BattleUnitState rawKnownSkillRestored =
            BattleUnitState.FromDictionary(rawKnownSkillPayload);
        _test.True(
            rawKnownSkillRestored != null,
            "known-skill flat maps 的既有 raw int payload 应继续可加载。"
        );
        _test.Eq(
            rawKnownSkillRestored?.GetKnownSkillLevelTyped("raw_negative", 99) ?? 99,
            -2,
            "known skill level codec 应继续保留负数 raw 值。"
        );
        _test.Eq(
            rawKnownSkillRestored?.GetKnownSkillLockHitBonusTyped("raw_zero", -1) ?? -1,
            0,
            "known skill lock-hit codec 应继续保留显式 0。"
        );
        GDictionary malformedSaveBonusPayload = Payload();
        malformedSaveBonusPayload["save_bonus_by_ability"] =
            new GDictionary { ["wisdom"] = "3" };
        BattleUnitState malformedSaveBonusRestored =
            BattleUnitState.FromDictionary(malformedSaveBonusPayload);
        _test.True(
            malformedSaveBonusRestored != null,
            "save_bonus_by_ability 的既有 malformed-map fallback 语义应保持可加载。"
        );
        _test.Eq(
            malformedSaveBonusRestored
                ?.GetSaveModifiersReadViewTyped()
                .BonusByAbility.Count
                ?? -1,
            0,
            "malformed save_bonus_by_ability 应继续回落为空 map。"
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

    private void TestCombatResourceStrictLoadAndClonePreserveRaw()
    {
        GDictionary payload = Payload();
        payload["current_hp"] = -1;
        payload["current_mp"] = -2;
        payload["current_stamina"] = -3;
        payload["current_aura"] = -4;
        payload["current_ap"] = -5;
        payload["current_move_points"] = 6;
        payload["stamina_recovery_progress"] = -7;
        payload["is_alive"] = true;

        var expected = new BattleUnitCombatResourceValues(
            -1,
            -2,
            -3,
            -4,
            -5,
            6,
            -7,
            true
        );
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(
            restored != null,
            "strict load 应继续接受既有 raw 资源整数 sentinel。"
        );
        _test.Eq(
            restored?.CaptureCombatResourcesForMutationSnapshotExact().Values
                ?? default,
            expected,
            "strict load 应原样恢复全部 8 项 combat-resource 值。"
        );

        BattleUnitState cloned = restored?.clone();
        _test.True(cloned != null, "包含 raw combat-resource sentinel 的 unit 应可 clone。");
        _test.Eq(
            cloned?.CaptureCombatResourcesForMutationSnapshotExact().Values
                ?? default,
            expected,
            "clone 应原样保留全部 8 项 combat-resource raw 值。"
        );

        cloned?.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(
                expected with
                {
                    Hp = 44,
                    StaminaRecoveryProgress = 3,
                    IsAlive = false,
                }
            )
        );
        _test.Eq(
            restored?.CaptureCombatResourcesForMutationSnapshotExact().Values
                ?? default,
            expected,
            "clone 的 combat-resource exact 修改不得回写原 unit。"
        );

        GDictionary inconsistentPayload = Payload();
        inconsistentPayload["current_hp"] = 4;
        inconsistentPayload["is_alive"] = false;
        BattleUnitState inconsistent =
            BattleUnitState.FromDictionary(inconsistentPayload);
        _test.True(
            inconsistent != null,
            "strict load 应继续接受既有 HP/alive 不一致 raw payload。"
        );
        _test.Eq(
            inconsistent
                ?.CaptureCombatResourcesForMutationSnapshotExact()
                .Values.Hp
                ?? -1,
            4,
            "strict load 不应按 alive 状态改写 raw HP。"
        );
        _test.False(
            inconsistent
                ?.CaptureCombatResourcesForMutationSnapshotExact()
                .Values.IsAlive
                ?? true,
            "strict load 不应按 raw HP 改写 alive sentinel。"
        );
    }

    private void TestDetachedOwnerSnapshotsPreserveFlatValues()
    {
        BattleUnitState unit = BuildMinimalUnit();
        var resourceValues = new BattleUnitCombatResourceValues(
            31,
            7,
            13,
            5,
            2,
            4,
            9,
            true
        );
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(resourceValues)
        );
        unit.ReplaceShieldStateTyped(
            6,
            9,
            45,
            "detached_ward",
            "detached_source",
            "detached_skill"
        );
        unit.SetCooldownTyped("detached_cooldown", 12);
        unit.SetCooldownAnchorTuTyped(50);
        unit.MarkActionTakenThisTurnTyped();
        unit.MarkMovedThisTurnTyped();
        unit.GrantLockedMovePointsThisTurnTyped();
        unit.MarkTurnCastingExhaustedTyped();
        unit.RestoreActionClockForMutationSnapshotExact(
            BattleUnitActionClockSnapshot.Present(45, 135, 75)
        );
        unit.SetKnownActiveSkillIds(
            new StringName[] { "detached_primary", "detached_secondary" }
        );
        unit.SetKnownSkillLevelsTyped(
            new Dictionary<StringName, int>
            {
                ["detached_primary"] = 4,
                ["detached_zero"] = 0,
            },
            preserveZero: true
        );
        unit.SetKnownSkillLockHitBonusesTyped(
            new Dictionary<StringName, int> { ["detached_primary"] = 2 }
        );

        Dictionary<string, object> snapshot = BattleUnitStatePlainSnapshot.Build(unit);
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(
                resourceValues with
                {
                    Hp = 1,
                    Mp = 2,
                    Stamina = 3,
                    Aura = 4,
                    Ap = 5,
                    MovePoints = 6,
                    StaminaRecoveryProgress = 1,
                    IsAlive = false,
                }
            )
        );
        unit.ClearShield();
        unit.SetCooldownTyped("detached_cooldown", 3);
        unit.SetCooldownAnchorTuTyped(75);
        unit.ResetTurnStateForTurnStartTyped();
        unit.SetActionProgressTyped(5);
        unit.SetActionThresholdTyped(120);
        unit.SetKnownActiveSkillIds(new StringName[] { "mutated_skill" });
        unit.SetKnownSkillLevelTyped("detached_primary", 1);
        unit.SetKnownSkillLockHitBonusTyped("detached_primary", 1);

        _test.Eq((int)snapshot["current_hp"], 31, "detached snapshot 应保留 current HP。");
        _test.Eq((int)snapshot["current_mp"], 7, "detached snapshot 应保留 current MP。");
        _test.Eq(
            (int)snapshot["current_stamina"],
            13,
            "detached snapshot 应保留 current stamina。"
        );
        _test.Eq((int)snapshot["current_aura"], 5, "detached snapshot 应保留 current aura。");
        _test.Eq((int)snapshot["current_ap"], 2, "detached snapshot 应保留 current AP。");
        _test.Eq(
            (int)snapshot["current_move_points"],
            4,
            "detached snapshot 应保留 current move points。"
        );
        _test.Eq(
            (int)snapshot["stamina_recovery_progress"],
            9,
            "detached snapshot 应保留 stamina recovery progress。"
        );
        _test.True(
            snapshot["is_alive"] is true,
            "detached snapshot 应保留 alive 状态。"
        );
        _test.Eq((int)snapshot["current_shield_hp"], 6, "detached snapshot 应保留 current shield HP。");
        _test.Eq((int)snapshot["shield_max_hp"], 9, "detached snapshot 应保留 shield max HP。");
        _test.Eq((int)snapshot["shield_duration"], 45, "detached snapshot 应保留 shield duration。");
        _test.Eq(snapshot["shield_family"]?.ToString() ?? "", "detached_ward", "detached snapshot 应保留 family。");
        _test.Eq(snapshot["shield_source_unit_id"]?.ToString() ?? "", "detached_source", "detached snapshot 应保留 source unit。");
        _test.Eq(snapshot["shield_source_skill_id"]?.ToString() ?? "", "detached_skill", "detached snapshot 应保留 source skill。");
        var cooldowns = (Dictionary<StringName, int>)snapshot["cooldowns"];
        _test.Eq(
            cooldowns[new StringName("detached_cooldown")],
            12,
            "detached snapshot 不应共享 cooldown owner map。"
        );
        _test.Eq((int)snapshot["last_turn_tu"], 50, "detached snapshot 应保留 cooldown anchor。");
        _test.Eq((int)snapshot["action_progress"], 45, "detached snapshot 应保留 action progress。");
        _test.Eq((int)snapshot["action_threshold"], 135, "detached snapshot 应保留 action threshold。");
        _test.False(
            snapshot.ContainsKey("action_progress_rate_remainder"),
            "detached snapshot 不应新增 runtime-only action progress 余数。"
        );
        _test.True(
            snapshot["has_taken_action_this_turn"] is true,
            "detached snapshot 应保留 has_taken_action_this_turn。"
        );
        _test.True(
            snapshot["can_use_locked_move_points_this_turn"] is true,
            "detached snapshot 应保留 can_use_locked_move_points_this_turn。"
        );
        _test.False(
            snapshot.ContainsKey("has_moved_this_turn"),
            "detached snapshot 不应新增 runtime-only has_moved_this_turn。"
        );
        _test.False(
            snapshot.ContainsKey("turn_casting_exhausted"),
            "detached snapshot 不应新增 runtime-only turn_casting_exhausted。"
        );
        var detachedSkillIds =
            (List<object>)snapshot["known_active_skill_ids"];
        var detachedSkillLevels =
            (Dictionary<string, object>)snapshot["known_skill_level_map"];
        var detachedLockHitBonuses =
            (Dictionary<string, object>)snapshot["known_skill_lock_hit_bonus_map"];
        _test.Eq(
            detachedSkillIds[0]?.ToString() ?? "",
            "detached_primary",
            "detached snapshot 应保留 known active skill 首项顺序。"
        );
        _test.Eq(
            (int)detachedSkillLevels["detached_zero"],
            0,
            "detached snapshot 应保留显式 0 skill level。"
        );
        _test.Eq(
            (int)detachedLockHitBonuses["detached_primary"],
            2,
            "detached snapshot 不应共享 lock-hit owner map。"
        );
    }

    private void TestClonePreservesEphemeralRuntimeState()
    {
        BattleUnitState unit = BuildMinimalUnit();
        var resourceValues = new BattleUnitCombatResourceValues(
            27,
            8,
            14,
            3,
            2,
            5,
            11,
            true
        );
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(resourceValues)
        );
        unit.encounter_actor_id = "clone_actor";
        unit.SetPerBattleChargeTyped("dragon_breath", 1);
        unit.SetPerTurnChargeTyped("nimble_escape", 1);
        unit.SetPerTurnChargeLimitTyped("nimble_escape", 1);
        unit.SetFumbleProtectionUsedTyped("mage_fireball", 1);
        unit.SetCooldownTyped("clone_cooldown", 25);
        unit.SetCooldownAnchorTuTyped(40);
        unit.ReplaceShieldStateTyped(
            4,
            8,
            30,
            "clone_ward",
            "clone_source",
            "clone_skill"
        );
        unit.MarkActionTakenThisTurnTyped();
        unit.MarkMovedThisTurnTyped();
        unit.GrantLockedMovePointsThisTurnTyped();
        unit.MarkTurnCastingExhaustedTyped();
        unit.MarkContingencySetupConsumed("contingency_alpha");
        unit.RestoreActionClockForMutationSnapshotExact(
            BattleUnitActionClockSnapshot.Present(35, 145, 65)
        );
        unit.SetKnownActiveSkillIds(
            new StringName[] { "clone_primary", "clone_secondary" }
        );
        unit.SetKnownSkillLevelsTyped(
            new Dictionary<StringName, int>
            {
                ["clone_primary"] = 3,
                ["clone_zero"] = 0,
            },
            preserveZero: true
        );
        unit.SetKnownSkillLockHitBonusesTyped(
            new Dictionary<StringName, int> { ["clone_primary"] = 2 }
        );

        BattleUnitState cloned = unit.clone();
        _test.True(cloned != null, "BattleUnitState.clone() 应返回可用副本。");
        if (cloned == null)
            return;

        AssertVariantEq(Project(cloned), Project(unit), "clone 应保留序列化字段。");
        _test.Eq(
            cloned.CaptureCombatResourcesForMutationSnapshotExact().Values,
            resourceValues,
            "clone 应保留完整 combat-resource owner 状态。"
        );
        _test.Eq(cloned.GetPerBattleChargeTyped("dragon_breath", -1), 1, "clone 应深拷贝 per_battle_charges。");
        _test.Eq(
            cloned.encounter_actor_id,
            new StringName("clone_actor"),
            "clone 应保留 encounter_actor_id。"
        );
        _test.Eq(cloned.GetPerTurnChargeTyped("nimble_escape", -1), 1, "clone 应深拷贝 per_turn_charges。");
        _test.Eq(cloned.GetPerTurnChargeLimitTyped("nimble_escape", -1), 1, "clone 应深拷贝 per_turn_charge_limits。");
        _test.Eq(cloned.GetFumbleProtectionUsedTyped("mage_fireball", -1), 1, "clone 应深拷贝 fumble_protection_used。");
        _test.Eq(
            cloned.GetShieldStateTyped(),
            unit.GetShieldStateTyped(),
            "clone 应保留完整 shield owner 状态。"
        );
        _test.Eq(
            cloned.GetCooldownTyped("clone_cooldown", -1),
            25,
            "clone 应保留 cooldown owner map。"
        );
        _test.Eq(
            cloned.GetCooldownAnchorTuTyped(),
            40,
            "clone 应保留 cooldown anchor。"
        );
        _test.Eq(
            cloned.GetTurnStateTyped(),
            unit.GetTurnStateTyped(),
            "clone 应保留完整 turn owner runtime 状态。"
        );
        _test.Eq(
            cloned.GetActionClockStateTyped(),
            unit.GetActionClockStateTyped(),
            "clone 应保留完整 action clock runtime 状态。"
        );
        _test.True(
            cloned.HasConsumedContingencySetup("contingency_alpha"),
            "clone 应保留 consumed contingency setup。"
        );
        _test.Eq(
            cloned.GetKnownActiveSkillIdsTyped()[0],
            new StringName("clone_primary"),
            "clone 应保留 known active skill 首项顺序。"
        );
        _test.True(
            cloned.HasKnownSkillLevelTyped("clone_zero"),
            "clone 应保留显式 0 skill level。"
        );
        _test.Eq(
            cloned.GetKnownSkillLockHitBonusTyped("clone_primary", -1),
            2,
            "clone 应保留 lock-hit bonus。"
        );

        cloned.SetPerBattleChargeTyped("dragon_breath", 0);
        cloned.SetPerTurnChargeTyped("nimble_escape", 0);
        cloned.SetPerTurnChargeLimitTyped("nimble_escape", 0);
        cloned.SetFumbleProtectionUsedTyped("mage_fireball", 0);
        cloned.ClearShield();
        cloned.SetCooldownTyped("clone_cooldown", 5);
        cloned.SetCooldownAnchorTuTyped(60);
        cloned.ResetTurnStateForTurnStartTyped();
        cloned.RestoreActionClockForMutationSnapshotExact(
            BattleUnitActionClockSnapshot.Present(9, 25, 5)
        );
        cloned.AddKnownActiveSkill("clone_only");
        cloned.SetKnownSkillLevelTyped("clone_primary", 1);
        cloned.SetKnownSkillLockHitBonusTyped("clone_primary", 1);
        cloned.ReplaceConsumedContingencySetupIdsTyped(
            new StringName[] { "contingency_clone_only" }
        );
        cloned.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(
                resourceValues with
                {
                    Hp = 1,
                    StaminaRecoveryProgress = 2,
                    IsAlive = false,
                }
            )
        );
        _test.Eq(
            unit.CaptureCombatResourcesForMutationSnapshotExact().Values,
            resourceValues,
            "clone 修改 combat-resource owner 不应回写原 unit。"
        );
        _test.Eq(unit.GetPerBattleChargeTyped("dragon_breath", -1), 1, "clone 不应共享 per_battle_charges 字典。");
        _test.Eq(unit.GetPerTurnChargeTyped("nimble_escape", -1), 1, "clone 不应共享 per_turn_charges 字典。");
        _test.Eq(unit.GetPerTurnChargeLimitTyped("nimble_escape", -1), 1, "clone 不应共享 per_turn_charge_limits 字典。");
        _test.Eq(unit.GetFumbleProtectionUsedTyped("mage_fireball", -1), 1, "clone 不应共享 fumble_protection_used 字典。");
        _test.True(unit.HasShield(), "clone 清理 shield 不应回写原 unit。");
        _test.Eq(
            unit.GetCooldownTyped("clone_cooldown", -1),
            25,
            "clone 不应共享 cooldown owner map。"
        );
        _test.Eq(
            unit.GetCooldownAnchorTuTyped(),
            40,
            "clone 不应共享 cooldown anchor。"
        );
        BattleUnitTurnSnapshot originalTurnState = unit.GetTurnStateTyped();
        _test.True(
            originalTurnState.HasTakenActionThisTurn
                && originalTurnState.HasMovedThisTurn
                && originalTurnState.CanUseLockedMovePointsThisTurn
                && originalTurnState.CastingExhausted,
            "clone 重置 turn owner 不应回写原 unit。"
        );
        _test.True(
            unit.HasConsumedContingencySetup("contingency_alpha"),
            "clone 不应共享 consumed contingency setup owner。"
        );
        _test.False(
            unit.HasConsumedContingencySetup("contingency_clone_only"),
            "clone 的 consumed contingency 修改不应回写原 unit。"
        );
        _test.Eq(
            unit.GetActionClockStateTyped(),
            BattleUnitActionClockSnapshot.Present(35, 145, 65),
            "clone 修改 action clock 不应回写原 unit。"
        );
        _test.False(
            unit.KnowsActiveSkill("clone_only"),
            "clone 修改 active skill list 不应回写原 owner。"
        );
        _test.Eq(
            unit.GetKnownSkillLevelTyped("clone_primary", -1),
            3,
            "clone 修改 skill level map 不应回写原 owner。"
        );
        _test.Eq(
            unit.GetKnownSkillLockHitBonusTyped("clone_primary", -1),
            2,
            "clone 修改 lock-hit map 不应回写原 owner。"
        );
    }

    private void TestClonePreservesAiBlackboardOneShotMarkers()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.ai_blackboard.low_luck_reverse_fate_used = true;
        unit.ai_blackboard.low_luck_black_star_wedge_used = true;
        unit.ai_blackboard.madness_ai_control = true;

        BattleUnitState cloned = unit.clone();
        _test.True(
            cloned.ai_blackboard.low_luck_reverse_fate_used,
            "clone 应携带黑板一次性标记(逆命护符已用),否则预览把已消耗遗物当可用。"
        );
        _test.True(
            cloned.ai_blackboard.low_luck_black_star_wedge_used,
            "clone 应携带黑星楔已用标记。"
        );
        _test.True(cloned.ai_blackboard.madness_ai_control, "clone 应携带疯狂控制标记。");

        cloned.ai_blackboard.low_luck_reverse_fate_used = false;
        cloned.ai_blackboard.madness_ai_control = false;
        _test.True(
            unit.ai_blackboard.low_luck_reverse_fate_used,
            "clone 的黑板必须是拷贝:改克隆体不应影响原单位的一次性标记。"
        );
        _test.True(
            unit.ai_blackboard.madness_ai_control,
            "clone 的黑板必须是拷贝:改克隆体不应影响原单位的疯狂标记。"
        );
    }

    private void TestEffectiveTraitPayloadRoundtripAndClone()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.ReplaceEffectiveTraitsTyped(TraitTestData.EffectiveTraits(
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
        ));

        GDictionary payload = Project(unit);
        using GArray traitPayloads = payload["effective_trait_instances"].AsGodotArray();
        using GDictionary savageAttacksPayload = traitPayloads[1].AsGodotDictionary();
        using GDictionary rollValuesPayload = savageAttacksPayload["roll_values"]
            .AsGodotDictionary();
        _test.True(
            HasKeyWithType(rollValuesPayload, "amount", Variant.Type.StringName),
            "effective trait roll_values projection 应保持正式 StringName key shape。"
        );
        payload["effective_trait_ids"] = new GArray
        {
            "savage_attacks",
            "halfling_luck",
        };
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "effective trait payload 应可 round-trip。");
        _test.Eq(
            restored?.GetEffectiveTraitInstanceCountTyped() ?? -1,
            2,
            "effective trait payload 数量应保留。"
        );
        _test.Eq(
            restored?.GetEffectiveTraitsReadViewTyped().TraitIds.Count ?? -1,
            2,
            "effective_trait_ids 派生投影应保留。"
        );
        BattleUnitEffectiveTraitSnapshot restoredRaw =
            restored.CaptureEffectiveTraitsForMutationSnapshotExact();
        _test.Eq(
            restoredRaw.TraitIds[0],
            new StringName("savage_attacks"),
            "strict load 应保留 set-equivalent payload ids 的原始顺序。"
        );
        restored.RestoreEffectiveTraitsForMutationSnapshotExact(
            BattleUnitEffectiveTraitSnapshot.Present(
                restoredRaw.Instances,
                new StringNameList { "stale_raw_trait" }
            )
        );
        GDictionary restoredCanonical = Project(restored);
        using GArray restoredCanonicalIds =
            restoredCanonical["effective_trait_ids"].AsGodotArray();
        _test.Eq(
            restoredCanonicalIds.Count,
            2,
            "canonical projection 应完全忽略 stale raw ids。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(
                restoredCanonicalIds[0]
            ),
            new StringName("halfling_luck"),
            "canonical projection 应忽略 raw ids 顺序并从 instances 重派生。"
        );
        Dictionary<string, object> restoredDetached =
            BattleUnitStatePlainSnapshot.Build(restored);
        var restoredDetachedIds =
            (List<object>)restoredDetached["effective_trait_ids"];
        _test.Eq(
            restoredDetachedIds.Count,
            2,
            "detached projection 应完全忽略 stale raw ids。"
        );
        _test.Eq(
            restoredDetachedIds[0]?.ToString() ?? "",
            "halfling_luck",
            "detached projection 应忽略 raw ids 顺序并从 instances 重派生。"
        );

        BattleUnitState cloned = unit.clone();
        _test.Eq(cloned.GetEffectiveTraitInstanceCountTyped(), 2, "clone 应保留 effective trait payload。");
        _test.Eq(
            cloned.GetEffectiveTraitsReadViewTyped().TraitIds[0],
            new StringName("halfling_luck"),
            "gameplay clone 应从 instances 重派生排序 ids。"
        );
        List<BattleEffectiveTraitInstanceState> clonedInstances =
            cloned.CopyEffectiveTraitInstancesTyped();
        clonedInstances[0].trait_id = "mutated";
        cloned.ReplaceEffectiveTraitsTyped(clonedInstances);
        _test.Eq(
            unit.GetEffectiveTraitsReadViewTyped().Instances[0].TraitId,
            new StringName("halfling_luck"),
            "clone 不应共享 effective trait state。"
        );
    }

    private void TestEquipmentAbilitySourcePayloadRoundtripAndClone()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.ReplaceCreatureTypeTagsTyped(
            new GStringNameArray { "undead", "construct" }
        );
        unit.ReplaceEquipmentAbilityProjectionTyped(
            new List<BattleEquipmentAbilitySourceState>
            {
                new()
                {
                    EffectiveInstanceKey =
                        "equipment_fixed::eq_000001::trait.weapon.flame",
                    EquipmentDefId = "test_blade",
                    SourceEquipmentInstanceId = "eq_000001",
                    SourceKind =
                        EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                    AbilityIds = new List<StringName>
                    {
                        "binding.weapon.flame",
                    },
                },
            },
            new List<BattleTemporalProgressModifierState>
            {
                new()
                {
                    ModifierId = "runtime_only_temporal",
                    BindingId = "binding.weapon.flame",
                    SourceEquipmentInstanceId = "eq_000001",
                    AppliesToActionProgress = true,
                    AppliesToCastProgress = true,
                    SaveDc = 10,
                    AttributeModifierId = "intelligence",
                    SuccessRatePercent = 200,
                    FailureRatePercent = 50,
                    Label = "runtime only",
                },
            }
        );

        GDictionary payload = Project(unit);
        _test.False(
            payload.ContainsKey("temporal_progress_modifiers"),
            "runtime-only temporal progress modifiers 不应进入正式 unit payload。"
        );
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "equipment ability source payload 应可 round-trip。");
        BattleUnitEquipmentAbilityProjectionReadView restoredProjection =
            restored?.GetEquipmentAbilityProjectionReadViewTyped()
            ?? BattleUnitEquipmentAbilityProjectionReadView.MissingOwner;
        _test.True(
            restoredProjection.OwnerPresent,
            "strict load 应重建 equipment ability projection owner。"
        );
        _test.Eq(
            restoredProjection.Sources.Count,
            1,
            "equipment ability source 数量应保留。"
        );
        BattleEquipmentAbilitySourceReadView restoredSource =
            restoredProjection.Sources.Count > 0
                ? restoredProjection.Sources[0]
                : null;
        _test.Eq(
            restoredSource?.EffectiveInstanceKey ?? "",
            new StringName("equipment_fixed::eq_000001::trait.weapon.flame"),
            "equipment ability source 应保留 effective instance key。"
        );
        _test.Eq(
            restoredSource?.SourceEquipmentInstanceId ?? "",
            new StringName("eq_000001"),
            "player equipment ability source 应保留持久装备 instance id。"
        );
        _test.Eq(
            restoredSource?.SourceKind ?? EquipmentAbilitySourceKind.Unknown,
            EquipmentAbilitySourceKind.PlayerPersistentEquipment,
            "player equipment ability source 应保留 source kind。"
        );
        _test.True(
            restoredSource?.AbilityIds?.Contains("binding.weapon.flame") == true,
            "equipment ability source 应保留 ability/binding id。"
        );
        _test.True(
            restoredProjection.TemporalProgressModifiers.IsPresent
            && restoredProjection.TemporalProgressModifiers.Count == 0,
            "strict payload 不含 runtime-only temporal modifiers，load 后应 materialize 为空组件。"
        );
        _test.True(
            restored != null && restored.HasCreatureTypeTag("undead"),
            "creature_type_tags 应随 unit payload round-trip。"
        );

        BattleUnitState cloned = unit.clone();
        BattleUnitEquipmentAbilityProjectionReadView clonedProjection =
            cloned.GetEquipmentAbilityProjectionReadViewTyped();
        _test.Eq(
            clonedProjection.Sources.Count,
            1,
            "clone 应保留 equipment ability source payload。"
        );
        _test.Eq(
            clonedProjection.TemporalProgressModifiers.Count,
            1,
            "clone 应保留 runtime-only temporal modifier。"
        );
        BattleUnitEquipmentAbilityProjectionSnapshot clonedRaw =
            cloned.CaptureEquipmentAbilityProjectionForMutationSnapshotExact();
        clonedRaw.Sources[0].AbilityIds[0] = "mutated.binding";
        cloned.RestoreEquipmentAbilityProjectionForMutationSnapshotExact(clonedRaw);
        cloned.AddCreatureTypeTagTyped("mutated_creature");
        _test.Eq(
            cloned.GetEquipmentAbilitySourcesReadViewTyped()[0].AbilityIds[0],
            new StringName("mutated.binding"),
            "clone 应可通过正式 exact seam 独立改变 nested ability id。"
        );
        _test.Eq(
            unit.GetEquipmentAbilitySourcesReadViewTyped()[0].AbilityIds[0],
            new StringName("binding.weapon.flame"),
            "clone 的 equipment ability projection 写入不得回写 source owner。"
        );
        _test.True(
            !unit.HasCreatureTypeTag("mutated_creature"),
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
        unit.MarkActionTakenThisTurnTyped();
        unit.MarkMovedThisTurnTyped();
        unit.GrantLockedMovePointsThisTurnTyped();
        unit.MarkTurnCastingExhaustedTyped();

        GDictionary payload = Project(unit);
        _test.True(!payload.ContainsKey("pending_cast"), "pending_cast 是 runtime-only 字段，不应进入 unit payload。");
        _test.True(
            payload["has_taken_action_this_turn"].AsBool(),
            "已有 has_taken_action_this_turn 扁平字段应继续进入 unit payload。"
        );
        _test.True(
            payload["can_use_locked_move_points_this_turn"].AsBool(),
            "已有 can_use_locked_move_points_this_turn 扁平字段应继续进入 unit payload。"
        );
        _test.True(
            !payload.ContainsKey("has_moved_this_turn"),
            "has_moved_this_turn 仍应保持 runtime-only。"
        );
        _test.True(!payload.ContainsKey("turn_casting_exhausted"), "turn_casting_exhausted 是 runtime-only 字段，不应进入 unit payload。");
        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(
            restored?.pending_cast == null,
            "从 payload 恢复时不应恢复 pending_cast runtime 状态。"
        );
        _test.True(
            restored?.HasTakenActionThisTurnTyped() == true,
            "round-trip 应恢复已有 has_taken_action_this_turn 字段。"
        );
        _test.True(
            restored?.CanUseLockedMovePointsThisTurnTyped() == true,
            "round-trip 应恢复已有 can_use_locked_move_points_this_turn 字段。"
        );
        _test.False(
            restored?.HasMovedThisTurnTyped() ?? true,
            "round-trip 不应恢复 runtime-only has_moved_this_turn。"
        );
        _test.False(
            restored?.IsTurnCastingExhaustedTyped() ?? true,
            "round-trip 不应恢复 runtime-only turn_casting_exhausted。"
        );

        BattleUnitState cloned = unit.clone();
        _test.True(cloned?.pending_cast != null, "clone 应保留 pending_cast runtime 状态。");
        _test.True(
            cloned?.HasTakenActionThisTurnTyped() == true
                && cloned.HasMovedThisTurnTyped()
                && cloned.CanUseLockedMovePointsThisTurnTyped(),
            "clone 应保留三个移动/行动 turn flags。"
        );
        _test.True(
            cloned?.IsTurnCastingExhaustedTyped() == true,
            "clone 应保留 turn_casting_exhausted runtime 状态。"
        );
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

        GDictionary badBodySizeType = Payload();
        badBodySizeType["body_size"] = "3";
        AssertRejected(badBodySizeType, "body_size 非 int 应拒绝。");

        GDictionary badBodySizeCategoryType = Payload();
        badBodySizeCategoryType["body_size_category"] = 3;
        AssertRejected(badBodySizeCategoryType, "body_size_category 非 String/StringName 应拒绝。");

        GDictionary badFootprintType = Payload();
        badFootprintType["footprint_size"] = "2,2";
        AssertRejected(badFootprintType, "footprint_size 非 Vector2i 应拒绝。");

        GDictionary mismatchedFootprint = Payload();
        mismatchedFootprint["footprint_size"] = Vector2I.One;
        AssertRejected(mismatchedFootprint, "footprint_size 与 body_size 派生结果不一致应拒绝。");

        GDictionary badOccupiedType = Payload();
        badOccupiedType["occupied_coords"] = Vector2I.Zero;
        AssertRejected(badOccupiedType, "occupied_coords 非 Array 应拒绝。");

        GDictionary badOccupiedElement = Payload();
        badOccupiedElement["occupied_coords"] = new GArray { "3,4" };
        AssertRejected(badOccupiedElement, "occupied_coords 含非 Vector2i 元素应拒绝。");

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
        foreach (
            string fieldName in new[]
            {
                "current_hp",
                "current_mp",
                "current_stamina",
                "current_aura",
                "current_ap",
                "current_move_points",
                "stamina_recovery_progress",
                "aura_max",
                "weapon_attack_range",
                "last_turn_tu",
            }
        )
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

        GDictionary badLockHitBonus = Payload();
        DictDictionary(badLockHitBonus, "known_skill_lock_hit_bonus_map")["slash"] = "2";
        AssertRejected(
            badLockHitBonus,
            "known_skill_lock_hit_bonus_map value 非 int 应拒绝。"
        );

        GDictionary negativeLockHitBonus = Payload();
        DictDictionary(negativeLockHitBonus, "known_skill_lock_hit_bonus_map")[
            "slash"
        ] = -1;
        AssertRejected(
            negativeLockHitBonus,
            "known_skill_lock_hit_bonus_map 负值应拒绝。"
        );
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

        GDictionary badVisionTag = Payload();
        badVisionTag["vision_tags"] = new GArray { "darkvision", 3 };
        AssertRejected(badVisionTag, "vision_tags 非 String/StringName 元素应拒绝。");

        GDictionary duplicateProficiencyTag = Payload();
        duplicateProficiencyTag["proficiency_tags"] =
            new GArray { "light_armor", "light_armor" };
        AssertRejected(
            duplicateProficiencyTag,
            "proficiency_tags 重复元素应拒绝。"
        );

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

        GDictionary badEncounterActorId = Payload();
        badEncounterActorId["encounter_actor_id"] = 7;
        AssertRejected(badEncounterActorId, "encounter_actor_id 非 String/StringName 应拒绝。");
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

    private void TestWeaponProjectionStrictLoadPreservesRawUntilCanonicalBoundary()
    {
        GDictionary payload = Payload();
        payload["weapon_attack_range"] = -4;
        payload["weapon_current_grip"] = "two_handed";
        payload["weapon_uses_two_hands"] = false;

        BattleUnitState restored = BattleUnitState.FromDictionary(payload);
        _test.True(restored != null, "严格 codec 应继续接受类型合法的 raw weapon cross-state。");
        if (restored == null)
            return;

        BattleWeaponProjectionValues raw =
            restored.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(raw.AttackRange, -4, "严格加载不得提前 clamp raw attack range。");
        _test.Eq(
            raw.CurrentGrip,
            new StringName("two_handed"),
            "严格加载不得提前修正 raw grip/uses-two-hands 组合。"
        );
        _test.False(raw.UsesTwoHands, "严格加载应保留 raw uses-two-hands。");

        BattleUnitState cloned = restored.clone();
        BattleWeaponProjectionValues canonicalSource =
            restored.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponProjectionValues canonicalClone =
            cloned.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(canonicalSource.AttackRange, 0, "clone 前 canonical 边界应规范化 source。");
        _test.Eq(canonicalClone.AttackRange, 0, "clone 应取得 canonical attack range。");
        _test.Eq(
            canonicalSource.CurrentGrip,
            new StringName("none"),
            "零射程 canonical 状态应清空 grip。"
        );
        _test.False(canonicalClone.UsesTwoHands, "clone 应保留 canonical uses-two-hands。");

        WeaponDice detached = cloned.GetWeaponOneHandedDiceTyped();
        detached.dice_sides = 100;
        _test.Eq(
            restored.GetWeaponProjectionReadViewTyped().Values.OneHandedDice.DiceSides,
            8,
            "clone/getter 的 WeaponDice 修改不得回写 source owner。"
        );
        _test.Eq(
            cloned.GetWeaponProjectionReadViewTyped().Values.OneHandedDice.DiceSides,
            8,
            "getter 返回的 WeaponDice 修改不得回写 clone owner。"
        );
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
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = "schema_unit",
            source_member_id = "member_1",
            enemy_template_id = "schema_template",
            encounter_actor_id = "schema_actor",
            display_name = "Schema Unit",
            faction_id = "player",
            control_mode = "manual",
            versatility_pick = "strength",
        }.WithCombatResourcesForTestExact(
            hp: 21,
            mp: 4,
            stamina: 13,
            aura: 2,
            ap: 1,
            movePoints: 5,
            staminaRecoveryProgress: 7
        );
        unit.ReplaceMovementTagsTyped(
            new GStringNameArray { "grounded" }
        );
        unit.ReplaceVisionProficiencyTagsTyped(
            new GStringNameArray { "darkvision" },
            new GStringNameArray { "light_armor" }
        );
        unit.ReplaceSaveModifiersTyped(
            new GStringNameArray { "charm" },
            new GStringNameArray { "fear" },
            new GStringNameArray { "poison" },
            new Dictionary<StringName, int>
            {
                ["wisdom"] = 3,
                ["constitution"] = 0,
            }
        );
        unit.SetUnlockedCombatResourceIds(
            new GStringNameArray { "hp", "stamina", "aura" }
        );
        unit.MarkRestingTyped();
        unit.SetActionProgressTyped(20);
        unit.SetActionThresholdTyped(140);
        unit.SetKnownActiveSkillIds(new StringName[] { "slash" });
        unit.SetCooldownAnchorTuTyped(50);
        unit.ReplaceShieldStateTyped(
            4,
            8,
            30,
            "ward",
            "schema_unit",
            "ward_skill"
        );
        unit.SetBodySizeProjection(BattleUnitState.BodySizeLarge);
        unit.SetAnchorCoord(new Vector2I(3, 4));
        unit.SetKnownSkillLevelsTyped(new Dictionary<StringName, int> { ["slash"] = 2 });
        unit.SetKnownSkillLockHitBonusesTyped(
            new Dictionary<StringName, int> { ["slash"] = 3 }
        );
        unit.SetDamageResistanceTyped("fire", "half");
        unit.SetCooldownsTyped(new Dictionary<StringName, int> { ["slash"] = 12 });
        unit.attribute_snapshot.SetValue("strength", 3);
        unit.attribute_snapshot.SetValue("aura_max", 6);
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "training_longsword",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
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

    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action?.Invoke();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
