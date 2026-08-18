// 伤害来源 closed domain：区分主技能/攻击直接伤害与 timeline/upkeep、terrain、
// reflection、self damage、equipment bonus、equipment direct reaction、
// equipment trigger-skill/immediate-attack 等来源。Unknown 是 fail-closed 默认值；
// 不得从具体 skill/item/binding id 或调用深度猜测 origin。
internal enum BattleDamageOriginKind
{
    Unknown = 0,
    MainDirectEffect,
    TimelineUpkeep,
    Terrain,
    Reflection,
    SelfDamage,
    EquipmentBonus,
    EquipmentDirectReaction,
    EquipmentTriggeredSkill,
}

public static class BattleDamageOriginContentRules
{
    // 生产者入口的显式 origin 收口：主直接伤害段在 source 与 target 为同一单位时
    // 归类为 self_damage；其余 declared origin 原样保留（equipment/terrain/timeline
    // 等来源优先于自伤判定，与 canonical 结算的事实顺序一致）。
    internal static BattleDamageOriginKind ResolveProducerOrigin(
        BattleDamageOriginKind declaredOrigin,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (
            declaredOrigin == BattleDamageOriginKind.MainDirectEffect
            && sourceUnit != null
            && targetUnit != null
            && sourceUnit.unit_id == targetUnit.unit_id
        )
        {
            return BattleDamageOriginKind.SelfDamage;
        }
        return declaredOrigin;
    }
}
