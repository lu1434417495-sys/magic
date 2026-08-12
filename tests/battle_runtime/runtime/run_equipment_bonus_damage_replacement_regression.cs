using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_bonus_damage_replacement_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName LowBindingId = "binding.test.replacement.low";
    private static readonly StringName HighBindingId = "binding.test.replacement.high";
    private static readonly StringName IndependentBindingId =
        "binding.test.replacement.independent";
    private static readonly StringName UngroupedBindingId =
        "binding.test.replacement.ungrouped";
    private static readonly StringName TieAlphaBindingId =
        "binding.test.replacement.tie_alpha";
    private static readonly StringName TieZetaBindingId =
        "binding.test.replacement.tie_zeta";
    private static readonly StringName MainGroupId = "test.primary_append";
    private static readonly StringName TieGroupId = "test.tie_append";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        BattleRuntimeModule runtime = null;
        BattleState state = null;
        try
        {
            IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings =
                BuildBindings();
            runtime = new BattleRuntimeModule();
            runtime.setup(equipment_ability_bindings: bindings);
            BattleUnitState source = BuildUnit("replacement_source", "ally", new Vector2I(1, 1));
            BattleUnitState target = BuildUnit("replacement_target", "enemy", new Vector2I(2, 1));
            InstallAbilitySources(
                source,
                LowBindingId,
                HighBindingId,
                IndependentBindingId,
                UngroupedBindingId,
                TieZetaBindingId,
                TieAlphaBindingId
            );
            state = BattleTestFixture.BuildFlatState(
                "equipment_bonus_damage_replacement",
                new Vector2I(4, 4)
            );
            BattleTestFixture.InstallUnits(state, new[] { source }, new[] { target });
            runtime.SetupStateForTests(state);

            TestDamageQueryPath(runtime, state, source, target);
            TestAfterHitResultPath(runtime, state, source, target);
            TestPriorityFallback(runtime, state, source, target);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state);
        }
        RequestTestExit(_test.Finish("Equipment bonus-damage replacement regression"));
    }

    private void TestDamageQueryPath(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState source,
        BattleUnitState target
    )
    {
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> results =
            runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceOnHit(
                BuildQuery(state, source, target)
            );

        AssertWinningSet(results, "damage-query path");
    }

    private void TestAfterHitResultPath(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState source,
        BattleUnitState target
    )
    {
        BattleEquipmentAbilityAfterHitResult result =
            runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
                new BattleEquipmentAbilityAfterHitContext
                {
                    SourceUnit = source,
                    TargetUnit = target,
                    BattleState = state,
                    AttackSucceeded = true,
                    ApplyDamageDiceActions = true,
                }
            );

        AssertWinningSet(result.BonusDamageDice, "after-hit result path");
    }

    private void TestPriorityFallback(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState source,
        BattleUnitState target
    )
    {
        InstallAbilitySources(
            source,
            LowBindingId,
            IndependentBindingId,
            UngroupedBindingId,
            TieZetaBindingId,
            TieAlphaBindingId
        );
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> results =
            runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceOnHit(
                BuildQuery(state, source, target)
            );

        _test.True(
            ContainsDice(results, LowBindingId, 1, 4),
            "移除高优先级来源后，低优先级同组附伤必须自动回落生效。"
        );
        _test.False(
            ContainsBinding(results, HighBindingId),
            "回落结果不得保留已经移除的高优先级来源。"
        );
        _test.True(
            ContainsDice(results, IndependentBindingId, 1, 12)
                && ContainsDice(results, UngroupedBindingId, 2, 3),
            "优先级回落不得吞掉其他组或未分组附伤。"
        );
    }

    private void AssertWinningSet(
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> results,
        string lane
    )
    {
        _test.False(
            ContainsBinding(results, LowBindingId),
            $"{lane}: 同组高优先级存在时不得保留低优先级附伤。"
        );
        _test.True(
            ContainsDice(results, HighBindingId, 1, 6)
                && ContainsDice(results, HighBindingId, 2, 8),
            $"{lane}: 获胜动作的全部骰项必须作为一个动作包保留。"
        );
        _test.True(
            ContainsDice(results, IndependentBindingId, 1, 12),
            $"{lane}: 不同 replacement group 的附伤必须继续叠加。"
        );
        _test.True(
            ContainsDice(results, UngroupedBindingId, 2, 3),
            $"{lane}: 未分组附伤必须继续叠加。"
        );
        _test.True(
            ContainsDice(results, TieAlphaBindingId, 1, 10),
            $"{lane}: 同优先级应由 ordinal 更小的 binding_id 稳定获胜。"
        );
        _test.False(
            ContainsBinding(results, TieZetaBindingId),
            $"{lane}: 稳定 tie 仲裁后不得保留另一同组候选。"
        );
    }

    private static BattleEquipmentAbilityBonusDamageDiceContext BuildQuery(
        BattleState state,
        BattleUnitState source,
        BattleUnitState target
    ) =>
        new()
        {
            SourceUnit = source,
            TargetUnit = target,
            BattleState = state,
            AttackSucceeded = true,
            IncludesWeaponDamage = false,
        };

    private static IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        BuildBindings()
    {
        return new Dictionary<StringName, EquipmentAbilityBindingDefinition>
        {
            [LowBindingId] = BuildBinding(
                LowBindingId,
                "low",
                MainGroupId,
                replacementPriority: 100,
                reactionPriority: 0,
                new[] { (1, 4) }
            ),
            [HighBindingId] = BuildBinding(
                HighBindingId,
                "high",
                MainGroupId,
                replacementPriority: 200,
                reactionPriority: 10,
                new[] { (1, 6), (2, 8) }
            ),
            [IndependentBindingId] = BuildBinding(
                IndependentBindingId,
                "independent",
                "test.independent_append",
                replacementPriority: 50,
                reactionPriority: 0,
                new[] { (1, 12) }
            ),
            [UngroupedBindingId] = BuildBinding(
                UngroupedBindingId,
                "ungrouped",
                "",
                replacementPriority: 0,
                reactionPriority: 0,
                new[] { (2, 3) }
            ),
            [TieAlphaBindingId] = BuildBinding(
                TieAlphaBindingId,
                "tie_alpha",
                TieGroupId,
                replacementPriority: 500,
                reactionPriority: 20,
                new[] { (1, 10) }
            ),
            [TieZetaBindingId] = BuildBinding(
                TieZetaBindingId,
                "tie_zeta",
                TieGroupId,
                replacementPriority: 500,
                reactionPriority: 0,
                new[] { (1, 20) }
            ),
        };
    }

    private static EquipmentAbilityBindingDefinition BuildBinding(
        StringName bindingId,
        StringName actionSuffix,
        StringName replacementGroupId,
        int replacementPriority,
        int reactionPriority,
        IReadOnlyList<(int Count, int Sides)> diceTerms
    )
    {
        var terms = new List<DiceExpressionTermDefinition>();
        foreach ((int count, int sides) in diceTerms)
        {
            terms.Add(
                new DiceExpressionTermDefinition
                {
                    DiceCount = count,
                    DiceSides = sides,
                }
            );
        }
        return new EquipmentAbilityBindingDefinition
        {
            BindingId = bindingId,
            Reactions = new[]
            {
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = $"reaction.{actionSuffix}",
                    Trigger = EquipmentAbilityTriggerKind.OnHit,
                    Timing = EquipmentAbilityTimingKind.AfterHit,
                    Priority = reactionPriority,
                    Actions = new[]
                    {
                        new EquipmentAbilityActionDefinition
                        {
                            ActionId = $"action.{actionSuffix}",
                            Kind = BattleEquipmentAbilityRuntimeService.ActionKindAddDamageDice,
                            PayloadDefinition = new AddDamageDiceActionPayloadDefinition
                            {
                                TargetSelector = "target",
                                Dice = new DiceExpressionDefinition { Terms = terms },
                                DamageType = "fire",
                                RequireWeaponDamage = false,
                                ReplacementGroupId = replacementGroupId,
                                ReplacementPriority = replacementPriority,
                            },
                        },
                    },
                },
            },
        };
    }

    private static void InstallAbilitySources(
        BattleUnitState source,
        params StringName[] bindingIds
    )
    {
        source.ReplaceEquipmentAbilityProjectionTyped(
            new[]
            {
                new BattleEquipmentAbilitySourceState
                {
                    EffectiveInstanceKey = "test:replacement_source",
                    EquipmentDefId = "test_replacement_source",
                    SourceKind = EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment,
                    AbilityIds = new List<StringName>(bindingIds),
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
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.SetCombatResources(100, 0, 0, 0, 2, 2);
        unit.SetAnchorCoord(coord);
        unit.SetUnarmedWeaponProjectionTyped();
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static bool ContainsBinding(
        IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> results,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilityBonusDamageDiceResult result in results
                ?? Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>()
        )
        {
            if (result?.BindingId == bindingId)
                return true;
        }
        return false;
    }

    private static bool ContainsDice(
        IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> results,
        StringName bindingId,
        int diceCount,
        int diceSides
    )
    {
        foreach (
            BattleEquipmentAbilityBonusDamageDiceResult result in results
                ?? Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>()
        )
        {
            if (
                result?.BindingId == bindingId
                && result.DiceCount == diceCount
                && result.DiceSides == diceSides
            )
            {
                return true;
            }
        }
        return false;
    }
}
