using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_score_ordering_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestScoreInputSealUsesTypedFingerprint();
        TestNullCandidateRules();
        TestOrderingPriorityChain();
        TestLowerCostTieBreakers();
        TestDecisionEngineOrderingMigratedFromGdRunner();
        TestGroundControlMinimumPolicyMigratedFromGdRunner();

        Quit(_test.Finish("Battle AI score ordering regression"));
    }

    private void TestNullCandidateRules()
    {
        _test.True(!BattleAiScoreOrdering.IsBetter(null, Score()), "null candidate 不应优于已有候选。");
        _test.True(BattleAiScoreOrdering.IsBetter(Score(), null), "非空 candidate 应优于 null best。");
    }

    private void TestScoreInputSealUsesTypedFingerprint()
    {
        var score = new BattleAiScoreInput
        {
            action_kind = "skill",
            action_label = "typed fingerprint",
            estimated_damage = 12,
        };
        score.target_unit_ids.Add("enemy_a");
        score.high_priority_reasons["enemy_a"] = new System.Collections.Generic.List<string>
        {
            "focus_fire",
        };

        score.Seal();
        _test.True(score.IsSealed(), "BattleAiScoreInput.Seal 应保留 sealed 状态。");
        _test.True(score.MatchesSealedFingerprint(), "未变更时 sealed fingerprint 应匹配。");

        score.target_unit_ids.Add("enemy_b");
        _test.True(
            !score.MatchesSealedFingerprint(),
            "typed list 变更后 sealed fingerprint 应失配。"
        );

        score.target_unit_ids.Remove("enemy_b");
        _test.True(score.MatchesSealedFingerprint(), "恢复 typed list 后 fingerprint 应重新匹配。");

        score.estimated_damage += 5;
        _test.True(
            !score.MatchesSealedFingerprint(),
            "typed scalar 变更后 sealed fingerprint 应失配。"
        );
    }

    private void TestOrderingPriorityChain()
    {
        AssertBetter(
            Score(bucket: 2),
            Score(bucket: 1, lethalThreats: 99, lethalTargets: 99, total: 9999, hit: 100),
            "score_bucket_priority 应是最高优先级。"
        );
        AssertBetter(
            Score(lethalThreats: 2),
            Score(lethalThreats: 1, lethalTargets: 99, total: 9999, hit: 100),
            "estimated_lethal_threat_target_count 应优先于 lethal target / total score。"
        );
        AssertBetter(
            Score(lethalTargets: 2),
            Score(lethalTargets: 1, total: 9999, hit: 100),
            "estimated_lethal_target_count 应优先于 total score。"
        );
        AssertBetter(
            Score(total: 20),
            Score(total: 10, hit: 100),
            "total_score 应优先于命中率。"
        );
        AssertBetter(
            Score(hit: 80),
            Score(hit: 70),
            "estimated_hit_rate_percent 应在 total_score 相同时作为 tie-breaker。"
        );
    }

    private void TestLowerCostTieBreakers()
    {
        AssertBetter(
            Score(move: 1, resource: 10),
            Score(move: 2, resource: 0),
            "move_cost 更低应优先于 resource_cost_score。"
        );
        AssertBetter(
            Score(move: 2, resource: 1),
            Score(move: 2, resource: 5),
            "resource_cost_score 更低应作为最后 tie-breaker。"
        );
        _test.True(
            !BattleAiScoreOrdering.IsBetter(Score(move: 2, resource: 5), Score(move: 2, resource: 5)),
            "完全相同的分数不应替换当前 best。"
        );
    }

    private void TestDecisionEngineOrderingMigratedFromGdRunner()
    {
        var engine = new BattleAiDecisionEngine();
        var actionProbe = new ScoreComparisonProbeAction();

        BattleAiScoreInput lethalThreatOffense = ScoreForDecisionOrdering(
            "mist_offense",
            100,
            80,
            lethalThreatCount: 1,
            lethalCount: 1,
            effectiveTargetCount: 1
        );
        BattleAiScoreInput lethalEnemyOffense = ScoreForDecisionOrdering(
            "mist_offense",
            100,
            80,
            lethalCount: 1,
            effectiveTargetCount: 1
        );
        BattleAiScoreInput control = ScoreForDecisionOrdering(
            "mist_control",
            110,
            900,
            effectiveTargetCount: 2
        );
        AssertEngineBetter(
            engine,
            lethalThreatOffense,
            control,
            "跨 action bucket 比较时，威胁击杀应压过普通控场 bucket。"
        );
        AssertActionBetter(
            actionProbe,
            lethalThreatOffense,
            control,
            "单个 action 内部候选比较也应让威胁击杀压过普通控场。"
        );

        BattleAiScoreInput emergencyEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            220,
            positionScore: 120
        );
        emergencyEscape.position_current_distance = 2;
        emergencyEscape.position_safe_distance = 4;
        emergencyEscape.distance_to_primary_coord = 4;
        AssertEngineBetter(
            engine,
            lethalThreatOffense,
            emergencyEscape,
            "能击杀威胁目标时，应压过纯逃生换位。"
        );
        AssertEngineBetter(
            engine,
            lethalEnemyOffense,
            emergencyEscape,
            "普通击杀也应压过纯逃生换位。"
        );

        BattleAiScoreInput mildEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            220,
            positionScore: 120
        );
        mildEscape.position_current_distance = 3;
        mildEscape.position_safe_distance = 4;
        mildEscape.distance_to_primary_coord = 4;
        _test.True(
            engine.IsBetterScoreInput(lethalThreatOffense, mildEscape),
            "轻度不安全的换位不应压过可击杀目标。"
        );

        BattleAiScoreInput shortEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            260,
            positionScore: 160
        );
        shortEscape.position_current_distance = 1;
        shortEscape.position_safe_distance = 4;
        shortEscape.distance_to_primary_coord = 3;
        _test.True(
            engine.IsBetterScoreInput(lethalEnemyOffense, shortEscape),
            "未真正脱离安全距离的换位不应以紧急生存身份压过击杀。"
        );

        BattleAiScoreInput unsafeLethalEnemyOffense = ScoreForDecisionOrdering(
            "mist_offense",
            100,
            180,
            lethalCount: 1,
            effectiveTargetCount: 1
        );
        BattleAiScoreInput safeEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            120,
            positionScore: 120
        );
        MarkSurvivalProjection(unsafeLethalEnemyOffense, true, true, 45, -21);
        MarkSurvivalProjection(safeEscape, true, false, 0, 24);
        AssertEngineBetter(
            engine,
            safeEscape,
            unsafeLethalEnemyOffense,
            "若击杀后仍会被剩余威胁秒杀，安全换位应跨 bucket 压过普通击杀。"
        );
        AssertActionBetter(
            actionProbe,
            safeEscape,
            unsafeLethalEnemyOffense,
            "单个 action 内部候选比较也应使用行动后致死风险。"
        );

        MarkSurvivalProjection(lethalThreatOffense, true, false, 0, 24);
        MarkSurvivalProjection(safeEscape, true, false, 0, 24);
        _test.True(
            engine.IsBetterScoreInput(lethalThreatOffense, safeEscape),
            "当击杀动作同样解除致死风险时，威胁击杀应继续压过纯生存动作。"
        );

        BattleAiScoreInput riskFreeEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            220,
            positionScore: 120
        );
        BattleAiScoreInput riskyEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            220,
            positionScore: 120
        );
        MarkNonfatalSurvivalProjection(riskFreeEscape, 0, 0, 24);
        MarkNonfatalSurvivalProjection(riskyEscape, 1, 8, 16);
        AssertEngineBetter(
            engine,
            riskFreeEscape,
            riskyEscape,
            "跨 action 同等收益时，应优先选择行动后无剩余威胁的换位。"
        );
        AssertActionBetter(
            actionProbe,
            riskFreeEscape,
            riskyEscape,
            "闪现候选同等收益时，应优先选择行动后无剩余威胁的落点。"
        );

        BattleAiScoreInput zeroDamageSafe = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            220,
            positionScore: 120
        );
        BattleAiScoreInput zeroDamageRisky = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            220,
            positionScore: 120
        );
        MarkNonfatalSurvivalProjection(zeroDamageSafe, 0, 0, 24);
        MarkNonfatalSurvivalProjection(zeroDamageRisky, 1, 0, 24);
        _test.True(
            actionProbe.IsBetter(zeroDamageSafe, zeroDamageRisky),
            "闪现候选同等收益且预期伤害同为 0 时，仍应优先无威胁落点。"
        );

        BattleAiScoreInput lethalSafe = ScoreForDecisionOrdering(
            "mist_offense",
            100,
            180,
            lethalCount: 1,
            effectiveTargetCount: 1
        );
        BattleAiScoreInput lethalRisky = ScoreForDecisionOrdering(
            "mist_offense",
            100,
            180,
            lethalCount: 1,
            effectiveTargetCount: 1
        );
        MarkNonfatalSurvivalProjection(lethalSafe, 0, 0, 24);
        MarkNonfatalSurvivalProjection(lethalRisky, 1, 8, 16);
        AssertEngineBetter(
            engine,
            lethalSafe,
            lethalRisky,
            "同等击杀价值下，也应优先行动后威胁更低的候选。"
        );
        AssertActionBetter(
            actionProbe,
            lethalSafe,
            lethalRisky,
            "单个 action 内同等击杀价值下，也应优先行动后威胁更低的候选。"
        );

        BattleAiScoreInput higherValueRiskyEscape = ScoreForDecisionOrdering(
            "archer_survival",
            150,
            240,
            positionScore: 120
        );
        MarkNonfatalSurvivalProjection(higherValueRiskyEscape, 1, 8, 16);
        _test.True(
            engine.IsBetterScoreInput(higherValueRiskyEscape, riskFreeEscape),
            "跨 action 比较中，更高收益的非致死风险换位仍应允许胜出。"
        );
        _test.True(
            actionProbe.IsBetter(higherValueRiskyEscape, riskFreeEscape),
            "闪现候选中，更高收益的非致死风险落点仍应允许胜出。"
        );

        BattleAiScoreInput higherValueLethalRisky = ScoreForDecisionOrdering(
            "mist_offense",
            100,
            200,
            lethalCount: 1,
            effectiveTargetCount: 1
        );
        MarkNonfatalSurvivalProjection(higherValueLethalRisky, 1, 8, 16);
        _test.True(
            engine.IsBetterScoreInput(higherValueLethalRisky, lethalSafe),
            "击杀候选中，更高收益的非致死风险动作仍应允许胜出。"
        );
        _test.True(
            actionProbe.IsBetter(higherValueLethalRisky, lethalSafe),
            "单个 action 内击杀候选中，更高收益的非致死风险动作仍应允许胜出。"
        );
    }

    private void TestGroundControlMinimumPolicyMigratedFromGdRunner()
    {
        var action = new UseGroundSkillAction
        {
            action_id = "partial_hit_ground_control_probe",
            minimum_hit_count = 2,
            allow_empty_ground_control = true,
            minimum_ground_control_score = 1,
        };
        var scoreInput = new BattleAiScoreInput
        {
            effective_target_count = 1,
            estimated_ground_control_cell_count = 3,
            ground_control_score = 999,
        };
        _test.True(
            !action.PassesMinimumEffectiveTargetOrGroundControl(scoreInput),
            "已有有效命中但未达到 minimum_hit_count 时，空地控场豁免不能绕过命中门槛。"
        );

        action.allow_ground_control_supplement_partial_hits = true;
        _test.True(
            action.PassesMinimumEffectiveTargetOrGroundControl(scoreInput),
            "显式开启地格控制补足时，部分命中且地格控制分达标的候选应能通过。"
        );
    }

    private static BattleAiScoreInput Score(
        int bucket = 0,
        int lethalThreats = 0,
        int lethalTargets = 0,
        int total = 0,
        int hit = 0,
        int move = 0,
        int resource = 0
    ) =>
        new()
        {
            score_bucket_priority = bucket,
            estimated_lethal_threat_target_count = lethalThreats,
            estimated_lethal_target_count = lethalTargets,
            total_score = total,
            estimated_hit_rate_percent = hit,
            move_cost = move,
            resource_cost_score = resource,
        };

    private static BattleAiScoreInput ScoreForDecisionOrdering(
        StringName bucketId,
        int bucketPriority,
        int totalScore,
        int lethalThreatCount = 0,
        int lethalCount = 0,
        int effectiveTargetCount = 0,
        int positionScore = 0
    ) =>
        new()
        {
            score_bucket_id = bucketId,
            score_bucket_priority = bucketPriority,
            total_score = totalScore,
            hit_payoff_score = totalScore,
            target_count = effectiveTargetCount,
            effective_target_count = effectiveTargetCount,
            enemy_target_count = effectiveTargetCount,
            estimated_damage = effectiveTargetCount > 0 ? 10 : 0,
            estimated_control_count = bucketId == new StringName("mist_control") ? effectiveTargetCount : 0,
            estimated_lethal_threat_target_count = lethalThreatCount,
            estimated_lethal_target_count = lethalCount,
            position_objective_score = positionScore,
        };

    private static void MarkSurvivalProjection(
        BattleAiScoreInput scoreInput,
        bool preFatal,
        bool postFatal,
        int postDamage,
        int postMargin
    )
    {
        if (scoreInput == null)
        {
            return;
        }
        scoreInput.has_post_action_threat_projection = true;
        scoreInput.pre_action_threat_count = preFatal ? 1 : 0;
        scoreInput.pre_action_threat_expected_damage = preFatal ? 40 : 0;
        scoreInput.pre_action_survival_margin = preFatal ? -16 : 24;
        scoreInput.pre_action_is_lethal_survival_risk = preFatal;
        scoreInput.post_action_remaining_threat_count = postFatal ? 1 : 0;
        scoreInput.post_action_remaining_threat_expected_damage = postDamage;
        scoreInput.post_action_survival_margin = postMargin;
        scoreInput.post_action_is_lethal_survival_risk = postFatal;
    }

    private static void MarkNonfatalSurvivalProjection(
        BattleAiScoreInput scoreInput,
        int postCount,
        int postDamage,
        int postMargin
    )
    {
        if (scoreInput == null)
        {
            return;
        }
        scoreInput.has_post_action_threat_projection = true;
        scoreInput.pre_action_threat_count = 0;
        scoreInput.pre_action_threat_expected_damage = 0;
        scoreInput.pre_action_survival_margin = 24;
        scoreInput.pre_action_is_lethal_survival_risk = false;
        scoreInput.post_action_remaining_threat_count = postCount;
        scoreInput.post_action_remaining_threat_expected_damage = postDamage;
        scoreInput.post_action_survival_margin = postMargin;
        scoreInput.post_action_is_lethal_survival_risk = false;
    }

    private void AssertBetter(BattleAiScoreInput candidate, BattleAiScoreInput best, string message)
    {
        _test.True(BattleAiScoreOrdering.IsBetter(candidate, best), message);
        _test.True(!BattleAiScoreOrdering.IsBetter(best, candidate), $"{message} 反向比较不应成立。");
    }

    private void AssertEngineBetter(
        BattleAiDecisionEngine engine,
        BattleAiScoreInput candidate,
        BattleAiScoreInput best,
        string message
    )
    {
        _test.True(engine.IsBetterScoreInput(candidate, best), message);
        _test.True(!engine.IsBetterScoreInput(best, candidate), $"{message} 反向比较不应成立。");
    }

    private void AssertActionBetter(
        ScoreComparisonProbeAction action,
        BattleAiScoreInput candidate,
        BattleAiScoreInput best,
        string message
    )
    {
        _test.True(action.IsBetter(candidate, best), message);
        _test.True(!action.IsBetter(best, candidate), $"{message} 反向比较不应成立。");
    }

    private sealed partial class ScoreComparisonProbeAction : EnemyAiAction
    {
        public bool IsBetter(BattleAiScoreInput candidate, BattleAiScoreInput best) =>
            _is_better_skill_score_input(candidate, best);
    }
}
