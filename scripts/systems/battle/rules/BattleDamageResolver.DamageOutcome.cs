using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class BattleDamageResolver
{
    private readonly record struct DamageOutcomeResult(
        DamageEventResult Event,
        bool InvalidDamageTag,
        string ErrorCode,
        string Reason,
        string DamageTagSource,
        StringName DamageTag,
        int ResolvedDamage,
        bool BypassShield,
        bool BypassDeathPrevention,
        double ShieldAbsorptionPercent,
        int MinHpAfterDamage,
        bool LowLuckBlackStarWedgeTriggered,
        DamageDiceEventSnapshot DamageDiceEvent
    )
    {
        public DamageOutcomeResult WithResolvedDamage(int resolvedDamage)
        {
            int normalizedDamage = Math.Max(resolvedDamage, 0);
            DamageEventResult @event = Event;
            @event.ResolvedDamage = normalizedDamage;
            return this with { Event = @event, ResolvedDamage = normalizedDamage };
        }

        public DamageApplicationInput ToDamageApplicationInput(
            bool suppressDamageApplicationHook = false
        )
        {
            return new DamageApplicationInput(
                Event,
                Math.Max(ResolvedDamage, 0),
                BypassShield,
                BypassDeathPrevention,
                ShieldAbsorptionPercent,
                MinHpAfterDamage,
                LowLuckBlackStarWedgeTriggered,
                DamageDiceEvent,
                suppressDamageApplicationHook
            );
        }
    }

    private readonly record struct EquipmentAbilityTaggedBonusDamageRoll(
        StringName DamageTag,
        DicePoolRollResult Roll,
        bool Subtract,
        IReadOnlyList<StringName> MitigationBypassDamageTags,
        IReadOnlyList<StringName> MitigationBypassTiers
    );

    private static DicePoolRollResult FindEquipmentAbilityBonusDamageRoll(
        IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> rolls,
        StringName damageTag,
        bool subtract = false
    )
    {
        foreach (EquipmentAbilityTaggedBonusDamageRoll roll in rolls ?? Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>())
        {
            if (roll.DamageTag == damageTag && roll.Subtract == subtract)
                return roll.Roll;
        }
        return DicePoolRollResult.Empty;
    }

    private static IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> FindExtraEquipmentAbilityBonusDamageRolls(
        IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> rolls,
        StringName primaryDamageTag
    )
    {
        if (rolls == null || rolls.Count == 0)
            return Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>();
        var result = new List<EquipmentAbilityTaggedBonusDamageRoll>();
        foreach (EquipmentAbilityTaggedBonusDamageRoll roll in rolls)
        {
            if (
                roll.DamageTag != ""
                && roll.DamageTag != primaryDamageTag
                && !roll.Subtract
                && !DicePoolRollIsEmpty(roll.Roll)
            )
            {
                result.Add(roll);
            }
        }
        return result.Count == 0 ? Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>() : result;
    }

    private DamageOutcomeResult ResolveDamageOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext
    )
    {
        return ResolveDamageOutcome(
            sourceUnit,
            targetUnit,
            effectDefinition,
            damageContext,
            out _
        );
    }

    private DamageOutcomeResult ResolveDamageOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        out IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> extraEquipmentBonusRolls
    )
    {
        extraEquipmentBonusRolls = Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>();
        StringName damageTag = ResolveDamageTag(sourceUnit, effectDefinition);
        if (damageTag == "")
        {
            return BuildInvalidDamageTagOutcome(sourceUnit, effectDefinition);
        }
        StringName rollMode = damageContext.DamageRollMode;
        IBattleEquipmentAbilityReactionService equipmentAbilityService =
            _equipment_ability_runtime_service;
        if (equipmentAbilityService != null)
        {
            rollMode = equipmentAbilityService.ResolveDamageRollModeOverride(
                new BattleEquipmentAbilityDamageRollModeContext
                {
                    SourceUnit = sourceUnit,
                    TargetUnit = targetUnit,
                    BattleState = equipmentAbilityService.GetBattleState(),
                    CurrentRollMode = rollMode,
                    AttackSucceeded = damageContext.AttackSuccess,
                    CriticalHit = damageContext.CriticalHit,
                }
            );
        }
        DicePoolRollResult damageRoll = RollDamageDice(
            effectDefinition,
            true,
            "damage_dice",
            rollMode
        );
        DicePoolRollResult weaponRoll = RollWeaponDice(
            sourceUnit,
            effectDefinition,
            true,
            "weapon_damage_dice",
            rollMode
        );
        bool criticalHit = damageContext.CriticalHit;
        bool bonusConditionMet = HasBonusCondition(effectDefinition, targetUnit);
        DicePoolRollResult bonusDamageRoll = bonusConditionMet
            ? RollBonusDamageDice(effectDefinition, true, "bonus_damage_dice", rollMode)
            : DicePoolRollResult.Empty;
        SourceBoundWeaponBonusDamageRoll sourceBoundWeaponBonusRoll =
            RollSourceBoundWeaponBonusDamageDice(
                sourceUnit,
                targetUnit,
                !DicePoolRollIsEmpty(weaponRoll),
                rollMode
            );
        bonusDamageRoll = CombineDicePoolRolls(
            bonusDamageRoll,
            sourceBoundWeaponBonusRoll.Roll
        );
        IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> equipmentBonusRolls =
            RollEquipmentAbilityBonusDamageDiceByTag(
                sourceUnit,
                targetUnit,
                damageContext,
                damageTag,
                !DicePoolRollIsEmpty(weaponRoll),
                rollMode
            );
        bonusDamageRoll = CombineDicePoolRolls(
            bonusDamageRoll,
            FindEquipmentAbilityBonusDamageRoll(equipmentBonusRolls, damageTag)
        );
        DicePoolRollResult equipmentDamagePenaltyRoll =
            FindEquipmentAbilityBonusDamageRoll(
                equipmentBonusRolls,
                damageTag,
                subtract: true
            );
        extraEquipmentBonusRolls = FindExtraEquipmentAbilityBonusDamageRolls(
            equipmentBonusRolls,
            damageTag
        );
        DicePoolRollResult criticalDamageRoll =
            criticalHit && damageRoll.HasDice
                ? RollDamageDice(
                    effectDefinition,
                    false,
                    "critical_extra_damage_dice",
                    rollMode
                )
                : DicePoolRollResult.Empty;
        DicePoolRollResult criticalWeaponRoll =
            criticalHit && weaponRoll.HasDice
                ? RollWeaponDice(
                    sourceUnit,
                    effectDefinition,
                    false,
                    "critical_extra_weapon_damage_dice",
                    rollMode
                )
                : DicePoolRollResult.Empty;
        DicePoolRollResult criticalBonusDamageRoll =
            criticalHit && bonusDamageRoll.HasDice
                ? RollBonusDamageDice(
                    effectDefinition,
                    false,
                    "critical_extra_bonus_damage_dice",
                    rollMode
                )
                : DicePoolRollResult.Empty;
        TraitTriggerResultSnapshot traitCritResult = ResolveCritTraitResult(
            sourceUnit,
            targetUnit,
            effectDefinition,
            criticalHit
        );
        DicePoolRollResult traitExtraWeaponRoll = traitCritResult.Triggered
            ? RollDicePool(
                traitCritResult.ExtraWeaponDiceCount,
                traitCritResult.ExtraWeaponDiceSides,
                0,
                "trait_extra_weapon_damage_dice",
                rollMode
            )
            : DicePoolRollResult.Empty;
        DicePoolRollResult consumedStackRoll = RollConsumedStackDice(
            sourceUnit,
            effectDefinition,
            rollMode
        );

        int baseDamage =
            Math.Max(effectDefinition?.Power ?? 0, 0)
            + weaponRoll.TotalWithBonus
            + damageRoll.TotalWithBonus
            + bonusDamageRoll.TotalWithBonus
            + criticalWeaponRoll.Total
            + criticalDamageRoll.Total
            + criticalBonusDamageRoll.Total
            + traitExtraWeaponRoll.Total
            + consumedStackRoll.Total
            - equipmentDamagePenaltyRoll.TotalWithBonus;
        double offenseMultiplier = BuildOffenseMultiplier(
            sourceUnit,
            targetUnit,
            effectDefinition
        );
        int rolledDamage = Math.Max(RoundToInt(baseDamage * offenseMultiplier), 0);
        MitigationTierResolution mitigationTierResult = ResolveMitigationTierResult(
            targetUnit,
            damageTag,
            effectDefinition?.MitigationBypassDamageTags,
            effectDefinition?.MitigationBypassTiers
        );
        StringName mitigationTier = mitigationTierResult.Tier;
        int tierAdjustedDamage = rolledDamage;
        if (mitigationTier == MitigationTierImmune)
        {
            tierAdjustedDamage = 0;
        }
        else if (mitigationTier == MitigationTierHalf)
        {
            tierAdjustedDamage /= 2;
        }
        else if (mitigationTier == MitigationTierDouble)
        {
            tierAdjustedDamage *= 2;
        }

        FixedMitigationResult mitigation = BuildFixedMitigation(
            sourceUnit,
            targetUnit,
            effectDefinition,
            damageTag,
            damageContext
        );
        ApplyBlackStarBrandGuardIgnore(mitigation, targetUnit);
        bool lowLuckBlackStarWedgeTriggered = ApplyLowLuckBlackStarWedgeGuardIgnore(
            mitigation,
            sourceUnit
        );
        TrimFixedMitigationSources(mitigation);
        int buffReduction = mitigation.BuffReduction;
        int stanceReduction = mitigation.StanceReduction;
        int passiveReduction = mitigation.PassiveReduction;
        int contentDr = mitigation.ContentDr;
        int guardBlock = mitigation.GuardBlock;
        int guardIgnoreApplied = mitigation.GuardIgnoreApplied;
        int fixedMitigationTotal = mitigation.Total;
        int resolvedDamage = Math.Max(tierAdjustedDamage - fixedMitigationTotal, MinDamageFloor);
        DamageDiceEventFlags damageDiceEventFlags = BuildDamageDiceEventFlags(
            criticalHit,
            damageRoll,
            weaponRoll,
            bonusDamageRoll
        );

        DamageDiceEventSnapshot diceSnapshot = damageDiceEventFlags.Snapshot;
        DamageEventResult result = new()
        {
            DamageTag = damageTag,
            MitigationTier = AttackEffectResolutionResultReader.ParseMitigationTier(
                mitigationTier
            ),
            MitigationSources = mitigationTierResult.Sources,
            BaseDamage = baseDamage,
            CriticalHit = criticalHit,
            AddWeaponDice = ShouldAddWeaponDice(effectDefinition),
            DamageDice = damageRoll.ToDamageDiceRollDetail(),
            BonusConditionMet = bonusConditionMet,
            BonusDamageDice = bonusDamageRoll.ToDamageDiceRollDetail(),
            WeaponDamageDice = weaponRoll.ToDamageDiceRollDetail(),
            CriticalExtraDamageDice = criticalDamageRoll.ToDamageDiceRollDetail(),
            CriticalExtraBonusDamageDice = criticalBonusDamageRoll.ToDamageDiceRollDetail(),
            CriticalExtraWeaponDamageDice = criticalWeaponRoll.ToDamageDiceRollDetail(),
            TraitExtraWeaponDamageDice = traitExtraWeaponRoll.ToDamageDiceRollDetail(),
            SourceBoundWeaponBonusSkillIds =
                sourceBoundWeaponBonusRoll.SkillIds != null
                    ? new List<StringName>(sourceBoundWeaponBonusRoll.SkillIds).ToArray()
                    : Array.Empty<StringName>(),
            OffenseMultiplier = offenseMultiplier,
            RolledDamage = rolledDamage,
            TierAdjustedDamage = tierAdjustedDamage,
            ResolvedDamage = resolvedDamage,
            BuffReduction = buffReduction,
            StanceReduction = stanceReduction,
            PassiveReduction = passiveReduction,
            ContentDr = contentDr,
            GuardBlock = guardBlock,
            GuardIgnoreApplied = guardIgnoreApplied,
            FixedMitigationSourceLabels =
                mitigation.SourceLabels(),
            LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered,
            FixedMitigationTotal = fixedMitigationTotal,
            FullyAbsorbedByMitigation =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            TraitTriggerResults = traitCritResult.Triggered
                ? new[] { traitCritResult.ToEventResult() }
                : Array.Empty<TraitTriggerEventResult>(),
            DamageDiceHighTotalRoll = diceSnapshot.DamageDiceHighTotalRoll,
            DamageDiceHighTotalRollReason = diceSnapshot.DamageDiceHighTotalRollReason,
            SkillDamageDiceIsMax = diceSnapshot.SkillDamageDiceIsMax,
            SkillDamageDiceIsMaxReason = diceSnapshot.SkillDamageDiceIsMaxReason,
            WeaponDamageDiceIsMax = diceSnapshot.WeaponDamageDiceIsMax,
            WeaponDamageDiceIsMaxReason = diceSnapshot.WeaponDamageDiceIsMaxReason,
        };
        return new DamageOutcomeResult(
            result,
            false,
            "",
            "",
            "",
            damageTag,
            resolvedDamage,
            false,
            false,
            100.0,
            0,
            lowLuckBlackStarWedgeTriggered,
            damageDiceEventFlags.Snapshot
        );
    }

    private DamageOutcomeResult ResolveExtraDamageSegmentOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        CombatDamageSegmentDefinition segment,
        DamageResolutionContext damageContext
    )
    {
        if (segment == null)
        {
            return BuildInvalidDamageTagOutcomeFromTag("");
        }
        StringName damageTag = ProgressionDataUtils.to_string_name(segment.DamageTag);
        if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
        {
            return BuildInvalidDamageTagOutcomeFromTag(damageTag);
        }

        StringName rollMode = (damageContext ?? DamageResolutionContext.Empty()).DamageRollMode;
        IBattleEquipmentAbilityReactionService equipmentAbilityService =
            _equipment_ability_runtime_service;
        if (equipmentAbilityService != null)
        {
            rollMode = equipmentAbilityService.ResolveDamageRollModeOverride(
                new BattleEquipmentAbilityDamageRollModeContext
                {
                    SourceUnit = sourceUnit,
                    TargetUnit = targetUnit,
                    BattleState = equipmentAbilityService.GetBattleState(),
                    CurrentRollMode = rollMode,
                    AttackSucceeded = damageContext.AttackSuccess,
                    CriticalHit = false,
                }
            );
        }

        DicePoolRollResult damageRoll = RollDicePool(
            Math.Max(segment.DiceCount, 0),
            Math.Max(segment.DiceSides, 0),
            segment.DiceBonus,
            "extra_damage_segment_dice",
            rollMode
        );
        int baseDamage = Math.Max(segment.Power, 0) + damageRoll.TotalWithBonus;
        double offenseMultiplier =
            BuildOffenseMultiplier(sourceUnit, targetUnit, effectDefinition)
            * Math.Max(segment.PreResistanceDamageMultiplier, 0.0);
        int rolledDamage = Math.Max(RoundToInt(baseDamage * offenseMultiplier), 0);
        IReadOnlyList<StringName> mitigationBypassDamageTags =
            segment.MitigationBypassDamageTags != null
            && segment.MitigationBypassDamageTags.Count > 0
                ? segment.MitigationBypassDamageTags
                : effectDefinition?.MitigationBypassDamageTags;
        IReadOnlyList<StringName> mitigationBypassTiers =
            segment.MitigationBypassTiers != null
            && segment.MitigationBypassTiers.Count > 0
                ? segment.MitigationBypassTiers
                : effectDefinition?.MitigationBypassTiers;
        MitigationTierResolution mitigationTierResult = ResolveMitigationTierResult(
            targetUnit,
            damageTag,
            mitigationBypassDamageTags,
            mitigationBypassTiers
        );
        StringName mitigationTier = mitigationTierResult.Tier;
        int tierAdjustedDamage = rolledDamage;
        if (mitigationTier == MitigationTierImmune)
        {
            tierAdjustedDamage = 0;
        }
        else if (mitigationTier == MitigationTierHalf)
        {
            tierAdjustedDamage /= 2;
        }
        else if (mitigationTier == MitigationTierDouble)
        {
            tierAdjustedDamage *= 2;
        }

        FixedMitigationResult mitigation = BuildFixedMitigation(
            sourceUnit,
            targetUnit,
            effectDefinition,
            damageTag,
            damageContext
        );
        ApplyBlackStarBrandGuardIgnore(mitigation, targetUnit);
        bool lowLuckBlackStarWedgeTriggered = ApplyLowLuckBlackStarWedgeGuardIgnore(
            mitigation,
            sourceUnit
        );
        TrimFixedMitigationSources(mitigation);
        int resolvedDamage = Math.Max(tierAdjustedDamage - mitigation.Total, MinDamageFloor);
        DamageDiceEventFlags damageDiceEventFlags = BuildDamageDiceEventFlags(
            false,
            damageRoll,
            DicePoolRollResult.Empty
        );
        DamageDiceEventSnapshot diceSnapshot = damageDiceEventFlags.Snapshot;
        DamageEventResult result = new()
        {
            DamageTag = damageTag,
            MitigationTier = AttackEffectResolutionResultReader.ParseMitigationTier(
                mitigationTier
            ),
            MitigationSources = mitigationTierResult.Sources,
            BaseDamage = baseDamage,
            CriticalHit = false,
            AddWeaponDice = false,
            DamageDice = damageRoll.ToDamageDiceRollDetail(),
            BonusConditionMet = false,
            BonusDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            WeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraBonusDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraWeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            TraitExtraWeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            OffenseMultiplier = offenseMultiplier,
            RolledDamage = rolledDamage,
            TierAdjustedDamage = tierAdjustedDamage,
            ResolvedDamage = resolvedDamage,
            BuffReduction = mitigation.BuffReduction,
            StanceReduction = mitigation.StanceReduction,
            PassiveReduction = mitigation.PassiveReduction,
            ContentDr = mitigation.ContentDr,
            GuardBlock = mitigation.GuardBlock,
            GuardIgnoreApplied = mitigation.GuardIgnoreApplied,
            FixedMitigationSourceLabels = mitigation.SourceLabels(),
            LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered,
            FixedMitigationTotal = mitigation.Total,
            FullyAbsorbedByMitigation =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            TraitTriggerResults = Array.Empty<TraitTriggerEventResult>(),
            DamageDiceHighTotalRoll = diceSnapshot.DamageDiceHighTotalRoll,
            DamageDiceHighTotalRollReason = diceSnapshot.DamageDiceHighTotalRollReason,
            SkillDamageDiceIsMax = diceSnapshot.SkillDamageDiceIsMax,
            SkillDamageDiceIsMaxReason = diceSnapshot.SkillDamageDiceIsMaxReason,
            WeaponDamageDiceIsMax = diceSnapshot.WeaponDamageDiceIsMax,
            WeaponDamageDiceIsMaxReason = diceSnapshot.WeaponDamageDiceIsMaxReason,
        };
        return new DamageOutcomeResult(
            result,
            false,
            "",
            "",
            "",
            damageTag,
            resolvedDamage,
            false,
            false,
            100.0,
            0,
            lowLuckBlackStarWedgeTriggered,
            damageDiceEventFlags.Snapshot
        );
    }

    private DamageOutcomeResult ResolveEquipmentAbilityBonusDamageOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        EquipmentAbilityTaggedBonusDamageRoll taggedRoll
    )
    {
        StringName damageTag = ProgressionDataUtils.to_string_name(taggedRoll.DamageTag);
        if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
        {
            return BuildInvalidDamageTagOutcomeFromTag(damageTag);
        }

        DicePoolRollResult bonusDamageRoll = taggedRoll.Roll;
        bool criticalHit = damageContext?.CriticalHit == true;
        int baseDamage = bonusDamageRoll.TotalWithBonus;
        double offenseMultiplier = BuildOffenseMultiplier(
            sourceUnit,
            targetUnit,
            effectDefinition
        );
        int rolledDamage = Math.Max(RoundToInt(baseDamage * offenseMultiplier), 0);
        MitigationTierResolution mitigationTierResult = ResolveMitigationTierResult(
            targetUnit,
            damageTag,
            taggedRoll.MitigationBypassDamageTags,
            taggedRoll.MitigationBypassTiers
        );
        StringName mitigationTier = mitigationTierResult.Tier;
        int tierAdjustedDamage = rolledDamage;
        if (mitigationTier == MitigationTierImmune)
        {
            tierAdjustedDamage = 0;
        }
        else if (mitigationTier == MitigationTierHalf)
        {
            tierAdjustedDamage /= 2;
        }
        else if (mitigationTier == MitigationTierDouble)
        {
            tierAdjustedDamage *= 2;
        }

        FixedMitigationResult mitigation = BuildFixedMitigation(
            sourceUnit,
            targetUnit,
            effectDefinition,
            damageTag,
            damageContext
        );
        ApplyBlackStarBrandGuardIgnore(mitigation, targetUnit);
        bool lowLuckBlackStarWedgeTriggered = ApplyLowLuckBlackStarWedgeGuardIgnore(
            mitigation,
            sourceUnit
        );
        TrimFixedMitigationSources(mitigation);
        int fixedMitigationTotal = mitigation.Total;
        int resolvedDamage = Math.Max(tierAdjustedDamage - fixedMitigationTotal, MinDamageFloor);
        DamageDiceEventFlags damageDiceEventFlags = BuildDamageDiceEventFlags(
            criticalHit,
            DicePoolRollResult.Empty,
            DicePoolRollResult.Empty,
            bonusDamageRoll
        );
        DamageDiceEventSnapshot diceSnapshot = damageDiceEventFlags.Snapshot;
        DamageEventResult result = new()
        {
            DamageTag = damageTag,
            MitigationTier = AttackEffectResolutionResultReader.ParseMitigationTier(
                mitigationTier
            ),
            MitigationSources = mitigationTierResult.Sources,
            BaseDamage = baseDamage,
            CriticalHit = criticalHit,
            AddWeaponDice = false,
            DamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            BonusConditionMet = false,
            BonusDamageDice = bonusDamageRoll.ToDamageDiceRollDetail(),
            WeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraBonusDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraWeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            TraitExtraWeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            OffenseMultiplier = offenseMultiplier,
            RolledDamage = rolledDamage,
            TierAdjustedDamage = tierAdjustedDamage,
            ResolvedDamage = resolvedDamage,
            BuffReduction = mitigation.BuffReduction,
            StanceReduction = mitigation.StanceReduction,
            PassiveReduction = mitigation.PassiveReduction,
            ContentDr = mitigation.ContentDr,
            GuardBlock = mitigation.GuardBlock,
            GuardIgnoreApplied = mitigation.GuardIgnoreApplied,
            FixedMitigationSourceLabels =
                mitigation.SourceLabels(),
            LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered,
            FixedMitigationTotal = fixedMitigationTotal,
            FullyAbsorbedByMitigation =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            TraitTriggerResults = Array.Empty<TraitTriggerEventResult>(),
            DamageDiceHighTotalRoll = diceSnapshot.DamageDiceHighTotalRoll,
            DamageDiceHighTotalRollReason = diceSnapshot.DamageDiceHighTotalRollReason,
            SkillDamageDiceIsMax = diceSnapshot.SkillDamageDiceIsMax,
            SkillDamageDiceIsMaxReason = diceSnapshot.SkillDamageDiceIsMaxReason,
            WeaponDamageDiceIsMax = diceSnapshot.WeaponDamageDiceIsMax,
            WeaponDamageDiceIsMaxReason = diceSnapshot.WeaponDamageDiceIsMaxReason,
        };
        return new DamageOutcomeResult(
            result,
            false,
            "",
            "",
            "equipment_ability_bonus_damage",
            damageTag,
            resolvedDamage,
            false,
            false,
            100.0,
            0,
            lowLuckBlackStarWedgeTriggered,
            damageDiceEventFlags.Snapshot
        );
    }
}
