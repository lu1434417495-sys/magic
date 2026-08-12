using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct BattleSpellReactionOutcome(
    bool Triggered,
    bool Interrupted,
    int HpDamage,
    int SaveDc,
    BattleSaveResult SaveResult,
    StringName ReactorUnitId,
    StringName ReadiedSkillId
)
{
    internal static BattleSpellReactionOutcome None =>
        new(false, false, 0, 0, BattleSaveResult.Empty("normal"), "", "");
}

internal sealed partial class BattleSkillExecutionOrchestrator
{
    internal BattleSpellReactionOutcome ResolveSpellReactionsAfterCost(
        BattleUnitState caster,
        SkillDefinition castSkill,
        BattleEventBatch batch,
        BattleSaveContext saveContext = default
    )
    {
        BattleState state = RtState();
        if (
            state == null
            || caster?.IsAlive() != true
            || castSkill?.CombatProfile == null
        )
        {
            return BattleSpellReactionOutcome.None;
        }

        IReadOnlyList<StringName> castCategories = BattleEffectCategoryResolver.ResolveCategories(
            castSkill,
            castSkill.CombatProfile.EffectDefinitions
        );
        List<SpellReactionCandidate> candidates = CollectSpellReactionCandidates(
            state,
            caster,
            castCategories
        );
        BattleSpellReactionOutcome lastTriggeredOutcome = BattleSpellReactionOutcome.None;
        foreach (SpellReactionCandidate candidate in candidates)
        {
            BattleSpellReactionOutcome outcome = ResolveSpellReactionCandidate(
                caster,
                candidate,
                batch,
                saveContext
            );
            if (!outcome.Triggered)
                continue;
            lastTriggeredOutcome = outcome;
            if (outcome.Interrupted)
                return outcome;
        }
        return lastTriggeredOutcome;
    }

    internal bool ExpireSpellReactionStatusesAtTurnStart(
        BattleUnitState unitState,
        BattleEventBatch batch
    )
    {
        if (unitState == null)
            return false;
        var expiredStatusIds = new List<StringName>();
        foreach (BattleStatusEffectState statusEntry in unitState.GetStatusEffectsTyped())
        {
            SkillDefinition sourceSkill = Runtime?.GetSkillDefinitionTyped(
                statusEntry?.source_skill_id ?? new StringName("")
            );
            if (sourceSkill?.CombatProfile?.SpellReaction?.ExpireOnOwnerTurnStart == true)
                expiredStatusIds.Add(statusEntry.status_id);
        }
        foreach (StringName statusId in expiredStatusIds)
            unitState.EraseStatusEffect(statusId);
        if (expiredStatusIds.Count == 0)
            return false;
        Runtime?._append_changed_unit_id(batch, unitState.unit_id);
        batch?.AddLogLine($"{ReactionDisplayName(unitState)} 的扰咒待机结束。");
        return true;
    }

    internal static int ComputeSpellReactionSaveDc(
        CombatSpellReactionDefinition profile,
        int hpDamage,
        int skillLevel
    )
    {
        if (profile == null)
            return 0;
        int damageDc = Math.Max(hpDamage, 0) / Math.Max(profile.HpDamageDivisor, 1);
        return Math.Max(profile.BaseSaveDc, damageDc) + profile.GetSaveDcBonus(skillLevel);
    }

