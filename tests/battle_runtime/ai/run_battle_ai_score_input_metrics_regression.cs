using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_score_input_metrics_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGroundSkillEffectiveTargetsExcludeFriendlyFire();
            TestEmptyGroundControlCellsStaySeparateFromUnitTargets();
            TestGroundSkillScoreInputExposesMetrics();
            TestRepeatAttackScoreUsesStageSuccessRate();
            TestChainSkillScoresFriendlyBounceRisk();
            TestDamageScoreUsesFormalResistanceAndShieldRules();
            TestWillPenetrateShieldBonusCondition();
            TestMultiHitDamageScoreConsumesPreviewShieldSequentially();
            TestLayeredBarrierProjectionTracksLayersAndLifetime();
            TestLayeredBarrierProjectionRequiresNearbyBoundaryThreat();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI score input metrics regression"));
    }

    private void TestGroundSkillEffectiveTargetsExcludeFriendlyFire()
    {
        using Fixture fixture = BuildFixture("score_input_ground_effective_targets", new Vector2I(8, 6));
        SkillDefinition skill = BuildSkill(
            "friendly_fire_fireball_probe",
            "Friendly Fire Fireball Probe",
            BuildDamageEffect(10, "any")
        );
        fixture.AddSkill(skill);

        BattleUnitState caster = BuildUnit("friendly_fire_fireball_mage", "hostile", new Vector2I(1, 2));
        BattleUnitState target = BuildUnit("friendly_fire_target", "player", new Vector2I(5, 2));
        BattleUnitState ally = BuildUnit("friendly_fire_ally", "hostile", new Vector2I(5, 3));
        fixture.AddUnit(caster);
        fixture.AddUnit(target);
        fixture.AddUnit(ally);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, target.GetAnchorCoord()),
            BuildPreview(target, ally),
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(null, 4, 5)
        );

        _test.True(score != null, "友伤火球评分应可生成。");
        if (score == null)
        {
            return;
        }
        _test.Eq(score.enemy_target_count, 1, "minimum_hit_count 应只把敌方有效目标计入收益。");
        _test.Eq(score.effective_target_count, 1, "友军被火球覆盖不能贡献有效命中数。");
        _test.True(score.estimated_friendly_fire_target_count >= 1, "评分应识别火球友伤目标。");
    }

    private void TestEmptyGroundControlCellsStaySeparateFromUnitTargets()
    {
        using Fixture fixture = BuildFixture("score_input_empty_ground_control", new Vector2I(6, 5));
        SkillDefinition skill = BuildSkill(
            "ai_empty_ground_control_score_probe",
            "Empty Ground Control Probe",
            BuildTerrainEffect("mist_pool")
        );
        fixture.AddSkill(skill);

        BattleUnitState caster = BuildUnit("empty_ground_control_scorer", "hostile", new Vector2I(1, 2));
        fixture.AddUnit(caster);
        Vector2I targetCoord = new(3, 2);
        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, targetCoord),
            BuildGroundPreview(targetCoord),
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(null, 0, 5)
        );

        _test.True(score != null, "空地控场评分应可生成。");
        if (score == null)
        {
            return;
        }
        _test.Eq(score.target_count, 0, "空地控场不应把地格计入 target_count。");
        _test.Eq(score.enemy_target_count, 0, "空地控场不应伪造敌方目标。");
        _test.Eq(score.effective_target_count, 0, "空地控场不应伪造有效命中数。");
        _test.Eq(score.estimated_ground_control_cell_count, 1, "空地控场应按 preview target_coords 暴露受控地格数。");
        _test.True(score.ground_control_score > 0, "空地控场应产生独立地格控制评分。");
    }

    private void TestGroundSkillScoreInputExposesMetrics()
    {
        using Fixture fixture = BuildFixture("score_input_ground_metrics", new Vector2I(7, 5));
        SkillDefinition skill = BuildSkill(
            "archer_suppressive_fire_probe",
            "Suppressive Fire Probe",
            effects: new[] { BuildDamageEffect(8, "enemy") },
            apCost: 2,
            staminaCost: 2,
            cooldownTu: 15
        );
        fixture.AddSkill(skill);

        BattleUnitState harrier = BuildUnit("mist_harrier_score", "hostile", new Vector2I(1, 2));
        BattleUnitState playerA = BuildUnit("player_a", "player", new Vector2I(4, 2));
        BattleUnitState playerB = BuildUnit("player_b", "player", new Vector2I(5, 2));
        fixture.AddUnit(harrier);
        fixture.AddUnit(playerA);
        fixture.AddUnit(playerB);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(harrier),
            skill,
            BuildCommand(harrier, skill.SkillId, playerA.GetAnchorCoord()),
            BuildPreview(playerA, playerB),
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(null, 0, 6)
        );

        _test.True(score != null, "AI skill score input 应由 BattleAiScoreService 正式构造。");
        if (score == null)
        {
            return;
        }
        _test.True(score.hit_payoff_score > 0, "ground skill score input 应暴露正向命中收益。");
        _test.True(score.target_count >= 2, "ground skill score input 应暴露目标数量。");
        _test.Eq(score.ap_cost, 2, "ground skill score input 应暴露 AP 消耗。");
        _test.Eq(score.stamina_cost, 2, "ground skill score input 应暴露 ST 消耗。");
        _test.Eq(score.cooldown_tu, 15, "ground skill score input 应暴露 cooldown_tu。");
        _test.True(score.resource_cost_score > 0, "ground skill score input 应暴露资源消耗评分。");
        _test.Eq(score.position_objective_kind, new StringName("cast_distance"), "ground skill score input 应记录默认站位目标类型。");
        _test.True(score.distance_to_primary_coord >= 0, "ground skill score input 应记录站位目标距离。");
        _test.True(score.position_objective_score >= 0, "ground skill score input 应暴露站位目标评分。");
    }

    private void TestRepeatAttackScoreUsesStageSuccessRate()
    {
        using Fixture fixture = BuildFixture("score_input_fate_aware_hit_rate", new Vector2I(5, 3));
        SkillDefinition skill = BuildSkill(
            "ai_fate_preview_combo_probe",
            "Fate Preview Combo Probe",
            BuildDamageEffect(10, "enemy")
        );
        fixture.AddSkill(skill);

        BattleUnitState scorer = BuildUnit("fate_score_user", "hostile", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("fate_score_target", "player", new Vector2I(2, 1));
        fixture.AddUnit(scorer);
        fixture.AddUnit(target);
        BattlePreview preview = BuildPreview(target);
        preview.hit_preview = new AttackPreviewData
        {
            Stages = new List<AttackPreviewStage>
            {
                new(10, 15, 10, 19, 19, "15%"),
            },
            HitRatePercent = 10,
            SuccessRatePercent = 15,
            BaseHitRatePercent = 10,
        };

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(scorer),
            skill,
            BuildCommand(scorer, skill.SkillId, target.GetAnchorCoord(), target),
            preview,
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(target, 1, 1)
        );

        _test.True(score != null, "AI fate-aware 命中率回归应构造出合法 score input。");
        if (score == null)
        {
            return;
        }
        _test.Eq(preview.hit_preview.StageBaseHitRates[0], 10, "AI 回归前置：preview 应保留 raw 命中率。");
        _test.Eq(preview.hit_preview.StageSuccessRates[0], 15, "AI 回归前置：preview 应保留正式成功率。");
        _test.Eq(score.estimated_hit_rate_percent, 15, "AI 评分应消费 fate-aware repeat_attack 成功率，而不是 raw hit rate。");
    }

    private void TestChainSkillScoresFriendlyBounceRisk()
    {
        using Fixture fixture = BuildFixture("score_input_chain_friendly_bounce", new Vector2I(8, 6));
        SkillDefinition skill = BuildSkill(
            "mage_chain_lightning_probe",
            "Chain Lightning Probe",
            BuildChainDamageEffect(1),
            BuildDamageEffect(7, "any")
        );
        fixture.AddSkill(skill);

        BattleUnitState mage = BuildUnit("friendly_chain_mage", "hostile", new Vector2I(1, 2));
        BattleUnitState target = BuildUnit("friendly_chain_target", "player", new Vector2I(5, 2));
        BattleUnitState ally = BuildUnit("friendly_chain_ally", "hostile", new Vector2I(5, 3));
        fixture.AddUnit(mage);
        fixture.AddUnit(target);
        fixture.AddUnit(ally);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(mage),
            skill,
            BuildCommand(mage, skill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { skill.CombatProfile.EffectDefinitions[0], skill.CombatProfile.EffectDefinitions[1] },
            BuildPositionMetadata(target, 4, 5)
        );

        _test.True(score != null, "友伤链闪评分应可生成。");
        if (score == null)
        {
            return;
        }
        _test.True(score.estimated_chain_ally_target_count >= 1, "链闪评分应预估会弹射到友军。");
        _test.True(score.estimated_friendly_fire_target_count >= 1, "链闪评分应把友军弹射计为友伤风险。");
    }

    private void TestDamageScoreUsesFormalResistanceAndShieldRules()
    {
        using Fixture fixture = BuildFixture(
            "score_input_formal_damage_mitigation",
            new Vector2I(5, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 20,
            damageTag: "fire"
        );
        SkillDefinition skill = BuildSkill(
            "formal_damage_mitigation_probe",
            "Formal Damage Mitigation Probe",
            effect
        );
        fixture.AddSkill(skill);

        BattleUnitState caster = BuildUnit(
            "formal_damage_mitigation_caster",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState target = BuildUnit(
            "formal_damage_mitigation_target",
            "player",
            new Vector2I(2, 1),
            hp: 10
        );
        target.SetDamageResistanceTyped("fire", "half");
        target.ReplaceShieldStateTyped(
            5,
            5,
            100,
            "formal_ward",
            caster.unit_id,
            skill.SkillId
        );
        BattleUnitShieldSnapshot shieldBefore = target.GetShieldStateTyped();
        fixture.AddUnit(caster);
        fixture.AddUnit(target);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { effect },
            BuildPositionMetadata(target, 1, 1)
        );

        _test.True(score != null, "正式减伤评分应生成 score input。");
        if (score == null)
        {
            return;
        }
        _test.Eq(score.estimated_post_save_damage, 10, "AI 应复用正式 fire half 抗性结果。");
        _test.Eq(score.estimated_shield_absorbed, 5, "AI 应记录正式护盾吸收量。");
        _test.Eq(score.estimated_damage, 5, "AI 应只把穿透护盾的部分计为生命伤害。");
        _test.Eq(score.estimated_lethal_target_count, 0, "护盾后的 5 点生命伤害不应误判为击杀。");
        _test.Eq(target.GetCurrentHp(), 10, "AI 伤害预览不得修改真实目标生命。");
        _test.Eq(
            target.GetShieldStateTyped(),
            shieldBefore,
            "AI 伤害预览不得修改真实目标护盾。"
        );
    }

    private void TestWillPenetrateShieldBonusCondition()
    {
        SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_will_penetrate.tres",
            "battle_ai_score_input_metrics_will_penetrate"
        );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "意志穿透正式技能定义应可加载。");
        if (combat == null)
        {
            return;
        }
        _test.Eq(combat.EffectDefinitions.Count, 3, "意志穿透应保留两档主伤害和一段护盾增伤。");
        if (combat.EffectDefinitions.Count < 3)
        {
            return;
        }

        CombatEffectDefinition shieldBonusEffect = combat.EffectDefinitions[2];
        _test.Eq(
            shieldBonusEffect.BonusConditionKind,
            BattleDamageBonusConditionKind.TargetHasShield,
            "意志穿透护盾增伤应投影为 typed target_has_shield 条件。"
        );

        using (var registry = new SkillContentRegistry(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        ))
        using (var invalidEffect = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "fire",
            power = 1,
            bonus_condition = "unsupported_bonus_condition_probe",
        })
        {
            var errors = new Godot.Collections.Array<string>();
            registry.AppendEffectValidationErrors(
                errors,
                "invalid_bonus_condition_probe",
                invalidEffect,
                "effect_defs[0]"
            );
            string formattedErrors = string.Join(" | ", errors);
            _test.True(
                formattedErrors.Contains(
                    "uses unsupported bonus_condition unsupported_bonus_condition_probe"
                ),
                $"未知 bonus_condition 必须在 schema 边界被拒绝。 errors={formattedErrors}"
            );
        }

        BattleUnitState source = BuildUnit(
            "will_penetrate_runtime_source",
            "hostile",
            new Vector2I(1, 1),
            hp: 1000
        );
        BattleUnitState shieldedTarget = BuildUnit(
            "will_penetrate_runtime_shielded",
            "player",
            new Vector2I(2, 1),
            hp: 1000
        );
        BattleUnitState unshieldedTarget = BuildUnit(
            "will_penetrate_runtime_unshielded",
            "player",
            new Vector2I(3, 1),
            hp: 1000
        );
        shieldedTarget.ReplaceShieldStateTyped(
            1000,
            1000,
            100,
            "will_penetrate_probe",
            source.unit_id,
            skill.SkillId
        );
        try
        {
            using var resolver = new FixedHitMaxDamageResolver();
            AttackEffectResolutionResult shieldedResult = resolver.ResolveEffects(
                source,
                shieldedTarget,
                new[] { shieldBonusEffect },
                DamageResolutionContext.Empty()
            );
            AttackEffectResolutionResult unshieldedResult = resolver.ResolveEffects(
                source,
                unshieldedTarget,
                new[] { shieldBonusEffect },
                DamageResolutionContext.Empty()
            );
            DamageEventResult shieldedEvent = shieldedResult.DamageEvents[0];
            DamageEventResult unshieldedEvent = unshieldedResult.DamageEvents[0];
            _test.True(shieldedEvent.BonusConditionMet, "有有效护盾时应命中 target_has_shield 条件。");
            _test.False(
                unshieldedEvent.BonusConditionMet,
                "无护盾时不得命中 target_has_shield 条件。"
            );
            _test.Eq(shieldedEvent.ResolvedDamage, 11, "有盾目标的 1d8 最大值应按 140% 结算为 11。");
            _test.Eq(unshieldedEvent.ResolvedDamage, 8, "无盾目标的 1d8 最大值应保持基础 8 点。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(shieldedTarget);
            BattleTestFixture.DisposeBattleUnit(unshieldedTarget);
        }

        using Fixture fixture = BuildFixture(
            "score_input_will_penetrate_shield_bonus",
            new Vector2I(5, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        fixture.AddSkill(skill);
        BattleUnitState caster = BuildUnit(
            "will_penetrate_ai_caster",
            "hostile",
            new Vector2I(1, 1),
            hp: 1000
        );
        caster.AddKnownActiveSkill(skill.SkillId);
        caster.SetKnownSkillLevelTyped(skill.SkillId, 5);
        BattleUnitState target = BuildUnit(
            "will_penetrate_ai_target",
            "player",
            new Vector2I(2, 1),
            hp: 1000
        );
        target.ReplaceShieldStateTyped(
            1000,
            1000,
            100,
            "will_penetrate_ai_probe",
            caster.unit_id,
            skill.SkillId
        );
        fixture.AddUnit(caster);
        fixture.AddUnit(target);
        BattleUnitShieldSnapshot shieldBefore = target.GetShieldStateTyped();
        BattleAiScoreInput shieldedScore = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { shieldBonusEffect },
            BuildPositionMetadata(target, 1, 1)
        );
        _test.Eq(
            target.GetShieldStateTyped(),
            shieldBefore,
            "AI 条件估值不得修改真实护盾状态。"
        );
        target.ClearShield();
        BattleAiScoreInput unshieldedScore = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { shieldBonusEffect },
            BuildPositionMetadata(target, 1, 1)
        );
        _test.True(
            shieldedScore != null && unshieldedScore != null,
            "意志穿透有盾/无盾 AI 估值都应生成 score input。"
        );
        if (shieldedScore != null && unshieldedScore != null)
        {
            _test.Eq(shieldedScore.estimated_post_save_damage, 7, "AI 应把有盾 1d8 均值按 140% 估为 7。");
            _test.Eq(unshieldedScore.estimated_post_save_damage, 5, "AI 对无盾 1d8 均值应保持 5。");
        }
    }

    private void TestMultiHitDamageScoreConsumesPreviewShieldSequentially()
    {
        using Fixture fixture = BuildFixture(
            "score_input_sequential_shield_consumption",
            new Vector2I(5, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        CombatEffectDefinition firstHit = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 4,
            damageTag: "force"
        );
        CombatEffectDefinition secondHit = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 4,
            damageTag: "force"
        );
        SkillDefinition skill = BuildSkill(
            "sequential_shield_probe",
            "Sequential Shield Probe",
            firstHit,
            secondHit
        );
        fixture.AddSkill(skill);

        BattleUnitState caster = BuildUnit(
            "sequential_shield_caster",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState target = BuildUnit(
            "sequential_shield_target",
            "player",
            new Vector2I(2, 1),
            hp: 10
        );
        target.ReplaceShieldStateTyped(
            5,
            5,
            100,
            "sequential_ward",
            caster.unit_id,
            skill.SkillId
        );
        BattleUnitShieldSnapshot shieldBefore = target.GetShieldStateTyped();
        fixture.AddUnit(caster);
        fixture.AddUnit(target);

        BattleAiScoreInput score = fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, target.GetAnchorCoord(), target),
            BuildPreview(target),
            new[] { firstHit, secondHit },
            BuildPositionMetadata(target, 1, 1)
        );

        _test.True(score != null, "多段护盾评分应生成 score input。");
        if (score == null)
        {
            return;
        }
        _test.Eq(score.estimated_post_save_damage, 8, "两段正式伤害预算应累计为 8。");
        _test.Eq(score.estimated_shield_absorbed, 5, "两段预览只能消耗现有的 5 点护盾。");
        _test.Eq(score.estimated_damage, 3, "第二段应穿透已被第一段消耗的护盾并造成 3 点生命伤害。");
        _test.Eq(target.GetCurrentHp(), 10, "多段 AI 预览不得修改真实目标生命。");
        _test.Eq(
            target.GetShieldStateTyped(),
            shieldBefore,
            "多段 AI 预览不得修改真实目标护盾。"
        );
    }

    private void TestLayeredBarrierProjectionTracksLayersAndLifetime()
    {
        using Fixture fixture = BuildFixture("score_input_layered_barrier", new Vector2I(8, 5));
        StringName profileId = "ai_layered_barrier_probe";
        SkillDefinition skill = BuildLayeredBarrierSkill("ai_layered_barrier_skill", profileId);
        fixture.AddSkill(skill);
        fixture.AddBarrierProfile(BuildLayeredBarrierProfile(profileId));

        BattleUnitState caster = BuildUnit("barrier_scorer", "hostile", new Vector2I(2, 2));
        BattleUnitState enemy = BuildUnit("barrier_boundary_enemy", "player", new Vector2I(5, 2));
        fixture.AddUnit(caster);
        fixture.AddUnit(enemy);

        BattleAiScoreInput freshScore = ScoreLayeredBarrier(fixture, caster, skill);
        _test.True(freshScore?.layered_barrier_projection != null, "屏障评分应输出 typed 战术投影。");
        if (freshScore?.layered_barrier_projection == null)
            return;
        _test.Eq(freshScore.layered_barrier_projection.utility_control_count, 1, "边界外存在近敌时，新法球应提供控场收益。");
        _test.Eq(freshScore.layered_barrier_projection.reason, "tactical_boundary", "新法球应记录边界战术原因。");
        _test.True(freshScore.hit_payoff_score > 0, "有效法球应贡献正向命中收益。");

        fixture.PutBarrier(BuildLayeredBarrierState(profileId, caster.GetAnchorCoord(), 90, false, false));
        BattleAiScoreInput redundantScore = ScoreLayeredBarrier(fixture, caster, skill);
        _test.True(redundantScore.layered_barrier_projection.redundant_same_anchor, "同锚点完整法球且寿命充足时应判定重复。");
        _test.Eq(redundantScore.layered_barrier_projection.utility_control_count, 0, "重复法球不应再次贡献控场收益。");
        _test.Eq(redundantScore.hit_payoff_score, 0, "重复法球的效果收益应为零。");
        _test.Eq(redundantScore.low_value_penalty_reason, "layered_barrier:redundant_same_anchor", "重复原因应进入评分输入。");

        fixture.PutBarrier(BuildLayeredBarrierState(profileId, caster.GetAnchorCoord(), 90, true, false));
        BattleAiScoreInput brokenLayerScore = ScoreLayeredBarrier(fixture, caster, skill);
        _test.False(brokenLayerScore.layered_barrier_projection.redundant_same_anchor, "已有破层时，重建完整法球不应判定重复。");
        _test.Eq(brokenLayerScore.layered_barrier_projection.strongest_same_anchor_active_layer_count, 1, "投影应读取现存有效层数。");
        _test.Eq(brokenLayerScore.layered_barrier_projection.strongest_same_anchor_broken_layer_count, 1, "投影应读取现存破层数。");
        _test.Eq(brokenLayerScore.layered_barrier_projection.utility_control_count, 1, "破层法球允许重建控场价值。");

        fixture.PutBarrier(BuildLayeredBarrierState(profileId, caster.GetAnchorCoord(), 10, false, false));
        BattleAiScoreInput expiringScore = ScoreLayeredBarrier(fixture, caster, skill);
        _test.Eq(expiringScore.layered_barrier_projection.replacement_threshold_tu, 30, "替换阈值应由投影持续时间稳定导出。");
        _test.False(expiringScore.layered_barrier_projection.redundant_same_anchor, "完整但即将过期的法球允许提前替换。");
        _test.Eq(expiringScore.layered_barrier_projection.utility_control_count, 1, "低剩余 TU 法球应保留续场价值。");
    }

    private void TestLayeredBarrierProjectionRequiresNearbyBoundaryThreat()
    {
        using Fixture fixture = BuildFixture("score_input_layered_barrier_no_threat", new Vector2I(10, 5));
        StringName profileId = "ai_layered_barrier_no_threat_probe";
        SkillDefinition skill = BuildLayeredBarrierSkill("ai_layered_barrier_no_threat_skill", profileId);
        fixture.AddSkill(skill);
        fixture.AddBarrierProfile(BuildLayeredBarrierProfile(profileId));

        BattleUnitState caster = BuildUnit("barrier_idle_scorer", "hostile", new Vector2I(1, 2));
        BattleUnitState distantEnemy = BuildUnit("barrier_distant_enemy", "player", new Vector2I(8, 2));
        fixture.AddUnit(caster);
        fixture.AddUnit(distantEnemy);

        BattleAiScoreInput score = ScoreLayeredBarrier(fixture, caster, skill);
        _test.True(score?.layered_barrier_projection != null, "无近敌局面仍应输出可解释投影。");
        if (score?.layered_barrier_projection == null)
            return;
        _test.Eq(score.layered_barrier_projection.nearby_outside_enemy_count, 0, "远敌不应伪装成法球边界威胁。");
        _test.Eq(score.layered_barrier_projection.utility_control_count, 0, "没有近距离边界威胁时不应奖励施放法球。");
        _test.Eq(score.layered_barrier_projection.reason, "no_nearby_outside_enemy", "投影应解释无收益原因。");
        _test.Eq(score.hit_payoff_score, 0, "无边界威胁时法球效果收益应为零。");
    }

    private static Fixture BuildFixture(string battleId, Vector2I mapSize) =>
        new(battleId, mapSize);

    private static BattleState BuildFlatState(string battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y += 1)
        {
            for (int x = 0; x < mapSize.X; x += 1)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp = 30
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: hp,
            mp: 100,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue("strength", 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("perception", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        string displayName,
        params CombatEffectDefinition[] effects
    ) =>
        BuildSkill(
            skillId,
            displayName,
            effects: effects,
            apCost: 0,
            staminaCost: 0,
            cooldownTu: 0
        );

    private static SkillDefinition BuildSkill(
        StringName skillId,
        string displayName,
        IReadOnlyList<CombatEffectDefinition> effects,
        int apCost,
        int staminaCost,
        int cooldownTu
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName,
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects ?? Array.Empty<CombatEffectDefinition>(),
                rangeValue: 5,
                apCost: apCost,
                staminaCost: staminaCost,
                cooldownTu: cooldownTu
            )
        );
    }

    private static CombatEffectDefinition BuildDamageEffect(int power, StringName targetFilter) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: targetFilter,
            power: power
        );

    private static CombatEffectDefinition BuildTerrainEffect(StringName terrainEffectId) =>
        TestSkillDefinitionProjection.BuildEffect(
            "terrain_effect",
            terrainEffectId: terrainEffectId
        );

    private static CombatEffectDefinition BuildChainDamageEffect(int radius) =>
        TestSkillDefinitionProjection.BuildEffect(
            "chain_damage",
            effectTargetTeamFilter: "any",
            preventRepeatTarget: true,
            parameters: new Dictionary<string, object> { ["base_chain_radius"] = radius }
        );

    private static SkillDefinition BuildLayeredBarrierSkill(
        StringName skillId,
        StringName profileId
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            effectTargetTeamFilter: "self",
            durationTu: 120,
            parameters: new Dictionary<string, object>
            {
                ["profile_id"] = profileId,
                ["radius_cells"] = 2L,
                ["area_pattern"] = new StringName("diamond"),
            }
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "Layered Barrier AI Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "self",
                rangeValue: 0,
                areaPattern: "self"
            )
        );
    }

    private static BarrierProfileDefinition BuildLayeredBarrierProfile(StringName profileId) =>
        new(
            profileId,
            "Layered Barrier AI Probe",
            "fixed",
            "diamond",
            2,
            120,
            true,
            new[]
            {
                new BarrierLayerDefinition(
                    "red",
                    "Red",
                    1,
                    Array.Empty<StringName>(),
                    Array.Empty<StringName>(),
                    Array.Empty<BarrierOutcomeDefinition>()
                ),
                new BarrierLayerDefinition(
                    "orange",
                    "Orange",
                    2,
                    Array.Empty<StringName>(),
                    Array.Empty<StringName>(),
                    Array.Empty<BarrierOutcomeDefinition>()
                ),
            }
        );

    private static BattleBarrierInstanceState BuildLayeredBarrierState(
        StringName profileId,
        Vector2I anchorCoord,
        int remainingTu,
        bool redBroken,
        bool orangeBroken
    )
    {
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "ai_layered_barrier_instance",
            ProfileId = profileId,
            DisplayName = "Layered Barrier AI Probe",
            AnchorCoord = anchorCoord,
            RadiusCells = 2,
            AreaPattern = "diamond",
            RemainingTu = remainingTu,
        };
        barrier.SetLayers(
            new[]
            {
                new BattleBarrierLayerState
                {
                    LayerId = "red",
                    DisplayName = "Red",
                    Order = 1,
                    Broken = redBroken,
                },
                new BattleBarrierLayerState
                {
                    LayerId = "orange",
                    DisplayName = "Orange",
                    Order = 2,
                    Broken = orangeBroken,
                },
            }
        );
        return barrier;
    }

    private static BattleAiScoreInput ScoreLayeredBarrier(
        Fixture fixture,
        BattleUnitState caster,
        SkillDefinition skill
    ) =>
        fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(caster),
            skill,
            BuildCommand(caster, skill.SkillId, caster.GetAnchorCoord(), caster),
            BuildPreview(caster),
            new[] { skill.CombatProfile.EffectDefinitions[0] },
            BuildPositionMetadata(caster, 0, 0)
        );

    private static BattleCommand BuildCommand(
        BattleUnitState actor,
        StringName skillId,
        Vector2I targetCoord,
        BattleUnitState targetUnit = null
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = actor.unit_id,
            skill_id = skillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        if (targetUnit != null)
        {
            command.target_unit_id = targetUnit.unit_id;
            command.AddTargetUnitId(targetUnit.unit_id);
        }
        return command;
    }

    private static BattlePreview BuildPreview(params BattleUnitState[] targets)
    {
        var preview = new BattlePreview
        {
            allowed = true,
        };
        foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
        {
            if (target == null)
            {
                continue;
            }
            preview.AddTargetUnitId(target.unit_id);
            preview.AddTargetCoord(target.GetAnchorCoord());
        }
        return preview;
    }

    private static BattlePreview BuildGroundPreview(params Vector2I[] targetCoords)
    {
        var preview = new BattlePreview
        {
            allowed = true,
        };
        foreach (Vector2I coord in targetCoords ?? Array.Empty<Vector2I>())
        {
            preview.AddTargetCoord(coord);
        }
        return preview;
    }

    private static Dictionary<string, object> BuildPositionMetadata(
        BattleUnitState positionTarget,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = desiredMinDistance,
            ["desired_max_distance"] = desiredMaxDistance,
        };
        if (positionTarget != null)
        {
            metadata["position_target_unit_id"] = positionTarget.unit_id;
        }
        return metadata;
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BattleState State;
        public readonly BattleGridService GridService = new();
        public readonly BattleAiScoreService ScoreService = new();
        private readonly Dictionary<StringName, SkillDefinition> _skillDefinitions = new();
        private readonly Dictionary<StringName, BarrierProfileDefinition> _barrierProfiles = new();

        public Fixture(string battleId, Vector2I mapSize)
        {
            State = BuildFlatState(battleId, mapSize);
        }

        public void Dispose()
        {
            ScoreService.Dispose();
        }

        public void AddSkill(SkillDefinition skillDefinition)
        {
            if (skillDefinition == null || skillDefinition.SkillId == "")
            {
                return;
            }
            _skillDefinitions[skillDefinition.SkillId] = skillDefinition;
        }

        public void AddUnit(BattleUnitState unit)
        {
            if (unit == null || unit.unit_id == "")
            {
                return;
            }
            State.SetUnit(unit);
            if (unit.faction_id == "hostile")
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            bool placed = GridService.PlaceUnit(State, unit, unit.GetAnchorCoord(), true);
            if (!placed)
            {
                throw new InvalidOperationException($"Failed to place test unit {unit.unit_id} at {unit.GetAnchorCoord()}.");
            }
        }

        public void AddBarrierProfile(BarrierProfileDefinition profile)
        {
            if (profile == null || profile.ProfileId == "")
                return;
            _barrierProfiles[profile.ProfileId] = profile;
        }

        public void PutBarrier(BattleBarrierInstanceState barrier)
        {
            if (barrier == null || barrier.BarrierInstanceId == "")
                return;
            State.LayeredBarrierStore.Put(barrier.BarrierInstanceId, barrier);
        }

        public BattleAiContext BuildContext(BattleUnitState actor)
        {
            var context = new BattleAiContext
            {
                state = State,
                unit_state = actor,
                grid_service = GridService,
            };
            context.SetSkillDefinitions(_skillDefinitions);
            context.SetBarrierProfileDefinitions(_barrierProfiles);
            return context;
        }
    }
}
