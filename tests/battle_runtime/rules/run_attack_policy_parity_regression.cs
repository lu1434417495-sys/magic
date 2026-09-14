using System;
using System.Collections.Generic;
using Godot;

public partial class run_attack_policy_parity_regression : LifecycleTestSceneTree
{
    private sealed class ProbeAttackCheckQuery : IBattleEquipmentAttackCheckQuery
    {
        public IReadOnlyList<BattleAttackRollModifierSpec> CollectAttackRollModifierCandidates(
            BattleAttackCheckPolicyContext context
        )
        {
            return new List<BattleAttackRollModifierSpec>
            {
                new()
                {
                    source_domain = "equipment_ability",
                    source_id = "probe_attack_query",
                    source_instance_id = "probe_equipment",
                    label = "probe modifier",
                    modifier_delta = 3,
                    stack_key = "probe_attack_query",
                    stack_mode = "max",
                    target_team_filter = "any",
                    endpoint_mode = "target",
                    footprint_mode = "any_cell",
                    applies_to = "attack_roll",
                },
            };
        }

        public EquipmentAttackDefenseAdjustment CollectAttackDefenseAdjustment(
            BattleAttackCheckPolicyContext context
        )
        {
            var adjustment = new EquipmentAttackDefenseAdjustment();
            adjustment.AddLockDodgeBonus();
            return adjustment;
        }

