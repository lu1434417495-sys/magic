using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_autocast_origin_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<BattleRuntimeModule> _runtimeFixtures = new();
    private readonly List<BattleState> _stateFixtures = new();

    public override void _Initialize()
    {
        try
        {
            TestBurstAutoCastBypassesTurnAndCostsButCommitsEffects();
            TestNonPlayerLearnedSourceDoesNotCreateBattleInstance();
            TestSequentialReleaseQueuesAtReleaseTimeAndDrainsOnePerOwnerTurn();
            TestInvalidTargetResolutionSkipAndAbortGateExecution();
            TestSpecialProfileAutoCastUsesFormalCommitWithoutCostsOrProgression();
        }
        catch (Exception ex)
        {
            _test.Fail($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
        }

        CleanupFixtures();
        Quit(_test.Finish("Contingency auto-cast origin regression"));
    }

    private void TestBurstAutoCastBypassesTurnAndCostsButCommitsEffects()
    {
        SkillDefinition storedSkill = StoredBoltSkill();
        PartyState partyState = BuildPartyState(
            ChargedSetup(
                "combat_burst",
                "combat_started",
                "burst_release",
                new GArray
                {
                    StoredSpell("contingency_bolt", 1, Resolver("nearest_enemy_to_owner")),
                }
            )
        );
        using TrackingBattleGateway gateway = new(partyState);
        BattleUnitState caster = Unit(
            "caster",
            "player",
            Vector2I.Zero,
            "hero",
            knowsStoredSkill: false
        );
        BattleUnitState activeAlly = Unit("active_ally", "player", new Vector2I(0, 1), "ally");
        BattleUnitState target = Unit("enemy", "enemy", new Vector2I(2, 0), "");

        BattleRuntimeModule runtime = Track(new BattleRuntimeModule());
        runtime.setup(
            character_gateway: gateway,
            skill_definitions: new Dictionary<StringName, SkillDefinition> { [storedSkill.SkillId] = storedSkill }
        );
        BattleState state = Track(
            BattleTestFixture.BuildFlatState(
                "contingency_auto_cast_origin",
                new Vector2I(4, 3)
            )
        );
        BattleTestFixture.InstallUnits(state, new[] { caster, activeAlly }, new[] { target });
        state.active_unit_id = activeAlly.unit_id;
        state.phase = "unit_acting";
        runtime.SetupStateForTests(state);
        BattleTestFixture.ConfigureDamageResolverForTests(
            runtime,
            new FixedSuccessOneDamageResolver()
        );

        int casterApBefore = caster.current_ap;
        int casterMpBefore = caster.current_mp;
        int casterStaminaBefore = caster.current_stamina;
        int casterAuraBefore = caster.current_aura;
        _test.False(
            caster.KnowsActiveSkill("contingency_bolt"),
            "fixture should prove the stored spell is absent from caster known_active_skill_ids."
        );
        _test.False(
            caster.HasKnownSkillLevelTyped("contingency_bolt"),
            "fixture should prove the scoped auto-cast level does not come from known_skill_level_map."
        );

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        using BattleEventBatch batch = new();
        runtime.OnBattleConfirmed(batch);

        AssertV1ReportEntry(
            FindReportEntry(batch, "contingency_triggered"),
            "triggered",
            "trigger_matched",
            "combat_burst",
            "combat_started",
            "",
            "",
            "combat-start trigger report"
        );
        AssertV1ReportEntry(
            FindReportEntry(batch, "contingency_released"),
            "released",
            "ok",
            "combat_burst",
            "combat_started",
            "",
            "",
            "combat-start release report"
        );

        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            0,
            "combat_started burst release should execute immediately instead of leaving a queued context."
        );

        _test.True(target.current_hp < 30, "auto-cast should apply formal damage to the resolved target.");
        _test.True(
            target.HasStatusEffect("contingency_marked"),
            "auto-cast should commit ordinary status effects through the formal effect path."
        );
        _test.Eq(caster.current_ap, casterApBefore, "auto-cast should not consume caster AP.");
        _test.Eq(caster.current_mp, casterMpBefore, "auto-cast should not consume caster MP.");
        _test.Eq(caster.current_stamina, casterStaminaBefore, "auto-cast should not consume stamina.");
        _test.Eq(caster.current_aura, casterAuraBefore, "auto-cast should not consume aura.");
        _test.Eq(
            caster.GetCooldownTyped(storedSkill.SkillId),
            0,
            "auto-cast should not place the stored skill on cooldown."
        );
        _test.Eq(
            batch.ProgressionDeltasTyped.Count,
            0,
            "auto-cast should not grant battle mastery progression deltas."
        );
        _test.Eq(
            gateway.SkillUsedAchievementEvents,
            0,
            "auto-cast should not emit ordinary skill_used achievements."
        );
        _test.True(
            HasSuppressedOrigin(batch),
            "damage/status report facts from auto-cast should carry CanTriggerContingencies=false."
        );
        _test.True(
            HasAutoCastOriginSkillEntryId(
                batch,
                "scoped_auto:hero:combat_burst:contingency_bolt"
            ),
            "auto-cast effect origin should expose the scoped skill_entry_id."
        );
        _test.False(
            caster.KnowsActiveSkill("contingency_bolt"),
            "auto-cast should not leave the stored spell in known_active_skill_ids."
        );
        _test.False(
            caster.HasKnownSkillLevelTyped("contingency_bolt"),
            "auto-cast should not leave the scoped level in known_skill_level_map."
        );
        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            0,
            "contingency scanner should not enqueue nested releases from suppressed auto-cast facts."
        );

        runtime.SetupStateForTests(null);
    }

    private void TestNonPlayerLearnedSourceDoesNotCreateBattleInstance()
    {
        SkillDefinition storedSkill = StoredBoltSkill();
        PartyState partyState = BuildPartyState(
            ChargedSetup(
                "race_source",
                "combat_started",
                "burst_release",
                new GArray
                {
                    StoredSpell("contingency_bolt", 1, Resolver("nearest_enemy_to_owner")),
                }
            ),
            sourceGrantType: UnitSkillGrantSourceType.Race
        );
        using TrackingBattleGateway gateway = new(partyState);
        BattleUnitState caster = Unit("race_source_caster", "player", Vector2I.Zero, "hero");
        BattleUnitState target = Unit("race_source_enemy", "enemy", new Vector2I(2, 0), "");

        BattleRuntimeModule runtime = Track(new BattleRuntimeModule());
        runtime.setup(
            character_gateway: gateway,
            skill_definitions: new Dictionary<StringName, SkillDefinition> { [storedSkill.SkillId] = storedSkill }
        );
        BattleState state = Track(BattleTestFixture.BuildFlatState("race_source_gate", new Vector2I(4, 3)));
        BattleTestFixture.InstallUnits(state, new[] { caster }, new[] { target });
        state.active_unit_id = caster.unit_id;
        state.phase = "unit_acting";
        runtime.SetupStateForTests(state);
        BattleTestFixture.ConfigureDamageResolverForTests(
            runtime,
            new FixedSuccessOneDamageResolver()
        );

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        _test.False(
            sidecar.HasInstanceForSetup("hero", "race_source"),
            "battle setup should not create contingency instances for non-player-learned source skills."
        );

        using BattleEventBatch batch = new();
        runtime.OnBattleConfirmed(batch);

        _test.Eq(
            target.current_hp,
            30,
            "non-player-learned contingency source should not execute stored spells."
        );
        _test.Eq(
            CountNonContingencyReports(batch),
            0,
            "blocked non-player source should not emit auto-cast effect reports."
        );

        runtime.SetupStateForTests(null);
    }

    private void TestSequentialReleaseQueuesAtReleaseTimeAndDrainsOnePerOwnerTurn()
    {
        SkillDefinition storedSkill = StoredBoltSkill();
        PartyState partyState = BuildPartyState(
            ChargedSetup(
                "combat_sequence",
                "combat_started",
                "sequential_release",
                new GArray
                {
                    StoredSpell("contingency_bolt", 1, Resolver("nearest_enemy_to_owner")),
                    StoredSpell("contingency_bolt", 2, Resolver("nearest_enemy_to_owner")),
                }
            )
        );
        using TrackingBattleGateway gateway = new(partyState);
        BattleUnitState caster = Unit("caster", "player", Vector2I.Zero, "hero");
        BattleUnitState target = Unit("enemy", "enemy", new Vector2I(2, 0), "");

        BattleRuntimeModule runtime = Track(new BattleRuntimeModule());
        runtime.setup(
            character_gateway: gateway,
            skill_definitions: new Dictionary<StringName, SkillDefinition> { [storedSkill.SkillId] = storedSkill }
        );
        BattleState state = Track(
            BattleTestFixture.BuildFlatState(
                "contingency_sequential_auto_cast_origin",
                new Vector2I(4, 3)
            )
        );
        BattleTestFixture.InstallUnits(state, new[] { caster }, new[] { target });
        state.active_unit_id = caster.unit_id;
        state.phase = "unit_acting";
        runtime.SetupStateForTests(state);
        BattleTestFixture.ConfigureDamageResolverForTests(
            runtime,
            new FixedSuccessOneDamageResolver()
        );

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        using BattleEventBatch releaseBatch = new();
        runtime.OnBattleConfirmed(releaseBatch);

        _test.Eq(
            target.current_hp,
            30,
            "sequential combat-start release should not execute a stored spell at release time."
        );
        _test.Eq(
            sidecar.GetQueuedSequentialAutoCastCount(),
            2,
            "sequential combat-start release should queue all resolved spells for later owner turns."
        );
        _test.Eq(
            CountNonContingencyReports(releaseBatch),
            0,
            "sequential release-time queueing should not emit effect reports."
        );
        AssertV1ReportEntry(
            FindReportEntry(releaseBatch, "contingency_triggered"),
            "triggered",
            "trigger_matched",
            "combat_sequence",
            "combat_started",
            "",
            "",
            "sequential combat-start trigger report"
        );
        AssertV1ReportEntry(
            FindReportEntry(releaseBatch, "contingency_released"),
            "released",
            "sequential_queued",
            "combat_sequence",
            "combat_started",
            "",
            "",
            "sequential combat-start release report"
        );

        using BattleEventBatch firstTurnBatch = new();
        runtime._record_turn_started(caster, firstTurnBatch);
        int hpAfterFirst = target.current_hp;
        _test.True(hpAfterFirst < 30, "first owner-turn hook should execute exactly one queued spell.");
        _test.Eq(
            sidecar.GetQueuedSequentialAutoCastCount(),
            1,
            "first owner-turn hook should leave the second queued spell pending."
        );

        using BattleEventBatch secondTurnBatch = new();
        runtime._record_turn_started(caster, secondTurnBatch);
        _test.True(
            target.current_hp < hpAfterFirst,
            "second owner-turn hook should execute the next queued spell in order."
        );
        _test.Eq(
            sidecar.GetQueuedSequentialAutoCastCount(),
            0,
            "second owner-turn hook should drain the sequential queue."
        );

        runtime.SetupStateForTests(null);
    }

    private void TestInvalidTargetResolutionSkipAndAbortGateExecution()
    {
        SkillDefinition storedSkill = StoredBoltSkill();
        RunInvalidTargetResolutionScenario(
            "skip_invalid_resolution",
            new GArray
            {
                StoredSpell(
                    "contingency_bolt",
                    1,
                    Resolver("trigger_source"),
                    "skip_if_invalid"
                ),
                StoredSpell(
                    "contingency_bolt",
                    2,
                    Resolver("nearest_enemy_to_owner"),
                    "skip_if_invalid"
                ),
            },
            storedSkill,
            shouldDamageTarget: true,
            expectedReportCount: 1,
            "skip_if_invalid should skip the invalid stored spell and execute the later valid spell."
        );
        RunInvalidTargetResolutionScenario(
            "abort_invalid_resolution",
            new GArray
            {
                StoredSpell(
                    "contingency_bolt",
                    1,
                    Resolver("trigger_source"),
                    "abort_remaining_if_invalid"
                ),
                StoredSpell(
                    "contingency_bolt",
                    2,
                    Resolver("nearest_enemy_to_owner"),
                    "skip_if_invalid"
                ),
            },
            storedSkill,
            shouldDamageTarget: false,
            expectedReportCount: 0,
            "abort_remaining_if_invalid should prevent later stored spells from executing."
        );
    }

    private void TestSpecialProfileAutoCastUsesFormalCommitWithoutCostsOrProgression()
    {
        SkillDefinition meteorSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/mage_meteor_swarm.tres",
            "contingency_autocast_origin:mage_meteor_swarm"
        );
        MeteorSwarmProfile meteorProfile = GD.Load<MeteorSwarmProfile>(
            "res://data/configs/skill_special_profiles/profiles/meteor_swarm_profile.tres"
        );
        _test.True(meteorSkill != null, "auto-cast meteor skill fixture should load.");
        _test.True(meteorProfile != null, "auto-cast meteor profile fixture should load.");
        if (meteorSkill == null || meteorProfile == null)
            return;

        PartyState partyState = BuildPartyState(
            ChargedSetup(
                "combat_meteor",
                "combat_started",
                "burst_release",
                new GArray
                {
                    StoredSpell("mage_meteor_swarm", 1, Resolver("owner_centered_area")),
                }
            )
        );
        using TrackingBattleGateway gateway = new(partyState);
        BattleUnitState caster = Unit("meteor_caster", "player", new Vector2I(4, 4), "hero");
        caster.current_mp = 200;
        caster.current_aura = 10;
        caster.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 200);
        caster.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 10);
        BattleUnitState target = Unit("meteor_target", "enemy", new Vector2I(5, 4), "");
        target.current_hp = 160;
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 160);
        SeedBaseAttributesAndDeriveAc(target);
        SeedBaseAttributesAndDeriveAc(caster);

        BattleRuntimeModule runtime = Track(new BattleRuntimeModule());
        runtime.setup(
            character_gateway: gateway,
            skill_definitions: new Dictionary<StringName, SkillDefinition> { [meteorSkill.SkillId] = meteorSkill },
            battle_special_profile_registry_snapshot: BuildMeteorSpecialProfileSnapshot(meteorProfile),
            battle_special_profile_view: BattleSpecialProfileRuntimeView.ForMeteorSwarm(
                "meteor_swarm",
                meteorProfile
            )
        );
        BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
        BattleState state = Track(
            BattleTestFixture.BuildFlatState(
                "contingency_meteor_auto_cast_origin",
                new Vector2I(9, 9)
            )
        );
        BattleTestFixture.InstallUnits(state, new[] { caster }, new[] { target });
        state.active_unit_id = caster.unit_id;
        state.phase = "unit_acting";
        runtime.SetupStateForTests(state);

        int casterApBefore = caster.current_ap;
        int casterMpBefore = caster.current_mp;
        int casterStaminaBefore = caster.current_stamina;
        int casterAuraBefore = caster.current_aura;

        using BattleEventBatch batch = new();
        runtime.OnBattleConfirmed(batch);

        _test.True(
            HasReportEntryKind(batch, "meteor_swarm_impact_summary"),
            $"auto-cast meteor swarm should use the formal special-profile commit report. logs={FormatLogs(batch)} reports={batch.ReportEntriesTyped.Count}"
        );
        _test.True(
            target.current_hp < 160,
            "auto-cast meteor swarm should apply special-profile component damage."
        );
        _test.Eq(caster.current_ap, casterApBefore, "special-profile auto-cast should not consume AP.");
        _test.Eq(caster.current_mp, casterMpBefore, "special-profile auto-cast should not consume MP.");
        _test.Eq(
            caster.current_stamina,
            casterStaminaBefore,
            "special-profile auto-cast should not consume stamina."
        );
        _test.Eq(
            caster.current_aura,
            casterAuraBefore,
            "special-profile auto-cast should not consume aura."
        );
        _test.Eq(
            batch.ProgressionDeltasTyped.Count,
            0,
            "special-profile auto-cast should not grant battle mastery progression deltas."
        );
        _test.Eq(
            gateway.SkillUsedAchievementEvents,
            0,
            "special-profile auto-cast should not emit ordinary skill_used achievements."
        );
        _test.True(
            HasSuppressedOrigin(batch),
            "special-profile auto-cast report facts should carry CanTriggerContingencies=false."
        );

        runtime.SetupStateForTests(null);
    }

    private static bool HasSuppressedOrigin(BattleEventBatch batch)
    {
        foreach (GDictionary reportEntry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
        {
            if (!reportEntry.ContainsKey("effect_origin"))
                continue;
            GDictionary origin = reportEntry["effect_origin"].AsGodotDictionary();
            if (
                origin.ContainsKey("can_trigger_contingencies")
                && !origin["can_trigger_contingencies"].AsBool()
            )
                return true;
        }
        return false;
    }

    private static bool HasAutoCastOriginSkillEntryId(BattleEventBatch batch, string expectedSkillEntryId)
    {
        foreach (GDictionary reportEntry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
        {
            if (!reportEntry.ContainsKey("effect_origin"))
                continue;
            GDictionary origin = reportEntry["effect_origin"].AsGodotDictionary();
            if (
                origin.ContainsKey("origin_kind")
                && origin["origin_kind"].AsString() == "contingency_auto_cast"
                && origin.ContainsKey("skill_entry_id")
                && origin["skill_entry_id"].AsString() == expectedSkillEntryId
            )
                return true;
        }
        return false;
    }

    private static bool HasReportEntryKind(BattleEventBatch batch, string entryKind)
    {
        foreach (GDictionary reportEntry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
        {
            if (DictString(reportEntry, "entry_type", "") == entryKind)
                return true;
        }
        return false;
    }

    private void AssertV1ReportEntry(
        GDictionary entry,
        string decision,
        string reasonId,
        string setupId,
        string triggerType,
        string storedSkillId,
        string targetResolver,
        string message
    )
    {
        _test.True(entry.Count > 0, $"{message} should exist.");
        if (entry.Count == 0)
            return;
        _test.Eq(DictString(entry, "decision", ""), decision, $"{message} decision mismatch.");
        _test.Eq(DictString(entry, "reason_id", ""), reasonId, $"{message} reason mismatch.");
        _test.Eq(DictString(entry, "owner_member_id", ""), "hero", $"{message} owner member mismatch.");
        _test.True(DictString(entry, "owner_unit_id", "") != "", $"{message} owner unit should be present.");
        _test.Eq(DictString(entry, "setup_id", ""), setupId, $"{message} setup mismatch.");
        _test.True(entry.ContainsKey("source_event_id"), $"{message} should expose source_event_id.");
        _test.True(entry.ContainsKey("damage_event_id"), $"{message} should expose damage_event_id.");
        _test.Eq(DictString(entry, "trigger_type", ""), triggerType, $"{message} trigger mismatch.");
        _test.True(DictString(entry, "release_mode", "") != "", $"{message} release mode should be present.");
        _test.Eq(DictString(entry, "stored_skill_id", ""), storedSkillId, $"{message} stored skill mismatch.");
        _test.Eq(DictString(entry, "target_resolver", ""), targetResolver, $"{message} target resolver mismatch.");
    }

    private static GDictionary FindReportEntry(BattleEventBatch batch, string entryType)
    {
        foreach (GDictionary entry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
            if (DictString(entry, "entry_type", "") == entryType)
                return entry;
        return new GDictionary();
    }

    private static int CountNonContingencyReports(BattleEventBatch batch)
    {
        int count = 0;
        foreach (GDictionary reportEntry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
            if (!DictString(reportEntry, "entry_type", "").StartsWith("contingency_", StringComparison.Ordinal))
                count += 1;
        return count;
    }

    private static string FormatLogs(BattleEventBatch batch)
    {
        List<string> logs = new();
        foreach (string value in batch?.LogLinesTyped ?? Array.Empty<string>())
            logs.Add(value);
        return string.Join(" | ", logs);
    }

    private static SkillDefinition StoredBoltSkill() =>
        TestSkillDefinitionProjection.BuildSkill(
            "contingency_bolt",
            "Contingency Bolt",
            TestSkillDefinitionProjection.BuildCombatProfile(
                "contingency_bolt",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "damage",
                        effectTargetTeamFilter: "enemy",
                        damageTag: "physical_slash",
                        power: 1,
                        diceCount: 1,
                        diceSides: 6
                    ),
                    TestSkillDefinitionProjection.BuildEffect(
                        "status",
                        effectTargetTeamFilter: "enemy",
                        statusId: "contingency_marked",
                        power: 1,
                        durationTu: 30
                    ),
                },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 5,
                apCost: 2,
                mpCost: 7,
                staminaCost: 3,
                auraCost: 1,
                cooldownTu: 40
            ),
            maxLevel: 5,
            nonCoreMaxLevel: 5
        );

    private void RunInvalidTargetResolutionScenario(
        string setupId,
        GArray storedSpells,
        SkillDefinition storedSkill,
        bool shouldDamageTarget,
        int expectedReportCount,
        string message
    )
    {
        PartyState partyState = BuildPartyState(
            ChargedSetup(setupId, "combat_started", "burst_release", storedSpells)
        );
        using TrackingBattleGateway gateway = new(partyState);
        BattleUnitState caster = Unit($"{setupId}_caster", "player", Vector2I.Zero, "hero");
        BattleUnitState target = Unit($"{setupId}_enemy", "enemy", new Vector2I(2, 0), "");

        BattleRuntimeModule runtime = Track(new BattleRuntimeModule());
        runtime.setup(
            character_gateway: gateway,
            skill_definitions: new Dictionary<StringName, SkillDefinition> { [storedSkill.SkillId] = storedSkill }
        );
        BattleState state = Track(BattleTestFixture.BuildFlatState(setupId, new Vector2I(4, 3)));
        BattleTestFixture.InstallUnits(state, new[] { caster }, new[] { target });
        state.active_unit_id = caster.unit_id;
        state.phase = "unit_acting";
        runtime.SetupStateForTests(state);
        BattleTestFixture.ConfigureDamageResolverForTests(
            runtime,
            new FixedSuccessOneDamageResolver()
        );

        using BattleEventBatch batch = new();
        runtime.OnBattleConfirmed(batch);

        if (shouldDamageTarget)
            _test.True(target.current_hp < 30, message);
        else
            _test.Eq(target.current_hp, 30, message);
        _test.Eq(
            CountNonContingencyReports(batch),
            expectedReportCount,
            $"{message} effect report count mismatch."
        );
        AssertV1ReportEntry(
            FindReportEntry(batch, "contingency_spell_skipped"),
            "skipped",
            "trigger_source_missing",
            setupId,
            "combat_started",
            "contingency_bolt",
            "trigger_source",
            $"{message} skipped spell report"
        );

        runtime.SetupStateForTests(null);
    }

    private static BattleUnitState Unit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        StringName sourceMemberId,
        bool knowsStoredSkill = true
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(unitId, factionId, coord, currentAp: 2);
        unit.source_member_id = sourceMemberId;
        unit.current_mp = 200;
        unit.current_stamina = 10;
        unit.current_aura = 10;
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 200);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 6);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, AttributeService.BASE_ARMOR_CLASS);
        unit.UnlockCombatResource("mp");
        unit.UnlockCombatResource("stamina");
        unit.UnlockCombatResource("aura");
        if (knowsStoredSkill)
        {
            unit.AddKnownActiveSkill("contingency_bolt");
            unit.known_skill_level_map[new StringName("contingency_bolt")] = 3;
        }
        return unit;
    }

    private static void SeedBaseAttributesAndDeriveAc(BattleUnitState unit)
    {
        StringName[] baseAttributes =
        {
            "strength",
            "agility",
            "constitution",
            "perception",
            "intelligence",
            "willpower",
        };
        foreach (StringName attributeId in baseAttributes)
        {
            if (!unit.attribute_snapshot.HasValue(attributeId))
                unit.attribute_snapshot.SetValue(attributeId, 10);
        }
        if (!unit.attribute_snapshot.HasValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)))
        {
            int agilityModifier = AttributeSnapshot.CalculateScoreModifier(
                unit.attribute_snapshot.GetValue("agility")
            );
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                Math.Clamp(AttributeService.BASE_ARMOR_CLASS + agilityModifier, 1, 99)
            );
        }
    }

    private static PartyState BuildPartyState(
        ContingencyMatrixSetupState setup,
        UnitSkillGrantSourceType sourceGrantType = UnitSkillGrantSourceType.Player
    )
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero", "ally" },
            reserve_member_ids = new GStringNameArray(),
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(Member("hero", setup, sourceGrantType));
        partyState.SetMemberState(Member("ally", null));
        return partyState;
    }

    private static PartyMemberState Member(
        StringName memberId,
        ContingencyMatrixSetupState setup,
        UnitSkillGrantSourceType sourceGrantType = UnitSkillGrantSourceType.Player
    )
    {
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = memberId.ToString(),
            },
            current_hp = 100,
            current_mp = 200,
            current_aura = 10,
        };
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 100);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 200);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.STAMINA_MAX, 10);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 10);
        member.progression.SetSkillProgress(LearnedSkill("mage_chain_contingency", 5, sourceGrantType));
        return setup != null
            ? member.WithContingencySetupsForMutation(new[] { setup })
            : member;
    }

    private static UnitSkillProgress LearnedSkill(
        StringName skillId,
        int level,
        UnitSkillGrantSourceType grantSourceType = UnitSkillGrantSourceType.Player
    ) =>
        new()
        {
            skill_id = skillId,
            is_learned = true,
            skill_level = level,
            current_mastery = 0,
            total_mastery_earned = 0,
            is_core = false,
            granted_source_type = UnitSkillProgress.ToStringName(grantSourceType),
        };

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        StringName triggerType,
        StringName releaseMode,
        GArray storedSpells
    ) =>
        ContingencyMatrixSetupState.FromDictionary(
            new GDictionary
            {
                ["setup_id"] = setupId,
                ["display_name"] = setupId,
                ["enabled"] = true,
                ["charged"] = true,
                ["source_skill_id"] = "mage_chain_contingency",
                ["source_skill_level"] = 5,
                ["matrix_load"] = 3,
                ["reserved_mp_max"] = 5,
                ["material_costs"] = new GArray
                {
                    new GDictionary { ["item_id"] = "special_contingency_gem", ["quantity"] = 1 },
                },
                ["trigger"] = Trigger(triggerType),
                ["release_mode"] = releaseMode.ToString(),
                ["stored_spells"] = storedSpells,
            }
        );

    private static GDictionary Trigger(StringName triggerType)
    {
        string timing = triggerType.ToString() switch
        {
            "combat_started" => "after_battle_confirmed",
            "owner_turn_started" => "owner_turn_started",
            _ => "after_hp_changed",
        };
        return new GDictionary
        {
            ["type"] = triggerType.ToString(),
            ["subject"] = "owner",
            ["timing"] = timing,
        };
    }

    private static GDictionary StoredSpell(
        StringName storedSkillId,
        int order,
        ContingencyTargetResolverState resolver
    ) =>
        StoredSpell(storedSkillId, order, resolver, "skip_if_invalid");

    private static GDictionary StoredSpell(
        StringName storedSkillId,
        int order,
        ContingencyTargetResolverState resolver,
        string fallbackPolicy
    ) =>
        new()
        {
            ["stored_skill_id"] = storedSkillId.ToString(),
            ["cast_level"] = 3,
            ["order"] = order,
            ["target_resolver"] = resolver.ToDictionary(),
            ["parameter_bindings"] = new GDictionary(),
            ["fallback_policy"] = fallbackPolicy,
        };

    private static ContingencyTargetResolverState Resolver(string type) =>
        ContingencyTargetResolverState.FromDictionary(new GDictionary { ["type"] = type });

    private static GDictionary BuildMeteorSpecialProfileSnapshot(MeteorSwarmProfile meteorProfile)
    {
        GDictionary profiles = new()
        {
            ["meteor_swarm"] = new GDictionary
            {
                ["profile_id"] = "meteor_swarm",
                ["runtime_resolver_id"] = "meteor_swarm",
                ["owning_skill_ids"] = new GStringArray { "mage_meteor_swarm" },
                ["profile_resource_path"] = meteorProfile?.ResourcePath ?? "",
                ["presentation_metadata"] = new GDictionary
                {
                    ["display_name"] = "陨星雨",
                    ["coverage_shape_id"] = "square_7x7",
                    ["radius"] = 3,
                },
                ["required_regression_tests"] = new GStringArray(),
            },
        };
        return new GDictionary
        {
            ["ok"] = true,
            ["errors"] = new GStringArray(),
            ["profiles"] = profiles,
            ["profile_id_by_skill_id"] = new GDictionary
            {
                ["mage_meteor_swarm"] = "meteor_swarm",
            },
        };
    }

    private static string DictString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || key == null || !source.ContainsKey(key))
            return fallback;
        return source[key].AsString();
    }

    private BattleRuntimeModule Track(BattleRuntimeModule runtime)
    {
        if (runtime != null)
            _runtimeFixtures.Add(runtime);
        return runtime;
    }

    private BattleState Track(BattleState state)
    {
        if (state != null)
            _stateFixtures.Add(state);
        return state;
    }

    private void CleanupFixtures()
    {
        foreach (BattleRuntimeModule runtime in _runtimeFixtures)
        {
            runtime?.SetupStateForTests(null);
            runtime?.Dispose();
        }
        _runtimeFixtures.Clear();

        foreach (BattleState state in _stateFixtures)
            BattleTestFixture.DisposeBattleState(state);
        _stateFixtures.Clear();
    }

    private sealed class TrackingBattleGateway : IBattleRuntimeCharacterGateway, IDisposable
    {
        private readonly PartyState _partyState;

        internal TrackingBattleGateway(PartyState partyState)
        {
            _partyState = partyState;
        }

        internal int SkillUsedAchievementEvents { get; private set; }

        public PartyState GetPartyState() => _partyState;

        public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDef>();

        public bool HasItemDefCatalog() => false;

        public ItemDef GetItemDef(StringName item_id) => null;

        public PartyMemberState GetMemberState(StringName member_id) =>
            _partyState?.GetMemberState(member_id);

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        )
        {
            AttributeSnapshot snapshot = new();
            snapshot.SetValue(AttributeService.HP_MAX, 100);
            snapshot.SetValue(AttributeService.MP_MAX, 200);
            snapshot.SetValue(AttributeService.STAMINA_MAX, 10);
            snapshot.SetValue(AttributeService.AURA_MAX, 10);
            snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
            return snapshot;
        }

        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName member_id,
            EquipmentState equipment_view
        ) =>
            new();

        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) =>
            BattleEffectiveTraitProjection.Empty;

        public PassiveSourceContext BuildPassiveSourceContext(
            StringName member_id,
            UnitProgress progression_state
        ) =>
            null;

        public CharacterProgressionDelta PromoteProfession(
            StringName member_id,
            StringName profession_id,
            PromotionSelectionData selection
        ) =>
            new();

        public BattleResourceCommitResult CommitBattleResources(
            StringName member_id,
            int current_hp,
            int current_mp,
            int current_aura
        ) =>
            BattleResourceCommitResult.Success(member_id);

        public void CommitBattleDeath(StringName member_id) { }

        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName member_id,
            StringName skill_id,
            int amount
        ) =>
            new();

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName member_id,
            StringName skill_id,
            int amount,
            StringName source_type,
            string source_label,
            string reason_text,
            bool emit_achievement_event
        ) =>
            new();

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount
        ) =>
            new();

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount,
            StringName subject_id,
            GDictionary meta
        )
        {
            if (event_type == "skill_used")
                SkillUsedAchievementEvents++;
            return new GStringNameArray();
        }

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName member_id,
            StringName source_type,
            string source_label,
            IEnumerable<PendingCharacterRewardEntry> entry_options,
            string summary_text
        ) =>
            null;

        public void Dispose() { }
    }
}
