using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_contingency_target_resolver_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly ContingencyTargetResolverService _resolver = new();

    public override void _Initialize()
    {
        try
        {
            TestSelfResolverRequiresLiveOwner();
            TestTriggerSourceUsesFrozenSourceCell();
            TestTriggerSourceAndTargetRequireFrozenCells();
            TestTriggerTargetFailsWhenGone();
            TestNearestEnemyToOwnerUsesCurrentOwnerCellAndUnitIdTieBreak();
            TestNearestEnemyToTriggerCellUsesFrozenTriggerCellAndUnitIdTieBreak();
            TestOwnerCenteredAreaUsesOwnerCurrentCellAndSkillArea();
            TestAttackerCellUsesFrozenSourceCell();
            TestEmptyCellRejectsIllegalCells();
            TestEmptyCellAwayFromTriggerSourceScoresLegalCells();
            TestEmptyCellCoordinateTieBreakUsesRowMajorAscendingOrder();
            TestEmptyCellSafeCellPrefersOutsideCurrentDamageArea();
            TestEmptyCellFallsBackWhenNoSafeCellExists();
            TestEmptyCellFailureDoesNotUnconsumeEnteredReleaseContext();
            TestStoredSpellFallbackPoliciesGateLaterResolution();
            TestFrozenAndResultAreaListsAreImmutableCopies();
            TestFatalDamageEscapeFlagRequiresLeavingCurrentDamageArea();
        }
        catch (Exception ex)
        {
            _test.Fail($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
        }

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Contingency target resolver regression"));
    }

    private void TestSelfResolverRequiresLiveOwner()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(1, 1)) },
            Array.Empty<BattleUnitState>()
        );

        ContingencyTargetResolutionResult resolved = Resolve(
            fixture.State,
            "owner",
            Resolver("self"),
            Facts(triggerCell: new Vector2I(0, 0))
        );

        _test.True(resolved.Ok, "self should resolve a live owner unit.");
        _test.Eq(resolved.TargetUnitId, new StringName("owner"), "self should target owner unit id.");
        _test.Eq(resolved.TargetCell, new Vector2I(1, 1), "self should expose owner current cell.");

        fixture.State.GetUnit("owner").MarkDead();
        ContingencyTargetResolutionResult missing = Resolve(
            fixture.State,
            "owner",
            Resolver("self"),
            Facts(triggerCell: new Vector2I(0, 0))
        );

        _test.False(missing.Ok, "self should fail after the owner is no longer live.");
        _test.Eq(
            missing.ReasonId,
            new StringName("owner_unit_missing"),
            "self missing owner should use stable reason id."
        );
    }

    private void TestTriggerSourceUsesFrozenSourceCell()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(1, 1)) },
            new[] { Unit("source", "enemy", new Vector2I(0, 1)) }
        );
        BattleUnitState source = fixture.State.GetUnit("source");
        fixture.GridService.PlaceUnit(fixture.State, source, new Vector2I(4, 1), true);

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            Resolver("trigger_source"),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 1), triggerCell: new Vector2I(0, 1))
        );

        _test.True(result.Ok, "trigger_source should resolve an existing frozen source unit.");
        _test.Eq(result.TargetUnitId, new StringName("source"), "trigger_source unit mismatch.");
        _test.Eq(
            result.TargetCell,
            new Vector2I(0, 1),
            "trigger_source should keep the frozen source cell instead of following later movement."
        );
    }

    private void TestTriggerSourceAndTargetRequireFrozenCells()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(1, 1)) },
            new[]
            {
                Unit("source", "enemy", new Vector2I(0, 1)),
                Unit("target", "enemy", new Vector2I(2, 1)),
            }
        );

        ContingencyTargetResolutionResult missingSourceCell = Resolve(
            fixture.State,
            "owner",
            Resolver("trigger_source"),
            Facts(sourceUnitId: "source", triggerCell: new Vector2I(0, 1))
        );
        _test.False(
            missingSourceCell.Ok,
            "trigger_source should fail when the frozen source cell fact is missing."
        );
        _test.Eq(
            missingSourceCell.ReasonId,
            new StringName("trigger_source_cell_missing"),
            "trigger_source missing frozen cell reason mismatch."
        );

        ContingencyTargetResolutionResult missingTargetCell = Resolve(
            fixture.State,
            "owner",
            Resolver("trigger_target"),
            Facts(targetUnitId: "target", triggerCell: new Vector2I(2, 1))
        );
        _test.False(
            missingTargetCell.Ok,
            "trigger_target should fail when the frozen target cell fact is missing."
        );
        _test.Eq(
            missingTargetCell.ReasonId,
            new StringName("trigger_target_cell_missing"),
            "trigger_target missing frozen cell reason mismatch."
        );
    }

    private void TestTriggerTargetFailsWhenGone()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(1, 1)) },
            new[] { Unit("target", "enemy", new Vector2I(2, 1)) }
        );

        ContingencyTargetResolutionResult live = Resolve(
            fixture.State,
            "owner",
            Resolver("trigger_target"),
            Facts(targetUnitId: "target", targetCell: new Vector2I(2, 1), triggerCell: new Vector2I(2, 1))
        );
        _test.True(live.Ok, "trigger_target should resolve while the frozen target is live.");

        fixture.State.GetUnit("target").MarkDead();
        ContingencyTargetResolutionResult gone = Resolve(
            fixture.State,
            "owner",
            Resolver("trigger_target"),
            Facts(targetUnitId: "target", targetCell: new Vector2I(2, 1), triggerCell: new Vector2I(2, 1))
        );

        _test.False(gone.Ok, "trigger_target should fail when the target is gone before release.");
        _test.Eq(
            gone.ReasonId,
            new StringName("trigger_target_unit_missing"),
            "trigger_target gone reason mismatch."
        );
    }

    private void TestNearestEnemyToOwnerUsesCurrentOwnerCellAndUnitIdTieBreak()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            new[]
            {
                Unit("enemy_b", "enemy", new Vector2I(4, 2)),
                Unit("enemy_a", "enemy", new Vector2I(2, 4)),
                Unit("far_enemy", "enemy", new Vector2I(5, 5)),
            }
        );

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            Resolver("nearest_enemy_to_owner"),
            Facts(triggerCell: new Vector2I(0, 0))
        );

        _test.True(result.Ok, "nearest_enemy_to_owner should find a hostile target.");
        _test.Eq(
            result.TargetUnitId,
            new StringName("enemy_a"),
            "nearest_enemy_to_owner should tie-break equal distance by unit id."
        );
    }

    private void TestNearestEnemyToTriggerCellUsesFrozenTriggerCellAndUnitIdTieBreak()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(0, 0)) },
            new[]
            {
                Unit("enemy_b", "enemy", new Vector2I(2, 1)),
                Unit("enemy_a", "enemy", new Vector2I(1, 2)),
                Unit("far_enemy", "enemy", new Vector2I(5, 5)),
            }
        );

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            Resolver("nearest_enemy_to_trigger_cell"),
            Facts(triggerCell: new Vector2I(1, 1))
        );

        _test.True(result.Ok, "nearest_enemy_to_trigger_cell should find a hostile target.");
        _test.Eq(
            result.TargetUnitId,
            new StringName("enemy_a"),
            "nearest_enemy_to_trigger_cell should use frozen trigger cell and unit id tie-break."
        );
    }

    private void TestOwnerCenteredAreaUsesOwnerCurrentCellAndSkillArea()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            Array.Empty<BattleUnitState>()
        );
        SkillDef skill = GroundSkill("ward", "diamond", 1);

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            Resolver("owner_centered_area"),
            Facts(triggerCell: new Vector2I(0, 0)),
            skill
        );

        _test.True(result.Ok, "owner_centered_area should resolve.");
        _test.True(result.IsGroundTarget, "owner_centered_area should be a ground target.");
        _test.Eq(result.TargetCell, new Vector2I(2, 2), "owner_centered_area anchor mismatch.");
        AssertCells(
            result.AreaCells,
            new[]
            {
                new Vector2I(1, 2),
                new Vector2I(2, 1),
                new Vector2I(2, 2),
                new Vector2I(2, 3),
                new Vector2I(3, 2),
            },
            "owner_centered_area should collect current skill area cells."
        );
    }

    private void TestAttackerCellUsesFrozenSourceCell()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            new[] { Unit("attacker", "enemy", new Vector2I(0, 2)) }
        );
        fixture.GridService.PlaceUnit(
            fixture.State,
            fixture.State.GetUnit("attacker"),
            new Vector2I(4, 2),
            true
        );

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            Resolver("attacker_cell"),
            Facts(sourceUnitId: "attacker", sourceCell: new Vector2I(0, 2), triggerCell: new Vector2I(2, 2))
        );

        _test.True(result.Ok, "attacker_cell should resolve.");
        _test.True(result.IsGroundTarget, "attacker_cell should be a ground target.");
        _test.Eq(result.TargetCell, new Vector2I(0, 2), "attacker_cell should use frozen attacker/source cell.");
    }

    private void TestEmptyCellRejectsIllegalCells()
    {
        using ResolverFixture fixture = BuildFixture(
            new[]
            {
                Unit("owner", "player", new Vector2I(1, 1)),
                Unit("ally_blocker", "player", new Vector2I(2, 1)),
            },
            new[] { Unit("source", "enemy", new Vector2I(0, 1)) },
            new Vector2I(4, 3)
        );
        fixture.State.GetCell(new Vector2I(1, 0)).SetPassable(false);
        fixture.State.GetCell(new Vector2I(1, 2)).SetBaseTerrain("deep_water");

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("away_from_trigger_source", 2),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 1), triggerCell: new Vector2I(0, 1))
        );

        _test.True(result.Ok, "empty_cell_near_owner should find the only high-scoring legal cell.");
        _test.Eq(
            result.TargetCell,
            new Vector2I(2, 0),
            "empty_cell_near_owner should reject illegal cells, then row-major tie-break equally scored legal cells."
        );
    }

    private void TestEmptyCellAwayFromTriggerSourceScoresLegalCells()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            new[] { Unit("source", "enemy", new Vector2I(0, 2)) },
            new Vector2I(5, 5)
        );
        Occupy(fixture, "block_top", "player", new Vector2I(2, 0));
        Occupy(fixture, "block_lower_tie", "player", new Vector2I(2, 4));
        fixture.State.GetCell(new Vector2I(3, 1)).SetPassable(false);
        fixture.State.GetCell(new Vector2I(3, 3)).SetPassable(false);

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("away_from_trigger_source", 2),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 2), triggerCell: new Vector2I(0, 2))
        );

        _test.True(result.Ok, "away_from_trigger_source should resolve a legal cell.");
        _test.Eq(
            result.TargetCell,
            new Vector2I(4, 2),
            "away_from_trigger_source should choose the farthest legal cell from the frozen source."
        );
    }

    private void TestEmptyCellCoordinateTieBreakUsesRowMajorAscendingOrder()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(1, 1)) },
            Array.Empty<BattleUnitState>(),
            new Vector2I(3, 3)
        );

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("safe_cell", 1),
            Facts(triggerCell: new Vector2I(1, 1))
        );

        _test.True(result.Ok, "safe_cell tie-break fixture should resolve.");
        _test.Eq(
            result.TargetCell,
            new Vector2I(1, 0),
            "empty_cell final tie-break should prefer row-major ascending coordinates."
        );
    }

    private void TestEmptyCellSafeCellPrefersOutsideCurrentDamageArea()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            new[] { Unit("source", "enemy", new Vector2I(0, 2)) },
            new Vector2I(5, 5)
        );
        IReadOnlyList<Vector2I> damageArea = new[]
        {
            new Vector2I(1, 2),
            new Vector2I(2, 1),
            new Vector2I(2, 2),
            new Vector2I(2, 3),
            new Vector2I(3, 2),
            new Vector2I(4, 2),
        };

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("safe_cell", 2),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 2), triggerCell: new Vector2I(0, 2), damageArea: damageArea)
        );

        _test.True(result.Ok, "safe_cell should resolve.");
        _test.Eq(
            result.TargetCell,
            new Vector2I(2, 0),
            "safe_cell should prefer a legal cell outside the current damage area, then row-major ascending tie-break."
        );
    }

    private void TestEmptyCellFallsBackWhenNoSafeCellExists()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            new[] { Unit("source", "enemy", new Vector2I(0, 2)) },
            new Vector2I(5, 5)
        );
        IReadOnlyList<Vector2I> allNearby = NearbyCells(new Vector2I(2, 2), 2);

        ContingencyTargetResolutionResult result = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("safe_cell", 2),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 2), triggerCell: new Vector2I(0, 2), damageArea: allNearby)
        );

        _test.True(result.Ok, "safe_cell should still resolve when no perfect safe cell exists.");
        _test.Eq(
            result.TargetCell,
            new Vector2I(2, 0),
            "safe_cell fallback should choose the highest-scoring legal cell, then row-major ascending tie-break."
        );
    }

    private void TestEmptyCellFailureDoesNotUnconsumeEnteredReleaseContext()
    {
        PartyState party = PartyWithSetup(ChargedSetup("no_escape", EmptyCellResolver("safe_cell", 1)));
        using CharacterManagementModule manager = BuildManager(party);
        using BattleRuntimeModule runtime = new();
        runtime.setup(character_gateway: manager);
        BattleState state = BattleTestFixture.BuildFlatState("contingency_target_no_legal", new Vector2I(1, 1));
        BattleUnitState owner = Unit("owner", "player", Vector2I.Zero);
        owner.source_member_id = "hero";
        BattleTestFixture.InstallUnits(state, new[] { owner }, Array.Empty<BattleUnitState>());
        runtime.SetupStateForTests(state);

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        ContingencyReleaseContext context = sidecar.EnterReleaseContext("hero:no_escape");
        IReadOnlyList<ContingencyTargetResolutionResult> results =
            sidecar.ResolveStoredSpellTargetsForRelease(context, Facts(triggerCell: Vector2I.Zero));

        _test.True(context.IsValid, "release context should be entered before target resolution.");
        _test.Eq(results.Count, 1, "stored spell resolution count mismatch.");
        if (results.Count > 0)
        {
            _test.False(results[0].Ok, "empty_cell_near_owner should fail when no legal cell exists.");
            _test.Eq(results[0].ReasonId, new StringName("no_legal_cell"), "no legal cell reason mismatch.");
        }
        _test.True(
            sidecar.IsSetupConsumedForMember("hero", "no_escape"),
            "failed target resolution must not un-consume an already entered release context."
        );

        BattleTestFixture.DisposeBattleState(state);
    }

    private void TestStoredSpellFallbackPoliciesGateLaterResolution()
    {
        PartyState party = PartyWithSetups(
            ChargedSetup(
                "abort_setup",
                new GArray
                {
                    StoredSpell("escape_step", 1, EmptyCellResolver("safe_cell", 1), "abort_remaining_if_invalid"),
                    StoredSpell("escape_step", 2, Resolver("self"), "skip_if_invalid"),
                }
            ),
            ChargedSetup(
                "skip_setup",
                new GArray
                {
                    StoredSpell("escape_step", 1, EmptyCellResolver("safe_cell", 1), "skip_if_invalid"),
                    StoredSpell("escape_step", 2, Resolver("self"), "skip_if_invalid"),
                }
            )
        );
        using CharacterManagementModule manager = BuildManager(party);
        using BattleRuntimeModule runtime = new();
        runtime.setup(character_gateway: manager);
        BattleState state = BattleTestFixture.BuildFlatState("contingency_target_fallback_policy", new Vector2I(1, 1));
        BattleUnitState owner = Unit("owner", "player", Vector2I.Zero);
        owner.source_member_id = "hero";
        BattleTestFixture.InstallUnits(state, new[] { owner }, Array.Empty<BattleUnitState>());
        runtime.SetupStateForTests(state);

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        IReadOnlyList<ContingencyTargetResolutionResult> abortResults =
            sidecar.ResolveStoredSpellTargetsForRelease(
                sidecar.EnterReleaseContext("hero:abort_setup"),
                Facts(triggerCell: Vector2I.Zero)
            );
        _test.Eq(
            abortResults.Count,
            1,
            "abort_remaining_if_invalid should stop resolving later stored spells in the same release."
        );
        if (abortResults.Count > 0)
            _test.False(abortResults[0].Ok, "abort policy first spell should preserve its failed result.");

        IReadOnlyList<ContingencyTargetResolutionResult> skipResults =
            sidecar.ResolveStoredSpellTargetsForRelease(
                sidecar.EnterReleaseContext("hero:skip_setup"),
                Facts(triggerCell: Vector2I.Zero)
            );
        _test.Eq(
            skipResults.Count,
            2,
            "skip_if_invalid should continue resolving later stored spells after a failed stored spell."
        );
        if (skipResults.Count > 1)
            _test.True(skipResults[1].Ok, "skip policy should allow the later valid spell to resolve.");

        BattleTestFixture.DisposeBattleState(state);
    }

    private void TestFrozenAndResultAreaListsAreImmutableCopies()
    {
        List<Vector2I> frozenSource = new() { new Vector2I(1, 1) };
        ContingencyFrozenTriggerFacts facts = Facts(
            triggerCell: Vector2I.Zero,
            damageArea: frozenSource
        );
        frozenSource.Add(new Vector2I(2, 2));
        _test.Eq(
            facts.CurrentDamageEventAreaCells.Count,
            1,
            "frozen trigger facts should copy damage-area cells at construction time."
        );
        _test.True(
            CannotMutateCellList(facts.CurrentDamageEventAreaCells),
            "frozen trigger facts should expose a read-only damage-area list."
        );

        List<Vector2I> resultSource = new() { new Vector2I(3, 3) };
        ContingencyTargetResolutionResult result =
            ContingencyTargetResolutionResult.GroundTarget(new Vector2I(3, 3), resultSource);
        resultSource.Add(new Vector2I(4, 4));
        _test.Eq(
            result.AreaCells.Count,
            1,
            "target resolution results should copy area cells at construction time."
        );
        _test.True(
            CannotMutateCellList(result.AreaCells),
            "target resolution results should expose a read-only area-cell list."
        );
    }

    private void TestFatalDamageEscapeFlagRequiresLeavingCurrentDamageArea()
    {
        using ResolverFixture fixture = BuildFixture(
            new[] { Unit("owner", "player", new Vector2I(2, 2)) },
            new[] { Unit("source", "enemy", new Vector2I(0, 2)) },
            new Vector2I(5, 5)
        );
        IReadOnlyList<Vector2I> damageArea = new[]
        {
            new Vector2I(1, 2),
            new Vector2I(2, 1),
            new Vector2I(2, 2),
            new Vector2I(2, 3),
            new Vector2I(3, 2),
            new Vector2I(4, 2),
        };

        ContingencyTargetResolutionResult outside = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("safe_cell", 2),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 2), triggerCell: new Vector2I(0, 2), damageArea: damageArea, fatalDamageIncoming: true)
        );
        _test.True(outside.Ok, "fatal damage safe-cell resolution should succeed.");
        _test.True(
            outside.MovedOutsideCurrentDamageEvent,
            "fatal damage escape flag should be true only when resolved cell leaves the current damage area."
        );

        ContingencyTargetResolutionResult inside = Resolve(
            fixture.State,
            "owner",
            EmptyCellResolver("safe_cell", 1),
            Facts(sourceUnitId: "source", sourceCell: new Vector2I(0, 2), triggerCell: new Vector2I(0, 2), damageArea: NearbyCells(new Vector2I(2, 2), 2), fatalDamageIncoming: true)
        );
        _test.True(inside.Ok, "fatal damage fallback resolution should succeed.");
        _test.False(
            inside.MovedOutsideCurrentDamageEvent,
            "fatal damage escape flag should remain false when the resolved cell is still inside the damage area."
        );
    }

    private ContingencyTargetResolutionResult Resolve(
        BattleState state,
        StringName ownerUnitId,
        ContingencyTargetResolverState resolverState,
        ContingencyFrozenTriggerFacts facts,
        SkillDef skill = null
    ) =>
        _resolver.ResolveTarget(
            new ContingencyTargetResolutionRequest
            {
                BattleState = state,
                GridService = new BattleGridService(),
                OwnerUnitId = ownerUnitId,
                ResolverState = resolverState,
                FrozenFacts = facts,
                StoredSkillDef = skill,
            }
        );

    private static ResolverFixture BuildFixture(
        IReadOnlyList<BattleUnitState> allies,
        IReadOnlyList<BattleUnitState> enemies,
        Vector2I? mapSize = null
    ) =>
        ResolverFixture.Create(
            "contingency_target_resolver",
            mapSize ?? new Vector2I(6, 6),
            allies,
            enemies
        );

    private static BattleUnitState Unit(StringName unitId, StringName factionId, Vector2I coord)
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(unitId, factionId, coord, currentHp: 20);
        if (factionId == "player")
            unit.source_member_id = unitId == new StringName("owner") ? "hero" : unitId;
        return unit;
    }

    private static void Occupy(ResolverFixture fixture, StringName unitId, StringName factionId, Vector2I coord)
    {
        BattleUnitState unit = Unit(unitId, factionId, coord);
        fixture.State.SetUnit(unit);
        fixture.GridService.PlaceUnit(fixture.State, unit, coord, true);
        if (factionId == "player")
            fixture.State.ally_unit_ids.Add(unitId);
        else
            fixture.State.enemy_unit_ids.Add(unitId);
    }

    private static ContingencyTargetResolverState Resolver(string type) =>
        ContingencyTargetResolverState.FromDictionary(new GDictionary { ["type"] = type });

    private static ContingencyTargetResolverState EmptyCellResolver(string preference, int maxDistance) =>
        ContingencyTargetResolverState.FromDictionary(
            new GDictionary
            {
                ["type"] = "empty_cell_near_owner",
                ["preference"] = preference,
                ["max_distance"] = maxDistance,
            }
        );

    private static ContingencyFrozenTriggerFacts Facts(
        StringName sourceUnitId = default,
        Vector2I? sourceCell = null,
        StringName targetUnitId = default,
        Vector2I? targetCell = null,
        Vector2I? triggerCell = null,
        IReadOnlyList<Vector2I> damageArea = null,
        bool fatalDamageIncoming = false
    ) =>
        new()
        {
            TriggerSourceUnitId = Normalize(sourceUnitId),
            TriggerSourceCell = sourceCell ?? new Vector2I(-1, -1),
            TriggerTargetUnitId = Normalize(targetUnitId),
            TriggerTargetCell = targetCell ?? new Vector2I(-1, -1),
            TriggerCell = triggerCell ?? sourceCell ?? targetCell ?? new Vector2I(-1, -1),
            CurrentDamageEventAreaCells = damageArea ?? Array.Empty<Vector2I>(),
            FatalDamageIncoming = fatalDamageIncoming,
        };

    private static SkillDef GroundSkill(StringName skillId, StringName areaPattern, int areaValue) =>
        new()
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = new CombatSkillDef
            {
                skill_id = skillId,
                target_mode = "ground",
                target_team_filter = "any",
                area_pattern = areaPattern,
                area_value = areaValue,
            },
        };

    private static PartyState PartyWithSetup(ContingencyMatrixSetupState setup) =>
        PartyWithSetups(setup);

    private static PartyState PartyWithSetups(params ContingencyMatrixSetupState[] setups)
    {
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            progression = new UnitProgress { unit_id = "hero", display_name = "Hero" },
            current_hp = 20,
            current_mp = 5,
        };
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 30);
        member = member.WithContingencySetupsForMutation(setups);
        PartyState party = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
            warehouse_state = new WarehouseState(),
        };
        party.SetMemberState(member);
        return party;
    }

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        ContingencyTargetResolverState resolver
    ) =>
        ChargedSetup(
            setupId,
            new GArray { StoredSpell("escape_step", 1, resolver, "skip_if_invalid") }
        );

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        GArray storedSpells
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
                ["reserved_mp_max"] = 12,
                ["material_costs"] = new GArray
                {
                    new GDictionary { ["item_id"] = "special_contingency_gem", ["quantity"] = 1 },
                },
                ["trigger"] = new GDictionary
                {
                    ["type"] = "hp_below_percent",
                    ["subject"] = "owner",
                    ["percent"] = 30,
                    ["crossing_only"] = true,
                    ["timing"] = "after_hp_changed",
                },
                ["release_mode"] = "burst_release",
                ["stored_spells"] = storedSpells,
            }
        );

    private static GDictionary StoredSpell(
        StringName storedSkillId,
        int order,
        ContingencyTargetResolverState resolver,
        StringName fallbackPolicy
    ) =>
        new()
        {
            ["stored_skill_id"] = storedSkillId.ToString(),
            ["cast_level"] = 1,
            ["order"] = order,
            ["target_resolver"] = resolver.ToDictionary(),
            ["parameter_bindings"] = new GDictionary(),
            ["fallback_policy"] = fallbackPolicy.ToString(),
        };

    private static bool CannotMutateCellList(IReadOnlyList<Vector2I> cells)
    {
        if (cells is not IList<Vector2I> mutableCells)
            return true;
        try
        {
            mutableCells.Add(new Vector2I(99, 99));
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    private static CharacterManagementModule BuildManager(PartyState party)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new Dictionary<StringName, SkillDef>
            {
                ["escape_step"] = GroundSkill("escape_step", "single", 0),
            },
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

    private void AssertCells(IReadOnlyList<Vector2I> actual, IReadOnlyList<Vector2I> expected, string message)
    {
        string actualText = JoinCells(actual);
        string expectedText = JoinCells(expected);
        _test.Eq(actualText, expectedText, message);
    }

    private static IReadOnlyList<Vector2I> NearbyCells(Vector2I center, int maxDistance)
    {
        List<Vector2I> cells = new();
        for (int y = center.Y - maxDistance; y <= center.Y + maxDistance; y++)
        {
            for (int x = center.X - maxDistance; x <= center.X + maxDistance; x++)
            {
                Vector2I coord = new(x, y);
                if (Math.Abs(coord.X - center.X) + Math.Abs(coord.Y - center.Y) <= maxDistance)
                    cells.Add(coord);
            }
        }
        return cells;
    }

    private static string JoinCells(IEnumerable<Vector2I> cells)
    {
        List<string> entries = new();
        foreach (Vector2I cell in cells ?? Array.Empty<Vector2I>())
            entries.Add($"{cell.X},{cell.Y}");
        entries.Sort(StringComparer.Ordinal);
        return string.Join("|", entries);
    }

    private static StringName Normalize(StringName value) =>
        value == default ? new StringName("") : value;

    private sealed class ResolverFixture : IDisposable
    {
        private ResolverFixture(BattleState state, BattleGridService gridService)
        {
            State = state;
            GridService = gridService;
        }

        internal BattleState State { get; }
        internal BattleGridService GridService { get; }

        internal static ResolverFixture Create(
            StringName battleId,
            Vector2I mapSize,
            IReadOnlyList<BattleUnitState> allies,
            IReadOnlyList<BattleUnitState> enemies
        )
        {
            BattleState state = BattleTestFixture.BuildFlatState(battleId, mapSize);
            BattleGridService gridService = new();
            BattleTestFixture.InstallUnits(state, allies, enemies);
            return new ResolverFixture(state, gridService);
        }

        public void Dispose()
        {
            GridService.Dispose();
            BattleTestFixture.DisposeBattleState(State);
        }
    }
}
