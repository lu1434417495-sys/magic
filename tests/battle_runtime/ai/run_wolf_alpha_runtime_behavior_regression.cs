using System;
using System.Collections.Generic;
using Godot;

public partial class run_wolf_alpha_runtime_behavior_regression : LifecycleTestSceneTree
{
    private static readonly StringName BrainId = "pack_leader";
    private static readonly StringName StateId = "hunt";
    private static readonly StringName HowlSkillId = "wolf_alpha_dominance_howl";
    private static readonly StringName CullSkillId = "wolf_alpha_cull_the_weak";
    private static readonly StringName AttackBuffStatusId = "attack_roll_bonus_up";

    private readonly TestHarness _test = new();
    private ContentSnapshot _snapshot;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            _snapshot = GameSessionTestFactory.GetProcessSnapshot();
            TestHowlPreviewAndExecutionFilterWolfAllies();
            TestHowlAiRequiresTwoWolfTargets();
            TestCullAiSelectorChoosesLowestHpRatio();
            TestAlphaWithAdjacentEnemyChoosesOffensiveSkill();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Wolf alpha runtime behavior regression"));
    }

    private void TestHowlPreviewAndExecutionFilterWolfAllies()
    {
        using RuntimeScope scope = RuntimeScope.Create(
            _snapshot,
            "wolf_alpha_howl_runtime",
            new Vector2I(6, 6)
        );
        BattleUnitState alpha = BuildWolfAlpha(
            "howl_alpha",
            new Vector2I(2, 2),
            HowlSkillId
        );
        BattleUnitState wolf = BuildUnit(
            "howl_wolf",
            "hostile",
            new Vector2I(3, 2),
            20,
            20
        );
        wolf.AddCreatureTypeTagTyped("wolf");
        BattleUnitState nonWolf = BuildUnit(
            "howl_non_wolf",
            "hostile",
            new Vector2I(2, 3),
            20,
            20
        );
        nonWolf.AddCreatureTypeTagTyped("humanoid");
        BattleUnitState opposingUnit = BuildUnit(
            "howl_opponent",
            "player",
            new Vector2I(5, 5),
            20,
            20
        );
        scope.AddUnit(alpha, isEnemy: true);
        scope.AddUnit(wolf, isEnemy: true);
        scope.AddUnit(nonWolf, isEnemy: true);
        scope.AddUnit(opposingUnit, isEnemy: false);
        scope.Activate(alpha);

        BattleCommand command = BuildGroundSkillCommand(
            alpha,
            HowlSkillId,
            alpha.GetAnchorCoord()
        );
        BattlePreview preview = null;
        BattleEventBatch batch = null;
        try
        {
            preview = scope.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "统御长嚎正式preview应允许以狼王自身为中心施放。");
            _test.True(
                preview?.ContainsTargetUnitId(alpha.unit_id) == true,
                "统御长嚎preview应包含具有wolf标签的狼王自身。"
            );
            _test.True(
                preview?.ContainsTargetUnitId(wolf.unit_id) == true,
                "统御长嚎preview应包含范围内同阵营狼类。"
            );
            _test.False(
                preview?.ContainsTargetUnitId(nonWolf.unit_id) == true,
                "统御长嚎preview不得包含范围内同阵营非狼单位。"
            );
            _test.False(alpha.HasStatusEffect(AttackBuffStatusId), "preview不得提前强化狼王。");
            _test.False(wolf.HasStatusEffect(AttackBuffStatusId), "preview不得提前强化友军狼。");
            _test.Eq(alpha.GetCurrentAp(), 2, "preview不得消耗狼王AP。");
            _test.Eq(alpha.GetCurrentStamina(), 100, "preview不得消耗狼王体力。");

            batch = scope.Runtime.IssueCommand(command);

            _test.True(batch != null, "统御长嚎正式执行应返回事件批次。");
            _test.True(alpha.HasStatusEffect(AttackBuffStatusId), "正式执行后狼王自身应获得攻击检定强化。");
            _test.True(wolf.HasStatusEffect(AttackBuffStatusId), "正式执行后友军狼应获得攻击检定强化。");
            _test.False(
                nonWolf.HasStatusEffect(AttackBuffStatusId),
                "正式执行后同阵营非狼单位仍不得获得强化。"
            );
            _test.Eq(
                wolf.GetStatusEffect(AttackBuffStatusId)?.power ?? -1,
                1,
                "1级统御长嚎应为友军狼提供+1攻击检定。"
            );
            _test.Eq(alpha.GetCurrentAp(), 1, "统御长嚎正式执行应消耗1AP。");
            _test.Eq(alpha.GetCurrentStamina(), 80, "统御长嚎正式执行应消耗20体力。");
        }
        finally
        {
            batch?.Dispose();
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestHowlAiRequiresTwoWolfTargets()
    {
        UseGroundSkillActionDefinition howlAction = FindAction<UseGroundSkillActionDefinition>(
            "pack_leader_howl"
        );
        _test.True(howlAction != null, "正式pack_leader brain应暴露统御长嚎动作。");
        if (howlAction == null)
            return;

        using (
            RuntimeScope loneScope = RuntimeScope.Create(
                _snapshot,
                "wolf_alpha_howl_ai_lone",
                new Vector2I(6, 6)
            )
        )
        {
            BattleUnitState loneAlpha = BuildWolfAlpha(
                "lone_howl_alpha",
                new Vector2I(2, 2),
                HowlSkillId
            );
            BattleUnitState nonWolf = BuildUnit(
                "lone_howl_non_wolf",
                "hostile",
                new Vector2I(3, 2),
                20,
                20
            );
            nonWolf.AddCreatureTypeTagTyped("humanoid");
            loneScope.AddUnit(loneAlpha, isEnemy: true);
            loneScope.AddUnit(nonWolf, isEnemy: true);
            loneScope.Activate(loneAlpha);

            BattleAiContext context = loneScope.BuildAiContext(loneAlpha, traceEnabled: true);
            BattleAiDecision decision = null;
            try
            {
                decision = new BattleAiGroundSkillActionEvaluator().Evaluate(
                    howlAction,
                    context
                );
                _test.True(
                    decision == null,
                    "只有狼王自身一个有效狼类目标时，AI不得使用统御长嚎。"
                );
            }
            finally
            {
                decision?.ClearOwnedRuntimeReferences();
                context.ClearRuntimeBindings();
            }
        }

        using RuntimeScope packScope = RuntimeScope.Create(
            _snapshot,
            "wolf_alpha_howl_ai_pack",
            new Vector2I(6, 6)
        );
        BattleUnitState alpha = BuildWolfAlpha(
            "pack_howl_alpha",
            new Vector2I(2, 2),
            HowlSkillId
        );
        BattleUnitState packmate = BuildUnit(
            "pack_howl_wolf",
            "hostile",
            new Vector2I(3, 2),
            20,
            20
        );
        packmate.AddCreatureTypeTagTyped("wolf");
        packScope.AddUnit(alpha, isEnemy: true);
        packScope.AddUnit(packmate, isEnemy: true);
        packScope.Activate(alpha);

        BattleAiContext packContext = packScope.BuildAiContext(alpha, traceEnabled: true);
        BattleAiDecision packDecision = null;
        try
        {
            packDecision = new BattleAiGroundSkillActionEvaluator().Evaluate(
                howlAction,
                packContext
            );
            _test.Eq(
                packDecision?.command?.skill_id ?? new StringName(""),
                HowlSkillId,
                "狼王与一只友军狼形成两个有效目标时，AI应产生统御长嚎候选。"
            );
        }
        finally
        {
            packDecision?.ClearOwnedRuntimeReferences();
            packContext.ClearRuntimeBindings();
        }
    }

    private void TestCullAiSelectorChoosesLowestHpRatio()
    {
        UseUnitSkillActionDefinition cullAction = FindAction<UseUnitSkillActionDefinition>(
            "pack_leader_cull"
        );
        _test.True(cullAction != null, "正式pack_leader brain应暴露弱者扑杀动作。");
        if (cullAction == null)
            return;

        using RuntimeScope scope = RuntimeScope.Create(
            _snapshot,
            "wolf_alpha_cull_ai",
            new Vector2I(6, 6)
        );
        BattleUnitState alpha = BuildWolfAlpha(
            "cull_alpha",
            new Vector2I(2, 2),
            CullSkillId
        );
        BattleUnitState lowerRatioTarget = BuildUnit(
            "cull_lower_ratio_target",
            "player",
            new Vector2I(3, 2),
            100,
            20
        );
        BattleUnitState lowerAbsoluteHpTarget = BuildUnit(
            "cull_lower_absolute_hp_target",
            "player",
            new Vector2I(2, 3),
            20,
            8
        );
        scope.AddUnit(alpha, isEnemy: true);
        scope.AddUnit(lowerAbsoluteHpTarget, isEnemy: false);
        scope.AddUnit(lowerRatioTarget, isEnemy: false);
        scope.Activate(alpha);

        BattleAiContext context = scope.BuildAiContext(alpha, traceEnabled: true);
        context.skill_score_input_callback = null;
        BattleAiDecision decision = null;
        try
        {
            _test.True(
                lowerRatioTarget.GetCurrentHp() > lowerAbsoluteHpTarget.GetCurrentHp(),
                "扑杀selector夹具必须让生命比例排序与当前生命绝对值排序相反。"
            );
            decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
                cullAction,
                context
            );
            _test.Eq(
                decision?.command?.skill_id ?? new StringName(""),
                CullSkillId,
                "弱者扑杀AI应生成正式技能命令。"
            );
            _test.Eq(
                decision?.command?.target_unit_id ?? new StringName(""),
                lowerRatioTarget.unit_id,
                "关闭技能评分回调后，弱者扑杀应仅按lowest_hp_enemy选择生命比例最低的敌人。"
            );
        }
        finally
        {
            decision?.ClearOwnedRuntimeReferences();
            context.ClearRuntimeBindings();
        }
    }

    private void TestAlphaWithAdjacentEnemyChoosesOffensiveSkill()
    {
        using RuntimeScope scope = RuntimeScope.Create(
            _snapshot,
            "wolf_alpha_adjacent_enemy_offense",
            new Vector2I(6, 6)
        );
        BattleUnitState alpha = BuildWolfAlpha(
            "adjacent_enemy_alpha",
            new Vector2I(2, 2),
            "basic_attack",
            "charge",
            "wolf_alpha_hamstring_bite",
            HowlSkillId,
            CullSkillId
        );
        BattleUnitState target = BuildUnit(
            "adjacent_enemy_target",
            "player",
            new Vector2I(3, 2),
            20,
            8
        );
        scope.AddUnit(alpha, isEnemy: true);
        scope.AddUnit(target, isEnemy: false);
        scope.Activate(alpha);

        BattleAiContext context = scope.BuildAiContext(alpha, traceEnabled: true);
        BattleAiDecision decision = null;
        try
        {
            decision = scope.Runtime._ai_service
                .ChooseCommand(context, captureTrace: true)
                ?.Decision;
            _test.True(
                decision?.command != null,
                "相邻敌人且进攻技能合法时，荒狼头目应产生合法AI指令。"
            );
            _test.Eq(
                decision?.state_id ?? new StringName(""),
                StateId,
                "相邻敌人存在时，荒狼头目应保持hunt状态。"
            );
            EnemyAiActionDefinition selectedAction = FindAction(
                decision?.action_id ?? new StringName("")
            );
            _test.True(
                selectedAction
                    is UseUnitSkillActionDefinition
                        or UseGroundSkillActionDefinition
                        or UseChargeActionDefinition,
                $"相邻敌人且进攻技能合法时，荒狼头目应选择正式技能动作，不能待机、fallback或仅移动。 actual={decision?.action_id}"
            );
            _test.True(
                decision?.command?.IsSkill() == true,
                "相邻敌人且进攻技能合法时，荒狼头目的正式决策应提交技能命令。"
            );
        }
        finally
        {
            decision?.ClearOwnedRuntimeReferences();
        }
    }

    private EnemyAiActionDefinition FindAction(StringName actionId)
    {
        if (
            !_snapshot.EnemyBrains.TryGetValue(
                BrainId,
                out EnemyAiBrainDefinition brain
            )
        )
        {
            return null;
        }
        foreach (
            EnemyAiActionDefinition action in
            brain.GetState(StateId)?.Actions ?? Array.Empty<EnemyAiActionDefinition>()
        )
        {
            if (action?.ActionId == actionId)
                return action;
        }
        return null;
    }

    private T FindAction<T>(StringName actionId)
        where T : EnemyAiActionDefinition
    {
        return FindAction(actionId) as T;
    }

    private static BattleUnitState BuildWolfAlpha(
        StringName unitId,
        Vector2I coord,
        params StringName[] skillIds
    )
    {
        BattleUnitState unit = BuildUnit(unitId, "hostile", coord, 50, 50);
        unit.control_mode = "ai";
        unit.ai_brain_id = BrainId;
        unit.ai_state_id = StateId;
        unit.AddCreatureTypeTagTyped("wolf");
        unit.AddCreatureTypeTagTyped("beast");
        unit.AddCreatureTypeTagTyped("bite");
        foreach (StringName skillId in skillIds ?? Array.Empty<StringName>())
        {
            unit.AddKnownActiveSkill(skillId);
            unit.SetKnownSkillLevelTyped(skillId, 1);
        }
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "natural",
                weapon_profile_type_id = "wolf_alpha_bite",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 3,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        return unit;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int maxHp,
        int currentHp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: currentHp,
            stamina: 100,
            ap: 2,
            movePoints: 2,
            isAlive: true
        );
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            unit.attribute_snapshot.SetValue(attributeId, 10);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.HpMax),
            maxHp
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.StaminaMax),
            100
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ActionPoints),
            2
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AttackBonus),
            8
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            10
        );
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleCommand BuildGroundSkillCommand(
        BattleUnitState caster,
        StringName skillId,
        Vector2I targetCoord
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private sealed class RuntimeScope : IDisposable
    {
        private RuntimeScope(BattleRuntimeModule runtime, BattleState state)
        {
            Runtime = runtime;
            State = state;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }

        internal static RuntimeScope Create(
            ContentSnapshot snapshot,
            StringName battleId,
            Vector2I mapSize
        )
        {
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                skill_definitions: snapshot.Skills,
                enemy_templates: snapshot.EnemyTemplates,
                enemy_ai_brains: snapshot.EnemyBrains,
                item_defs: snapshot.Items,
                battle_special_profile_view: snapshot.BattleSpecialProfiles,
                trait_defs: snapshot.Traits,
                equipment_ability_bindings: snapshot.EquipmentAbilityBindings,
                barrier_profile_definitions: snapshot.BarrierProfiles
            );
            return new RuntimeScope(
                runtime,
                BattleTestFixture.BuildFlatState(battleId, mapSize)
            );
        }

        internal void AddUnit(BattleUnitState unit, bool isEnemy)
        {
            State.SetUnit(unit);
            if (isEnemy)
                State.enemy_unit_ids.Add(unit.unit_id);
            else
                State.ally_unit_ids.Add(unit.unit_id);
            if (!Runtime._grid_service.PlaceUnit(State, unit, unit.GetAnchorCoord(), true))
            {
                throw new InvalidOperationException(
                    $"Failed to place {unit.unit_id} at {unit.GetAnchorCoord()}."
                );
            }
        }

        internal void Activate(BattleUnitState actor)
        {
            State.phase = "unit_acting";
            State.active_unit_id = actor.unit_id;
            Runtime.SetupStateForTests(State);
        }

        internal BattleAiContext BuildAiContext(
            BattleUnitState actor,
            bool traceEnabled
        )
        {
            Runtime._ensure_ai_action_plan_for_unit(actor);
            Runtime.TryGetAiActionPlanForUnit(
                actor.unit_id,
                out BattleAiRuntimeActionPlan actionPlan
            );
            var context = new BattleAiContext
            {
                state = State,
                unit_state = actor,
                grid_service = Runtime._grid_service,
                runtime_action_plan = actionPlan,
                trace_enabled = traceEnabled,
            };
            context.SetSkillDefinitions(Runtime.GetSkillDefinitionIndexTyped());
            Runtime._bind_ai_helper_services_for_decision(actor, context);
            return context;
        }

        public void Dispose() =>
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
    }
}
