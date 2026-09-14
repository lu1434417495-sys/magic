using Godot;

// 伤害来源 closed domain：区分主技能/攻击直接伤害与 timeline/upkeep、terrain、
// reflection、self damage、equipment bonus、equipment direct reaction、
// equipment trigger-skill/immediate-attack 等来源。fail-closed：未识别字符串
// 映射到 Unknown，Unknown 不是有效来源；不得从具体 skill/item/binding id
// 或调用深度猜测 origin。
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
    internal static BattleDamageOriginKind ToDamageOriginKind(StringName value)
    {
        return value.ToString() switch
        {
            "main_direct_effect" => BattleDamageOriginKind.MainDirectEffect,
            "timeline_upkeep" => BattleDamageOriginKind.TimelineUpkeep,
            "terrain" => BattleDamageOriginKind.Terrain,
            "reflection" => BattleDamageOriginKind.Reflection,
            "self_damage" => BattleDamageOriginKind.SelfDamage,
            "equipment_bonus" => BattleDamageOriginKind.EquipmentBonus,
            "equipment_direct_reaction" => BattleDamageOriginKind.EquipmentDirectReaction,
            "equipment_triggered_skill" => BattleDamageOriginKind.EquipmentTriggeredSkill,
            _ => BattleDamageOriginKind.Unknown,
        };
    }

    internal static StringName ToStringName(BattleDamageOriginKind kind)
    {
        return kind switch
        {
            BattleDamageOriginKind.MainDirectEffect => new StringName("main_direct_effect"),
            BattleDamageOriginKind.TimelineUpkeep => new StringName("timeline_upkeep"),
            BattleDamageOriginKind.Terrain => new StringName("terrain"),
            BattleDamageOriginKind.Reflection => new StringName("reflection"),
            BattleDamageOriginKind.SelfDamage => new StringName("self_damage"),
            BattleDamageOriginKind.EquipmentBonus => new StringName("equipment_bonus"),
            BattleDamageOriginKind.EquipmentDirectReaction => new StringName(
                "equipment_direct_reaction"
            ),
            BattleDamageOriginKind.EquipmentTriggeredSkill => new StringName(
                "equipment_triggered_skill"
            ),
            _ => new StringName(""),
        };
    }

    internal static bool IsValid(BattleDamageOriginKind kind)
    {
        return kind != BattleDamageOriginKind.Unknown;
    }

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

    public static string ValidDamageOriginLabel()
    {
        return "equipment_bonus, equipment_direct_reaction, equipment_triggered_skill, main_direct_effect, reflection, self_damage, terrain, timeline_upkeep";
    }
}
