using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GEnemyAiActionArray = Godot.Collections.Array<EnemyAiAction>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal enum EnemyAiGenerationSlotRole
{
    Unknown,
    Offense,
    Control,
    Support,
    Positioning,
    Survival,
    Engage,
}

internal enum EnemyAiSkillAffordance
{
    Unknown,
    UnitHostileDamage,
    UnitHostileControl,
    GroundHostileAoe,
    GroundControl,
    TerrainControl,
    DisplacementControl,
    ChargeEngage,
    ChargePathAoe,
    MultiUnit,
    RandomChain,
    SpecialGround,
    AllyHeal,
    SelfOrAllyBuff,
    Reposition,
    Escape,
    Utility,
    Breaker,
}

internal enum EnemyAiActionFamily
{
    Unknown,
    UseUnitSkill,
    UseGroundSkill,
    UseMultiUnitSkill,
    UseRandomChainSkill,
    UseCharge,
    UseChargePathAoe,
    MoveToRange,
    MoveToMultiUnitSkillPosition,
}

internal enum EnemyAiGenerationSuppressionPolicy
{
    Unknown,
    SuppressMatchingFamily,
    AllowCompanion,
    ManualOnly,
}

[Tool]
[GlobalClass]
public partial class EnemyAiGenerationSlotDef : Resource
{
    [Export]
    public StringName slot_id = "";

    [Export]
    public StringName slot_role = "offense";
    internal EnemyAiGenerationSlotRole SlotRoleKind
    {
        get => ToSlotRole(slot_role);
        set => slot_role = ToStringName(value);
    }

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
    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    [Export]
    public StringName suppression_policy = "suppress_matching_family";
    internal EnemyAiGenerationSuppressionPolicy SuppressionPolicyKind
    {
        get => ToSuppressionPolicy(suppression_policy);
        set => suppression_policy = ToStringName(value);
    }

    internal static EnemyAiGenerationSlotRole ToSlotRole(StringName value)
    {
        return value.ToString() switch
        {
            "offense" => EnemyAiGenerationSlotRole.Offense,
            "control" => EnemyAiGenerationSlotRole.Control,
            "support" => EnemyAiGenerationSlotRole.Support,
            "positioning" => EnemyAiGenerationSlotRole.Positioning,
            "survival" => EnemyAiGenerationSlotRole.Survival,
            "engage" => EnemyAiGenerationSlotRole.Engage,
            _ => EnemyAiGenerationSlotRole.Unknown,
        };
    }

    internal static StringName ToStringName(EnemyAiGenerationSlotRole value) =>
        value switch
        {
            EnemyAiGenerationSlotRole.Offense => "offense",
            EnemyAiGenerationSlotRole.Control => "control",
            EnemyAiGenerationSlotRole.Support => "support",
            EnemyAiGenerationSlotRole.Positioning => "positioning",
            EnemyAiGenerationSlotRole.Survival => "survival",
            EnemyAiGenerationSlotRole.Engage => "engage",
            _ => "",
        };

    internal static EnemyAiSkillAffordance ToAffordance(StringName value)
    {
        return value.ToString() switch
        {
            "unit_hostile.damage" => EnemyAiSkillAffordance.UnitHostileDamage,
            "unit_hostile.control" => EnemyAiSkillAffordance.UnitHostileControl,
            "ground_hostile.aoe" => EnemyAiSkillAffordance.GroundHostileAoe,
            "ground_control" => EnemyAiSkillAffordance.GroundControl,
            "terrain_control" => EnemyAiSkillAffordance.TerrainControl,
            "displacement_control" => EnemyAiSkillAffordance.DisplacementControl,
            "charge_engage" => EnemyAiSkillAffordance.ChargeEngage,
            "charge_path_aoe" => EnemyAiSkillAffordance.ChargePathAoe,
            "multi_unit" => EnemyAiSkillAffordance.MultiUnit,
            "random_chain" => EnemyAiSkillAffordance.RandomChain,
            "special_ground" => EnemyAiSkillAffordance.SpecialGround,
            "ally_heal" => EnemyAiSkillAffordance.AllyHeal,
            "self_or_ally_buff" => EnemyAiSkillAffordance.SelfOrAllyBuff,
            "reposition" => EnemyAiSkillAffordance.Reposition,
            "escape" => EnemyAiSkillAffordance.Escape,
            "utility" => EnemyAiSkillAffordance.Utility,
            "breaker" => EnemyAiSkillAffordance.Breaker,
            _ => EnemyAiSkillAffordance.Unknown,
        };
    }

