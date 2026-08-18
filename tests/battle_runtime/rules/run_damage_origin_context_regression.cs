public partial class run_damage_origin_context_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestDamageOriginKindDefaultsFailClosed();
        TestProducerOriginResolvesSelfDamageWithoutReclassifyingOtherOrigins();
        TestDamageResolutionContextPreservesOriginAcrossClones();

        RequestTestExit(_test.Finish("Damage origin context regression"));
    }

    private void TestDamageOriginKindDefaultsFailClosed()
    {
        _test.Eq(
            default(BattleDamageOriginKind),
            BattleDamageOriginKind.Unknown,
            "The typed damage-origin domain should default to Unknown."
        );
        _test.True(
            BattleDamageOriginKind.MainDirectEffect != BattleDamageOriginKind.Unknown,
            "Main direct damage should be distinct from the fail-closed origin."
        );
        _test.True(
            BattleDamageOriginKind.SelfDamage != BattleDamageOriginKind.Unknown,
            "Self damage should be distinct from the fail-closed origin."
        );
    }

    private void TestProducerOriginResolvesSelfDamageWithoutReclassifyingOtherOrigins()
    {
        var source = new BattleUnitState { unit_id = "origin_source" };
        var sameUnitTarget = new BattleUnitState { unit_id = "origin_source" };
        var otherTarget = new BattleUnitState { unit_id = "origin_target" };
        try
        {
            _test.Eq(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    source,
                    sameUnitTarget
                ),
                BattleDamageOriginKind.SelfDamage,
                "Main direct damage should become self damage when unit ids match."
            );
            _test.Eq(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    source,
                    otherTarget
                ),
                BattleDamageOriginKind.MainDirectEffect,
                "Main direct damage should keep its declared origin for another unit."
            );
            _test.Eq(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.Terrain,
                    source,
                    sameUnitTarget
                ),
                BattleDamageOriginKind.Terrain,
                "An explicit non-main origin should not be reclassified as self damage."
            );
            _test.Eq(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    null,
                    sameUnitTarget
                ),
                BattleDamageOriginKind.MainDirectEffect,
                "A missing source should preserve the declared origin."
            );
            _test.Eq(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    source,
                    null
                ),
                BattleDamageOriginKind.MainDirectEffect,
                "A missing target should preserve the declared origin."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(sameUnitTarget);
            BattleTestFixture.DisposeBattleUnit(otherTarget);
        }
    }

    private void TestDamageResolutionContextPreservesOriginAcrossClones()
    {
        DamageResolutionContext empty = DamageResolutionContext.Empty();
        _test.Eq(
            empty.DamageOriginKind,
            BattleDamageOriginKind.Unknown,
            "An empty damage context should fail closed to Unknown origin."
        );

        DamageResolutionContext context = empty.WithDamageOriginKind(
            BattleDamageOriginKind.MainDirectEffect
        );
        _test.Eq(
            empty.DamageOriginKind,
            BattleDamageOriginKind.Unknown,
            "Setting an origin should not mutate the source context."
        );
        ExpectOrigin(context, "WithDamageOriginKind");

        context = context.WithDamageRollMode("max");
        ExpectOrigin(context, "WithDamageRollMode");
        context = context.WithSourceSkillLevel(3);
        ExpectOrigin(context, "WithSourceSkillLevel");
        context = context.WithSaveRollOverrides(new[] { 7, 14 });
        ExpectOrigin(context, "WithSaveRollOverrides");
        context = context.WithBattleState(null);
        ExpectOrigin(context, "WithBattleState");
        context = context.WithDamageApplicationHookContext(null, null);
        ExpectOrigin(context, "WithDamageApplicationHookContext");
        context = context.WithPreviewMode();
        ExpectOrigin(context, "WithPreviewMode");
        context = context.WithDetachedPreviewMode();
        ExpectOrigin(context, "WithDetachedPreviewMode");
        context = context.WithDetachedPreviewDepth(2);
        ExpectOrigin(context, "WithDetachedPreviewDepth");
        context = context.WithForcedMoveApplied();
        ExpectOrigin(context, "WithForcedMoveApplied");
    }

    private void ExpectOrigin(DamageResolutionContext context, string cloneMethod)
    {
        _test.Eq(
            context.DamageOriginKind,
            BattleDamageOriginKind.MainDirectEffect,
            $"{cloneMethod} should preserve the typed damage origin."
        );
    }
}
