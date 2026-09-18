using System.Collections.Generic;

// 伤害范围是共享预览事实，不依赖构建它的规则服务。
public readonly record struct BattleDamageDiceRange(
    int DiceCount,
    int DiceSides,
    int DiceBonus,
    int MinDamage,
    int MaxDamage
)
{
    public static BattleDamageDiceRange Empty => new(0, 0, 0, 0, 0);
}

public readonly record struct BattleDamageEffectRange(
    int EffectIndex,
    int Power,
    bool AddWeaponDice,
    int MinDamage,
    int MaxDamage,
    BattleDamageDiceRange SkillDiceRange,
    BattleDamageDiceRange WeaponDiceRange
);

public readonly record struct BattleSkillDamagePreview(
    bool HasDamage,
    int MinDamage,
    int MaxDamage,
    IReadOnlyList<BattleDamageEffectRange> DamageRanges
)
{
    public string SummaryText =>
        HasDamage
            ? (MinDamage == MaxDamage ? $"伤害 {MinDamage}" : $"伤害 {MinDamage}-{MaxDamage}")
            : "";
}
