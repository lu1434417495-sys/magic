using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：减免/抗性/护盾格挡/DR 与命中加成条件。按伤害管线阶段拆出，不改逻辑。
public partial class BattleDamageResolver : RefCounted
{
    private GDictionary ResolveMitigationTierResult(
        BattleUnitState targetUnit,
        StringName damageTag
    )
    {
        if (targetUnit == null)
        {
            return new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = new GArray() };
        }
        var halfSources = new GArray();
        var doubleSources = new GArray();
        var immuneSources = new GArray();
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
            return new GDictionary { ["tier"] = MitigationTierImmune, ["sources"] = immuneSources };
        if (halfSources.Count > 0 && doubleSources.Count > 0)
        {
            var cancelled = new GArray();
            cancelled.AddRange(halfSources);
            cancelled.AddRange(doubleSources);
            return new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = cancelled };
        }
        if (halfSources.Count > 0)
            return new GDictionary { ["tier"] = MitigationTierHalf, ["sources"] = halfSources };
        if (doubleSources.Count > 0)
            return new GDictionary { ["tier"] = MitigationTierDouble, ["sources"] = doubleSources };
        return new GDictionary { ["tier"] = MitigationTierNormal, ["sources"] = new GArray() };
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

    private GDictionary BuildFixedMitigation(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        StringName damageTag
    )
    {
        GDictionary buffReduction = ResolveBuffReductionResult(targetUnit);
        GDictionary stanceReduction = ResolveStanceReductionResult(targetUnit, damageTag);
        GDictionary passiveReduction = ResolvePassiveReductionResult(targetUnit);
        GDictionary contentDr = ResolveContentDrResult(targetUnit, effectDef, damageTag);
        GDictionary guardBlock = ResolveGuardBlockResult(targetUnit, damageTag);
        var sources = new GArray();
        sources.AddRange(GetArray(buffReduction, "sources"));
        sources.AddRange(GetArray(stanceReduction, "sources"));
        sources.AddRange(GetArray(passiveReduction, "sources"));
        sources.AddRange(GetArray(contentDr, "sources"));
        sources.AddRange(GetArray(guardBlock, "sources"));
        return new GDictionary
        {
            ["buff_reduction"] = DictInt(buffReduction, "value"),
            ["stance_reduction"] = DictInt(stanceReduction, "value"),
            ["passive_reduction"] = DictInt(passiveReduction, "value"),
            ["content_dr"] = DictInt(contentDr, "value"),
            ["guard_block"] = DictInt(guardBlock, "value"),
            ["fixed_mitigation_sources"] = sources,
            ["guard_ignore_applied"] = 0,
        };
    }

    private GDictionary ResolveBuffReductionResult(BattleUnitState targetUnit)
    {
        if (!HasStatusEffect(targetUnit, StatusDamageReductionUp))
        {
            return ZeroSourceResult();
        }
        int strength = GetStatusStrength(targetUnit, StatusDamageReductionUp);
        int value = Math.Max(strength, 0) * DamageReductionUpFixedPerPower;
        return new GDictionary
        {
            ["value"] = value,
            ["sources"] = new GArray
            {
                BuildMitigationSource(StatusDamageReductionUp, "buff_reduction", value),
            },
        };
    }

    private GDictionary ResolveStanceReductionResult(
        BattleUnitState targetUnit,
        StringName damageTag
    )
    {
        if (!IsPhysicalDamageTag(damageTag) || !HasStatusEffect(targetUnit, StatusGuarding))
        {
            return ZeroSourceResult();
        }
        int value = Math.Max(GetStatusStrength(targetUnit, StatusGuarding), 0);
        return new GDictionary
        {
            ["value"] = value,
            ["sources"] = new GArray
            {
                BuildMitigationSource(StatusGuarding, "stance_reduction", value),
            },
        };
    }

    private GDictionary ResolvePassiveReductionResult(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return ZeroSourceResult();
        }
        int maxPassiveReduction = 0;
        var sources = new GArray();
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
                sources.Add(BuildMitigationSource(statusId, "passive_reduction", passiveReduction));
            }
            else if (passiveReduction == maxPassiveReduction)
            {
                sources.Add(BuildMitigationSource(statusId, "passive_reduction", passiveReduction));
            }
        }
        return new GDictionary { ["value"] = maxPassiveReduction, ["sources"] = sources };
    }

    private GDictionary ResolveContentDrResult(
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
        var sources = new GArray();
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
                sources.Add(BuildMitigationSource(statusId, "content_dr", contentDr));
            }
            else if (contentDr == maxContentDr)
            {
                sources.Add(BuildMitigationSource(statusId, "content_dr", contentDr));
            }
        }
        return new GDictionary { ["value"] = maxContentDr, ["sources"] = sources };
    }

    private GDictionary ResolveGuardBlockResult(BattleUnitState targetUnit, StringName damageTag)
    {
        if (targetUnit == null)
        {
            return ZeroSourceResult();
        }
        int maxGuardBlock = 0;
        var sources = new GArray();
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
                sources.Add(BuildMitigationSource(statusId, "guard_block", guardBlock));
            }
            else if (guardBlock == maxGuardBlock)
            {
                sources.Add(BuildMitigationSource(statusId, "guard_block", guardBlock));
            }
        }
        return new GDictionary { ["value"] = maxGuardBlock, ["sources"] = sources };
    }

    private static GDictionary ZeroSourceResult()
    {
        return new GDictionary { ["value"] = 0, ["sources"] = new GArray() };
    }

    private static GDictionary BuildMitigationSource(
        StringName statusId,
        string sourceType,
        int value = 0,
        StringName tier = default
    )
    {
        return new GDictionary
        {
            ["status_id"] = statusId.ToString(),
            ["type"] = sourceType,
            ["value"] = value,
            ["tier"] = (tier == default ? new StringName("") : tier).ToString(),
        };
    }

    private void ApplyBlackStarBrandGuardIgnore(GDictionary mitigation, BattleUnitState targetUnit)
    {
        if (
            mitigation == null
            || targetUnit == null
            || !targetUnit.HasStatusEffect(StatusBlackStarBrandEliteGuardWindow)
        )
        {
            return;
        }
        int remainingIgnore = BlackStarBrandGuardIgnoreFlat;
        int ignoredTotal = ApplyIgnoreToMitigationField(
            mitigation,
            "guard_block",
            ref remainingIgnore
        );
        ignoredTotal += ApplyIgnoreToMitigationField(
            mitigation,
            "stance_reduction",
            ref remainingIgnore
        );
        mitigation["guard_ignore_applied"] = ignoredTotal;
        targetUnit.EraseStatusEffect(StatusBlackStarBrandEliteGuardWindow);
    }

    private static int ApplyIgnoreToMitigationField(
        GDictionary mitigation,
        string field,
        ref int remainingIgnore
    )
    {
        if (remainingIgnore <= 0)
        {
            return 0;
        }
        int value = Math.Max(DictInt(mitigation, field), 0);
        if (value <= 0)
        {
            return 0;
        }
        int ignored = Math.Min(value, remainingIgnore);
        mitigation[field] = value - ignored;
        remainingIgnore -= ignored;
        return ignored;
    }

    private bool ApplyLowLuckBlackStarWedgeGuardIgnore(
        GDictionary mitigation,
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
        int remainingIgnore = LowLuckRelicRules.BlackStarWedgeGuardIgnoreFlat;
        int ignoredTotal = ApplyIgnoreToMitigationField(
            mitigation,
            "guard_block",
            ref remainingIgnore
        );
        ignoredTotal += ApplyIgnoreToMitigationField(
            mitigation,
            "stance_reduction",
            ref remainingIgnore
        );
        mitigation["guard_ignore_applied"] =
            DictInt(mitigation, "guard_ignore_applied") + ignoredTotal;
        mitigation["low_luck_black_star_wedge_triggered"] = true;
        return true;
    }

    private static void TrimFixedMitigationSources(GDictionary mitigation)
    {
        if (mitigation == null)
        {
            return;
        }
        GArray sources = GetArray(mitigation, "fixed_mitigation_sources");
        var filteredSources = new GArray();
        foreach (GDictionary source in ReadDictionaryItems(sources))
        {
            string sourceType = DictString(source, "type");
            int remaining = sourceType switch
            {
                "buff_reduction" => DictInt(mitigation, "buff_reduction"),
                "stance_reduction" => DictInt(mitigation, "stance_reduction"),
                "passive_reduction" => DictInt(mitigation, "passive_reduction"),
                "content_dr" => DictInt(mitigation, "content_dr"),
                "guard_block" => DictInt(mitigation, "guard_block"),
                _ => 0,
            };
            if (remaining <= 0)
            {
                continue;
            }
            GDictionary updatedSource = DuplicateDictionary(source, false);
            updatedSource["value"] = remaining;
            filteredSources.Add(updatedSource);
        }
        mitigation["fixed_mitigation_sources"] = filteredSources;
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
