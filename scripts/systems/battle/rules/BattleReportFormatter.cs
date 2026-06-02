using Godot;
using Godot.Collections;

public sealed class BattleReportFormatter
{
    public static readonly StringName ENTRY_TYPE_FATE_ATTACK = "fate_attack_resolution";
    public static readonly StringName ENTRY_TYPE_SKILL_EVENT = "battle_skill_event";
    public static readonly StringName ENTRY_TYPE_METEOR_SWARM_IMPACT =
        "meteor_swarm_impact_summary";

    public static readonly StringName REASON_CRITICAL_SUCCESS_GATE_DIE =
        "critical_success_gate_die";
    public static readonly StringName REASON_CRITICAL_SUCCESS_HIGH_THREAT =
        "critical_success_high_threat";
    public static readonly StringName REASON_ORDINARY_HIT_GATE_DIE_PENDING =
        "ordinary_hit_gate_die_pending";
    public static readonly StringName REASON_CRITICAL_FAIL_FUMBLE_BAND =
        "critical_fail_fumble_band";
    public static readonly StringName REASON_ORDINARY_MISS_THRESHOLD = "ordinary_miss_threshold";
    public static readonly StringName REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED =
        "ordinary_miss_fumble_downgraded";
    public static readonly StringName REASON_DOOM_SENTENCE_APPLIED = "doom_sentence_applied";

    public static readonly StringName TAG_DOOM_SENTENCE = "doom_sentence";

    private sealed class DamageResultSummary
    {
        public int Damage;
        public int Healing;
        public int ShieldAbsorbed;
        public bool ShieldBroken;
        public bool HasDamageEvent;
        public bool AnyImmune;
        public bool AnyHalf;
        public bool AnyDouble;
        public int FixedMitigationTotal;
        public System.Collections.Generic.List<string> AbsorbLabels = new();
        public System.Collections.Generic.List<string> HalfSourceLabels = new();
        public System.Collections.Generic.List<string> DoubleSourceLabels = new();
        public System.Collections.Generic.List<string> ImmuneSourceLabels = new();
        public System.Collections.Generic.List<string> FixedMitigationSourceLabels = new();
        public string AbsorbReasonText = "";
        public string FixedMitigationSourceText = "";

        internal Dictionary ToDictionary()
        {
            return new Dictionary
            {
                ["damage"] = Damage,
                ["healing"] = Healing,
                ["shield_absorbed"] = ShieldAbsorbed,
                ["shield_broken"] = ShieldBroken,
                ["has_damage_event"] = HasDamageEvent,
                ["any_immune"] = AnyImmune,
                ["any_half"] = AnyHalf,
                ["any_double"] = AnyDouble,
                ["fixed_mitigation_total"] = FixedMitigationTotal,
                ["absorb_labels"] = ToStringArray(AbsorbLabels),
                ["half_source_labels"] = ToStringArray(HalfSourceLabels),
                ["double_source_labels"] = ToStringArray(DoubleSourceLabels),
                ["immune_source_labels"] = ToStringArray(ImmuneSourceLabels),
                ["fixed_mitigation_source_labels"] = ToStringArray(FixedMitigationSourceLabels),
                ["absorb_reason_text"] = AbsorbReasonText,
                ["fixed_mitigation_source_text"] = FixedMitigationSourceText,
            };
        }

        internal static DamageResultSummary FromDictionary(Dictionary summary)
        {
            summary ??= new Dictionary();
            return new DamageResultSummary
            {
                Damage = (int)summary.GetValueOrDefault("damage", 0),
                Healing = (int)summary.GetValueOrDefault("healing", 0),
                ShieldAbsorbed = (int)summary.GetValueOrDefault("shield_absorbed", 0),
                ShieldBroken = DictBool(summary, "shield_broken", false),
                HasDamageEvent = DictBool(summary, "has_damage_event", false),
                AnyImmune = DictBool(summary, "any_immune", false),
                AnyHalf = DictBool(summary, "any_half", false),
                AnyDouble = DictBool(summary, "any_double", false),
                FixedMitigationTotal = (int)summary.GetValueOrDefault(
                    "fixed_mitigation_total",
                    0
                ),
                AbsorbLabels = ReadStringArray(summary, "absorb_labels"),
                HalfSourceLabels = ReadStringArray(summary, "half_source_labels"),
                DoubleSourceLabels = ReadStringArray(summary, "double_source_labels"),
                ImmuneSourceLabels = ReadStringArray(summary, "immune_source_labels"),
                FixedMitigationSourceLabels = ReadStringArray(
                    summary,
                    "fixed_mitigation_source_labels"
                ),
                AbsorbReasonText = summary.GetValueOrDefault("absorb_reason_text", "").AsString(),
                FixedMitigationSourceText = summary
                    .GetValueOrDefault("fixed_mitigation_source_text", "")
                    .AsString(),
            };
        }

