using System;
using System.Collections.Generic;
using Godot;

// BattleDamageResolver 的 partial：减免/抗性/护盾格挡/DR 与命中加成条件。按伤害管线阶段拆出，不改逻辑。
public partial class BattleDamageResolver
{
    private readonly record struct FixedMitigationComponent(
        int Value,
        IReadOnlyList<MitigationSourceResult> Sources
    );

    private readonly record struct MitigationTierResolution(
        StringName Tier,
        MitigationSourceResult[] Sources
    );

    private MitigationTierResolution ResolveMitigationTierResult(
        BattleUnitState targetUnit,
        StringName damageTag,
        IReadOnlyList<StringName> mitigationBypassDamageTags = null,
        IReadOnlyList<StringName> mitigationBypassTiers = null
    )
    {
        if (targetUnit == null)
            return new MitigationTierResolution(
                MitigationTierNormal,
                Array.Empty<MitigationSourceResult>()
            );

        var halfSources = new List<MitigationSourceResult>();
        var doubleSources = new List<MitigationSourceResult>();
        var immuneSources = new List<MitigationSourceResult>();
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
            if (
                ShouldBypassMitigationTier(
                    damageTag,
                    mitigationTier,
                    mitigationBypassDamageTags,
                    mitigationBypassTiers
                )
            )
            {
                continue;
            }
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
            immuneSources,
            mitigationBypassDamageTags,
            mitigationBypassTiers
        );
        if (immuneSources.Count > 0)
            return new MitigationTierResolution(MitigationTierImmune, immuneSources.ToArray());
        if (halfSources.Count > 0 && doubleSources.Count > 0)
        {
            var cancelled = new List<MitigationSourceResult>(
                halfSources.Count + doubleSources.Count
            );
            cancelled.AddRange(halfSources);
            cancelled.AddRange(doubleSources);
            return new MitigationTierResolution(MitigationTierNormal, cancelled.ToArray());
        }
        if (halfSources.Count > 0)
            return new MitigationTierResolution(MitigationTierHalf, halfSources.ToArray());
        if (doubleSources.Count > 0)
            return new MitigationTierResolution(MitigationTierDouble, doubleSources.ToArray());
        return new MitigationTierResolution(
            MitigationTierNormal,
            Array.Empty<MitigationSourceResult>()
        );
    }

    private static void AppendDamageResistanceSources(
        BattleUnitState targetUnit,
        StringName damageTag,
        List<MitigationSourceResult> halfSources,
        List<MitigationSourceResult> doubleSources,
        List<MitigationSourceResult> immuneSources,
        IReadOnlyList<StringName> mitigationBypassDamageTags = null,
        IReadOnlyList<StringName> mitigationBypassTiers = null
    )
    {
        if (targetUnit == null || damageTag == "")
        {
            return;
        }
        if (
            !targetUnit.TryGetDamageResistanceTyped(
                damageTag,
                out StringName mitigationTier
            )
        )
        {
            return;
        }
        mitigationTier = ProgressionDataUtils.to_string_name(
            mitigationTier
        );
        if (
            ShouldBypassMitigationTier(
                damageTag,
                mitigationTier,
                mitigationBypassDamageTags,
                mitigationBypassTiers
            )
        )
        {
            return;
        }
        StringName sourceId = new($"damage_resistance_{damageTag}");
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

    private static bool ShouldBypassMitigationTier(
        StringName damageTag,
        StringName mitigationTier,
        IReadOnlyList<StringName> mitigationBypassDamageTags,
        IReadOnlyList<StringName> mitigationBypassTiers
    )
    {
        StringName normalizedDamageTag = ProgressionDataUtils.to_string_name(damageTag);
        StringName normalizedTier = ProgressionDataUtils.to_string_name(mitigationTier);
        if (
            normalizedDamageTag == ""
            || normalizedTier == ""
            || mitigationBypassDamageTags == null
            || mitigationBypassTiers == null
            || mitigationBypassDamageTags.Count == 0
            || mitigationBypassTiers.Count == 0
        )
        {
            return false;
        }
        return ContainsStringName(mitigationBypassDamageTags, normalizedDamageTag)
            && ContainsStringName(mitigationBypassTiers, normalizedTier);
    }

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName rawValue in values ?? Array.Empty<StringName>())
        {
            if (ProgressionDataUtils.to_string_name(rawValue) == expected)
                return true;
        }
        return false;
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

    private static int ResolveFixedMitigationDamageFloor(
        int tierAdjustedDamage,
        FixedMitigationResult mitigation
    )
    {
        if (tierAdjustedDamage <= 0)
        {
            return MinDamageFloor;
        }
        int stanceReduction = Math.Max(mitigation?.StanceReduction ?? 0, 0);
        if (stanceReduction <= 0)
        {
            return MinDamageFloor;
        }
        int nonStanceMitigation = Math.Max(
            (mitigation?.Total ?? 0) - stanceReduction,
            0
        );
        return tierAdjustedDamage > nonStanceMitigation ? 1 : MinDamageFloor;
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

    private static MitigationSourceResult BuildMitigationSource(
        StringName statusId,
        string sourceType,
        int value = 0,
        StringName tier = default
    )
    {
        return new MitigationSourceResult
        {
            StatusId = statusId.ToString(),
            Type = sourceType,
            Value = value,
            Tier = AttackEffectResolutionResultReader.ParseMitigationTier(
                tier == default ? new StringName("") : tier
            ),
        };
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















    private static BattleWeaponDiceValues GetCurrentWeaponDamageDice(
        BattleUnitState unitState
    )
    {
        if (unitState == null)
        {
            return default;
        }
        BattleWeaponProjectionValues weaponProjection =
            unitState.GetWeaponProjectionReadViewTyped().Values;
        return weaponProjection.ActiveDice;
    }

    private static int GetCurrentWeaponDamageDiceSides(
        BattleWeaponProjectionValues weaponProjection
    )
    {
        BattleWeaponDiceValues dice = weaponProjection.ActiveDice;
        return dice.HasUsableDice ? Math.Max(dice.DiceSides, 0) : 0;
    }




}
