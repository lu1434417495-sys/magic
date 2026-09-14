using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_action_assembler_plan_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestAssemblerReturnsDefinitionPlanWithoutMutatingAuthoringState();
            TestAssemblerEnablesCandidateMetadataWithoutMutatingAuthoredMove();
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

    private void TestAssemblerReturnsDefinitionPlanWithoutMutatingAuthoringState()
    {
        Fixture fixture = BuildFixture();
        int originalActionCount = fixture.StateDefinition.Actions.Count;
        using BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefinitions
        );
        IReadOnlyList<BattleAiRuntimeActionEntry> entries = plan.GetActionEntries("engage");

        _test.True(plan.HasState("engage"), "Assembler should create a plan state for the brain state.");
        _test.True(
            entries.Count > originalActionCount,
            "Runtime plan should contain authored and generated definitions."
        );
        _test.Eq(
            fixture.StateDefinition.Actions.Count,
            originalActionCount,
            "Assembler should not write generated actions back into the immutable state definition."
        );
    }

    private void TestAssemblerEnablesCandidateMetadataWithoutMutatingAuthoredMove()
    {
        Fixture fixture = BuildFixture();
        using BattleAiRuntimeActionPlan plan = fixture.Assembler.BuildUnitActionPlan(
            fixture.Unit,
            fixture.Brain,
            fixture.SkillDefinitions
        );
        MoveToRangeActionDefinition runtimeTemplateMove = FindActionById(
            plan.GetActions("engage"),
            "template_move"
        ) as MoveToRangeActionDefinition;
        _test.True(runtimeTemplateMove != null, "Runtime plan should keep the authored move definition.");
        _test.True(
            ReferenceEquals(runtimeTemplateMove, fixture.MoveTemplateDefinition),
            "Runtime plan should borrow the process-snapshot move definition."
        );
        _test.True(
            plan.GetActionMetadata(runtimeTemplateMove).force_candidate_request_evaluation,
            "Authored no-screening move metadata should select candidate evaluation."
        );
        _test.True(
            fixture.MoveTemplateDefinition.AiEvaluationMode != (StringName)"candidate_request",
            "Assembler should not mutate the borrowed move definition."
        );

        BattleAiRuntimeActionEntry generatedMove = FindEntryForSkill<MoveToRangeActionDefinition>(
            plan.GetActionEntries("engage"),
            "chain_arc"
        );
        _test.True(
            generatedMove?.Metadata.force_candidate_request_evaluation == true,
            "Generated move_to_range should enable candidate-request evaluation metadata."
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
        IReadOnlyList<BattleAiRuntimeActionEntry> entries = plan.GetActionEntries("engage");

        _test.True(
            FindEntryForSkill<UseRandomChainSkillActionDefinition>(entries, "chain_arc") != null,
            "Random-chain skills should generate use_random_chain_skill definitions."
        );
        _test.True(
            FindEntryForSkill<MoveToRangeActionDefinition>(entries, "chain_arc") != null,
            "The same random-chain skill should also generate its move_to_range companion."
        );
        _test.True(
            FindEntryForSkill<UseMultiUnitSkillActionDefinition>(entries, "chain_arc") == null,
            "Random-chain skills should not generate use_multi_unit_skill definitions."
        );
        _test.True(
            FindEntryForSkill<MoveToMultiUnitSkillPositionActionDefinition>(entries, "chain_arc")
                == null,
            "Random-chain skills should not generate multi-unit positioning definitions."
        );
        _test.True(
            FindEntryForSkill<MoveToMultiUnitSkillPositionActionDefinition>(entries, "wide_arc")
                != null,
            "Multi-unit skills should generate typed positioning definitions."
        );
        _test.True(
            FindEntryForSkill<UseGroundSkillActionDefinition>(entries, "ground_burst") != null,
            "Ground skills should generate typed ground-skill definitions."
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
                continue;

            _test.Eq(metadata.state_id, new StringName("engage"), "Generated metadata should include state_id.");
            _test.Eq(metadata.slot_id, new StringName("offense"), "Generated metadata should include slot_id.");
            _test.Eq(
                metadata.action_family,
                new StringName("use_unit_skill"),
                "Generated metadata should include action_family."
            );
            _test.Eq(
                metadata.identity_key,
                "engage/offense/bolt/use_unit_skill",
                "Generated metadata should use stable typed content identity."
            );
            _test.Eq(
                entry.ScoreBucketId,
                new StringName("harrier_pressure"),
                "Slot score_bucket_id should override the generated definition."
            );
            _test.True(
                entry.Action is UseUnitSkillActionDefinition,
                "Generated unit-skill entries should use the shared definition type."
            );
            return;
        }
        _test.Fail("Expected generated metadata for bolt.");
    }

    private static Fixture BuildFixture()
    {
        UseUnitSkillActionDefinition unitTemplate =
            TestEnemyDefinitionFactory.UseUnitSkill(
                "template_unit",
                scoreBucketId: "frontline_pressure",
                targetSelector: "nearest_enemy"
            );
        MoveToRangeActionDefinition moveTemplate = TestEnemyDefinitionFactory.MoveToRange(
            "template_move",
            scoreBucketId: "archer_survival",
            targetSelector: "nearest_enemy"
        );
        var slots = new List<EnemyAiGenerationSlotDefinition>
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
        EnemyAiStateDefinition stateDefinition = TestEnemyDefinitionFactory.State(
            "engage",
            new EnemyAiActionDefinition[] { unitTemplate, moveTemplate },
            slots
        );
        EnemyAiBrainDefinition brain = TestEnemyDefinitionFactory.Brain(
            "plan_brain",
            "engage",
            new[] { stateDefinition }
        );
        var unit = new BattleUnitState
        {
            unit_id = "actor",
            ai_brain_id = brain.BrainId,
        };
        unit.SetKnownActiveSkillIds(
            new StringName[] { "bolt", "chain_arc", "wide_arc", "ground_burst" }
        );
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
            StateDefinition = stateDefinition,
            Unit = unit,
            MoveTemplateDefinition = moveTemplate,
            SkillDefinitions = skillDefinitions,
        };
    }

    private static EnemyAiGenerationSlotDefinition Slot(
        StringName slotId,
        int order,
        IEnumerable<StringName> affordances,
        IEnumerable<StringName> families,
        StringName templateActionId,
        StringName bucketId
    )
    {
        return TestEnemyDefinitionFactory.GenerationSlot(
            slotId,
            order: order,
            allowedAffordances: new List<StringName>(affordances),
            actionFamilies: new List<StringName>(families),
            styleTemplateActionId: templateActionId,
            scoreBucketId: bucketId,
            targetSelector: "nearest_enemy"
        );
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
            BattleTypedNames.ToStringName(BattleTargetSelectionMode.RandomChain),
            2
        );

    private static SkillDefinition MultiUnitSkill() =>
        Skill(
            "wide_arc",
            "unit",
            "enemy",
            "damage",
            BattleTypedNames.ToStringName(BattleTargetSelectionMode.MultiUnit),
            0
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

    private static BattleAiRuntimeActionEntry FindEntryForSkill<TAction>(
        IReadOnlyList<BattleAiRuntimeActionEntry> entries,
        StringName skillId
    )
        where TAction : EnemyAiActionDefinition
    {
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            if (entry?.Action is TAction && ContainsSkillId(entry.Action.DeclaredSkillIds, skillId))
                return entry;
        }
        return null;
    }

    private static bool ContainsSkillId(IReadOnlyList<StringName> skillIds, StringName skillId)
    {
        foreach (StringName candidate in skillIds ?? Array.Empty<StringName>())
        {
            if (candidate == skillId)
                return true;
        }
        return false;
    }

    private static EnemyAiActionDefinition FindActionById(
        IReadOnlyList<EnemyAiActionDefinition> actions,
        StringName actionId
    )
    {
        foreach (EnemyAiActionDefinition action in actions)
        {
            if (action?.ActionId == actionId)
                return action;
        }
        return null;
    }

    private sealed class Fixture
    {
        public BattleAiActionAssembler Assembler;
        public EnemyAiBrainDefinition Brain;
        public EnemyAiStateDefinition StateDefinition;
        public BattleUnitState Unit;
        public MoveToRangeActionDefinition MoveTemplateDefinition;
        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
    }
}