    private List<SpellReactionCandidate> CollectSpellReactionCandidates(
        BattleState state,
        BattleUnitState caster,
        IReadOnlyList<StringName> castCategories
    )
    {
        var candidates = new List<SpellReactionCandidate>();
        foreach (BattleState.BattleUnitEntry entry in state.GetUnitEntriesTyped())
        {
            BattleUnitState reactor = entry.Unit;
            if (
                reactor?.IsAlive() != true
                || reactor.unit_id == caster.unit_id
                || reactor.faction_id == ""
                || caster.faction_id == ""
                || reactor.faction_id == caster.faction_id
                || BattleStatusSemanticTable.IsHardControlled(reactor)
                || Runtime?._skill_turn_resolver.HasCounterattackLockStatus(reactor) == true
            )
            {
                continue;
            }
            foreach (BattleStatusEffectState statusEntry in reactor.GetStatusEffectsTyped())
            {
                SkillDefinition readiedSkill = Runtime?.GetSkillDefinitionTyped(
                    statusEntry?.source_skill_id ?? new StringName("")
                );
                CombatSpellReactionDefinition profile =
                    readiedSkill?.CombatProfile?.SpellReaction;
                if (
                    profile == null
                    || statusEntry.status_id != profile.ReadinessStatusId
                    || !ContainsCategory(castCategories, profile.TriggerDeliveryCategory)
                    || (
                        profile.RequiredWeaponFamily != ""
                        && !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                            reactor,
                            new[] { profile.RequiredWeaponFamily }
                        )
                    )
                )
                {
                    continue;
                }
                SkillDefinition reactionSkill = Runtime?.GetSkillDefinitionTyped(
                    profile.ReactionSkillId
                );
                if (
                    reactionSkill?.CombatProfile == null
                    || !_targetValidationService._can_skill_target_unit(
                        reactor,
                        caster,
                        reactionSkill,
                        require_ap: false
                    )
                )
                {
                    continue;
                }
                int skillLevel = Math.Max(
                    statusEntry.source_skill_level
                        ?? reactor.GetKnownSkillLevelTyped(readiedSkill.SkillId, fallback: 1),
                    1
                );
                candidates.Add(
                    new SpellReactionCandidate(
                        reactor,
                        statusEntry.status_id,
                        readiedSkill,
                        reactionSkill,
                        profile,
                        skillLevel
                    )
                );
            }
        }
        candidates.Sort(CompareSpellReactionCandidates);
        return candidates;
    }

    private BattleSpellReactionOutcome ResolveSpellReactionCandidate(
        BattleUnitState caster,
        SpellReactionCandidate candidate,
        BattleEventBatch batch,
        BattleSaveContext saveContext
    )
    {
        if (candidate.Profile.ConsumeOnTrigger)
            candidate.Reactor.EraseStatusEffect(candidate.StatusId);
        Runtime?._append_changed_unit_id(batch, candidate.Reactor.unit_id);
        batch?.AddLogLine(
            $"{ReactionDisplayName(candidate.Reactor)} 的扰咒箭被 {ReactionDisplayName(caster)} 的施法触发。"
        );

        int hpBefore = caster.GetCurrentHp();
        _apply_unit_skill_result(
            candidate.Reactor,
            caster,
            candidate.ReactionSkill,
            null,
            candidate.ReactionSkill.CombatProfile.EffectDefinitions,
            batch,
            flat_attack_bonus: candidate.Profile.GetAttackRollBonus(candidate.SkillLevel)
        );
        int hpDamage = Math.Max(hpBefore - caster.GetCurrentHp(), 0);
        if (!caster.IsAlive())
        {
            batch?.AddLogLine($"{ReactionDisplayName(caster)} 被扰咒箭击倒，法术中断。");
            return new BattleSpellReactionOutcome(
                true,
                true,
                hpDamage,
                0,
                BattleSaveResult.Empty("normal"),
                candidate.Reactor.unit_id,
                candidate.ReadiedSkill.SkillId
            );
        }
        if (candidate.Profile.RequireHpDamage && hpDamage <= 0)
        {
            batch?.AddLogLine($"扰咒箭未造成生命伤害，{ReactionDisplayName(caster)} 的法术继续结算。");
            return new BattleSpellReactionOutcome(
                true,
                false,
                0,
                0,
                BattleSaveResult.Empty("normal"),
                candidate.Reactor.unit_id,
                candidate.ReadiedSkill.SkillId
            );
        }

        int saveDc = ComputeSpellReactionSaveDc(
            candidate.Profile,
            hpDamage,
            candidate.SkillLevel
        );
        CombatEffectDefinition saveEffect = BattleRuntimeEffectDefinitions.StaticSave(
            saveDc,
            candidate.Profile.SaveAbility,
            candidate.Profile.SaveTag
        );
        BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
            candidate.Reactor,
            caster,
            saveEffect,
            saveContext
        );
        if (saveResult.Success)
        {
            batch?.AddLogLine(
                $"{ReactionDisplayName(caster)} 通过扰咒维持检定（{saveResult.RollTotal} 对 DC {saveDc}），法术继续结算。"
            );
        }
        else
        {
            batch?.AddLogLine(
                $"{ReactionDisplayName(caster)} 扰咒维持检定失败（{saveResult.RollTotal} 对 DC {saveDc}），法术中断。"
            );
        }
        return new BattleSpellReactionOutcome(
            true,
            !saveResult.Success,
            hpDamage,
            saveDc,
            saveResult,
            candidate.Reactor.unit_id,
            candidate.ReadiedSkill.SkillId
        );
    }

    private static bool ContainsCategory(
        IReadOnlyList<StringName> categories,
        StringName expected
    )
    {
        if (expected == "")
            return false;
        foreach (StringName category in categories ?? Array.Empty<StringName>())
        {
            if (category == expected)
                return true;
        }
        return false;
    }

    private static string ReactionDisplayName(BattleUnitState unitState) =>
        unitState != null && !string.IsNullOrWhiteSpace(unitState.display_name)
            ? unitState.display_name
            : "未知单位";

    private static int CompareSpellReactionCandidates(
        SpellReactionCandidate left,
        SpellReactionCandidate right
    )
    {
        int progressOrder = right.Reactor.GetActionProgressTyped().CompareTo(
            left.Reactor.GetActionProgressTyped()
        );
        if (progressOrder != 0)
            return progressOrder;
        int unitOrder = StringComparer.Ordinal.Compare(
            left.Reactor.unit_id.ToString(),
            right.Reactor.unit_id.ToString()
        );
        return unitOrder != 0
            ? unitOrder
            : StringComparer.Ordinal.Compare(left.StatusId.ToString(), right.StatusId.ToString());
    }

    private sealed record SpellReactionCandidate(
        BattleUnitState Reactor,
        StringName StatusId,
        SkillDefinition ReadiedSkill,
        SkillDefinition ReactionSkill,
        CombatSpellReactionDefinition Profile,
        int SkillLevel
    );
}
