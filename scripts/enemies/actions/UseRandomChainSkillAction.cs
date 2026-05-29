using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class UseRandomChainSkillAction : EnemyAiAction
{
    private static readonly StringName DistanceRefCandidatePool = "candidate_pool",
        DistanceRefEnemyFrontline = "enemy_frontline";

    public static StringName DISTANCE_REF_CANDIDATE_POOL() => DistanceRefCandidatePool;

    public static StringName DISTANCE_REF_ENEMY_FRONTLINE() => DistanceRefEnemyFrontline;

    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = -1;

    [Export]
    public int desired_max_distance { get; set; } = -1;

    [Export]
    public StringName distance_reference { get; set; } = DistanceRefCandidatePool;

    [Export]
    public int minimum_candidate_count { get; set; } = 1;

    public override BattleAiDecision decide(BattleAiContext context)
    {
        if (!_has_explicit_distance_contract())
            return null;
        var actionTrace = _begin_action_trace(
            context,
            new Godot.Collections.Dictionary
            {
                { "action_kind", "random_chain_skill" },
                { "target_selection_mode", "random_chain" },
                { "target_selector", (string)target_selector },
                { "distance_reference", (string)distance_reference },
                { "desired_min_distance", desired_min_distance },
                { "desired_max_distance", desired_max_distance },
                { "selection_policy", "random_from_living_pool" },
                { "pool_refresh_policy", "before_each_attempt" },
                { "score_estimate_policy", "expected_value" },
                { "minimum_candidate_count", minimum_candidate_count },
            }
        );
        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids))
        {
            _trace_count_increment(actionTrace, "skill_considered_count", 1);
            var skillDef = _get_skill_def(context, sid);
            if (skillDef?.combat_profile == null || !_is_random_chain_skill(skillDef))
            {
                _trace_add_block_reason(
                    actionTrace,
                    skillDef == null ? "missing_skill_def" : "non_random_chain_skill"
                );
                continue;
            }
            var blockReason = _get_skill_cast_block_reason(context, skillDef);
            if (blockReason.Length > 0)
            {
                _trace_add_block_reason(actionTrace, blockReason);
                continue;
            }
            foreach (var cv in _get_random_chain_cast_variants(context, skillDef))
            {
                _trace_count_increment(actionTrace, "evaluation_count", 1);
                var command = _build_random_chain_skill_command(context, sid, cv);
                BattlePreview preview = _build_fast_random_chain_skill_preview(
                    context,
                    skillDef,
                    cv,
                    command
                );
                if (preview?.allowed != true)
                {
                    _trace_count_increment(actionTrace, "preview_reject_count", 1);
                    continue;
                }
                var candidateUnits = _resolve_candidate_units(context, preview, skillDef);
                if (candidateUnits.Count == 0)
                {
                    _trace_add_block_reason(actionTrace, "no_random_chain_candidates");
                    continue;
                }
                if (candidateUnits.Count < Mathf.Max(minimum_candidate_count, 1))
                {
                    _trace_add_block_reason(
                        actionTrace,
                        "minimum_random_chain_candidate_count"
                    );
                    continue;
                }
                var candidateIds = _candidate_unit_ids(candidateUnits);
                var posMeta = _build_position_metadata(context, candidateUnits, skillDef);
                posMeta["action_kind"] = "random_chain_skill";
                posMeta["target_selection_mode"] = "random_chain";
                posMeta["action_label"] = _format_skill_variant_label(skillDef, cv);
                posMeta["candidate_pool_unit_ids"] = new Godot.Collections.Array<StringName>(
                    candidateIds
                );
                posMeta["candidate_pool_count"] = candidateIds.Count;
                int maxHits = Mathf.Max(
                    (skillDef.combat_profile as CombatSkillDef).max_hits_per_target,
                    1
                );
                posMeta["random_chain_max_hits_per_target"] = maxHits;
                posMeta["random_chain_max_attempt_count"] = Mathf.Max(
                    candidateIds.Count * maxHits,
                    1
                );
                posMeta["random_chain_selection_policy"] = "random_from_living_pool";
                posMeta["random_chain_pool_refresh_policy"] = "before_each_attempt";
                posMeta["random_chain_score_estimate_policy"] = "expected_value";
                _update_trace_metadata(actionTrace, posMeta);
                var scoreInput = _build_typed_skill_score_input(
                    context,
                    skillDef,
                    command,
                    preview,
                    _collect_random_chain_effect_defs(skillDef, cv),
                    posMeta
                );
                var ctxUnitState = context.unit_state;
                if (scoreInput == null)
                {
                    if (fallbackDecision == null)
                        fallbackDecision = _create_decision(
                            command,
                            $"{ctxUnitState.display_name} 准备发动 {skillDef.display_name}，候选池 {candidateIds.Count} 个单位。"
                        );
                    _trace_offer_candidate(
                        actionTrace,
                        _build_candidate_summary(
                            _format_skill_variant_label(skillDef, cv),
                            command,
                            null,
                            new Godot.Collections.Dictionary
                            {
                                { "skill_id", (string)sid },
                                { "candidate_pool_count", candidateIds.Count },
                                { "candidate_pool_unit_ids", _stringify_unit_ids(candidateIds) },
                            }
                        )
                    );
                    continue;
                }
                _trace_offer_candidate(
                    actionTrace,
                    _build_candidate_summary(
                        _format_skill_variant_label(skillDef, cv),
                        command,
                        scoreInput,
                        new Godot.Collections.Dictionary
                        {
                            { "skill_id", (string)sid },
                            { "candidate_pool_count", candidateIds.Count },
                            { "candidate_pool_unit_ids", _stringify_unit_ids(candidateIds) },
                        }
                    )
                );
                if (!_is_better_skill_score_input(scoreInput, bestScoreInput))
                    continue;
                bestScoreInput = scoreInput;
                bestDecision = _create_scored_decision(
                    command,
                    scoreInput,
                    $"{ctxUnitState.display_name} 准备发动 {skillDef.display_name}，候选池 {candidateIds.Count} 个单位（评分 {_score_total(scoreInput)}）。"
                );
            }
        }
        var resolved = bestDecision ?? fallbackDecision;
        _finalize_action_trace(context, actionTrace, resolved);
        return resolved;
    }

    private static bool _is_random_chain_skill(SkillDef sd) =>
        sd?.combat_profile != null
        && BattleTypedNames.ToTargetMode((sd.combat_profile as CombatSkillDef).target_mode)
            == BattleTargetMode.Unit
        && BattleTypedNames.ToTargetSelectionMode(
            ProgressionDataUtils.to_string_name(
                (sd.combat_profile as CombatSkillDef).target_selection_mode
            )
        ) == BattleTargetSelectionMode.RandomChain;

    private Godot.Collections.Array<CombatCastVariantDef> _get_random_chain_cast_variants(
        BattleAiContext context,
        SkillDef sd
    )
    {
        var r = new Godot.Collections.Array<CombatCastVariantDef>();
        if (sd?.combat_profile == null)
            return r;
        var cp = sd.combat_profile as CombatSkillDef;
        if (cp.cast_variants.Count == 0)
        {
            r.Add(null);
            return r;
        }
        int sl = context?.unit_state != null ? _get_skill_level(context.unit_state, sd.skill_id) : 0;
        foreach (var cv in cp.get_unlocked_cast_variants(sl))
            if (cv != null)
                r.Add(cv);
        return r;
    }

    private static BattleCommand _build_random_chain_skill_command(
        BattleAiContext context,
        StringName sid,
        CombatCastVariantDef cv
    )
    {
        if (context?.unit_state == null)
            return null;
        var cmd = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = context.unit_state.unit_id,
            skill_id = sid,
            skill_variant_id = cv?.variant_id ?? new StringName(""),
        };
        return cmd;
    }

    private List<BattleUnitState> _resolve_candidate_units(
        BattleAiContext context,
        BattlePreview preview,
        SkillDef sd
    )
    {
        var cids = new HashSet<StringName>();
        if (preview != null)
            foreach (var ru in preview.random_chain_candidate_unit_ids)
            {
                var uid = ProgressionDataUtils.to_string_name(ru);
                if (uid != "")
                    cids.Add(uid);
            }
        if (cids.Count == 0)
            return new List<BattleUnitState>();
        List<BattleUnitState> sorted = _sort_target_units_typed(
            context,
            (sd.combat_profile as CombatSkillDef).target_team_filter,
            target_selector
        );
        var r = new List<BattleUnitState>();
        foreach (BattleUnitState su in sorted)
        {
            if (su != null && cids.Contains(su.unit_id))
                r.Add(su);
        }
        return r;
    }

    private static List<StringName> _candidate_unit_ids(
        IEnumerable<BattleUnitState> candidates
    )
    {
        var r = new List<StringName>();
        foreach (var c in candidates)
            if (c != null)
                r.Add(c.unit_id);
        return r;
    }

    private static Godot.Collections.Array<string> _stringify_unit_ids(
        IEnumerable<StringName> ids
    )
    {
        var r = new Godot.Collections.Array<string>();
        foreach (var id in ids)
            r.Add((string)id);
        return r;
    }

    private static List<CombatEffectDef> _collect_random_chain_effect_defs(
        SkillDef sd,
        CombatCastVariantDef cv
    )
    {
        var r = new List<CombatEffectDef>();
        if (sd?.combat_profile != null)
            foreach (var ed in (sd.combat_profile as CombatSkillDef).effect_defs)
                if (ed != null)
                    r.Add(ed);
        if (cv != null)
            foreach (var ed in cv.effect_defs)
                if (ed != null)
                    r.Add(ed);
        return r;
    }

    private Godot.Collections.Dictionary _build_position_metadata(
        BattleAiContext context,
        IReadOnlyList<BattleUnitState> candidates,
        SkillDef sd
    )
    {
        var dc = _resolve_desired_distance_contract(context, sd);
        var m = dc;
        if (distance_reference == DistanceRefCandidatePool)
        {
            var pc = candidates.Count > 0 ? candidates[0] : null;
            if (pc != null)
                m["position_target_unit_id"] = pc.unit_id;
            else
                m["position_objective_kind"] = "none";
        }
        else if (distance_reference == DistanceRefEnemyFrontline)
        {
            var fl = _resolve_enemy_frontline_unit(context);
            if (fl != null)
                m["position_target_unit_id"] = fl.unit_id;
            else
                m["position_objective_kind"] = "none";
        }
        else
            m["position_objective_kind"] = "none";
        return m;
    }

    private BattleUnitState _resolve_enemy_frontline_unit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _sort_target_units_typed(
            context,
            "enemy",
            "nearest_enemy"
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private static void _update_trace_metadata(
        Godot.Collections.Dictionary at,
        Godot.Collections.Dictionary sm
    )
    {
        if (at.Count == 0)
            return;
        var m = at.ContainsKey("metadata")
            ? at["metadata"].AsGodotDictionary()
            : new Godot.Collections.Dictionary();
        m["candidate_pool_count"] = sm.ContainsKey("candidate_pool_count")
            ? sm["candidate_pool_count"].AsInt32()
            : 0;
        m["candidate_pool_unit_ids"] = _stringify_unit_ids(
            sm.ContainsKey("candidate_pool_unit_ids")
                ? ProgressionDataUtils.to_string_name_array(sm["candidate_pool_unit_ids"])
                : new Godot.Collections.Array<StringName>()
        );
        m["max_hits_per_target"] = sm.ContainsKey("random_chain_max_hits_per_target")
            ? sm["random_chain_max_hits_per_target"].AsInt32()
            : 0;
        m["max_attempt_count"] = sm.ContainsKey("random_chain_max_attempt_count")
            ? sm["random_chain_max_attempt_count"].AsInt32()
            : 0;
        at["metadata"] = m;
    }

    private bool _has_explicit_distance_contract() =>
        desired_min_distance >= 0
        && desired_max_distance >= desired_min_distance
        && (
            distance_reference == DistanceRefCandidatePool
            || distance_reference == DistanceRefEnemyFrontline
        );

    public override Godot.Collections.Array<string> validate_schema()
    {
        var e = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            e.Add($"UseRandomChainSkillAction {action_id} must declare at least one skill_id.");
        if (target_selector == "")
            e.Add($"UseRandomChainSkillAction {action_id} is missing target_selector.");
        if (desired_min_distance < 0)
            e.Add($"UseRandomChainSkillAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
            e.Add(
                $"UseRandomChainSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        if (
            distance_reference != DistanceRefCandidatePool
            && distance_reference != DistanceRefEnemyFrontline
        )
            e.Add(
                $"UseRandomChainSkillAction {action_id} distance_reference must be candidate_pool or enemy_frontline."
            );
        if (minimum_candidate_count < 1)
            e.Add(
                $"UseRandomChainSkillAction {action_id} minimum_candidate_count must be >= 1."
            );
        return e;
    }
}
