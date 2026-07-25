using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_unit_state_owner_api_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHpApiSynchronizesAliveState();
        TestResourceApiClampsNegativeValues();
        TestCombatResourceOwnerKeepsExactAndRecoveryState();
        TestRestOwnerKeepsCrossActivationAndExactState();
        TestMovementTagOwnerNormalizesAndKeepsExactState();
        TestMovementTagReadViewPreservesTerrainRules();
        TestVisionProficiencyOwnerNormalizesAndKeepsExactState();
        TestSaveModifierOwnerNormalizesAndKeepsExactState();
        TestDamageResistanceOwnerNormalizesAndKeepsExactState();
        TestEffectiveTraitOwnerNormalizesAndKeepsExactState();
        TestCreatureTypeOwnerNormalizesAndKeepsExactState();
        TestGeometryOwnerKeepsDerivedStateAndExactSnapshots();
        TestCombatResourceUnlockOwnerNormalizesAndDetaches();
        TestSkillCostApiOwnsResourceMutation();
        TestCooldownOwnerKeepsMapAndAnchorLifecycle();
        TestActionClockOwnsProgressAndRemainder();
        TestTurnStateKeepsActivationLifecycle();
        TestKnownSkillApiFiltersInternalCollection();
        TestStatusApiCapturesAndClearsOwnedDictionary();
        TestShieldOwnerApiKeepsAtomicState();
        TestEquipmentAbilityProjectionOwnerKeepsAtomicState();
        TestWeaponProjectionOwnerKeepsAtomicState();

        RequestTestExit(_test.Finish("Battle unit state owner API regression"));
    }

    private void TestHpApiSynchronizesAliveState()
    {
        BattleUnitState unit = BuildUnit();

        unit.SetCurrentHp(-10);
        _test.Eq(unit.GetCurrentHp(), 0, "SetCurrentHp 应 clamp 到 0。");
        _test.False(unit.IsAlive(), "HP 为 0 时 BattleUnitState 应同步 is_alive=false。");

        unit.ReviveWithHp(99, 30);
        _test.Eq(unit.GetCurrentHp(), 30, "ReviveWithHp 应按 hpMax clamp。");
        _test.True(unit.IsAlive(), "ReviveWithHp 应同步 is_alive=true。");

        int damage = unit.ApplyHpDamage(12);
        _test.Eq(damage, 12, "ApplyHpDamage 应返回实际伤害。");
        _test.Eq(unit.GetCurrentHp(), 18, "ApplyHpDamage 应扣减 HP。");

        int healing = unit.ApplyHealing(100, 25);
        _test.Eq(healing, 7, "ApplyHealing 应返回实际治疗量。");
        _test.Eq(unit.GetCurrentHp(), 25, "ApplyHealing 不应超过 hpMax。");

        unit.MarkDead();
        _test.Eq(unit.GetCurrentHp(), 0, "MarkDead 应清空 HP。");
        _test.False(unit.IsAlive(), "MarkDead 应同步 is_alive=false。");
    }

    private void TestResourceApiClampsNegativeValues()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetCombatResources(-1, -2, -3, -4, -5, -6);

        _test.Eq(unit.GetCurrentHp(), 0, "SetCombatResources 应 clamp HP。");
        _test.Eq(unit.GetCurrentMp(), 0, "SetCombatResources 应 clamp MP。");
        _test.Eq(unit.GetCurrentStamina(), 0, "SetCombatResources 应 clamp stamina。");
        _test.Eq(unit.GetCurrentAura(), 0, "SetCombatResources 应 clamp aura。");
        _test.Eq(unit.GetCurrentAp(), 0, "SetCombatResources 应 clamp AP。");
        _test.Eq(unit.GetCurrentMovePoints(), 0, "SetCombatResources 应 clamp move points。");

        unit.SetCombatResources(99, 99, 99, 99, 99, 99);
        unit.ClampCombatResources(new BattleResourceCaps(30, 10, 20, 3, 2, 6));
        _test.Eq(unit.GetCurrentHp(), 30, "ClampCombatResources 应 clamp HP 上限。");
        _test.Eq(unit.GetCurrentMp(), 10, "ClampCombatResources 应 clamp MP 上限。");
        _test.Eq(unit.GetCurrentStamina(), 20, "ClampCombatResources 应 clamp stamina 上限。");
        _test.Eq(unit.GetCurrentAura(), 3, "ClampCombatResources 应 clamp aura 上限。");
        _test.Eq(unit.GetCurrentAp(), 2, "ClampCombatResources 应 clamp AP 上限。");
        _test.Eq(unit.GetCurrentMovePoints(), 6, "ClampCombatResources 应 clamp move points 上限。");
    }

    private void TestCombatResourceOwnerKeepsExactAndRecoveryState()
    {
        var unit = new BattleUnitState();
        BattleUnitCombatResourceReadView defaults =
            unit.GetCombatResourcesReadViewTyped();
        _test.True(defaults.OwnerPresent, "combat-resource owner 默认应存在。");
        _test.Eq(defaults.Values.Hp, 0, "combat-resource 默认 HP 应保持 0。");
        _test.Eq(defaults.Values.Mp, 0, "combat-resource 默认 MP 应保持 0。");
        _test.Eq(defaults.Values.Stamina, 0, "combat-resource 默认 stamina 应保持 0。");
        _test.Eq(defaults.Values.Aura, 0, "combat-resource 默认 aura 应保持 0。");
        _test.Eq(defaults.Values.Ap, 0, "combat-resource 默认 AP 应保持 0。");
        _test.Eq(
            defaults.Values.MovePoints,
            BattleUnitState.DefaultMovePointsPerTurn,
            "combat-resource 默认 move points 应保持既有常量。"
        );
        _test.Eq(
            defaults.Values.StaminaRecoveryProgress,
            0,
            "combat-resource 默认 stamina recovery progress 应为 0。"
        );
        _test.True(
            defaults.Values.IsAlive,
            "combat-resource 默认 alive=true 的既有语义不得改变。"
        );

        var rawValues = new BattleUnitCombatResourceValues(
            -1,
            -2,
            -3,
            -4,
            -5,
            -6,
            -7,
            false
        );
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(rawValues)
        );
        _test.Eq(
            unit.CaptureCombatResourcesForMutationSnapshotExact().Values,
            rawValues,
            "exact combat-resource seam 应原样保留全部 8 项 raw sentinel。"
        );

        BattleUnitCombatResourceReadView detached =
            unit.GetCombatResourcesReadViewTyped();
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(
                rawValues with { Hp = 99, IsAlive = true }
            )
        );
        _test.Eq(
            detached.Values,
            rawValues,
            "combat-resource read view 应是 detached value snapshot。"
        );

        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.MissingOwner
        );
        _test.False(
            unit.GetCombatResourcesReadViewTyped().OwnerPresent,
            "exact combat-resource seam 应保留 missing owner，不能静默重建。"
        );
        _test.False(
            unit.CaptureCombatResourcesForMutationSnapshotExact().OwnerPresent,
            "missing combat-resource owner 的 exact capture 应继续标记 missing。"
        );

        RestoreRecoveryState(unit, stamina: 1, progress: 0);
        _test.False(
            unit.ApplyStaminaRecoveryTyped(1, 3, 9, 10),
            "未跨 recovery denominator 时不应报告 stamina 恢复。"
        );
        _test.Eq(unit.GetCurrentStamina(), 1, "未跨 denominator 时 stamina 不应变化。");
        _test.Eq(
            unit.GetStaminaRecoveryProgressTyped(),
            9,
            "未跨 denominator 的 recovery progress 应留在 owner 中。"
        );

        _test.True(
            unit.ApplyStaminaRecoveryTyped(1, 3, 1, 10),
            "跨 recovery denominator 时应报告 stamina 恢复。"
        );
        _test.Eq(unit.GetCurrentStamina(), 2, "跨 denominator 应恢复 1 点 stamina。");
        _test.Eq(
            unit.GetStaminaRecoveryProgressTyped(),
            0,
            "跨 denominator 后应保留正确余数。"
        );

        RestoreRecoveryState(unit, stamina: 3, progress: 5);
        _test.True(
            unit.ApplyStaminaRecoveryTyped(1, 3, 1, 10),
            "stamina 已满但仍有 progress 时应报告清理。"
        );
        _test.Eq(unit.GetCurrentStamina(), 3, "满 stamina 清理不应改变 stamina。");
        _test.Eq(
            unit.GetStaminaRecoveryProgressTyped(),
            0,
            "stamina 已满时应清空 recovery progress。"
        );

        RestoreRecoveryState(unit, stamina: 2, progress: 5);
        _test.True(
            unit.ApplyStaminaRecoveryTyped(1, 0, 1, 10),
            "stamina max<=0 时应报告资源与 progress 清理。"
        );
        _test.Eq(unit.GetCurrentStamina(), 0, "stamina max<=0 时应清空 stamina。");
        _test.Eq(
            unit.GetStaminaRecoveryProgressTyped(),
            0,
            "stamina max<=0 时应清空 recovery progress。"
        );
    }

    private static void RestoreRecoveryState(
        BattleUnitState unit,
        int stamina,
        int progress
    )
    {
        BattleUnitCombatResourceValues values =
            unit.CaptureCombatResourcesForMutationSnapshotExact().Values;
        unit.RestoreCombatResourcesForMutationSnapshotExact(
            BattleUnitCombatResourceSnapshot.Present(
                values with
                {
                    Stamina = stamina,
                    StaminaRecoveryProgress = progress,
                }
            )
        );
    }

    private void TestRestOwnerKeepsCrossActivationAndExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitRestSnapshot defaults = unit.GetRestStateTyped();
        _test.True(defaults.OwnerPresent, "rest owner 默认应存在。");
        _test.False(defaults.IsResting, "rest owner 默认不应处于休息状态。");

        unit.MarkRestingTyped();
        _test.True(unit.IsRestingTyped(), "回合结束未行动时应进入休息状态。");
        unit.ResetTurnStateForTurnStartTyped();
        _test.True(
            unit.IsRestingTyped(),
            "resting 跨 activation 保留，不得随 turn flags 一起清除。"
        );

        BattleUnitState clone = unit.clone();
        _test.True(clone.IsRestingTyped(), "gameplay clone 应保留 resting。");
        clone.ClearRestingTyped();
        _test.True(unit.IsRestingTyped(), "clone 的 rest owner 不得与 source 共享。");

        unit.RestoreRestForMutationSnapshotExact(
            BattleUnitRestSnapshot.MissingOwner
        );
        _test.False(
            unit.CaptureRestForMutationSnapshotExact().OwnerPresent,
            "exact rest seam 应保留 missing owner，不能静默重建。"
        );

        unit.MarkRestingTyped();
        BattleUnitRestSnapshot rematerialized = unit.GetRestStateTyped();
        _test.True(rematerialized.OwnerPresent, "正常 rest 写入口应重建 missing owner。");
        _test.True(rematerialized.IsResting, "重建后应写入 resting=true。");

        unit.ClearRestingTyped();
        _test.False(unit.IsRestingTyped(), "实际行动后应退出休息状态。");
    }

    private void TestCreatureTypeOwnerNormalizesAndKeepsExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitCreatureTypeReadView defaults =
            unit.GetCreatureTypeTagsReadViewTyped();
        _test.True(defaults.OwnerPresent, "creature-type owner 默认应存在。");
        _test.True(defaults.Tags.IsPresent, "默认 creature tags 集合应存在。");
        _test.Eq(defaults.Tags.Count, 0, "默认 creature tags 应为空。");

        unit.ReplaceCreatureTypeTagsTyped(
            new StringNameList { "beast", "", "beast", "undead" }
        );
        BattleUnitCreatureTypeReadView normalized =
            unit.GetCreatureTypeTagsReadViewTyped();
        _test.Eq(normalized.Tags.Count, 2, "正常写入口应过滤空值并去重。");
        _test.Eq(normalized.Tags[0], new StringName("beast"), "去重后应保留首次顺序。");
        _test.Eq(normalized.Tags[1], new StringName("undead"), "后续合法标签应保序。");
        _test.False(
            unit.AddCreatureTypeTagTyped("beast"),
            "重复 creature tag 不应再次写入。"
        );
        _test.True(
            unit.AddCreatureTypeTagTyped("construct"),
            "新的 creature tag 应追加成功。"
        );

        var rawTags = new StringNameList
        {
            "raw",
            "",
            "raw",
        };
        unit.RestoreCreatureTypesForMutationSnapshotExact(
            BattleUnitCreatureTypeSnapshot.Present(rawTags)
        );
        BattleUnitCreatureTypeSnapshot raw =
            unit.CaptureCreatureTypesForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact creature-type snapshot 应保留 owner presence。");
        _test.Eq(raw.Tags.Count, 3, "exact seam 不应过滤 raw 标签。");
        _test.Eq(raw.Tags[1], new StringName(""), "exact seam 应保留空标签 sentinel。");
        _test.Eq(raw.Tags[2], new StringName("raw"), "exact seam 应保留重复与顺序。");

        unit.RestoreCreatureTypesForMutationSnapshotExact(
            BattleUnitCreatureTypeSnapshot.Present(null)
        );
        BattleUnitState clone = unit.clone();
        _test.True(
            clone.GetCreatureTypeTagsReadViewTyped().Tags.IsPresent,
            "gameplay clone 应把 null creature tags 归一为空集合。"
        );
        _test.Eq(
            clone.GetCreatureTypeTagsReadViewTyped().Tags.Count,
            0,
            "gameplay clone 的 null creature tags 应归一为空集合。"
        );
        clone.AddCreatureTypeTagTyped("mutated_clone");
        _test.True(
            unit.CaptureCreatureTypesForMutationSnapshotExact().Tags == null,
            "clone 的 creature-type owner 不得与 source 共享。"
        );

        unit.RestoreCreatureTypesForMutationSnapshotExact(
            BattleUnitCreatureTypeSnapshot.MissingOwner
        );
        _test.False(
            unit.GetCreatureTypeTagsReadViewTyped().OwnerPresent,
            "exact creature-type seam 应保留 missing owner。"
        );
        BattleUnitState missingOwnerClone = unit.clone();
        _test.False(
            unit.GetCreatureTypeTagsReadViewTyped().OwnerPresent,
            "gameplay clone 不得在 source 上物化 missing creature-type owner。"
        );
        _test.True(
            missingOwnerClone.GetCreatureTypeTagsReadViewTyped().OwnerPresent,
            "gameplay clone 应把 missing creature-type owner 归一为默认 owner。"
        );
        _test.Eq(
            missingOwnerClone.GetCreatureTypeTagsReadViewTyped().Tags.Count,
            0,
            "gameplay clone 的默认 creature-type owner 应为空集合。"
        );
        _test.True(
            unit.AddCreatureTypeTagTyped("rematerialized"),
            "正常 creature-type 写入口应重建 missing owner。"
        );
        _test.True(
            unit.GetCreatureTypeTagsReadViewTyped().OwnerPresent,
            "正常写入口重建后 owner 应存在。"
        );
        _test.True(
            unit.HasCreatureTypeTag("rematerialized"),
            "重建 owner 后应写入新标签。"
        );
    }

    private void TestMovementTagOwnerNormalizesAndKeepsExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitMovementTagReadView defaults =
            unit.GetMovementTagsReadViewTyped();
        _test.True(defaults.OwnerPresent, "movement-tag owner 默认应存在。");
        _test.True(defaults.Tags.IsPresent, "默认 movement tags 集合应存在。");
        _test.Eq(defaults.Tags.Count, 0, "默认 movement tags 应为空。");

        unit.ReplaceMovementTagsTyped(
            new StringNameList { "grounded", "", "grounded", "flying" }
        );
        BattleUnitMovementTagReadView normalized =
            unit.GetMovementTagsReadViewTyped();
        _test.Eq(normalized.Tags.Count, 2, "正常写入口应过滤空值并去重。");
        _test.Eq(
            normalized.Tags[0],
            new StringName("grounded"),
            "去重后应保留首次顺序。"
        );
        _test.Eq(
            normalized.Tags[1],
            new StringName("flying"),
            "后续合法标签应保序。"
        );
        _test.False(
            unit.AddMovementTagTyped("grounded"),
            "重复 movement tag 不应再次写入。"
        );
        _test.True(
            unit.AddMovementTagTyped("amphibious"),
            "新的 movement tag 应追加成功。"
        );

        var rawTags = new StringNameList
        {
            "raw",
            "",
            "raw",
        };
        unit.RestoreMovementTagsForMutationSnapshotExact(
            BattleUnitMovementTagSnapshot.Present(rawTags)
        );
        BattleUnitMovementTagSnapshot raw =
            unit.CaptureMovementTagsForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact movement-tag snapshot 应保留 owner presence。");
        _test.Eq(raw.Tags.Count, 3, "exact seam 不应过滤 raw 标签。");
        _test.Eq(raw.Tags[1], new StringName(""), "exact seam 应保留空标签 sentinel。");
        _test.Eq(raw.Tags[2], new StringName("raw"), "exact seam 应保留重复与顺序。");

        unit.RestoreMovementTagsForMutationSnapshotExact(
            BattleUnitMovementTagSnapshot.Present(null)
        );
        BattleUnitState clone = unit.clone();
        _test.True(
            clone.GetMovementTagsReadViewTyped().Tags.IsPresent,
            "gameplay clone 应把 null movement tags 归一为空集合。"
        );
        _test.Eq(
            clone.GetMovementTagsReadViewTyped().Tags.Count,
            0,
            "gameplay clone 的 null movement tags 应归一为空集合。"
        );
        clone.AddMovementTagTyped("mutated_clone");
        _test.True(
            unit.CaptureMovementTagsForMutationSnapshotExact().Tags == null,
            "clone 的 movement-tag owner 不得与 source 共享。"
        );

        unit.RestoreMovementTagsForMutationSnapshotExact(
            BattleUnitMovementTagSnapshot.MissingOwner
        );
        _test.False(
            unit.GetMovementTagsReadViewTyped().OwnerPresent,
            "exact movement-tag seam 应保留 missing owner。"
        );
        BattleUnitState missingOwnerClone = unit.clone();
        _test.False(
            unit.GetMovementTagsReadViewTyped().OwnerPresent,
            "gameplay clone 不得在 source 上物化 missing movement-tag owner。"
        );
        _test.True(
            missingOwnerClone.GetMovementTagsReadViewTyped().OwnerPresent,
            "gameplay clone 应把 missing movement-tag owner 归一为默认 owner。"
        );
        _test.Eq(
            missingOwnerClone.GetMovementTagsReadViewTyped().Tags.Count,
            0,
            "gameplay clone 的默认 movement-tag owner 应为空集合。"
        );
        _test.True(
            unit.AddMovementTagTyped("rematerialized"),
            "正常 movement-tag 写入口应重建 missing owner。"
        );
        _test.True(
            unit.GetMovementTagsReadViewTyped().OwnerPresent,
            "正常写入口重建后 owner 应存在。"
        );
        _test.True(
            unit.HasMovementTag("rematerialized"),
            "重建 owner 后应写入新标签。"
        );
    }

    private void TestMovementTagReadViewPreservesTerrainRules()
    {
        var unit = new BattleUnitState();
        StringName deepWater = BattleTerrainRules.ToStringName(
            BattleTerrainKind.DeepWater
        );

        unit.ReplaceMovementTagsTyped(new StringNameList { "fly" });
        BattleMovementTagReadView flyingTags =
            unit.GetMovementTagsReadViewTyped().Tags;
        _test.True(
            BattleTerrainRules.CanUnitEnterTerrain(deepWater, flyingTags),
            "fly read view 应保持深水可通行语义。"
        );
        _test.Eq(
            BattleTerrainRules.GetUnitMoveCost(deepWater, flyingTags),
            1,
            "fly read view 应保持深水移动成本。"
        );

        unit.ReplaceMovementTagsTyped(
            new StringNameList { "amphibious" }
        );
        BattleMovementTagReadView amphibiousTags =
            unit.GetMovementTagsReadViewTyped().Tags;
        _test.True(
            BattleTerrainRules.CanUnitEnterTerrain(
                deepWater,
                amphibiousTags
            ),
            "amphibious read view 应保持深水可通行语义。"
        );
        _test.Eq(
            BattleTerrainRules.GetUnitMoveCost(
                deepWater,
                amphibiousTags
            ),
            2,
            "amphibious read view 应保持深水移动成本。"
        );
    }

    private void TestVisionProficiencyOwnerNormalizesAndKeepsExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitVisionProficiencyReadView defaults =
            unit.GetVisionProficiencyReadViewTyped();
        _test.True(defaults.OwnerPresent, "vision/proficiency owner 默认应存在。");
        _test.True(defaults.VisionTags.IsPresent, "默认 vision tags 集合应存在。");
        _test.True(
            defaults.ProficiencyTags.IsPresent,
            "默认 proficiency tags 集合应存在。"
        );

        unit.ReplaceVisionProficiencyTagsTyped(
            new StringNameList { "normal_vision", "", "normal_vision", "darkvision" },
            new StringNameList { "civilian", "", "civilian", "light_armor" }
        );
        BattleUnitVisionProficiencyReadView normalized =
            unit.GetVisionProficiencyReadViewTyped();
        _test.Eq(normalized.VisionTags.Count, 2, "vision 正常写入口应过滤空值并去重。");
        _test.Eq(
            normalized.VisionTags[1],
            new StringName("darkvision"),
            "vision 去重后应保留首次顺序。"
        );
        _test.Eq(
            normalized.ProficiencyTags.Count,
            2,
            "proficiency 正常写入口应过滤空值并去重。"
        );
        _test.Eq(
            normalized.ProficiencyTags[1],
            new StringName("light_armor"),
            "proficiency 去重后应保留首次顺序。"
        );
        _test.False(
            unit.AddVisionTagTyped("darkvision"),
            "重复 vision tag 不应再次写入。"
        );
        _test.True(
            unit.AddProficiencyTagTyped("weapon_type_spear"),
            "新的 proficiency tag 应追加成功。"
        );

        unit.RestoreVisionProficiencyForMutationSnapshotExact(
            BattleUnitVisionProficiencySnapshot.Present(
                new StringNameList { "raw_vision", "", "raw_vision" },
                new StringNameList
                {
                    "raw_proficiency",
                    "",
                    "raw_proficiency",
                }
            )
        );
        BattleUnitVisionProficiencySnapshot raw =
            unit.CaptureVisionProficiencyForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact snapshot 应保留 owner presence。");
        _test.Eq(raw.VisionTags.Count, 3, "exact seam 应保留 raw vision 标签。");
        _test.Eq(
            raw.VisionTags[1],
            new StringName(""),
            "exact seam 应保留 vision 空标签 sentinel。"
        );
        _test.Eq(
            raw.ProficiencyTags[2],
            new StringName("raw_proficiency"),
            "exact seam 应保留 proficiency 重复与顺序。"
        );

        unit.RestoreVisionProficiencyForMutationSnapshotExact(
            BattleUnitVisionProficiencySnapshot.Present(
                null,
                new StringNameList { "raw_proficiency" }
            )
        );
        BattleUnitState clone = unit.clone();
        BattleUnitVisionProficiencyReadView cloneView =
            clone.GetVisionProficiencyReadViewTyped();
        _test.True(
            cloneView.VisionTags.IsPresent,
            "gameplay clone 应把 null vision tags 归一为空集合。"
        );
        _test.Eq(cloneView.VisionTags.Count, 0, "clone 的 vision tags 应归一为空。");
        _test.Eq(
            cloneView.ProficiencyTags.Count,
            1,
            "clone 应保留非 null proficiency tags。"
        );
        clone.AddProficiencyTagTyped("mutated_clone");
        _test.Eq(
            unit.CaptureVisionProficiencyForMutationSnapshotExact()
                .ProficiencyTags.Count,
            1,
            "clone 的 vision/proficiency owner 不得与 source 共享。"
        );

        unit.RestoreVisionProficiencyForMutationSnapshotExact(
            BattleUnitVisionProficiencySnapshot.MissingOwner
        );
        BattleUnitState missingOwnerClone = unit.clone();
        _test.False(
            unit.GetVisionProficiencyReadViewTyped().OwnerPresent,
            "gameplay clone 不得在 source 上物化 missing owner。"
        );
        _test.True(
            missingOwnerClone.GetVisionProficiencyReadViewTyped().OwnerPresent,
            "gameplay clone 应把 missing owner 归一为默认 owner。"
        );
        _test.True(
            unit.AddVisionTagTyped("rematerialized"),
            "正常 vision 写入口应重建 missing owner。"
        );
        _test.True(
            unit.HasVisionTag("rematerialized"),
            "重建 owner 后应写入 vision tag。"
        );
    }

    private void TestSaveModifierOwnerNormalizesAndKeepsExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitSaveModifierReadView defaults =
            unit.GetSaveModifiersReadViewTyped();
        _test.True(defaults.OwnerPresent, "save-modifier owner 默认应存在。");
        _test.True(defaults.AdvantageTags.IsPresent, "默认 advantage tags 应存在。");
        _test.True(
            defaults.DisadvantageTags.IsPresent,
            "默认 disadvantage tags 应存在。"
        );
        _test.True(defaults.ImmunityTags.IsPresent, "默认 immunity tags 应存在。");
        _test.True(defaults.BonusByAbility.IsPresent, "默认 ability bonus map 应存在。");

        unit.ReplaceSaveModifiersTyped(
            new StringNameList { "charm", "", "charm", "fear" },
            new StringNameList { "poison", "", "poison", "curse" },
            new StringNameList { "sleep", "", "sleep", "death" },
            new Dictionary<StringName, int>
            {
                ["wisdom"] = 2,
                [""] = 99,
            }
        );
        BattleUnitSaveModifierReadView normalized =
            unit.GetSaveModifiersReadViewTyped();
        _test.Eq(normalized.AdvantageTags.Count, 2, "advantage tags 应过滤空值并去重。");
        _test.Eq(
            normalized.AdvantageTags[1],
            new StringName("fear"),
            "advantage tags 应保留首次出现顺序。"
        );
        _test.Eq(
            normalized.DisadvantageTags[1],
            new StringName("curse"),
            "disadvantage tags 应过滤空值、去重并保序。"
        );
        _test.Eq(
            normalized.ImmunityTags[1],
            new StringName("death"),
            "immunity tags 应过滤空值、去重并保序。"
        );
        _test.Eq(normalized.BonusByAbility.Count, 1, "ability bonus 应过滤空 ability。");

        _test.True(
            unit.AddSaveBonusByAbilityTyped("wisdom", 3),
            "相同 ability 的 bonus 应累加。"
        );
        _test.Eq(
            unit.GetSaveBonusByAbilityTyped("wisdom"),
            5,
            "相同 ability 的 bonus 应保留累加结果。"
        );
        _test.True(
            unit.AddSaveBonusByAbilityTyped("wisdom", -5),
            "反向 bonus 应参与同一 ability 累加。"
        );
        _test.Eq(
            unit.GetSaveBonusByAbilityTyped("wisdom", 99),
            0,
            "bonus 抵消后应保留显式 0，而不是删除 ability。"
        );
        _test.True(
            unit.GetSaveModifiersReadViewTyped()
                .BonusByAbility.TryGetValue("wisdom", out int zeroBonus)
            && zeroBonus == 0,
            "显式 0 ability bonus 应继续存在于 owner map。"
        );

        var rawImmunityTags = new StringNameList
        {
            "raw_immunity",
            "",
            "raw_immunity",
        };
        var rawBonuses = new BattleStringNameIntMap();
        rawBonuses.Put("constitution", 0);
        rawBonuses.Put("wisdom", -2);
        unit.RestoreSaveModifiersForMutationSnapshotExact(
            BattleUnitSaveModifierSnapshot.Present(
                null,
                new StringNameList(),
                rawImmunityTags,
                rawBonuses
            )
        );
        rawImmunityTags.Clear();
        rawBonuses.Put("wisdom", 99);
        BattleUnitSaveModifierSnapshot raw =
            unit.CaptureSaveModifiersForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact save-modifier snapshot 应保留 owner presence。");
        _test.True(raw.AdvantageTags == null, "exact seam 应保留 null advantage tags。");
        _test.Eq(raw.DisadvantageTags.Count, 0, "exact seam 应区分 empty 与 null tags。");
        _test.Eq(raw.ImmunityTags.Count, 3, "exact restore 应深拷贝 raw immunity tags。");
        _test.Eq(
            raw.ImmunityTags[1],
            new StringName(""),
            "exact seam 应保留空标签 sentinel。"
        );
        _test.Eq(
            raw.ImmunityTags[2],
            new StringName("raw_immunity"),
            "exact seam 应保留重复标签与顺序。"
        );
        _test.Eq(raw.BonusByAbility.Get("constitution", 99), 0, "exact seam 应保留显式 0 bonus。");
        _test.Eq(raw.BonusByAbility.Get("wisdom"), -2, "exact restore 应深拷贝 raw bonus map。");
        raw.ImmunityTags.Clear();
        raw.BonusByAbility.Put("wisdom", 77);
        BattleUnitSaveModifierSnapshot recaptured =
            unit.CaptureSaveModifiersForMutationSnapshotExact();
        _test.Eq(recaptured.ImmunityTags.Count, 3, "exact capture 应返回 detached tags。");
        _test.Eq(recaptured.BonusByAbility.Get("wisdom"), -2, "exact capture 应返回 detached map。");

        BattleUnitState clone = unit.clone();
        BattleUnitSaveModifierReadView cloneView =
            clone.GetSaveModifiersReadViewTyped();
        _test.True(
            cloneView.AdvantageTags.IsPresent,
            "gameplay clone 应把 null advantage tags 分组件归一为空集合。"
        );
        _test.Eq(cloneView.AdvantageTags.Count, 0, "clone 的 null advantage tags 应为空。");
        _test.Eq(
            cloneView.ImmunityTags.Count,
            3,
            "clone 应保留非 null immunity tags 的 raw 形态。"
        );
        clone.AddSaveImmunityTagTyped("clone_only");
        clone.AddSaveBonusByAbilityTyped("wisdom", 5);
        _test.Eq(
            unit.CaptureSaveModifiersForMutationSnapshotExact().ImmunityTags.Count,
            3,
            "clone 的 tag 写入不得回写 source owner。"
        );
        _test.Eq(
            unit.GetSaveBonusByAbilityTyped("wisdom"),
            -2,
            "clone 的 bonus 写入不得回写 source owner。"
        );

        unit.RestoreSaveModifiersForMutationSnapshotExact(
            BattleUnitSaveModifierSnapshot.Present(null, null, null, null)
        );
        BattleUnitSaveModifierReadView nullComponentClone =
            unit.clone().GetSaveModifiersReadViewTyped();
        _test.True(
            nullComponentClone.AdvantageTags.IsPresent
            && nullComponentClone.DisadvantageTags.IsPresent
            && nullComponentClone.ImmunityTags.IsPresent
            && nullComponentClone.BonusByAbility.IsPresent,
            "gameplay clone 应把四个 present-null 组件分别归一为空集合。"
        );

        unit.RestoreSaveModifiersForMutationSnapshotExact(
            BattleUnitSaveModifierSnapshot.MissingOwner
        );
        _test.False(
            unit.GetSaveModifiersReadViewTyped().OwnerPresent,
            "exact save-modifier seam 应保留 missing owner。"
        );
        _test.True(
            unit.AddSaveAdvantageTagTyped("rematerialized"),
            "正常 save-modifier 写入口应重建 missing owner。"
        );
        BattleUnitSaveModifierReadView rematerialized =
            unit.GetSaveModifiersReadViewTyped();
        _test.True(rematerialized.OwnerPresent, "正常写入口重建后 owner 应存在。");
        _test.True(
            unit.HasSaveAdvantageTag("rematerialized"),
            "重建 owner 后应写入 advantage tag。"
        );
        _test.True(
            rematerialized.DisadvantageTags.IsPresent
            && rematerialized.ImmunityTags.IsPresent
            && rematerialized.BonusByAbility.IsPresent,
            "missing owner 重物化后其余组件也应恢复默认空 owner。"
        );
    }

    private void TestDamageResistanceOwnerNormalizesAndKeepsExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitDamageResistanceReadView defaults =
            unit.GetDamageResistancesReadViewTyped();
        _test.True(
            defaults.OwnerPresent,
            "damage-resistance owner 默认应存在。"
        );
        _test.True(
            defaults.Resistances.IsPresent,
            "默认 damage resistance map 应存在。"
        );
        _test.Eq(
            defaults.Resistances.Count,
            0,
            "默认 damage resistance map 应为空。"
        );

        var source = new Dictionary<StringName, StringName>
        {
            ["fire"] = "half",
            [""] = "immune",
            ["cold"] = "",
        };
        unit.ReplaceDamageResistancesTyped(source);
        source["fire"] = "double";
        _test.Eq(
            unit.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "normal replace 应过滤空 key/value 并深拷贝输入。"
        );
        _test.Eq(
            unit.GetDamageResistancesReadViewTyped()
                .Resistances.Count,
            1,
            "normal replace 应只保留有效 damage resistance。"
        );
        _test.False(
            unit.SetDamageResistanceTyped("", "immune"),
            "normal set 应拒绝空 damage tag。"
        );
        _test.False(
            unit.SetDamageResistanceTyped("cold", ""),
            "normal set 应拒绝空 mitigation tier。"
        );

        Dictionary<StringName, StringName> detached =
            unit.GetDamageResistancesTyped();
        detached["fire"] = "double";
        _test.Eq(
            unit.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "detached damage resistance copy 不得回写 owner。"
        );

        var rawMap = new BattleStringNameMap();
        rawMap.Put("fire", "double");
        unit.RestoreDamageResistancesForMutationSnapshotExact(
            BattleUnitDamageResistanceSnapshot.Present(rawMap)
        );
        rawMap.Put("fire", "immune");
        BattleUnitDamageResistanceSnapshot raw =
            unit.CaptureDamageResistancesForMutationSnapshotExact();
        _test.True(
            raw.OwnerPresent,
            "exact damage-resistance snapshot 应保留 owner presence。"
        );
        _test.Eq(
            raw.Resistances.Get("fire"),
            new StringName("double"),
            "exact restore 应深拷贝输入 map。"
        );
        raw.Resistances.Put("fire", "half");
        _test.Eq(
            unit.CaptureDamageResistancesForMutationSnapshotExact()
                .Resistances.Get("fire"),
            new StringName("double"),
            "exact capture 应返回 detached map。"
        );

        BattleUnitState clone = unit.clone();
        clone.SetDamageResistanceTyped("fire", "immune");
        _test.Eq(
            unit.GetDamageResistanceTyped("fire"),
            new StringName("double"),
            "gameplay clone 的 damage resistance 写入不得回写 source。"
        );

        unit.RestoreDamageResistancesForMutationSnapshotExact(
            BattleUnitDamageResistanceSnapshot.Present(null)
        );
        BattleUnitDamageResistanceReadView presentNull =
            unit.GetDamageResistancesReadViewTyped();
        _test.True(
            presentNull.OwnerPresent,
            "exact seam 应区分 present-null 与 missing owner。"
        );
        _test.False(
            presentNull.Resistances.IsPresent,
            "exact seam 应保留 present-null map。"
        );
        BattleUnitDamageResistanceReadView normalizedClone =
            unit.clone().GetDamageResistancesReadViewTyped();
        _test.True(
            normalizedClone.Resistances.IsPresent
            && normalizedClone.Resistances.Count == 0,
            "gameplay clone 应把 present-null map 归一为空 map。"
        );

        unit.RestoreDamageResistancesForMutationSnapshotExact(
            BattleUnitDamageResistanceSnapshot.MissingOwner
        );
        _test.False(
            unit.GetDamageResistancesReadViewTyped().OwnerPresent,
            "exact seam 应保留 missing damage-resistance owner。"
        );
        _test.True(
            unit.SetDamageResistanceTyped("cold", "immune"),
            "normal write 应重建 missing damage-resistance owner。"
        );
        _test.True(
            unit.GetDamageResistancesReadViewTyped().OwnerPresent
            && unit.HasDamageResistanceTyped("cold"),
            "normal write 重建 owner 后应保存 resistance。"
        );
    }

    private void TestEffectiveTraitOwnerNormalizesAndKeepsExactState()
    {
        var unit = new BattleUnitState();
        BattleUnitEffectiveTraitReadView defaults =
            unit.GetEffectiveTraitsReadViewTyped();
        _test.True(defaults.OwnerPresent, "effective-trait owner 默认应存在。");
        _test.True(defaults.Instances.IsPresent, "默认 instances 应存在。");
        _test.True(defaults.TraitIds.IsPresent, "默认 trait ids 应存在。");
        _test.Eq(defaults.Instances.Count, 0, "默认 instances 应为空。");

        var firstRoll = TraitRollValueState.CreateInt("amount", 3);
        var first = new BattleEffectiveTraitInstanceState
        {
            trait_id = "zeta_trait",
            effective_instance_key = "zeta@character",
            source_type = "character",
            source_id = "hero",
            effect_type = "passive_stat",
            trigger_type = "passive",
            charge_scope = "none",
            charge_reset_timing = "none",
            rank = 2,
            stacks = 1,
            roll_values = new List<TraitRollValueState> { firstRoll },
        };
        var second = new BattleEffectiveTraitInstanceState
        {
            trait_id = "alpha_trait",
            effective_instance_key = "alpha@equipment_a",
            source_type = "equipment_fixed",
            source_id = "equipment_a",
            effect_type = "passive_stat",
            trigger_type = "passive",
            charge_scope = "none",
            charge_reset_timing = "none",
        };
        var third = second.DuplicateState();
        third.effective_instance_key = "alpha@equipment_b";
        third.source_id = "equipment_b";
        var input = new List<BattleEffectiveTraitInstanceState>
        {
            first,
            null,
            second,
            third,
        };

        unit.ReplaceEffectiveTraitsTyped(input);
        first.trait_id = "mutated_input";
        firstRoll.int_value = 99;
        input.Clear();

        BattleUnitEffectiveTraitReadView normalized =
            unit.GetEffectiveTraitsReadViewTyped();
        _test.Eq(normalized.Instances.Count, 3, "normal replace 应过滤 null instance。");
        _test.Eq(
            normalized.Instances[0].TraitId,
            new StringName("zeta_trait"),
            "normal replace 应保留实例顺序并深拷贝 scalar。"
        );
        _test.Eq(
            normalized.Instances[1].EffectiveInstanceKey,
            new StringName("alpha@equipment_a"),
            "normal replace 不应按 trait id 合并重复实例。"
        );
        _test.Eq(normalized.TraitIds.Count, 2, "trait ids 应按 trait id 去重。");
        _test.Eq(
            normalized.TraitIds[0],
            new StringName("alpha_trait"),
            "trait ids 应按 ordinal 排序。"
        );
        _test.Eq(
            normalized.TraitIds[1],
            new StringName("zeta_trait"),
            "trait ids 应保留完整派生集合。"
        );
        List<BattleEffectiveTraitInstanceState> detached =
            unit.CopyEffectiveTraitInstancesTyped();
        _test.Eq(
            detached[0].roll_values[0].int_value,
            3,
            "normal replace 应深拷贝 nested roll value。"
        );
        detached[0].trait_id = "mutated_detached";
        detached[0].roll_values[0].int_value = 77;
        _test.Eq(
            unit.GetEffectiveTraitsReadViewTyped().Instances[0].TraitId,
            new StringName("zeta_trait"),
            "detached copy 修改不得回写 owner entry。"
        );
        _test.Eq(
            unit.CopyEffectiveTraitInstancesTyped()[0].roll_values[0].int_value,
            3,
            "detached copy 修改不得回写 owner roll value。"
        );

        var rawRoll = new TraitRollValueState
        {
            key = "raw_roll",
            value_type = "raw_type",
            int_value = -3,
        };
        var rawEntry = new BattleEffectiveTraitInstanceState
        {
            trait_id = "raw_trait",
            effective_instance_key = "raw_instance",
            source_type = "raw_source",
            source_id = "raw_source_id",
            effect_type = "raw_effect",
            trigger_type = "raw_trigger",
            charge_scope = "raw_scope",
            charge_reset_timing = "raw_reset",
            rank = 0,
            stacks = -2,
            roll_values = new List<TraitRollValueState>
            {
                null,
                rawRoll,
                rawRoll.DuplicateState(),
            },
        };
        var rawInstances = new List<BattleEffectiveTraitInstanceState>
        {
            null,
            rawEntry,
        };
        var rawIds = new StringNameList
        {
            "stale_trait",
            "",
            "stale_trait",
        };
        unit.RestoreEffectiveTraitsForMutationSnapshotExact(
            BattleUnitEffectiveTraitSnapshot.Present(
                rawInstances,
                rawIds
            )
        );
        rawInstances.Clear();
        rawEntry.rank = 99;
        rawIds.Clear();
        _test.Eq(
            normalized.Instances[0].TraitId,
            new StringName("zeta_trait"),
            "replace 后既有 immutable instance view 应保持原快照。"
        );
        _test.Eq(
            normalized.TraitIds.Count,
            2,
            "replace 后既有 immutable ids view 应保持原快照。"
        );

        BattleUnitEffectiveTraitSnapshot raw =
            unit.CaptureEffectiveTraitsForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact snapshot 应保留 owner presence。");
        _test.Eq(raw.Instances.Count, 2, "exact seam 应保留 null entry 与顺序。");
        _test.True(raw.Instances[0] == null, "exact seam 应保留 null entry。");
        _test.Eq(raw.Instances[1].rank, 0, "exact restore 应深拷贝 raw scalar。");
        _test.Eq(
            raw.Instances[1].roll_values.Count,
            3,
            "exact seam 应保留 nested null/重复 roll 结构。"
        );
        _test.Eq(raw.TraitIds.Count, 3, "exact seam 应保留 raw ids 重复项。");
        _test.Eq(
            raw.TraitIds[1],
            new StringName(""),
            "exact seam 应保留空 id sentinel。"
        );
        _test.True(
            unit.GetEffectiveTraitsReadViewTyped().Instances[0].IsPresent
                == false,
            "read view 不应把 exact null entry 暴露成 mutable state。"
        );

        raw.Instances[1].trait_id = "mutated_capture";
        raw.TraitIds.Clear();
        BattleUnitEffectiveTraitSnapshot recaptured =
            unit.CaptureEffectiveTraitsForMutationSnapshotExact();
        _test.Eq(
            recaptured.Instances[1].trait_id,
            new StringName("raw_trait"),
            "exact capture 应返回 detached instances。"
        );
        _test.Eq(recaptured.TraitIds.Count, 3, "exact capture 应返回 detached ids。");

        BattleUnitState clone = unit.clone();
        BattleUnitEffectiveTraitReadView cloneView =
            clone.GetEffectiveTraitsReadViewTyped();
        _test.Eq(cloneView.Instances.Count, 1, "gameplay clone 应过滤 raw null entry。");
        _test.Eq(cloneView.TraitIds.Count, 1, "gameplay clone 应丢弃 stale raw ids。");
        _test.Eq(
            cloneView.TraitIds[0],
            new StringName("raw_trait"),
            "gameplay clone 应从 instances 重派生 ids。"
        );

        unit.RestoreEffectiveTraitsForMutationSnapshotExact(
            BattleUnitEffectiveTraitSnapshot.Present(null, null)
        );
        BattleUnitEffectiveTraitReadView presentNull =
            unit.GetEffectiveTraitsReadViewTyped();
        _test.True(presentNull.OwnerPresent, "present-null 应保留 owner。");
        _test.False(presentNull.Instances.IsPresent, "present-null instances 应保持 null。");
        _test.False(presentNull.TraitIds.IsPresent, "present-null ids 应保持 null。");
        BattleUnitEffectiveTraitReadView presentNullClone =
            unit.clone().GetEffectiveTraitsReadViewTyped();
        _test.True(
            presentNullClone.Instances.IsPresent
            && presentNullClone.TraitIds.IsPresent,
            "gameplay clone 应把 present-null 组件归一为默认空集合。"
        );

        unit.RestoreEffectiveTraitsForMutationSnapshotExact(
            BattleUnitEffectiveTraitSnapshot.MissingOwner
        );
        _test.False(
            unit.GetEffectiveTraitsReadViewTyped().OwnerPresent,
            "exact seam 应保留 missing owner。"
        );
        BattleUnitState missingOwnerClone = unit.clone();
        _test.False(
            unit.GetEffectiveTraitsReadViewTyped().OwnerPresent,
            "gameplay clone 不得在 source 上物化 missing owner。"
        );
        _test.True(
            missingOwnerClone.GetEffectiveTraitsReadViewTyped().OwnerPresent,
            "gameplay clone 应把 missing owner 归一为默认 owner。"
        );
        unit.ReplaceEffectiveTraitsTyped(
            new[] { second }
        );
        _test.True(
            unit.GetEffectiveTraitsReadViewTyped().OwnerPresent,
            "正常写入口应重建 missing owner。"
        );
        _test.True(
            unit.HasEffectiveTrait("alpha_trait"),
            "重建 owner 后应恢复派生 trait id。"
        );
    }

    private void TestGeometryOwnerKeepsDerivedStateAndExactSnapshots()
    {
        var unit = new BattleUnitState();
        BattleUnitGeometryReadView defaults =
            unit.GetGeometryReadViewTyped();
        _test.True(defaults.OwnerPresent, "geometry owner 默认应存在。");
        _test.Eq(defaults.AnchorCoord, Vector2I.Zero, "默认 anchor 应为原点。");
        _test.Eq(
            defaults.BodySize,
            BattleUnitState.BodySizeMedium,
            "默认 body size 应保持 medium 对应值。"
        );
        _test.Eq(
            defaults.BodySizeCategory,
            new StringName("medium"),
            "默认 body size category 应为 medium。"
        );
        _test.Eq(defaults.FootprintSize, Vector2I.One, "默认 footprint 应为单格。");
        _test.True(defaults.OccupiedCoords.IsPresent, "默认 occupied coords 应存在。");
        _test.Eq(defaults.OccupiedCoords.Count, 1, "默认 occupied coords 应仅有 anchor。");
        _test.Eq(defaults.OccupiedCoords[0], Vector2I.Zero, "默认 occupied coord 应为原点。");

        unit.SetAnchorCoord(new Vector2I(3, 4));
        _test.True(
            unit.SetBodySizeCategory("large"),
            "正常 geometry 写入口应接受合法 body size category。"
        );
        BattleUnitGeometryReadView large =
            unit.GetGeometryReadViewTyped();
        _test.Eq(
            large.BodySize,
            BattleUnitState.BodySizeLarge,
            "category 写入应同步 body size。"
        );
        _test.Eq(
            large.FootprintSize,
            new Vector2I(2, 2),
            "Large body size 应派生 2x2 footprint。"
        );
        _test.Eq(large.OccupiedCoords.Count, 4, "2x2 footprint 应派生四个 occupied coords。");
        _test.Eq(large.OccupiedCoords[0], new Vector2I(3, 4), "occupied coords 应从 anchor 开始。");
        _test.Eq(large.OccupiedCoords[3], new Vector2I(4, 5), "occupied coords 应覆盖 footprint 末格。");

        _test.False(
            unit.SetBodySizeCategory("invalid"),
            "正常 geometry 写入口应拒绝非法 category。"
        );
        _test.Eq(
            unit.GetBodySizeCategory(),
            new StringName("large"),
            "非法 category 不得部分改写 geometry owner。"
        );
        _test.False(
            unit.SetBodySizeProjection(99),
            "正常 geometry 写入口应拒绝非法 body size。"
        );
        _test.Eq(
            unit.GetBodySize(),
            BattleUnitState.BodySizeLarge,
            "非法 body size 不得部分改写 geometry owner。"
        );

        BattleUnitGeometryReadView detachedReadView = large;
        unit.SetAnchorCoord(new Vector2I(8, 9));
        _test.Eq(
            detachedReadView.AnchorCoord,
            new Vector2I(3, 4),
            "geometry read view 的值快照不得随 owner 后续写入改变。"
        );
        _test.Eq(
            detachedReadView.OccupiedCoords[0],
            new Vector2I(3, 4),
            "geometry read view 不得共享 owner 替换后的 occupied coords。"
        );

        Vector2IList rawOccupied = new()
        {
            new Vector2I(6, 7),
            new Vector2I(9, 9),
        };
        unit.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.Present(
                new Vector2I(6, 7),
                -4,
                "raw_category",
                new Vector2I(2, 1),
                rawOccupied
            )
        );
        rawOccupied.Clear();
        BattleUnitGeometrySnapshot raw =
            unit.CaptureGeometryForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact geometry snapshot 应保留 owner presence。");
        _test.Eq(raw.AnchorCoord, new Vector2I(6, 7), "exact seam 应保留 raw anchor。");
        _test.Eq(raw.BodySize, -4, "exact seam 应保留非法 body size sentinel。");
        _test.Eq(
            raw.BodySizeCategory,
            new StringName("raw_category"),
            "exact seam 应保留非法 category sentinel。"
        );
        _test.Eq(raw.FootprintSize, new Vector2I(2, 1), "exact seam 应保留 raw footprint。");
        _test.Eq(raw.OccupiedCoords.Count, 2, "exact restore 不得共享调用方 occupied list。");
        raw.OccupiedCoords.Clear();
        _test.Eq(
            unit.CaptureGeometryForMutationSnapshotExact().OccupiedCoords.Count,
            2,
            "exact capture 应返回 detached occupied list。"
        );

        unit.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.Present(
                Vector2I.Zero,
                BattleUnitState.BodySizeMedium,
                "medium",
                Vector2I.One,
                null
            )
        );
        BattleUnitGeometryReadView presentNull =
            unit.GetGeometryReadViewTyped();
        _test.True(presentNull.OwnerPresent, "present-null occupied 状态仍应保留 geometry owner。");
        _test.False(
            presentNull.OccupiedCoords.IsPresent,
            "exact seam 应区分 null 与 present-empty occupied coords。"
        );
        _test.True(
            unit.CaptureGeometryForMutationSnapshotExact().OccupiedCoords == null,
            "exact capture 应保留 null occupied coords。"
        );

        unit.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.MissingOwner
        );
        _test.False(
            unit.GetGeometryReadViewTyped().OwnerPresent,
            "exact geometry seam 应保留 missing owner，不能静默重建。"
        );
        _test.False(
            unit.CaptureGeometryForMutationSnapshotExact().OwnerPresent,
            "missing geometry owner 的 exact capture 应继续标记 missing。"
        );

        unit.SetAnchorCoord(new Vector2I(1, 2));
        BattleUnitGeometryReadView rematerialized =
            unit.GetGeometryReadViewTyped();
        _test.True(rematerialized.OwnerPresent, "正常 owner 写入口应重建 missing geometry owner。");
        _test.Eq(rematerialized.AnchorCoord, new Vector2I(1, 2), "重建后应写入新 anchor。");
        _test.Eq(
            rematerialized.BodySizeCategory,
            new StringName("medium"),
            "重建后的 geometry owner 应恢复 canonical 默认体型。"
        );
    }

    private void TestCombatResourceUnlockOwnerNormalizesAndDetaches()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetUnlockedCombatResourceIds(
            new GStringNameArray
            {
                "aura",
                "",
                "rage",
                "aura",
                "mp",
            }
        );

        var readView = unit.GetCombatResourceUnlocksReadViewTyped();
        _test.True(readView.OwnerPresent, "combat-resource unlock read view 应标记 owner 存在。");
        _test.Eq(readView.ResourceIds.Count, 4, "正常写入口应过滤空值、非法值和重复值。");
        _test.Eq(readView.ResourceIds[0], new StringName("aura"), "正常写入口应保留 aura 首次出现顺序。");
        _test.Eq(readView.ResourceIds[1], new StringName("mp"), "正常写入口应保留 mp 首次出现顺序。");
        _test.Eq(readView.ResourceIds[2], new StringName("hp"), "缺失的默认 hp 应在有效输入之后补入。");
        _test.Eq(readView.ResourceIds[3], new StringName("stamina"), "缺失的默认 stamina 应最后补入。");
        _test.False(unit.HasCombatResourceUnlocked("rage"), "非法 combat resource 不得进入正常 owner 状态。");

        var detachedRaw = unit.CaptureCombatResourceUnlocksForMutationSnapshotExact();
        detachedRaw.ResourceIds.Clear();
        detachedRaw.ResourceIds.Add("mp");
        detachedRaw.ResourceIds.Add("mp");
        detachedRaw.ResourceIds.Add("rage");
        _test.Eq(readView.ResourceIds.Count, 4, "exact raw snapshot 修改不得泄漏到 owner 的只读 view。");
        _test.Eq(readView.ResourceIds[0], new StringName("aura"), "只读 view 不得共享 detached raw 集合。");

        unit.RestoreCombatResourceUnlocksForMutationSnapshotExact(detachedRaw);
        detachedRaw.ResourceIds.Clear();
        var restoredView = unit.GetCombatResourceUnlocksReadViewTyped();
        _test.Eq(restoredView.ResourceIds.Count, 3, "exact restore 应保留原始集合形态。");
        _test.Eq(restoredView.ResourceIds[0], new StringName("mp"), "exact restore 应保留原始首项。");
        _test.Eq(restoredView.ResourceIds[1], new StringName("mp"), "exact restore 不应去重诊断 sentinel。");
        _test.Eq(restoredView.ResourceIds[2], new StringName("rage"), "exact restore 不应过滤非法诊断 sentinel。");

        unit.RestoreCombatResourceUnlocksForMutationSnapshotExact(
            BattleUnitCombatResourceUnlockSnapshot.Present(
                new StringNameList { "" }
            )
        );
        _test.True(
            unit.GetCombatResourceUnlocksReadViewTyped().ResourceIds.Contains(""),
            "exact restore 应保留空值诊断 sentinel。"
        );
        _test.False(
            ((BattleUnitReadView)unit).HasCombatResourceUnlocked(""),
            "BattleUnitReadView 应继续拒绝空 combat resource id。"
        );
    }

    private void TestSkillCostApiOwnsResourceMutation()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetCombatResources(30, 12, 20, 4, 3, 2);

        var costs = new SkillCostTransaction
        {
            SkillId = "meteor",
            ApCost = 2,
            MpCost = 5,
            StaminaCost = 7,
            AuraCost = 3,
            CooldownTurns = 15,
        };
        unit.SpendSkillCosts(costs);

        _test.Eq(unit.GetCurrentAp(), 1, "SpendSkillCosts 应扣 AP。");
        _test.Eq(unit.GetCurrentMp(), 7, "SpendSkillCosts 应扣 MP。");
        _test.Eq(unit.GetCurrentStamina(), 13, "SpendSkillCosts 应扣 stamina。");
        _test.Eq(unit.GetCurrentAura(), 1, "SpendSkillCosts 应扣 aura。");
        _test.Eq(unit.GetCooldownTyped("meteor", -1), 15, "SpendSkillCosts 应写入 cooldown。");

        unit.RefundSkillCosts(costs, new BattleResourceCaps(30, 10, 18, 2, 3, 2));
        _test.Eq(unit.GetCurrentMp(), 10, "RefundSkillCosts 应按 cap 返还 MP。");
        _test.Eq(unit.GetCurrentStamina(), 18, "RefundSkillCosts 应按 cap 返还 stamina。");
        _test.Eq(unit.GetCurrentAura(), 2, "RefundSkillCosts 应按 cap 返还 aura。");
    }

    private void TestKnownSkillApiFiltersInternalCollection()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetKnownActiveSkillIds(
            new[]
            {
                new StringName("slash"),
                new StringName(""),
                new StringName("slash"),
                new StringName("guard"),
            }
        );

        _test.True(unit.KnowsActiveSkill("slash"), "KnowsActiveSkill 应识别已知主动技能。");
        _test.True(unit.KnowsActiveSkill("guard"), "SetKnownActiveSkillIds 应保留有效技能。");
        _test.Eq(unit.GetKnownActiveSkillIdsTyped().Count, 2, "SetKnownActiveSkillIds 应过滤空值和重复值。");
        _test.True(
            unit.TryGetFirstKnownActiveSkillIdTyped(out StringName firstSkillId),
            "known-skill owner 应提供首项技能读取。"
        );
        _test.Eq(firstSkillId, new StringName("slash"), "首项技能顺序不得改变。");

        unit.SetKnownSkillLevelTyped("slash", 3);
        _test.Eq(unit.GetKnownSkillLevelTyped("slash", -1), 3, "SetKnownSkillLevelTyped 应写入等级。");
        unit.SetKnownSkillLevelTyped("slash", 0);
        _test.False(unit.HasKnownSkillLevelTyped("slash"), "0 级应移除技能等级。");
        unit.SetKnownSkillLevelTyped("guard", 0, preserveZero: true);
        _test.True(unit.HasKnownSkillLevelTyped("guard"), "preserveZero 应保留显式 0 级。");
        _test.Eq(unit.GetKnownSkillLevelTyped("guard", -1), 0, "显式 0 级应与 missing 区分。");

        unit.SetKnownSkillLockHitBonusTyped("guard", 2);
        _test.Eq(
            unit.GetKnownSkillLockHitBonusTyped("guard", -1),
            2,
            "known-skill owner 应保存 lock-hit bonus。"
        );
        unit.SetKnownSkillLockHitBonusTyped("guard", 0);
        _test.Eq(
            unit.GetKnownSkillLockHitBonusTyped("guard", -1),
            -1,
            "typed 0 lock-hit bonus 应删除条目。"
        );

        List<StringName> detachedIds = unit.GetKnownActiveSkillIdsTyped();
        Dictionary<StringName, int> detachedLevels =
            unit.GetKnownSkillLevelsTyped();
        detachedIds.Add("snapshot_only");
        detachedLevels["guard"] = 9;
        _test.False(
            unit.KnowsActiveSkill("snapshot_only"),
            "known active skill snapshot 不应共享 owner list。"
        );
        _test.Eq(
            unit.GetKnownSkillLevelTyped("guard", -1),
            0,
            "known skill level snapshot 不应共享 owner map。"
        );
    }

    private void TestCooldownOwnerKeepsMapAndAnchorLifecycle()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetCooldownsTyped(
            new Dictionary<StringName, int>
            {
                ["slash"] = 30,
                ["expired"] = 0,
                ["invalid"] = -5,
                [""] = 10,
            }
        );

        _test.Eq(unit.GetCooldownTyped("slash", -1), 30, "cooldown owner 应保留正值。");
        _test.Eq(unit.GetCooldownTyped("expired", -1), -1, "bulk 写入应过滤 0 值。");
        _test.Eq(unit.GetCooldownTyped("invalid", -1), -1, "bulk 写入应过滤负值。");
        _test.Eq(unit.GetCooldownAnchorTuTyped(), -1, "cooldown anchor 默认应为未初始化。");

        Dictionary<StringName, int> detached = unit.GetCooldownsTyped();
        detached[new StringName("slash")] = 99;
        _test.Eq(unit.GetCooldownTyped("slash", -1), 30, "cooldown 读取不应暴露 owner map。");

        unit.SetCooldownAnchorTuTyped(10);
        BattleUnitCooldownAdvanceResult advance =
            unit.AdvanceCooldownClockToTyped(25, 5);
        _test.Eq(advance.ElapsedTu, 15, "cooldown owner 应按 anchor 返回流逝 TU。");
        _test.Eq(unit.GetCooldownAnchorTuTyped(), 25, "消费流逝 TU 后应推进 anchor。");
        _test.True(advance.AnchorChanged, "正 TU 推进应标记 anchor 变化。");
        _test.True(advance.CooldownMapChanged, "正 TU 推进应修改 cooldown。");
        _test.False(advance.InvalidGranularity, "5 TU 对齐推进不应被拒绝。");
        _test.Eq(unit.GetCooldownTyped("slash", -1), 15, "cooldown 应扣除流逝 TU。");

        unit.AdvanceCooldownAnchorForStasisTyped(10, 40);
        _test.Eq(unit.GetCooldownAnchorTuTyped(), 35, "静滞只应推进 cooldown anchor。");
        _test.Eq(unit.GetCooldownTyped("slash", -1), 15, "静滞不应扣除 cooldown。");

        BattleUnitCooldownAdvanceResult invalidAdvance =
            unit.AdvanceCooldownClockToTyped(42, 5);
        _test.True(invalidAdvance.InvalidGranularity, "非 5 TU delta 应被标记为非法。");
        _test.Eq(unit.GetCooldownAnchorTuTyped(), 42, "非法 delta 仍应先重基准 anchor。");
        _test.Eq(unit.GetCooldownTyped("slash", -1), 15, "非法 delta 不应扣除 cooldown。");

        unit.SetCooldownTyped("slash", 0);
        _test.Eq(unit.GetCooldownTyped("slash", -1), -1, "单项 0 值应删除 cooldown。");
    }

    private void TestActionClockOwnsProgressAndRemainder()
    {
        BattleUnitState unit = BuildUnit();
        BattleUnitActionClockSnapshot initial = unit.GetActionClockStateTyped();
        _test.Eq(initial.ActionProgress, 0, "action clock 默认进度应为 0。");
        _test.Eq(
            initial.ActionThreshold,
            BattleUnitState.DefaultActionThreshold,
            "action clock 默认阈值应保持正式常量。"
        );
        _test.Eq(
            initial.ActionProgressRateRemainder,
            0,
            "action clock 默认速率余数应为 0。"
        );

        int firstGain = unit.ConsumeActionProgressRateGainTyped(5, 50);
        _test.Eq(firstGain, 2, "50% 速率的首个 5 TU 应产生 2 进度。");
        _test.Eq(
            unit.GetActionProgressRateRemainderTyped(),
            50,
            "不能整除的 action progress 应留在 owner 余数中。"
        );
        _test.Eq(
            unit.ConsumeActionProgressRateGainTyped(5, 0),
            0,
            "零速率不应产生 action progress。"
        );
        _test.Eq(
            unit.GetActionProgressRateRemainderTyped(),
            50,
            "零速率不应消费已有余数。"
        );
        _test.Eq(
            unit.ConsumeActionProgressRateGainTyped(5, 50),
            3,
            "下一次 50% 进度应消费上次余数。"
        );
        _test.Eq(
            unit.GetActionProgressRateRemainderTyped(),
            0,
            "整除后 action progress 余数应归零。"
        );

        unit.SetActionProgressTyped(250);
        _test.True(
            unit.AdvanceActionClockTyped(0, 100),
            "已有进度跨阈值时，即使本 tick gain 为 0 也应进入 ready。"
        );
        _test.Eq(
            unit.GetActionProgressTyped(),
            50,
            "action clock 应扣除所有已跨越的阈值。"
        );

        unit.RestoreActionClockForMutationSnapshotExact(
            BattleUnitActionClockSnapshot.Present(-4, 7, -25)
        );
        BattleUnitActionClockSnapshot raw =
            unit.CaptureActionClockForMutationSnapshotExact();
        _test.Eq(raw.ActionProgress, -4, "exact seam 应保留负进度 sentinel。");
        _test.Eq(raw.ActionThreshold, 7, "exact seam 不应归一非法阈值。");
        _test.Eq(
            raw.ActionProgressRateRemainder,
            -25,
            "exact seam 应保留负余数 sentinel。"
        );
        _test.Eq(
            unit.ConsumeActionProgressRateGainTyped(5, 50),
            2,
            "正速率消费时，负余数应按既有语义从 0 起算。"
        );
        _test.Eq(
            unit.GetActionProgressRateRemainderTyped(),
            50,
            "负余数消费后的 carry 应保持既有取模语义。"
        );
    }

    private void TestTurnStateKeepsActivationLifecycle()
    {
        BattleUnitState unit = BuildUnit();
        BattleUnitTurnSnapshot initial = unit.GetTurnStateTyped();
        _test.False(initial.HasTakenActionThisTurn, "turn owner 默认不应标记已行动。");
        _test.False(initial.HasMovedThisTurn, "turn owner 默认不应标记已移动。");
        _test.False(
            initial.CanUseLockedMovePointsThisTurn,
            "turn owner 默认不应获得锁定后的移动点权限。"
        );
        _test.False(initial.CastingExhausted, "turn owner 默认不应标记施法耗尽。");
        _test.False(
            unit.IsNormalMovementLockedThisTurnTyped(),
            "未行动且未移动时，普通移动不应锁定。"
        );

        unit.MarkActionTakenThisTurnTyped();
        unit.MarkMovedThisTurnTyped();
        unit.GrantLockedMovePointsThisTurnTyped();
        unit.MarkTurnCastingExhaustedTyped();

        BattleUnitTurnSnapshot active = unit.GetTurnStateTyped();
        _test.True(active.HasTakenActionThisTurn, "owner 应记录本次激活已行动。");
        _test.True(active.HasMovedThisTurn, "owner 应记录本次激活已移动。");
        _test.True(
            active.CanUseLockedMovePointsThisTurn,
            "owner 应记录本次激活的锁定移动点授权。"
        );
        _test.True(active.CastingExhausted, "owner 应记录本次激活施法耗尽。");
        _test.True(
            unit.IsNormalMovementLockedThisTurnTyped(),
            "已行动或已移动后，普通移动应锁定。"
        );

        unit.ClearCastingTurnFlags();
        BattleUnitTurnSnapshot turnEnd = unit.GetTurnStateTyped();
        _test.True(turnEnd.HasTakenActionThisTurn, "激活结束不应提前清除已行动标记。");
        _test.True(turnEnd.HasMovedThisTurn, "激活结束不应提前清除已移动标记。");
        _test.True(
            turnEnd.CanUseLockedMovePointsThisTurn,
            "激活结束不应提前清除锁定移动点授权。"
        );
        _test.False(turnEnd.CastingExhausted, "激活结束只应清除施法耗尽标记。");

        unit.MarkTurnCastingExhaustedTyped();
        unit.ResetTurnStateForTurnStartTyped();
        BattleUnitTurnSnapshot nextTurn = unit.GetTurnStateTyped();
        _test.False(nextTurn.HasTakenActionThisTurn, "新激活应清除已行动标记。");
        _test.False(nextTurn.HasMovedThisTurn, "新激活应清除已移动标记。");
        _test.False(
            nextTurn.CanUseLockedMovePointsThisTurn,
            "新激活应清除锁定移动点授权。"
        );
        _test.False(nextTurn.CastingExhausted, "新激活应清除施法耗尽标记。");
        _test.False(
            unit.IsNormalMovementLockedThisTurnTyped(),
            "新激活重置后，普通移动应重新解锁。"
        );
    }

    private void TestStatusApiCapturesAndClearsOwnedDictionary()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "burning",
                source_unit_id = "caster",
                power = 2,
                stacks = 1,
            }
        );

        var captured = unit.CaptureStatusEffectsTyped();
        _test.Eq(captured.Count, 1, "CaptureStatusEffectsTyped 应返回有效状态。");
        _test.True(captured.ContainsKey("burning"), "CaptureStatusEffectsTyped 应按 status_id 建索引。");

        captured[new StringName("burning")].power = 99;
        _test.Eq(unit.GetStatusEffect("burning")?.power ?? -1, 2, "CaptureStatusEffectsTyped 不应共享状态引用。");

        unit.ClearStatusEffects();
        _test.False(unit.HasStatusEffect("burning"), "ClearStatusEffects 应清空 owner 状态字典。");
    }

    private void TestShieldOwnerApiKeepsAtomicState()
    {
        BattleUnitState unit = BuildUnit();
        unit.ReplaceShieldStateTyped(
            12,
            10,
            30,
            "ward",
            "caster",
            "ward_skill"
        );

        BattleUnitShieldSnapshot active = unit.GetShieldStateTyped();
        _test.True(unit.HasShield(), "有效 shield owner 状态应被识别。");
        _test.Eq(active.CurrentHp, 10, "shield current 应 clamp 到 max。");
        _test.Eq(active.MaxHp, 10, "shield max 应保持有效值。");
        _test.Eq(active.Duration, 30, "shield duration 应保持有效值。");
        _test.Eq(active.Family, new StringName("ward"), "shield family 应保持。");
        _test.Eq(
            ((BattleUnitReadView)unit).CurrentShieldHp,
            10,
            "BattleUnitReadView 应读取 owner 的 raw current shield。"
        );

        unit.RestoreShieldForMutationSnapshotExact(
            new BattleUnitShieldSnapshot(
                -3,
                10,
                30,
                default,
                "raw_source",
                "raw_skill"
            )
        );
        BattleUnitShieldSnapshot raw = unit.CaptureShieldForMutationSnapshotExact();
        _test.Eq(raw.CurrentHp, -3, "exact seam 不应提前 Normalize 负数 sentinel。");
        _test.True(raw.Family == null, "exact seam 应保留 null StringName sentinel。");

        unit.NormalizeShieldState();
        BattleUnitShieldSnapshot cleared = unit.GetShieldStateTyped();
        _test.False(unit.HasShield(), "无效 shield 状态 Normalize 后应清空。");
        _test.Eq(cleared.CurrentHp, 0, "Clear 应归零 current。");
        _test.Eq(cleared.MaxHp, 0, "Clear 应归零 max。");
        _test.Eq(cleared.Duration, -1, "Clear 应恢复 canonical duration=-1。");
        _test.Eq(cleared.Family, new StringName(""), "Clear 应清空 family。");
        _test.Eq(cleared.SourceUnitId, new StringName(""), "Clear 应清空 source unit。");
        _test.Eq(cleared.SourceSkillId, new StringName(""), "Clear 应清空 source skill。");
    }

    private void TestEquipmentAbilityProjectionOwnerKeepsAtomicState()
    {
        BattleUnitState unit = BuildUnit();
        BattleUnitEquipmentAbilityProjectionReadView defaults =
            unit.GetEquipmentAbilityProjectionReadViewTyped();
        _test.True(defaults.OwnerPresent, "equipment ability projection owner 默认应存在。");
        _test.True(defaults.Sources.IsPresent, "默认 equipment ability sources 应存在。");
        _test.True(
            defaults.TemporalProgressModifiers.IsPresent,
            "默认 temporal progress modifiers 应存在。"
        );
        _test.Eq(defaults.Sources.Count, 0, "默认 equipment ability sources 应为空。");
        _test.Eq(
            defaults.TemporalProgressModifiers.Count,
            0,
            "默认 temporal progress modifiers 应为空。"
        );

        BattleEquipmentAbilitySourceState inputSource = BuildEquipmentAbilitySource(
            "source_initial",
            "ability.initial"
        );
        var inputSources = new List<BattleEquipmentAbilitySourceState>
        {
            inputSource,
        };
        var inputModifiers = new List<BattleTemporalProgressModifierState>
        {
            BuildTemporalProgressModifier(
                "zeta_action",
                appliesToActionProgress: true,
                appliesToCastProgress: false,
                label: "zeta action"
            ),
            BuildTemporalProgressModifier(
                "alpha_action",
                appliesToActionProgress: true,
                appliesToCastProgress: false,
                label: "first alpha action",
                saveDc: 11
            ),
            BuildTemporalProgressModifier(
                "alpha_action",
                appliesToActionProgress: true,
                appliesToCastProgress: false,
                label: "second alpha action",
                saveDc: 12
            ),
            BuildTemporalProgressModifier(
                "zeta_cast",
                appliesToActionProgress: false,
                appliesToCastProgress: true,
                label: "zeta cast"
            ),
            BuildTemporalProgressModifier(
                "alpha_cast",
                appliesToActionProgress: false,
                appliesToCastProgress: true,
                label: "first alpha cast",
                saveDc: 13
            ),
            BuildTemporalProgressModifier(
                "alpha_cast",
                appliesToActionProgress: false,
                appliesToCastProgress: true,
                label: "second alpha cast",
                saveDc: 14
            ),
        };
        unit.ReplaceEquipmentAbilityProjectionTyped(inputSources, inputModifiers);
        inputSource.AbilityIds[0] = "ability.mutated_input";
        inputSources.Clear();
        inputModifiers.Clear();

        BattleUnitEquipmentAbilityProjectionReadView projected =
            unit.GetEquipmentAbilityProjectionReadViewTyped();
        _test.Eq(projected.Sources.Count, 1, "共同 Replace 应安装 source 组件。");
        _test.Eq(
            projected.TemporalProgressModifiers.Count,
            6,
            "共同 Replace 应同时安装 temporal modifier 组件。"
        );
        _test.Eq(
            projected.Sources[0].AbilityIds[0],
            new StringName("ability.initial"),
            "共同 Replace 应防御性复制 source nested ability ids。"
        );
        BattleTemporalProgressModifierReadView selectedAction =
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: true);
        _test.Eq(
            selectedAction?.ModifierId ?? "",
            new StringName("alpha_action"),
            "action modifier 应按 modifier id ordinal-min 选择。"
        );
        _test.Eq(
            selectedAction?.SaveDc ?? -1,
            11,
            "同 modifier id 时 action modifier 应保留输入首项。"
        );
        BattleTemporalProgressModifierReadView selectedCast =
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: false);
        _test.Eq(
            selectedCast?.ModifierId ?? "",
            new StringName("alpha_cast"),
            "cast modifier 应按 modifier id ordinal-min 选择。"
        );
        _test.Eq(
            selectedCast?.SaveDc ?? -1,
            13,
            "同 modifier id 时 cast modifier 应保留输入首项。"
        );

        unit.ReplaceEquipmentAbilityProjectionTyped(
            new[]
            {
                BuildEquipmentAbilitySource(
                    "source_replaced",
                    "ability.replaced"
                ),
            },
            new[]
            {
                BuildTemporalProgressModifier(
                    "replacement_cast",
                    appliesToActionProgress: false,
                    appliesToCastProgress: true,
                    label: "replacement cast"
                ),
            }
        );
        BattleUnitEquipmentAbilityProjectionReadView replaced =
            unit.GetEquipmentAbilityProjectionReadViewTyped();
        _test.Eq(
            replaced.Sources[0].EffectiveInstanceKey,
            new StringName("source_replaced"),
            "Replace 应原子替换旧 source 组件。"
        );
        _test.Eq(
            replaced.TemporalProgressModifiers.Count,
            1,
            "Replace 应原子替换旧 temporal modifier 组件。"
        );
        _test.True(
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: true) == null,
            "Replace 后 action modifier cache 不得残留旧选择。"
        );
        _test.Eq(
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: false)
                ?.ModifierId ?? "",
            new StringName("replacement_cast"),
            "Replace 后 cast modifier cache 应同步新组件。"
        );

        bool replaceThrew = false;
        try
        {
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    BuildEquipmentAbilitySource(
                        "source_partial_candidate",
                        "ability.partial_candidate"
                    ),
                },
                new ThrowingEnumerable<BattleTemporalProgressModifierState>()
            );
        }
        catch (InvalidOperationException)
        {
            replaceThrew = true;
        }
        _test.True(replaceThrew, "会抛异常的 modifier enumerable 应让 Replace 失败。");
        BattleUnitEquipmentAbilityProjectionReadView afterFailedReplace =
            unit.GetEquipmentAbilityProjectionReadViewTyped();
        _test.Eq(
            afterFailedReplace.Sources[0].EffectiveInstanceKey,
            new StringName("source_replaced"),
            "Replace 失败时不得提交已枚举完成的 source 候选。"
        );
        _test.Eq(
            afterFailedReplace.TemporalProgressModifiers[0].ModifierId,
            new StringName("replacement_cast"),
            "Replace 失败时不得改变旧 temporal modifier。"
        );
        _test.True(
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: true) == null,
            "Replace 失败时 action modifier cache 应保持旧值。"
        );
        _test.Eq(
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: false)
                ?.ModifierId ?? "",
            new StringName("replacement_cast"),
            "Replace 失败时 cast modifier cache 应保持旧值。"
        );

        unit.ClearEquipmentAbilityProjectionTyped();
        BattleUnitEquipmentAbilityProjectionReadView cleared =
            unit.GetEquipmentAbilityProjectionReadViewTyped();
        _test.True(cleared.OwnerPresent, "Clear 应保留 equipment ability projection owner。");
        _test.Eq(cleared.Sources.Count, 0, "Clear 应清空 sources。");
        _test.Eq(
            cleared.TemporalProgressModifiers.Count,
            0,
            "Clear 应同时清空 temporal modifiers。"
        );
        _test.True(
            unit.GetSelectedTemporalProgressModifierTyped(actionProgress: true) == null
            && unit.GetSelectedTemporalProgressModifierTyped(actionProgress: false) == null,
            "Clear 应同时清空 action/cast modifier cache。"
        );
    }

    private void TestWeaponProjectionOwnerKeepsAtomicState()
    {
        BattleUnitState unit = BuildUnit();
        WeaponDice oneHandedDice = new()
        {
            dice_count = 1,
            dice_sides = 8,
            flat_bonus = 2,
        };
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "owner_test_blade",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 2,
                weapon_one_handed_dice = oneHandedDice,
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 10,
                    flat_bonus = 3,
                },
                weapon_is_versatile = true,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
        oneHandedDice.dice_sides = 100;

        BattleUnitWeaponProjectionReadView readView =
            unit.GetWeaponProjectionReadViewTyped();
        BattleWeaponProjectionValues values = readView.Values;
        _test.True(readView.OwnerPresent, "weapon projection read view 应标记 owner 存在。");
        _test.Eq(values.ProfileKind, new StringName("equipped"), "owner 应保留 profile kind。");
        _test.Eq(values.ItemId, new StringName("owner_test_blade"), "owner 应保留 item id。");
        _test.Eq(values.ProfileTypeId, new StringName("longsword"), "owner 应保留 profile type。");
        _test.Eq(values.RangeType, new StringName("melee"), "owner 应保留 range type。");
        _test.Eq(values.Family, new StringName("sword"), "owner 应保留 family。");
        _test.Eq(
            values.CurrentGrip,
            new StringName("one_handed"),
            "uses_two_hands=false 时 two_handed grip 应按既有规则回落为 one_handed。"
        );
        _test.Eq(values.AttackRange, 2, "owner 应保留有效 attack range。");
        _test.Eq(values.OneHandedDice.DiceCount, 1, "owner 应保留单手骰数量。");
        _test.Eq(values.OneHandedDice.DiceSides, 8, "owner 不应共享写入方的 WeaponDice。");
        _test.Eq(values.OneHandedDice.FlatBonus, 2, "owner 应保留单手骰加值。");
        _test.Eq(values.TwoHandedDice.DiceSides, 10, "owner 应保留双手骰。");
        _test.True(values.IsVersatile, "owner 应保留 versatile 标记。");
        _test.False(values.UsesTwoHands, "owner 应保留规范化后的 uses-two-hands 标记。");
        _test.Eq(
            values.PhysicalDamageTag,
            new StringName("physical_slash"),
            "owner 应保留物理伤害标签。"
        );

        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_current_grip = "invalid_grip",
                weapon_attack_range = -3,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 0,
                    dice_sides = 6,
                    flat_bonus = 9,
                },
                weapon_two_handed_dice = null,
                weapon_uses_two_hands = true,
            }
        );
        values = unit.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(values.AttackRange, 0, "正常写入口应 clamp 负 attack range。");
        _test.Eq(values.CurrentGrip, new StringName("none"), "零射程应清空 grip。");
        _test.False(values.UsesTwoHands, "零射程应清除 uses-two-hands。");
        _test.True(
            values.OneHandedDice.IsPresent && !values.OneHandedDice.HasUsableDice,
            "正常写入口应把非法单手骰规范化为 present-empty。"
        );
        _test.True(
            values.TwoHandedDice.IsPresent && !values.TwoHandedDice.HasUsableDice,
            "正常写入口应把 null 双手骰规范化为 present-empty。"
        );

        unit.RestoreWeaponProjectionForMutationSnapshotExact(
            BattleUnitWeaponProjectionSnapshot.Present(
                new BattleWeaponProjectionValues(
                    new StringName("equipped"),
                    default,
                    new StringName("raw_profile"),
                    default,
                    new StringName("raw_family"),
                    new StringName("two_handed"),
                    -7,
                    BattleWeaponDiceValues.FromRaw(null),
                    BattleWeaponDiceValues.FromRaw(
                        new WeaponDice
                        {
                            dice_count = -1,
                            dice_sides = 0,
                            flat_bonus = -9,
                        }
                    ),
                    true,
                    false,
                    default
                )
            )
        );
        BattleUnitWeaponProjectionSnapshot raw =
            unit.CaptureWeaponProjectionForMutationSnapshotExact();
        _test.True(raw.OwnerPresent, "exact snapshot 应保留 owner presence。");
        _test.Eq(raw.Values.AttackRange, -7, "exact seam 不应 clamp 负 attack range sentinel。");
        _test.False(raw.Values.OneHandedDice.IsPresent, "exact seam 应区分 null dice。");
        _test.True(raw.Values.TwoHandedDice.IsPresent, "exact seam 应保留 present dice。");
        _test.Eq(raw.Values.TwoHandedDice.DiceCount, -1, "exact seam 应保留非法 raw dice。");
        _test.True(raw.Values.ItemId == null, "exact seam 应保留 null StringName sentinel。");

        unit.RestoreWeaponProjectionForMutationSnapshotExact(
            BattleUnitWeaponProjectionSnapshot.MissingOwner
        );
        _test.False(
            unit.GetWeaponProjectionReadViewTyped().OwnerPresent,
            "exact seam 应保留 missing owner，不能静默重建。"
        );
    }

    private static BattleEquipmentAbilitySourceState BuildEquipmentAbilitySource(
        StringName effectiveInstanceKey,
        StringName abilityId
    ) =>
        new()
        {
            EffectiveInstanceKey = effectiveInstanceKey,
            EquipmentDefId = "owner_test_equipment",
            SourceEquipmentInstanceId = "owner_test_instance",
            SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
            AbilityIds = new List<StringName> { abilityId },
        };

    private static BattleTemporalProgressModifierState BuildTemporalProgressModifier(
        StringName modifierId,
        bool appliesToActionProgress,
        bool appliesToCastProgress,
        string label,
        int saveDc = 10
    ) =>
        new()
        {
            ModifierId = modifierId,
            BindingId = "owner_test_binding",
            SourceEquipmentInstanceId = "owner_test_instance",
            AppliesToActionProgress = appliesToActionProgress,
            AppliesToCastProgress = appliesToCastProgress,
            SaveDc = saveDc,
            AttributeModifierId = "intelligence",
            SuccessRatePercent = 200,
            FailureRatePercent = 50,
            Label = label,
        };

    private sealed class ThrowingEnumerable<T> : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator() =>
            throw new InvalidOperationException("owner atomic replace probe");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static BattleUnitState BuildUnit()
    {
        BattleUnitState unit = new()
        {
            unit_id = "unit",
            display_name = "Unit",
            faction_id = "player",
        };
        unit.SetAnchorCoord(new Vector2I(1, 1));
        unit.SetCurrentHp(30);
        return unit;
    }
}
