using System;
using System.Collections.Generic;
using Godot;

internal static class BattleRangedWeaponReactionPreviewBuilder
{
    internal static BattleRangedWeaponReactionPreviewData Build(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatRangedWeaponReactionDefinition profile =
            skillDefinition?.CombatProfile?.RangedWeaponReaction;
        if (profile == null || !sourceUnit.IsValid)
            return null;

        int skillLevel = Math.Max(
            sourceUnit.GetKnownSkillLevel(skillDefinition.SkillId, fallback: 0),
            0
        );
        CombatEffectDefinition readinessEffect = null;
        foreach (
            CombatEffectDefinition effect in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectKind == BattleEffectKind.Status
                && effect.StatusId == profile.ReadinessStatusId
                && effect.IsUnlockedAtSkillLevel(skillLevel)
            )
            {
                readinessEffect = effect;
                break;
            }
        }
        if (readinessEffect == null)
            return null;

        string familyText = FormatWeaponFamilies(profile.TriggerWeaponFamilies);
        string outcomeText = profile.TriggerOnHit && profile.TriggerOnMiss
            ? "命中或未命中"
            : profile.TriggerOnHit
                ? "命中"
                : "未命中";
        string bonusText = profile.GetAttackRollBonus(skillLevel) >= 0
            ? $"+{profile.GetAttackRollBonus(skillLevel)}"
            : profile.GetAttackRollBonus(skillLevel).ToString();
        string criticalText = profile.AllowCritical ? "可暴击" : "不可暴击";
        string statusLabel = string.IsNullOrWhiteSpace(readinessEffect.DisplayName)
            ? skillDefinition.DisplayName
            : readinessEffect.DisplayName;
        string defenseLabel = FormatAttackDefenseMode(profile.AttackDefenseModeKind);
        string damageLabel = FormatDamageTag(profile.DamageTag);
        string summary =
            $"{statusLabel}：{Math.Max(readinessEffect.Power, 0)} 次，持续 {Math.Max(readinessEffect.DurationTu, 0)}TU；受到{familyText}远程武器攻击且原检定{outcomeText}时，在原攻击完整结算后消耗 {profile.ConsumeStatusStacks} 次，以 {bonusText} 进行独立{defenseLabel}攻击。命中复制原攻击快照的主武器骰与W倍率并转为{damageLabel}伤害，不复制固定加值、暴击额外骰或其他伤害段，且{criticalText}。重施恢复至当前上限并刷新持续时间。";
        return new BattleRangedWeaponReactionPreviewData(
            readinessEffect.Power,
            readinessEffect.DurationTu,
            profile.ConsumeStatusStacks,
            profile.GetAttackRollBonus(skillLevel),
            profile.AttackDefenseMode,
            profile.DamageTag,
            profile.TriggerWeaponFamilies,
            profile.TriggerOnHit,
            profile.TriggerOnMiss,
            profile.AllowCritical,
            summary
        );
    }

    private static string FormatWeaponFamilies(IReadOnlyList<StringName> families)
    {
        var labels = new List<string>();
        foreach (StringName family in families ?? Array.Empty<StringName>())
        {
            labels.Add(
                family == new StringName("bow")
                    ? "弓"
                    : family == new StringName("crossbow")
                        ? "弩"
                        : family.ToString()
            );
        }
        return labels.Count > 0 ? string.Join("/", labels) : "指定";
    }

    private static string FormatAttackDefenseMode(
        CombatSkillAttackDefenseMode defenseMode
    ) =>
        defenseMode switch
        {
            CombatSkillAttackDefenseMode.Touch => "接触",
            CombatSkillAttackDefenseMode.FlatFooted => "措手不及AC",
            CombatSkillAttackDefenseMode.Normal => "普通AC",
            _ => "配置AC",
        };

    private static string FormatDamageTag(StringName damageTag) =>
        damageTag == new StringName("force")
            ? "力场"
            : damageTag == new StringName("fire")
                ? "火焰"
                : damageTag == new StringName("cold")
                    ? "寒冷"
                    : damageTag.ToString();
}
