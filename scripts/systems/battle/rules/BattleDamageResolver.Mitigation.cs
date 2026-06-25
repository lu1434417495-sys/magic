using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：减免/抗性/护盾格挡/DR 与命中加成条件。按伤害管线阶段拆出，不改逻辑。
public partial class BattleDamageResolver
{
    private readonly record struct FixedMitigationComponent(
        int Value,
        IReadOnlyList<MitigationSourceResult> Sources
    );

    private GDictionary ResolveMitigationTierResult(
        BattleUnitState targetUnit,
        StringName damageTag
    )
    {
        if (targetUnit == null)
        {
            return MitigationPayload(
                new GDictionary
                {
                    ["tier"] = MitigationTierNormal,
                    ["sources"] = MitigationArray("mitigation.null_target.sources"),
                },
                "mitigation.null_target"
            );
        }
        var halfSources = MitigationArray("mitigation.half_sources");
        var doubleSources = MitigationArray("mitigation.double_sources");
        var immuneSources = MitigationArray("mitigation.immune_sources");
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (!StatusAppliesToDamageTag(statusEntry, damageTag))
            {
                continue;
            }
            StringName mitigationTier = statusEntry.mitigation_tier;
            if (mitigationTier == MitigationTierImmune)
            {
                immuneSources.Add(
                    BuildMitigationSource(statusId, "mitigation_tier", 0, mitigationTier)
                );
            }
            else if (mitigationTier == MitigationTierHalf)
            {
                halfSources.Add(
                    BuildMitigationSource(statusId, "mitigation_tier", 0, mitigationTier)
                );
            }
            else if (mitigationTier == MitigationTierDouble)
            {
                doubleSources.Add(
                    BuildMitigationSource(statusId, "mitigation_tier", 0, mitigationTier)
                );
            }
        }
        AppendDamageResistanceSources(
            targetUnit,
            damageTag,
            halfSources,
            doubleSources,
            immuneSources
        );
        if (immuneSources.Count > 0)
            return MitigationPayload(
                new GDictionary { ["tier"] = MitigationTierImmune, ["sources"] = immuneSources },
                "mitigation.immune"
            );
        if (halfSources.Count > 0 && doubleSources.Count > 0)
        {
            var cancelled = MitigationArray("mitigation.cancelled_sources");
            cancelled.AddRange(halfSources);
            cancelled.AddRange(doubleSources);
            return MitigationPayload(
                new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = cancelled },
                "mitigation.cancelled"
            );
        }
        if (halfSources.Count > 0)
            return MitigationPayload(
                new GDictionary { ["tier"] = MitigationTierHalf, ["sources"] = halfSources },
                "mitigation.half"
            );
        if (doubleSources.Count > 0)
            return MitigationPayload(
                new GDictionary { ["tier"] = MitigationTierDouble, ["sources"] = doubleSources },
                "mitigation.double"
            );
        return MitigationPayload(
            new GDictionary
            {
                ["tier"] = MitigationTierNormal,
                ["sources"] = MitigationArray("mitigation.normal.sources"),
            },
            "mitigation.normal"
        );
    }

    private static GArray MitigationArray(string reason)
    {
        var result = new GArray();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    private static GDictionary MitigationPayload(GDictionary payload, string reason)
    {
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(payload, reason);
        return payload;
    }

    private static void AppendDamageResistanceSources(
        BattleUnitState targetUnit,
        StringName damageTag,
        GArray halfSources,
        GArray doubleSources,
        GArray immuneSources
    )
    {
        if (targetUnit == null || damageTag == "")
        {
            return;
        }
        foreach (var rawDamageTag in targetUnit.damage_resistances.Keys)
        {
            StringName resistanceDamageTag = ProgressionDataUtils.to_string_name(rawDamageTag);
            if (resistanceDamageTag != damageTag)
            {
                continue;
            }
            StringName mitigationTier = ProgressionDataUtils.to_string_name(
                targetUnit.damage_resistances[rawDamageTag]
            );
            StringName sourceId = new($"damage_resistance_{resistanceDamageTag}");
            if (mitigationTier == MitigationTierImmune)
                immuneSources.Add(
                    BuildMitigationSource(sourceId, "damage_resistance", 0, mitigationTier)
                );
            else if (mitigationTier == MitigationTierHalf)
                halfSources.Add(
                    BuildMitigationSource(sourceId, "damage_resistance", 0, mitigationTier)
                );
            else if (mitigationTier == MitigationTierDouble)
                doubleSources.Add(
                    BuildMitigationSource(sourceId, "damage_resistance", 0, mitigationTier)
                );
        }
    }

    private bool StatusAppliesToDamageTag(
        BattleStatusEffectState statusEntry,
        StringName damageTag
    )
    {
        if (statusEntry == null || damageTag == "")
        {
            return true;
        }
        StringName explicitDamageTag = statusEntry.damage_tag;
        if (explicitDamageTag != "")
        {
            return explicitDamageTag == damageTag;
        }
        if (statusEntry.damage_tags.Count > 0)
        {
            foreach (StringName tagValue in statusEntry.damage_tags)
            {
                if (tagValue == damageTag)
                {
                    return true;
                }
            }
            return false;
        }
        StringName damageCategory = statusEntry.damage_category;
        if (damageCategory == "physical")
        {
            return IsPhysicalDamageTag(damageTag);
        }
        if (damageCategory == "spell" || damageCategory == "magic" || damageCategory == "energy")
        {
            return !IsPhysicalDamageTag(damageTag);
        }
        return true;
    }

    private static bool IsPhysicalDamageTag(StringName damageTag)
    {
        return DamageTagContentRules.IsPhysicalDamageTag(
            DamageTagContentRules.ToDamageTagKind(damageTag)
        );
    }

    private FixedMitigationResult BuildFixedMitigation(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        StringName damageTag
    )
    {
        FixedMitigationComponent buffReduction = ResolveBuffReductionResult(targetUnit);
        FixedMitigationComponent stanceReduction = ResolveStanceReductionResult(targetUnit, damageTag);
        FixedMitigationComponent passiveReduction = ResolvePassiveReductionResult(targetUnit);
        FixedMitigationComponent contentDr = ResolveContentDrResult(targetUnit, effectDef, damageTag);
        FixedMitigationComponent guardBlock = ResolveGuardBlockResult(targetUnit, damageTag);
        var result = new FixedMitigationResult
        {
            BuffReduction = buffReduction.Value,
            StanceReduction = stanceReduction.Value,
            PassiveReduction = passiveReduction.Value,
            ContentDr = contentDr.Value,
            GuardBlock = guardBlock.Value,
        };
        result.Sources.AddRange(buffReduction.Sources);
        result.Sources.AddRange(stanceReduction.Sources);
        result.Sources.AddRange(passiveReduction.Sources);
        result.Sources.AddRange(contentDr.Sources);
        result.Sources.AddRange(guardBlock.Sources);
        return result;
    }

    private FixedMitigationComponent ResolveBuffReductionResult(BattleUnitState targetUnit)
    {
        if (!HasStatusEffect(targetUnit, StatusDamageReductionUp))
        {
            return ZeroSourceResult();
        }
        int strength = GetStatusStrength(targetUnit, StatusDamageReductionUp);
        int value = Math.Max(strength, 0) * DamageReductionUpFixedPerPower;
        return new FixedMitigationComponent(
            value,
            new[] { BuildFixedMitigationSource(StatusDamageReductionUp, "buff_reduction", value) }
        );
    }

    private FixedMitigationComponent ResolveStanceReductionResult(
        BattleUnitState targetUnit,
        StringName damageTag
    )
    {
        if (!IsPhysicalDamageTag(damageTag) || !HasStatusEffect(targetUnit, StatusGuarding))
        {
            return ZeroSourceResult();
        }
        int value = Math.Max(GetStatusStrength(targetUnit, StatusGuarding), 0);
        return new FixedMitigationComponent(
            value,
            new[] { BuildFixedMitigationSource(StatusGuarding, "stance_reduction", value) }
        );
    }

    private FixedMitigationComponent ResolvePassiveReductionResult(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return ZeroSourceResult();
        }
        int maxPassiveReduction = 0;
        var sources = new List<MitigationSourceResult>();
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            int passiveReduction = Math.Max(statusEntry.passive_reduction, 0);
            if (passiveReduction <= 0)
            {
                continue;
            }
            if (passiveReduction > maxPassiveReduction)
            {
                maxPassiveReduction = passiveReduction;
                sources.Clear();
                sources.Add(BuildFixedMitigationSource(statusId, "passive_reduction", passiveReduction));
            }
            else if (passiveReduction == maxPassiveReduction)
            {
                sources.Add(BuildFixedMitigationSource(statusId, "passive_reduction", passiveReduction));
            }
        }
        return new FixedMitigationComponent(maxPassiveReduction, sources);
    }

    private FixedMitigationComponent ResolveContentDrResult(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        StringName damageTag
    )
    {
        if (targetUnit == null || !IsPhysicalDamageTag(damageTag))
        {
            return ZeroSourceResult();
        }
        int maxContentDr = 0;
        var sources = new List<MitigationSourceResult>();
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (!StatusAppliesToDamageTag(statusEntry, damageTag))
            {
                continue;
            }
            int contentDr = Math.Max(statusEntry.content_dr, 0);
            if (contentDr <= 0)
            {
                continue;
            }
            StringName bypassTag = statusEntry.dr_bypass_tag;
            if (bypassTag != "" && EffectHasBypassTag(effectDef, bypassTag))
            {
                continue;
            }
            if (contentDr > maxContentDr)
            {
                maxContentDr = contentDr;
                sources.Clear();
                sources.Add(BuildFixedMitigationSource(statusId, "content_dr", contentDr));
            }
            else if (contentDr == maxContentDr)
            {
                sources.Add(BuildFixedMitigationSource(statusId, "content_dr", contentDr));
            }
        }
        return new FixedMitigationComponent(maxContentDr, sources);
    }

    private FixedMitigationComponent ResolveGuardBlockResult(BattleUnitState targetUnit, StringName damageTag)
    {
        if (targetUnit == null)
        {
            return ZeroSourceResult();
        }
        int maxGuardBlock = 0;
        var sources = new List<MitigationSourceResult>();
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (!StatusAppliesToDamageTag(statusEntry, damageTag))
            {
                continue;
            }
            int guardBlock = Math.Max(statusEntry.guard_block, 0);
            if (guardBlock <= 0)
            {
                continue;
            }
            if (guardBlock > maxGuardBlock)
            {
                maxGuardBlock = guardBlock;
                sources.Clear();
                sources.Add(BuildFixedMitigationSource(statusId, "guard_block", guardBlock));
            }
            else if (guardBlock == maxGuardBlock)
            {
                sources.Add(BuildFixedMitigationSource(statusId, "guard_block", guardBlock));
            }
        }
        return new FixedMitigationComponent(maxGuardBlock, sources);
    }

    private static FixedMitigationComponent ZeroSourceResult()
    {
        return new FixedMitigationComponent(0, Array.Empty<MitigationSourceResult>());
    }

    private static MitigationSourceResult BuildFixedMitigationSource(
        StringName statusId,
        string sourceType,
        int value
    )
    {
        return new MitigationSourceResult
        {
            StatusId = statusId.ToString(),
            Type = sourceType,
            Value = value,
            Tier = MitigationTierKind.None,
        };
    }

    private static GDictionary BuildMitigationSource(
        StringName statusId,
        string sourceType,
        int value = 0,
        StringName tier = default
    )
    {
        return MitigationPayload(
            new GDictionary
            {
                ["status_id"] = statusId.ToString(),
                ["type"] = sourceType,
                ["value"] = value,
                ["tier"] = (tier == default ? new StringName("") : tier).ToString(),
            },
            "mitigation.source"
        );
    }

    private void ApplyBlackStarBrandGuardIgnore(
        FixedMitigationResult mitigation,
        BattleUnitState targetUnit
    )
    {
        if (
            mitigation == null
            || targetUnit == null
            || !targetUnit.HasStatusEffect(StatusBlackStarBrandEliteGuardWindow)
        )
        {
            return;
        }
        mitigation.ApplyGuardIgnore(BlackStarBrandGuardIgnoreFlat);
        targetUnit.EraseStatusEffect(StatusBlackStarBrandEliteGuardWindow);
    }

    private bool ApplyLowLuckBlackStarWedgeGuardIgnore(
        FixedMitigationResult mitigation,
        BattleUnitState sourceUnit
    )
    {
        if (mitigation == null || sourceUnit == null)
        {
            return false;
        }
        if (!LowLuckRelicRules.UnitHasFlag(sourceUnit, LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BlackStarWedge)))
        {
            return false;
        }
        BattleAiBlackboard aiBlackboard = sourceUnit.ai_blackboard;
        if (aiBlackboard == null || aiBlackboard.low_luck_black_star_wedge_used)
        {
            return false;
        }
        aiBlackboard.low_luck_black_star_wedge_used = true;
        mitigation.ApplyGuardIgnore(LowLuckRelicRules.BlackStarWedgeGuardIgnoreFlat);
        mitigation.LowLuckBlackStarWedgeTriggered = true;
        return true;
    }

    private static void TrimFixedMitigationSources(FixedMitigationResult mitigation)
    {
        if (mitigation == null)
        {
            return;
        }
        mitigation.TrimSources();
    }

    private static bool EffectHasBypassTag(CombatEffectDef effectDef, StringName bypassTag)
    {
        return effectDef != null
            && bypassTag != ""
            && ProgressionDataUtils.to_string_name(effectDef.dr_bypass_tag) == bypassTag;
    }

    private bool HasBonusCondition(CombatEffectDef effectDef, BattleUnitState targetUnit)
    {
        if (effectDef == null || targetUnit == null)
        {
            return false;
        }
        if (effectDef.bonus_condition == BonusConditionTargetLowHp)
        {
            return IsTargetLowHp(effectDef, targetUnit);
        }
        if (effectDef.bonus_condition == BonusConditionTargetDebuffCount)
        {
            return TargetHasEnoughDebuffs(effectDef, targetUnit);
        }
        return false;
    }

    private static bool IsTargetLowHp(CombatEffectDef effectDef, BattleUnitState targetUnit)
    {
        int maxHp = GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.HpMax));
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.current_hp, 1);
        }
        int thresholdPercent =
            effectDef != null && effectDef.hp_ratio_threshold_percent > 0
                ? Math.Clamp(effectDef.hp_ratio_threshold_percent, 0, 100)
                : 50;
        return targetUnit.current_hp * 100 <= maxHp * thresholdPercent;
    }

    private static bool TargetHasEnoughDebuffs(
        CombatEffectDef effectDef,
        BattleUnitState targetUnit
    )
    {
        if (targetUnit == null)
        {
            return false;
        }
        int threshold = Math.Max(effectDef?.debuff_count_threshold ?? 3, 1);
        int count = 0;
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            if (BattleStatusSemanticTable.IsHarmfulStatus(statusId))
            {
                count += 1;
                if (count >= threshold)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static double GetDamageRatioMultiplier(CombatEffectDef effectDef)
    {
        return effectDef == null ? 1.0 : Math.Max(effectDef.damage_ratio_percent / 100.0, 0.0);
    }

    private static double GetPreResistanceDamageMultiplier(CombatEffectDef effectDef)
    {
        return effectDef == null
            ? 1.0
            : Math.Max(effectDef.pre_resistance_damage_multiplier, 0.0);
    }

    private static bool ShouldAddWeaponDice(CombatEffectDef effectDef)
    {
        return DamageEffectRuntimeParameters.FromEffect(effectDef).AddWeaponDice;
    }

    private static WeaponDice GetCurrentWeaponDamageDice(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return new WeaponDice();
        }
        return unitState.GetActiveWeaponDiceTyped();
    }

    private static int GetCurrentWeaponDamageDiceSides(BattleUnitState unitState)
    {
        WeaponDice dice = GetCurrentWeaponDamageDice(unitState);
        return dice == null ? 0 : Math.Max(dice.dice_sides, 0);
    }

    private DamageOutcomeResult BuildInvalidDamageTagOutcome(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        StringName sourceLabel = "effect.damage_tag";
        StringName configuredTag;
        if (ShouldUseWeaponPhysicalDamageTag(effectDef))
        {
            sourceLabel = "weapon_physical_damage_tag";
            configuredTag = ProgressionDataUtils.to_string_name(
                sourceUnit != null
                    ? sourceUnit.weapon_physical_damage_tag
                    : Variant.From(new StringName(""))
            );
        }
        else
        {
            configuredTag = ProgressionDataUtils.to_string_name(
                effectDef != null
                    ? effectDef.damage_tag
                    : Variant.From(new StringName(""))
            );
        }
        StringName reason = configuredTag == "" ? "missing_damage_tag" : "unsupported_damage_tag";
        DamageEventResult @event = new()
        {
            DamageTag = configuredTag,
            MitigationTier = MitigationTierKind.Normal,
            MitigationSources = Array.Empty<MitigationSourceResult>(),
            BaseDamage = 0,
            RolledDamage = 0,
            TierAdjustedDamage = 0,
            ResolvedDamage = 0,
            FixedMitigationSourceLabels = Array.Empty<string>(),
            FixedMitigationTotal = 0,
            FullyAbsorbedByMitigation = false,
        };
        return new DamageOutcomeResult(
            @event,
            true,
            "invalid_damage_tag",
            reason.ToString(),
            sourceLabel.ToString(),
            configuredTag,
            0,
            false,
            false,
            100.0,
            0,
            false,
            DamageDiceEventSnapshot.Empty
        );
    }

    private static GDictionary BuildInvalidDamageTagDiagnostic(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        DamageOutcomeResult damageOutcome
    )
    {
        return new GDictionary
        {
            ["error_code"] = "invalid_damage_tag",
            ["reason"] = damageOutcome.Reason,
            ["damage_tag_source"] = damageOutcome.DamageTagSource,
            ["damage_tag"] = damageOutcome.DamageTag,
            ["effect_type"] = ProgressionDataUtils
                .to_string_name(
                    effectDef != null
                        ? effectDef.effect_type
                        : Variant.From(new StringName(""))
                )
                .ToString(),
            ["source_unit_id"] = sourceUnit != null ? sourceUnit.unit_id.ToString() : "",
            ["target_unit_id"] = targetUnit != null ? targetUnit.unit_id.ToString() : "",
        };
    }
}
