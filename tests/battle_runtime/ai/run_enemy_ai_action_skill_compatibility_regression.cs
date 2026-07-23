using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_enemy_ai_action_skill_compatibility_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestUnitAndGroundTargetModes();
        TestCastableSkillContract();
        TestSpecializedSelectionModes();
        TestExecutionRouteAndActionCapacity();
        TestChargeAndRepositionOptions();
        TestRangeReferenceSkillsRemainUnconstrained();

        RequestTestExit(_test.Finish("Enemy AI action skill compatibility regression"));
    }

    private void TestUnitAndGroundTargetModes()
    {
        SkillDefinition unitSkill = BuildSkill("unit_skill", targetMode: "unit");
        SkillDefinition groundSkill = BuildSkill(
            "ground_skill",
            targetMode: "ground",
            targetSelectionMode: "single_coord"
        );

        UseUnitSkillAction unitAction = OwnAction(
            new UseUnitSkillAction
            {
                action_id = "unit_action",
            }
        );
        unitAction.skill_ids.Add(groundSkill.SkillId);
        AssertInvalid(
            unitAction,
            groundSkill,
            "expected target_mode unit",
            "unit action 应拒绝 ground skill"
        );

        UseGroundSkillAction groundAction = OwnAction(
            new UseGroundSkillAction
            {
                action_id = "ground_action",
            }
        );
        groundAction.skill_ids.Add(unitSkill.SkillId);
        AssertInvalid(
            groundAction,
            unitSkill,
            "expected target_mode ground",
            "ground action 应拒绝 unit skill"
        );

        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(unitSkill.SkillId);
        AssertValid(unitAction, unitSkill, "unit action 应接受 unit skill");

        groundAction.skill_ids.Clear();
        groundAction.skill_ids.Add(groundSkill.SkillId);
        AssertValid(groundAction, groundSkill, "ground action 应接受非冲锋 ground skill");
    }

    private void TestCastableSkillContract()
    {
        SkillDefinition passiveSkill = TestSkillDefinitionProjection.BuildSkill(
            "passive_skill",
            skillType: "passive",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "passive_skill",
                targetMode: "unit"
            )
        );
        SkillDefinition profilelessActiveSkill = TestSkillDefinitionProjection.BuildSkill(
            "profileless_active_skill"
        );
        UseUnitSkillAction action = OwnAction(
            new UseUnitSkillAction
            {
                action_id = "castable_contract_action",
            }
        );
        action.skill_ids.Add(passiveSkill.SkillId);
        AssertInvalid(
            action,
            passiveSkill,
            "expected an active skill with a combat profile",
            "执行型 action 应拒绝 passive skill"
        );

        action.skill_ids.Clear();
        action.skill_ids.Add(profilelessActiveSkill.SkillId);
        AssertInvalid(
            action,
            profilelessActiveSkill,
            "expected an active skill with a combat profile",
            "执行型 action 应拒绝没有 combat profile 的 active skill"
        );

        SkillDefinition unitProfileWithGroundOnlyVariant = BuildSkill(
            "unit_profile_ground_variant",
            targetMode: "unit",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "ground_only",
                    0,
                    Array.Empty<CombatEffectDefinition>(),
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        action.skill_ids.Clear();
        action.skill_ids.Add(unitProfileWithGroundOnlyVariant.SkillId);
        AssertInvalid(
            action,
            unitProfileWithGroundOnlyVariant,
            "expected at least one unit-target cast option",
            "unit action 应拒绝只有 ground variant 的 unit profile"
        );
    }

    private void TestSpecializedSelectionModes()
    {
        SkillDefinition groundMultiUnitSkill = BuildSkill(
            "ground_multi_unit",
            targetMode: "ground",
            targetSelectionMode: "multi_unit",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "multi",
                    0,
                    Array.Empty<CombatEffectDefinition>(),
                    targetMode: "ground",
                    footprintPattern: "unordered",
                    requiredCoordCount: 1
                ),
            }
        );
        UseMultiUnitSkillAction multiAction = OwnAction(
            new UseMultiUnitSkillAction
            {
                action_id = "multi_action",
            }
        );
        multiAction.skill_ids.Add(groundMultiUnitSkill.SkillId);
        AssertValid(
            multiAction,
            groundMultiUnitSkill,
            "multi action 应允许正式存在的 ground + multi_unit 组合"
        );

        MoveToMultiUnitSkillPositionAction moveToMultiAction = OwnAction(
            new MoveToMultiUnitSkillPositionAction
            {
                action_id = "move_to_multi_action",
            }
        );
        moveToMultiAction.skill_ids.Add(groundMultiUnitSkill.SkillId);
        AssertValid(
            moveToMultiAction,
            groundMultiUnitSkill,
            "move-to-multi action 应复用可正式施放的 multi-unit 目标模式"
        );

        SkillDefinition mismatchedVariantSkill = BuildSkill(
            "mismatched_multi_variant",
            targetMode: "ground",
            targetSelectionMode: "multi_unit",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "unit_variant_on_ground_profile",
                    0,
                    Array.Empty<CombatEffectDefinition>(),
                    targetMode: "unit",
                    footprintPattern: "unordered",
                    requiredCoordCount: 1
                ),
            }
        );
        multiAction.skill_ids.Clear();
        multiAction.skill_ids.Add(mismatchedVariantSkill.SkillId);
        AssertInvalid(
            multiAction,
            mismatchedVariantSkill,
            "matching target_mode ground",
            "multi action 应拒绝会被正式 command route 拒绝的变体 target mode"
        );
        moveToMultiAction.skill_ids.Clear();
        moveToMultiAction.skill_ids.Add(mismatchedVariantSkill.SkillId);
        AssertInvalid(
            moveToMultiAction,
            mismatchedVariantSkill,
            "matching target_mode ground",
            "move-to-multi action 不应为不可正式施放的变体错误站位"
        );

        SkillDefinition unitSingleSkill = BuildSkill(
            "unit_single",
            targetMode: "unit",
            targetSelectionMode: "single_unit"
        );
        multiAction.skill_ids.Clear();
        multiAction.skill_ids.Add(unitSingleSkill.SkillId);
        AssertInvalid(
            multiAction,
            unitSingleSkill,
            "expected target_selection_mode multi_unit",
            "multi action 应拒绝普通单目标技能"
        );

        SkillDefinition randomChainSkill = BuildSkill(
            "random_chain",
            targetMode: "unit",
            targetSelectionMode: "random_chain"
        );
        UseRandomChainSkillAction randomChainAction = OwnAction(
            new UseRandomChainSkillAction
            {
                action_id = "random_chain_action",
            }
        );
        randomChainAction.skill_ids.Add(randomChainSkill.SkillId);
        AssertValid(
            randomChainAction,
            randomChainSkill,
            "random-chain action 应接受 unit + random_chain 技能"
        );

        randomChainAction.skill_ids.Clear();
        randomChainAction.skill_ids.Add(unitSingleSkill.SkillId);
        AssertInvalid(
            randomChainAction,
            unitSingleSkill,
            "expected target_selection_mode random_chain",
            "random-chain action 应拒绝普通单目标技能"
        );
    }

    private void TestExecutionRouteAndActionCapacity()
    {
        SkillDefinition unitRandomChainSkill = BuildSkill(
            "unit_random_chain_for_wrong_action",
            targetMode: "unit",
            targetSelectionMode: "random_chain"
        );
        UseUnitSkillAction unitAction = OwnAction(
            new UseUnitSkillAction
            {
                action_id = "unit_action_random_chain_guard",
            }
        );
        unitAction.skill_ids.Add(unitRandomChainSkill.SkillId);
        AssertInvalid(
            unitAction,
            unitRandomChainSkill,
            "random_chain requires UseRandomChainSkillAction",
            "普通 unit action 不会构造 random-chain 命令，不应接受 random_chain 技能"
        );

        SkillDefinition groundRandomChainSkill = BuildSkill(
            "ground_random_chain_for_wrong_action",
            targetMode: "ground",
            targetSelectionMode: "random_chain"
        );
        UseGroundSkillAction groundAction = OwnAction(
            new UseGroundSkillAction
            {
                action_id = "ground_action_random_chain_guard",
            }
        );
        groundAction.skill_ids.Add(groundRandomChainSkill.SkillId);
        AssertInvalid(
            groundAction,
            groundRandomChainSkill,
            "random_chain requires UseRandomChainSkillAction",
            "ground action 也不应绕过 random-chain 专用命令路径"
        );

        SkillDefinition requiresTwoTargetsSkill = BuildSkill(
            "requires_two_targets",
            targetMode: "unit",
            targetSelectionMode: "multi_unit",
            minTargetCount: 2,
            maxTargetCount: 2
        );
        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(requiresTwoTargetsSkill.SkillId);
        AssertInvalid(
            unitAction,
            requiresTwoTargetsSkill,
            "requiring at most one unit target",
            "单目标 unit action 只会生成一个 target_unit_id，不应接受至少双目标技能"
        );

        UseMultiUnitSkillAction undersizedMultiAction = OwnAction(
            new UseMultiUnitSkillAction
            {
                action_id = "undersized_multi_action",
                candidate_pool_limit = 1,
            }
        );
        undersizedMultiAction.skill_ids.Add(requiresTwoTargetsSkill.SkillId);
        AssertInvalid(
            undersizedMultiAction,
            requiresTwoTargetsSkill,
            "candidate_pool_limit >= required target count 2",
            "multi action 的候选池小于最低目标数时不可能组成合法命令"
        );

        MoveToMultiUnitSkillPositionAction undersizedMoveToMultiAction = OwnAction(
            new MoveToMultiUnitSkillPositionAction
            {
                action_id = "undersized_move_to_multi_action",
                candidate_pool_limit = 1,
            }
        );
        undersizedMoveToMultiAction.skill_ids.Add(requiresTwoTargetsSkill.SkillId);
        AssertInvalid(
            undersizedMoveToMultiAction,
            requiresTwoTargetsSkill,
            "candidate_pool_limit >= required target count 2",
            "move-to-multi 也不能为永远凑不齐最低目标数的技能站位"
        );

        SkillDefinition oneTargetMultiSkill = BuildSkill(
            "one_target_multi",
            targetMode: "unit",
            targetSelectionMode: "multi_unit",
            minTargetCount: 1,
            maxTargetCount: 1
        );
        undersizedMultiAction.skill_ids.Clear();
        undersizedMultiAction.skill_ids.Add(oneTargetMultiSkill.SkillId);
        AssertValid(
            undersizedMultiAction,
            oneTargetMultiSkill,
            "multi_unit 最低目标数为 1 时，candidate_pool_limit=1 应保持合法"
        );

        SkillDefinition unitMeteorSkill = BuildSkill(
            "unit_meteor_profile",
            targetMode: "unit",
            specialResolutionProfileId: "meteor_swarm"
        );
        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(unitMeteorSkill.SkillId);
        AssertInvalid(
            unitAction,
            unitMeteorSkill,
            "meteor_swarm requires UseGroundSkillAction",
            "meteor 专用 preview 只接受地格命令，unit action 不应被放行"
        );

        SkillDefinition groundMeteorSkill = BuildSkill(
            "ground_meteor_profile",
            targetMode: "ground",
            targetSelectionMode: "single_coord",
            specialResolutionProfileId: "meteor_swarm"
        );
        groundAction.skill_ids.Clear();
        groundAction.skill_ids.Add(groundMeteorSkill.SkillId);
        AssertValid(
            groundAction,
            groundMeteorSkill,
            "meteor 专用技能应保留 UseGroundSkillAction 正式路径"
        );

        CombatEffectDefinition terrainEffect =
            TestSkillDefinitionProjection.BuildEffect("terrain");
        CombatEffectDefinition damageEffect =
            TestSkillDefinitionProjection.BuildEffect("damage");
        SkillDefinition unsupportedMergedUnitSkill = BuildSkill(
            "unsupported_merged_unit_effects",
            targetMode: "unit",
            effects: new[] { terrainEffect },
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "unit_damage_variant",
                    0,
                    new[] { damageEffect },
                    targetMode: "unit",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(unsupportedMergedUnitSkill.SkillId);
        AssertValid(
            unitAction,
            unsupportedMergedUnitSkill,
            "brain 层只验证不随敌人等级变化的路由，effect gate 应留给模板实际等级"
        );
        AssertInvalidAtLevel(
            unitAction,
            unsupportedMergedUnitSkill,
            1,
            "supported by the unit execution pipeline at skill level 1",
            "模板等级校验应按 base + variant 合并效果拒绝单位执行器不支持的地形效果"
        );

        SkillDefinition highLevelVariantSkill = BuildSkill(
            "high_level_unit_variant",
            targetMode: "unit",
            maxLevel: 5,
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "level_five_unit",
                    5,
                    new[] { damageEffect },
                    targetMode: "unit",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(highLevelVariantSkill.SkillId);
        AssertValid(
            unitAction,
            highLevelVariantSkill,
            "brain 层应允许由高等级模板使用的合法 unit variant"
        );
        AssertInvalidAtLevel(
            unitAction,
            highLevelVariantSkill,
            1,
            "at skill level 1",
            "1 级模板不应绑定仅在 5 级解锁的 unit variant"
        );
        AssertValidAtLevel(
            unitAction,
            highLevelVariantSkill,
            5,
            "5 级模板应能使用已解锁的 unit variant"
        );

        SkillDefinition highLevelGroundVariantSkill = BuildSkill(
            "high_level_ground_variant",
            targetMode: "ground",
            targetSelectionMode: "single_coord",
            maxLevel: 5,
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "level_five_ground",
                    5,
                    new[] { damageEffect },
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        groundAction.skill_ids.Clear();
        groundAction.skill_ids.Add(highLevelGroundVariantSkill.SkillId);
        AssertValid(
            groundAction,
            highLevelGroundVariantSkill,
            "brain 层应允许由高等级模板使用的合法 ground variant"
        );
        AssertInvalidAtLevel(
            groundAction,
            highLevelGroundVariantSkill,
            1,
            "at skill level 1",
            "1 级模板不应绑定仅在 5 级解锁的 ground variant"
        );
        AssertValidAtLevel(
            groundAction,
            highLevelGroundVariantSkill,
            5,
            "5 级模板应能使用已解锁的 ground variant"
        );

        CombatEffectDefinition lowLevelTerrain =
            TestSkillDefinitionProjection.BuildEffect(
                "terrain",
                minSkillLevel: 0,
                maxSkillLevel: 4
            );
        CombatEffectDefinition highLevelRepeat =
            TestSkillDefinitionProjection.BuildEffect(
                "repeat_attack_until_fail",
                minSkillLevel: 5
            );
        SkillDefinition levelSplitUnitSkill = BuildSkill(
            "level_split_unit_effects",
            targetMode: "unit",
            effects: new[] { lowLevelTerrain, highLevelRepeat },
            maxLevel: 5
        );
        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(levelSplitUnitSkill.SkillId);
        AssertInvalidAtLevel(
            unitAction,
            levelSplitUnitSkill,
            1,
            "at skill level 1",
            "高等级 repeat_attack 不应掩盖 1 级只有 terrain 的正式执行失败"
        );
        AssertValidAtLevel(
            unitAction,
            levelSplitUnitSkill,
            5,
            "5 级只剩 repeat_attack 时应与正式 definition gate 一致"
        );

        CombatEffectDefinition sameLevelRepeat =
            TestSkillDefinitionProjection.BuildEffect("repeat_attack_until_fail");
        SkillDefinition repeatWithUnsupportedSurface = BuildSkill(
            "repeat_with_unsupported_surface",
            targetMode: "unit",
            effects: new[] { terrainEffect, sameLevelRepeat }
        );
        unitAction.skill_ids.Clear();
        unitAction.skill_ids.Add(repeatWithUnsupportedSurface.SkillId);
        AssertValidAtLevel(
            unitAction,
            repeatWithUnsupportedSurface,
            1,
            "同级 repeat_attack 与其他 effect 并存时应保持正式 gate 的 repeat 专用路径"
        );
    }

    private void TestChargeAndRepositionOptions()
    {
        CombatEffectDefinition chargeEffect =
            TestSkillDefinitionProjection.BuildEffect("charge");
        CombatEffectDefinition pathEffect =
            TestSkillDefinitionProjection.BuildEffect("path_step_aoe");
        SkillDefinition splitChargePathSkill = BuildSkill(
            "split_charge_path",
            targetMode: "ground",
            targetSelectionMode: "single_coord",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "charge_only",
                    0,
                    new[] { chargeEffect },
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
                TestSkillDefinitionProjection.BuildCastVariant(
                    "path_only",
                    0,
                    new[] { pathEffect },
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );

        UseChargeAction chargeAction = OwnAction(
            new UseChargeAction
            {
                action_id = "charge_action",
                skill_id = splitChargePathSkill.SkillId,
            }
        );
        AssertValid(
            chargeAction,
            splitChargePathSkill,
            "charge action 应接受含 charge option 的技能"
        );

        UseChargePathAoeAction chargePathAction = OwnAction(
            new UseChargePathAoeAction
            {
                action_id = "charge_path_action",
            }
        );
        chargePathAction.skill_ids.Add(splitChargePathSkill.SkillId);
        AssertInvalid(
            chargePathAction,
            splitChargePathSkill,
            "one single-coordinate ground cast option containing both charge and path_step_aoe",
            "charge-path action 不应跨两个 option 拼接效果"
        );

        SkillDefinition combinedChargePathSkill = BuildSkill(
            "combined_charge_path",
            targetMode: "ground",
            targetSelectionMode: "single_coord",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "charge_path",
                    0,
                    new[] { chargeEffect, pathEffect },
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        chargePathAction.skill_ids.Clear();
        chargePathAction.skill_ids.Add(combinedChargePathSkill.SkillId);
        AssertValid(
            chargePathAction,
            combinedChargePathSkill,
            "charge-path action 应接受同一 option 内的 charge + path_step_aoe"
        );

        SkillDefinition multiCoordChargePathSkill = BuildSkill(
            "multi_coord_charge_path",
            targetMode: "ground",
            targetSelectionMode: "coord_pair",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "charge_path_line2",
                    0,
                    new[] { chargeEffect, pathEffect },
                    targetMode: "ground",
                    footprintPattern: "line2",
                    requiredCoordCount: 2
                ),
            }
        );
        chargeAction.skill_id = multiCoordChargePathSkill.SkillId;
        AssertInvalid(
            chargeAction,
            multiCoordChargePathSkill,
            "single-coordinate ground cast option",
            "charge action 只构造一个地格，不应接受双格 charge option"
        );
        chargePathAction.skill_ids.Clear();
        chargePathAction.skill_ids.Add(multiCoordChargePathSkill.SkillId);
        AssertInvalid(
            chargePathAction,
            multiCoordChargePathSkill,
            "single-coordinate ground cast option",
            "charge-path action 只构造一个地格，不应接受双格 option"
        );

        CombatEffectDefinition blinkEffect = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            forcedMoveMode: "blink"
        );
        SkillDefinition blinkSkill = BuildSkill(
            "blink_skill",
            targetMode: "ground",
            targetSelectionMode: "single_coord",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "blink",
                    0,
                    new[] { blinkEffect },
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        UseGroundRepositionSkillAction repositionAction = OwnAction(
            new UseGroundRepositionSkillAction
            {
                action_id = "reposition_action",
            }
        );
        repositionAction.skill_ids.Add(blinkSkill.SkillId);
        AssertValid(
            repositionAction,
            blinkSkill,
            "ground reposition action 应接受 blink forced_move"
        );

        SkillDefinition multiCoordBlinkSkill = BuildSkill(
            "multi_coord_blink",
            targetMode: "ground",
            targetSelectionMode: "coord_pair",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "blink_line2",
                    0,
                    new[] { blinkEffect },
                    targetMode: "ground",
                    footprintPattern: "line2",
                    requiredCoordCount: 2
                ),
            }
        );
        repositionAction.skill_ids.Clear();
        repositionAction.skill_ids.Add(multiCoordBlinkSkill.SkillId);
        AssertInvalid(
            repositionAction,
            multiCoordBlinkSkill,
            "single-coordinate ground cast option",
            "ground reposition action 不应接受自身永远跳过的双格 option"
        );

        CombatEffectDefinition pushEffect = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            forcedMoveMode: "knockback"
        );
        SkillDefinition pushSkill = BuildSkill(
            "push_skill",
            targetMode: "ground",
            targetSelectionMode: "single_coord",
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "push",
                    0,
                    new[] { pushEffect },
                    targetMode: "ground",
                    footprintPattern: "single",
                    requiredCoordCount: 1
                ),
            }
        );
        repositionAction.skill_ids.Clear();
        repositionAction.skill_ids.Add(pushSkill.SkillId);
        AssertInvalid(
            repositionAction,
            pushSkill,
            "blink or jump forced_move",
            "ground reposition action 应拒绝普通 push"
        );
    }

    private void TestRangeReferenceSkillsRemainUnconstrained()
    {
        SkillDefinition unitSkill = BuildSkill("range_unit", targetMode: "unit");
        SkillDefinition groundSkill = BuildSkill(
            "range_ground",
            targetMode: "ground",
            targetSelectionMode: "single_coord"
        );
        MoveToRangeAction moveAction = OwnAction(
            new MoveToRangeAction
            {
                action_id = "move_to_range",
            }
        );
        moveAction.range_skill_ids.Add(unitSkill.SkillId);
        moveAction.range_skill_ids.Add(groundSkill.SkillId);

        AssertValid(
            moveAction,
            new[] { unitSkill, groundSkill },
            "range_skill_ids 只作为站位参考，应允许混合 target mode"
        );
    }

    private void AssertValid(
        EnemyAiAction action,
        SkillDefinition skillDefinition,
        string message
    ) => AssertValid(action, new[] { skillDefinition }, message);

    private void AssertValid(
        EnemyAiAction action,
        IReadOnlyList<SkillDefinition> skillDefinitions,
        string message
    )
    {
        GStringArray errors = Validate(action, skillDefinitions);
        _test.Eq(errors.Count, 0, $"{message}: {FormatErrors(errors)}");
    }

    private void AssertInvalid(
        EnemyAiAction action,
        SkillDefinition skillDefinition,
        string expectedFragment,
        string message
    )
    {
        GStringArray errors = Validate(action, new[] { skillDefinition });
        bool found = false;
        foreach (string error in errors)
        {
            if (error?.Contains(expectedFragment, StringComparison.Ordinal) == true)
            {
                found = true;
                break;
            }
        }
        _test.True(found, $"{message}: {FormatErrors(errors)}");
    }

    private void AssertValidAtLevel(
        EnemyAiAction action,
        SkillDefinition skillDefinition,
        int skillLevel,
        string message
    )
    {
        EnemyAiActionSkillCompatibilityResult result =
            action.EvaluateSkillCompatibility(skillDefinition, skillLevel);
        _test.True(result.IsCompatible, $"{message}: {result.Reason}");
    }

    private void AssertInvalidAtLevel(
        EnemyAiAction action,
        SkillDefinition skillDefinition,
        int skillLevel,
        string expectedFragment,
        string message
    )
    {
        EnemyAiActionSkillCompatibilityResult result =
            action.EvaluateSkillCompatibility(skillDefinition, skillLevel);
        _test.True(
            !result.IsCompatible
                && result.Reason.Contains(expectedFragment, StringComparison.Ordinal),
            $"{message}: {result.Reason}"
        );
    }

    private static GStringArray Validate(
        EnemyAiAction action,
        IReadOnlyList<SkillDefinition> skillDefinitions
    )
    {
        var index = new Dictionary<StringName, SkillDefinition>();
        foreach (SkillDefinition skillDefinition in skillDefinitions)
        {
            index[skillDefinition.SkillId] = skillDefinition;
        }
        return TestResourceOwnership.OwnWrapper(
            action.ValidateSkillReferences(index),
            $"enemy_ai_action_skill_compatibility.errors.{action.action_id}"
        );
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        StringName targetMode,
        StringName targetSelectionMode = default,
        IReadOnlyList<CombatCastVariantDefinition> castVariants = null,
        IReadOnlyList<CombatEffectDefinition> effects = null,
        int minTargetCount = 0,
        int maxTargetCount = 0,
        int maxLevel = 1,
        StringName specialResolutionProfileId = default
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            maxLevel: maxLevel,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: targetMode,
                targetSelectionMode: targetSelectionMode,
                minTargetCount: minTargetCount,
                maxTargetCount: maxTargetCount,
                specialResolutionProfileId: specialResolutionProfileId,
                castVariants: castVariants
            )
        );

    private static TAction OwnAction<TAction>(TAction action)
        where TAction : EnemyAiAction =>
        TestResourceOwnership.Own(
            action,
            $"enemy_ai_action_skill_compatibility.action.{action.action_id}"
        );

    private static string FormatErrors(IEnumerable<string> errors) =>
        string.Join(" | ", errors);
}
