internal enum EnemyAiActionKind
{
    UseUnitSkill,
    UseGroundSkill,
    UseMultiUnitSkill,
    MoveToMultiUnitSkillPosition,
    UseRandomChainSkill,
    UseCharge,
    UseChargePathAoe,
    MoveToRange,
    MoveToAdvantagePosition,
    UseGroundRepositionSkill,
    Retreat,
    Wait,
}

internal static class EnemyAiActionKinds
{
    internal static string ToContentId(EnemyAiActionKind kind) =>
        kind switch
        {
            EnemyAiActionKind.UseUnitSkill => "use_unit_skill",
            EnemyAiActionKind.UseGroundSkill => "use_ground_skill",
            EnemyAiActionKind.UseMultiUnitSkill => "use_multi_unit_skill",
            EnemyAiActionKind.MoveToMultiUnitSkillPosition =>
                "move_to_multi_unit_skill_position",
            EnemyAiActionKind.UseRandomChainSkill => "use_random_chain_skill",
            EnemyAiActionKind.UseCharge => "use_charge",
            EnemyAiActionKind.UseChargePathAoe => "use_charge_path_aoe",
            EnemyAiActionKind.MoveToRange => "move_to_range",
            EnemyAiActionKind.MoveToAdvantagePosition => "move_to_advantage_position",
            EnemyAiActionKind.UseGroundRepositionSkill =>
                "use_ground_reposition_skill",
            EnemyAiActionKind.Retreat => "retreat",
            EnemyAiActionKind.Wait => "wait",
            _ => "unknown",
        };
}
