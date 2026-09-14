using System.Collections.Generic;
using Godot;

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
        string legacyPlainText = resolver.FormatAttackCheckPreview(legacyOnlyCheck);
        _test.True(
            legacyPlainText.Contains("0%") && !legacyPlainText.Contains("87%"),
            $"plain attack preview should display the formal default 0%, never legacy hit_rate_percent. text={legacyPlainText}"
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
        string legacyFateText = resolver.FormatFateAwareAttackCheckPreview(fateLegacyCheck);
        _test.True(
            legacyFateText.Contains("0%") && !legacyFateText.Contains("87%"),
            $"fate-aware preview should display the formal default 0%, never legacy hit_rate_percent. text={legacyFateText}"
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
        string formalFateText = resolver.FormatFateAwareAttackCheckPreview(formalCheck);
        _test.True(
            formalFateText.Contains("42%") && !formalFateText.Contains("87%"),
            $"formal fate-aware preview should display success_rate_percent and exclude legacy hit_rate_percent. text={formalFateText}"
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
        _test.Eq(
            legacyText,
            "0%",
            "repeat attack resolution should use the missing formal success rate's 0% default and ignore both legacy hit rate and legacy resolution text."
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
        _test.Eq(
            formalText,
            "42%",
            "repeat attack resolution should display formal success_rate_percent and ignore legacy hit_rate_percent."
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
        _test.Eq(
            formalBadge,
            "命中 42%",
            "HUD hit badge must render formal success_rate_percent, not legacy hit_rate_percent."
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
        _test.Eq(
            formalStageBadge,
            "命中 43%",
            "HUD hit badge should use the first formal stage success rate when the aggregate rate is absent."
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
