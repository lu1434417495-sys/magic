using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GEnemyAiActionArray = Godot.Collections.Array<EnemyAiAction>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[Tool]
[GlobalClass]
public partial class EnemyAiGenerationSlotDef : Resource
{
    [Export]
    public StringName slot_id = "";

    [Export]
    public StringName slot_role = "offense";

    [Export]
    public int order = 0;

    [Export]
    public GStringNameArray allowed_affordances = new();

    [Export]
    public GStringNameArray action_families = new();

    [Export]
    public StringName style_template_action_id = "";

    [Export]
    public StringName score_bucket_id = "";

    [Export]
    public StringName target_selector = "";

    [Export]
    public int desired_min_distance = -1;

    [Export]
    public int desired_max_distance = -1;

    [Export]
    public StringName distance_reference = "";

    [Export]
    public StringName suppression_policy = "suppress_matching_family";

    private static readonly HashSet<string> ValidAffordances =
        new(System.StringComparer.Ordinal)
        {
            "unit_hostile.damage",
            "unit_hostile.control",
            "ground_hostile.aoe",
            "ground_control",
            "terrain_control",
            "displacement_control",
            "charge_engage",
            "charge_path_aoe",
            "multi_unit",
            "random_chain",
            "special_ground",
            "ally_heal",
            "self_or_ally_buff",
            "reposition",
            "escape",
            "utility",
            "breaker",
        };

    private static readonly HashSet<string> ValidActionFamilies =
        new(System.StringComparer.Ordinal)
        {
            "use_unit_skill",
            "use_ground_skill",
            "use_multi_unit_skill",
            "use_random_chain_skill",
            "use_charge",
            "use_charge_path_aoe",
            "move_to_range",
            "move_to_multi_unit_skill_position",
        };

    private static readonly HashSet<string> ValidSlotRoles =
        new(System.StringComparer.Ordinal)
        {
            "offense",
            "control",
            "support",
            "positioning",
            "survival",
            "engage",
        };

    private static readonly HashSet<string> ValidDistanceReferences =
        new(System.StringComparer.Ordinal)
        {
            "",
            "target_unit",
            "target_coord",
            "candidate_pool",
            "enemy_frontline",
        };

    private static readonly HashSet<string> ValidSuppressionPolicies =
        new(System.StringComparer.Ordinal)
        {
            "suppress_matching_family",
            "allow_companion",
            "manual_only",
        };

    internal bool MatchesAffordance(
        BattleAiSkillAffordanceRecord record,
        StringName actionFamily
    )
    {
        if (record == null || !action_families.Contains(actionFamily))
            return false;
        if (allowed_affordances.Count == 0)
            return false;
        if (record.affordances.Count == 0)
            return false;
        foreach (StringName affordance in record.affordances)
        {
            if (allowed_affordances.Contains(affordance))
                return true;
        }
        return false;
    }

    internal string BuildSignature() =>
        string.Join(
            "|",
            new[]
            {
                $"slot_id={slot_id}",
                $"slot_role={slot_role}",
                $"order={order}",
                $"allowed_affordances={StringifyArray(allowed_affordances)}",
                $"action_families={StringifyArray(action_families)}",
                $"style_template_action_id={style_template_action_id}",
                $"score_bucket_id={score_bucket_id}",
                $"target_selector={target_selector}",
                $"desired_min_distance={desired_min_distance}",
                $"desired_max_distance={desired_max_distance}",
                $"distance_reference={distance_reference}",
                $"suppression_policy={suppression_policy}",
            }
        );

    public GArray validate_schema(
        string context_label = "Enemy AI generation slot",
        GEnemyAiActionArray state_actions = null
    )
    {
        var errors = new GArray();
        var actions = state_actions ?? new GEnemyAiActionArray();
        var label = $"{context_label} generation slot {slot_id}";
        if (slot_id == (StringName)"")
            errors.Add($"{context_label} is missing slot_id.");
        if (!ValidSlotRoles.Contains(slot_role.ToString()))
            errors.Add($"{label} declares unsupported slot_role {slot_role}.");
        if (order < 0)
            errors.Add($"{label} order must be >= 0.");
        if (allowed_affordances.Count == 0)
            errors.Add($"{label} must declare at least one allowed_affordance.");
        foreach (var affordance in allowed_affordances)
        {
            if (!ValidAffordances.Contains(affordance.ToString()))
                errors.Add($"{label} declares unsupported affordance {affordance}.");
        }
        if (action_families.Count == 0)
            errors.Add($"{label} must declare at least one action_family.");
        foreach (var family in action_families)
        {
            if (!ValidActionFamilies.Contains(family.ToString()))
                errors.Add($"{label} declares unsupported action_family {family}.");
        }
        if (
            style_template_action_id != (StringName)""
            && _find_action_by_id(actions, style_template_action_id) == null
        )
            errors.Add(
                $"{label} style_template_action_id {style_template_action_id} does not exist in the same state."
            );
        if (!EnemyAiTargetSelectorRules.IsSupportedSelector(target_selector, allowEmpty: true))
            errors.Add($"{label} declares unsupported target_selector {target_selector}.");
        if (desired_min_distance < -1)
            errors.Add($"{label} desired_min_distance must be >= -1.");
        if (desired_max_distance < -1)
            errors.Add($"{label} desired_max_distance must be >= -1.");
        if (
            desired_min_distance >= 0
            && desired_max_distance >= 0
            && desired_min_distance > desired_max_distance
        )
            errors.Add($"{label} desired_min_distance cannot exceed desired_max_distance.");
        if (!ValidDistanceReferences.Contains(distance_reference.ToString()))
            errors.Add($"{label} declares unsupported distance_reference {distance_reference}.");
        if (!ValidSuppressionPolicies.Contains(suppression_policy.ToString()))
            errors.Add($"{label} declares unsupported suppression_policy {suppression_policy}.");
        return errors;
    }

    private static EnemyAiAction _find_action_by_id(
        GEnemyAiActionArray state_actions,
        StringName expected_action_id
    )
    {
        foreach (EnemyAiAction action in state_actions)
        {
            if (
                action != null
                && ProgressionDataUtils.to_string_name(action.action_id) == expected_action_id
            )
                return action;
        }
        return null;
    }

    private static string StringifyArray(GStringNameArray values)
    {
        var result = new List<string>();
        foreach (var value in values)
            result.Add(value.ToString());
        result.Sort(System.StringComparer.Ordinal);
        return string.Join(",", result);
    }
}
