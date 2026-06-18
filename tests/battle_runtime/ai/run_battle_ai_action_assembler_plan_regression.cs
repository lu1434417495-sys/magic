using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_ai_action_assembler_plan_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestAssemblerReturnsRuntimePlanWithoutMutatingState();
            TestAssemblerEnablesCandidateForRuntimeMoveClones();
            TestGenerationIsSlotFamilyScopedNotGlobalSkillSuppressed();
            TestGeneratedMetadataContainsStableRuntimeIdentity();
            Quit(_test.Finish("Battle AI action assembler plan regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(1);
        }
    }

    private void TestAssemblerReturnsRuntimePlanWithoutMutatingState()
    {
        Fixture fixture = BuildFixture();
        int originalActionCount = fixture.StateDef.actions.Count;
        BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefs
        );
        IReadOnlyList<EnemyAiAction> actions = plan.GetActions("engage");

        _test.True(plan.HasState("engage"), "Assembler should create a plan state for the brain state.");
        _test.True(
            actions.Count > originalActionCount,
            "Runtime plan should contain authored and generated actions."
        );
        _test.Eq(
            fixture.StateDef.actions.Count,
            originalActionCount,
            "Assembler should not write generated actions back into the state resource."
        );
    }

    private void TestAssemblerEnablesCandidateForRuntimeMoveClones()
    {
        Fixture fixture = BuildFixture();
        BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefs
        );
        IReadOnlyList<EnemyAiAction> actions = plan.GetActions("engage");
        MoveToRangeAction runtimeTemplateMove = FindActionById(actions, "template_move") as MoveToRangeAction;
        _test.True(runtimeTemplateMove != null, "Runtime plan should keep the authored move_to_range action.");
        _test.True(
            runtimeTemplateMove != fixture.MoveTemplate,
            "Runtime plan should clone authored move_to_range resources."
        );
        _test.True(
            runtimeTemplateMove != null && runtimeTemplateMove.UsesCandidateRequest(),
            "Runtime authored move_to_range clone should default to candidate_request."
        );
        _test.True(
            !fixture.MoveTemplate.UsesCandidateRequest(),
            "Assembler should not mutate the authored move_to_range resource."
        );

        MoveToRangeAction generatedMove = FindMoveActionForSkill(actions, "chain_arc");
        _test.True(
            generatedMove != null && generatedMove.UsesCandidateRequest(),
            "Generated move_to_range action should enable candidate_request."
        );
    }

    private void TestGenerationIsSlotFamilyScopedNotGlobalSkillSuppressed()
    {
        Fixture fixture = BuildFixture();
        BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefs
        );
        IReadOnlyList<EnemyAiAction> actions = plan.GetActions("engage");
        _test.True(
            HasActionForSkill<UseRandomChainSkillAction>(actions, "chain_arc"),
            "Random-chain skills should generate use_random_chain_skill actions."
        );
        _test.True(
            FindMoveActionForSkill(actions, "chain_arc") != null,
            "The same random-chain skill should also generate its move_to_range companion."
        );
        _test.True(
            !HasActionForSkill<UseMultiUnitSkillAction>(actions, "chain_arc"),
            "Random-chain skills should not generate use_multi_unit_skill actions."
        );
        _test.True(
            !HasActionForSkill<MoveToMultiUnitSkillPositionAction>(actions, "chain_arc"),
            "Random-chain skills should not generate move_to_multi_unit_skill_position actions."
        );
    }

    private void TestGeneratedMetadataContainsStableRuntimeIdentity()
    {
        Fixture fixture = BuildFixture();
        BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefs
        );

        foreach (EnemyAiAction action in plan.GetActions("engage"))
        {
            BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata = plan.GetActionMetadata(action);
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
                action.score_bucket_id,
                new StringName("harrier_pressure"),
                "Slot score_bucket_id should override generated action score bucket."
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
        };

        var brain = new EnemyAiBrainDef
        {
            brain_id = "plan_brain",
            default_state_id = "engage",
            states = new Godot.Collections.Array<EnemyAiStateDef> { stateDef },
        };
        var unit = new BattleUnitState
        {
            unit_id = "actor",
            ai_brain_id = brain.brain_id,
            known_active_skill_ids = new GStringNameArray { "bolt", "chain_arc" },
        };
        unit.SetKnownSkillLevelsTyped(
            new Dictionary<StringName, int>
            {
                ["bolt"] = 1,
                ["chain_arc"] = 1,
            }
        );

        return new Fixture
        {
            Assembler = new BattleAiActionAssembler(),
            Brain = brain,
            StateDef = stateDef,
            Unit = unit,
            MoveTemplate = moveTemplate,
            SkillDefs = new Dictionary<StringName, SkillDef>
            {
                ["bolt"] = Skill("bolt", "unit", "enemy", "damage"),
                ["chain_arc"] = ChainSkill(),
            },
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

    private static SkillDef Skill(
        StringName skillId,
        StringName targetMode,
        StringName targetFilter,
        StringName effectType
    )
    {
        var skill = new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            skill_type = "active",
        };
        var combat = new CombatSkillDef
        {
            target_mode = targetMode,
            target_team_filter = targetFilter,
            range_pattern = "fixed",
            range_value = 4,
        };
        combat.effect_defs.Add(Effect(effectType));
        skill.combat_profile = combat;
        return skill;
    }

    private static SkillDef ChainSkill()
    {
        SkillDef skill = Skill("chain_arc", "unit", "enemy", "chain_damage");
        CombatSkillDef combat = skill.combat_profile as CombatSkillDef;
        combat.target_selection_mode = "random_chain";
        combat.max_hits_per_target = 2;
        return skill;
    }

    private static CombatEffectDef Effect(StringName effectType)
    {
        return new CombatEffectDef { effect_type = effectType };
    }

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
        public Dictionary<StringName, SkillDef> SkillDefs;
    }
}
