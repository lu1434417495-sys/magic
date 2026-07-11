using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_ai_action_assembler_plan_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestAssemblerReturnsRuntimePlanWithoutMutatingState();
            TestAssemblerEnablesCandidateForRuntimeMovePlanMetadata();
            TestGenerationIsSlotFamilyScopedNotGlobalSkillSuppressed();
            TestGeneratedMetadataContainsStableRuntimeIdentity();
            RequestTestExit(_test.Finish("Battle AI action assembler plan regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Battle AI action assembler plan regression", 1));
        }
    }

    private void TestAssemblerReturnsRuntimePlanWithoutMutatingState()
    {
        Fixture fixture = BuildFixture();
        int originalActionCount = fixture.StateDef.actions.Count;
        using BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefinitions
        );
        IReadOnlyList<BattleAiRuntimeActionEntry> entries = plan.GetActionEntries("engage");

        _test.True(plan.HasState("engage"), "Assembler should create a plan state for the brain state.");
        _test.True(
            entries.Count > originalActionCount,
            "Runtime plan should contain authored and generated actions."
        );
        _test.Eq(
            fixture.StateDef.actions.Count,
            originalActionCount,
            "Assembler should not write generated actions back into the state resource."
        );
    }

    private void TestAssemblerEnablesCandidateForRuntimeMovePlanMetadata()
    {
        Fixture fixture = BuildFixture();
        using BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefinitions
        );
        IReadOnlyList<EnemyAiAction> actions = plan.GetActions("engage");
        MoveToRangeAction runtimeTemplateMove = FindActionById(actions, "template_move") as MoveToRangeAction;
        _test.True(runtimeTemplateMove != null, "Runtime plan should keep the authored move_to_range action.");
        _test.True(
            runtimeTemplateMove == fixture.MoveTemplate,
            "Runtime plan should reuse borrowed authored move_to_range resources."
        );
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata =
            plan.GetActionMetadata(runtimeTemplateMove);
        _test.True(
            metadata.force_candidate_request_evaluation,
            "Runtime authored move_to_range metadata should default to candidate_request."
        );
        _test.True(
            !fixture.MoveTemplate.UsesCandidateRequest(),
            "Assembler should not mutate the authored move_to_range resource."
        );

        BattleAiRuntimeActionEntry generatedMove = FindGeneratedMoveEntryForSkill(
            plan.GetActionEntries("engage"),
            "chain_arc"
        );
        _test.True(
            generatedMove?.IsGeneratedMoveToRange == true
                && generatedMove.ResourceAction == null
                && generatedMove.Metadata.force_candidate_request_evaluation,
            "Generated move_to_range action should be a plain candidate_request runtime entry."
        );
    }

    private void TestGenerationIsSlotFamilyScopedNotGlobalSkillSuppressed()
    {
        Fixture fixture = BuildFixture();
        using BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefinitions
        );
        _test.True(
            FindGeneratedRandomChainEntryForSkill(plan.GetActionEntries("engage"), "chain_arc")
                != null,
            "Random-chain skills should generate plain use_random_chain_skill runtime entries."
        );
        _test.True(
            FindGeneratedMoveEntryForSkill(plan.GetActionEntries("engage"), "chain_arc") != null,
            "The same random-chain skill should also generate its move_to_range companion."
        );
        IReadOnlyList<EnemyAiAction> actions = plan.GetActions("engage");
        _test.True(
            !HasActionForSkill<UseMultiUnitSkillAction>(actions, "chain_arc"),
            "Random-chain skills should not generate use_multi_unit_skill actions."
        );
        _test.True(
            !HasActionForSkill<MoveToMultiUnitSkillPositionAction>(actions, "chain_arc"),
            "Random-chain skills should not generate move_to_multi_unit_skill_position actions."
        );
        _test.True(
            FindGeneratedMoveToMultiUnitPositionEntryForSkill(
                plan.GetActionEntries("engage"),
                "wide_arc"
            )
                != null,
            "Multi-unit skills should generate plain move_to_multi_unit_skill_position runtime entries."
        );
        _test.True(
            !HasActionForSkill<MoveToMultiUnitSkillPositionAction>(actions, "wide_arc"),
            "Generated move_to_multi_unit_skill_position should not be a Resource action."
        );
        _test.True(
            FindGeneratedGroundEntryForSkill(plan.GetActionEntries("engage"), "ground_burst")
                != null,
            "Ground skills should generate plain use_ground_skill runtime entries."
        );
        _test.True(
            !HasActionForSkill<UseGroundSkillAction>(actions, "ground_burst"),
            "Generated use_ground_skill should not be a Resource action."
        );
    }

    private void TestGeneratedMetadataContainsStableRuntimeIdentity()
    {
        Fixture fixture = BuildFixture();
        using BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefinitions
        );

        foreach (BattleAiRuntimeActionEntry entry in plan.GetActionEntries("engage"))
        {
            BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata = entry.Metadata;
            if (!metadata.generated || metadata.skill_id != (StringName)"bolt")
            {
                continue;
            }

            _test.Eq(metadata.state_id, new StringName("engage"), "Generated metadata should include state_id.");
            _test.Eq(metadata.slot_id, new StringName("offense"), "Generated metadata should include slot_id.");
            _test.Eq(
                metadata.action_family,
                new StringName("use_unit_skill"),
                "Generated metadata should include action_family."
            );
            _test.Eq(
                entry.ScoreBucketId,
                new StringName("harrier_pressure"),
                "Slot score_bucket_id should override generated action score bucket."
            );
            _test.True(
                entry.IsGeneratedUseUnitSkill && entry.ResourceAction == null,
                "Generated use_unit_skill should be a plain runtime entry."
            );
            return;
        }
        _test.Fail("Expected generated metadata for bolt.");
    }

    private static Fixture BuildFixture()
    {
        var stateDef = new EnemyAiStateDef { state_id = "engage" };
        var unitTemplate = new UseUnitSkillAction
        {
            action_id = "template_unit",
            score_bucket_id = "frontline_pressure",
            target_selector = "nearest_enemy",
        };
        var moveTemplate = new MoveToRangeAction
        {
            action_id = "template_move",
            score_bucket_id = "archer_survival",
            target_selector = "nearest_enemy",
        };
        stateDef.actions = new Godot.Collections.Array<EnemyAiAction> { unitTemplate, moveTemplate };
        stateDef.generation_slots = new Godot.Collections.Array<EnemyAiGenerationSlotDef>
        {
            Slot(
                "offense",
                10,
                new[] { new StringName("unit_hostile.damage") },
                new[] { new StringName("use_unit_skill") },
                "template_unit",
                "harrier_pressure"
            ),
            Slot(
                "chain_cast",
                20,
                new[] { new StringName("random_chain") },
                new[] { new StringName("use_random_chain_skill") },
                "template_unit",
                "frontline_pressure"
            ),
            Slot(
                "chain_move",
                30,
                new[] { new StringName("random_chain") },
                new[] { new StringName("move_to_range") },
                "template_move",
                "archer_survival"
            ),
            Slot(
                "multi_move",
                40,
                new[] { new StringName("multi_unit") },
                new[] { new StringName("move_to_multi_unit_skill_position") },
                "template_move",
                "archer_survival"
            ),
            Slot(
                "ground_cast",
                50,
                new[] { new StringName("ground_hostile.aoe") },
                new[] { new StringName("use_ground_skill") },
                "template_unit",
                "frontline_pressure"
            ),
        };

        var brain = TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = "plan_brain",
                default_state_id = "engage",
                states = new Godot.Collections.Array<EnemyAiStateDef> { stateDef },
            },
            "BattleAiActionAssemblerPlan.BuildFixture.brain"
        );
        var unit = new BattleUnitState
        {
            unit_id = "actor",
            ai_brain_id = brain.brain_id,
            known_active_skill_ids = new GStringNameArray
            {
                "bolt",
                "chain_arc",
                "wide_arc",
                "ground_burst",
            },
        };
        unit.SetKnownSkillLevelsTyped(
            new Dictionary<StringName, int>
            {
                ["bolt"] = 1,
                ["chain_arc"] = 1,
                ["wide_arc"] = 1,
                ["ground_burst"] = 1,
            }
        );

        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            ["bolt"] = Skill("bolt", "unit", "enemy", "damage"),
            ["chain_arc"] = ChainSkill(),
            ["wide_arc"] = MultiUnitSkill(),
            ["ground_burst"] = Skill("ground_burst", "ground", "enemy", "damage"),
        };

        return new Fixture
        {
            Assembler = new BattleAiActionAssembler(),
            Brain = brain,
            StateDef = stateDef,
            Unit = unit,
            MoveTemplate = moveTemplate,
            SkillDefinitions = skillDefinitions,
        };
    }

    private static EnemyAiGenerationSlotDef Slot(
        StringName slotId,
        int order,
        IEnumerable<StringName> affordances,
        IEnumerable<StringName> families,
        StringName templateActionId,
        StringName bucketId
    )
    {
        var slot = new EnemyAiGenerationSlotDef
        {
            slot_id = slotId,
            order = order,
            style_template_action_id = templateActionId,
            score_bucket_id = bucketId,
            target_selector = "nearest_enemy",
        };
        foreach (StringName affordance in affordances)
        {
            slot.allowed_affordances.Add(affordance);
        }
        foreach (StringName family in families)
        {
            slot.action_families.Add(family);
        }
        return slot;
    }

    private static SkillDefinition Skill(
        StringName skillId,
        StringName targetMode,
        StringName targetFilter,
        StringName effectType
    ) =>
        Skill(
            skillId,
            targetMode,
            targetFilter,
            effectType,
            targetSelectionMode: default,
            maxHitsPerTarget: 0
        );

    private static SkillDefinition ChainSkill() =>
        Skill(
            "chain_arc",
            "unit",
            "enemy",
            "chain_damage",
            targetSelectionMode: BattleTypedNames.ToStringName(
                BattleTargetSelectionMode.RandomChain
            ),
            maxHitsPerTarget: 2
        );

    private static SkillDefinition MultiUnitSkill() =>
        Skill(
            "wide_arc",
            "unit",
            "enemy",
            "damage",
            targetSelectionMode: BattleTypedNames.ToStringName(
                BattleTargetSelectionMode.MultiUnit
            ),
            maxHitsPerTarget: 0
        );

    private static SkillDefinition Skill(
        StringName skillId,
        StringName targetMode,
        StringName targetFilter,
        StringName effectType,
        StringName targetSelectionMode,
        int maxHitsPerTarget
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            skillId.ToString(),
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { TestSkillDefinitionProjection.BuildEffect(effectType) },
                targetMode: targetMode,
                targetTeamFilter: targetFilter,
                rangePattern: "fixed",
                rangeValue: 4,
                targetSelectionMode: targetSelectionMode,
                maxHitsPerTarget: maxHitsPerTarget
            )
        );

    private static bool HasActionForSkill<TAction>(
        IReadOnlyList<EnemyAiAction> actions,
        StringName skillId
    )
        where TAction : EnemyAiAction
    {
        foreach (EnemyAiAction action in actions)
        {
            if (action is TAction && action.GetDeclaredSkillIds().Contains(skillId))
            {
                return true;
            }
        }
        return false;
    }

    private static MoveToRangeAction FindMoveActionForSkill(
        IReadOnlyList<EnemyAiAction> actions,
        StringName skillId
    )
    {
        foreach (EnemyAiAction action in actions)
        {
            if (action is MoveToRangeAction moveAction && moveAction.range_skill_ids.Contains(skillId))
            {
                return moveAction;
            }
        }
        return null;
    }

    private static BattleAiRuntimeActionEntry FindGeneratedMoveEntryForSkill(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
    {
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            BattleAiGeneratedMoveToRangeAction moveAction = entry?.GeneratedMoveToRange;
            if (
                moveAction != null
                && ContainsSkillId(moveAction.GetDeclaredSkillIds(), skillId)
            )
            {
                return entry;
            }
        }
        return null;
    }

    private static BattleAiRuntimeActionEntry FindGeneratedRandomChainEntryForSkill(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
    {
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            BattleAiRandomChainSkillActionSpec action = entry?.GeneratedRandomChainSkill;
            if (action != null && ContainsSkillId(action.GetDeclaredSkillIds(), skillId))
            {
                return entry;
            }
        }
        return null;
    }

    private static BattleAiRuntimeActionEntry FindGeneratedMoveToMultiUnitPositionEntryForSkill(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
    {
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            BattleAiMoveToMultiUnitSkillPositionActionSpec action =
                entry?.GeneratedMoveToMultiUnitSkillPosition;
            if (action != null && ContainsSkillId(action.GetDeclaredSkillIds(), skillId))
            {
                return entry;
            }
        }
        return null;
    }

    private static BattleAiRuntimeActionEntry FindGeneratedGroundEntryForSkill(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
    {
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            BattleAiGroundSkillActionSpec action = entry?.GeneratedGroundSkill;
            if (action != null && ContainsSkillId(action.GetDeclaredSkillIds(), skillId))
            {
                return entry;
            }
        }
        return null;
    }

    private static bool ContainsSkillId(IReadOnlyList<StringName> skillIds, StringName skillId)
    {
        foreach (StringName candidate in skillIds ?? Array.Empty<StringName>())
        {
            if (candidate == skillId)
            {
                return true;
            }
        }
        return false;
    }

    private static EnemyAiAction FindActionById(
        IReadOnlyList<EnemyAiAction> actions,
        StringName actionId
    )
    {
        foreach (EnemyAiAction action in actions)
        {
            if (action != null && action.action_id == actionId)
            {
                return action;
            }
        }
        return null;
    }

    private sealed class Fixture
    {
        public BattleAiActionAssembler Assembler;
        public EnemyAiBrainDef Brain;
        public EnemyAiStateDef StateDef;
        public BattleUnitState Unit;
        public MoveToRangeAction MoveTemplate;
        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
    }
}