        public BattleEquipmentAbilityCriticalHitOverrideResult ResolveCriticalHitOverride(
            BattleAttackCheckPolicyContext context
        )
        {
            return new BattleEquipmentAbilityCriticalHitOverrideResult
            {
                ForceCriticalOnHit = true,
                SourceEquipmentInstanceId = "probe_equipment",
                BindingId = "probe_attack_query",
                ActionId = "probe_critical",
            };
        }
    }

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var hitResolver = new BattleHitResolver();
        var policy = new BattleAttackCheckPolicyService();
        policy.Setup(hitResolver, null);
        var battleState = new BattleState();
        var activeUnit = new BattleUnitState
        {
            unit_id = "caster",
        };
        activeUnit.SetAnchorCoord(new Vector2I(1, 1));
        activeUnit.SetKnownSkillLevelTyped("parity_skill", 3);
        var targetUnit = new BattleUnitState
        {
            unit_id = "target",
        };
        targetUnit.SetAnchorCoord(new Vector2I(3, 1));
        targetUnit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 12);
        targetUnit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus), 4);
        SkillDefinition skillDefinition = BuildParitySkill();
        CombatEffectDefinition repeatEffectDefinition = BuildRepeatEffect();
        List<BattleRepeatAttackStageSpec> repeatStageSpecs =
            BattleRepeatAttackResolver.BuildStageSpecsFromRepeatAttackEffect(
                activeUnit,
                skillDefinition,
                repeatEffectDefinition,
                3,
                true
            );
        BattleAttackCheckPolicyContext repeatPreviewContext =
            policy.BuildRepeatAttackStageContext(
                battleState,
                activeUnit,
                targetUnit,
                skillDefinition,
                default,
                "repeat_attack_preview",
                "hud_preview"
            );
        BattleRepeatAttackStageSpec stageSpec =
            BattleRepeatAttackResolver.BuildStageSpecFromRepeatAttackEffect(
                activeUnit,
                skillDefinition,
                repeatEffectDefinition,
                2,
                0,
                true
            );
        BattleAttackCheckPolicyContext stageContext = policy.BuildRepeatAttackStageContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            stageSpec,
            "repeat_attack_stage_check",
            "execute"
        );
        BattleAttackCheckPolicyContext attackContext = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_check",
            "execute",
            false
        );
        BattleAttackCheckPolicyContext previewContext = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_preview",
            "hud_preview",
            false
        );
        AssertKnownSkillAttackCheck(policy.BuildAttackCheck(attackContext, 0, 0));
        AssertKnownSkillAttackPreview(policy.BuildAttackPreview(previewContext));
        AssertKnownRepeatAttackPreview(
            policy.BuildRepeatAttackPreview(repeatPreviewContext, repeatStageSpecs)
        );
        AssertKnownThirdRepeatStageCheck(
            policy.BuildFateAwareRepeatAttackStageHitCheck(stageContext)
        );
        AssertNarrowEquipmentQueryInjection(
            hitResolver,
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition
        );
        AssertAttackContextQueryDoesNotRepairFootprint(
            policy,
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition
        );

        RequestTestExit(_test.Finish("Attack policy parity regression"));
    }

    private void AssertKnownSkillAttackCheck(AttackCheckInput result)
    {
        _test.False(result.Invalid, "已提供目标 AC 的技能命中检定应有效。");
        _test.Eq(result.TargetArmorClass, 12, "目标 AC 应来自已知 fixture。");
        _test.Eq(result.SkillAttackBonus, -2, "技能 -2 命中修正应进入正式检定。");
        _test.Eq(result.RequiredRoll, 14, "AC 12 与技能 -2 修正应要求 d20=14。");
        _test.Eq(result.SuccessRatePercent, 35, "required roll 14 应对应 35% 命中率。");
    }

    private void AssertKnownSkillAttackPreview(AttackPreviewData preview)
    {
        _test.Eq(preview?.StageCount ?? 0, 1, "普通技能预览应只有一个命中阶段。");
        _test.Eq(preview?.SuccessRatePercent ?? -1, 35, "普通技能预览应公开 35% 命中率。");
        _test.Eq(
            preview?.Stages[0].RequiredRoll ?? -1,
            14,
            "普通技能预览阶段应公开 required roll 14。"
        );
    }

    private void AssertKnownRepeatAttackPreview(AttackPreviewData preview)
    {
        _test.Eq(preview?.StageCount ?? 0, 3, "连击预览 fixture 应生成三个阶段。");
        _test.Eq(preview?.BaseAttackBonus ?? -1, 1, "连击基础命中加值应为 +1。");
        _test.Eq(preview?.FollowUpAttackPenalty ?? -1, 2, "后续阶段基础惩罚应为 2。");
        _test.Eq(preview?.SuccessRatePercent ?? -1, 30, "40/30/20 三阶段平均命中率应为 30%。");
        if (preview == null || preview.StageCount != 3)
        {
            return;
        }
        int[] expectedRequiredRolls = { 13, 15, 17 };
        int[] expectedSuccessRates = { 40, 30, 20 };
        for (int index = 0; index < expectedRequiredRolls.Length; index++)
        {
            _test.Eq(
                preview.Stages[index].RequiredRoll,
                expectedRequiredRolls[index],
                $"连击阶段 {index} 应应用等级 3 的首段免罚与每段 +2 惩罚。"
            );
            _test.Eq(
                preview.Stages[index].SuccessRatePercent,
                expectedSuccessRates[index],
                $"连击阶段 {index} 应公开对应的业务命中率。"
            );
        }
    }

    private void AssertKnownThirdRepeatStageCheck(AttackCheckInput result)
    {
        _test.False(result.Invalid, "第三段连击检定应有效。");
        _test.Eq(result.RequiredRoll, 17, "第三段应应用 +1 基础加值和 4 点后续惩罚。");
        _test.Eq(result.SuccessRatePercent, 20, "required roll 17 应对应 20% 命中率。");
    }

    private void AssertAttackContextQueryDoesNotRepairFootprint(
        BattleAttackCheckPolicyService policy,
        BattleState battleState,
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        targetUnit.RestoreGeometryForMutationSnapshotExact(
            BattleUnitGeometrySnapshot.Present(
                targetUnit.GetAnchorCoord(),
                BattleUnitState.BodySizeMedium,
                new StringName("medium"),
                new Vector2I(2, 2),
                new Vector2IList { new Vector2I(9, 9) }
            )
        );
        BattleUnitGeometrySnapshot geometryBefore =
            targetUnit.CaptureGeometryForMutationSnapshotExact();
        long revisionBefore = battleState.MovementGeometryRevision;

        policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_preview",
            "query_no_write_probe",
            false
        );

        BattleUnitGeometrySnapshot geometryAfter =
            targetUnit.CaptureGeometryForMutationSnapshotExact();
        _test.Eq(
            geometryAfter.FootprintSize,
            geometryBefore.FootprintSize,
            "attack context query must not repair a stale footprint projection."
        );
        _test.Eq(
            geometryAfter.OccupiedCoords.Count,
            geometryBefore.OccupiedCoords.Count,
            "attack context query must not replace a stale occupied-coord projection."
        );
        _test.Eq(
            geometryAfter.OccupiedCoords[0],
            geometryBefore.OccupiedCoords[0],
            "attack context query must preserve the stale occupied-coord sentinel."
        );
        _test.Eq(
            targetUnit.GetFootprintSize(),
            new Vector2I(2, 2),
            "attack context query must not repair a stale footprint_size."
        );
        _test.Eq(
            battleState.MovementGeometryRevision,
            revisionBefore,
            "attack context query must not advance movement geometry revision."
        );
    }

    private void AssertNarrowEquipmentQueryInjection(
        BattleHitResolver hitResolver,
        BattleState battleState,
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        var query = new ProbeAttackCheckQuery();
        var policy = new BattleAttackCheckPolicyService();
        policy.Setup(hitResolver, null, query);
        BattleAttackCheckPolicyContext context = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_check",
            "probe",
            false
        );
        AttackCheckInput result = policy.BuildAttackCheck(context, 0, 0);

        _test.Eq(result.TargetArmorClass, 8, "injected defense adjustment should remove the target's +4 dodge component.");
        _test.Eq(result.SituationalAttackBonus, 3, "injected equipment modifier should reach the canonical attack check.");
        _test.True(result.ForceCriticalOnHit, "injected equipment critical override should reach the canonical attack check.");
        _test.Eq(
            result.ForcedCriticalSourceEquipmentInstanceId,
            new StringName("probe_equipment"),
            "critical override provenance should remain typed."
        );
        _test.True(
            ReferenceEquals(context.battle_state, battleState),
            "attack policy should preserve the explicitly supplied battle state."
        );

        BattleAttackCheckPolicyContext noStateContext = policy.BuildSkillDefinitionAttackContext(
            null,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_preview",
            "probe",
            false
        );
        _test.True(
            noStateContext.battle_state == null,
            "attack policy must not recover hidden battle state from a runtime owner."
        );
        policy.Dispose();
    }

    private static SkillDefinition BuildParitySkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "parity_skill",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "parity_skill",
                attackRollBonus: -2
            )
        );
    }

    private static CombatEffectDefinition BuildRepeatEffect()
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "repeat_attack_until_fail",
            payload: new RepeatAttackUntilFailEffectPayloadDefinition(
                baseAttackBonus: 1,
                followUpAttackPenalty: 2,
                penaltyFreeStagesByLevel: new Dictionary<int, int>
                {
                    [3] = 1,
                }
            )
        );
    }
}
