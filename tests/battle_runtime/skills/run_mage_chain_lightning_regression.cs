using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_mage_chain_lightning_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "mage_chain_lightning";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestApprovedLevelCurve(skill);
            TestSingleTargetCastIsLegal(skill);
            TestNormalChainUsesDeterministicNearestTargetWithoutCap(skill);
            TestFiniteTargetLimitCapsCanonicalPreviewExecutionAndAi(skill);
            TestAiCandidateEvaluatorRequestsCanonicalChainPreview(skill);
            TestNewShockDoesNotExtendFrozenRoute(skill);
            TestPreExistingShockExtendsItsOutgoingHop(skill);
            TestTimedTerrainEffectExtendsOutgoingHop(skill);
            TestMultiCellHopUsesStableEdgeCoordsForBarrier(skill);
            TestHighLevelConductiveHopRangeIsCappedAtTwo(skill);
            TestSecondaryBarrierStopsRemainingChain(skill);
            TestSuccessfulSaveHalvesDamageWithoutShock(skill);
            TestProtectedFumbleConsumesExtraMpAndSkipsChain(skill);
            TestUnprotectedFumbleUsesBacklashHopRange(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage chain lightning regression"));
    }

    private void TestApprovedLevelCurve(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "应能加载正式链式闪击等级曲线。");
        if (skill?.CombatProfile == null)
            return;

        CombatSkillResourceCosts level0Costs = skill.CombatProfile.GetEffectiveResourceCostValues(0);
        CombatSkillResourceCosts level2Costs = skill.CombatProfile.GetEffectiveResourceCostValues(2);
        CombatSkillResourceCosts level4Costs = skill.CombatProfile.GetEffectiveResourceCostValues(4);
        _test.Eq(level0Costs.MpCost, 180, "0级链式闪击应消耗180法力。");
        _test.Eq(level0Costs.CooldownTu, 160, "0级链式闪击应进入160TU冷却。");
        _test.Eq(level2Costs.MpCost, 160, "2级链式闪击应把法力消耗降至160。");
        _test.Eq(level2Costs.CooldownTu, 160, "2级链式闪击仍应保持160TU冷却。");
        _test.Eq(level4Costs.MpCost, 160, "4级链式闪击应保持160法力消耗。");
        _test.Eq(level4Costs.CooldownTu, 100, "4级链式闪击应把冷却降至100TU。");

        BattleUnitState caster = BuildUnit("save_dc_curve_caster", "player", Vector2I.Zero);
        try
        {
            int[] expectedBonuses = { 0, 1, 1, 1, 1, 1, 2, 2 };
            for (int level = 0; level < expectedBonuses.Length; level++)
            {
                CombatEffectDefinition damageEffect = FindDamageEffect(skill, level);
                _test.True(damageEffect != null, $"{level}级应恰有一个生效的伤害效果。");
                if (damageEffect == null)
                    continue;
                _test.Eq(
                    damageEffect.SaveDcBonus,
                    expectedBonuses[level],
                    $"{level}级豁免DC技能加值应匹配批准曲线。"
                );
                _test.True(damageEffect.SavePartialOnSuccess, $"{level}级成功豁免都必须保留半伤。");
                _test.Eq(
                    BattleSaveResolver.ResolveSaveDc(
                        caster,
                        damageEffect,
                        BattleSaveContext.ForSkill(SkillId)
                    ),
                    13 + expectedBonuses[level],
                    $"{level}级正式豁免DC应包含技能加值。"
                );
            }
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(caster);
        }
    }

    private void TestSingleTargetCastIsLegal(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "应能加载正式链式闪击技能资源。");
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(skill, new Vector2I(7, 5), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("single_chain_caster", "player", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("single_chain_primary", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState backlashOnly = fixture.AddUnit(
            BuildUnit("single_chain_backlash_only", "enemy", new Vector2I(6, 2))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "没有次要目标时仍应允许支付并施放链式闪击。");
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 1, "单目标预览只应包含主目标。");
        _test.Eq(preview?.ChainDamagePreviewTyped?.NormalHops.Count ?? -1, 0, "单目标预览应显示没有后续跳跃。");
        _test.Eq(preview?.ChainDamagePreviewTyped?.BacklashHops.Count ?? -1, 1, "未受保护大失败的潜在路线应体现每跳范围+1。");
        int backlashOnlyHpBefore = backlashOnly.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "单目标链式闪击应完成正式结算。");
        _test.Eq(caster.GetCurrentAp(), 0, "0级链式闪击应消耗2 AP。");
        _test.Eq(caster.GetCurrentMp(), 60, "0级链式闪击应消耗180法力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 160, "0级链式闪击应进入160TU冷却。");
        _test.Eq(backlashOnly.GetCurrentHp(), backlashOnlyHpBefore, "正常路线不得命中仅位于反噬扩展范围内的单位。");
    }

    private void TestNormalChainUsesDeterministicNearestTargetWithoutCap(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "应能加载正式链式闪击技能资源。");
        if (skill?.CombatProfile == null)
        {
            return;
        }

        using Fixture fixture = new(skill, new Vector2I(8, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("chain_lightning_caster", "player", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("chain_lightning_primary", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState secondaryEnemy = fixture.AddUnit(
            BuildUnit("chain_lightning_secondary_enemy", "enemy", new Vector2I(4, 1))
        );
        BattleUnitState secondaryAlly = fixture.AddUnit(
            BuildUnit("chain_lightning_secondary_ally", "player", new Vector2I(5, 1))
        );
        BattleUnitState tertiaryEnemy = fixture.AddUnit(
            BuildUnit("chain_lightning_tertiary_enemy", "enemy", new Vector2I(6, 1))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        BattlePreview allyPrimaryPreview = fixture.Runtime.PreviewCommand(
            BuildCommand(caster, secondaryAlly)
        );
        _test.True(
            allyPrimaryPreview != null && !allyPrimaryPreview.allowed,
            "链式闪击首目标必须是敌人，不能直接锁定友军。"
        );

        int primaryHpBefore = primary.GetCurrentHp();
        int secondaryEnemyHpBefore = secondaryEnemy.GetCurrentHp();
        int secondaryAllyHpBefore = secondaryAlly.GetCurrentHp();
        int tertiaryEnemyHpBefore = tertiaryEnemy.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "敌方首目标在射程内时应允许施放链式闪击。");
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 4, "0级连锁不应按目标数量截断可达路线。");
        _test.Eq(
            preview?.TargetUnitIdsTyped[1] ?? new StringName(""),
            secondaryEnemy.unit_id,
            "同距离候选应按目标锚点Y/X稳定选择次要敌人。"
        );
        AssertCanonicalPreviewAiMetrics(
            fixture,
            skill,
            caster,
            command,
            preview
        );

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "链式闪击应通过正式技能命令完成结算。");
        _test.True(
            batch?.changed_unit_ids.Contains(secondaryEnemy.unit_id) == true,
            "正式技能结算应把受到连锁伤害的次要目标记入 changed_unit_ids。"
        );
        _test.Eq(primaryHpBefore - primary.GetCurrentHp(), 24, "0级首目标应承受一次4D6完整伤害。");
        _test.Eq(
            secondaryEnemyHpBefore - secondaryEnemy.GetCurrentHp(),
            24,
            "基础连锁范围内的每名次要敌人应承受一次相同的4D6伤害。"
        );
        _test.Eq(secondaryAllyHpBefore - secondaryAlly.GetCurrentHp(), 24, "无限连锁应继续命中相邻友军。");
        _test.Eq(
            tertiaryEnemyHpBefore - tertiaryEnemy.GetCurrentHp(),
            24,
            "无限连锁应继续命中路线上的第三个次要目标。"
        );
        _test.True(primary.GetStatusEffect("shocked") != null, "首目标豁免失败后应获得感电。");
        _test.True(
            secondaryEnemy.GetStatusEffect("shocked") != null,
            "范围内次要敌人豁免失败后应获得感电。"
        );
        _test.True(
            secondaryAlly.GetStatusEffect("shocked") != null,
            "进入冻结路线的友军应在豁免失败后获得感电。"
        );
        _test.True(tertiaryEnemy.GetStatusEffect("shocked") != null, "无限路线上的后续敌人应获得感电。");
        _test.Eq(caster.GetCurrentAp(), 0, "链式闪击成功施放后应消耗2 AP。");
        _test.Eq(caster.GetCurrentMp(), 60, "链式闪击成功施放后应消耗180法力。");
    }

    private void AssertCanonicalPreviewAiMetrics(
        Fixture fixture,
        SkillDefinition skill,
        BattleUnitState caster,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using var scoreService = new BattleAiScoreService();
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            skill.CombatProfile.EffectDefinitions
        );

        _test.Eq(
            score?.estimated_chain_target_count ?? -1,
            3,
            "AI 应直接消费 canonical preview 的三个次要跳跃目标。"
        );
        _test.Eq(
            score?.estimated_chain_enemy_target_count ?? -1,
            2,
            "AI 应从 canonical preview 识别两个次要敌方目标。"
        );
        _test.Eq(
            score?.estimated_chain_ally_target_count ?? -1,
            1,
            "AI 应从 canonical preview 识别一个友方误伤目标。"
        );
    }

    private void TestFiniteTargetLimitCapsCanonicalPreviewExecutionAndAi(
        SkillDefinition formalSkill
    )
    {
        CombatEffectDefinition damage = FindDamageEffect(formalSkill, 0);
        _test.True(damage != null, "有限目标上限回归需要正式0级伤害 effect。");
        if (damage == null || formalSkill?.CombatProfile == null)
            return;

        CombatSkillDefinition formalCombat = formalSkill.CombatProfile;
        CombatEffectDefinition limitedChain = TestSkillDefinitionProjection.BuildEffect(
            "chain_damage",
            effectTargetTeamFilter: "any",
            preventRepeatTarget: true,
            chainDamage: new CombatChainDamageDefinition(
                baseHopRange: 1,
                conductiveHopRange: 2,
                maxTotalTargets: 3,
                conductiveStatusIds: new StringName[] { "shocked" },
                conductiveTerrainEffectIds: new StringName[] { "wet" },
                backlashHopRangeBonus: 1
            )
        );
        SkillDefinition limitedSkill = formalSkill.WithCombatProfile(
            TestSkillDefinitionProjection.BuildCombatProfile(
                formalSkill.SkillId,
                effects: new[] { damage, limitedChain },
                targetMode: formalCombat.TargetMode,
                targetTeamFilter: formalCombat.TargetTeamFilter,
                rangeValue: formalCombat.RangeValue,
                apCost: formalCombat.ApCost,
                mpCost: formalCombat.MpCost,
                staminaCost: formalCombat.StaminaCost,
                cooldownTu: formalCombat.CooldownTu,
                attackResolutionMode: formalCombat.AttackResolutionMode,
                rangePattern: formalCombat.RangePattern,
                aiTags: formalCombat.AiTags,
                targetSelectionMode: formalCombat.TargetSelectionMode,
                deliveryCategories: formalCombat.DeliveryCategories,
                projectileKind: formalCombat.ProjectileKind,
                attackDefenseMode: formalCombat.AttackDefenseMode
            )
        );

        using Fixture fixture = new(limitedSkill, new Vector2I(9, 5), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("limited_chain_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("limited_chain_primary", "enemy", new Vector2I(3, 2))
        );
        BattleUnitState firstSecondary = fixture.AddUnit(
            BuildUnit("limited_chain_secondary_1", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState secondSecondary = fixture.AddUnit(
            BuildUnit("limited_chain_secondary_2", "enemy", new Vector2I(5, 2))
        );
        BattleUnitState unselectedThird = fixture.AddUnit(
            BuildUnit("limited_chain_secondary_3", "enemy", new Vector2I(6, 2))
        );
        BattleUnitState unselectedFourth = fixture.AddUnit(
            BuildUnit("limited_chain_secondary_4", "enemy", new Vector2I(7, 2))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "有限上限派生技能应允许命中正式首目标。");
        _test.Eq(
            preview?.TargetUnitIdsTyped.Count ?? -1,
            3,
            "max_total_targets=3 必须把 canonical preview 严格限制为首目标加两个跳跃目标。"
        );
        _test.Eq(
            preview?.ChainDamagePreviewTyped?.NormalHops.Count ?? -1,
            2,
            "有限上限 canonical preview 应只保留两个次要跳跃。"
        );
        _test.Eq(
            preview?.TargetUnitIdsTyped[2] ?? new StringName(""),
            secondSecondary.unit_id,
            "有限路线的第三个总目标应是第二个最近次要单位。"
        );
        AssertFiniteTargetCanonicalPreviewAiMetrics(
            fixture,
            limitedSkill,
            caster,
            command,
            preview
        );

        int primaryHpBefore = primary.GetCurrentHp();
        int firstHpBefore = firstSecondary.GetCurrentHp();
        int secondHpBefore = secondSecondary.GetCurrentHp();
        int thirdHpBefore = unselectedThird.GetCurrentHp();
        int fourthHpBefore = unselectedFourth.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "有限目标上限技能应完成正式结算。");
        _test.Eq(primaryHpBefore - primary.GetCurrentHp(), 24, "首目标应承受正式0级伤害。");
        _test.Eq(firstHpBefore - firstSecondary.GetCurrentHp(), 24, "第一个次要目标应结算伤害。");
        _test.Eq(secondHpBefore - secondSecondary.GetCurrentHp(), 24, "第二个次要目标应结算伤害。");
        _test.Eq(
            unselectedThird.GetCurrentHp(),
            thirdHpBefore,
            "达到总目标上限后，第三个仍可达次要单位不得受影响。"
        );
        _test.Eq(
            unselectedFourth.GetCurrentHp(),
            fourthHpBefore,
            "达到总目标上限后，第四个仍可达次要单位不得受影响。"
        );
    }

    private void AssertFiniteTargetCanonicalPreviewAiMetrics(
        Fixture fixture,
        SkillDefinition skill,
        BattleUnitState caster,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using var scoreService = new BattleAiScoreService();
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            skill.CombatProfile.EffectDefinitions
        );

        _test.Eq(
            score?.preview?.TargetUnitIdsTyped.Count ?? -1,
            3,
            "AI score input 应保留 canonical preview 的严格三个总目标。"
        );
        _test.Eq(score?.estimated_chain_target_count ?? -1, 2, "AI 连锁计数应是两个次要目标。");
        _test.Eq(score?.estimated_chain_enemy_target_count ?? -1, 2, "有限路线的两个次要目标均为敌人。");
        _test.Eq(score?.estimated_chain_ally_target_count ?? -1, 0, "有限路线不得凭空产生友军目标。");
    }

    private void TestAiCandidateEvaluatorRequestsCanonicalChainPreview(
        SkillDefinition skill
    )
    {
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(skill, new Vector2I(8, 5), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("candidate_chain_caster", "enemy", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("candidate_chain_primary", "player", new Vector2I(4, 2))
        );
        fixture.AddUnit(
            BuildUnit("candidate_chain_secondary", "player", new Vector2I(5, 2))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        int canonicalPreviewCallCount = 0;
        using var scoreService = new BattleAiScoreService();
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
            preview_command_callback = command =>
            {
                canonicalPreviewCallCount += 1;
                return fixture.Runtime.PreviewCommand(command);
            },
            skill_cast_block_reason_callback = (_, _) =>
                BattleSkillCastBlockReasonKind.None,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        context.skill_score_input_callback = (
            scoreContext,
            definition,
            command,
            preview,
            effects,
            metadata,
            candidateFacts
        ) =>
        {
            BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
                scoreContext,
                definition,
                command,
                preview,
                effects,
                metadata,
                candidateFacts
            );
            if (score != null)
                score.total_score = 100;
            return score;
        };
        var action = new UseUnitSkillActionDefinition(
            "candidate_chain_action",
            "test",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            0,
            0,
            false,
            0,
            5,
            EnemyAiDistanceReferences.ToStringName(
                EnemyAiDistanceReference.TargetUnit
            )
        );

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            action,
            context
        );
        _test.True(decision != null, "正式 chain_damage 应生成 AI unit-skill 候选决策。");
        _test.True(
            canonicalPreviewCallCount > 0,
            "BattleAiUnitSkillCandidateEvaluator 遇到 chain_damage 必须请求 canonical preview。"
        );
        _test.True(
            decision?.score_input?.preview?.ChainDamagePreviewTyped != null,
            "AI 最终候选评分必须携带 runtime 生成的 typed chain preview，不能停留在 fast preview。"
        );
        _test.True(
            decision?.score_input?.preview?.ChainDamagePreviewTyped?.NormalHops.Count > 0,
            "AI 最终候选的 canonical preview 应包含真实后续跳跃。"
        );
        _test.Eq(
            decision?.command?.target_unit_id ?? new StringName(""),
            primary.unit_id,
            "nearest_enemy 候选应锁定最近正式首目标。"
        );
    }

    private void TestNewShockDoesNotExtendFrozenRoute(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            return;
        }

        using Fixture fixture = new(skill, new Vector2I(8, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_primary", "enemy", new Vector2I(3, 2))
        );
        BattleUnitState radiusTwoEnemy = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_radius_two_enemy", "enemy", new Vector2I(5, 2))
        );
        BattleUnitState secondHopEnemy = fixture.AddUnit(
            BuildUnit("wet_chain_lightning_second_hop_enemy", "enemy", new Vector2I(7, 2))
        );
        BattleCellState primaryCell = fixture.State.GetCell(primary.GetAnchorCoord());
        _test.True(primaryCell != null, "湿地连锁回归应能取得首目标地格。");
        primaryCell?.terrain_effect_ids.Add("wet");
        PrepareCaster(caster, 1);
        fixture.Activate(caster);

        int radiusTwoEnemyHpBefore = radiusTwoEnemy.GetCurrentHp();
        int secondHopEnemyHpBefore = secondHopEnemy.GetCurrentHp();
        BattlePreview preview = fixture.Runtime.PreviewCommand(BuildCommand(caster, primary));
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 2, "冻结路线应只包含湿地主目标与第一个2格跳跃目标。");
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(BuildCommand(caster, primary));

        _test.True(batch != null, "湿地上的首目标应允许完成链式闪击结算。");
        _test.Eq(
            radiusTwoEnemyHpBefore - radiusTwoEnemy.GetCurrentHp(),
            24,
            "首目标位于湿地时，距离2格的敌人应承受一次完整连锁伤害。"
        );
        _test.Eq(
            secondHopEnemy.GetCurrentHp(),
            secondHopEnemyHpBefore,
            "第一个次要目标由本次施法新获得的感电不得延长同一次冻结路线。"
        );
    }

    private void TestPreExistingShockExtendsItsOutgoingHop(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(skill, new Vector2I(8, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("pre_shock_chain_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("pre_shock_chain_primary", "enemy", new Vector2I(3, 2))
        );
        BattleUnitState conductiveSecondary = fixture.AddUnit(
            BuildUnit("pre_shock_chain_secondary", "enemy", new Vector2I(5, 2))
        );
        BattleUnitState finalEnemy = fixture.AddUnit(
            BuildUnit("pre_shock_chain_final", "enemy", new Vector2I(7, 2))
        );
        fixture.State.GetCell(primary.GetAnchorCoord())?.terrain_effect_ids.Add("wet");
        conductiveSecondary.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "shocked",
                source_unit_id = caster.unit_id,
                duration = 40,
                power = 1,
            }
        );
        PrepareCaster(caster, 1);
        fixture.Activate(caster);

        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 3, "施法前已感电的中继单位应使用2格导电跳距。");
        int finalHpBefore = finalEnemy.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "预先感电的多跳路线应完成正式结算。");
        _test.Eq(finalHpBefore - finalEnemy.GetCurrentHp(), 24, "第二个2格跳跃目标应承受相同4D6伤害。");
    }

    private void TestTimedTerrainEffectExtendsOutgoingHop(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(skill, new Vector2I(7, 5), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("timed_wet_chain_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("timed_wet_chain_primary", "enemy", new Vector2I(3, 2))
        );
        BattleUnitState radiusTwoEnemy = fixture.AddUnit(
            BuildUnit("timed_wet_chain_secondary", "enemy", new Vector2I(5, 2))
        );
        BattleCellState primaryCell = fixture.State.GetCell(primary.GetAnchorCoord());
        _test.True(primaryCell != null, "timed terrain 连锁回归应能取得首目标地格。");
        if (primaryCell == null)
            return;
        _test.False(
            primaryCell.terrain_effect_ids.Contains("wet"),
            "timed terrain 用例不得借用静态 wet terrain id。"
        );
        primaryCell.timed_terrain_effects.Add(
            new BattleTerrainEffectState
            {
                field_instance_id = "timed_wet_chain_field",
                effect_id = "wet",
                effect_type = "status",
                source_unit_id = caster.unit_id,
                source_skill_id = "timed_wet_chain_fixture",
                target_team_filter = "any",
                remaining_tu = 100,
                stack_behavior = "refresh",
            }
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 2, "timed wet 应把首跳范围从1扩展到2。");
        _test.True(
            preview?.ChainDamagePreviewTyped?.NormalHops.Count == 1
                && preview.ChainDamagePreviewTyped.NormalHops[0].OriginWasConductive
                && preview.ChainDamagePreviewTyped.NormalHops[0].OutgoingRange == 2,
            "canonical preview 应把 timed wet 识别为导电来源并投影2格跳距。"
        );
        int secondaryHpBefore = radiusTwoEnemy.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "timed wet 路线应完成正式结算。");
        _test.Eq(
            secondaryHpBefore - radiusTwoEnemy.GetCurrentHp(),
            24,
            "仅来自 timed_terrain_effects 的 wet 也应让2格次要目标承受伤害。"
        );
    }

    private void TestMultiCellHopUsesStableEdgeCoordsForBarrier(
        SkillDefinition skill
    )
    {
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(skill, new Vector2I(9, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("multi_cell_chain_caster", "player", new Vector2I(7, 0))
        );
        BattleUnitState primary = BuildUnit(
            "multi_cell_chain_primary",
            "enemy",
            new Vector2I(3, 1)
        );
        BattleUnitState secondary = BuildUnit(
            "multi_cell_chain_secondary",
            "enemy",
            new Vector2I(1, 3)
        );
        _test.True(primary.SetBodySizeCategory("large"), "主目标应设置为2x2 large footprint。");
        _test.True(secondary.SetBodySizeCategory("large"), "次要目标应设置为2x2 large footprint。");
        fixture.AddUnit(primary);
        fixture.AddUnit(secondary);
        primary.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "shocked",
                source_unit_id = caster.unit_id,
                duration = 40,
                power = 1,
            }
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        Vector2I expectedOriginCoord = new(3, 2);
        Vector2I expectedTargetCoord = new(2, 3);
        _test.True(
            BattleGridDistanceService.GetDistance(
                primary.GetAnchorCoord(),
                secondary.GetAnchorCoord()
            ) > 2,
            "multi-cell 用例的 anchor 距离必须超过导电跳距。"
        );
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "multi_cell_edge_barrier",
            ProfileId = "multi_cell_edge_barrier",
            DisplayName = "多格边缘屏障",
            SourceUnitId = "other_unit",
            AnchorCoord = expectedTargetCoord,
            RadiusCells = 0,
            AreaPattern = "diamond",
            RemainingTu = 100,
            CatchAllProjectedEffects = true,
        };
        barrier.SetLayers(
            new[]
            {
                new BattleBarrierLayerState
                {
                    LayerId = "multi_cell_edge_layer",
                    DisplayName = "多格边缘屏障",
                },
            }
        );
        fixture.State.PutLayeredBarrierField(barrier.BarrierInstanceId, barrier);

        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "multi-cell 首目标应保持合法。");
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 1, "edge coord 上的屏障应阻断唯一后续跳跃。");
        _test.True(
            preview?.ChainDamagePreviewTyped?.NormalHops.Count == 1,
            "multi-cell canonical preview 应保留被阻断跳跃的 typed facts。"
        );
        if (preview?.ChainDamagePreviewTyped?.NormalHops.Count == 1)
        {
            BattleChainDamagePreviewHopData hop =
                preview.ChainDamagePreviewTyped.NormalHops[0];
            _test.Eq(hop.OriginCoord, expectedOriginCoord, "应稳定选择主目标 footprint 的最近边缘坐标。");
            _test.Eq(hop.TargetCoord, expectedTargetCoord, "应稳定选择次要目标 footprint 的最近边缘坐标。");
            _test.Eq(hop.Distance, 2, "multi-cell 跳距必须按 footprint 最近坐标对计算。");
            _test.Eq(hop.OutgoingRange, 2, "预存感电应提供2格导电跳距。");
            _test.True(hop.Blocked, "只覆盖 edge target coord 的屏障必须阻断该跳跃。");
        }
        int secondaryHpBefore = secondary.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "multi-cell 屏障路线应完成主目标结算。");
        _test.Eq(
            secondary.GetCurrentHp(),
            secondaryHpBefore,
            "execution 必须沿 canonical edge coord 检查屏障，不能退回 anchor 绕过。"
        );
    }

    private void TestHighLevelConductiveHopRangeIsCappedAtTwo(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;

        using Fixture fixture = new(skill, new Vector2I(9, 5), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("high_level_range_chain_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("high_level_range_chain_primary", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState distanceThreeEnemy = fixture.AddUnit(
            BuildUnit("high_level_range_chain_distance_three", "enemy", new Vector2I(7, 2))
        );
        fixture.State.GetCell(primary.GetAnchorCoord())?.terrain_effect_ids.Add("wet");
        PrepareCaster(caster, 7);
        fixture.Activate(caster);

        int distanceThreeHpBefore = distanceThreeEnemy.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 1, "7级湿地主目标也不得连锁到3格外单位。");
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "7级导电跳距限制不应阻止主目标正常结算。");
        _test.Eq(
            distanceThreeEnemy.GetCurrentHp(),
            distanceThreeHpBefore,
            "7级导电连锁范围应封顶2格。"
        );
    }

    private void TestSecondaryBarrierStopsRemainingChain(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(skill, new Vector2I(8, 6), _test);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("barrier_chain_caster", "player", new Vector2I(0, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("barrier_chain_primary", "enemy", new Vector2I(3, 2))
        );
        BattleUnitState blockedSecondary = fixture.AddUnit(
            BuildUnit("barrier_chain_secondary", "enemy", new Vector2I(5, 2))
        );
        BattleUnitState remainingEnemy = fixture.AddUnit(
            BuildUnit("barrier_chain_remaining", "enemy", new Vector2I(7, 2))
        );
        fixture.State.GetCell(primary.GetAnchorCoord())?.terrain_effect_ids.Add("wet");
        PrepareCaster(caster, 1);
        fixture.Activate(caster);
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "chain_secondary_barrier",
            ProfileId = "chain_secondary_barrier",
            DisplayName = "连锁屏障",
            SourceUnitId = "other_unit",
            AnchorCoord = blockedSecondary.GetAnchorCoord(),
            RadiusCells = 0,
            AreaPattern = "diamond",
            RemainingTu = 100,
            CatchAllProjectedEffects = true,
        };
        barrier.SetLayers(
            new[]
            {
                new BattleBarrierLayerState
                {
                    LayerId = "chain_active_layer",
                    DisplayName = "连锁屏障",
                },
            }
        );
        fixture.State.PutLayeredBarrierField(barrier.BarrierInstanceId, barrier);

        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "次要跳跃被阻挡不应让主目标施法失效。");
        _test.Eq(preview?.TargetUnitIdsTyped.Count ?? -1, 1, "被屏障阻挡的次要目标不得进入实际影响目标列表。");
        _test.True(
            preview?.ChainDamagePreviewTyped?.NormalHops.Count == 1
                && preview.ChainDamagePreviewTyped.NormalHops[0].Blocked,
            "canonical preview 应标记首个被阻断的跳跃并停止后续路线。"
        );
        int blockedHpBefore = blockedSecondary.GetCurrentHp();
        int remainingHpBefore = remainingEnemy.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "主目标命中后应完成命令结算。");
        _test.Eq(blockedSecondary.GetCurrentHp(), blockedHpBefore, "屏障后的次要目标不应受伤。");
        _test.Eq(remainingEnemy.GetCurrentHp(), remainingHpBefore, "任一跳被阻断后必须停止全部剩余连锁。");
    }

    private void TestSuccessfulSaveHalvesDamageWithoutShock(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;
        using Fixture fixture = new(
            skill,
            new Vector2I(7, 5),
            _test,
            new FixedSuccessfulSaveDamageResolver()
        );
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("save_chain_caster", "player", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("save_chain_primary", "enemy", new Vector2I(4, 2))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        int hpBefore = primary.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(BuildCommand(caster, primary));
        _test.True(batch != null, "成功敏捷豁免分支应完成结算。");
        _test.Eq(hpBefore - primary.GetCurrentHp(), 12, "敏捷豁免成功应把4D6伤害减半。");
        _test.True(primary.GetStatusEffect("shocked") == null, "敏捷豁免成功不得施加感电。");
    }

    private void TestProtectedFumbleConsumesExtraMpAndSkipsChain(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;

        using Fixture fixture = new(skill, new Vector2I(7, 5), _test, spellControlRoll: 1);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("protected_fumble_chain_caster", "player", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("protected_fumble_chain_primary", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState secondary = fixture.AddUnit(
            BuildUnit("protected_fumble_chain_secondary", "enemy", new Vector2I(5, 2))
        );
        PrepareCaster(caster, 3);
        fixture.Activate(caster);

        int primaryHpBefore = primary.GetCurrentHp();
        int secondaryHpBefore = secondary.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(BuildCommand(caster, primary));
        _test.True(batch != null, "受保护大失败应完成资源结算。");
        _test.Eq(primary.GetCurrentHp(), primaryHpBefore, "受保护大失败不应伤害首目标。");
        _test.Eq(secondary.GetCurrentHp(), secondaryHpBefore, "受保护大失败不应释放后续连锁。");
        _test.Eq(caster.GetCurrentAp(), 0, "受保护大失败仍应消耗2 AP。");
        _test.Eq(caster.GetCurrentMp(), 0, "3级受保护大失败应消耗160基础法力与受剩余法力限制的80额外法力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 160, "受保护大失败仍应启动160TU冷却。");
        _test.Eq(caster.GetFumbleProtectionUsedTyped(SkillId), 1, "受保护大失败应消耗本场一次技能保护。");
    }

    private void TestUnprotectedFumbleUsesBacklashHopRange(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
            return;

        using Fixture fixture = new(skill, new Vector2I(8, 5), _test, spellControlRoll: 1);
        BattleUnitState caster = fixture.AddUnit(
            BuildUnit("backlash_chain_caster", "player", new Vector2I(1, 2))
        );
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("backlash_chain_primary", "enemy", new Vector2I(4, 2))
        );
        BattleUnitState backlashOnly = fixture.AddUnit(
            BuildUnit("backlash_chain_secondary", "enemy", new Vector2I(6, 2))
        );
        PrepareCaster(caster);
        fixture.Activate(caster);

        int backlashHpBefore = backlashOnly.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(BuildCommand(caster, primary));
        _test.True(batch != null, "未受保护大失败应继续完成反噬结算。");
        _test.Eq(
            backlashHpBefore - backlashOnly.GetCurrentHp(),
            24,
            "未受保护大失败应把基础跳距从1扩展到2并命中对应次要目标。"
        );
        _test.Eq(caster.GetFumbleProtectionUsedTyped(SkillId), 0, "0级无保护次数，不应写入保护消耗。");
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "mage_chain_lightning",
            "mage_chain_lightning_regression"
        );

    private static CombatEffectDefinition FindDamageEffect(SkillDefinition skill, int level)
    {
        foreach (CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions ?? System.Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == BattleEffectKind.Damage && effect.IsUnlockedAtSkillLevel(level))
                return effect;
        }
        return null;
    }

    private static void PrepareCaster(BattleUnitState caster, int level = 0)
    {
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        caster.SetCurrentAp(2);
        caster.SetCurrentMp(240);
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target?.unit_id ?? new StringName(""),
            target_coord = target?.GetAnchorCoord() ?? new Vector2I(-1, -1),
        };
        if (target != null)
        {
            command.AddTargetUnitId(target.unit_id);
        }
        return command;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "mage_chain_lightning_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y += 1)
        {
            for (int x = 0; x < mapSize.X; x += 1)
            {
                Vector2I coord = new(x, y);
                BattleCellState cell = new()
                {
                    coord = coord,
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 200,
            mp: 240,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 200);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 240);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("intelligence", 16);
        unit.attribute_snapshot.SetValue("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.SetValue("faith_luck_bonus", 0);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<BattleUnitState> _units = new();
        private readonly TestHarness _test;

        internal Fixture(
            SkillDefinition skill,
            Vector2I mapSize,
            TestHarness test,
            BattleDamageResolver damageResolver = null,
            int spellControlRoll = 10
        )
        {
            _test = test;
            Runtime = new BattleRuntimeModule();
            Runtime.setup(
                null,
                new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
            );
            Runtime.ConfigureDamageResolverForTests(
                damageResolver
                    ?? new FixedFailedSaveDamageResolver(new GArray(), new GArray { 10 })
            );
            Runtime.ConfigureHitResolverForTests(new FixedHitResolver(spellControlRoll));
            State = BuildState(mapSize);
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }

        internal BattleUnitState AddUnit(BattleUnitState unit)
        {
            State.SetUnit(unit);
            if (unit.faction_id == new StringName("player"))
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            _test.True(
                Runtime._grid_service.PlaceUnit(State, unit, unit.GetAnchorCoord(), true),
                $"测试单位应能放入棋盘：{unit.unit_id}"
            );
            _units.Add(unit);
            return unit;
        }

        internal void Activate(BattleUnitState caster)
        {
            State.active_unit_id = caster.unit_id;
            Runtime.SetupStateForTests(State);
        }

        public void Dispose()
        {
            Runtime?.Dispose();
            foreach (BattleUnitState unit in _units)
            {
                BattleTestFixture.DisposeBattleUnit(unit);
            }
            BattleTestFixture.DisposeBattleState(State);
        }
    }

    private sealed partial class FixedSuccessfulSaveDamageResolver : FixedRollDamageResolver
    {
        internal override AttackEffectResolutionResult ResolveEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            DamageResolutionContext damageContext
        )
        {
            return base.ResolveEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                (damageContext ?? DamageResolutionContext.Empty()).WithSaveRollOverrides(
                    new[] { 20 }
                )
            );
        }
    }
}