    internal static EnemyAiActionFamily ToActionFamily(StringName value)
    {
        return value.ToString() switch
        {
            "use_unit_skill" => EnemyAiActionFamily.UseUnitSkill,
            "use_ground_skill" => EnemyAiActionFamily.UseGroundSkill,
            "use_multi_unit_skill" => EnemyAiActionFamily.UseMultiUnitSkill,
            "use_random_chain_skill" => EnemyAiActionFamily.UseRandomChainSkill,
            "use_charge" => EnemyAiActionFamily.UseCharge,
            "use_charge_path_aoe" => EnemyAiActionFamily.UseChargePathAoe,
            "move_to_range" => EnemyAiActionFamily.MoveToRange,
            "move_to_multi_unit_skill_position" =>
                EnemyAiActionFamily.MoveToMultiUnitSkillPosition,
            _ => EnemyAiActionFamily.Unknown,
        };
    }

    internal static StringName ToStringName(EnemyAiActionFamily value) =>
        value switch
        {
            EnemyAiActionFamily.UseUnitSkill => "use_unit_skill",
            EnemyAiActionFamily.UseGroundSkill => "use_ground_skill",
            EnemyAiActionFamily.UseMultiUnitSkill => "use_multi_unit_skill",
            EnemyAiActionFamily.UseRandomChainSkill => "use_random_chain_skill",
            EnemyAiActionFamily.UseCharge => "use_charge",
            EnemyAiActionFamily.UseChargePathAoe => "use_charge_path_aoe",
            EnemyAiActionFamily.MoveToRange => "move_to_range",
            EnemyAiActionFamily.MoveToMultiUnitSkillPosition =>
                "move_to_multi_unit_skill_position",
            _ => "",
        };

    internal static EnemyAiGenerationSuppressionPolicy ToSuppressionPolicy(StringName value)
    {
        return value.ToString() switch
        {
            "suppress_matching_family" =>
                EnemyAiGenerationSuppressionPolicy.SuppressMatchingFamily,
            "allow_companion" => EnemyAiGenerationSuppressionPolicy.AllowCompanion,
            "manual_only" => EnemyAiGenerationSuppressionPolicy.ManualOnly,
            _ => EnemyAiGenerationSuppressionPolicy.Unknown,
        };
    }

    internal static StringName ToStringName(EnemyAiGenerationSuppressionPolicy value) =>
        value switch
        {
            EnemyAiGenerationSuppressionPolicy.SuppressMatchingFamily =>
                "suppress_matching_family",
            EnemyAiGenerationSuppressionPolicy.AllowCompanion => "allow_companion",
            EnemyAiGenerationSuppressionPolicy.ManualOnly => "manual_only",
            _ => "",
        };

    internal bool MatchesAffordance(
        BattleAiSkillAffordanceRecord record,
        StringName actionFamily
    )
    {
        EnemyAiActionFamily actionFamilyKind = ToActionFamily(actionFamily);
        if (record == null || !ContainsActionFamily(action_families, actionFamilyKind))
            return false;
        if (allowed_affordances.Count == 0)
            return false;
        if (record.affordances.Count == 0)
            return false;
        foreach (StringName affordance in record.affordances)
        {
            if (ContainsAffordance(allowed_affordances, ToAffordance(affordance)))
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

    public GArray ValidateSchema(
        string context_label = "Enemy AI generation slot",
        GEnemyAiActionArray state_actions = null
    )
    {
        var errors = new GArray();
        var actions = state_actions ?? new GEnemyAiActionArray();
        var label = $"{context_label} generation slot {slot_id}";
        if (slot_id == (StringName)"")
            errors.Add($"{context_label} is missing slot_id.");
        if (SlotRoleKind == EnemyAiGenerationSlotRole.Unknown)
            errors.Add($"{label} declares unsupported slot_role {slot_role}.");
        if (order < 0)
            errors.Add($"{label} order must be >= 0.");
        if (allowed_affordances.Count == 0)
            errors.Add($"{label} must declare at least one allowed_affordance.");
        foreach (var affordance in allowed_affordances)
        {
            if (ToAffordance(affordance) == EnemyAiSkillAffordance.Unknown)
                errors.Add($"{label} declares unsupported affordance {affordance}.");
        }
        if (action_families.Count == 0)
            errors.Add($"{label} must declare at least one action_family.");
        foreach (var family in action_families)
        {
            if (ToActionFamily(family) == EnemyAiActionFamily.Unknown)
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
        if (DistanceReferenceKind == EnemyAiDistanceReference.Unknown)
            errors.Add($"{label} declares unsupported distance_reference {distance_reference}.");
        if (SuppressionPolicyKind == EnemyAiGenerationSuppressionPolicy.Unknown)
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

    private static bool ContainsActionFamily(
        GStringNameArray values,
        EnemyAiActionFamily expected
    )
    {
        if (expected == EnemyAiActionFamily.Unknown)
            return false;
        foreach (StringName value in values)
        {
            if (ToActionFamily(value) == expected)
                return true;
        }
        return false;
    }

    private static bool ContainsAffordance(
        GStringNameArray values,
        EnemyAiSkillAffordance expected
    )
    {
        if (expected == EnemyAiSkillAffordance.Unknown)
            return false;
        foreach (StringName value in values)
        {
            if (ToAffordance(value) == expected)
                return true;
        }
        return false;
    }
}
