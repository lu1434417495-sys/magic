using Godot;

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

internal static class EnemyAiGenerationKinds
{
    internal static EnemyAiGenerationSlotRole ToSlotRole(StringName value) =>
        value.ToString() switch
        {
            "offense" => EnemyAiGenerationSlotRole.Offense,
            "control" => EnemyAiGenerationSlotRole.Control,
            "support" => EnemyAiGenerationSlotRole.Support,
            "positioning" => EnemyAiGenerationSlotRole.Positioning,
            "survival" => EnemyAiGenerationSlotRole.Survival,
            "engage" => EnemyAiGenerationSlotRole.Engage,
            _ => EnemyAiGenerationSlotRole.Unknown,
        };

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

    internal static EnemyAiSkillAffordance ToAffordance(StringName value) =>
        value.ToString() switch
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

    internal static EnemyAiActionFamily ToActionFamily(StringName value) =>
        value.ToString() switch
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

    internal static EnemyAiGenerationSuppressionPolicy ToSuppressionPolicy(
        StringName value
    ) =>
        value.ToString() switch
        {
            "suppress_matching_family" =>
                EnemyAiGenerationSuppressionPolicy.SuppressMatchingFamily,
            "allow_companion" => EnemyAiGenerationSuppressionPolicy.AllowCompanion,
            "manual_only" => EnemyAiGenerationSuppressionPolicy.ManualOnly,
            _ => EnemyAiGenerationSuppressionPolicy.Unknown,
        };

    internal static StringName ToStringName(EnemyAiGenerationSuppressionPolicy value) =>
        value switch
        {
            EnemyAiGenerationSuppressionPolicy.SuppressMatchingFamily =>
                "suppress_matching_family",
            EnemyAiGenerationSuppressionPolicy.AllowCompanion => "allow_companion",
            EnemyAiGenerationSuppressionPolicy.ManualOnly => "manual_only",
            _ => "",
        };
}
