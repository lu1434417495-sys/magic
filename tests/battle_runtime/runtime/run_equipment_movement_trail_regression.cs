using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_equipment_movement_trail_regression : LifecycleTestSceneTree
{
    private static readonly StringName BindingId = "binding.test.movement_trail";
    private static readonly StringName PassiveTrailId = "test_ember_footprint";
    private static readonly StringName ChargeTrailId = "test_flame_charge_footprint";
    private static readonly StringName ChargeSkillId = "test_flame_charge";
    private static readonly StringName ReplacementGroupId = "test_phoenix_footprint";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestNormalMovementLaysDepartedCellsAndContactsOnce();
            TestChargeTrailReplacesPassiveTrailBeforeContactRolls();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Equipment movement trail regression"));
    }

    private void TestNormalMovementLaysDepartedCellsAndContactsOnce()
    {
        using MovementTrailFixture fixture = MovementTrailFixture.Create(
            new GArray { 4, 5, 6 }
        );
        BattleCommand command = new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Move),
            unit_id = fixture.Holder.unit_id,
            target_coord = new Vector2I(3, 0),
        };
        using var batch = new BattleEventBatch();
        fixture.Runtime._movement_service.HandleMoveCommand(fixture.Holder, command, batch);

        _test.Eq(
            fixture.Holder.GetAnchorCoord(),
            new Vector2I(3, 0),
            "formal move command should reach the requested destination."
        );
        for (int x = 0; x < 3; x++)
        {
            BattleTerrainEffectState effect = SingleTrailAt(fixture, new Vector2I(x, 0));
            _test.Eq(
                effect?.effect_id ?? new StringName(""),
                PassiveTrailId,
                $"departed cell {x} should contain the passive footprint."
            );
            _test.Eq(
                effect?.remaining_tu ?? -1,
                60,
                $"departed cell {x} footprint should last exactly 60 TU."
            );
        }
        _test.Eq(
            TrailCountAt(fixture, new Vector2I(3, 0)),
            0,
            "the final occupied cell is not a departed cell and must not receive a trail."
        );

        BattleValidatedMoveExecutionResult contactMove =
            fixture.Runtime._movement_service.MoveUnitAlongValidatedPathTyped(
                fixture.Target,
                new[]
                {
                    new Vector2I(0, 1),
                    new Vector2I(0, 0),
                    new Vector2I(1, 0),
                    new Vector2I(2, 0),
                },
                new Vector2I(2, 0),
                new BattleEventBatch()
            );
        _test.True(contactMove.Executed, "target should traverse the three trail cells.");
        _test.Eq(
            fixture.Target.GetCurrentHp(),
            96,
            "one movement command crossing one field instance should roll 1D6 once, not once per cell."
        );

        fixture.Holder.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "test_fire_immunity",
            source_unit_id = fixture.Holder.unit_id,
            power = 1,
            stacks = 1,
            duration = 60,
            damage_tag = "fire",
            mitigation_tier = "immune",
        });
        _test.True(
            fixture.Runtime._grid_service.MoveUnitForce(
                fixture.State,
                fixture.Holder,
                Vector2I.Zero
            ),
            "holder should be repositioned onto its own footprint for the immunity check."
        );
        int hpBeforeImmuneContact = fixture.Holder.GetCurrentHp();
        fixture.Runtime._terrain_effect_system.ApplyContactEffectsForUnit(
            fixture.Holder,
            BattleSaveContext.Empty,
            new BattleEventBatch()
        );
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            hpBeforeImmuneContact,
            "the wearer is protected from its own any-team trail only while fire immune."
        );

        fixture.Holder.EraseStatusEffect("test_fire_immunity");
        fixture.Runtime._terrain_effect_system.ApplyContactEffectsForUnit(
            fixture.Holder,
            BattleSaveContext.Empty,
            new BattleEventBatch()
        );
        _test.True(
            fixture.Holder.GetCurrentHp() < hpBeforeImmuneContact,
            "without fire immunity, the any-team trail must also damage its own source."
        );
    }

    private void TestChargeTrailReplacesPassiveTrailBeforeContactRolls()
    {
        using MovementTrailFixture fixture = MovementTrailFixture.Create(
            new GArray { 3, 4 }
        );
        var executedPath = new[]
        {
            Vector2I.Zero,
            new Vector2I(1, 0),
            new Vector2I(2, 0),
            new Vector2I(3, 0),
        };
        _test.True(
            fixture.Runtime._grid_service.MoveUnit(
                fixture.State,
                fixture.Holder,
                executedPath[^1]
            ),
            "synthetic charge source should occupy the executed path destination."
        );
        int applied = fixture.Runtime.GetEquipmentAbilityRuntimeService().ApplyMovementTrails(
            fixture.Holder,
            executedPath,
            ChargeSkillId,
            new BattleEventBatch()
        );
        _test.Eq(applied, 3, "one winning charge trail should be written to three departed cells.");
        for (int x = 0; x < 3; x++)
        {
            BattleTerrainEffectState effect = SingleTrailAt(fixture, new Vector2I(x, 0));
            _test.Eq(
                effect?.effect_id ?? new StringName(""),
                ChargeTrailId,
                "the higher-priority skill-specific trail should replace the passive trail."
            );
            _test.Eq(
                TrailCountAt(fixture, new Vector2I(x, 0)),
                1,
                "replacement arbitration must happen before terrain creation."
            );
        }

        BattleValidatedMoveExecutionResult contactMove =
            fixture.Runtime._movement_service.MoveUnitAlongValidatedPathTyped(
                fixture.Target,
                new[]
                {
                    new Vector2I(0, 1),
                    new Vector2I(0, 0),
                    new Vector2I(1, 0),
                    new Vector2I(2, 0),
                },
                new Vector2I(2, 0),
                new BattleEventBatch()
            );
        _test.True(contactMove.Executed, "target should traverse the charge trail.");
        _test.Eq(
            fixture.Target.GetCurrentHp(),
            93,
            "the winning 2D6 charge trail should roll once for 3+4 damage across the command."
        );
    }

    private static BattleTerrainEffectState SingleTrailAt(
        MovementTrailFixture fixture,
        Vector2I coord
    )
    {
        BattleCellState cell = fixture.Runtime._grid_service.GetCellState(
            fixture.State,
            coord
        );
        return cell?.timed_terrain_effects?.Count == 1
            ? cell.timed_terrain_effects[0]
            : null;
    }

    private static int TrailCountAt(MovementTrailFixture fixture, Vector2I coord)
    {
        BattleCellState cell = fixture.Runtime._grid_service.GetCellState(
            fixture.State,
            coord
        );
        return cell?.timed_terrain_effects?.Count ?? 0;
    }

    private sealed class MovementTrailFixture : IDisposable
    {
        private MovementTrailFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState holder,
            BattleUnitState target
        )
        {
            Runtime = runtime;
            State = state;
            Holder = holder;
            Target = target;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Holder { get; }
        internal BattleUnitState Target { get; }

        internal static MovementTrailFixture Create(GArray damageRolls)
        {
            EquipmentAbilityBindingDefinition binding = BuildBinding();
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                equipment_ability_bindings:
                    new Dictionary<StringName, EquipmentAbilityBindingDefinition>
                    {
                        [BindingId] = binding,
                    }
            );
            runtime.ConfigureDamageResolverForTests(
                new FixedRollDamageResolver(damageRolls)
            );

            BattleState state = BuildState();
            runtime.SetupStateForTests(state);
            BattleUnitState holder = BuildUnit(
                "trail_holder",
                "heroes",
                Vector2I.Zero
            );
            BattleUnitState target = BuildUnit(
                "trail_target",
                "enemies",
                new Vector2I(0, 1)
            );
            AttachSource(holder);
            AddUnit(runtime, state, holder, ally: true);
            AddUnit(runtime, state, target, ally: false);
            state.active_unit_id = holder.unit_id;
            return new MovementTrailFixture(runtime, state, holder, target);
        }

        private static EquipmentAbilityBindingDefinition BuildBinding() => new()
        {
            BindingId = BindingId,
            TraitId = "trait.test.movement_trail",
            MovementTrails = new[]
            {
                Trail(PassiveTrailId, priority: 100, requiredSkillId: "", diceCount: 1),
                Trail(
                    ChargeTrailId,
                    priority: 200,
                    requiredSkillId: ChargeSkillId,
                    diceCount: 2
                ),
            },
        };

        private static EquipmentMovementTrailDefinition Trail(
            StringName trailId,
            int priority,
            StringName requiredSkillId,
            int diceCount
        ) => new()
        {
            TrailId = trailId,
            ReplacementGroupId = ReplacementGroupId,
            Priority = priority,
            RequiredSkillId = requiredSkillId,
            DurationTu = 60,
            TargetTeamFilter = "any",
            DamageDice = new DiceExpressionDefinition
            {
                Terms = new[]
                {
                    new DiceExpressionTermDefinition
                    {
                        DiceCount = diceCount,
                        DiceSides = 6,
                    },
                },
            },
            DamageTag = "fire",
            DamageTags = new[] { new StringName("fire") },
            DisplayName = trailId.ToString(),
        };

        private static BattleState BuildState()
        {
            var state = new BattleState
            {
                battle_id = "equipment_movement_trail_test",
                phase = "unit_acting",
                map_size = new Vector2I(5, 3),
            };
            for (int y = 0; y < state.map_size.Y; y++)
            {
                for (int x = 0; x < state.map_size.X; x++)
                {
                    Vector2I coord = new(x, y);
                    state.SetCell(
                        coord,
                        new BattleCellState
                        {
                            coord = coord,
                            passable = true,
                        }
                    );
                }
            }
            return state;
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
            }.WithCombatResourcesForTest(
                hp: 100,
                ap: 3,
                movePoints: 10,
                isAlive: true
            );
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
            unit.SetAnchorCoord(coord);
            return unit;
        }

        private static void AttachSource(BattleUnitState holder)
        {
            holder.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = "projected:eq_test_movement_trail",
                        EquipmentDefId = "item.test.movement_trail",
                        SourceEquipmentInstanceId = "eq_test_movement_trail",
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName> { BindingId },
                    },
                },
                temporalProgressModifiers: null
            );
        }

        private static void AddUnit(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState unit,
            bool ally
        )
        {
            state.SetUnit(unit);
            if (ally)
                state.ally_unit_ids.Add(unit.unit_id);
            else
                state.enemy_unit_ids.Add(unit.unit_id);
            if (!runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true))
                throw new InvalidOperationException($"failed to place {unit.unit_id}");
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
