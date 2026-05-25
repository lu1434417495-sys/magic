using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleReportFormatter : RefCounted
{
    public static readonly StringName ENTRY_TYPE_FATE_ATTACK = "fate_attack_resolution";
    public static readonly StringName ENTRY_TYPE_SKILL_EVENT = "battle_skill_event";
    public static readonly StringName ENTRY_TYPE_METEOR_SWARM_IMPACT = "meteor_swarm_impact_summary";

    public static readonly StringName REASON_CRITICAL_SUCCESS_GATE_DIE = "critical_success_gate_die";
    public static readonly StringName REASON_CRITICAL_SUCCESS_HIGH_THREAT = "critical_success_high_threat";
    public static readonly StringName REASON_ORDINARY_HIT_GATE_DIE_PENDING = "ordinary_hit_gate_die_pending";
    public static readonly StringName REASON_CRITICAL_FAIL_FUMBLE_BAND = "critical_fail_fumble_band";
    public static readonly StringName REASON_ORDINARY_MISS_THRESHOLD = "ordinary_miss_threshold";
    public static readonly StringName REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED = "ordinary_miss_fumble_downgraded";
    public static readonly StringName REASON_DOOM_SENTENCE_APPLIED = "doom_sentence_applied";

    public static readonly StringName TAG_DOOM_SENTENCE = "doom_sentence";

    public Dictionary BuildAttackReportEntry(BattleUnitState attacker, BattleUnitState defender, Dictionary attackResult)
    {
        if (attackResult == null || attackResult.Count == 0)
            return new Dictionary();
        var reasonId = _ResolveAttackReasonId(attackResult);
        if (reasonId == "")
            return new Dictionary();
        var eventTags = _NormalizeStringNameArray(DictionaryGet(attackResult, "fate_event_tags", new Godot.Collections.Array()));
        var entry = new Dictionary
        {
            ["entry_type"] = ENTRY_TYPE_FATE_ATTACK.ToString(),
            ["reason_id"] = reasonId.ToString(),
            ["text"] = "",
            ["event_tags"] = ProgressionDataUtils.string_name_array_to_string_array(eventTags),
            ["attacker_id"] = attacker != null ? attacker.unit_id.ToString() : "",
            ["attacker_member_id"] = attacker != null ? attacker.source_member_id.ToString() : "",
            ["attacker_name"] = attacker != null ? attacker.display_name : "",
            ["defender_id"] = defender != null ? defender.unit_id.ToString() : "",
            ["defender_member_id"] = defender != null ? defender.source_member_id.ToString() : "",
            ["defender_name"] = defender != null ? defender.display_name : "",
            ["defender_is_elite_or_boss"] = _IsEliteOrBoss(defender),
            ["attack_resolution"] = ProgressionDataUtils.to_string_name(DictionaryGet(attackResult, "attack_resolution", "")).ToString(),
            ["critical_source"] = ProgressionDataUtils.to_string_name(DictionaryGet(attackResult, "critical_source", "")).ToString(),
            ["is_disadvantage"] = DictionaryGet(attackResult, "is_disadvantage", false).AsBool(),
            ["crit_gate_die"] = (int)DictionaryGet(attackResult, "crit_gate_die", 0),
            ["crit_gate_roll"] = (int)DictionaryGet(attackResult, "crit_gate_roll", 0),
            ["hit_roll"] = (int)DictionaryGet(attackResult, "hit_roll", 0),
            ["required_roll"] = (int)DictionaryGet(attackResult, "required_roll", 0),
            ["display_required_roll"] = (int)DictionaryGet(attackResult, "display_required_roll", 0),
            ["luck_snapshot"] = new Dictionary
            {
                ["hidden_luck_at_birth"] = (int)DictionaryGet(attackResult, "hidden_luck_at_birth", 0),
                ["faith_luck_bonus"] = (int)DictionaryGet(attackResult, "faith_luck_bonus", 0),
                ["effective_luck"] = (int)DictionaryGet(attackResult, "effective_luck", 0),
                ["fumble_low_end"] = (int)DictionaryGet(attackResult, "fumble_low_end", 0),
                ["crit_threshold"] = (int)DictionaryGet(attackResult, "crit_threshold", 0),
            },
        };
        entry["text"] = _BuildAttackReportText(entry);
        return entry;
    }

    public Dictionary BuildSkillEventEntry(BattleUnitState attacker, BattleUnitState defender, StringName skillId, StringName reasonId, Godot.Collections.Array<StringName> eventTags)
    {
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        if (normalizedReasonId == "")
            return new Dictionary();
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        var normalizedTags = _NormalizeStringNameArray(eventTags);
        var entry = new Dictionary
        {
            ["entry_type"] = ENTRY_TYPE_SKILL_EVENT.ToString(),
            ["reason_id"] = normalizedReasonId.ToString(),
            ["text"] = "",
            ["event_tags"] = ProgressionDataUtils.string_name_array_to_string_array(normalizedTags),
            ["skill_id"] = normalizedSkillId.ToString(),
            ["attacker_id"] = attacker != null ? attacker.unit_id.ToString() : "",
            ["attacker_member_id"] = attacker != null ? attacker.source_member_id.ToString() : "",
            ["attacker_name"] = attacker != null ? attacker.display_name : "",
            ["defender_id"] = defender != null ? defender.unit_id.ToString() : "",
            ["defender_member_id"] = defender != null ? defender.source_member_id.ToString() : "",
            ["defender_name"] = defender != null ? defender.display_name : "",
            ["defender_is_elite_or_boss"] = _IsEliteOrBoss(defender),
        };
        entry["text"] = _BuildSkillEventText(entry);
        return entry;
    }

    public Godot.Collections.Array<string> FormatMeteorSwarmSummary(Dictionary entry)
    {
        var lines = new Godot.Collections.Array<string>();
        if (ProgressionDataUtils.to_string_name(DictionaryGet(entry, "entry_type", "")) != ENTRY_TYPE_METEOR_SWARM_IMPACT)
            return lines;
        var terrainSummary = DictionaryGet(entry, "terrain_summary", new Dictionary()).AsGodotDictionary();
        lines.Add($"陨星雨覆盖 {DictionaryGet(terrainSummary, "affected_coord_count", 0)} 格，波及 {DictionaryGet(entry, "target_count", 0)} 个单位，造成 {DictionaryGet(entry, "total_damage", 0)} 点总伤害；留下陨坑 {DictionaryGet(terrainSummary, "crater_count", 0)} 格、碎石 {DictionaryGet(terrainSummary, "rubble_count", 0)} 格、尘土 {DictionaryGet(terrainSummary, "dust_count", 0)} 格。");
        return lines;
    }

    public Dictionary SummarizeDamageResult(Dictionary result)
    {
        var absorbLabels = new Godot.Collections.Array<string>();
        var halfSourceLabels = new Godot.Collections.Array<string>();
        var doubleSourceLabels = new Godot.Collections.Array<string>();
        var immuneSourceLabels = new Godot.Collections.Array<string>();
        var fixedMitigationSourceLabels = new Godot.Collections.Array<string>();
        var summary = new Dictionary
        {
            ["damage"] = (int)DictionaryGet(result, "damage", 0),
            ["healing"] = (int)DictionaryGet(result, "healing", 0),
            ["shield_absorbed"] = (int)DictionaryGet(result, "shield_absorbed", 0),
            ["shield_broken"] = DictionaryGet(result, "shield_broken", false).AsBool(),
            ["has_damage_event"] = false,
            ["any_immune"] = false,
            ["any_half"] = false,
            ["any_double"] = false,
            ["fixed_mitigation_total"] = 0,
            ["absorb_labels"] = absorbLabels,
            ["half_source_labels"] = halfSourceLabels,
            ["double_source_labels"] = doubleSourceLabels,
            ["immune_source_labels"] = immuneSourceLabels,
            ["fixed_mitigation_source_labels"] = fixedMitigationSourceLabels,
            ["absorb_reason_text"] = "",
            ["fixed_mitigation_source_text"] = "",
        };
        var damageEvents = DictionaryGet(result, "damage_events", new Godot.Collections.Array()).AsGodotArray();
        foreach (Variant eventVariant in damageEvents)
        {
            if (eventVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var evt = eventVariant.AsGodotDictionary();
            summary["has_damage_event"] = true;
            summary["fixed_mitigation_total"] = (int)DictionaryGet(summary, "fixed_mitigation_total", 0) + (int)DictionaryGet(evt, "fixed_mitigation_total", 0);
            var mitigationTier = ProgressionDataUtils.to_string_name(DictionaryGet(evt, "mitigation_tier", ""));
            switch (mitigationTier)
            {
                case "immune":
                    summary["any_immune"] = true;
                    break;
                case "half":
                    summary["any_half"] = true;
                    break;
                case "double":
                    summary["any_double"] = true;
                    break;
            }
            _AppendDamageMitigationSourceLabels(DictionaryGet(evt, "mitigation_sources", new Godot.Collections.Array()).AsGodotArray(), halfSourceLabels, doubleSourceLabels, immuneSourceLabels);
            _AppendDamageFixedSourceLabels(DictionaryGet(evt, "fixed_mitigation_sources", new Godot.Collections.Array()).AsGodotArray(), fixedMitigationSourceLabels);
            if ((int)DictionaryGet(evt, "buff_reduction", 0) > 0 || (int)DictionaryGet(evt, "passive_reduction", 0) > 0 || (int)DictionaryGet(evt, "content_dr", 0) > 0)
                _AppendUniqueDamageAbsorbLabel(absorbLabels, "减伤");
            if ((int)DictionaryGet(evt, "stance_reduction", 0) > 0 || (int)DictionaryGet(evt, "guard_block", 0) > 0)
                _AppendUniqueDamageAbsorbLabel(absorbLabels, "格挡");
        }
        summary["absorb_reason_text"] = BuildDamageAbsorbReasonText(summary);
        summary["fixed_mitigation_source_text"] = _FormatDamageSourceLabels((Godot.Collections.Array)fixedMitigationSourceLabels);
        return summary;
    }

    public string BuildDamageAbsorbReasonText(Dictionary summary)
    {
        if (DictionaryGet(summary, "any_immune", false).AsBool())
            return _FormatDamageSourceLabels(DictionaryGet(summary, "immune_source_labels", new Godot.Collections.Array()).AsGodotArray(), "免疫");
        var labels = new Godot.Collections.Array<string>();
        if (DictionaryGet(summary, "any_half", false).AsBool())
        {
            var halfSourceText = _FormatDamageSourceLabels(DictionaryGet(summary, "half_source_labels", new Godot.Collections.Array()).AsGodotArray());
            labels.Add(string.IsNullOrEmpty(halfSourceText) ? "减半" : halfSourceText);
        }
        var absorbLabels = DictionaryGet(summary, "absorb_labels", new Godot.Collections.Array()).AsGodotArray();
        if (_FormatDamageSourceLabels(DictionaryGet(summary, "fixed_mitigation_source_labels", new Godot.Collections.Array()).AsGodotArray()).Length == 0)
        {
            foreach (Variant labelVariant in absorbLabels)
            {
                var label = labelVariant.AsString();
                if (string.IsNullOrEmpty(label))
                    continue;
                labels.Add(label);
            }
        }
        var fixedSourceText = _FormatDamageSourceLabels(DictionaryGet(summary, "fixed_mitigation_source_labels", new Godot.Collections.Array()).AsGodotArray());
        if (!string.IsNullOrEmpty(fixedSourceText))
            labels.Add(fixedSourceText);
        if (labels.Count == 0)
            return "防护";
        return string.Join("、", labels);
    }

    public void AppendDamageResultLogLines(BattleEventBatch batch, string subjectLabel, string targetDisplayName, Dictionary result)
    {
        if (batch == null)
            return;
        var executeOutcome = ProgressionDataUtils.to_string_name(DictionaryGet(result, "execute_outcome", ""));
        if (executeOutcome == "resisted")
        {
            batch.log_lines.Add("目标抵抗死亡律令。");
            return;
        }
        var summary = SummarizeDamageResult(result);
        if (!DictionaryGet(summary, "has_damage_event", false).AsBool())
            return;
        var damage = (int)DictionaryGet(summary, "damage", 0);
        var shieldAbsorbed = (int)DictionaryGet(summary, "shield_absorbed", 0);
        var fixedMitigationTotal = (int)DictionaryGet(summary, "fixed_mitigation_total", 0);
        if (damage > 0)
        {
            var damageLine = $"{subjectLabel} 对 {targetDisplayName} 造成 {damage} 点伤害{_FormatDamageTierLogSuffix(summary)}";
            batch.log_lines.Add($"{damageLine}。");
            if (executeOutcome == "failed_save_fatal")
            {
                batch.log_lines.Add("死亡律令生效 / 无视免死效果。");
                if (_DamageResultHasBypassShieldEvent(result))
                    batch.log_lines.Add("死亡律令穿透护盾。");
            }
            if (fixedMitigationTotal > 0)
            {
                var fixedSourceText = DictionaryGet(summary, "fixed_mitigation_source_text", "").AsString();
                if (string.IsNullOrEmpty(fixedSourceText))
                    fixedSourceText = DictionaryGet(summary, "absorb_reason_text", "防护").AsString();
                batch.log_lines.Add($"{targetDisplayName} 的 {fixedSourceText} 吸收了 {fixedMitigationTotal} 点伤害。");
            }
            if (shieldAbsorbed > 0)
                batch.log_lines.Add($"{targetDisplayName} 的护盾吸收了 {shieldAbsorbed} 点伤害。");
        }
        else
        {
            if (DictionaryGet(summary, "any_immune", false).AsBool())
            {
                var immuneSourceText = _FormatDamageSourceLabels(DictionaryGet(summary, "immune_source_labels", new Godot.Collections.Array()).AsGodotArray());
                if (string.IsNullOrEmpty(immuneSourceText))
                    batch.log_lines.Add($"{subjectLabel} 命中 {targetDisplayName}，但其免疫该伤害。");
                else
                    batch.log_lines.Add($"{subjectLabel} 命中 {targetDisplayName}，但其因 {immuneSourceText} 免疫该伤害。");
            }
            else if (shieldAbsorbed > 0)
                batch.log_lines.Add($"{subjectLabel} 命中 {targetDisplayName}，但被护盾吸收了 {shieldAbsorbed} 点伤害。");
            else
                batch.log_lines.Add($"{subjectLabel} 命中 {targetDisplayName}，但被 {DictionaryGet(summary, "absorb_reason_text", "防护")} 完全吸收。");
        }
        if (DictionaryGet(summary, "shield_broken", false).AsBool())
            batch.log_lines.Add($"{targetDisplayName} 的护盾被击碎。");
    }

    private bool _DamageResultHasBypassShieldEvent(Dictionary result)
    {
        var damageEvents = DictionaryGet(result, "damage_events", new Godot.Collections.Array()).AsGodotArray();
        foreach (Variant eventVariant in damageEvents)
        {
            if (eventVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var evt = eventVariant.AsGodotDictionary();
            if (DictionaryGet(evt, "bypass_shield", false).AsBool())
                return true;
        }
        return false;
    }

    private StringName _ResolveAttackReasonId(Dictionary attackResult)
    {
        var attackResolution = ProgressionDataUtils.to_string_name(DictionaryGet(attackResult, "attack_resolution", ""));
        var criticalSource = ProgressionDataUtils.to_string_name(DictionaryGet(attackResult, "critical_source", ""));
        var critGateDie = (int)DictionaryGet(attackResult, "crit_gate_die", 0);
        var hitRoll = (int)DictionaryGet(attackResult, "hit_roll", 0);
        if (attackResolution == "critical_hit")
        {
            if (criticalSource == "high_threat")
                return REASON_CRITICAL_SUCCESS_HIGH_THREAT;
            if (criticalSource == "gate_die")
                return REASON_CRITICAL_SUCCESS_GATE_DIE;
        }
        if (attackResolution == "hit" && hitRoll >= 20 && critGateDie > 20)
            return REASON_ORDINARY_HIT_GATE_DIE_PENDING;
        if (attackResolution == "miss" && DictionaryGet(attackResult, "reverse_fate_downgraded", false).AsBool())
            return REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED;
        if (attackResolution == "critical_fail")
            return REASON_CRITICAL_FAIL_FUMBLE_BAND;
        if (attackResolution == "miss")
            return REASON_ORDINARY_MISS_THRESHOLD;
        return "";
    }

    private string _BuildAttackReportText(Dictionary entry)
    {
        var reasonId = ProgressionDataUtils.to_string_name(DictionaryGet(entry, "reason_id", ""));
        var critGateDie = (int)DictionaryGet(entry, "crit_gate_die", 0);
        var critGateRoll = (int)DictionaryGet(entry, "crit_gate_roll", 0);
        var hitRoll = (int)DictionaryGet(entry, "hit_roll", 0);
        var luckSnapshotVariant = DictionaryGet(entry, "luck_snapshot", new Dictionary());
        var luckSnapshot = luckSnapshotVariant.VariantType == Variant.Type.Dictionary ? luckSnapshotVariant.AsGodotDictionary() : new Dictionary();
        var fumbleLowEnd = (int)DictionaryGet(luckSnapshot, "fumble_low_end", 0);
        var critThreshold = (int)DictionaryGet(luckSnapshot, "crit_threshold", 0);
        var text = "";
        switch (reasonId)
        {
            case "critical_success_gate_die":
                text = $"命运判定：先掷大成功门骰 d{critGateDie}={critGateRoll}/{critGateDie}，这次大成功来自门骰。";
                break;
            case "critical_success_high_threat":
                text = $"命运判定：命中骰 d20={hitRoll} 落入高位大成功区 {critThreshold}-20，这次大成功来自高位威胁。";
                break;
            case "ordinary_hit_gate_die_pending":
                text = $"命运判定：d20={hitRoll} 仍只是普通命中；当前大成功门骰为 d{critGateDie}，必须先中过门骰。";
                break;
            case "critical_fail_fumble_band":
                text = $"命运判定：d20={hitRoll} 落入大失败区间 1-{fumbleLowEnd}，直接判定为大失败。";
                break;
            case "ordinary_miss_fumble_downgraded":
                text = $"命运判定：d20={hitRoll} 落入大失败区间 1-{fumbleLowEnd}，但被逆命护符降级为普通 miss。";
                break;
            case "ordinary_miss_threshold":
                var displayRequiredRoll = (int)DictionaryGet(entry, "display_required_roll", 0);
                if (displayRequiredRoll <= 0)
                    displayRequiredRoll = (int)DictionaryGet(entry, "required_roll", 0);
                var fumbleText = fumbleLowEnd > 1 ? $"1-{fumbleLowEnd}" : "1";
                text = $"命运判定：命中骰 d20={hitRoll} 未达到命中线 {displayRequiredRoll}，也不在大失败区 {fumbleText}，因此只是普通 miss。";
                break;
        }
        return _AppendEventTagSuffix(text, DictionaryGet(entry, "event_tags", new Godot.Collections.Array()).AsGodotArray());
    }

    private string _BuildSkillEventText(Dictionary entry)
    {
        var reasonId = ProgressionDataUtils.to_string_name(DictionaryGet(entry, "reason_id", ""));
        var attackerName = DictionaryGet(entry, "attacker_name", "").AsString().StripEdges();
        var defenderName = DictionaryGet(entry, "defender_name", "").AsString().StripEdges();
        var actorLabel = string.IsNullOrEmpty(attackerName) ? "该单位" : attackerName;
        var targetLabel = string.IsNullOrEmpty(defenderName) ? "目标" : defenderName;
        var text = "";
        switch (reasonId)
        {
            case "doom_sentence_applied":
                text = $"{actorLabel} 对 {targetLabel} 落下厄命宣判。";
                break;
        }
        return _AppendEventTagSuffix(text, DictionaryGet(entry, "event_tags", new Godot.Collections.Array()).AsGodotArray());
    }

    private string _AppendEventTagSuffix(string text, Godot.Collections.Array eventTags)
    {
        var tags = _NormalizeStringNameArray(eventTags);
        if (string.IsNullOrEmpty(text) || tags.Count == 0)
            return text;
        return $"{text} 事件标签：{string.Join(", ", ProgressionDataUtils.string_name_array_to_string_array(tags))}。";
    }

    private void _AppendDamageMitigationSourceLabels(Godot.Collections.Array sources, Godot.Collections.Array<string> halfSourceLabels, Godot.Collections.Array<string> doubleSourceLabels, Godot.Collections.Array<string> immuneSourceLabels)
    {
        foreach (Variant sourceVariant in sources)
        {
            if (sourceVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var source = sourceVariant.AsGodotDictionary();
            var sourceLabel = _FormatDamageSourceLabel(source);
            if (string.IsNullOrEmpty(sourceLabel))
                continue;
            var tier = ProgressionDataUtils.to_string_name(DictionaryGet(source, "tier", ""));
            switch (tier)
            {
                case "half":
                    _AppendUniqueDamageAbsorbLabel(halfSourceLabels, sourceLabel);
                    break;
                case "double":
                    _AppendUniqueDamageAbsorbLabel(doubleSourceLabels, sourceLabel);
                    break;
                case "immune":
                    _AppendUniqueDamageAbsorbLabel(immuneSourceLabels, sourceLabel);
                    break;
            }
        }
    }

    private void _AppendDamageFixedSourceLabels(Godot.Collections.Array sources, Godot.Collections.Array<string> fixedSourceLabels)
    {
        foreach (Variant sourceVariant in sources)
        {
            if (sourceVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var sourceLabel = _FormatDamageSourceLabel(sourceVariant.AsGodotDictionary());
            if (string.IsNullOrEmpty(sourceLabel))
                continue;
            _AppendUniqueDamageAbsorbLabel(fixedSourceLabels, sourceLabel);
        }
    }

    private string _FormatDamageSourceLabel(Dictionary source)
    {
        var statusId = DictionaryGet(source, "status_id", "").AsString();
        if (!string.IsNullOrEmpty(statusId))
            return statusId;
        return DictionaryGet(source, "type", "").AsString();
    }

    private string _FormatDamageSourceLabels(Godot.Collections.Array labelsVariant, string fallback = "")
    {
        if (labelsVariant == null)
            return fallback;
        var labels = new Godot.Collections.Array<string>();
        foreach (Variant labelVariant in labelsVariant)
        {
            var label = labelVariant.AsString();
            if (string.IsNullOrEmpty(label) || labels.Contains(label))
                continue;
            labels.Add(label);
        }
        if (labels.Count == 0)
            return fallback;
        return string.Join("、", labels);
    }

    private string _FormatDamageTierLogSuffix(Dictionary summary)
    {
        if (DictionaryGet(summary, "any_double", false).AsBool())
        {
            var doubleSourceText = _FormatDamageSourceLabels(DictionaryGet(summary, "double_source_labels", new Godot.Collections.Array()).AsGodotArray());
            if (!string.IsNullOrEmpty(doubleSourceText))
                return $"（因 {doubleSourceText} 触发易伤）";
            return "（触发易伤）";
        }
        if (DictionaryGet(summary, "any_half", false).AsBool())
        {
            var halfSourceText = _FormatDamageSourceLabels(DictionaryGet(summary, "half_source_labels", new Godot.Collections.Array()).AsGodotArray());
            if (!string.IsNullOrEmpty(halfSourceText))
                return $"（因 {halfSourceText} 减半后结算）";
            return "（减半后结算）";
        }
        return "";
    }

    private void _AppendUniqueDamageAbsorbLabel(Godot.Collections.Array<string> absorbLabels, string label)
    {
        if (string.IsNullOrEmpty(label) || absorbLabels.Contains(label))
            return;
        absorbLabels.Add(label);
    }

    private Godot.Collections.Array<StringName> _NormalizeStringNameArray(Variant values)
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values.VariantType != Variant.Type.Array)
            return result;
        foreach (Variant value in values.AsGodotArray())
        {
            var normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }

    private bool _IsEliteOrBoss(BattleUnitState unitState)
    {
        return BattleExecutionRules.is_elite_or_boss_target(unitState);
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }
}
