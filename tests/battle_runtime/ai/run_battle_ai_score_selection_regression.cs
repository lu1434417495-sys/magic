using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_score_selection_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBrainScoreProfileFeedsDecisionScopeAndFactionOverrideWins();
            TestMeleeActionPrefersLaterHigherScoreSkillAction();
            TestRangedScorePrefersUnitNukeOverSingleTargetAreaBlast();
            TestUnitSkillActionSelectsHigherHitPayoffTarget();
            TestMultiUnitPositionActionHonorsLockedMovePoints();
            TestRetreatActionHonorsLockedMovePoints();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI score selection regression"));
    }

    private void TestBrainScoreProfileFeedsDecisionScopeAndFactionOverrideWins()
    {
        BattleAiScoreProfile brainProfile = new() { damage_weight = 77 };
        EnemyAiBrainDef brain = new()
        {
            brain_id = "score_profile_brain",
            default_state_id = "pressure",
            score_profile = brainProfile,
        };
        BattleAiService aiService = new() { EnableMutationGuard = false };
        aiService.Setup(BuildBrainMap(brain));

        BattleUnitState actor = BuildUnit("score_profile_actor", "hostile", Vector2I.Zero);
        actor.ai_brain_id = brain.brain_id;
        BattleAiScoreService scoreService = aiService.GetScoreService();
        scoreService.BeginDecisionScope(
            new BattleState(),
            actor,
            new Dictionary<StringName, SkillDef>()
        );
        _test.Eq(
            scoreService.GetProfile()?.damage_weight ?? -1,
            77,
            "brain.score_profile 应在 AI decision scope 内成为当前评分 profile。"
        );
        scoreService.EndDecisionScope();

        BattleAiScoreProfile factionProfile = new() { damage_weight = 12 };
        aiService.SetFactionScoreProfiles(
            new Dictionary<StringName, BattleAiScoreProfile>
            {
                ["hostile"] = factionProfile,
            }
        );
        scoreService.BeginDecisionScope(
            new BattleState(),
            actor,
            new Dictionary<StringName, SkillDef>()
        );
        _test.Eq(
            scoreService.GetProfile()?.damage_weight ?? -1,
            12,
            "simulation faction score profile 应优先于 brain.score_profile。"
        );
        scoreService.EndDecisionScope();
    }

    private void TestMeleeActionPrefersLaterHigherScoreSkillAction()
    {
        Fixture fixture = BuildFixture("score_selection_melee_action", new Vector2I(5, 3));
        SkillDef heavySkill = BuildUnitSkill(
            "warrior_heavy_strike",
            "Heavy Strike",
            5,
            range: 1
        );
        SkillDef executeSkill = BuildUnitSkill(
            "warrior_execution_cleave",
            "Execution Cleave",
            18,
            range: 1
        );
        fixture.AddSkill(heavySkill);
        fixture.AddSkill(executeSkill);

        BattleUnitState wolf = BuildUnit("wolf_score_melee", "hostile", new Vector2I(1, 1));
        wolf.ai_brain_id = "melee_score_probe";
        wolf.known_active_skill_ids.Add(heavySkill.skill_id);
        wolf.known_active_skill_ids.Add(executeSkill.skill_id);
        BattleUnitState player = BuildUnit("low_hp_target", "player", new Vector2I(2, 1), hp: 20);
        player.current_hp = 5;
        fixture.AddUnit(wolf);
        fixture.AddUnit(player);

        BattleAiContext context = fixture.BuildContext(wolf);
        context.allow_authored_action_fallback_for_tests = true;
        BattleAiScoreInput heavyScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            heavySkill,
            BuildCommand(wolf, heavySkill.skill_id, player.coord, player),
            BuildPreview(player),
            new[] { heavySkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(player, 1, 1)
        );
        BattleAiScoreInput executeScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            executeSkill,
            BuildCommand(wolf, executeSkill.skill_id, player.coord, player),
            BuildPreview(player),
            new[] { executeSkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(player, 1, 1)
        );

        _test.True(heavyScore != null && executeScore != null, "melee 评分回归应拿到两个合法技能候选的评分。");
        if (heavyScore == null || executeScore == null)
        {
            return;
        }
        _test.True(
            executeScore.total_score > heavyScore.total_score,
            "残血目标场景下，warrior_execution_cleave 的评分应高于 warrior_heavy_strike。"
        );

        BattleAiService aiService = new() { EnableMutationGuard = false };
        aiService.Setup(BuildBrainMap(BuildTwoUnitSkillActionBrain(wolf.ai_brain_id, heavySkill.skill_id, executeSkill.skill_id)));
        BattleAiDecision decision = aiService.ChooseCommand(context);
        _test.True(decision != null && decision.state_id == new StringName("pressure"), "melee 评分选技回归应保持 pressure 状态。");
        _test.Eq(
            decision?.command?.skill_id ?? new StringName(""),
            executeSkill.skill_id,
            "melee AI 不应只按 action 顺序选择先声明的 warrior_heavy_strike。"
        );
        _test.Eq(
            decision?.action_id ?? new StringName(""),
            new StringName("score_probe_higher"),
            "melee AI 应能选中后声明但评分更高的技能 action。"
        );
    }

    private void TestRangedScorePrefersUnitNukeOverSingleTargetAreaBlast()
    {
        Fixture fixture = BuildFixture("score_selection_ranged_skill_compare", new Vector2I(7, 5));
        SkillDef fireballSkill = BuildGroundSkill("mage_fireball", "Fireball", 6, range: 4);
        SkillDef iceLanceSkill = BuildUnitSkill("mage_ice_lance", "Ice Lance", 16, range: 4);
        fixture.AddSkill(fireballSkill);
        fixture.AddSkill(iceLanceSkill);

        BattleUnitState caster = BuildUnit("mist_score_caster", "hostile", new Vector2I(1, 2));
        caster.known_active_skill_ids.Add(fireballSkill.skill_id);
        caster.known_active_skill_ids.Add(iceLanceSkill.skill_id);
        BattleUnitState player = BuildUnit("single_target", "player", new Vector2I(4, 2));
        fixture.AddUnit(caster);
        fixture.AddUnit(player);

        BattleAiContext context = fixture.BuildContext(caster);
        BattleAiScoreInput fireballScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            fireballSkill,
            BuildCommand(caster, fireballSkill.skill_id, player.coord),
            BuildPreview(player),
            new[] { fireballSkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(null, 3, 4)
        );
        BattleAiScoreInput iceLanceScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            iceLanceSkill,
            BuildCommand(caster, iceLanceSkill.skill_id, player.coord, player),
            BuildPreview(player),
            new[] { iceLanceSkill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(player, 3, 4)
        );

        _test.True(fireballScore != null && iceLanceScore != null, "ranged_controller 评分回归应拿到两个合法技能候选的评分。");
        if (fireballScore == null || iceLanceScore == null)
        {
            return;
        }
        _test.True(
            iceLanceScore.total_score > fireballScore.total_score,
            "单体目标场景下，mage_ice_lance 的评分应高于 mage_fireball。"
        );
    }

    private void TestUnitSkillActionSelectsHigherHitPayoffTarget()
    {
        Fixture fixture = BuildFixture("score_selection_unit_target_payoff", new Vector2I(7, 5));
        SkillDef skill = BuildUnitSkill("archer_pinning_shot", "Pinning Shot", 10, range: 6);
        fixture.AddSkill(skill);

        BattleUnitState archer = BuildUnit("score_archer", "hostile", new Vector2I(1, 2));
        archer.known_active_skill_ids.Add(skill.skill_id);
        BattleUnitState closeTank = BuildUnit("close_tank", "player", new Vector2I(2, 2));
        BattleUnitState farScout = BuildUnit("far_scout", "player", new Vector2I(4, 2));
        fixture.AddUnit(archer);
        fixture.AddUnit(closeTank);
        fixture.AddUnit(farScout);

        BattleAiContext context = fixture.BuildContext(archer);
        context.skill_score_input_callback = (aiContext, skillDef, command, preview, effects, metadata) =>
        {
            if (command != null && preview != null)
            {
                preview.hit_preview =
                    command.target_unit_id == closeTank.unit_id
                        ? BuildHitPreview(20)
                        : BuildHitPreview(90);
            }
            return fixture.ScoreService.BuildSkillScoreInput(
                aiContext,
                skillDef,
                command,
                preview,
                effects ?? Array.Empty<CombatEffectDef>(),
                metadata
            );
        };

        BattleAiScoreInput closeScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            skill,
            BuildCommand(archer, skill.skill_id, closeTank.coord, closeTank),
            BuildPreview(closeTank, BuildHitPreview(20)),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(closeTank, 0, 6)
        );
        BattleAiScoreInput farScore = fixture.ScoreService.BuildSkillScoreInput(
            context,
            skill,
            BuildCommand(archer, skill.skill_id, farScout.coord, farScout),
            BuildPreview(farScout, BuildHitPreview(90)),
            new[] { skill.combat_profile.effect_defs[0] },
            BuildPositionMetadata(farScout, 0, 6)
        );

        _test.True(closeScore != null && farScore != null, "unit skill score input 应能为多个候选目标生成评分上下文。");
        if (closeScore == null || farScore == null)
        {
            return;
        }
        _test.True(
            farScore.hit_payoff_score > closeScore.hit_payoff_score,
            "更脆弱的远处目标应提供更高的命中收益评分。"
        );
        _test.True(
            farScore.total_score > closeScore.total_score,
            "共享评分上下文应允许高收益目标压过默认最近目标。"
        );

        var action = new UseUnitSkillAction
        {
            action_id = "score_pick_best_target",
            target_selector = "nearest_enemy",
            desired_min_distance = 0,
            desired_max_distance = 6,
            DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
        };
        action.skill_ids.Add(skill.skill_id);
        BattleAiDecision decision = action.Decide(context);
        _test.True(decision != null && decision.command != null, "共享 unit score input 后应仍能生成合法指令。");
        _test.Eq(
            decision?.command?.target_unit_id ?? new StringName(""),
            farScout.unit_id,
            "UseUnitSkillAction 应根据共享评分上下文选择更高命中收益的目标。"
        );
    }

    private void TestMultiUnitPositionActionHonorsLockedMovePoints()
    {
        Fixture fixture = BuildFixture("multi_unit_position_move_lock", new Vector2I(6, 5));
        SkillDef multishot = BuildMultiUnitSkill("archer_multishot_lock_probe", "Multishot Lock Probe", range: 4);
        fixture.AddSkill(multishot);

        BattleUnitState archer = BuildUnit("locked_multishot_archer", "hostile", new Vector2I(0, 2));
        archer.current_move_points = 1;
        archer.has_moved_this_turn = true;
        archer.can_use_locked_move_points_this_turn = false;
        archer.known_active_skill_ids.Add(multishot.skill_id);
        BattleUnitState targetA = BuildUnit("multi_lock_target_a", "player", new Vector2I(4, 1));
        BattleUnitState targetB = BuildUnit("multi_lock_target_b", "player", new Vector2I(4, 3));
        fixture.AddUnit(archer);
        fixture.AddUnit(targetA);
        fixture.AddUnit(targetB);

        var action = new MoveToMultiUnitSkillPositionAction
        {
            action_id = "locked_multishot_position",
            target_selector = "nearest_enemy",
            desired_min_distance = 3,
            desired_max_distance = 5,
            DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
        };
        action.skill_ids.Add(multishot.skill_id);

        BattleAiContext lockedContext = fixture.BuildContext(archer);
        InstallSimpleActionScoreInput(lockedContext);
        BattleAiDecision lockedDecision = action.Decide(lockedContext);
        _test.True(
            lockedDecision == null,
            "已移动且未获准使用锁定移动力时，multi-unit 站位动作不应继续产出移动指令。"
        );

        archer.can_use_locked_move_points_this_turn = true;
        BattleAiContext allowedContext = fixture.BuildContext(archer);
        InstallSimpleActionScoreInput(allowedContext);
        BattleAiDecision allowedDecision = action.Decide(allowedContext);
        _test.True(
            allowedDecision?.command?.IsMove() == true,
            "获得锁定移动力许可时，multi-unit 站位动作仍应能产出移动指令。"
        );
        _test.Eq(
            allowedDecision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(1, 2),
            "multi-unit 站位动作应选择能增加覆盖目标数的合法移动格。"
        );
    }

    private void TestRetreatActionHonorsLockedMovePoints()
    {
        Fixture fixture = BuildFixture("retreat_move_lock", new Vector2I(5, 5));
        BattleUnitState archer = BuildUnit("locked_retreat_archer", "hostile", new Vector2I(2, 2));
        archer.current_move_points = 1;
        archer.has_moved_this_turn = true;
        archer.can_use_locked_move_points_this_turn = false;
        BattleUnitState threat = BuildUnit("retreat_lock_threat", "player", new Vector2I(2, 1));
        fixture.AddUnit(archer);
        fixture.AddUnit(threat);

        var action = new RetreatAction
        {
            action_id = "locked_retreat_probe",
            target_selector = "nearest_enemy",
            minimum_safe_distance = 3,
        };

        BattleAiContext lockedContext = fixture.BuildContext(archer);
        InstallSimpleActionScoreInput(lockedContext);
        BattleAiDecision lockedDecision = action.Decide(lockedContext);
        _test.True(
            lockedDecision == null,
            "已移动且未获准使用锁定移动力时，retreat 动作不应继续产出移动指令。"
        );

        archer.can_use_locked_move_points_this_turn = true;
        BattleAiContext allowedContext = fixture.BuildContext(archer);
        InstallSimpleActionScoreInput(allowedContext);
        BattleAiDecision allowedDecision = action.Decide(allowedContext);
        _test.True(
            allowedDecision?.command?.IsMove() == true,
            "获得锁定移动力许可时，retreat 动作仍应能产出移动指令。"
        );
    }

    private static Fixture BuildFixture(string battleId, Vector2I mapSize) =>
        new(battleId, mapSize);

    private static Dictionary<StringName, EnemyAiBrainDef> BuildBrainMap(EnemyAiBrainDef brain) =>
        new()
        {
            [brain.brain_id] = brain,
        };

    private static EnemyAiBrainDef BuildTwoUnitSkillActionBrain(
        StringName brainId,
        StringName lowerSkillId,
        StringName higherSkillId
    )
    {
        var lowerAction = new UseUnitSkillAction
        {
            action_id = "score_probe_lower",
            target_selector = "nearest_enemy",
            desired_min_distance = 1,
            desired_max_distance = 1,
            DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
        };
        lowerAction.skill_ids.Add(lowerSkillId);
        var higherAction = new UseUnitSkillAction
        {
            action_id = "score_probe_higher",
            target_selector = "nearest_enemy",
            desired_min_distance = 1,
            desired_max_distance = 1,
            DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
        };
        higherAction.skill_ids.Add(higherSkillId);

        var state = new EnemyAiStateDef
        {
            state_id = "pressure",
        };
        state.actions.Add(lowerAction);
        state.actions.Add(higherAction);
        var brain = new EnemyAiBrainDef
        {
            brain_id = brainId,
            default_state_id = state.state_id,
        };
        brain.states.Add(state);
        return brain;
    }

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
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
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
            coord = coord,
            current_hp = hp,
            current_ap = 2,
            current_mp = 100,
            current_stamina = 100,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue("strength", 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("perception", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.RefreshFootprint();
        return unit;
    }

    private static SkillDef BuildUnitSkill(StringName skillId, string displayName, int power, int range)
    {
        SkillDef skill = BuildSkill(skillId, displayName, power, range);
        skill.combat_profile.target_mode = "unit";
        return skill;
    }

    private static SkillDef BuildGroundSkill(StringName skillId, string displayName, int power, int range)
    {
        SkillDef skill = BuildSkill(skillId, displayName, power, range);
        skill.combat_profile.target_mode = "ground";
        return skill;
    }

    private static SkillDef BuildMultiUnitSkill(StringName skillId, string displayName, int range)
    {
        SkillDef skill = BuildUnitSkill(skillId, displayName, 6, range);
        skill.combat_profile.TargetSelectionModeKind = BattleTargetSelectionMode.MultiUnit;
        skill.combat_profile.min_target_count = 1;
        skill.combat_profile.max_target_count = 2;
        return skill;
    }

    private static SkillDef BuildSkill(StringName skillId, string displayName, int power, int range)
    {
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            target_team_filter = "enemy",
            range_value = range,
            ap_cost = 0,
            mp_cost = 0,
            stamina_cost = 0,
            cooldown_tu = 0,
        };
        combatProfile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "damage",
                effect_target_team_filter = "enemy",
                power = power,
            }
        );
        return new SkillDef
        {
            skill_id = skillId,
            display_name = displayName,
            combat_profile = combatProfile,
        };
    }

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

    private static BattlePreview BuildPreview(BattleUnitState target, AttackPreviewData hitPreview = null)
    {
        var preview = new BattlePreview
        {
            allowed = true,
            hit_preview = hitPreview,
        };
        if (target != null)
        {
            preview.AddTargetUnitId(target.unit_id);
            preview.AddTargetCoord(target.coord);
        }
        return preview;
    }

    private static void InstallSimpleActionScoreInput(BattleAiContext context)
    {
        if (context == null)
        {
            return;
        }
        context.action_score_input_callback = (
            _,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        ) =>
            new BattleAiScoreInput
            {
                action_kind = actionKind,
                action_label = actionLabel,
                score_bucket_id = scoreBucketId,
                command = command,
                move_cost = preview?.move_cost ?? 0,
                total_score = 0,
            };
    }

    private static AttackPreviewData BuildHitPreview(int successRate) =>
        new()
        {
            Stages = new List<AttackPreviewStage>
            {
                new(successRate, successRate, successRate, 0, 0, ""),
            },
            HitRatePercent = successRate,
            SuccessRatePercent = successRate,
            BaseHitRatePercent = successRate,
        };

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

    private sealed class Fixture
    {
        public readonly BattleState State;
        public readonly BattleGridService GridService = new();
        public readonly BattleAiScoreService ScoreService = new();
        private readonly BattleRuntimeModule _runtime = new();
        private readonly Dictionary<StringName, SkillDef> _skillDefs = new();

        public Fixture(string battleId, Vector2I mapSize)
        {
            State = BuildFlatState(battleId, mapSize);
        }

        public void AddSkill(SkillDef skillDef)
        {
            if (skillDef == null || skillDef.skill_id == "")
            {
                return;
            }
            _skillDefs[skillDef.skill_id] = skillDef;
        }

        public void AddUnit(BattleUnitState unit)
        {
            if (unit == null || unit.unit_id == "")
            {
                return;
            }
            State.units[unit.unit_id] = unit;
            if (unit.faction_id == "hostile")
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            bool placed = GridService.PlaceUnit(State, unit, unit.coord, true);
            if (!placed)
            {
                throw new InvalidOperationException($"Failed to place test unit {unit.unit_id} at {unit.coord}.");
            }
        }

        public BattleAiContext BuildContext(BattleUnitState actor)
        {
            _runtime.setup(null, _skillDefs);
            var context = new BattleAiContext
            {
                state = State,
                unit_state = actor,
                grid_service = GridService,
                skill_cast_block_reason_callback = _runtime.GetSkillCastBlockReason,
            };
            context.SetSkillDefs(_skillDefs);
            return context;
        }
    }
}
