using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

// 龙威真实 producer 回归（龙鳞套装提案 §8.5 后半 / §15 阶段 3）。
// 内容接线：red_dragon 模板携带 dragon_frightful_presence，dragon_tyrant brain 的
// engage/pressure 状态各有一条使用该技能的 ground action（自身中心半径 4，与
// range_value=0 + area radius 4 的技能形态一致）；两只幼龙模板不携带该技能。
// AI 门槛：engage action 的 minimum_hit_count=2 真实生效（单目标不产出候选，
// 双目标产出指向自身锚点的龙威命令）。
// 实际施放：正式红龙模板单位经 dragon_tyrant AI ChooseCommand 选出龙威并通过
// canonical preview，IssueCommand 后未防护单位豁免失败进入 frightened（60 TU、
// control save tag 语义）；龙鳞胫甲单件、2 件套阈值与胫甲+2 件套单位免疫龙威，
// fear save tag bonus 分别为 +3/+3/+6（覆盖 frightened 与 dragon_frightful_presence）。
// 普通 fear 来源（execution_fear，save_tag=frightened）不被 2 件套免疫但保留 +3。
public partial class run_dragon_frightful_presence_regression : LifecycleTestSceneTree
{
    private static readonly StringName TemplateId = "red_dragon";
    private static readonly StringName BrainId = "dragon_tyrant";
    private static readonly StringName PresenceSkillId = "dragon_frightful_presence";
    private static readonly StringName FrightenedStatusId = "frightened";
    private static readonly StringName FrightenedTag = "frightened";
    private static readonly StringName PresenceTag = "dragon_frightful_presence";
    private static readonly StringName EngageActionId = "dragon_frightful_presence_sweep";
    private static readonly StringName PressureActionId = "dragon_frightful_presence_point_blank";
    private static readonly StringName GenericFearSkillId = "weapon_axe_executioner_execution_fear";

    private static readonly (StringName ItemId, StringName SlotId)[] Members =
    {
        ("armor_dragon_scale_head", "head"),
        ("armor_dragon_scale_body", "body"),
        ("armor_dragon_scale_hands", "hands"),
        ("armor_dragon_scale_feet", "feet"),
    };

