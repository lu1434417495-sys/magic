using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_damage_application_projection_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestNullHookPreservesLegacyShieldAndHpApplication();
            TestShieldAbsorptionPercentProjection();
            TestPartialShieldDrainPreservesMetadata();
            TestProjectionDoesNotMutateStaleShieldState();
            TestCancelDamageSkipsShieldAndHpMutation();
            TestModifiedResolvedDamageRecomputesProjection();
            TestStateMayHaveChangedRecomputesProjectionAfterHookSideEffects();
            TestPreviewAndSuppressedInputsDoNotInvokeHook();
            TestDeathPreventionAndMinHpSemanticsRemainUnchanged();
        }
        catch (Exception ex)
        {
            _test.Fail($"Damage application projection regression crashed: {ex}");
        }

        RequestTestExit(_test.Finish("Damage application projection regression"));
    }

    private void TestNullHookPreservesLegacyShieldAndHpApplication()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState target = Unit("legacy_target", hp: 20, shieldHp: 5);
        using GodotProjectionLease<GDictionary> damageInputLease =
            Input(resolvedDamage: 8).ToDictionaryLease();
        int hpDamage = resolver.ApplyDirectDamageToTargetTyped(
            target,
            damageInputLease.Value,
            Unit("legacy_source", "enemy")
        );

        _test.Eq(hpDamage, 3, "Null hook should preserve legacy HP damage after shield absorption.");
        _test.Eq(target.GetCurrentHp(), 17, "Null hook should preserve legacy HP mutation.");
        BattleUnitShieldSnapshot cleared = target.GetShieldStateTyped();
        _test.Eq(cleared.CurrentHp, 0, "Null hook should preserve legacy shield drain.");
        _test.Eq(cleared.MaxHp, 0, "Broken shield should clear max HP.");
        _test.Eq(cleared.Duration, -1, "Broken shield should restore canonical duration.");
        _test.Eq(cleared.Family, new StringName(""), "Broken shield should clear family.");
        _test.Eq(cleared.SourceUnitId, new StringName(""), "Broken shield should clear source unit.");
        _test.Eq(cleared.SourceSkillId, new StringName(""), "Broken shield should clear source skill.");
    }

    private void TestShieldAbsorptionPercentProjection()
    {
        BattleUnitState halfTarget = Unit("half_target", hp: 20, shieldHp: 10);
        DamageApplicationProjection half = BattleDamageResolver.ProjectDamageApplication(
            halfTarget,
            Input(resolvedDamage: 9, shieldAbsorptionPercent: 50.0)
        );
        _test.Eq(half.ShieldAbsorbed, 5, "50 percent projection should absorb only effective shield capacity.");
        _test.Eq(half.ShieldDrain, 10, "50 percent projection should drain the shield amount needed for absorption.");
        _test.Eq(half.HpDamage, 4, "50 percent projection should expose post-shield HP damage.");
        _test.Eq(half.ProjectedHp, 16, "50 percent projection should expose projected HP.");

        BattleUnitState fullTarget = Unit("full_target", hp: 20, shieldHp: 10);
        DamageApplicationProjection full = BattleDamageResolver.ProjectDamageApplication(
            fullTarget,
            Input(resolvedDamage: 9, shieldAbsorptionPercent: 100.0)
        );
        _test.Eq(full.ShieldAbsorbed, 9, "100 percent projection should absorb resolved damage up to shield HP.");
        _test.Eq(full.ShieldDrain, 9, "100 percent projection should drain one shield HP per absorbed damage.");
        _test.Eq(full.HpDamage, 0, "100 percent projection should leave no HP damage while shield covers it.");
        _test.Eq(full.ProjectedShieldHp, 1, "100 percent projection should expose projected shield HP.");
    }

    private void TestPartialShieldDrainPreservesMetadata()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState target = Unit("partial_drain_target", hp: 20, shieldHp: 10);
        BattleUnitShieldSnapshot before = target.GetShieldStateTyped();
        using GodotProjectionLease<GDictionary> damageInputLease =
            Input(resolvedDamage: 4).ToDictionaryLease();

        int hpDamage = resolver.ApplyDirectDamageToTargetTyped(
            target,
            damageInputLease.Value,
            Unit("partial_drain_source", "enemy")
        );

        _test.Eq(hpDamage, 0, "部分护盾吸收不应穿透到 HP。");
        _test.Eq(target.GetCurrentHp(), 20, "部分护盾吸收不应修改 HP。");
        _test.Eq(
            target.GetShieldStateTyped(),
            before with { CurrentHp = 6 },
            "部分扣减只应改变 current HP，max/duration/family/source metadata 应保持。"
        );
    }

    private void TestProjectionDoesNotMutateStaleShieldState()
    {
        BattleUnitState target = Unit("stale_shield_projection_target", hp: 20, shieldHp: 7);
        BattleUnitShieldSnapshot valid = target.GetShieldStateTyped();
        target.RestoreShieldForMutationSnapshotExact(valid with { Duration = 0 });

        DamageApplicationProjection projection = BattleDamageResolver.ProjectDamageApplication(
            target,
            Input(resolvedDamage: 5)
        );

        _test.Eq(projection.ShieldHpBefore, 0, "Projection should ignore expired shield fields.");
        _test.Eq(projection.ShieldAbsorbed, 0, "Projection should not absorb damage with an expired shield.");
        _test.Eq(projection.HpDamage, 5, "Projection should route damage to HP when shield is expired.");
        BattleUnitShieldSnapshot stale = target.GetShieldStateTyped();
        _test.Eq(stale.CurrentHp, 7, "Projection should not mutate stale shield HP.");
        _test.Eq(stale.MaxHp, 7, "Projection should not mutate stale shield max HP.");
        _test.Eq(stale.Duration, 0, "Projection should not mutate stale shield duration.");
    }

    private void TestCancelDamageSkipsShieldAndHpMutation()
    {
        BattleDamageResolver resolver = new();
        resolver.SetDamageApplicationHook(new StaticDamageHook(_ => BattleDamageApplicationHookResult.Cancel()));
        BattleUnitState target = Unit("cancel_target", hp: 20, shieldHp: 5);
        using GodotProjectionLease<GDictionary> damageInputLease =
            Input(resolvedDamage: 8).ToDictionaryLease();
        int hpDamage = resolver.ApplyDirectDamageToTargetTyped(
            target,
            damageInputLease.Value,
            Unit("cancel_source", "enemy")
        );

        _test.Eq(hpDamage, 0, "CancelDamage should report zero HP damage.");
        _test.Eq(target.GetCurrentHp(), 20, "CancelDamage should not mutate target HP.");
        _test.Eq(
            target.GetShieldStateTyped().CurrentHp,
            5,
            "CancelDamage should not mutate target shield."
        );
    }

    private void TestModifiedResolvedDamageRecomputesProjection()
    {
        BattleDamageResolver resolver = new();
        resolver.SetDamageApplicationHook(new StaticDamageHook(_ => BattleDamageApplicationHookResult.ModifyResolvedDamage(4)));
        BattleUnitState target = Unit("modify_target", hp: 20, shieldHp: 3);
        using GodotProjectionLease<GDictionary> damageInputLease =
            Input(resolvedDamage: 12).ToDictionaryLease();
        int hpDamage = resolver.ApplyDirectDamageToTargetTyped(
            target,
            damageInputLease.Value,
            Unit("modify_source", "enemy")
        );

        _test.Eq(hpDamage, 1, "ModifiedResolvedDamage should recompute HP damage from modified damage.");
        _test.Eq(target.GetCurrentHp(), 19, "ModifiedResolvedDamage should apply recomputed HP mutation.");
        _test.Eq(
            target.GetShieldStateTyped().CurrentHp,
            0,
            "ModifiedResolvedDamage should apply recomputed shield drain."
        );
    }

    private void TestStateMayHaveChangedRecomputesProjectionAfterHookSideEffects()
    {
        BattleDamageResolver resolver = new();
        resolver.SetDamageApplicationHook(
            new StaticDamageHook(context =>
            {
                context.TargetUnit.SetCurrentShieldHpAndNormalizeTyped(2);
                return BattleDamageApplicationHookResult.StateChanged();
            })
        );
        BattleUnitState target = Unit("state_changed_target", hp: 20, shieldHp: 10);
        using GodotProjectionLease<GDictionary> damageInputLease =
            Input(resolvedDamage: 10).ToDictionaryLease();
        int hpDamage = resolver.ApplyDirectDamageToTargetTyped(
            target,
            damageInputLease.Value,
            Unit("state_changed_source", "enemy")
        );

        _test.Eq(hpDamage, 8, "StateMayHaveChanged should recompute HP damage after hook side effects.");
        _test.Eq(target.GetCurrentHp(), 12, "StateMayHaveChanged should apply recomputed HP mutation.");
        _test.Eq(
            target.GetShieldStateTyped().CurrentHp,
            0,
            "StateMayHaveChanged should apply recomputed shield drain."
        );
    }

    private void TestPreviewAndSuppressedInputsDoNotInvokeHook()
    {
        CountingDamageHook hook = new();
        BattleDamageResolver resolver = new();
        resolver.SetDamageApplicationHook(hook);

        BattleUnitState previewSource = Unit("preview_source", "player");
        BattleUnitState previewTarget = Unit("preview_target", hp: 20, shieldHp: 5);
        resolver.PreviewDamageEffectTyped(
            previewSource,
            previewTarget,
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                damageTag: "physical_slash",
                power: 8
            ),
            DamageResolutionContext.Empty()
        );
        _test.Eq(hook.CallCount, 0, "Preview damage should suppress BeforeDamageResolved hooks.");

        BattleUnitState suppressedTarget = Unit("suppressed_target", hp: 20, shieldHp: 5);
        using GodotProjectionLease<GDictionary> suppressedDamageInputLease =
            Input(resolvedDamage: 8)
                .WithSuppressDamageApplicationHook(true)
                .ToDictionaryLease();
        resolver.ApplyDirectDamageToTargetTyped(
            suppressedTarget,
            suppressedDamageInputLease.Value,
            Unit("suppressed_source", "enemy")
        );
        _test.Eq(hook.CallCount, 0, "Explicit hook suppression should skip hook invocation.");
    }

    private void TestDeathPreventionAndMinHpSemanticsRemainUnchanged()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState minHpTarget = Unit("min_hp_target", hp: 5, shieldHp: 0);
        using GodotProjectionLease<GDictionary> minHpDamageInputLease =
            Input(resolvedDamage: 10, minHpAfterDamage: 1).ToDictionaryLease();
        int minHpDamage = resolver.ApplyDirectDamageToTargetTyped(
            minHpTarget,
            minHpDamageInputLease.Value,
            Unit("min_hp_source", "enemy")
        );
        _test.Eq(minHpDamage, 4, "min_hp_after_damage should still clamp actual HP damage.");
        _test.Eq(minHpTarget.GetCurrentHp(), 1, "min_hp_after_damage should still leave the target at the floor.");
        _test.True(minHpTarget.IsAlive(), "min_hp_after_damage should not mark the target dead.");

        BattleUnitState fatalTarget = Unit("fatal_target", hp: 5, shieldHp: 0);
        using GodotProjectionLease<GDictionary> fatalDamageInputLease =
            Input(resolvedDamage: 10).ToDictionaryLease();
        int fatalDamage = resolver.ApplyDirectDamageToTargetTyped(
            fatalTarget,
            fatalDamageInputLease.Value,
            Unit("fatal_source", "enemy")
        );
        _test.Eq(fatalDamage, 5, "Ordinary fatal damage should still report actual HP lost.");
        _test.False(fatalTarget.IsAlive(), "Ordinary fatal damage should still mark the target dead.");
    }

    private static DamageApplicationInput Input(
        int resolvedDamage,
        double shieldAbsorptionPercent = 100.0,
        int minHpAfterDamage = 0
    )
    {
        DamageEventResult damageEvent = new()
        {
            DamageTag = "physical_slash",
            ResolvedDamage = resolvedDamage,
            ShieldAbsorptionPercent = shieldAbsorptionPercent,
            MinHpAfterDamage = minHpAfterDamage,
        };
        return DamageApplicationInput.Create(
            damageEvent,
            resolvedDamage,
            shieldAbsorptionPercent: shieldAbsorptionPercent,
            minHpAfterDamage: minHpAfterDamage
        );
    }

    private static BattleUnitState Unit(
        StringName unitId,
        StringName factionId = default,
        int hp = 20,
        int shieldHp = 0
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId == default || factionId == "" ? "player" : factionId,
            Vector2I.Zero
        );
        unit.SetCurrentHp(hp);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hp, 20));
        if (shieldHp > 0)
        {
            unit.ReplaceShieldStateTyped(
                shieldHp,
                shieldHp,
                10,
                "test_shield",
                unitId,
                "test_shield_skill"
            );
        }
        else
        {
            unit.ClearShield();
        }
        return unit;
    }

    private sealed class CountingDamageHook : IBattleDamageApplicationHook
    {
        internal int CallCount { get; private set; }

        public BattleDamageApplicationHookResult BeforeDamageResolved(
            BattleDamageApplicationHookContext context
        )
        {
            CallCount += 1;
            return BattleDamageApplicationHookResult.None;
        }
    }

    private sealed class StaticDamageHook : IBattleDamageApplicationHook
    {
        private readonly Func<BattleDamageApplicationHookContext, BattleDamageApplicationHookResult> _handler;

        internal StaticDamageHook(
            Func<BattleDamageApplicationHookContext, BattleDamageApplicationHookResult> handler
        )
        {
            _handler = handler;
        }

        public BattleDamageApplicationHookResult BeforeDamageResolved(
            BattleDamageApplicationHookContext context
        ) =>
            _handler?.Invoke(context) ?? BattleDamageApplicationHookResult.None;
    }
}