        private static System.Collections.Generic.List<string> ReadStringArray(
            Dictionary source,
            string key
        )
        {
            var result = new System.Collections.Generic.List<string>();
            if (source == null || !source.ContainsKey(key))
                return result;
            foreach (var value in source[key].AsGodotArray())
            {
                string normalized = value.AsString();
                if (!string.IsNullOrEmpty(normalized))
                    result.Add(normalized);
            }
            return result;
        }

        private static Godot.Collections.Array<string> ToStringArray(
            System.Collections.Generic.IEnumerable<string> values
        )
        {
            var result = new Godot.Collections.Array<string>();
            if (values == null)
                return result;
            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value))
                    result.Add(value);
            }
            return result;
        }
    }

    internal Dictionary BuildAttackReportEntry(
        BattleUnitState attacker,
        BattleUnitState defender,
        Dictionary attackResult
    )
    {
        if (attackResult == null || attackResult.Count == 0)
            return new Dictionary();
        var reasonId = _ResolveAttackReasonId(attackResult);
        if (reasonId == "")
            return new Dictionary();
        var eventTags = _NormalizeStringNameArray(
            attackResult
                .GetValueOrDefault("fate_event_tags", new Godot.Collections.Array())
                .AsGodotArray()
        );
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
            ["attack_resolution"] = ProgressionDataUtils
                .to_string_name(attackResult.GetValueOrDefault("attack_resolution", ""))
                .ToString(),
            ["critical_source"] = ProgressionDataUtils
                .to_string_name(attackResult.GetValueOrDefault("critical_source", ""))
                .ToString(),
            ["is_disadvantage"] = DictBool(attackResult, "is_disadvantage", false),
            ["crit_gate_die"] = (int)attackResult.GetValueOrDefault("crit_gate_die", 0),
            ["crit_gate_roll"] = (int)attackResult.GetValueOrDefault("crit_gate_roll", 0),
            ["hit_roll"] = (int)attackResult.GetValueOrDefault("hit_roll", 0),
            ["required_roll"] = (int)attackResult.GetValueOrDefault("required_roll", 0),
            ["display_required_roll"] = (int)attackResult.GetValueOrDefault(
                "display_required_roll",
                0
            ),
            ["luck_snapshot"] = new Dictionary
            {
                ["hidden_luck_at_birth"] = (int)attackResult.GetValueOrDefault(
                    "hidden_luck_at_birth",
                    0
                ),
                ["faith_luck_bonus"] = (int)attackResult.GetValueOrDefault("faith_luck_bonus", 0),
                ["effective_luck"] = (int)attackResult.GetValueOrDefault("effective_luck", 0),
                ["fumble_low_end"] = (int)attackResult.GetValueOrDefault("fumble_low_end", 0),
                ["crit_threshold"] = (int)attackResult.GetValueOrDefault("crit_threshold", 0),
            },
        };
        entry["text"] = _BuildAttackReportText(entry);
        return entry;
    }

    internal Dictionary BuildAttackReportEntry(
        BattleUnitState attacker,
        BattleUnitState defender,
        AttackResolutionMetadata attackMetadata,
        StringName criticalSource,
        Godot.Collections.Array<StringName> eventTags
    )
    {
        attackMetadata ??= new AttackResolutionMetadata();
        var reasonId = _ResolveAttackReasonId(attackMetadata, criticalSource);
        if (reasonId == "")
            return new Dictionary();
        var normalizedTags = _NormalizeStringNameArray(eventTags);
        var entry = new Dictionary
        {
            ["entry_type"] = ENTRY_TYPE_FATE_ATTACK.ToString(),
            ["reason_id"] = reasonId.ToString(),
            ["text"] = "",
            ["event_tags"] = ProgressionDataUtils.string_name_array_to_string_array(normalizedTags),
            ["attacker_id"] = attacker != null ? attacker.unit_id.ToString() : "",
            ["attacker_member_id"] = attacker != null ? attacker.source_member_id.ToString() : "",
            ["attacker_name"] = attacker != null ? attacker.display_name : "",
            ["defender_id"] = defender != null ? defender.unit_id.ToString() : "",
            ["defender_member_id"] = defender != null ? defender.source_member_id.ToString() : "",
            ["defender_name"] = defender != null ? defender.display_name : "",
            ["defender_is_elite_or_boss"] = _IsEliteOrBoss(defender),
            ["attack_resolution"] = attackMetadata.AttackResolution.ToString(),
            ["critical_source"] = criticalSource.ToString(),
            ["is_disadvantage"] = attackMetadata.IsDisadvantage,
            ["crit_gate_die"] = attackMetadata.CritGateDie,
            ["crit_gate_roll"] = attackMetadata.CritGateRoll,
            ["hit_roll"] = attackMetadata.HitRoll,
            ["required_roll"] = attackMetadata.RequiredRoll,
            ["display_required_roll"] = attackMetadata.DisplayRequiredRoll,
            ["luck_snapshot"] = new Dictionary
            {
                ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
                ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
                ["effective_luck"] = attackMetadata.EffectiveLuck,
                ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
                ["crit_threshold"] = attackMetadata.CritThreshold,
            },
        };
        entry["text"] = _BuildAttackReportText(entry);
        return entry;
    }

    internal Dictionary BuildSkillEventEntry(
        BattleUnitState attacker,
        BattleUnitState defender,
        StringName skillId,
        StringName reasonId,
        Godot.Collections.Array<StringName> eventTags
    )
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

    internal Godot.Collections.Array<string> FormatMeteorSwarmSummary(Dictionary entry)
    {
        var lines = new Godot.Collections.Array<string>();
        if (
            ProgressionDataUtils.to_string_name(entry.GetValueOrDefault("entry_type", ""))
            != ENTRY_TYPE_METEOR_SWARM_IMPACT
        )
            return lines;
        var terrainSummary = entry.GetValueOrDefault("terrain_summary", new Dictionary())
            .AsGodotDictionary();
        lines.Add(
            $"陨星雨覆盖 {terrainSummary.GetValueOrDefault("affected_coord_count", 0)} 格，波及 {entry.GetValueOrDefault("target_count", 0)} 个单位，造成 {entry.GetValueOrDefault("total_damage", 0)} 点总伤害；留下陨坑 {terrainSummary.GetValueOrDefault("crater_count", 0)} 格、碎石 {terrainSummary.GetValueOrDefault("rubble_count", 0)} 格、尘土 {terrainSummary.GetValueOrDefault("dust_count", 0)} 格。"
        );
        return lines;
    }

    internal Dictionary SummarizeDamageResult(Dictionary result)
    {
        return BuildDamageResultSummary(result).ToDictionary();
    }

    private DamageResultSummary BuildDamageResultSummary(Dictionary result)
    {
        result ??= new Dictionary();
        var summary = new DamageResultSummary
        {
            Damage = (int)result.GetValueOrDefault("damage", 0),
            Healing = (int)result.GetValueOrDefault("healing", 0),
            ShieldAbsorbed = (int)result.GetValueOrDefault("shield_absorbed", 0),
            ShieldBroken = DictBool(result, "shield_broken", false),
        };
        var damageEvents = result.GetValueOrDefault("damage_events", new Godot.Collections.Array())
            .AsGodotArray();
        foreach (var eventValue in damageEvents)
        {
            var evt = eventValue.AsGodotDictionary();
            summary.HasDamageEvent = true;
            summary.FixedMitigationTotal += (int)evt.GetValueOrDefault(
                "fixed_mitigation_total",
                0
            );
            var mitigationTier = ProgressionDataUtils.to_string_name(
                evt.GetValueOrDefault("mitigation_tier", "")
            );
            switch (mitigationTier)
            {
                case "immune":
                    summary.AnyImmune = true;
                    break;
                case "half":
                    summary.AnyHalf = true;
                    break;
                case "double":
                    summary.AnyDouble = true;
                    break;
            }
            _AppendDamageMitigationSourceLabels(
                evt.GetValueOrDefault("mitigation_sources", new Godot.Collections.Array())
                    .AsGodotArray(),
                summary.HalfSourceLabels,
                summary.DoubleSourceLabels,
                summary.ImmuneSourceLabels
            );
            _AppendDamageFixedSourceLabels(
                evt.GetValueOrDefault("fixed_mitigation_sources", new Godot.Collections.Array())
                    .AsGodotArray(),
                summary.FixedMitigationSourceLabels
            );
            if (
                (int)evt.GetValueOrDefault("buff_reduction", 0) > 0
                || (int)evt.GetValueOrDefault("passive_reduction", 0) > 0
                || (int)evt.GetValueOrDefault("content_dr", 0) > 0
            )
                _AppendUniqueDamageAbsorbLabel(summary.AbsorbLabels, "减伤");
            if (
                (int)evt.GetValueOrDefault("stance_reduction", 0) > 0
                || (int)evt.GetValueOrDefault("guard_block", 0) > 0
            )
                _AppendUniqueDamageAbsorbLabel(summary.AbsorbLabels, "格挡");
        }
        summary.AbsorbReasonText = BuildDamageAbsorbReasonText(summary);
        summary.FixedMitigationSourceText = _FormatDamageSourceLabels(
            summary.FixedMitigationSourceLabels
        );
        return summary;
    }

    internal string BuildDamageAbsorbReasonText(Dictionary summary)
    {
        return BuildDamageAbsorbReasonText(DamageResultSummary.FromDictionary(summary));
    }

    private string BuildDamageAbsorbReasonText(DamageResultSummary summary)
    {
        summary ??= new DamageResultSummary();
        if (summary.AnyImmune)
            return _FormatDamageSourceLabels(summary.ImmuneSourceLabels, "免疫");
        var labels = new System.Collections.Generic.List<string>();
        if (summary.AnyHalf)
        {
            var halfSourceText = _FormatDamageSourceLabels(summary.HalfSourceLabels);
            labels.Add(string.IsNullOrEmpty(halfSourceText) ? "减半" : halfSourceText);
        }
        if (_FormatDamageSourceLabels(summary.FixedMitigationSourceLabels).Length == 0)
        {
            foreach (var label in summary.AbsorbLabels)
            {
                if (string.IsNullOrEmpty(label))
                    continue;
                labels.Add(label);
            }
        }
        var fixedSourceText = _FormatDamageSourceLabels(summary.FixedMitigationSourceLabels);
        if (!string.IsNullOrEmpty(fixedSourceText))
            labels.Add(fixedSourceText);
        if (labels.Count == 0)
            return "防护";
        return string.Join("、", labels);
    }

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        Dictionary result
    )
    {
        if (batch == null)
            return;
        var executeOutcome = ProgressionDataUtils.to_string_name(
            result.GetValueOrDefault("execute_outcome", "")
        );
        if (executeOutcome == "resisted")
        {
            batch.log_lines.Add("目标抵抗死亡律令。");
            return;
        }
        DamageResultSummary summary = BuildDamageResultSummary(result);
        if (!summary.HasDamageEvent)
            return;
        var damage = summary.Damage;
        var shieldAbsorbed = summary.ShieldAbsorbed;
        var fixedMitigationTotal = summary.FixedMitigationTotal;
        if (damage > 0)
        {
            var damageLine =
                $"{subjectLabel} 对 {targetDisplayName} 造成 {damage} 点伤害{_FormatDamageTierLogSuffix(summary)}";
            batch.log_lines.Add($"{damageLine}。");
            if (executeOutcome == "failed_save_fatal")
            {
                batch.log_lines.Add("死亡律令生效 / 无视免死效果。");
                if (_DamageResultHasBypassShieldEvent(result))
                    batch.log_lines.Add("死亡律令穿透护盾。");
            }
            if (fixedMitigationTotal > 0)
            {
                var fixedSourceText = summary.FixedMitigationSourceText;
                if (string.IsNullOrEmpty(fixedSourceText))
                    fixedSourceText = string.IsNullOrEmpty(summary.AbsorbReasonText)
                        ? "防护"
                        : summary.AbsorbReasonText;
                batch.log_lines.Add(
                    $"{targetDisplayName} 的 {fixedSourceText} 吸收了 {fixedMitigationTotal} 点伤害。"
                );
            }
            if (shieldAbsorbed > 0)
                batch.log_lines.Add($"{targetDisplayName} 的护盾吸收了 {shieldAbsorbed} 点伤害。");
        }
        else
        {
            if (summary.AnyImmune)
            {
                var immuneSourceText = _FormatDamageSourceLabels(summary.ImmuneSourceLabels);
                if (string.IsNullOrEmpty(immuneSourceText))
                    batch.log_lines.Add(
                        $"{subjectLabel} 命中 {targetDisplayName}，但其免疫该伤害。"
                    );
                else
                    batch.log_lines.Add(
                        $"{subjectLabel} 命中 {targetDisplayName}，但其因 {immuneSourceText} 免疫该伤害。"
                    );
            }
            else if (shieldAbsorbed > 0)
                batch.log_lines.Add(
                    $"{subjectLabel} 命中 {targetDisplayName}，但被护盾吸收了 {shieldAbsorbed} 点伤害。"
                );
            else
                batch.log_lines.Add(
                    $"{subjectLabel} 命中 {targetDisplayName}，但被 {(string.IsNullOrEmpty(summary.AbsorbReasonText) ? "防护" : summary.AbsorbReasonText)} 完全吸收。"
                );
        }
        if (summary.ShieldBroken)
            batch.log_lines.Add($"{targetDisplayName} 的护盾被击碎。");
    }

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subjectLabel,
        string targetDisplayName,
        AttackEffectResolutionResult result
    )
    {
        if (batch == null)
            return;
        if (result.ExecuteOutcome == ExecuteOutcomeKind.Resisted)
        {
            batch.log_lines.Add("目标抵抗死亡律令。");
            return;
        }
        if (!result.HasDamageEvent)
            return;
        int damage = result.Damage;
        int shieldAbsorbed = result.ShieldAbsorbed;
        int fixedMitigationTotal = result.FixedMitigationTotal;
        if (damage > 0)
        {
            string damageLine =
                $"{subjectLabel} 对 {targetDisplayName} 造成 {damage} 点伤害{FormatDamageTierLogSuffix(result)}";
            batch.log_lines.Add($"{damageLine}。");
            if (result.ExecuteOutcome == ExecuteOutcomeKind.FailedSaveFatal)
            {
                batch.log_lines.Add("死亡律令生效 / 无视免死效果。");
                if (result.BypassShield)
                    batch.log_lines.Add("死亡律令穿透护盾。");
            }
            if (fixedMitigationTotal > 0)
            {
                string fixedSourceText = result.FixedMitigationSourceText;
                if (string.IsNullOrEmpty(fixedSourceText))
                    fixedSourceText = string.IsNullOrEmpty(result.AbsorbReasonText)
                        ? "防护"
                        : result.AbsorbReasonText;
                batch.log_lines.Add(
                    $"{targetDisplayName} 的 {fixedSourceText} 吸收了 {fixedMitigationTotal} 点伤害。"
                );
            }
            if (shieldAbsorbed > 0)
                batch.log_lines.Add($"{targetDisplayName} 的护盾吸收了 {shieldAbsorbed} 点伤害。");
        }
        else
        {
            if (result.AnyImmune)
            {
                string immuneSourceText = AttackEffectResolutionResultReader.JoinLabels(
                    result.ImmuneSourceLabels
                );
                if (string.IsNullOrEmpty(immuneSourceText))
                    batch.log_lines.Add(
                        $"{subjectLabel} 命中 {targetDisplayName}，但其免疫该伤害。"
                    );
                else
                    batch.log_lines.Add(
                        $"{subjectLabel} 命中 {targetDisplayName}，但其因 {immuneSourceText} 免疫该伤害。"
                    );
            }
            else if (shieldAbsorbed > 0)
                batch.log_lines.Add(
                    $"{subjectLabel} 命中 {targetDisplayName}，但被护盾吸收了 {shieldAbsorbed} 点伤害。"
                );
            else
                batch.log_lines.Add(
                    $"{subjectLabel} 命中 {targetDisplayName}，但被 {(string.IsNullOrEmpty(result.AbsorbReasonText) ? "防护" : result.AbsorbReasonText)} 完全吸收。"
                );
        }
        if (result.ShieldBroken)
            batch.log_lines.Add($"{targetDisplayName} 的护盾被击碎。");
    }

    private string FormatDamageTierLogSuffix(AttackEffectResolutionResult result)
    {
        if (result.AnyDouble)
        {
            string doubleSourceText = AttackEffectResolutionResultReader.JoinLabels(
                result.DoubleSourceLabels
            );
            if (!string.IsNullOrEmpty(doubleSourceText))
                return $"（因 {doubleSourceText} 触发易伤）";
            return "（触发易伤）";
        }
        if (result.AnyHalf)
        {
            string halfSourceText = AttackEffectResolutionResultReader.JoinLabels(
                result.HalfSourceLabels
            );
            if (!string.IsNullOrEmpty(halfSourceText))
                return $"（因 {halfSourceText} 减半后结算）";
            return "（减半后结算）";
        }
        return "";
    }

    private bool _DamageResultHasBypassShieldEvent(Dictionary result)
    {
        var damageEvents = result.GetValueOrDefault("damage_events", new Godot.Collections.Array())
            .AsGodotArray();
        foreach (var eventValue in damageEvents)
        {
            var evt = eventValue.AsGodotDictionary();
            if (DictBool(evt, "bypass_shield", false))
                return true;
        }
        return false;
    }

    private StringName _ResolveAttackReasonId(Dictionary attackResult)
    {
        var attackResolution = ProgressionDataUtils.to_string_name(
            attackResult.GetValueOrDefault("attack_resolution", "")
        );
        var criticalSource = ProgressionDataUtils.to_string_name(
            attackResult.GetValueOrDefault("critical_source", "")
        );
        var critGateDie = (int)attackResult.GetValueOrDefault("crit_gate_die", 0);
        var hitRoll = (int)attackResult.GetValueOrDefault("hit_roll", 0);
        if (attackResolution == "critical_hit")
        {
            if (criticalSource == "high_threat")
                return REASON_CRITICAL_SUCCESS_HIGH_THREAT;
            if (criticalSource == "gate_die")
                return REASON_CRITICAL_SUCCESS_GATE_DIE;
        }
        if (attackResolution == "hit" && hitRoll >= 20 && critGateDie > 20)
            return REASON_ORDINARY_HIT_GATE_DIE_PENDING;
        if (
            attackResolution == "miss"
            && DictBool(attackResult, "reverse_fate_downgraded", false)
        )
            return REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED;
        if (attackResolution == "critical_fail")
            return REASON_CRITICAL_FAIL_FUMBLE_BAND;
        if (attackResolution == "miss")
            return REASON_ORDINARY_MISS_THRESHOLD;
        return "";
    }

    private StringName _ResolveAttackReasonId(
        AttackResolutionMetadata attackMetadata,
        StringName criticalSource
    )
    {
        attackMetadata ??= new AttackResolutionMetadata();
        var attackResolution = ProgressionDataUtils.to_string_name(
            attackMetadata.AttackResolution
        );
        var normalizedCriticalSource = ProgressionDataUtils.to_string_name(criticalSource);
        if (attackResolution == "critical_hit")
        {
            if (normalizedCriticalSource == "high_threat")
                return REASON_CRITICAL_SUCCESS_HIGH_THREAT;
            if (normalizedCriticalSource == "gate_die")
                return REASON_CRITICAL_SUCCESS_GATE_DIE;
        }
        if (
            attackResolution == "hit"
            && attackMetadata.HitRoll >= 20
            && attackMetadata.CritGateDie > 20
        )
            return REASON_ORDINARY_HIT_GATE_DIE_PENDING;
        if (attackResolution == "miss" && attackMetadata.ReverseFateDowngraded)
            return REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED;
        if (attackResolution == "critical_fail")
            return REASON_CRITICAL_FAIL_FUMBLE_BAND;
        if (attackResolution == "miss")
            return REASON_ORDINARY_MISS_THRESHOLD;
        return "";
    }

    private string _BuildAttackReportText(Dictionary entry)
    {
        var reasonId = ProgressionDataUtils.to_string_name(entry.GetValueOrDefault("reason_id", ""));
        var critGateDie = (int)entry.GetValueOrDefault("crit_gate_die", 0);
        var critGateRoll = (int)entry.GetValueOrDefault("crit_gate_roll", 0);
        var hitRoll = (int)entry.GetValueOrDefault("hit_roll", 0);
        var luckSnapshot = entry.GetValueOrDefault("luck_snapshot", new Dictionary())
            .AsGodotDictionary();
        var fumbleLowEnd = (int)luckSnapshot.GetValueOrDefault("fumble_low_end", 0);
        var critThreshold = (int)luckSnapshot.GetValueOrDefault("crit_threshold", 0);
        var text = "";
        switch (reasonId)
        {
            case "critical_success_gate_die":
                text =
                    $"命运判定：先掷大成功门骰 d{critGateDie}={critGateRoll}/{critGateDie}，这次大成功来自门骰。";
                break;
            case "critical_success_high_threat":
                text =
                    $"命运判定：命中骰 d20={hitRoll} 落入高位大成功区 {critThreshold}-20，这次大成功来自高位威胁。";
                break;
            case "ordinary_hit_gate_die_pending":
                text =
                    $"命运判定：d20={hitRoll} 仍只是普通命中；当前大成功门骰为 d{critGateDie}，必须先中过门骰。";
                break;
            case "critical_fail_fumble_band":
                text =
                    $"命运判定：d20={hitRoll} 落入大失败区间 1-{fumbleLowEnd}，直接判定为大失败。";
                break;
            case "ordinary_miss_fumble_downgraded":
                text =
                    $"命运判定：d20={hitRoll} 落入大失败区间 1-{fumbleLowEnd}，但被逆命护符降级为普通 miss。";
                break;
            case "ordinary_miss_threshold":
                var displayRequiredRoll = (int)entry.GetValueOrDefault("display_required_roll", 0);
                if (displayRequiredRoll <= 0)
                    displayRequiredRoll = (int)entry.GetValueOrDefault("required_roll", 0);
                var fumbleText = fumbleLowEnd > 1 ? $"1-{fumbleLowEnd}" : "1";
                text =
                    $"命运判定：命中骰 d20={hitRoll} 未达到命中线 {displayRequiredRoll}，也不在大失败区 {fumbleText}，因此只是普通 miss。";
                break;
        }
        return _AppendEventTagSuffix(
            text,
            entry.GetValueOrDefault("event_tags", new Godot.Collections.Array()).AsGodotArray()
        );
    }

    private string _BuildSkillEventText(Dictionary entry)
    {
        var reasonId = ProgressionDataUtils.to_string_name(entry.GetValueOrDefault("reason_id", ""));
        var attackerName = entry.GetValueOrDefault("attacker_name", "").AsString().StripEdges();
        var defenderName = entry.GetValueOrDefault("defender_name", "").AsString().StripEdges();
        var actorLabel = string.IsNullOrEmpty(attackerName) ? "该单位" : attackerName;
        var targetLabel = string.IsNullOrEmpty(defenderName) ? "目标" : defenderName;
        var text = "";
        switch (reasonId)
        {
            case "doom_sentence_applied":
                text = $"{actorLabel} 对 {targetLabel} 落下厄命宣判。";
                break;
        }
        return _AppendEventTagSuffix(
            text,
            entry.GetValueOrDefault("event_tags", new Godot.Collections.Array()).AsGodotArray()
        );
    }

    private string _AppendEventTagSuffix(string text, Godot.Collections.Array eventTags)
    {
        var tags = _NormalizeStringNameArray(eventTags);
        if (string.IsNullOrEmpty(text) || tags.Count == 0)
            return text;
        return $"{text} 事件标签：{string.Join(", ", ProgressionDataUtils.string_name_array_to_string_array(tags))}。";
    }

    private void _AppendDamageMitigationSourceLabels(
        Godot.Collections.Array sources,
        System.Collections.Generic.List<string> halfSourceLabels,
        System.Collections.Generic.List<string> doubleSourceLabels,
        System.Collections.Generic.List<string> immuneSourceLabels
    )
    {
        foreach (var sourceValue in sources)
        {
            var source = sourceValue.AsGodotDictionary();
            var sourceLabel = _FormatDamageSourceLabel(source);
            if (string.IsNullOrEmpty(sourceLabel))
                continue;
            var tier = ProgressionDataUtils.to_string_name(source.GetValueOrDefault("tier", ""));
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

    private void _AppendDamageFixedSourceLabels(
        Godot.Collections.Array sources,
        System.Collections.Generic.List<string> fixedSourceLabels
    )
    {
        foreach (var sourceValue in sources)
        {
            var sourceLabel = _FormatDamageSourceLabel(sourceValue.AsGodotDictionary());
            if (string.IsNullOrEmpty(sourceLabel))
                continue;
            _AppendUniqueDamageAbsorbLabel(fixedSourceLabels, sourceLabel);
        }
    }

    private string _FormatDamageSourceLabel(Dictionary source)
    {
        var statusId = source.GetValueOrDefault("status_id", "").AsString();
        if (!string.IsNullOrEmpty(statusId))
            return statusId;
        return source.GetValueOrDefault("type", "").AsString();
    }

    private string _FormatDamageSourceLabels(
        Godot.Collections.Array labelsValue,
        string fallback = ""
    )
    {
        if (labelsValue == null)
            return fallback;
        var labels = new Godot.Collections.Array<string>();
        foreach (var labelValue in labelsValue)
        {
            var label = labelValue.AsString();
            if (string.IsNullOrEmpty(label) || labels.Contains(label))
                continue;
            labels.Add(label);
        }
        if (labels.Count == 0)
            return fallback;
        return string.Join("、", labels);
    }

    private string _FormatDamageSourceLabels(
        System.Collections.Generic.IEnumerable<string> labelsValue,
        string fallback = ""
    )
    {
        if (labelsValue == null)
            return fallback;
        var labels = new System.Collections.Generic.List<string>();
        foreach (string label in labelsValue)
        {
            if (string.IsNullOrEmpty(label) || labels.Contains(label))
                continue;
            labels.Add(label);
        }
        if (labels.Count == 0)
            return fallback;
        return string.Join("、", labels);
    }

    private string _FormatDamageTierLogSuffix(DamageResultSummary summary)
    {
        if (summary != null && summary.AnyDouble)
        {
            var doubleSourceText = _FormatDamageSourceLabels(summary.DoubleSourceLabels);
            if (!string.IsNullOrEmpty(doubleSourceText))
                return $"（因 {doubleSourceText} 触发易伤）";
            return "（触发易伤）";
        }
        if (summary != null && summary.AnyHalf)
        {
            var halfSourceText = _FormatDamageSourceLabels(summary.HalfSourceLabels);
            if (!string.IsNullOrEmpty(halfSourceText))
                return $"（因 {halfSourceText} 减半后结算）";
            return "（减半后结算）";
        }
        return "";
    }

    private void _AppendUniqueDamageAbsorbLabel(
        System.Collections.Generic.List<string> absorbLabels,
        string label
    )
    {
        if (string.IsNullOrEmpty(label) || absorbLabels.Contains(label))
            return;
        absorbLabels.Add(label);
    }

    private Godot.Collections.Array<StringName> _NormalizeStringNameArray(
        Godot.Collections.Array<StringName> rawValues
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (rawValues == null)
            return result;
        foreach (StringName value in rawValues)
        {
            var normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }

    private Godot.Collections.Array<StringName> _NormalizeStringNameArray(
        Godot.Collections.Array rawValues
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (rawValues == null)
            return result;
        foreach (var value in rawValues)
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
        return BattleExecutionRules.IsEliteOrBossTarget(unitState);
    }

    private static bool DictBool(Dictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }
}
