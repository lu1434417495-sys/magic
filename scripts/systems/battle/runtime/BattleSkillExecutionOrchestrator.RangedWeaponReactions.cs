using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
    : IBattleRangedWeaponAttackReactionSink
{
    void IBattleRangedWeaponAttackReactionSink.ResolveRangedWeaponAttackReaction(
        BattleRangedWeaponAttackReactionContext context
    )
    {
        ResolveRangedWeaponAttackReaction(context);
    }

    internal void ResolveRangedWeaponAttackReaction(
        BattleRangedWeaponAttackReactionContext context
    )
    {
        BattleUnitState attacker = context?.Attacker;
        BattleUnitState defender = context?.Defender;
        BattleRangedWeaponAttackSnapshot snapshot =
            context?.WeaponSnapshot ?? BattleRangedWeaponAttackSnapshot.Empty;
        if (
            Runtime == null
            || attacker?.IsAlive() != true
            || defender?.IsAlive() != true
            || attacker.unit_id == defender.unit_id
            || attacker.faction_id == ""
            || defender.faction_id == ""
            || attacker.faction_id == defender.faction_id
            || !snapshot.IsUsable
        )
        {
            return;
        }

        RangedWeaponReactionCandidate candidate = FindRangedWeaponReactionCandidate(
            defender,
            snapshot.WeaponFamily,
            context.TriggeringAttackSucceeded
        );
        if (candidate == null)
            return;

        ConsumeReadinessStacks(
            defender,
            candidate.Status,
            candidate.Profile.ConsumeStatusStacks
        );
        Runtime._append_changed_unit_id(context.Batch, defender.unit_id);
        context.Batch?.AddLogLine(
            $"{ReactionDisplayName(defender)} 的{candidate.SkillDefinition.DisplayName}被 {ReactionDisplayName(attacker)} 的{FormatWeaponFamily(snapshot.WeaponFamily)}攻击触发，消耗{candidate.Profile.ConsumeStatusStacks}次反应准备。"
        );

        BattleAttackCheckPolicyService attackPolicy =
            Runtime.GetAttackCheckPolicyService();
        BattleAttackCheckPolicyContext policyContext =
            attackPolicy.BuildSkillDefinitionAttackContext(
                context.BattleState,
                defender,
                attacker,
                candidate.SkillDefinition,
                "ranged_weapon_reaction",
                "execute",
                false
            );
        AttackCheckInput attackCheck = attackPolicy.BuildAttackCheck(
            policyContext,
            candidate.Profile.GetAttackRollBonus(candidate.SkillLevel),
            0
        );
        if (!candidate.Profile.AllowCritical)
            attackCheck = BattleAttackCheckInputRules.LockCritical(attackCheck);

        CombatEffectDefinition reactionDamage = BattleRuntimeEffectDefinitions.Damage(
            candidate.Profile.DamageTag,
            snapshot.TotalDiceCount,
            snapshot.DiceSides,
            0
        );
        var attackContext = new AttackContext
        {
            BattleState = context.BattleState,
            SkillId = candidate.SkillDefinition.SkillId,
            EventBatch = context.Batch,
            DamageOriginKind = BattleDamageOriginKind.MainDirectEffect,
        };
        AttackEffectResolutionResult result = Runtime._damage_resolver.ResolveAttackEffects(
            defender,
            attacker,
            new[] { reactionDamage },
            attackCheck,
            attackContext
        );
        if (result.Applied)
            Runtime._append_changed_unit_id(context.Batch, attacker.unit_id);

        string damageLabel = FormatDamageTag(candidate.Profile.DamageTag);
        string defenseLabel = FormatAttackDefenseMode(
            candidate.Profile.AttackDefenseModeKind
        );
        string resultLabel = result.AttackSuccess
            ? $"命中，造成{Math.Max(result.HpDamage, 0)}点{damageLabel}生命伤害"
            : "未命中";
        context.Batch?.AddLogLine(
            $"{candidate.SkillDefinition.DisplayName}以 {snapshot.TotalDiceCount}D{snapshot.DiceSides} 进行独立{defenseLabel}攻击：{resultLabel}；本次反击{(candidate.Profile.AllowCritical ? "可暴击" : "不能暴击")}。"
        );
    }

    private RangedWeaponReactionCandidate FindRangedWeaponReactionCandidate(
        BattleUnitState defender,
        StringName weaponFamily,
        bool triggeringAttackSucceeded
    )
    {
        foreach (StringName statusId in defender.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = defender.GetStatusEffect(statusId);
            if (
                status == null
                || status.stacks <= 0
                || status.duration == 0
                || status.source_skill_id == ""
                || status.source_unit_id != defender.unit_id
            )
            {
                continue;
            }
            SkillDefinition skillDefinition = Runtime.GetSkillDefinitionTyped(
                status.source_skill_id
            );
            CombatRangedWeaponReactionDefinition profile =
                skillDefinition?.CombatProfile?.RangedWeaponReaction;
            if (
                profile == null
                || profile.ReadinessStatusId != status.status_id
                || !profile.SupportsWeaponFamily(weaponFamily)
                || status.stacks < profile.ConsumeStatusStacks
                || (triggeringAttackSucceeded && !profile.TriggerOnHit)
                || (!triggeringAttackSucceeded && !profile.TriggerOnMiss)
            )
            {
                continue;
            }
            int skillLevel = Math.Max(
                status.source_skill_level
                    ?? defender.GetKnownSkillLevelTyped(skillDefinition.SkillId, fallback: 0),
                0
            );
            return new RangedWeaponReactionCandidate(
                status,
                skillDefinition,
                profile,
                skillLevel
            );
        }
        return null;
    }

    private static void ConsumeReadinessStacks(
        BattleUnitState defender,
        BattleStatusEffectState status,
        int consumeCount
    )
    {
        int remaining = Math.Max(status?.stacks ?? 0, 0) - Math.Max(consumeCount, 1);
        if (remaining > 0)
            status.stacks = remaining;
        else if (status != null)
            defender.EraseStatusEffect(status.status_id);
    }

    private static string FormatWeaponFamily(StringName family) =>
        family == new StringName("bow")
            ? "弓"
            : family == new StringName("crossbow")
                ? "弩"
                : family.ToString();

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

    private sealed record RangedWeaponReactionCandidate(
        BattleStatusEffectState Status,
        SkillDefinition SkillDefinition,
        CombatRangedWeaponReactionDefinition Profile,
        int SkillLevel
    );
}
