internal static class CharacterAttributeDisplayText
{
    internal static string GetLabel(Godot.StringName attributeId)
    {
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength))
            return "力量";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            return "敏捷";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution))
            return "体质";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception))
            return "感知";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence))
            return "智力";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower))
            return "意志";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth))
            return "出生隐藏幸运";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.FaithLuckBonus))
            return "信仰幸运加值";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.HpMax))
            return "生命上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus))
            return "人物生命加成%";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.MpMax))
            return "法力上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.StaminaMax))
            return "体力上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus))
            return "体力恢复加成%";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.NaturalArmorAcBonus))
            return "天生护甲 AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.AuraMax))
            return "灵气上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ActionPoints))
            return "行动点";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ActionThreshold))
            return "行动阈值 TU";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ArmorClass))
            return "AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus))
            return "护甲 AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ShieldAcBonus))
            return "盾牌 AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.DodgeBonus))
            return "闪避加值";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.DeflectionBonus))
            return "偏斜加值";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus))
            return "护甲敏捷上限";
        if (attributeId == AttributeService.BASE_ATTACK_BONUS)
            return "基础攻击加值";
        if (attributeId == AttributeService.ATTACK_BONUS)
            return "攻击加值";
        if (attributeId == AttributeService.SPELL_PROFICIENCY_BONUS)
            return "施法熟练加值";
        if (attributeId == AttributeService.WEAPON_ATTACK_RANGE)
            return "武器射程";
        if (attributeId == AttributeService.MP_MAX_UNRESERVED)
            return "完整法力上限";
        if (attributeId == AttributeService.RESERVED_MP_MAX)
            return "预留法力";
        return attributeId.ToString();
    }

}
