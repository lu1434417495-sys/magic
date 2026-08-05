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
            TestGuardScoreAppliesPerHitPhysicalMitigationInsideWindow();
            TestGuardScoreIncludesFormalWeaponThreat();
            TestGuardScoreRespectsExpiryMagicFallbackAndRedundancy();
            TestGuardScorePricesSelfSlowWithoutGenericStatusBenefit();
            TestTauntScoresExpectedAllyDamageReliefByCognitionAndWindow();
            TestTauntIgnoresUnavailableOrUnreducibleThreats();
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

    private void TestGuardScoreAppliesPerHitPhysicalMitigationInsideWindow()
    {
        using Fixture fixture = BuildFixture(
            "score_input_guard_per_hit",
            new Vector2I(5, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        SkillDefinition guard = BuildGuardScoreSkill(
            "guard_per_hit_probe",
            guardPower: 2,
            durationTu: 40,
            includeSlow: false
        );
        SkillDefinition doublePhysical = BuildThreatDamageSkill(
            "guard_double_physical_threat",
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 3,
                damageTag: "physical_slash"
            ),
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 3,
                damageTag: "physical_blunt"
            )
        );
        fixture.AddSkill(guard);
        fixture.AddSkill(doublePhysical);

        BattleUnitState actor = BuildUnit(
            "guard_per_hit_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState threat = BuildUnit(
            "guard_per_hit_threat",
            "player",
            new Vector2I(2, 1)
        );
        threat.AddKnownActiveSkill(doublePhysical.SkillId);
        threat.SetActionThresholdTyped(120);
        threat.SetActionProgressTyped(90);
        fixture.AddUnit(actor);
        fixture.AddUnit(threat);

        BattleAiScoreInput score = ScoreGuard(fixture, actor, guard);
        _test.True(score?.has_post_action_threat_projection == true, "格挡评分应生成生存威胁投影。");
        if (score == null)
        {
            return;
        }
        _test.Eq(
            score.pre_action_threat_expected_damage,
            6,
            "格挡前应保留两次各3点的物理伤害。"
        );
        _test.Eq(
            score.post_action_remaining_threat_expected_damage,
            2,
            "2点格挡应逐次作用于两段3点物理伤害，并让每段最低保留1点。"
        );
        _test.False(
            actor.HasStatusEffect("guarding") || actor.HasStatusEffect("slow"),
            "AI 格挡评分不得把候选状态写入正式施法者。"
        );
        _test.Eq(
            threat.GetActionProgressTyped(),
            90,
            "AI 格挡时间窗评分不得修改威胁单位的行动进度。"
        );
    }

    private void TestGuardScoreIncludesFormalWeaponThreat()
    {
        using Fixture fixture = BuildFixture(
            "score_input_guard_weapon_threat",
            new Vector2I(5, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        SkillDefinition guard = BuildGuardScoreSkill(
            "guard_weapon_probe",
            guardPower: 2,
            durationTu: 40,
            includeSlow: false
        );
        fixture.AddSkill(guard);
        BattleUnitState actor = BuildUnit(
            "guard_weapon_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState threat = BuildUnit(
            "guard_weapon_threat",
            "player",
            new Vector2I(2, 1)
        );
        threat.SetNaturalWeaponProjectionTyped(
            "natural_weapon",
            "physical_pierce",
            1,
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            }
        );
        threat.SetActionThresholdTyped(120);
        threat.SetActionProgressTyped(90);
        fixture.AddUnit(actor);
        fixture.AddUnit(threat);

        BattleAiScoreInput score = ScoreGuard(fixture, actor, guard);
        _test.Eq(
            score?.pre_action_threat_expected_damage ?? -1,
            4,
            "武器威胁应通过正式平均伤害预览计为1d6的4点伤害。"
        );
        _test.Eq(
            score?.post_action_remaining_threat_expected_damage ?? -1,
            2,
            "2点格挡应作用于正式武器伤害威胁。"
        );
    }

    private void TestGuardScoreRespectsExpiryMagicFallbackAndRedundancy()
    {
        using (Fixture boundaryFixture = BuildFixture(
            "score_input_guard_expiry_boundary",
            new Vector2I(5, 3)
        ))
        {
            boundaryFixture.ScoreService.Setup(new BattleDamageResolver());
            SkillDefinition guard = BuildGuardScoreSkill(
                "guard_boundary_probe",
                guardPower: 2,
                durationTu: 40,
                includeSlow: false
            );
            SkillDefinition physical = BuildThreatDamageSkill(
                "guard_boundary_physical",
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    effectTargetTeamFilter: "enemy",
                    power: 6,
                    damageTag: "physical_slash"
                )
            );
            boundaryFixture.AddSkill(guard);
            boundaryFixture.AddSkill(physical);
            BattleUnitState actor = BuildUnit(
                "guard_boundary_actor",
                "hostile",
                new Vector2I(1, 1)
            );
            BattleUnitState threat = BuildUnit(
                "guard_boundary_threat",
                "player",
                new Vector2I(2, 1)
            );
            threat.AddKnownActiveSkill(physical.SkillId);
            threat.SetActionThresholdTyped(120);
            threat.SetActionProgressTyped(80);
            boundaryFixture.AddUnit(actor);
            boundaryFixture.AddUnit(threat);

            BattleAiScoreInput boundaryScore = ScoreGuard(
                boundaryFixture,
                actor,
                guard
            );
            _test.Eq(
                boundaryScore?.pre_action_threat_expected_damage ?? -1,
                6,
                "40TU 边界回归前置：威胁应为6点物理伤害。"
            );
            _test.Eq(
                boundaryScore?.post_action_remaining_threat_expected_damage ?? -1,
                6,
                "敌人恰好在40TU后行动时，40TU格挡应已到期，不能计入减伤。"
            );
        }

        using (Fixture fallbackFixture = BuildFixture(
            "score_input_guard_magic_fallback",
            new Vector2I(5, 3)
        ))
        {
            fallbackFixture.ScoreService.Setup(new BattleDamageResolver());
            SkillDefinition guard = BuildGuardScoreSkill(
                "guard_magic_fallback_probe",
                guardPower: 2,
                durationTu: 40,
                includeSlow: false
            );
            SkillDefinition physical = BuildThreatDamageSkill(
                "guard_fallback_physical",
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    effectTargetTeamFilter: "enemy",
                    power: 6,
                    damageTag: "physical_slash"
                )
            );
            SkillDefinition magic = BuildThreatDamageSkill(
                "guard_fallback_magic",
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    effectTargetTeamFilter: "enemy",
                    power: 8,
                    damageTag: "force"
                )
            );
            fallbackFixture.AddSkill(guard);
            fallbackFixture.AddSkill(physical);
            fallbackFixture.AddSkill(magic);
            BattleUnitState actor = BuildUnit(
                "guard_fallback_actor",
                "hostile",
                new Vector2I(1, 1)
            );
            BattleUnitState threat = BuildUnit(
                "guard_fallback_threat",
                "player",
                new Vector2I(2, 1)
            );
            threat.AddKnownActiveSkill(physical.SkillId);
            threat.AddKnownActiveSkill(magic.SkillId);
            threat.SetActionThresholdTyped(120);
            threat.SetActionProgressTyped(90);
            fallbackFixture.AddUnit(actor);
            fallbackFixture.AddUnit(threat);

            BattleAiScoreInput fallbackScore = ScoreGuard(
                fallbackFixture,
                actor,
                guard
            );
            _test.Eq(
                fallbackScore?.pre_action_threat_expected_damage ?? -1,
                8,
                "魔法替代回归前置：敌人最佳攻击应为8点非物理伤害。"
            );
            _test.Eq(
                fallbackScore?.post_action_remaining_threat_expected_damage ?? -1,
                8,
                "AI 不应把格挡错误应用到非物理最佳攻击。"
            );
        }

        using Fixture redundancyFixture = BuildFixture(
            "score_input_guard_redundancy",
            new Vector2I(5, 3)
        );
        redundancyFixture.ScoreService.Setup(new BattleDamageResolver());
        SkillDefinition redundantGuard = BuildGuardScoreSkill(
            "guard_redundancy_probe",
            guardPower: 2,
            durationTu: 40,
            includeSlow: false
        );
        SkillDefinition redundantThreatSkill = BuildThreatDamageSkill(
            "guard_redundancy_physical",
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 6,
                damageTag: "physical_slash"
            )
        );
        redundancyFixture.AddSkill(redundantGuard);
        redundancyFixture.AddSkill(redundantThreatSkill);
        BattleUnitState redundantActor = BuildUnit(
            "guard_redundancy_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        redundantActor.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "guarding",
                power = 2,
                duration = 40,
                stacks = 1,
            }
        );
        BattleUnitState redundantThreat = BuildUnit(
            "guard_redundancy_threat",
            "player",
            new Vector2I(2, 1)
        );
        redundantThreat.AddKnownActiveSkill(redundantThreatSkill.SkillId);
        redundantThreat.SetActionThresholdTyped(120);
        redundantThreat.SetActionProgressTyped(90);
        redundancyFixture.AddUnit(redundantActor);
        redundancyFixture.AddUnit(redundantThreat);

        BattleAiScoreInput redundancyScore = ScoreGuard(
            redundancyFixture,
            redundantActor,
            redundantGuard
        );
        _test.Eq(
            redundancyScore?.pre_action_threat_expected_damage ?? -1,
            4,
            "已有同强度格挡时，格挡前威胁应已包含现有减伤。"
        );
        _test.Eq(
            redundancyScore?.post_action_remaining_threat_expected_damage ?? -1,
            4,
            "重复施放同强度格挡不应再次获得完整减伤收益。"
        );
    }

    private void TestGuardScorePricesSelfSlowWithoutGenericStatusBenefit()
    {
        using Fixture fixture = BuildFixture(
            "score_input_guard_self_slow",
            new Vector2I(5, 3)
        );
        SkillDefinition guardOnly = BuildGuardScoreSkill(
            "guard_without_slow_probe",
            guardPower: 1,
            durationTu: 40,
            includeSlow: false
        );
        SkillDefinition guardWithSlow = BuildGuardScoreSkill(
            "guard_with_slow_probe",
            guardPower: 1,
            durationTu: 40,
            includeSlow: true
        );
        fixture.AddSkill(guardOnly);
        fixture.AddSkill(guardWithSlow);
        BattleUnitState actor = BuildUnit(
            "guard_slow_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        actor.SetCurrentMovePoints(6);
        fixture.AddUnit(actor);

        BattleAiScoreInput guardOnlyScore = ScoreGuard(fixture, actor, guardOnly);
        BattleAiScoreInput slowScore = ScoreGuard(fixture, actor, guardWithSlow);
        _test.True(
            guardOnlyScore != null && slowScore != null,
            "格挡移动成本回归应生成两个合法评分。"
        );
        if (guardOnlyScore == null || slowScore == null)
        {
            return;
        }
        _test.Eq(
            guardOnlyScore.estimated_status_count,
            0,
            "专用减伤投影接管 guarding 后，不应再叠加泛化状态收益。"
        );
        _test.Eq(
            slowScore.estimated_status_count,
            0,
            "自施加 slow 不应被当成第二个正向状态收益。"
        );
        int expectedLostReachCost =
            3 * fixture.ScoreService.GetProfile().MovementCostWeight;
        _test.Eq(
            slowScore.resource_cost_score - guardOnlyScore.resource_cost_score,
            expectedLostReachCost,
            "6点移动力在每格+1成本下会损失3格可达距离，AI 应按移动权重计入机会成本。"
        );
    }

    private void TestTauntScoresExpectedAllyDamageReliefByCognitionAndWindow()
    {
        using Fixture fixture = BuildFixture(
            "score_input_taunt_ally_relief",
            new Vector2I(7, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        SkillDefinition taunt = BuildTauntScoreSkill();
        SkillDefinition threatSkill = BuildThreatDamageSkill(
            "taunt_damage_threat",
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 10,
                damageTag: "physical_slash"
            )
        );
        fixture.AddSkill(taunt);
        fixture.AddSkill(threatSkill);

        BattleUnitState actor = BuildUnit(
            "taunt_score_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState threat = BuildUnit(
            "taunt_score_threat",
            "player",
            new Vector2I(3, 1)
        );
        BattleUnitState protectedAlly = BuildUnit(
            "taunt_score_protected_ally",
            "hostile",
            new Vector2I(4, 1)
        );
        threat.SetBaseCognitionKindTyped(BattleCognitionKind.Sapient);
        threat.AddKnownActiveSkill(threatSkill.SkillId);
        threat.SetActionThresholdTyped(120);
        threat.SetActionProgressTyped(90);
        protectedAlly.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            10
        );
        fixture.AddUnit(actor);
        fixture.AddUnit(threat);
        fixture.AddUnit(protectedAlly);

        BattleAiScoreInput sapientScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            sapientScore?.estimated_taunt_ally_damage_relief ?? -1,
            2,
            "55%命中、10点伤害在劣势下应减少约2点友军预期伤害，不能误用命中率平方。"
        );
        _test.Eq(
            sapientScore?.estimated_control_count ?? -1,
            0,
            "挑衅不应继续领取通用状态控制分。"
        );
        _test.Eq(
            sapientScore?.estimated_status_count ?? -1,
            0,
            "挑衅的专用收益不应伪装成通用状态数量。"
        );
        _test.True(
            (sapientScore?.hit_payoff_score ?? 0) > 0,
            "能保护友军的挑衅应产生正向 AI 收益。"
        );

        threat.SetCooldownTyped(threatSkill.SkillId, 35);
        BattleAiScoreInput cooldownBlockedScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            cooldownBlockedScore
                ?.estimated_taunt_ally_damage_relief
                ?? -1,
            0,
            "威胁30TU后行动但技能仍剩5TU冷却时，挑衅不得领取该技能的保护分。"
        );
        threat.SetCooldownTyped(threatSkill.SkillId, 30);
        BattleAiScoreInput cooldownReadyScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            cooldownReadyScore?.estimated_taunt_ally_damage_relief
                ?? -1,
            2,
            "技能冷却恰好在威胁行动前归零时，应重新计入挑衅保护分。"
        );
        threat.SetCooldownTyped(threatSkill.SkillId, 0);

        threat.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TAUNTED,
                source_unit_id = actor.unit_id,
                duration = 60,
                power = 1,
                stacks = 1,
            }
        );
        BattleAiScoreInput redundantDisadvantageScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            redundantDisadvantageScore
                ?.estimated_taunt_ally_damage_relief
                ?? -1,
            0,
            "友军已经受到该威胁的攻击劣势保护时，重复挑衅不得再次计价。"
        );
        threat.EraseStatusEffect(
            BattleStatusSemanticTable.STATUS_TAUNTED
        );

        threat.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_MADNESS,
                duration = 60,
                power = 1,
                stacks = 1,
            }
        );
        BattleAiScoreInput madnessScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            madnessScore?.estimated_taunt_ally_damage_relief ?? -1,
            0,
            "疯狂将有效认知压到野兽心智后，AI 不应给挑衅保护分。"
        );
        threat.EraseStatusEffect(BattleStatusSemanticTable.STATUS_MADNESS);

        threat.SetBaseCognitionKindTyped(BattleCognitionKind.Instinctive);
        BattleAiScoreInput instinctiveScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            instinctiveScore?.effective_target_count ?? -1,
            0,
            "基础认知为野兽心智的单位不应成为挑衅有效目标。"
        );

        threat.SetBaseCognitionKindTyped(BattleCognitionKind.Sapient);
        threat.SetActionProgressTyped(80);
        BattleAiScoreInput expiredAtReadyScore = ScoreTaunt(
            fixture,
            actor,
            threat,
            taunt
        );
        _test.Eq(
            expiredAtReadyScore?.estimated_taunt_ally_damage_relief ?? -1,
            0,
            "敌人恰好40TU后行动时，40TU挑衅应已到期，不能领取保护分。"
        );

        using Fixture weaponFixture = BuildFixture(
            "score_input_taunt_weapon_threat",
            new Vector2I(5, 3)
        );
        weaponFixture.ScoreService.Setup(new BattleDamageResolver());
        SkillDefinition weaponTaunt = BuildTauntScoreSkill();
        weaponFixture.AddSkill(weaponTaunt);
        BattleUnitState weaponActor = BuildUnit(
            "taunt_weapon_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState weaponThreat = BuildUnit(
            "taunt_weapon_threat",
            "player",
            new Vector2I(3, 1)
        );
        BattleUnitState weaponProtectedAlly = BuildUnit(
            "taunt_weapon_protected_ally",
            "hostile",
            new Vector2I(4, 1)
        );
        weaponThreat.SetBaseCognitionKindTyped(
            BattleCognitionKind.Sapient
        );
        weaponThreat.SetNaturalWeaponProjectionTyped(
            "taunt_test_weapon",
            "physical_pierce",
            1,
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            }
        );
        weaponThreat.SetActionThresholdTyped(120);
        weaponThreat.SetActionProgressTyped(90);
        weaponProtectedAlly.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            10
        );
        weaponFixture.AddUnit(weaponActor);
        weaponFixture.AddUnit(weaponThreat);
        weaponFixture.AddUnit(weaponProtectedAlly);

        BattleAiScoreInput weaponScore = ScoreTaunt(
            weaponFixture,
            weaponActor,
            weaponThreat,
            weaponTaunt
        );
        _test.Eq(
            weaponScore?.estimated_taunt_ally_damage_relief ?? -1,
            1,
            "即使敌人没有已学伤害技能，标准1d6武器攻击也应给挑衅贡献1点预期友军减伤。"
        );
    }

    private void TestTauntIgnoresUnavailableOrUnreducibleThreats()
    {
        CombatEffectDefinition damageEffect =
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 20,
                damageTag: "physical_slash"
            );
        BattleAiScoreInput directEffectScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "taunt_direct_effect_threat",
                    "direct_effect",
                    0,
                    damageEffect
                )
            );
        _test.Eq(
            directEffectScore?.estimated_taunt_ally_damage_relief
                ?? -1,
            0,
            "直伤模式不做攻击检定，挑衅施加的劣势不能降低其伤害。"
        );

        BattleAiScoreInput forceHitScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "taunt_force_hit_threat",
                    "force_hit_no_crit",
                    0,
                    damageEffect
                )
            );
        _test.Eq(
            forceHitScore?.estimated_taunt_ally_damage_relief ?? -1,
            0,
            "显式强制命中技能不受劣势影响，不得计入挑衅保护分。"
        );

        BattleAiScoreInput specialForceHitScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "black_contract_push",
                    "",
                    0,
                    damageEffect
                ),
                bindCastRules: false
            );
        _test.Eq(
            specialForceHitScore
                ?.estimated_taunt_ally_damage_relief
                ?? -1,
            0,
            "规则层认定的强制命中特例也必须由挑衅评分统一排除。"
        );

        BattleAiScoreInput insufficientStaminaScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "taunt_stamina_blocked_threat",
                    "",
                    20,
                    damageEffect
                ),
                threat =>
                {
                    threat.SetCurrentStamina(0);
                    threat.attribute_snapshot.SetValue(
                        AttributeService.ToStringName(
                            AttributeIdKind.StaminaMax
                        ),
                        10
                    );
                }
            );
        _test.Eq(
            insufficientStaminaScore
                ?.estimated_taunt_ally_damage_relief
                ?? -1,
            0,
            "威胁在下次行动前恢复到体力上限仍付不起技能时，不得虚增挑衅收益。"
        );

        BattleAiScoreInput recoveredStaminaScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "taunt_stamina_recovers_in_time",
                    "",
                    0,
                    10,
                    damageEffect
                ),
                threat =>
                {
                    threat.SetCurrentStamina(0);
                    threat.attribute_snapshot.SetValue(
                        AttributeService.ToStringName(
                            AttributeIdKind.StaminaMax
                        ),
                        10
                    );
                }
            );
        _test.True(
            (
                recoveredStaminaScore
                    ?.estimated_taunt_ally_damage_relief
                ?? 0
            ) > 0,
            "当前体力不足但能在下次行动前恢复到成本时，技能仍应计入挑衅收益。"
        );

        BattleAiScoreInput nextTurnApScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "taunt_ap_resets_in_time",
                    "",
                    1,
                    0,
                    damageEffect
                ),
                threat =>
                {
                    threat.SetCurrentAp(0);
                    threat.attribute_snapshot.SetValue(
                        AttributeService.ToStringName(
                            AttributeIdKind.ActionPoints
                        ),
                        1
                    );
                }
            );
        _test.True(
            (
                nextTurnApScore?.estimated_taunt_ally_damage_relief
                ?? 0
            ) > 0,
            "当前AP为0但下次行动会重置到1AP时，技能仍应计入挑衅收益。"
        );

        BattleAiScoreInput noProtectedAllyScore =
            ScoreIsolatedTauntThreat(
                BuildThreatDamageSkill(
                    "taunt_no_ally_threat",
                    damageEffect
                ),
                includeProtectedAlly: false
            );
        _test.Eq(
            noProtectedAllyScore
                ?.estimated_taunt_ally_damage_relief
                ?? -1,
            0,
            "场上没有可被保护的其他友军时，挑衅不得凭空获得减伤收益。"
        );
    }

    private BattleAiScoreInput ScoreIsolatedTauntThreat(
        SkillDefinition threatSkill,
        Action<BattleUnitState> configureThreat = null,
        bool includeProtectedAlly = true,
        bool bindCastRules = true
    )
    {
        using Fixture fixture = BuildFixture(
            $"score_input_{threatSkill.SkillId}",
            new Vector2I(7, 3)
        );
        fixture.ScoreService.Setup(new BattleDamageResolver());
        fixture.BindSkillCastRules = bindCastRules;
        SkillDefinition taunt = BuildTauntScoreSkill();
        fixture.AddSkill(taunt);
        fixture.AddSkill(threatSkill);

        BattleUnitState actor = BuildUnit(
            "isolated_taunt_actor",
            "hostile",
            new Vector2I(1, 1)
        );
        BattleUnitState threat = BuildUnit(
            "isolated_taunt_threat",
            "player",
            new Vector2I(3, 1)
        );
        threat.SetBaseCognitionKindTyped(BattleCognitionKind.Sapient);
        threat.AddKnownActiveSkill(threatSkill.SkillId);
        threat.SetActionThresholdTyped(120);
        threat.SetActionProgressTyped(90);
        configureThreat?.Invoke(threat);
        int apBeforeScore = threat.GetCurrentAp();
        int staminaBeforeScore = threat.GetCurrentStamina();
        int cooldownBeforeScore =
            threat.GetCooldownTyped(threatSkill.SkillId);
        fixture.AddUnit(actor);
        fixture.AddUnit(threat);

        if (includeProtectedAlly)
        {
            BattleUnitState protectedAlly = BuildUnit(
                "isolated_taunt_protected_ally",
                "hostile",
                new Vector2I(4, 1)
            );
            protectedAlly.attribute_snapshot.SetValue(
                AttributeService.ToStringName(
                    AttributeIdKind.ArmorClass
                ),
                10
            );
            fixture.AddUnit(protectedAlly);
        }
        BattleAiScoreInput score =
            ScoreTaunt(fixture, actor, threat, taunt);
        _test.Eq(
            threat.GetCurrentAp(),
            apBeforeScore,
            "挑衅未来行动投影不得改写威胁单位的真实AP。"
        );
        _test.Eq(
            threat.GetCurrentStamina(),
            staminaBeforeScore,
            "挑衅未来行动投影不得改写威胁单位的真实体力。"
        );
        _test.Eq(
            threat.GetCooldownTyped(threatSkill.SkillId),
            cooldownBeforeScore,
            "挑衅未来行动投影不得推进真实技能冷却。"
        );
        return score;
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

    private static SkillDefinition BuildGuardScoreSkill(
        StringName skillId,
        int guardPower,
        int durationTu,
        bool includeSlow
    )
    {
        var effects = new List<CombatEffectDefinition>
        {
            TestSkillDefinitionProjection.BuildEffect(
                "status",
                effectTargetTeamFilter: "self",
                statusId: "guarding",
                power: guardPower,
                durationTu: durationTu
            ),
        };
        if (includeSlow)
        {
            effects.Add(
                TestSkillDefinitionProjection.BuildEffect(
                    "status",
                    effectTargetTeamFilter: "self",
                    statusId: "slow",
                    power: 1,
                    durationTu: durationTu
                )
            );
        }
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "Guard AI Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: "unit",
                targetTeamFilter: "self",
                rangeValue: 0,
                apCost: 1,
                staminaCost: 20,
                cooldownTu: 120,
                targetSelectionMode: "self"
            )
        );
    }

    private static SkillDefinition BuildTauntScoreSkill()
    {
        CombatEffectDefinition effect =
            TestSkillDefinitionProjection.BuildEffect(
                "status",
                effectTargetTeamFilter: "enemy",
                statusId: BattleStatusSemanticTable.STATUS_TAUNTED,
                power: 1,
                durationTu: 40,
                requiredTargetMinCognition:
                    BattleCognitionKind.Sapient
            );
        return TestSkillDefinitionProjection.BuildSkill(
            "taunt_score_probe",
            "Taunt AI Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                "taunt_score_probe",
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 1
            )
        );
    }

    private static SkillDefinition BuildThreatDamageSkill(
        StringName skillId,
        params CombatEffectDefinition[] effects
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "Guard Threat Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 5
            )
        );

    private static SkillDefinition BuildThreatDamageSkill(
        StringName skillId,
        StringName attackResolutionMode,
        int staminaCost,
        params CombatEffectDefinition[] effects
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "Taunt Threat Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 5,
                staminaCost: staminaCost,
                attackResolutionMode: attackResolutionMode
            )
        );

    private static SkillDefinition BuildThreatDamageSkill(
        StringName skillId,
        StringName attackResolutionMode,
        int apCost,
        int staminaCost,
        params CombatEffectDefinition[] effects
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "Taunt Threat Resource Probe",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 5,
                apCost: apCost,
                staminaCost: staminaCost,
                attackResolutionMode: attackResolutionMode
            )
        );

    private static BattleAiScoreInput ScoreGuard(
        Fixture fixture,
        BattleUnitState actor,
        SkillDefinition skill
    ) =>
        fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(actor),
            skill,
            BuildCommand(actor, skill.SkillId, actor.GetAnchorCoord(), actor),
            BuildPreview(actor),
            skill.CombatProfile.EffectDefinitions,
            BuildPositionMetadata(actor, 0, 0)
        );

    private static BattleAiScoreInput ScoreTaunt(
        Fixture fixture,
        BattleUnitState actor,
        BattleUnitState threat,
        SkillDefinition skill
    ) =>
        fixture.ScoreService.BuildSkillScoreInput(
            fixture.BuildContext(actor),
            skill,
            BuildCommand(
                actor,
                skill.SkillId,
                threat.GetAnchorCoord(),
                threat
            ),
            BuildPreview(threat),
            skill.CombatProfile.EffectDefinitions,
            BuildPositionMetadata(threat, 0, 1)
        );

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
        public bool BindSkillCastRules = true;
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
            if (BindSkillCastRules)
            {
                var skillTurnResolver =
                    new BattleRuntimeSkillTurnResolver();
                context.skill_cast_block_reason_callback =
                    skillTurnResolver.GetSkillCastBlockReason;
            }
            return context;
        }
    }
}
