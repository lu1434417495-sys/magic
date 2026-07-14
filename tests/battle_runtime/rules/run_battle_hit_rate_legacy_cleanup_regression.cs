using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_hit_rate_legacy_cleanup_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHitResolverPreviewRequiresSuccessRate();
        TestRepeatAttackResolutionTextRequiresSuccessRate();
        TestHudBadgeRequiresSuccessRate();
        TestAiScoreServiceRequiresSuccessRate();

        RequestTestExit(_test.Finish("Battle hit_rate legacy cleanup regression"));
    }

    private void TestHitResolverPreviewRequiresSuccessRate()
    {
        var resolver = new BattleHitResolver();
        var legacyOnlyCheck = new AttackCheckInput(
            hitRatePercent: 87,
            baseHitRatePercent: 0,
            requiredRoll: 4
        );

        _test.Eq(
            legacyOnlyCheck.SuccessRatePercent,
            0,
            "plain attack preview input must not materialize legacy-only hit_rate_percent as success_rate_percent."
        );
        _test.True(
            !string.IsNullOrWhiteSpace(resolver.FormatAttackCheckPreview(legacyOnlyCheck)),
            "plain attack preview should still produce display feedback."
        );

        AttackCheckInput fateLegacyCheck = resolver.BuildFateAwareAttackCheckPreview(
            null,
            null,
            null,
            legacyOnlyCheck
        );
        _test.Eq(
            fateLegacyCheck.SuccessRatePercent,
            0,
            "fate-aware attack preview must keep legacy-only hit_rate_percent out of success_rate_percent."
        );
        _test.True(
            !string.IsNullOrWhiteSpace(resolver.FormatFateAwareAttackCheckPreview(fateLegacyCheck)),
            "fate-aware attack preview should still produce display feedback."
        );

        var formalCheck = new AttackCheckInput(
            hitRatePercent: 87,
            successRatePercent: 42,
            baseHitRatePercent: 0,
            requiredRoll: 4
        );
        _test.Eq(
            formalCheck.SuccessRatePercent,
            42,
            "fate-aware attack preview input must carry formal success_rate_percent."
        );
        _test.True(
            !string.IsNullOrWhiteSpace(resolver.FormatFateAwareAttackCheckPreview(formalCheck)),
            "formal fate-aware attack preview should produce display feedback."
        );
    }

    private void TestRepeatAttackResolutionTextRequiresSuccessRate()
    {
        var resolver = new BattleRepeatAttackResolver();
        var legacyOnlyCheck = new AttackCheckInput(hitRatePercent: 87, requiredRoll: 4);
        string legacyText = resolver.FormatRepeatAttackStageResolutionText(
            legacyOnlyCheck,
            new AttackEffectResolutionResult
            {
                AttackSuccess = false,
                AttackResolution = AttackResolutionKind.Miss,
                HitRatePercent = 87,
                ResolutionText = "legacy 87%",
            }
        );
        _test.True(
            !string.IsNullOrWhiteSpace(legacyText),
            "repeat attack resolution text should still produce display feedback for legacy-only input."
        );

        var formalCheck = new AttackCheckInput(
            hitRatePercent: 87,
            successRatePercent: 42,
            requiredRoll: 4
        );
        string formalText = resolver.FormatRepeatAttackStageResolutionText(
            formalCheck,
            new AttackEffectResolutionResult
            {
                AttackSuccess = false,
                AttackResolution = AttackResolutionKind.Miss,
                HitRatePercent = 87,
            }
        );
        _test.Eq(
            formalCheck.SuccessRatePercent,
            42,
            "repeat attack resolution fallback input must carry formal success_rate_percent."
        );
        _test.True(
            !string.IsNullOrWhiteSpace(formalText),
            "repeat attack formal resolution text should produce display feedback."
        );
    }

    private void TestHudBadgeRequiresSuccessRate()
    {
        var adapter = new BattleHudAdapter();
        _test.Eq(
            adapter.FormatSelectedSkillHitBadgeText(new AttackPreviewData { HitRatePercent = 87 }),
            "",
            "HUD hit badge must ignore legacy-only hit_rate_percent."
        );
        string formalBadge = adapter.FormatSelectedSkillHitBadgeText(
            new AttackPreviewData
            {
                SuccessRatePercent = 42,
                HitRatePercent = 87,
            }
        );
        _test.True(
            !string.IsNullOrWhiteSpace(formalBadge),
            "HUD hit badge must render when formal success_rate_percent is present."
        );
        var stagedPreview = new AttackPreviewData
        {
            Stages = new List<AttackPreviewStage> { new AttackPreviewStage(0, 43, 0, 0, 0, "") },
        };
        string formalStageBadge = adapter.FormatSelectedSkillHitBadgeText(
            stagedPreview
        );
        _test.Eq(stagedPreview.StageSuccessRates.Count, 1, "formal stage_success_rates should be exposed.");
        _test.Eq(stagedPreview.StageSuccessRates[0], 43, "formal stage_success_rates should preserve stage value.");
        _test.True(
            !string.IsNullOrWhiteSpace(formalStageBadge),
            "HUD hit badge may render when formal stage_success_rates are present."
        );
    }

    private void TestAiScoreServiceRequiresSuccessRate()
    {
        using var scoreService = new BattleAiScoreService();
        _test.Eq(
            scoreService.ResolveEstimatedHitRatePercent(
                BuildPreview(new AttackPreviewData { HitRatePercent = 87 })
            ),
            100,
            "AI score estimated_hit_rate_percent must ignore legacy-only hit_rate_percent."
        );
        _test.Eq(
            scoreService.ResolveEstimatedHitRatePercent(
                BuildPreview(
                    new AttackPreviewData
                    {
                        SuccessRatePercent = 42,
                        HitRatePercent = 87,
                    }
                )
            ),
            42,
            "AI score estimated_hit_rate_percent must use formal success_rate_percent."
        );
        _test.Eq(
            scoreService.ResolveEstimatedHitRatePercent(
                BuildPreview(
                    new AttackPreviewData
                    {
                        Stages = new List<AttackPreviewStage>
                        {
                            new AttackPreviewStage(0, 40, 0, 0, 0, ""),
                            new AttackPreviewStage(0, 60, 0, 0, 0, ""),
                        },
                        HitRatePercent = 87,
                    }
                )
            ),
            50,
            "AI score estimated_hit_rate_percent must use formal stage_success_rates."
        );
    }

    private static BattlePreview BuildPreview(AttackPreviewData hitPreview)
    {
        return new BattlePreview { hit_preview = hitPreview };
    }

}
