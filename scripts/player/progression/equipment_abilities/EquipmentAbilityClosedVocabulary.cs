#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal static class EquipmentAbilityClosedVocabulary
{
    internal static IReadOnlyList<string> TriggerValues { get; } = Values(
        "on_hit", "on_attack_hit", "on_kill", "on_granted_skill_used", "on_turn_end",
        "on_damage_roll", "on_damage_applied", "on_damage_taken_finalized",
        "on_hit_received", "on_attack_check", "on_target_mark_expired"
    );

    internal static IReadOnlyList<string> TimingValues { get; } = Values(
        "before_hit", "after_hit", "after_kill", "after_skill", "after_turn",
        "before_damage", "after_damage", "after_hit_received",
        "after_attack_check", "after_status_expired"
    );

    internal static IReadOnlyList<string> ConditionGroupModeValues { get; } =
        Values("", "all", "any");
    internal static IReadOnlyList<string> FactQueryKindValues { get; } =
        Values("fact", "literal");
    internal static IReadOnlyList<string> FactIdValues { get; } = Values(
        "creature_type_tags", "battle_environment_tag", "hp_percent_bp", "hp_before",
        "hp_before_percent_bp", "critical_hit", "hp_damage", "raw_damage", "damage_tag",
        "is_equipment_generated", "is_self_damage", "skill_damaged_target_count",
        "skill_killed_target_count", "skill_hp_damage_dealt", "skill_moved_target_count",
        "skill_unmoved_target_count", "body_size", "attribute_value", "current_tu",
        "current_action_points", "equipment_ability_state", "kill_source_is_attack",
        "kill_source_equipment_instance_matches", "kill_source_binding_matches",
        "equipment_target_mark_matches", "equipment_target_mark_stacks",
        "expired_target_mark_matches", "status_stacks", "nearby_enemy_count",
        "nearby_unit_count", "nearby_ally_count", "summoned_unit_count",
        "source_status_total_stacks", "unit_distance", "weapon_range_type", "save_tag"
    );
    internal static IReadOnlyList<string> FactSubjectValues { get; } = Values(
        "", "source", "attacker", "owner", "target", "attack_target", "defender",
        "defeated", "victim", "skill_target", "selected_target"
    );
    internal static IReadOnlyList<string> FactAggregationValues { get; } =
        Values("", "value", "floor_div");
    internal static IReadOnlyList<string> FactValueKindValues { get; } = Values(
        "", "bool", "int", "float", "string_name", "string_name_set", "source",
        "attacker", "owner", "target", "attack_target", "defender", "defeated",
        "victim", "skill_target", "selected_target"
    );

    private static readonly IReadOnlySet<string> TriggerSet = Set(TriggerValues);
    private static readonly IReadOnlySet<string> TimingSet = Set(TimingValues);
    private static readonly IReadOnlySet<string> ConditionGroupModeSet = Set(ConditionGroupModeValues);
    private static readonly IReadOnlySet<string> FactQueryKindSet = Set(FactQueryKindValues);
    private static readonly IReadOnlySet<string> FactIdSet = Set(FactIdValues);
    private static readonly IReadOnlySet<string> FactSubjectSet = Set(FactSubjectValues);
    private static readonly IReadOnlySet<string> FactAggregationSet = Set(FactAggregationValues);
    private static readonly IReadOnlySet<string> FactValueKindSet = Set(FactValueKindValues);

    internal static bool IsKnownTrigger(string? value) => TriggerSet.Contains(value ?? "");
    internal static bool IsKnownTiming(string? value) => TimingSet.Contains(value ?? "");
    internal static bool IsKnownConditionGroupMode(string? value) =>
        ConditionGroupModeSet.Contains(value ?? "");
    internal static bool IsKnownFactQueryKind(string? value) =>
        FactQueryKindSet.Contains(value ?? "");
    internal static bool IsKnownFactId(string? value) => FactIdSet.Contains(value ?? "");
    internal static bool IsKnownFactSubject(string? value) => FactSubjectSet.Contains(value ?? "");
    internal static bool IsKnownFactAggregation(string? value) =>
        FactAggregationSet.Contains(value ?? "");
    internal static bool IsKnownFactValueKind(string? value) =>
        FactValueKindSet.Contains(value ?? "");

    private static IReadOnlyList<string> Values(params string[] values) =>
        new ReadOnlyCollection<string>(values);
    private static IReadOnlySet<string> Set(IEnumerable<string> values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