    private readonly TestHarness _test = new();
    private ContentSnapshot _snapshot;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            _snapshot = GameSessionTestFactory.GetProcessSnapshot();
            TestTemplateAndBrainWireFrightfulPresence();
            TestEngageActionAiGateAndCandidate();
            TestRedDragonRealCastFrightfulPresence();
            TestGenericFearNotImmunizedButKeepsBonus();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Dragon frightful presence regression"));
    }

    private void TestTemplateAndBrainWireFrightfulPresence()
    {
        _test.True(
            _snapshot.EnemyTemplates.TryGetValue(TemplateId, out EnemyTemplateDefinition template),
            "正式内容应注册红龙模板。"
        );
        if (template != null)
        {
            _test.True(
                template.SkillIds.Contains(PresenceSkillId),
                "红龙模板 skill_ids 应包含 dragon_frightful_presence。"
            );
            _test.Eq(
                template.GetSkillLevelTyped(PresenceSkillId),
                1,
                "红龙模板的龙威应为 1 级。"
            );
            _test.Eq(template.BrainId, BrainId, "红龙模板应使用 dragon_tyrant brain。");
            _test.True(
                template.SaveImmunityTags.Contains(FrightenedTag),
                "红龙自身应保持 frightened 豁免免疫。"
            );
        }

        foreach (StringName wyrmlingId in new StringName[] { "white_dragon_wyrmling", "green_dragon_wyrmling" })
        {
            if (
                _snapshot.EnemyTemplates.TryGetValue(wyrmlingId, out EnemyTemplateDefinition wyrmling)
            )
            {
                _test.False(
                    wyrmling.SkillIds.Contains(PresenceSkillId),
                    $"{wyrmlingId} 不应获得龙威（内容文档无幼龙龙威依据，仅 10 级红龙持有）。"
                );
            }
        }

        _test.True(
            _snapshot.EnemyBrains.TryGetValue(BrainId, out EnemyAiBrainDefinition brain),
            "正式内容应注册 dragon_tyrant brain。"
        );
        if (brain == null)
            return;
        AssertPresenceAction(brain, "engage", EngageActionId, 2);
        AssertPresenceAction(brain, "pressure", PressureActionId, 1);
    }

    private void AssertPresenceAction(
        EnemyAiBrainDefinition brain,
        StringName stateId,
        StringName actionId,
        int expectedMinimumHitCount
    )
    {
        UseGroundSkillActionDefinition action = FindAction<UseGroundSkillActionDefinition>(
            brain,
            stateId,
            actionId
        );
        _test.True(action != null, $"dragon_tyrant 的 {stateId} 状态应声明 {actionId}。");
        if (action == null)
            return;
        _test.True(
            action.SkillIds.Contains(PresenceSkillId),
            $"{actionId} 应使用 dragon_frightful_presence。"
        );
        _test.Eq(
            action.MinimumHitCount,
            expectedMinimumHitCount,
            $"{actionId} 的 minimum_hit_count 不符。"
        );
        _test.Eq(action.DesiredMinDistance, 0, $"{actionId} 应以自身为中心（desired_min=0）。");
        _test.Eq(action.DesiredMaxDistance, 0, $"{actionId} 应以自身为中心（desired_max=0）。");
        _test.Eq(
            action.DistanceReference,
            new StringName("target_coord"),
            $"{actionId} 的 distance_reference 应为 target_coord。"
        );
    }

    private void TestEngageActionAiGateAndCandidate()
    {
        if (!_snapshot.EnemyBrains.TryGetValue(BrainId, out EnemyAiBrainDefinition brain))
        {
            _test.Fail("正式内容缺少 dragon_tyrant brain。");
            return;
        }
        UseGroundSkillActionDefinition engageAction = FindAction<UseGroundSkillActionDefinition>(
            brain,
            "engage",
            EngageActionId
        );
        _test.True(engageAction != null, "engage 状态应暴露龙威 action。");
        if (engageAction == null)
            return;

        using (RuntimeScope loneScope = RuntimeScope.Create(_snapshot, "frightful_ai_lone", new Vector2I(8, 6)))
        {
            BattleUnitState loneDragon = BuildDragonCaster("lone_dragon", new Vector2I(2, 2));
            BattleUnitState loneEnemy = BuildTargetUnit("lone_target", new Vector2I(4, 2));
            loneScope.AddUnit(loneDragon, isEnemy: true);
            loneScope.AddUnit(loneEnemy, isEnemy: false);
            loneScope.Activate(loneDragon);

            BattleAiContext loneContext = loneScope.BuildAiContext(loneDragon);
            BattleAiDecision loneDecision = null;
            try
            {
                loneDecision = new BattleAiGroundSkillActionEvaluator().Evaluate(
                    engageAction,
                    loneContext
                );
                _test.True(
                    loneDecision == null,
                    "半径 4 内只有一名敌人时，engage 龙威（minimum_hit_count=2）不得产出候选。"
                );
            }
            finally
            {
                loneDecision?.ClearOwnedRuntimeReferences();
                loneContext.ClearRuntimeBindings();
            }
        }

        using RuntimeScope pairScope = RuntimeScope.Create(_snapshot, "frightful_ai_pair", new Vector2I(8, 6));
        BattleUnitState dragon = BuildDragonCaster("pair_dragon", new Vector2I(2, 2));
        BattleUnitState firstEnemy = BuildTargetUnit("pair_target_a", new Vector2I(4, 2));
        BattleUnitState secondEnemy = BuildTargetUnit("pair_target_b", new Vector2I(4, 3));
        pairScope.AddUnit(dragon, isEnemy: true);
        pairScope.AddUnit(firstEnemy, isEnemy: false);
        pairScope.AddUnit(secondEnemy, isEnemy: false);
        pairScope.Activate(dragon);

        BattleAiContext context = pairScope.BuildAiContext(dragon, traceEnabled: true);
        BattleAiDecision decision = null;
        try
        {
            decision = new BattleAiGroundSkillActionEvaluator().Evaluate(engageAction, context);
            _test.Eq(
                decision?.command?.skill_id ?? new StringName(""),
                PresenceSkillId,
                "半径 4 内有两名敌人时，engage 龙威 action 应产出龙威命令。" + DumpTraces(context)
            );
            _test.Eq(
                decision?.command?.target_coord ?? new Vector2I(-1, -1),
                dragon.GetAnchorCoord(),
                "自身中心龙威的目标格应为施放者锚点。"
            );
        }
        finally
        {
            decision?.ClearOwnedRuntimeReferences();
            context.ClearRuntimeBindings();
        }
    }

    private void TestRedDragonRealCastFrightfulPresence()
    {
        using PresenceFixture fixture = PresenceFixture.Build(_snapshot, _test);
        BattleUnitState dragon = fixture.BuildRedDragon("real_cast_dragon", new Vector2I(1, 2));
        _test.True(dragon != null, "正式红龙模板应能构建战斗单位。");
        if (dragon == null)
            return;
        _test.True(
            dragon.KnowsActiveSkill(PresenceSkillId),
            "正式红龙单位应携带 dragon_frightful_presence。"
        );
        _test.Eq(dragon.ai_brain_id, BrainId, "正式红龙单位应使用 dragon_tyrant brain。");

        Dictionary<StringName, BattleUnitState> allies = fixture.BuildSetAllies(
            new Vector2I(5, 2),
            new Vector2I(5, 3),
            new Vector2I(5, 4),
            new Vector2I(5, 5),
            new Vector2I(4, 5)
        );
        BattleUnitState bare = allies["hero_bare"];
        BattleUnitState bareSecond = allies["hero_bare_second"];
        BattleUnitState feet = allies["hero_feet"];
        BattleUnitState twoPiece = allies["hero_two_piece"];
        BattleUnitState feetTwoPiece = allies["hero_feet_two_piece"];

        BattleState state = fixture.SetupBattle(
            "dragon_frightful_presence_real_cast",
            new List<BattleUnitState> { bare, bareSecond, feet, twoPiece, feetTwoPiece },
            new List<BattleUnitState> { dragon }
        );

        AssertSaveBonus(feet, 3, "胫甲单件");
        AssertSaveBonus(twoPiece, 3, "2 件套阈值");
        AssertSaveBonus(feetTwoPiece, 6, "胫甲加 2 件套按 add 叠加");
        _test.True(
            BattleSaveContentRules.IsControlSaveTag(PresenceTag),
            "dragon_frightful_presence 应归类为 control save tag。"
        );
        _test.True(
            BattleSaveContentRules.IsControlSaveTag(FrightenedTag),
            "frightened 应归类为 control save tag。"
        );

        // 只保留龙威可用：其余已知技能全部进入冷却，dragon_tyrant AI 必须选择龙威。
        foreach (StringName skillId in dragon.GetKnownActiveSkillsViewTyped())
        {
            if (skillId != PresenceSkillId)
                dragon.SetCooldownTyped(skillId, 30);
        }
        dragon.ai_state_id = "engage";

        BattleAiDecision decision = null;
        BattlePreview preview = null;
        BattleAiTurnTraceProjection turnTrace = null;
        try
        {
            BattleAiDecisionResult decisionResult = fixture.Runtime._ai_service
                .ChooseCommand(fixture.BuildAiContext(dragon), captureTrace: true);
            decision = decisionResult?.Decision;
            turnTrace = decisionResult?.TurnTrace;
            _test.True(decision?.command != null, "dragon_tyrant AI 应产出正式指令。" + DumpTurnTrace(turnTrace));
            if (decision?.command == null)
                return;
            _test.True(decision.command.IsSkill(), "红龙应选择技能指令而不是移动或等待。" + DumpTurnTrace(turnTrace));
            _test.Eq(
                decision.command.skill_id,
                PresenceSkillId,
                "其余技能不可用且敌群近身时，红龙 AI 应选择 dragon_frightful_presence。" + DumpTurnTrace(turnTrace)
            );

            preview = fixture.Runtime.PreviewCommand(decision.command);
            _test.True(preview?.allowed == true, "龙威 AI 命令必须通过 canonical preview。");
            _test.False(bare.HasStatusEffect(FrightenedStatusId), "preview 不得提前施加 frightened。");

            fixture.Activate(dragon);
            BattleEventBatch batch = fixture.Runtime.IssueCommand(decision.command);
            _test.True(batch != null, "龙威正式执行应返回事件批次。");
            _test.Eq(dragon.GetCurrentAp(), 0, "龙威应消耗 2 AP。");

            BattleStatusEffectState frightened = bare.GetStatusEffect(FrightenedStatusId);
            _test.True(frightened != null, "未防护单位豁免失败应进入 frightened。");
            if (frightened != null)
            {
                _test.Eq(frightened.duration, 60, "龙威 frightened 应持续 60 TU。");
                _test.Eq(
                    frightened.source_unit_id,
                    dragon.unit_id,
                    "frightened 的来源应为施放龙威的红龙。"
                );
            }
            _test.True(
                bareSecond.HasStatusEffect(FrightenedStatusId),
                "第二个未防护单位豁免失败同样应进入 frightened。"
            );
            _test.False(feet.HasStatusEffect(FrightenedStatusId), "龙鳞胫甲单件应免疫龙威。");
            _test.False(twoPiece.HasStatusEffect(FrightenedStatusId), "2 件套阈值应免疫龙威。");
            _test.False(
                feetTwoPiece.HasStatusEffect(FrightenedStatusId),
                "胫甲加 2 件套仍只产生一次布尔免疫。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            DisposeDecision(decision);
        }
    }

    private void TestGenericFearNotImmunizedButKeepsBonus()
    {
        using PresenceFixture fixture = PresenceFixture.Build(_snapshot, _test);
        Dictionary<StringName, BattleUnitState> allies = fixture.BuildSetAllies(
            new Vector2I(5, 2),
            new Vector2I(5, 3),
            new Vector2I(5, 4),
            new Vector2I(5, 5),
            new Vector2I(4, 5)
        );
        CombatEffectDefinition fearEffect = fixture.Skills[GenericFearSkillId]
            .CombatProfile
            .EffectDefinitions[0];
        _test.Eq(
            fearEffect.SaveTag,
            FrightenedTag,
            "对照恐惧来源应使用普通 frightened save tag。"
        );

        foreach (
            (StringName label, int expectedBonus) in new (StringName, int)[]
            {
                ("hero_feet", 3),
                ("hero_two_piece", 3),
                ("hero_feet_two_piece", 6),
            }
        )
        {
            BattleUnitState holder = allies[label];
            BattleSaveResult save = BattleSaveResolver.ResolveSaveResult(
                null,
                holder,
                fearEffect,
                BattleSaveContext.WithSaveRollOverride(1)
            );
            _test.False(save.Immune, $"{label} 不得免疫普通 frightened 来源。");
            _test.Eq(save.Bonus, expectedBonus, $"{label} 对普通恐惧应保留 +{expectedBonus}。");
        }

        BattleSaveResult bareSave = BattleSaveResolver.ResolveSaveResult(
            null,
            allies["hero_bare"],
            fearEffect,
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.False(bareSave.Immune, "无龙鳞单位不应免疫普通恐惧。");
        _test.Eq(bareSave.Bonus, 0, "无龙鳞单位不应有恐惧 save tag 加值。");
    }

    private void AssertSaveBonus(BattleUnitState unit, int expected, string scenario)
    {
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            expected,
            $"{scenario}：frightened tag bonus 应为 +{expected}。"
        );
        _test.Eq(
            unit.GetSaveBonusByTagTyped(PresenceTag),
            expected,
            $"{scenario}：dragon_frightful_presence tag bonus 应为 +{expected}。"
        );
    }

    private T FindAction<T>(EnemyAiBrainDefinition brain, StringName stateId, StringName actionId)
        where T : EnemyAiActionDefinition
    {
        foreach (
            EnemyAiActionDefinition action in
            brain?.GetState(stateId)?.Actions ?? Array.Empty<EnemyAiActionDefinition>()
        )
        {
            if (action?.ActionId == actionId)
                return action as T;
        }
        return null;
    }

    private static string DumpTraces(BattleAiContext context)
    {
        return DumpActionTraces(context?.GetActionTracesTyped());
    }

    private static string DumpTurnTrace(BattleAiTurnTraceProjection turnTrace)
    {
        if (turnTrace == null)
            return " turnTrace=<null>";
        return $" chosen_action={turnTrace.ActionId} reason={turnTrace.ReasonText}"
            + DumpActionTraces(turnTrace.ActionTraces);
    }

    private static string DumpActionTraces(IReadOnlyList<AiActionTrace> traces)
    {
        var parts = new List<string>();
        foreach (AiActionTrace trace in traces ?? Array.Empty<AiActionTrace>())
        {
            var reasons = new List<string>();
            foreach (KeyValuePair<string, int> reason in trace?.BlockReasons ?? new Dictionary<string, int>())
                reasons.Add($"{reason.Key}x{reason.Value}");
            parts.Add(
                $"[{trace?.ActionId} evals={trace?.EvaluationCount} candidates={trace?.CandidateCount} previewRejects={trace?.PreviewRejectCount} gate={trace?.GateRejectionReason} blocks={string.Join(",", reasons)}]"
            );
        }
        return " traces=" + string.Join(" ", parts);
    }

    private static BattleUnitState BuildDragonCaster(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = BuildTargetUnit(unitId, coord);
        unit.faction_id = "hostile";
        unit.control_mode = "ai";
        unit.ai_brain_id = BrainId;
        unit.ai_state_id = "engage";
        unit.ReplaceCreatureTypeTagsTyped(new StringName[] { "dragon", "beast" });
        unit.AddKnownActiveSkill(PresenceSkillId);
        unit.SetKnownSkillLevelTyped(PresenceSkillId, 1);
        return unit;
    }

    private static BattleUnitState BuildTargetUnit(StringName unitId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
        }.WithCombatResourcesForTest(hp: 200, ap: 2, movePoints: 2, isAlive: true);
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            unit.attribute_snapshot.SetValue(attributeId, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 200);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ActionPoints),
            2
        );
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void DisposeDecision(BattleAiDecision decision)
    {
        BattleTestFixture.DisposeBattleCommand(decision?.command);
        BattleTestFixture.DisposeBattleAiScoreInput(decision?.score_input);
        BattleTestFixture.DisposeBattleAiScoreInput(decision?.skill_score_input);
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

        internal BattleAiContext BuildAiContext(BattleUnitState actor, bool traceEnabled = false)
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

        public void Dispose() => BattleTestFixture.DisposeBattleFixture(Runtime, State);
    }

    private sealed class PresenceFixture : IDisposable
    {
        private static readonly (StringName MemberId, int[] MemberIndexes)[] AllyLoadouts =
        {
            ("hero_bare", Array.Empty<int>()),
            ("hero_bare_second", Array.Empty<int>()),
            ("hero_feet", new[] { 3 }),
            ("hero_two_piece", new[] { 0, 1 }),
            ("hero_feet_two_piece", new[] { 0, 3 }),
        };

        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private readonly EncounterRosterBuilder _encounterBuilder;
        private readonly ContentSnapshot _snapshot;
        private readonly TestHarness _test;
        private bool _disposed;

        private PresenceFixture(
            CharacterManagementModule characterManagement,
            PartyState partyState,
            BattleRuntimeModule runtime,
            EncounterRosterBuilder encounterBuilder,
            ContentSnapshot snapshot,
            TestHarness test
        )
        {
            _characterManagement = characterManagement;
            _partyState = partyState;
            Runtime = runtime;
            _encounterBuilder = encounterBuilder;
            _snapshot = snapshot;
            _test = test;
            Skills = snapshot.Skills;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }

        internal static PresenceFixture Build(ContentSnapshot snapshot, TestHarness test)
        {
            CharacterManagementModule characterManagement = null;
            BattleRuntimeModule runtime = null;
            EncounterRosterBuilder encounterBuilder = null;
            try
            {
                PartyState partyState = BuildPartyState();
                characterManagement = new CharacterManagementModule();
                characterManagement.setup(
                    partyState,
                    snapshot.Skills,
                    snapshot.Professions,
                    snapshot.Achievements,
                    snapshot.Items,
                    snapshot.Quests,
                    snapshot.Traits,
                    null,
                    new ProgressionIdentityCatalogData(),
                    snapshot.GearSets
                );
                encounterBuilder = BuildEncounterRosterBuilder(snapshot.EnemyTemplates);
                runtime = new BattleRuntimeModule();
                runtime.setup(
                    characterManagement,
                    snapshot.Skills,
                    snapshot.EnemyTemplates,
                    snapshot.EnemyBrains,
                    encounterBuilder,
                    null,
                    snapshot.Items,
                    battle_special_profile_view: snapshot.BattleSpecialProfiles,
                    trait_defs: snapshot.Traits,
                    equipment_ability_bindings: snapshot.EquipmentAbilityBindings,
                    barrier_profile_definitions: snapshot.BarrierProfiles
                );
                // 固定豁免掷骰为 1：未防护单位必定豁免失败，免疫单位在掷骰前短路。
                BattleTestFixture.ConfigureDamageResolverForTests(
                    runtime,
                    new FixedFailedSaveDamageResolver()
                );
                return new PresenceFixture(
                    characterManagement,
                    partyState,
                    runtime,
                    encounterBuilder,
                    snapshot,
                    test
                );
            }
            catch
            {
                BattleTestFixture.DisposeRuntime(runtime);
                characterManagement?.Dispose();
                encounterBuilder?.Dispose();
                throw;
            }
        }

        internal BattleUnitState BuildRedDragon(StringName label, Vector2I coord)
        {
            IReadOnlyList<BattleUnitState> units = _encounterBuilder.BuildEnemyUnitStatesFromDefinitions(
                new EncounterAnchorData
                {
                    entity_id = label,
                    display_name = "红龙",
                    encounter_profile_id = BuildEncounterProfileId(TemplateId),
                    faction_id = "hostile",
                    world_coord = Vector2I.Zero,
                    region_tag = "default",
                },
                _snapshot.Skills,
                _snapshot.EnemyTemplates,
                _snapshot.EnemyBrains,
                _snapshot.Items,
                _snapshot.Traits,
                _snapshot.EquipmentAbilityBindings,
                generationSeed: 1701
            );
            if (units.Count != 1 || units[0] == null)
                return null;
            BattleUnitState dragon = units[0];
            dragon.SetAnchorCoord(coord);
            return dragon;
        }

        internal Dictionary<StringName, BattleUnitState> BuildSetAllies(
            params Vector2I[] coords
        )
        {
            for (int index = 0; index < AllyLoadouts.Length; index++)
            {
                (StringName memberId, int[] memberIndexes) = AllyLoadouts[index];
                PartyMemberState member = _partyState.GetMemberState(memberId);
                member.equipment_state = new EquipmentState();
                foreach (int memberIndex in memberIndexes)
                {
                    (StringName itemId, StringName slotId) = Members[memberIndex];
                    member.equipment_state.SetEquippedEntry(
                        slotId,
                        itemId,
                        new[] { slotId },
                        EquipmentInstanceState.CreateInstance(
                            itemId,
                            $"eq_presence_{memberId}_{itemId}"
                        )
                    );
                }
            }

            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(
                _partyState,
                null
            );
            var result = new Dictionary<StringName, BattleUnitState>();
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnitState unit = units[index];
                unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
                unit.SetCombatResources(200, mp: 30, stamina: 30, aura: 0, ap: 2, movePoints: 4);
                if (index < coords.Length)
                    unit.SetAnchorCoord(coords[index]);
                result[unit.unit_id] = unit;
            }
            return result;
        }

        internal BattleState SetupBattle(
            StringName battleId,
            IReadOnlyList<BattleUnitState> allies,
            IReadOnlyList<BattleUnitState> enemies
        )
        {
            BattleState state = BattleTestFixture.BuildFlatState(battleId, new Vector2I(10, 8));
            BattleTestFixture.InstallUnits(state, allies, enemies);
            Runtime.SetupStateForTests(state);
            return state;
        }

        internal void Activate(BattleUnitState actor)
        {
            BattleState state = Runtime.GetState();
            state.PhaseKind = BattlePhaseKind.UnitActing;
            state.active_unit_id = actor.unit_id;
        }

        internal BattleAiContext BuildAiContext(BattleUnitState actor)
        {
            Runtime._ensure_ai_action_plan_for_unit(actor);
            Runtime.TryGetAiActionPlanForUnit(
                actor.unit_id,
                out BattleAiRuntimeActionPlan actionPlan
            );
            var context = new BattleAiContext
            {
                state = Runtime.GetState(),
                unit_state = actor,
                grid_service = Runtime._grid_service,
                runtime_action_plan = actionPlan,
                trace_enabled = true,
            };
            context.SetSkillDefinitions(Runtime.GetSkillDefinitionIndexTyped());
            Runtime._bind_ai_helper_services_for_decision(actor, context);
            return context;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
            _characterManagement?.Dispose();
            _encounterBuilder?.Dispose();
        }

        private static PartyState BuildPartyState()
        {
            var party = new PartyState();
            foreach ((StringName memberId, _) in AllyLoadouts)
            {
                var member = new PartyMemberState
                {
                    member_id = memberId,
                    display_name = memberId.ToString(),
                    progression = new UnitProgress
                    {
                        unit_id = memberId,
                        display_name = memberId.ToString(),
                    },
                    equipment_state = new EquipmentState(),
                };
                party.SetMemberState(member);
                party.active_member_ids.Add(memberId);
            }
            party.leader_member_id = AllyLoadouts[0].MemberId;
            return party;
        }

        private static EncounterRosterBuilder BuildEncounterRosterBuilder(
            IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates
        )
        {
            var encounters = new Dictionary<StringName, BattleEncounterDefinition>();
            var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>();
            foreach (
                (StringName templateId, EnemyTemplateDefinition template) in
                enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDefinition>()
            )
            {
                if (templateId == "" || template == null)
                    continue;
                StringName rosterProfileId = BuildRosterProfileId(templateId);
                StringName encounterProfileId = BuildEncounterProfileId(templateId);
                rosters[rosterProfileId] = new WildEncounterRosterDefinition(
                    rosterProfileId,
                    template.DisplayName,
                    0,
                    0,
                    new[]
                    {
                        new WildEncounterRosterStageDefinition(
                            0,
                            new[]
                            {
                                new WildEncounterRosterUnitEntryDefinition(
                                    templateId,
                                    Mathf.Max(template.EnemyCount, 1),
                                    template.DisplayName
                                ),
                            }
                        ),
                    }
                );
                encounters[encounterProfileId] = new BattleEncounterDefinition(
                    encounterProfileId,
                    template.DisplayName,
                    rosterProfileId,
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                );
            }

            var builder = new EncounterRosterBuilder();
            builder.Setup(encounters, rosters, enemyTemplates);
            return builder;
        }

        private static StringName BuildEncounterProfileId(StringName templateId) =>
            new($"test_frightful_presence_encounter_{templateId}");

        private static StringName BuildRosterProfileId(StringName templateId) =>
            new($"test_frightful_presence_roster_{templateId}");
    }
}
