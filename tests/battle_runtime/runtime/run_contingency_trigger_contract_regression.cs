using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_trigger_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<BattleRuntimeModule> _runtimeFixtures = new();
    private readonly List<CharacterManagementModule> _managerFixtures = new();

    public override void _Initialize()
    {
        try
        {
            TestBattleLocalSidecarAndReleaseOverlay();
            TestOwnerTurnStartedHookQueuesOnlyTriggeringOwner();
            TestNonDamageHookFactsQueueMatchingTriggersAndFreezeSourceFacts();
            TestNonDamageHookFactsIgnoreAllySourcesAndSuppressedOrigins();
            TestSameOwnerSourceEventQueuesOneRelease();
            TestRuntimeAoeSpellEmitterFreezesMatchedOwnerAsTriggerTarget();
            TestTriggerCandidateIndexSnapshotUsesRelevantKindsAndDeterministicOrder();
            TestSuppressedAndDepletedReportVocabulary();
        }
        catch (Exception ex)
        {
            _test.Fail($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
        }

        CleanupFixtures();
        RequestTestExit(_test.Finish("Contingency trigger contract regression"));
    }

    private void TestBattleLocalSidecarAndReleaseOverlay()
    {
        PartyState partyState = BuildPartyState();
        PartyMemberState activeMember = partyState.GetMemberState("hero");
        PartyMemberState reserveMember = partyState.GetMemberState("reserve_mage");

        using CharacterManagementModule manager = BuildManager(partyState);
        using BattleRuntimeModule runtime = new();
        runtime.setup(character_gateway: manager);

        BattleUnitState heroUnit = BuildBattleUnit(
            "hero_unit",
            "hero",
            manager.GetMemberAttributeSnapshotForEquipmentView("hero", new EquipmentState())
        );
        BattleState battleState = BuildBattleState(heroUnit);
        runtime.SetupStateForTests(battleState);

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        _test.True(sidecar != null, "BattleRuntimeModule should expose the battle-local contingency sidecar.");
        if (sidecar == null)
            return;

        IReadOnlyList<BattleContingencyInstance> instances = sidecar.GetInstancesTyped();
        _test.Eq(instances.Count, 1, "Only active charged members should create battle-local contingency instances.");
        BattleContingencyInstance instance = instances.Count > 0 ? instances[0] : null;
        _test.Eq(instance?.OwnerMemberId ?? new StringName(""), new StringName("hero"), "Instance should store owner member id.");
        _test.Eq(instance?.OwnerUnitId ?? new StringName(""), new StringName("hero_unit"), "Instance should store owner battle unit id.");
        _test.Eq(instance?.CasterUnitId ?? new StringName(""), new StringName("hero_unit"), "Initial caster unit id should equal owner unit id.");
        _test.False(
            sidecar.HasInstanceForSetup("reserve_mage", "reserve_setup"),
            "Reserve charged setups should remain party reservations, not battle-local instances."
        );
        _test.Eq(
            reserveMember.GetTotalReservedMpMax(),
            8,
            "Reserve member should keep persistent MP reservation while absent from the battle sidecar."
        );

        _test.Eq(
            heroUnit.attribute_snapshot.GetValue(AttributeService.MP_MAX),
            18,
            "Before release overlay, active owner MP max should include persistent contingency reservation."
        );

        ContingencyReleaseContext releaseContext = sidecar.EnterReleaseContext(instance?.InstanceId ?? "");
        _test.True(releaseContext.IsValid, "Entering release context should create a valid release context.");
        _test.Eq(releaseContext.OwnerMemberId, new StringName("hero"), "Release context should preserve owner member id.");
        _test.True(
            sidecar.IsSetupConsumedForMember("hero", "active_setup"),
            "Release context should mark setup consumed in the sidecar overlay."
        );
        _test.True(
            ContainsStringName(heroUnit.GetConsumedContingencySetupIdsTyped(), "active_setup"),
            "Release context should bridge consumed setup ids to BattleUnitState finalization state."
        );
        _test.Eq(
            heroUnit.attribute_snapshot.GetValue(AttributeService.RESERVED_MP_MAX),
            0,
            "Release overlay refresh should remove owner reservation from the battle-local snapshot."
        );
        _test.Eq(
            heroUnit.attribute_snapshot.GetValue(AttributeService.MP_MAX),
            30,
            "Release overlay refresh should restore owner effective MP max for the current battle."
        );

        GDictionary unitPayload = heroUnit.ToDictionary();
        _test.False(
            unitPayload.ContainsKey("contingency_instances"),
            "Battle-local contingency instances must not enter BattleUnitState.ToDictionary()."
        );
        _test.False(
            unitPayload.ContainsKey("consumed_contingency_setup_ids"),
            "Consumed contingency overlay must not enter BattleUnitState save payload."
        );

        runtime.Dispose();
        AssertSetupStillCharged(activeMember, "active_setup", 12, "Uncommitted battle disposal should not mutate active member setup.");
        AssertSetupStillCharged(reserveMember, "reserve_setup", 8, "Uncommitted battle disposal should not mutate reserve member setup.");
    }

    private void TestOwnerTurnStartedHookQueuesOnlyTriggeringOwner()
    {
        PartyState partyState = BuildOwnerTurnPartyState();
        using CharacterManagementModule manager = BuildManager(partyState);

        using BattleRuntimeModule runtimeFromTurnProgression = new();
        runtimeFromTurnProgression.setup(character_gateway: manager);
        BattleUnitState hookHeroUnit = BuildBattleUnit(
            "hero_unit",
            "hero",
            manager.GetMemberAttributeSnapshotForEquipmentView("hero", new EquipmentState()),
            Vector2I.Zero
        );
        BattleUnitState hookClericUnit = BuildBattleUnit(
            "cleric_unit",
            "cleric",
            manager.GetMemberAttributeSnapshotForEquipmentView("cleric", new EquipmentState()),
            new Vector2I(1, 0)
        );
        runtimeFromTurnProgression.SetupStateForTests(BuildBattleState(new[] { hookHeroUnit, hookClericUnit }));
        BattleContingencySystem hookSidecar = runtimeFromTurnProgression.GetContingencySystemTyped();

        _test.Eq(
            hookSidecar.GetInstancesTyped().Count,
            2,
            "Two active owner-turn members should create two battle-local contingency instances."
        );
        using BattleEventBatch ownerTurnBatch = new();
        runtimeFromTurnProgression._record_turn_started(hookHeroUnit, ownerTurnBatch);
        _test.Eq(
            hookSidecar.GetQueuedReleaseContextsTyped().Count,
            0,
            "Real battle turn starts should release owner-turn contexts during the turn-start batch."
        );
        AssertV1ReportEntry(
            FindReportEntry(ownerTurnBatch, "contingency_triggered"),
            "triggered",
            "trigger_matched",
            "hero_turn_setup",
            "owner_turn_started",
            "Owner-turn trigger report"
        );
        AssertV1ReportEntry(
            FindReportEntry(ownerTurnBatch, "contingency_released"),
            "released",
            "ok",
            "hero_turn_setup",
            "owner_turn_started",
            "Owner-turn release report"
        );

        using BattleRuntimeModule runtimeFromDirectSidecar = new();
        runtimeFromDirectSidecar.setup(character_gateway: manager);
        BattleUnitState directHeroUnit = BuildBattleUnit(
            "hero_unit",
            "hero",
            manager.GetMemberAttributeSnapshotForEquipmentView("hero", new EquipmentState()),
            Vector2I.Zero
        );
        BattleUnitState directClericUnit = BuildBattleUnit(
            "cleric_unit",
            "cleric",
            manager.GetMemberAttributeSnapshotForEquipmentView("cleric", new EquipmentState()),
            new Vector2I(1, 0)
        );
        runtimeFromDirectSidecar.SetupStateForTests(BuildBattleState(new[] { directHeroUnit, directClericUnit }));
        BattleContingencySystem directSidecar = runtimeFromDirectSidecar.GetContingencySystemTyped();

        using BattleEventBatch directOwnerTurnBatch = new();
        directSidecar.OnOwnerTurnStarted(directHeroUnit, directOwnerTurnBatch);
        AssertSingleOwnerTurnContext(
            directSidecar.GetQueuedReleaseContextsTyped(),
            "hero_unit",
            "hero_turn_setup",
            "Owner-turn queueing should ignore other members' owner-turn instances."
        );
        AssertV1ReportEntry(
            FindReportEntry(directOwnerTurnBatch, "contingency_triggered"),
            "triggered",
            "trigger_matched",
            "hero_turn_setup",
            "owner_turn_started",
            "Direct owner-turn trigger report"
        );
    }

    private void AssertSingleOwnerTurnContext(
        IReadOnlyList<ContingencyReleaseContext> queuedContexts,
        StringName ownerUnitId,
        StringName setupId,
        string message
    )
    {
        _test.Eq(queuedContexts.Count, 1, $"{message} queue count mismatch.");
        ContingencyReleaseContext context = queuedContexts.Count > 0 ? queuedContexts[0] : null;
        _test.Eq(context?.OwnerUnitId ?? new StringName(""), ownerUnitId, $"{message} owner unit mismatch.");
        _test.Eq(context?.TriggeringUnitId ?? new StringName(""), ownerUnitId, $"{message} triggering unit mismatch.");
        _test.Eq(context?.SetupId ?? new StringName(""), setupId, $"{message} setup mismatch.");
        _test.Eq(context?.TriggerType ?? new StringName(""), new StringName("owner_turn_started"), $"{message} trigger mismatch.");
    }

    private void TestNonDamageHookFactsQueueMatchingTriggersAndFreezeSourceFacts()
    {
        CharacterManagementModule manager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("hp_hook", reservedMpMax: 2, "hp_below_percent"),
                ChargedSetup("status_hook", reservedMpMax: 2, "status_applied"),
                ChargedSetup("radius_hook", reservedMpMax: 2, "enemy_enter_radius"),
                ChargedSetup("spell_hook", reservedMpMax: 2, "affected_by_spell")
            )
        ));
        BattleRuntimeModule runtime = TrackRuntime(BuildHookRuntime(manager));
        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();

        sidecar.OnHookFact(
            ContingencyHookFact.HpChanged(
                "hp_event",
                "enemy_unit",
                "hero_unit",
                previousHp: 12,
                currentHp: 5,
                maxHp: 20,
                BattleEffectOrigin.PlayerCommand()
            )
        );
        AssertQueuedContext(sidecar, index: 0, "hero_unit", "hp_hook", "hp_below_percent", "enemy_unit");

        sidecar.OnHookFact(
            ContingencyHookFact.StatusApplied(
                "status_event",
                "enemy_unit",
                "hero_unit",
                new[] { new StringName("burning") },
                BattleEffectOrigin.PlayerCommand()
            )
        );
        AssertQueuedContext(sidecar, index: 1, "hero_unit", "status_hook", "status_applied", "enemy_unit");

        sidecar.OnHookFact(
            ContingencyHookFact.PositionChanged(
                "move_event",
                "enemy_unit",
                new Vector2I(5, 0),
                new Vector2I(1, 0),
                BattleEffectOrigin.PlayerCommand()
            )
        );
        AssertQueuedContext(sidecar, index: 2, "hero_unit", "radius_hook", "enemy_enter_radius", "enemy_unit");

        sidecar.OnHookFact(
            ContingencyHookFact.SpellAffected(
                "spell_event",
                "enemy_unit",
                "hero_unit",
                new[] { new StringName("hero_unit") },
                BattleEffectOrigin.PlayerCommand()
            )
        );
        AssertQueuedContext(sidecar, index: 3, "hero_unit", "spell_hook", "affected_by_spell", "enemy_unit");

        ContingencyReleaseContext spellContext = sidecar.GetQueuedReleaseContextsTyped()[3];
        GDictionary snapshot = sidecar.BuildSnapshot();
        _test.Eq(
            GetInt(snapshot, "release_queue_count", -1),
            4,
            "Contingency snapshot should expose release queue count."
        );
        _test.Eq(
            GetInt(snapshot, "sequential_auto_cast_queue_count", -1),
            0,
            "Contingency snapshot should expose sequential auto-cast queue count."
        );
        _test.Eq(
            spellContext.FrozenFacts.TriggerSourceUnitId,
            new StringName("enemy_unit"),
            "Queued spell trigger should freeze source unit before release."
        );
        _test.Eq(
            spellContext.FrozenFacts.TriggerTargetUnitId,
            new StringName("hero_unit"),
            "Queued spell trigger should freeze affected owner as target before release."
        );

        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");
        enemy.SetAnchorCoord(new Vector2I(4, 4));
        _test.Eq(
            spellContext.FrozenFacts.TriggerSourceCell,
            new Vector2I(5, 0),
            "Frozen source facts should not follow later auto-cast or runtime mutations."
        );
    }

    private void TestNonDamageHookFactsIgnoreAllySourcesAndSuppressedOrigins()
    {
        CharacterManagementModule manager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("radius_hook", reservedMpMax: 2, "enemy_enter_radius"),
                ChargedSetup("spell_hook", reservedMpMax: 2, "affected_by_spell")
            )
        ));
        BattleRuntimeModule runtime = TrackRuntime(BuildHookRuntime(manager));
        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();

        sidecar.OnHookFact(
            ContingencyHookFact.PositionChanged(
                "ally_move",
                "ally_unit",
                new Vector2I(5, 0),
                new Vector2I(1, 0),
                BattleEffectOrigin.PlayerCommand()
            )
        );
        sidecar.OnHookFact(
            ContingencyHookFact.SpellAffected(
                "ally_spell",
                "ally_unit",
                "hero_unit",
                new[] { new StringName("hero_unit") },
                BattleEffectOrigin.PlayerCommand()
            )
        );
        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            0,
            "Owner summon / ally movement and ally spell facts should not trigger hostile-source contingencies."
        );

        sidecar.OnHookFact(
            ContingencyHookFact.PositionChanged(
                "hostile_summon",
                "enemy_unit",
                new Vector2I(5, 0),
                new Vector2I(1, 0),
                BattleEffectOrigin.PlayerCommand()
            )
        );
        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            1,
            "Hostile summon or hostile source entering owner radius should trigger owner contingency."
        );

        sidecar.OnHookFact(
            ContingencyHookFact.SpellAffected(
                "suppressed_spell",
                "enemy_unit",
                "hero_unit",
                new[] { new StringName("hero_unit") },
                BattleEffectOrigin.AutoCast(
                    new AutoCastRequest
                    {
                        CasterUnitId = "hero_unit",
                        OwnerMemberId = "hero",
                        OwnerUnitId = "hero_unit",
                        SetupId = "radius_hook",
                        InstanceId = "hero:radius_hook",
                        StoredSkillId = "mage_mirror_image",
                        CastLevel = 1,
                        TargetResolution = ContingencyTargetResolutionResult.UnitTarget(
                            "hero_unit",
                            Vector2I.Zero
                        ),
                        ReleaseContext = new ContingencyReleaseContext
                        {
                            InstanceId = "hero:radius_hook",
                            SetupId = "radius_hook",
                            OwnerMemberId = "hero",
                            OwnerUnitId = "hero_unit",
                            CasterUnitId = "hero_unit",
                            TriggerType = "enemy_enter_radius",
                        },
                    }
                )
            )
        );
        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            1,
            "Auto-cast facts with CanTriggerContingencies=false must not enqueue nested releases."
        );
    }

    private void TestSameOwnerSourceEventQueuesOneRelease()
    {
        CharacterManagementModule manager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("spell_hook", reservedMpMax: 2, "affected_by_spell")
            )
        ));
        BattleRuntimeModule runtime = TrackRuntime(BuildHookRuntime(manager));
        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();

        sidecar.OnHookFact(
            ContingencyHookFact.SpellAffected(
                "shared_spell_event",
                "enemy_unit",
                "hero_unit",
                new[] { new StringName("hero_unit") },
                BattleEffectOrigin.PlayerCommand()
            )
        );
        sidecar.OnHookFact(
            ContingencyHookFact.SpellAffected(
                "shared_spell_event",
                "enemy_unit",
                "hero_unit",
                new[] { new StringName("hero_unit") },
                BattleEffectOrigin.PlayerCommand()
            )
        );

        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            1,
            "Same owner and same source_event_id should produce one stable release queue entry."
        );
    }

    private void TestRuntimeAoeSpellEmitterFreezesMatchedOwnerAsTriggerTarget()
    {
        CharacterManagementModule manager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("aoe_trigger_target", reservedMpMax: 2, "affected_by_spell", "trigger_target")
            )
        ));
        BattleRuntimeModule runtime = TrackRuntime(BuildHookRuntime(manager));
        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");

        runtime.EmitContingencySpellAffected(
            enemy,
            null,
            new[] { new StringName("hero_unit") },
            "runtime_aoe_spell",
            new[] { new Vector2I(0, 0), new Vector2I(1, 0) }
        );

        IReadOnlyList<ContingencyReleaseContext> queued = sidecar.GetQueuedReleaseContextsTyped();
        _test.Eq(queued.Count, 1, "Runtime AoE spell emitter should queue the affected owner contingency.");
        ContingencyReleaseContext context = queued.Count > 0 ? queued[0] : null;
        _test.Eq(
            context?.FrozenFacts?.TriggerTargetUnitId ?? new StringName(""),
            new StringName("hero_unit"),
            "AoE owner match with no direct target should freeze the matched owner as trigger target."
        );
        _test.Eq(
            context?.FrozenFacts?.TriggerTargetCell ?? new Vector2I(-1, -1),
            Vector2I.Zero,
            "AoE owner match with no direct target should freeze the matched owner's cell."
        );

        IReadOnlyList<ContingencyTargetResolutionResult> targets =
            runtime.ResolveContingencyStoredSpellTargetsForRelease(
                context,
                context?.FrozenFacts ?? ContingencyFrozenTriggerFacts.Empty
            );
        _test.True(
            targets.Count == 1 && targets[0].Ok && targets[0].TargetUnitId == new StringName("hero_unit"),
            "Stored spell trigger_target resolver should resolve from frozen AoE owner facts."
        );
    }

    private void TestTriggerCandidateIndexSnapshotUsesRelevantKindsAndDeterministicOrder()
    {
        CharacterManagementModule manager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("spell_b", reservedMpMax: 2, "affected_by_spell"),
                ChargedSetup("hp_a", reservedMpMax: 2, "hp_below_percent"),
                ChargedSetup("spell_a", reservedMpMax: 2, "affected_by_spell")
            )
        ));
        BattleRuntimeModule runtime = TrackRuntime(BuildHookRuntime(manager));
        GDictionary snapshot = runtime.GetContingencySystemTyped().BuildSnapshot();
        GArray instances = GetArray(snapshot, "instances");
        GDictionary firstInstance = FindSnapshotInstance(instances, "spell_a");
        _test.Eq(
            GetString(firstInstance, "trigger_type"),
            "affected_by_spell",
            "Instance snapshot should expose trigger type."
        );
        _test.Eq(
            GetString(firstInstance, "release_mode"),
            "burst_release",
            "Instance snapshot should expose release mode."
        );
        _test.Eq(
            GetArray(firstInstance, "stored_spells").Count,
            1,
            "Instance snapshot should expose stored spell state."
        );
        GDictionary index = GetDict(snapshot, "trigger_candidate_index");

        GArray spellCandidates = GetArray(index, "affected_by_spell");
        _test.Eq(
            spellCandidates.Count,
            2,
            "Trigger index should expose only affected_by_spell candidates for affected_by_spell hooks."
        );
        if (spellCandidates.Count < 2)
            return;
        _test.Eq(
            spellCandidates[0].ToString(),
            "hero:spell_a",
            "Trigger index should sort affected_by_spell candidate ids deterministically."
        );
        _test.Eq(
            spellCandidates[1].ToString(),
            "hero:spell_b",
            "Trigger index should preserve deterministic order for same-kind candidates."
        );
        _test.Eq(
            GetArray(index, "hp_below_percent").Count,
            1,
            "Trigger index should keep hp_below_percent candidates in a separate relevant list."
        );
    }

    private void TestSuppressedAndDepletedReportVocabulary()
    {
        CharacterManagementModule suppressedManager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("suppressed_combat", reservedMpMax: 2, "combat_started")
            )
        ));
        BattleRuntimeModule suppressedRuntime = TrackRuntime(BuildHookRuntime(suppressedManager));
        BattleContingencySystem suppressedSidecar = suppressedRuntime.GetContingencySystemTyped();
        IReadOnlyList<BattleContingencyInstance> suppressedInstances =
            suppressedSidecar.GetInstancesTyped();
        foreach (BattleContingencyInstance instance in suppressedInstances)
        {
            if (instance?.SetupId == new StringName("suppressed_combat"))
            {
                instance.SetSuppressed(true);
                break;
            }
        }
        using BattleEventBatch suppressedBatch = new();
        suppressedRuntime.OnBattleConfirmed(suppressedBatch);
        AssertV1ReportEntry(
            FindReportEntry(suppressedBatch, "contingency_suppressed"),
            "suppressed",
            "instance_suppressed",
            "suppressed_combat",
            "combat_started",
            "Suppressed instance report"
        );

        CharacterManagementModule depletedManager = TrackManager(BuildManager(
            BuildPartyStateWithSetups(
                ChargedSetup("depleted_combat", reservedMpMax: 2, "combat_started")
            )
        ));
        BattleRuntimeModule depletedRuntime = TrackRuntime(BuildHookRuntime(depletedManager));
        using BattleEventBatch firstBatch = new();
        depletedRuntime.OnBattleConfirmed(firstBatch);
        using BattleEventBatch depletedBatch = new();
        depletedRuntime.OnBattleConfirmed(depletedBatch);
        AssertV1ReportEntry(
            FindReportEntry(depletedBatch, "contingency_depleted"),
            "depleted",
            "setup_already_consumed",
            "depleted_combat",
            "combat_started",
            "Depleted setup report"
        );
    }

    private void AssertQueuedContext(
        BattleContingencySystem sidecar,
        int index,
        StringName ownerUnitId,
        StringName setupId,
        StringName triggerType,
        StringName triggeringUnitId
    )
    {
        IReadOnlyList<ContingencyReleaseContext> queued = sidecar.GetQueuedReleaseContextsTyped();
        _test.True(queued.Count > index, $"Expected queued context at index {index}.");
        ContingencyReleaseContext context = queued.Count > index ? queued[index] : null;
        _test.Eq(context?.OwnerUnitId ?? new StringName(""), ownerUnitId, "Queued context owner mismatch.");
        _test.Eq(context?.SetupId ?? new StringName(""), setupId, "Queued context setup mismatch.");
        _test.Eq(context?.TriggerType ?? new StringName(""), triggerType, "Queued context trigger mismatch.");
        _test.Eq(context?.TriggeringUnitId ?? new StringName(""), triggeringUnitId, "Queued context source mismatch.");
    }

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private void AssertV1ReportEntry(
        GDictionary entry,
        string decision,
        string reasonId,
        string setupId,
        string triggerType,
        string message
    )
    {
        _test.True(entry.Count > 0, $"{message} should exist.");
        if (entry.Count == 0)
            return;
        _test.Eq(GetString(entry, "decision"), decision, $"{message} decision mismatch.");
        _test.Eq(GetString(entry, "reason_id"), reasonId, $"{message} reason mismatch.");
        _test.Eq(GetString(entry, "owner_member_id"), "hero", $"{message} owner member mismatch.");
        _test.True(GetString(entry, "owner_unit_id") != "", $"{message} owner unit should be present.");
        _test.Eq(GetString(entry, "setup_id"), setupId, $"{message} setup mismatch.");
        _test.True(entry.ContainsKey("source_event_id"), $"{message} should expose source_event_id.");
        _test.True(entry.ContainsKey("damage_event_id"), $"{message} should expose damage_event_id.");
        _test.Eq(GetString(entry, "trigger_type"), triggerType, $"{message} trigger mismatch.");
        _test.True(GetString(entry, "release_mode") != "", $"{message} release mode should be present.");
        _test.True(entry.ContainsKey("stored_skill_id"), $"{message} should expose stored_skill_id.");
        _test.True(entry.ContainsKey("target_resolver"), $"{message} should expose target_resolver.");
    }

    private static GDictionary FindReportEntry(BattleEventBatch batch, string entryType)
    {
        foreach (GDictionary entry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
            if (GetString(entry, "entry_type") == entryType)
                return entry;
        return new GDictionary();
    }

    private static GDictionary FindSnapshotInstance(GArray instances, string setupId)
    {
        foreach (GDictionary instance in Dictionaries(instances))
            if (GetString(instance, "setup_id") == setupId)
                return instance;
        return new GDictionary();
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        foreach (Variant value in values ?? new GArray())
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
    }

    private BattleRuntimeModule TrackRuntime(BattleRuntimeModule runtime)
    {
        if (runtime != null)
            _runtimeFixtures.Add(runtime);
        return runtime;
    }

    private CharacterManagementModule TrackManager(CharacterManagementModule manager)
    {
        if (manager != null)
            _managerFixtures.Add(manager);
        return manager;
    }

    private void CleanupFixtures()
    {
        foreach (BattleRuntimeModule runtime in _runtimeFixtures)
            runtime?.Dispose();
        _runtimeFixtures.Clear();
        foreach (CharacterManagementModule manager in _managerFixtures)
            manager?.Dispose();
        _managerFixtures.Clear();
    }

    private void AssertSetupStillCharged(
        PartyMemberState member,
        StringName setupId,
        int reservedMpMax,
        string message
    )
    {
        _test.True(member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup), $"{message} setup should exist.");
        if (setup == null)
            return;
        _test.True(setup.Charged, $"{message} setup should remain charged.");
        _test.Eq(setup.ReservedMpMax, reservedMpMax, $"{message} reserved MP should remain persistent.");
    }

    private static BattleState BuildBattleState(BattleUnitState heroUnit)
    {
        return BuildBattleState(new[] { heroUnit });
    }

    private static BattleState BuildBattleState(IReadOnlyList<BattleUnitState> allyUnits)
    {
        BattleState battleState = BattleTestFixture.BuildFlatState(
            "contingency_trigger_contract",
            new Vector2I(5, 5)
        );
        battleState.SetPartyBackpackView(new WarehouseState());
        BattleTestFixture.InstallUnits(
            battleState,
            allyUnits,
            new[] { BattleTestFixture.BuildUnit("enemy_unit", "enemy", new Vector2I(4, 4)) }
        );
        return battleState;
    }

    private static BattleRuntimeModule BuildHookRuntime(CharacterManagementModule manager)
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(character_gateway: manager);
        BattleUnitState heroUnit = BuildBattleUnit(
            "hero_unit",
            "hero",
            manager.GetMemberAttributeSnapshotForEquipmentView("hero", new EquipmentState()),
            Vector2I.Zero
        );
        BattleUnitState allyUnit = BuildBattleUnit(
            "ally_unit",
            "ally",
            manager.GetMemberAttributeSnapshotForEquipmentView("ally", new EquipmentState()),
            new Vector2I(2, 0)
        );
        BattleUnitState enemyUnit = BattleTestFixture.BuildUnit("enemy_unit", "enemy", new Vector2I(5, 0));
        BattleState battleState = BattleTestFixture.BuildFlatState(
            "contingency_trigger_hook_contract",
            new Vector2I(6, 6)
        );
        battleState.SetPartyBackpackView(new WarehouseState());
        BattleTestFixture.InstallUnits(battleState, new[] { heroUnit, allyUnit }, new[] { enemyUnit });
        runtime.SetupStateForTests(battleState);
        return runtime;
    }

    private static BattleUnitState BuildBattleUnit(
        StringName unitId,
        StringName memberId,
        AttributeSnapshot snapshot,
        Vector2I? coord = null
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            source_member_id = memberId,
            display_name = "Hero",
            faction_id = "player",
            control_mode = "manual",
            current_hp = 20,
            current_mp = 18,
            current_stamina = 10,
            current_ap = 2,
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            is_alive = true,
            attribute_snapshot = snapshot ?? new AttributeSnapshot(),
        };
        unit.SetEquipmentView(new EquipmentState());
        unit.SetAnchorCoord(coord ?? Vector2I.Zero);
        return unit;
    }

    private static CharacterManagementModule BuildManager(PartyState partyState)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            new Dictionary<StringName, TraitDef>(),
            null,
            new ProgressionIdentityCatalogData()
        );
        return manager;
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            reserve_member_ids = new GStringNameArray { "reserve_mage" },
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(BuildMember("hero", "Hero", ChargedSetup("active_setup", reservedMpMax: 12)));
        partyState.SetMemberState(BuildMember("reserve_mage", "Reserve Mage", ChargedSetup("reserve_setup", reservedMpMax: 8)));
        return partyState;
    }

    private static PartyState BuildOwnerTurnPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero", "cleric" },
            reserve_member_ids = new GStringNameArray(),
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(BuildMember("hero", "Hero", ChargedSetup("hero_turn_setup", reservedMpMax: 12, "owner_turn_started")));
        partyState.SetMemberState(BuildMember("cleric", "Cleric", ChargedSetup("cleric_turn_setup", reservedMpMax: 10, "owner_turn_started")));
        return partyState;
    }

    private static PartyState BuildPartyStateWithSetups(params ContingencyMatrixSetupState[] setups)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero", "ally" },
            reserve_member_ids = new GStringNameArray(),
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState hero = BuildMember("hero", "Hero", setups.Length > 0 ? setups[0] : ChargedSetup("fallback", 1));
        hero = hero.WithContingencySetupsForMutation(setups);
        partyState.SetMemberState(hero);
        partyState.SetMemberState(BuildMember("ally", "Ally", ChargedSetup("ally_unused", reservedMpMax: 1, "owner_turn_started")));
        return partyState;
    }

    private static PartyMemberState BuildMember(
        StringName memberId,
        string displayName,
        ContingencyMatrixSetupState setup
    )
    {
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = displayName,
            progression = MakeProgress(memberId),
            current_hp = 20,
            current_mp = 5,
            current_aura = 0,
        };
        return member.WithContingencySetupsForMutation(new[] { setup });
    }

    private static UnitProgress MakeProgress(StringName memberId)
    {
        UnitProgress progress = new()
        {
            unit_id = memberId,
            display_name = memberId.ToString(),
        };
        progress.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 30);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 0);
        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = "mage_chain_contingency",
                is_learned = true,
                skill_level = 5,
            }
        );
        return progress;
    }

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        int reservedMpMax,
        StringName triggerType = default,
        StringName targetResolverType = default
    ) =>
        ContingencyMatrixSetupState.FromDictionary(
            new GDictionary
            {
                ["setup_id"] = setupId,
                ["display_name"] = "Emergency Matrix",
                ["enabled"] = true,
                ["charged"] = true,
                ["source_skill_id"] = "mage_chain_contingency",
                ["source_skill_level"] = 5,
                ["matrix_load"] = 3,
                ["reserved_mp_max"] = reservedMpMax,
                ["material_costs"] = new GArray
                {
                    new GDictionary
                    {
                        ["item_id"] = "special_contingency_gem",
                        ["quantity"] = 1,
                    },
                },
                ["trigger"] = BuildTriggerPayload(triggerType),
                ["release_mode"] = "burst_release",
                ["stored_spells"] = new GArray
                {
                    new GDictionary
                    {
                        ["stored_skill_id"] = "mage_mirror_image",
                        ["cast_level"] = 2,
                        ["order"] = 1,
                        ["target_resolver"] = new GDictionary
                        {
                            ["type"] = targetResolverType == default || targetResolverType == new StringName("")
                                ? "self"
                                : targetResolverType,
                        },
                        ["parameter_bindings"] = new GDictionary(),
                        ["fallback_policy"] = "skip_if_invalid",
                    },
                },
            }
        );

    private static GDictionary BuildTriggerPayload(StringName triggerType)
    {
        triggerType = triggerType == default ? new StringName("") : triggerType;
        if (triggerType == "owner_turn_started")
        {
            return new GDictionary
            {
                ["type"] = "owner_turn_started",
                ["subject"] = "owner",
                ["timing"] = "owner_turn_started",
            };
        }
        if (triggerType == "combat_started")
        {
            return new GDictionary
            {
                ["type"] = "combat_started",
                ["subject"] = "owner",
                ["timing"] = "after_battle_confirmed",
            };
        }
        if (triggerType == "status_applied")
        {
            return new GDictionary
            {
                ["type"] = "status_applied",
                ["subject"] = "owner",
                ["status_tags"] = new GArray { "burning" },
                ["application_match"] = "new_status_only",
                ["timing"] = "after_status_applied",
            };
        }
        if (triggerType == "enemy_enter_radius")
        {
            return new GDictionary
            {
                ["type"] = "enemy_enter_radius",
                ["center"] = "owner",
                ["radius"] = 2,
                ["radius_metric"] = "manhattan",
                ["source_team"] = "hostile",
                ["timing"] = "after_position_changed",
            };
        }
        if (triggerType == "affected_by_spell")
        {
            return new GDictionary
            {
                ["type"] = "affected_by_spell",
                ["subject"] = "owner",
                ["source_team"] = "hostile",
                ["spell_match"] = "any",
                ["timing"] = "before_spell_effect_resolved",
            };
        }
        return new GDictionary
        {
            ["type"] = "hp_below_percent",
            ["subject"] = "owner",
            ["percent"] = 30,
            ["crossing_only"] = true,
            ["timing"] = "after_hp_changed",
        };
    }

    private static GDictionary GetDict(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key) || !dict.ContainsKey(key))
            return new GDictionary();
        return dict[key].AsGodotDictionary();
    }

    private static GArray GetArray(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key) || !dict.ContainsKey(key))
            return new GArray();
        return dict[key].AsGodotArray();
    }

    private static string GetString(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key) || !dict.ContainsKey(key))
            return "";
        return dict[key].AsString();
    }

    private static int GetInt(GDictionary dict, string key, int fallback = 0)
    {
        if (dict == null || string.IsNullOrEmpty(key) || !dict.ContainsKey(key))
            return fallback;
        return dict[key].AsInt32();
    }
}
