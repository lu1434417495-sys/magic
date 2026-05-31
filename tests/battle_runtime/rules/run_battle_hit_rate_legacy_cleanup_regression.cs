using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_hit_rate_legacy_cleanup_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestHitResolverPreviewRequiresSuccessRate();
        TestRepeatAttackResolutionTextRequiresSuccessRate();
        TestHudBadgeRequiresSuccessRate();
        TestAiScoreServiceRequiresSuccessRate();

        if (_failures.Count == 0)
        {
            GD.Print("Battle hit_rate legacy cleanup regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle hit_rate legacy cleanup regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestHitResolverPreviewRequiresSuccessRate()
    {
        var resolver = new BattleHitResolver();
        var legacyOnlyCheck = new AttackCheckInput(
            hitRatePercent: 87,
            baseHitRatePercent: 0,
            requiredRoll: 4
        );

        string plainLegacyText = resolver.format_attack_check_preview(legacyOnlyCheck);
        AssertTrue(
            plainLegacyText.StartsWith("0%") && !plainLegacyText.Contains("87"),
            "plain attack preview must ignore legacy-only hit_rate_percent."
        );

        AttackCheckInput fateLegacyCheck = resolver._build_fate_aware_attack_check_preview(
            null,
            null,
            null,
            legacyOnlyCheck
        );
        string fateLegacyText = resolver._format_fate_aware_attack_check_preview(fateLegacyCheck);
        AssertTrue(
            fateLegacyText.StartsWith("0%") && !fateLegacyText.Contains("87"),
            "fate-aware attack preview must ignore legacy-only hit_rate_percent."
        );

        var formalCheck = new AttackCheckInput(
            hitRatePercent: 87,
            successRatePercent: 42,
            baseHitRatePercent: 0,
            requiredRoll: 4
        );
        AssertTrue(
            resolver._format_fate_aware_attack_check_preview(formalCheck).StartsWith("42%"),
            "fate-aware attack preview must use formal success_rate_percent."
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
        AssertTrue(
            legacyText.StartsWith("0%") && !legacyText.Contains("87"),
            "repeat attack resolution text must not display legacy-only hit_rate_percent."
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
        AssertEq(
            formalText,
            "42%",
            "repeat attack resolution fallback text must use formal success_rate_percent."
        );
    }

    private void TestHudBadgeRequiresSuccessRate()
    {
        var adapter = new BattleHudAdapter();
        AssertEq(
            adapter._build_selected_skill_hit_badge_text(new AttackPreviewData { HitRatePercent = 87 }),
            "",
            "HUD hit badge must ignore legacy-only hit_rate_percent."
        );
        string formalBadge = adapter._build_selected_skill_hit_badge_text(
            new AttackPreviewData
            {
                SuccessRatePercent = 42,
                HitRatePercent = 87,
            }
        );
        AssertTrue(
            formalBadge.Contains("42%") && !formalBadge.Contains("87"),
            "HUD hit badge must use formal success_rate_percent."
        );
        string formalStageBadge = adapter._build_selected_skill_hit_badge_text(
            new AttackPreviewData
            {
                Stages = new List<AttackPreviewStage> { new AttackPreviewStage(0, 43, 0, 0, 0, "") },
            }
        );
        AssertTrue(
            formalStageBadge.Contains("43%"),
            "HUD hit badge may use formal stage_success_rates."
        );
    }

    private void TestAiScoreServiceRequiresSuccessRate()
    {
        var scoreService = new BattleAiScoreService();
        AssertEq(
            scoreService._resolve_estimated_hit_rate_percent(
                BuildPreview(new AttackPreviewData { HitRatePercent = 87 })
            ),
            100,
            "AI score estimated_hit_rate_percent must ignore legacy-only hit_rate_percent."
        );
        AssertEq(
            scoreService._resolve_estimated_hit_rate_percent(
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
        AssertEq(
            scoreService._resolve_estimated_hit_rate_percent(
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

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }
}
