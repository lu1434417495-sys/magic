using Godot;

/// <summary>
/// 武器训练技能的识别与按当前武器族映射。执行期熟练度归属与反击 capability 校验共用这一份规则。
/// </summary>
internal static class BattleWeaponTrainingRules
{
    private static readonly StringName BowTrainingSkillId = "bow_training";
    private static readonly StringName SwordTrainingSkillId = "sword_training";
    private static readonly StringName UnarmedTrainingSkillId = "unarmed_training";

    internal static bool IsWeaponTrainingSkillId(StringName skillId)
    {
        StringName normalizedSkillId =
            ProgressionDataUtils.to_string_name(skillId);
        return normalizedSkillId == SwordTrainingSkillId
            || normalizedSkillId == BowTrainingSkillId
            || normalizedSkillId == UnarmedTrainingSkillId;
    }

    internal static StringName ResolveWeaponTrainingSkillId(
        BattleUnitState sourceUnit
    )
    {
        if (sourceUnit == null)
            return new StringName("");
        BattleWeaponProjectionValues weaponProjection =
            sourceUnit.GetWeaponProjectionReadViewTyped().Values;
        var weaponFamily = ProgressionDataUtils.to_string_name(weaponProjection.Family);
        if (weaponFamily == "sword")
            return SwordTrainingSkillId;
        if (weaponFamily == "bow")
            return BowTrainingSkillId;
        if (weaponFamily == "unarmed")
            return UnarmedTrainingSkillId;
        var weaponKind = ProgressionDataUtils.to_string_name(weaponProjection.ProfileKind);
        if (
            weaponKind == BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed)
            || weaponKind == BattleUnitState.ToStringName(BattleWeaponProfileKind.Natural)
        )
            return UnarmedTrainingSkillId;
        return new StringName("");
    }
}
