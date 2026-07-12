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
        int originalActionCount = fixture.StateResource.actions.Count;
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
            fixture.StateResource.actions.Count,
            originalActionCount,
            "Assembler should not write generated actions back into the authoring Resource."
        );
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            _test.True(
                entry?.Action is EnemyAiActionDefinition,
                "Authored and generated entries should share EnemyAiActionDefinition."
            );
        }
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
            fixture.MoveTemplateResource.ai_evaluation_mode != (StringName)"candidate_request",
            "Assembler should not mutate the authored move Resource."
        );

        BattleAiRuntimeActionEntry generatedMove = FindEntryForSkill<MoveToRangeActionDefinition>(
            plan.GetActionEntries("engage"),
            "chain_arc"
        );
        _test.True(
            generatedMove?.Metadata.force_candidate_request_evaluation == true
                && generatedMove.Action is MoveToRangeActionDefinition,
            "Generated move_to_range should use one immutable definition and candidate metadata."
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
        var stateResource = new EnemyAiStateDef { state_id = "engage" };
        var unitTemplate = TestResourceOwnership.Own(
            new UseUnitSkillAction
            {
                action_id = "template_unit",
                score_bucket_id = "frontline_pressure",
                target_selector = "nearest_enemy",
            },
            "BattleAiActionAssemblerPlan.BuildFixture.unit_template"
        );
        var moveTemplate = TestResourceOwnership.Own(
            new MoveToRangeAction
            {
                action_id = "template_move",
                score_bucket_id = "archer_survival",
                target_selector = "nearest_enemy",
            },
            "BattleAiActionAssemblerPlan.BuildFixture.move_template"
        );
        stateResource.actions = new Godot.Collections.Array<EnemyAiAction>
        {
            unitTemplate,
            moveTemplate,
        };
        stateResource.generation_slots = new Godot.Collections.Array<EnemyAiGenerationSlotDef>
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
        TestResourceOwnership.Own(
            stateResource,
            "BattleAiActionAssemblerPlan.BuildFixture.state"
        );

        var brainResource = TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = "plan_brain",
                default_state_id = "engage",
                states = new Godot.Collections.Array<EnemyAiStateDef> { stateResource },
            },
            "BattleAiActionAssemblerPlan.BuildFixture.brain"
        );
        EnemyAiBrainDefinition brain = brainResource.ToDefinition();
        var unit = new BattleUnitState
        {
            unit_id = "actor",
            ai_brain_id = brain.BrainId,
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
            StateResource = stateResource,
            Unit = unit,
            MoveTemplateResource = moveTemplate,
            MoveTemplateDefinition = brain.GetState("engage").Actions[1] as MoveToRangeActionDefinition,
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
            slot.allowed_affordances.Add(affordance);
        foreach (StringName family in families)
            slot.action_families.Add(family);
        return TestResourceOwnership.Own(
            slot,
            $"BattleAiActionAssemblerPlan.Slot.{slotId}"
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
        public EnemyAiStateDef StateResource;
        public BattleUnitState Unit;
        public MoveToRangeAction MoveTemplateResource;
        public MoveToRangeActionDefinition MoveTemplateDefinition;
        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
    }
}
