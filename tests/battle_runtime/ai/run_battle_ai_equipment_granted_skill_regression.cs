using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_equipment_granted_skill_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "fixture_equipment_granted_skill";
    private static readonly StringName BindingId = "binding.fixture.ai_equipment_skill";
    private static readonly StringName GrantId = "grant.fixture.ai_equipment_skill";
    private static readonly StringName StateId = "engage";
    private static readonly StringName AshSkillId =
        "equipment_phoenix_rebirth_ash_ring_fire";
    private static readonly StringName AshBindingId =
        "binding.phoenix_rebirth.ash_ring.fire";
    private static readonly StringName SolarSkillId =
        "gear_set_phoenix_rebirth_solar_rebirth";
    private static readonly StringName SolarBindingId =
        "binding.gear_set.phoenix_rebirth.10.solar_rebirth";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestPlanIncludesEquipmentSkillAndTracksEquipmentShape();
            TestQueryUsesEquipmentGrantedSkillLevel();
            TestDecisionUsesAvailabilityAndCanonicalPreview();
            TestFormalPhoenixMixedSkillsGenerateCanonicalCommandsWithoutMutation();
            RequestTestExit(_test.Finish("Battle AI equipment-granted skill regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(
                _test.Finish("Battle AI equipment-granted skill regression", 1)
            );
        }
    }

    private void TestQueryUsesEquipmentGrantedSkillLevel()
    {
        Fixture fixture = BuildFixture();
        fixture.Actor.SetCurrentHp(50);
        var query = new BattleAiQueryService();
        try
        {
            query.SetupReadOnly(
                fixture.State,
                fixture.Grid,
                fixture.Actor.unit_id,
                fixture.SkillDefinitions,
                skillCatalog: null,
                fixture.Bindings,
                fixture.ItemDefinitions
            );
            _test.True(
                query.TryGetSkillRecordTyped(
                    SkillId,
                    out BattleAiQueryService.SkillRecord record
                ),
                "AI 查询应能解析装备授予技能。"
            );
            _test.Eq(
                record?.range_value ?? -1,
                5,
                "AI 查询应使用装备 grant 的技能等级计算有效数值。"
            );
        }
        finally
        {
            query.ClearRuntimeBindings();
        }
    }

    private void TestPlanIncludesEquipmentSkillAndTracksEquipmentShape()
    {
        Fixture fixture = BuildFixture();
        using BattleAiRuntimeActionPlan plan = new BattleAiActionAssembler().BuildUnitActionPlan(
            fixture.Actor,
            fixture.Brain,
            fixture.SkillDefinitions,
            skillCatalog: null,
            fixture.Bindings,
            fixture.ItemDefinitions,
            fixture.State,
            worldStep: 0
        );

        _test.True(
            ContainsGeneratedSkill(plan.GetActionEntries(StateId), SkillId),
            "动作计划应纳入装备授予的主动技能，即使当前 HP 门槛尚未满足。"
        );

        fixture.Actor.SetCurrentHp(50);
        _test.False(
            plan.IsStaleFor(
                fixture.Actor,
                fixture.Brain,
                skillCatalog: null,
                fixture.SkillDefinitions,
                fixture.Bindings,
                fixture.ItemDefinitions,
                fixture.State,
                worldStep: 0
            ),
            "HP/次数等动态可用性变化不应改变动作计划形状。"
        );

        fixture.Actor.ClearEquipmentAbilityProjectionTyped();
        _test.True(
            plan.IsStaleFor(
                fixture.Actor,
                fixture.Brain,
                skillCatalog: null,
                fixture.SkillDefinitions,
                fixture.Bindings,
                fixture.ItemDefinitions,
                fixture.State,
                worldStep: 0
            ),
            "移除装备技能来源后，装备入口指纹应使旧动作计划失效。"
        );
    }

    private void TestDecisionUsesAvailabilityAndCanonicalPreview()
    {
        Fixture fixture = BuildFixture();
        UseUnitSkillActionDefinition action = new(
            "fixture_equipment_skill_action",
            "fixture",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            4,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );
        var evaluator = new BattleAiUnitSkillCandidateEvaluator();
        var context = new BattleAiContext();
        int previewCallCount = 0;
        bool canonicalPreviewAllowed = true;

        void ResetContext()
        {
            context.ResetForDecision(
                fixture.State,
                fixture.Actor,
                fixture.Grid,
                actionPlan: null,
                fixture.SkillDefinitions,
                traceEnabled: true,
                skillCatalog: null,
                barrierProfileDefinitions: null,
                fixture.Bindings,
                fixture.ItemDefinitions
            );
            context.skill_cast_block_reason_callback = (_, _) =>
                BattleSkillCastBlockReasonKind.None;
            context.preview_command_callback = command =>
            {
                previewCallCount += 1;
                var preview = new BattlePreview { allowed = canonicalPreviewAllowed };
                if (canonicalPreviewAllowed)
                {
                    preview.AddTargetUnitId(fixture.Target.unit_id);
                    preview.AddTargetCoord(fixture.Target.GetAnchorCoord());
                    preview.resolved_anchor_coord = fixture.Target.GetAnchorCoord();
                }
                return preview;
            };
        }

        ResetContext();
        BattleAiDecision fullHpDecision = evaluator.Evaluate(action, context);
        _test.True(fullHpDecision == null, "HP 高于门槛时，AI 不应提出装备技能候选。"
        );
        _test.Eq(
            previewCallCount,
            0,
            "HP 门槛应先由统一 availability 拦截，不应进入预览。"
        );

        fixture.Actor.SetCurrentHp(50);
        canonicalPreviewAllowed = false;
        ResetContext();
        BattleAiDecision previewRejectedDecision = evaluator.Evaluate(action, context);
        _test.True(
            previewRejectedDecision == null,
            "正式预览拒绝时，即使装备技能入口可选，AI 也不得选择该技能。"
        );
        _test.True(
            previewCallCount > 0,
            "装备授予技能必须调用正式预览，不能只依赖快速预览。"
        );

        canonicalPreviewAllowed = true;
        ResetContext();
        BattleAiDecision readyDecision = evaluator.Evaluate(action, context);
        _test.True(readyDecision?.command != null, "满足 HP 门槛且预览允许时，AI 应看到装备技能。"
        );
        _test.Eq(
            readyDecision?.command?.skill_id ?? new StringName(""),
            SkillId,
            "AI 命令应携带装备授予技能 id。"
        );
        _test.True(
            !BattleRuntimeModule.IsEmpty(readyDecision?.command?.skill_entry_id ?? ""),
            "AI 命令必须保留具体装备技能入口 id。"
        );

        BattleAvailableSkillEntry readyEntry = FindEquipmentSkillEntry(fixture);
        _test.True(
            EquipmentAbilityUsageRuntime.TryCommitUsage(fixture.Actor, readyEntry, worldStep: 0),
            "fixture 装备技能首次使用应能提交统一 usage。"
        );
        fixture.Actor.ResetPerTurnCharges();
        int previewCallsBeforeExhaustedCheck = previewCallCount;
        ResetContext();
        BattleAiDecision exhaustedDecision = evaluator.Evaluate(action, context);
        _test.True(exhaustedDecision == null, "装备技能次数耗尽后，AI 不应再次提出候选。"
        );
        _test.Eq(
            previewCallCount,
            previewCallsBeforeExhaustedCheck,
            "次数门槛应由统一 availability 在预览前拦截。"
        );
    }

    private void TestFormalPhoenixMixedSkillsGenerateCanonicalCommandsWithoutMutation()
    {
        using FormalPhoenixAiFixture fixture = FormalPhoenixAiFixture.Build();
        using BattleAiRuntimeActionPlan plan = new BattleAiActionAssembler().BuildUnitActionPlan(
            fixture.Actor,
            fixture.Brain,
            fixture.Snapshot.Skills,
            skillCatalog: null,
            fixture.Snapshot.EquipmentAbilityBindings,
            fixture.Snapshot.Items,
            fixture.State,
            worldStep: 0
        );

        AssertFormalMixedAffordances(
            plan,
            AshSkillId,
            "unit_hostile.damage",
            "ally_heal",
            "灰烬之火"
        );
        AssertFormalMixedAffordances(
            plan,
            SolarSkillId,
            "ground_hostile.aoe",
            "ally_heal",
            "太阳涅槃"
        );

        BattleAiRuntimeActionEntry ashEntry = FindGeneratedEntry<UseUnitSkillActionDefinition>(
            plan.GetActionEntries(StateId),
            AshSkillId
        );
        BattleAiRuntimeActionEntry solarEntry = FindGeneratedEntry<UseGroundSkillActionDefinition>(
            plan.GetActionEntries(StateId),
            SolarSkillId
        );
        _test.True(ashEntry != null, "灰烬之火应从正式装备 grant 生成敌方 unit action。");
        _test.True(solarEntry != null, "太阳涅槃应从正式套装 grant 生成 ground action。");
        if (ashEntry == null || solarEntry == null)
            return;

        var context = new BattleAiContext();
        BattleAiDecision ashDecision = null;
        BattleAiDecision solarDecision = null;
        BattlePreview ashPreview = null;
        BattlePreview solarPreview = null;
        try
        {
            context.ResetForDecision(
                fixture.State,
                fixture.Actor,
                fixture.Runtime.GetGridService(),
                plan,
                fixture.Snapshot.Skills,
                traceEnabled: true,
                skillCatalog: null,
                barrierProfileDefinitions: fixture.Snapshot.BarrierProfiles,
                equipmentAbilityBindings: fixture.Snapshot.EquipmentAbilityBindings,
                itemDefinitions: fixture.Snapshot.Items
            );
            fixture.Runtime._bind_ai_helper_services_for_decision(fixture.Actor, context);
            int canonicalPreviewCallCount = 0;
            context.preview_command_callback = command =>
            {
                canonicalPreviewCallCount += 1;
                return fixture.Runtime.PreviewCommand(command);
            };

            BattleAiMutationSnapshot before = BattleAiMutationSnapshot.Capture(context);
            var decisionEngine = new BattleAiDecisionEngine();
            var ashAction = (UseUnitSkillActionDefinition)ashEntry.Action;
            var solarAction = (UseGroundSkillActionDefinition)solarEntry.Action;
            _test.True(
                ashAction.DesiredMinDistance >= 0
                    && ashAction.DesiredMaxDistance >= ashAction.DesiredMinDistance
                    && ashAction.DistanceReferenceKind
                        is EnemyAiDistanceReference.TargetUnit
                            or EnemyAiDistanceReference.EnemyFrontline,
                $"灰烬之火生成 action 应带合法距离合同，实际为 {ashAction.DesiredMinDistance}-{ashAction.DesiredMaxDistance}/{ashAction.DistanceReference}。"
            );
            _test.True(
                solarAction.DesiredMinDistance >= 0
                    && solarAction.DesiredMaxDistance >= solarAction.DesiredMinDistance
                    && solarAction.DistanceReferenceKind
                        is EnemyAiDistanceReference.TargetCoord
                            or EnemyAiDistanceReference.EnemyFrontline,
                $"太阳涅槃生成 action 应带合法距离合同，实际为 {solarAction.DesiredMinDistance}-{solarAction.DesiredMaxDistance}/{solarAction.DistanceReference}。"
            );
            List<BattleAvailableSkillEntry> availableAsh = new BattleAiTypedActionHelper()
                .ResolveAvailableSkillEntries(context, ashAction.SkillIds);
            List<BattleAvailableSkillEntry> availableSolar = new BattleAiTypedActionHelper()
                .ResolveAvailableSkillEntries(context, solarAction.SkillIds);
            _test.Eq(availableAsh.Count, 1, "灰烬之火在 evaluator 前应仍是可选装备技能。");
            _test.Eq(availableSolar.Count, 1, "太阳涅槃在 evaluator 前应仍是可选套装技能。");
            ashDecision = decisionEngine.EvaluateEntry(context, ashEntry);
            solarDecision = decisionEngine.EvaluateEntry(context, solarEntry);

            _test.True(
                ashDecision?.command != null,
                $"灰烬之火应生成合法敌方单位命令。{FormatActionTraces(context)}"
            );
            _test.Eq(
                ashDecision?.command?.skill_id ?? new StringName(""),
                AshSkillId,
                "灰烬之火 AI 命令应保留正式技能 id。"
            );
            _test.Eq(
                ashDecision?.command?.target_unit_id ?? new StringName(""),
                fixture.Enemy.unit_id,
                "灰烬之火敌对候选应选择合法敌方单位，而不是被治疗分支改成友军目标。"
            );
            _test.True(
                !BattleRuntimeModule.IsEmpty(ashDecision?.command?.skill_entry_id ?? ""),
                "灰烬之火 AI 命令应保留正式装备 grant entry。"
            );

            _test.True(
                solarDecision?.command != null,
                $"太阳涅槃应生成合法 ground 命令。{FormatActionTraces(context)}"
            );
            _test.Eq(
                solarDecision?.command?.skill_id ?? new StringName(""),
                SolarSkillId,
                "太阳涅槃 AI 命令应保留正式技能 id。"
            );
            _test.True(
                solarDecision?.command?.TargetCoordsTyped.Contains(fixture.Actor.GetAnchorCoord())
                    == true,
                "太阳涅槃 range 0 ground 命令应锚定施术者所在格。"
            );
            _test.True(
                !BattleRuntimeModule.IsEmpty(solarDecision?.command?.skill_entry_id ?? ""),
                "太阳涅槃 AI 命令应保留正式套装 grant entry。"
            );

            ashPreview = fixture.Runtime.PreviewCommand(ashDecision?.command);
            solarPreview = fixture.Runtime.PreviewCommand(solarDecision?.command);
            _test.True(ashPreview?.allowed == true, "灰烬之火 AI 命令必须通过 runtime canonical preview。");
            _test.True(solarPreview?.allowed == true, "太阳涅槃 AI 命令必须通过 runtime canonical preview。");
            _test.True(
                canonicalPreviewCallCount >= 2,
                "两项装备授予技能的 AI evaluator 都必须调用 canonical preview。"
            );
            _test.True(
                before.MatchesCurrentState(context),
                $"凤凰装备技能 AI preview 不应污染状态：{string.Join(" | ", before.CompareCurrentState(context))}"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(ashPreview);
            BattleTestFixture.DisposeBattlePreview(solarPreview);
            ashDecision?.ClearOwnedRuntimeReferences();
            solarDecision?.ClearOwnedRuntimeReferences();
            context.ClearRuntimeBindings();
            fixture.Runtime._runtime_services.ClearRuntimeBindings();
        }
    }

    private void AssertFormalMixedAffordances(
        BattleAiRuntimeActionPlan plan,
        StringName skillId,
        StringName hostileAffordance,
        StringName supportAffordance,
        string label
    )
    {
        _test.True(
            plan.TryGetSkillAffordanceRecordTyped(
                skillId,
                out BattleAiSkillAffordanceRecord record
            ),
            $"{label} 应进入正式 AI affordance plan。"
        );
        if (record == null)
            return;
        _test.True(record.is_generatable, $"{label} 应标为 is_generatable。");
        _test.Eq(record.team_intent, new StringName("mixed"), $"{label} 应保留 mixed team intent。");
        _test.True(record.affordances.Contains(hostileAffordance), $"{label} 应保留 {hostileAffordance}。");
        _test.True(record.affordances.Contains(supportAffordance), $"{label} 应保留 {supportAffordance}。");
    }

    private static BattleAiRuntimeActionEntry FindGeneratedEntry<TAction>(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
        where TAction : EnemyAiActionDefinition
    {
        foreach (
            BattleAiRuntimeActionEntry entry in
                entries ?? Array.Empty<BattleAiRuntimeActionEntry>()
        )
        {
            if (
                entry?.Metadata.generated == true
                && entry.Metadata.skill_id == skillId
                && entry.Action is TAction
            )
            {
                return entry;
            }
        }
        return null;
    }

    private static string FormatActionTraces(BattleAiContext context)
    {
        var summaries = new List<string>();
        foreach (AiActionTrace trace in context?.GetActionTracesTyped() ?? Array.Empty<AiActionTrace>())
        {
            summaries.Add(
                $" action={trace.ActionId}, evaluations={trace.EvaluationCount}, previews_rejected={trace.PreviewRejectCount}, candidates={trace.CandidateCount}, blocks=[{string.Join(",", trace.BlockReasons.Select(entry => $"{entry.Key}:{entry.Value}"))}], counters=[{string.Join(",", trace.CandidateTraceCounters.Select(entry => $"{entry.Key}:{entry.Value}"))}]"
            );
        }
        return string.Join(";", summaries);
    }

    private static Fixture BuildFixture()
    {
        BattleUnitState actor = BuildUnit("fixture_ai_actor", "hostile", new Vector2I(0, 0));
        actor.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        actor.SetCurrentHp(100);
        ApplyEquipmentSkillSource(actor);
        BattleUnitState target = BuildUnit("fixture_ai_target", "player", new Vector2I(1, 0));

        BattleState state = new()
        {
            battle_id = "fixture_ai_equipment_skill_battle",
            phase = "unit_acting",
            map_size = new Vector2I(6, 2),
            timeline = new BattleTimelineState { current_tu = 0 },
            active_unit_id = actor.unit_id,
        };
        state.SetUnit(actor);
        state.SetUnit(target);

        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            SkillId,
            "Fixture Equipment Skill",
            TestSkillDefinitionProjection.BuildCombatProfile(
                SkillId,
                effects: new[] { TestSkillDefinitionProjection.BuildEffect("damage") },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangePattern: "fixed",
                rangeValue: 4,
                levelOverrides: new Dictionary<int, CombatSkillLevelOverrideImportModel>
                {
                    [2] = new CombatSkillLevelOverrideImportModel(rangeValue: 5),
                }
            )
        );
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [SkillId] = skill,
        };
        var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
        {
            [BindingId] = BuildBinding(),
        };

        UseUnitSkillActionDefinition template =
            TestEnemyDefinitionFactory.UseUnitSkill(
                "fixture_unit_skill_template",
                scoreBucketId: "fixture",
                targetSelector: "nearest_enemy"
            );
        EnemyAiGenerationSlotDefinition slot =
            TestEnemyDefinitionFactory.GenerationSlot(
                "fixture_offense",
                order: 10,
                allowedAffordances: new StringName[] { "unit_hostile.damage" },
                actionFamilies: new StringName[] { "use_unit_skill" },
                styleTemplateActionId: template.ActionId,
                scoreBucketId: "fixture",
                targetSelector: "nearest_enemy"
            );
        EnemyAiStateDefinition stateDefinition = TestEnemyDefinitionFactory.State(
            StateId,
            new EnemyAiActionDefinition[] { template },
            new[] { slot }
        );
        EnemyAiBrainDefinition brain = TestEnemyDefinitionFactory.Brain(
            "fixture_equipment_skill_brain",
            StateId,
            new[] { stateDefinition }
        );
        actor.ai_brain_id = brain.BrainId;

        return new Fixture
        {
            Actor = actor,
            Target = target,
            State = state,
            Grid = new BattleGridService(),
            SkillDefinitions = skillDefinitions,
            Bindings = bindings,
            ItemDefinitions = new Dictionary<StringName, ItemDefinition>(),
            Brain = brain,
        };
    }

    private static EquipmentAbilityBindingDefinition BuildBinding() =>
        new()
        {
            BindingId = BindingId,
            GrantedActions = new EquipmentGrantedActionDefinition[]
            {
                new()
                {
                    GrantedActionId = GrantId,
                    GrantedKind = EquipmentGrantedActionKind.Skill,
                    SkillId = SkillId,
                    SkillLevel = 2,
                    UsagePeriodKind = EquipmentAbilityUsagePeriodKind.PerBattle,
                    MaxUsesPerPeriod = 1,
                    AvailabilityConditions = new EquipmentConditionGroupDefinition
                    {
                        Mode = "all",
                        Conditions = new EquipmentAbilityConditionDefinition[]
                        {
                            new()
                            {
                                ConditionId = "fixture_hp_gate",
                                Kind = "compare_fact",
                                PayloadDefinition = new CompareFactConditionPayloadDefinition
                                {
                                    Left = new EquipmentAbilityFactQueryDefinition
                                    {
                                        QueryKind = "fact",
                                        FactId = "hp_percent_bp",
                                        Subject = "source",
                                        ValueKind = "int",
                                    },
                                    Compare = "lte",
                                    Right = new EquipmentAbilityFactQueryDefinition
                                    {
                                        QueryKind = "literal",
                                        IntLiteral = 5000,
                                        ValueKind = "int",
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

    private static void ApplyEquipmentSkillSource(BattleUnitState actor)
    {
        actor.ReplaceEquipmentAbilityProjectionTyped(
            new BattleEquipmentAbilitySourceState[]
            {
                new()
                {
                    EffectiveInstanceKey = "fixture_equipment_source",
                    EquipmentDefId = "fixture_equipment_item",
                    SourceEquipmentInstanceId = "",
                    SourceKind = EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment,
                    AbilityIds = new List<StringName> { BindingId },
                },
            },
            Array.Empty<BattleTemporalProgressModifierState>()
        );
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
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(hp: 100, mp: 10, stamina: 10, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleAvailableSkillEntry FindEquipmentSkillEntry(Fixture fixture)
    {
        BattleSkillAvailabilityView view = new BattleSkillAvailabilityService(
            fixture.SkillDefinitions,
            fixture.Bindings,
            fixture.ItemDefinitions
        ).BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = fixture.Actor,
                Consumer = BattleSkillAvailabilityConsumer.AiPlanning,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                WorldStep = 0,
                BattleState = fixture.State,
            }
        );
        foreach (BattleAvailableSkillEntry entry in view.SkillEntries)
        {
            if (entry?.EntryRef.SkillId == SkillId && entry.IsSelectable)
                return entry;
        }
        throw new InvalidOperationException("fixture equipment skill entry missing");
    }

    private static bool ContainsGeneratedSkill(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
    {
        foreach (BattleAiRuntimeActionEntry entry in entries ?? Array.Empty<BattleAiRuntimeActionEntry>())
        {
            if (entry?.Metadata.generated == true && entry.Metadata.skill_id == skillId)
                return true;
        }
        return false;
    }

    private sealed class FormalPhoenixAiFixture : IDisposable
    {
        private bool _disposed;

        private FormalPhoenixAiFixture(
            ContentSnapshot snapshot,
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState actor,
            BattleUnitState enemy,
            EnemyAiBrainDefinition brain
        )
        {
            Snapshot = snapshot;
            Runtime = runtime;
            State = state;
            Actor = actor;
            Enemy = enemy;
            Brain = brain;
        }

        internal ContentSnapshot Snapshot { get; }
        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Actor { get; }
        internal BattleUnitState Enemy { get; }
        internal EnemyAiBrainDefinition Brain { get; }

        internal static FormalPhoenixAiFixture Build()
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            BattleRuntimeModule runtime = null;
            BattleState state = null;
            try
            {
                BattleUnitState actor = BuildUnit(
                    "formal_phoenix_ai_actor",
                    "enemy",
                    new Vector2I(3, 3)
                );
                actor.control_mode = "ai";
                actor.ai_brain_id = "formal_phoenix_equipment_brain";
                actor.ai_state_id = StateId;
                actor.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
                actor.SetCombatResources(
                    hp: 40,
                    mp: 30,
                    stamina: 30,
                    aura: 0,
                    ap: 3,
                    movePoints: 4
                );
                actor.SetUnarmedWeaponProjectionTyped();
                var equipment = new EquipmentState();
                equipment.SetEquippedEntry(
                    "ring_2",
                    "acc_phoenix_rebirth_ring_2",
                    new[] { new StringName("ring_2") },
                    EquipmentInstanceState.CreateInstance(
                        "acc_phoenix_rebirth_ring_2",
                        "eq_formal_phoenix_ash_ring"
                    )
                );
                equipment.SetEquippedEntry(
                    "head",
                    "armor_phoenix_rebirth_head",
                    new[] { new StringName("head") },
                    EquipmentInstanceState.CreateInstance(
                        "armor_phoenix_rebirth_head",
                        "eq_formal_phoenix_solar_anchor"
                    )
                );
                actor.SetEquipmentView(equipment);
                actor.ReplaceEquipmentAbilityProjectionTyped(
                    new BattleEquipmentAbilitySourceState[]
                    {
                        new()
                        {
                            EffectiveInstanceKey = "formal_phoenix_ash_ring_source",
                            EquipmentDefId = "acc_phoenix_rebirth_ring_2",
                            SourceEquipmentInstanceId = "eq_formal_phoenix_ash_ring",
                            SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                            AbilityIds = new List<StringName> { AshBindingId },
                        },
                        new()
                        {
                            EffectiveInstanceKey = "formal_phoenix_solar_set_source",
                            EquipmentDefId = "armor_phoenix_rebirth_head",
                            SourceEquipmentInstanceId = "eq_formal_phoenix_solar_anchor",
                            SourceKind =
                                EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold,
                            AbilityIds = new List<StringName> { SolarBindingId },
                        },
                    },
                    Array.Empty<BattleTemporalProgressModifierState>()
                );

                BattleUnitState enemy = BuildUnit(
                    "formal_phoenix_ai_enemy",
                    "player",
                    new Vector2I(4, 3)
                );
                enemy.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
                enemy.SetCurrentHp(100);
                BattleUnitState ally = BuildUnit(
                    "formal_phoenix_ai_ally",
                    "enemy",
                    new Vector2I(3, 4)
                );
                ally.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
                ally.SetCurrentHp(20);

                state = BattleTestFixture.BuildFlatState(
                    "formal_phoenix_equipment_ai",
                    new Vector2I(7, 7)
                );
                state.terrain_profile_id = "default";
                state.ReplaceEnvironmentSnapshot(
                    BattleEnvironmentSnapshot.FromBattleStartContext(
                        new GDictionary { ["world_step"] = 0 }
                    )
                );
                BattleTestFixture.InstallUnits(
                    state,
                    new[] { enemy },
                    new[] { actor, ally }
                );
                state.PhaseKind = BattlePhaseKind.UnitActing;
                state.active_unit_id = actor.unit_id;

                runtime = new BattleRuntimeModule();
                runtime.setup(
                    null,
                    snapshot.Skills,
                    item_defs: snapshot.Items,
                    trait_defs: snapshot.Traits,
                    equipment_ability_bindings: snapshot.EquipmentAbilityBindings,
                    barrier_profile_definitions: snapshot.BarrierProfiles
                );
                runtime.SetupStateForTests(state);
                return new FormalPhoenixAiFixture(
                    snapshot,
                    runtime,
                    state,
                    actor,
                    enemy,
                    BuildBrain()
                );
            }
            catch
            {
                BattleTestFixture.DisposeBattleFixture(runtime, state);
                throw;
            }
        }

        private static EnemyAiBrainDefinition BuildBrain()
        {
            UseUnitSkillActionDefinition unitTemplate =
                TestEnemyDefinitionFactory.UseUnitSkill(
                    "formal_phoenix_unit_template",
                    scoreBucketId: "fixture",
                    targetSelector: "nearest_enemy",
                    desiredMinDistance: 0,
                    desiredMaxDistance: 1,
                    distanceReference: "target_unit"
                );
            UseGroundSkillActionDefinition groundTemplate =
                TestEnemyDefinitionFactory.UseGroundSkill(
                    "formal_phoenix_ground_template",
                    scoreBucketId: "fixture",
                    desiredMinDistance: 0,
                    desiredMaxDistance: 0,
                    distanceReference: "target_coord"
                );
            EnemyAiGenerationSlotDefinition unitSlot =
                TestEnemyDefinitionFactory.GenerationSlot(
                    "formal_phoenix_unit_offense",
                    slotRole: "offense",
                    order: 10,
                    allowedAffordances: new StringName[] { "unit_hostile.damage" },
                    actionFamilies: new StringName[] { "use_unit_skill" },
                    styleTemplateActionId: unitTemplate.ActionId,
                    scoreBucketId: "fixture",
                    targetSelector: "nearest_enemy",
                    desiredMinDistance: 0,
                    desiredMaxDistance: 1,
                    distanceReference: "target_unit"
                );
            EnemyAiGenerationSlotDefinition groundSlot =
                TestEnemyDefinitionFactory.GenerationSlot(
                    "formal_phoenix_ground_offense",
                    slotRole: "offense",
                    order: 20,
                    allowedAffordances: new StringName[] { "ground_hostile.aoe" },
                    actionFamilies: new StringName[] { "use_ground_skill" },
                    styleTemplateActionId: groundTemplate.ActionId,
                    scoreBucketId: "fixture",
                    targetSelector: "nearest_enemy",
                    desiredMinDistance: 0,
                    desiredMaxDistance: 0,
                    distanceReference: "target_coord"
                );
            EnemyAiStateDefinition stateDefinition = TestEnemyDefinitionFactory.State(
                StateId,
                new EnemyAiActionDefinition[] { unitTemplate, groundTemplate },
                new[] { unitSlot, groundSlot }
            );
            return TestEnemyDefinitionFactory.Brain(
                "formal_phoenix_equipment_brain",
                StateId,
                new[] { stateDefinition }
            );
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }

    private sealed class Fixture
    {
        internal BattleUnitState Actor;
        internal BattleUnitState Target;
        internal BattleState State;
        internal BattleGridService Grid;
        internal EnemyAiBrainDefinition Brain;
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings;
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefinitions;
    }
}
