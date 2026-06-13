using System;
using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class UseChargeAction : EnemyAiAction
{
    private static readonly Vector2I[] ChargeDirections =
    {
        Vector2I.Left,
        Vector2I.Right,
        Vector2I.Up,
        Vector2I.Down,
    };

    private readonly struct ChargeTargetInfo
    {
        public readonly bool Valid;
        public readonly int Distance;
        public readonly Vector2I Direction;
        public readonly Vector2I PredictedAnchor;

        public ChargeTargetInfo(bool valid, int distance, Vector2I direction, Vector2I predictedAnchor)
        {
            Valid = valid;
            Distance = distance;
            Direction = direction;
            PredictedAnchor = predictedAnchor;
        }
    }

    private readonly struct ChargeDistanceBreakpoint
    {
        public readonly int Level;
        public readonly int Distance;

        public ChargeDistanceBreakpoint(int level, int distance)
        {
            Level = level;
            Distance = distance;
        }
    }

    [Export]
    public StringName skill_id { get; set; } = "charge";

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_charge_move_distance { get; set; } = 3;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:charge");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:charge");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        var actionTrace = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "charge" },
                { "target_selector", (string)target_selector },
            }
        );
        var skillDef = _get_skill_def(context, skill_id);
        if (
            skillDef?.combat_profile == null
            || (skillDef.combat_profile as CombatSkillDef).TargetModeKind
                != BattleTargetMode.Ground
        )
        {
            _trace_add_block_reason(actionTrace, "invalid_charge_skill");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        var blockReason = _get_skill_cast_block_reason(context, skillDef);
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            _trace_add_block_reason(
                actionTrace,
                BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
            );
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        AiTraceRecorder.Enter("charge:sort_targets");
        List<BattleUnitState> targets = _sort_target_units_typed(context, "enemy", target_selector);
        AiTraceRecorder.Exit("charge:sort_targets");
        if (targets.Count == 0)
        {
            _trace_add_block_reason(actionTrace, "no_valid_targets");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        var focusTarget = targets[0];
        var ctxUnitState = context.unit_state;
        int focusTargetDist = _distance_between_units(context, ctxUnitState, focusTarget);
        actionTrace.Metadata["focus_target_distance"] = focusTargetDist;
        actionTrace.Metadata["minimum_charge_move_distance"] = minimum_charge_move_distance;
        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        int bestFallbackScore = -999999;
        var chargeInfoCache = new Dictionary<Vector2I, ChargeTargetInfo>();
        var focusDistanceByAnchor = new Dictionary<Vector2I, int>();

        ChargeTargetInfo ResolveChargeInfo(Vector2I targetCoord)
        {
            if (chargeInfoCache.TryGetValue(targetCoord, out ChargeTargetInfo cachedInfo))
            {
                return cachedInfo;
            }
            ChargeTargetInfo resolvedInfo = _resolve_charge_target_info(ctxUnitState, targetCoord);
            chargeInfoCache[targetCoord] = resolvedInfo;
            return resolvedInfo;
        }

        int DistanceFromAnchorToFocus(Vector2I anchorCoord)
        {
            if (focusDistanceByAnchor.TryGetValue(anchorCoord, out int cachedDistance))
            {
                return cachedDistance;
            }
            int distance = _distance_from_anchor_to_unit(
                context,
                ctxUnitState,
                anchorCoord,
                focusTarget
            );
            focusDistanceByAnchor[anchorCoord] = distance;
            return distance;
        }

        AiTraceRecorder.Enter("charge:get_ground_options");
        List<CombatCastVariantDef> groundOptions = _get_ground_options_typed(context, skillDef);
        AiTraceRecorder.Exit("charge:get_ground_options");
        foreach (CombatCastVariantDef cv in groundOptions)
        {
            if (cv == null || !_is_charge_option(cv))
                continue;
            string variantLabel = _format_skill_variant_label(skillDef, cv);
            AiTraceRecorder.Enter("charge:evaluate_variant");
            foreach (Vector2I targetCoord in _enumerate_charge_target_coords(context, ctxUnitState, cv))
            {
                _trace_count_increment(actionTrace, "evaluation_count", 1);
                var chargeInfo = ResolveChargeInfo(targetCoord);
                if (!chargeInfo.Valid)
                    continue;
                int predictedDist = DistanceFromAnchorToFocus(chargeInfo.PredictedAnchor);
                if (predictedDist >= focusTargetDist)
                {
                    _trace_add_block_reason(actionTrace, "charge_does_not_close_focus_target");
                    continue;
                }
                var shortBlock = _resolve_short_charge_block_reason(
                    context,
                    chargeInfo.PredictedAnchor,
                    chargeInfo.Distance,
                    focusTargetDist
                );
                if (shortBlock.Length > 0)
                {
                    _trace_add_block_reason(actionTrace, shortBlock);
                    continue;
                }
                var command = _build_typed_ground_skill_command(
                    context,
                    skill_id,
                    cv.variant_id,
                    new[] { targetCoord }
                );
                AiTraceRecorder.Enter("charge:formal_preview");
                BattlePreview preview = context.PreviewCommand(command);
                AiTraceRecorder.Exit("charge:formal_preview");
                preview ??= BuildFastChargePreview(command, chargeInfo, targetCoord);
                if (preview?.allowed != true)
                {
                    _trace_count_increment(actionTrace, "preview_reject_count", 1);
                    continue;
                }
                var resolvedAnchor = preview.resolved_anchor_coord;
                if (resolvedAnchor == new Vector2I(-1, -1))
                    resolvedAnchor = ctxUnitState.coord;
                int resolvedDist = DistanceFromAnchorToFocus(resolvedAnchor);
                int resolvedMoveDist =
                    context.grid_service?.GetDistance(ctxUnitState.coord, resolvedAnchor) ?? 0;
                var resolvedShortBlock = _resolve_short_charge_block_reason(
                    context,
                    resolvedAnchor,
                    resolvedMoveDist,
                    focusTargetDist
                );
                if (resolvedShortBlock.Length > 0)
                {
                    _trace_add_block_reason(actionTrace, resolvedShortBlock);
                    continue;
                }
                AiTraceRecorder.Enter("charge:formal_score_input");
                var scoreInput = BuildChargeScoreInput(
                    context,
                    skillDef,
                    command,
                    preview,
                    cv,
                    focusTarget,
                    resolvedAnchor,
                    resolvedMoveDist,
                    variantLabel
                );
                AiTraceRecorder.Exit("charge:formal_score_input");
                _trace_offer_candidate(
                    actionTrace,
                    _build_candidate_summary(
                        $"{variantLabel}->{focusTarget.display_name}",
                        command,
                        scoreInput,
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "resolved_anchor_coord", resolvedAnchor },
                            { "resolved_distance", resolvedDist },
                            { "resolved_move_distance", resolvedMoveDist },
                        }
                    )
                );
                if (scoreInput != null)
                {
                    if (!_is_better_skill_score_input(scoreInput, bestScoreInput))
                        continue;
                    bestScoreInput = scoreInput;
                    bestDecision = _create_scored_decision(
                        command,
                        scoreInput,
                        $"{ctxUnitState.display_name} 准备用冲锋逼近 {focusTarget.display_name}（评分 {_score_total(scoreInput)}）。"
                    );
                    continue;
                }
                int movedDist =
                    context.grid_service?.GetDistance(ctxUnitState.coord, resolvedAnchor) ?? 0;
                int fallback = 1000 - resolvedDist * 100 + movedDist;
                if (fallback <= bestFallbackScore)
                    continue;
                bestFallbackScore = fallback;
                bestDecision = _create_decision(
                    command,
                    $"{ctxUnitState.display_name} 准备用冲锋逼近 {focusTarget.display_name}。"
                );
            }
            AiTraceRecorder.Exit("charge:evaluate_variant");
        }
        _finalize_action_trace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    private IEnumerable<Vector2I> _enumerate_charge_target_coords(
        BattleAiContext context,
        BattleUnitState unitState,
        CombatCastVariantDef castVariant
    )
    {
        if (context?.grid_service == null || context.state == null || unitState == null || castVariant == null)
            yield break;

        unitState.RefreshFootprint();
        int maxDistance = _resolve_charge_max_distance(unitState, castVariant);
        if (maxDistance <= 0)
            yield break;

        int minX = unitState.coord.X;
        int maxX = unitState.coord.X + unitState.footprint_size.X - 1;
        int minY = unitState.coord.Y;
        int maxY = unitState.coord.Y + unitState.footprint_size.Y - 1;
        int anchorX = unitState.coord.X;
        int anchorY = unitState.coord.Y;

        foreach (Vector2I direction in ChargeDirections)
        {
            for (int distance = 1; distance <= maxDistance; distance += 1)
            {
                Vector2I targetCoord = direction == Vector2I.Left
                    ? new Vector2I(minX - distance, anchorY)
                    : direction == Vector2I.Right
                        ? new Vector2I(maxX + distance, anchorY)
                        : direction == Vector2I.Up
                            ? new Vector2I(anchorX, minY - distance)
                            : new Vector2I(anchorX, maxY + distance);
                if (context.grid_service.IsInside(context.state, targetCoord))
                    yield return targetCoord;
            }
        }
    }

    private static int _resolve_charge_max_distance(
        BattleUnitState unitState,
        CombatCastVariantDef castVariant
    )
    {
        CombatEffectDef chargeEffect = null;
        foreach (CombatEffectDef effectDef in castVariant.effect_defs)
        {
            if (effectDef != null && effectDef.EffectKind == BattleEffectKind.Charge)
            {
                chargeEffect = effectDef;
                break;
            }
        }
        if (chargeEffect == null)
            return 0;

        int maxDistance = Mathf.Max(ReadInt(chargeEffect.@params, "base_distance", 3), 0);
        int skillLevel = _get_skill_level(
            unitState,
            ReadStringName(chargeEffect.@params, "skill_id", "charge")
        );
        foreach (ChargeDistanceBreakpoint breakpoint in ReadDistanceBreakpoints(chargeEffect))
        {
            if (skillLevel >= breakpoint.Level)
                maxDistance = Mathf.Max(maxDistance, breakpoint.Distance);
        }
        return maxDistance;
    }

    private static List<ChargeDistanceBreakpoint> ReadDistanceBreakpoints(
        CombatEffectDef chargeEffect
    )
    {
        var result = new List<ChargeDistanceBreakpoint>();
        Godot.Collections.Dictionary distanceByLevel = ReadDictionary(
            chargeEffect?.@params,
            "distance_by_level"
        );
        foreach (var rawKey in distanceByLevel.Keys)
        {
            if (!TryReadLevelBreakpoint(rawKey, out int levelBreakpoint))
                continue;
            int distance = ReadInt(distanceByLevel, rawKey, -1);
            if (distance < 0)
                continue;
            result.Add(new ChargeDistanceBreakpoint(levelBreakpoint, distance));
        }
        result.Sort((left, right) => left.Level.CompareTo(right.Level));
        return result;
    }

    private static bool TryReadLevelBreakpoint(Variant rawKey, out int levelBreakpoint)
    {
        levelBreakpoint = 0;
        if (rawKey.VariantType == Variant.Type.Int)
        {
            levelBreakpoint = rawKey.AsInt32();
            return true;
        }
        if (rawKey.VariantType != Variant.Type.String && rawKey.VariantType != Variant.Type.StringName)
            return false;
        return int.TryParse(rawKey.AsString(), out levelBreakpoint);
    }

    private static ChargeTargetInfo _resolve_charge_target_info(
        BattleUnitState us,
        Vector2I tc
    )
    {
        if (us == null)
            return new ChargeTargetInfo(false, 0, Vector2I.Zero, new Vector2I(-1, -1));
        us.RefreshFootprint();
        int minX = us.coord.X,
            maxX = us.coord.X + us.footprint_size.X - 1,
            minY = us.coord.Y,
            maxY = us.coord.Y + us.footprint_size.Y - 1;
        if (tc.Y >= minY && tc.Y <= maxY)
        {
            if (tc.X < minX)
            {
                int ld = minX - tc.X;
                return new ChargeTargetInfo(true, ld, Vector2I.Left, us.coord + Vector2I.Left * ld);
            }
            if (tc.X > maxX)
            {
                int rd = tc.X - maxX;
                return new ChargeTargetInfo(true, rd, Vector2I.Right, us.coord + Vector2I.Right * rd);
            }
        }
        if (tc.X >= minX && tc.X <= maxX)
        {
            if (tc.Y < minY)
            {
                int ud = minY - tc.Y;
                return new ChargeTargetInfo(true, ud, Vector2I.Up, us.coord + Vector2I.Up * ud);
            }
            if (tc.Y > maxY)
            {
                int dd = tc.Y - maxY;
                return new ChargeTargetInfo(true, dd, Vector2I.Down, us.coord + Vector2I.Down * dd);
            }
        }
        return new ChargeTargetInfo(false, 0, Vector2I.Zero, new Vector2I(-1, -1));
    }

    private static BattlePreview BuildFastChargePreview(
        BattleCommand command,
        ChargeTargetInfo chargeInfo,
        Vector2I targetCoord
    )
    {
        var preview = new BattlePreview
        {
            allowed = command != null
                && chargeInfo.Valid
                && chargeInfo.PredictedAnchor != new Vector2I(-1, -1),
            resolved_anchor_coord = chargeInfo.PredictedAnchor,
            move_cost = Mathf.Max(chargeInfo.Distance, 0),
        };
        if (preview.allowed)
        {
            preview.AddTargetCoord(targetCoord);
        }
        return preview;
    }

    private BattleAiScoreInput BuildChargeScoreInput(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        CombatCastVariantDef castVariant,
        BattleUnitState focusTarget,
        Vector2I resolvedAnchor,
        int resolvedMoveDistance,
        string variantLabel
    )
    {
        int chargeBaseScore = 20 + Mathf.Max(resolvedMoveDistance - 1, 0) * 8;
        return _build_typed_skill_score_input(
            context,
            skillDef,
            command,
            preview,
            castVariant?.effect_defs,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "action_kind", "move" },
                { "action_base_score", chargeBaseScore },
                { "position_target_unit_id", focusTarget?.unit_id ?? new StringName("") },
                { "position_anchor_coord", resolvedAnchor },
                { "desired_min_distance", 1 },
                { "desired_max_distance", 1 },
                { "action_label", variantLabel ?? "" },
            }
        );
    }

    private string _resolve_short_charge_block_reason(
        BattleAiContext context,
        Vector2I ra,
        int rmd,
        int ftd
    )
    {
        if (minimum_charge_move_distance <= 1)
            return "";
        if (ftd <= minimum_charge_move_distance)
            return "target_distance_below_minimum_charge";
        if (rmd > minimum_charge_move_distance)
            return "";
        var ctxUs = context?.unit_state;
        if (ctxUs == null || ra == ctxUs.coord)
            return "";
        return "short_charge_below_minimum";
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (skill_id == "")
            e.Add($"UseChargeAction {action_id} is missing skill_id.");
        if (target_selector == "")
            e.Add($"UseChargeAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(e, "UseChargeAction", target_selector);
        if (minimum_charge_move_distance < 1)
            e.Add($"UseChargeAction {action_id} minimum_charge_move_distance must be >= 1.");
        return e;
    }

    private static Godot.Collections.Dictionary ReadDictionary(
        Godot.Collections.Dictionary data,
        string key
    )
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    private static int ReadInt(Godot.Collections.Dictionary data, object key, int fallback = 0)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary data,
        string key,
        StringName fallback = default
    )
    {
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return fallback ?? new StringName("");
    }

    private static Variant ReadValue(Godot.Collections.Dictionary data, object key)
    {
        if (data == null || key == null)
            return default;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            StringName stringNameKey => stringNameKey,
            string stringKey => stringKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return default;
        if (data.ContainsKey(variantKey))
            return data[variantKey];
        return default;
    }
}
