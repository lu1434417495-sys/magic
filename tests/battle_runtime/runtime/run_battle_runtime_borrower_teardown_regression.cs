using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_runtime_borrower_teardown_regression : LifecycleTestSceneTree
{
    private sealed class ProbeSpecialProfileView : IBattleSpecialProfileView
    {
        public bool TryGetMeteorSwarmProfile(
            StringName profileId,
            out MeteorSwarmProfileData profile
        )
        {
            profile = null;
            return false;
        }
    }

    private sealed class ThrowingTerrainGenerator : BattleTerrainGenerator
    {
        internal int DisposeAttemptCount { get; private set; }

        public override void Dispose()
        {
            DisposeAttemptCount++;
            throw new InvalidOperationException("expected terrain teardown failure");
        }
    }

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestContentRebindClearsAiBorrowers();
            TestSuccessfulBorrowerFirstTeardownAndDoubleDispose();
            TestExceptionalFinalLeaseCloseStillClearsBorrowers();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle runtime borrower teardown regression"));
    }

    private void TestContentRebindClearsAiBorrowers()
    {
        ContentFixture content = LoadContentFixture();
        var runtime = new BattleRuntimeModule();
        try
        {
            SetupRuntime(runtime, content);
            BattleState state = BuildState(out BattleUnitState actor);
            runtime.SetupStateForTests(state);
            BindDecisionBorrowers(runtime, actor);

            _test.True(runtime.HasAiRuntimeBorrowers, "precondition: AI helper borrowers are bound");

            runtime.SyncContentCatalogsTyped(
                new Dictionary<StringName, ItemDefinition>(),
                new Dictionary<StringName, SkillDefinition>(),
                new Dictionary<StringName, TraitDefinition>(),
                new Dictionary<StringName, EquipmentAbilityBindingDefinition>(),
                new Dictionary<StringName, BarrierProfileDefinition>()
            );

            _test.True(!runtime.HasAiRuntimeBorrowers, "content sync clears decision/helper borrowers");
            _test.Eq(runtime.GetSkillDefinitionIndexTyped().Count, 0, "skill rebind drops old definitions");
            _test.Eq(runtime.GetItemDefIndexTyped().Count, 0, "item rebind drops old definitions");
            _test.Eq(runtime.GetTraitDefIndexTyped().Count, 0, "trait rebind drops old definitions");
            _test.Eq(
                runtime.GetEquipmentAbilityBindingIndexTyped().Count,
                0,
                "equipment binding rebind drops old definitions"
            );

            runtime.ReplaceEnemyTemplatesTyped(new Dictionary<StringName, EnemyTemplateDefinition>());
            runtime.ReplaceEnemyAiBrainsTyped(new Dictionary<StringName, EnemyAiBrainDefinition>());
            _test.Eq(runtime.GetEnemyTemplateIndexTyped().Count, 0, "enemy template rebind drops old definitions");
            _test.Eq(runtime.GetEnemyAiBrainIndexTyped().Count, 0, "enemy brain rebind drops old definitions");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestSuccessfulBorrowerFirstTeardownAndDoubleDispose()
    {
        ContentFixture content = LoadContentFixture();
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var runtime = new BattleRuntimeModule();
        BattleTerrainGenerator ownedTerrainGenerator = runtime.GetTerrainGenerator();
        BattleState state = BuildState(out BattleUnitState actor);
        SetupRuntime(runtime, content);
        runtime.SetupStateForTests(state);
        BindDecisionBorrowers(runtime, actor);

        _test.True(runtime.HasContentCatalogBorrowers, "precondition: all content borrowers are populated");
        _test.True(runtime.HasAiRuntimeBorrowers, "precondition: decision/plan borrowers are populated");
        _test.True(runtime.HasRuntimeSidecarBindings, "precondition: runtime sidecars are bound");

        runtime.Dispose();
        AssertRuntimeCleared(runtime, state, "successful teardown");
        _test.True(ownedTerrainGenerator.IsDisposed, "owned terrain resource closes after borrowers/state");
        AssertAuditBaseline(baseline, "successful teardown");

        LifecycleAuditSnapshot afterFirstDispose = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        runtime.Dispose();
        AssertAuditEqual(afterFirstDispose, LifecycleAuditRegistry.Shared.CaptureSnapshot(), "double Dispose");
    }

    private void TestExceptionalFinalLeaseCloseStillClearsBorrowers()
    {
        ContentFixture content = LoadContentFixture();
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var runtime = new BattleRuntimeModule();
        var throwingTerrainGenerator = new ThrowingTerrainGenerator();
        runtime.ConfigureOwnedTerrainGeneratorForTests(throwingTerrainGenerator);
        BattleState state = BuildState(out BattleUnitState actor);
        SetupRuntime(runtime, content);
        runtime.SetupStateForTests(state);
        BindDecisionBorrowers(runtime, actor);

        bool threwExpectedFailure = false;
        try
        {
            runtime.Dispose();
        }
        catch (InvalidOperationException exception)
        {
            threwExpectedFailure = exception.Message.Contains(
                "expected terrain teardown failure",
                StringComparison.Ordinal
            );
        }

        _test.True(threwExpectedFailure, "owned resource close failure remains visible to the caller");
        _test.Eq(throwingTerrainGenerator.DisposeAttemptCount, 1, "failing owner closes exactly once");
        AssertRuntimeCleared(runtime, state, "exceptional teardown");
        AssertAuditBaseline(baseline, "exceptional teardown");

        LifecycleAuditSnapshot afterFailure = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        runtime.Dispose();
        _test.Eq(throwingTerrainGenerator.DisposeAttemptCount, 1, "double Dispose does not retry failed owner");
        AssertAuditEqual(afterFailure, LifecycleAuditRegistry.Shared.CaptureSnapshot(), "exception double Dispose");
    }

    private static void SetupRuntime(BattleRuntimeModule runtime, ContentFixture content)
    {
        runtime.setup(
            skill_definitions: content.SkillDefinitions,
            enemy_templates: content.EnemyTemplates,
            enemy_ai_brains: content.EnemyBrains,
            item_defs: content.ItemDefs,
            equipment_instance_id_allocator: () => "teardown-equipment-instance",
            skill_catalog: new SkillCatalog(null),
            battle_special_profile_view: new ProbeSpecialProfileView(),
            trait_defs: content.TraitDefs,
            equipment_ability_bindings: content.EquipmentBindings
        );
    }

    private static void BindDecisionBorrowers(BattleRuntimeModule runtime, BattleUnitState actor)
    {
        runtime._ai_action_plans_by_unit_id[actor.unit_id] = new BattleAiRuntimeActionPlan();
        BattleAiContext context = runtime._prepare_ai_context_for_decision(actor);
        runtime._bind_ai_helper_services_for_decision(actor, context);
    }

    private static BattleState BuildState(out BattleUnitState actor)
    {
        var state = new BattleState
        {
            battle_id = "borrower_teardown",
            map_size = Vector2I.One,
            phase = "unit_acting",
            timeline = new BattleTimelineState(),
        };
        actor = new BattleUnitState
        {
            unit_id = "borrower_actor",
            display_name = "Borrower Actor",
            faction_id = "player",
            control_mode = "manual",
            current_hp = 10,
            current_ap = 1,
            is_alive = true,
        };
        actor.SetAnchorCoord(Vector2I.Zero);
        state.SetUnit(actor);
        state.ally_unit_ids.Add(actor.unit_id);
        state.active_unit_id = actor.unit_id;
        state.timeline.ready_unit_ids.Add(actor.unit_id);
        return state;
    }

    private ContentFixture LoadContentFixture()
    {
        SkillDef skillResource = RequireResource<SkillDef>(
            "res://data/configs/skills/mage_arcane_aegis.tres"
        );
        SkillDefinition skillDefinition = SkillDefinition.FromResource(skillResource);
        ItemDef itemResource = RequireResource<ItemDef>(
            "res://data/configs/items/whetstone.tres"
        );
        ItemDefinition itemDefinition = itemResource.ToDefinition();
        TraitDef traitResource = RequireResource<TraitDef>(
            "res://data/configs/traits/brave.tres"
        );
        TraitDefinition traitDefinition = TraitDefinition.FromResource(traitResource);
        EnemyTemplateDef enemyTemplate = RequireResource<EnemyTemplateDef>(
            "res://data/configs/enemies/templates/zombie_shambler.tres"
        );
        EnemyAiBrainDef enemyBrain = RequireResource<EnemyAiBrainDef>(
            "res://data/configs/enemies/brains/melee_aggressor.tres"
        );
        var equipmentBinding = new EquipmentAbilityBindingDefinition
        {
            BindingId = "borrower_teardown_binding",
            TraitId = traitDefinition.TraitId,
        };

        return new ContentFixture(
            new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            },
            new Dictionary<StringName, ItemDefinition>
            {
                [itemDefinition.ItemId] = itemDefinition,
            },
            new Dictionary<StringName, TraitDefinition>
            {
                [traitDefinition.TraitId] = traitDefinition,
            },
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [equipmentBinding.BindingId] = equipmentBinding,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [enemyTemplate.template_id] = enemyTemplate.ToDefinition(
                    new Dictionary<StringName, ItemDefinition>
                    {
                        [itemDefinition.ItemId] = itemDefinition,
                    }
                ),
            },
            new Dictionary<StringName, EnemyAiBrainDefinition>
            {
                [enemyBrain.brain_id] = enemyBrain.ToDefinition(),
            }
        );
    }

    private static T RequireResource<T>(string path)
        where T : Resource
    {
        return GD.Load<T>(path)
            ?? throw new InvalidOperationException($"Missing teardown fixture resource: {path}");
    }

    private void AssertRuntimeCleared(BattleRuntimeModule runtime, BattleState state, string label)
    {
        _test.True(runtime.IsDisposed, $"{label}: runtime reports disposed");
        _test.True(!runtime.HasAiRuntimeBorrowers, $"{label}: decision/plan borrowers clear");
        _test.True(!runtime.HasRuntimeSidecarBindings, $"{label}: sidecar runtime borrowers clear");
        _test.True(!runtime.HasContentCatalogBorrowers, $"{label}: content borrowers clear");
        _test.Eq(runtime.GetSkillDefinitionIndexTyped().Count, 0, $"{label}: skill index zero");
        _test.Eq(runtime.GetTraitDefIndexTyped().Count, 0, $"{label}: trait index zero");
        _test.Eq(
            runtime.GetEquipmentAbilityBindingIndexTyped().Count,
            0,
            $"{label}: equipment binding index zero"
        );
        _test.Eq(runtime.GetItemDefIndexTyped().Count, 0, $"{label}: item index zero");
        _test.Eq(runtime.GetEnemyTemplateIndexTyped().Count, 0, $"{label}: enemy template index zero");
        _test.Eq(runtime.GetEnemyAiBrainIndexTyped().Count, 0, $"{label}: enemy brain index zero");
        _test.True(runtime.GetState() == null, $"{label}: runtime state reference clear");
        _test.Eq(state.GetUnitsTyped().Count, 0, $"{label}: state unit topology clear");
        _test.Eq(state.ally_unit_ids.Count, 0, $"{label}: ally index clear");
        _test.Eq(state.enemy_unit_ids.Count, 0, $"{label}: enemy index clear");
        _test.Eq(state.timeline.ready_unit_ids.Count, 0, $"{label}: ready-unit index clear");
    }

    private void AssertAuditBaseline(LifecycleAuditSnapshot expected, string label) =>
        AssertAuditEqual(expected, LifecycleAuditRegistry.Shared.CaptureSnapshot(), label);

    private void AssertAuditEqual(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owner count");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: lease count");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label}: scope count");
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            expected.ActiveContentBorrowerCount,
            $"{label}: content borrower count"
        );
    }

    private sealed record ContentFixture(
        IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions,
        IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs,
        IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentBindings,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyBrains
    );
}
