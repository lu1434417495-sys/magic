using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private sealed class DamageEstimateResult
    {
        public int Damage;
        public int HpDamage;
        public int PostSaveDamage;
        public int IncomingBudgetDamage;
        public int ShieldAbsorbed;
        public bool ShieldBroken;
        public bool StableLethal;
        public int LethalProbabilityBasisPoints;
        public List<DamageSaveEstimate> SaveEstimates = new();
        public List<DamageEstimateBreakdown> DamageEstimates = new();

        public DamageEstimateResult Clone()
        {
            return new DamageEstimateResult
            {
                Damage = Damage,
                HpDamage = HpDamage,
                PostSaveDamage = PostSaveDamage,
                IncomingBudgetDamage = IncomingBudgetDamage,
                ShieldAbsorbed = ShieldAbsorbed,
                ShieldBroken = ShieldBroken,
                StableLethal = StableLethal,
                LethalProbabilityBasisPoints = LethalProbabilityBasisPoints,
                SaveEstimates = CloneSaveEstimates(SaveEstimates),
                DamageEstimates = CloneDamageEstimates(DamageEstimates),
            };
        }
    }

    public sealed class DamageSaveEstimate
    {
        public bool HasSave;
        public int DamageBeforeSave;
        public int DamageAfterSaveEstimate;
        public int DamageOnSaveFailure;
        public int DamageOnSaveSuccess;
        public bool SavePartialOnSuccess;
        public int SaveSuccessProbabilityBasisPoints;
        public int SaveSuccessRatePercent;
        public int SaveFailureProbabilityBasisPoints;
        public int Dc;
        public string Ability = "";
        public string SaveTag = "";
        public string AdvantageState = "";
        public int AbilityValue;
        public int AbilityModifier;
        public int Bonus;
        public bool Immune;
        public int HitCount = 1;

        public DamageSaveEstimate Clone()
        {
            return new DamageSaveEstimate
            {
                HasSave = HasSave,
                DamageBeforeSave = DamageBeforeSave,
                DamageAfterSaveEstimate = DamageAfterSaveEstimate,
                DamageOnSaveFailure = DamageOnSaveFailure,
                DamageOnSaveSuccess = DamageOnSaveSuccess,
                SavePartialOnSuccess = SavePartialOnSuccess,
                SaveSuccessProbabilityBasisPoints = SaveSuccessProbabilityBasisPoints,
                SaveSuccessRatePercent = SaveSuccessRatePercent,
                SaveFailureProbabilityBasisPoints = SaveFailureProbabilityBasisPoints,
                Dc = Dc,
                Ability = Ability,
                SaveTag = SaveTag,
                AdvantageState = AdvantageState,
                AbilityValue = AbilityValue,
                AbilityModifier = AbilityModifier,
                Bonus = Bonus,
                Immune = Immune,
                HitCount = HitCount,
            };
        }

        public DamageSaveEstimate Scaled(int hitCount)
        {
            int safeHitCount = Math.Max(hitCount, 1);
            DamageSaveEstimate scaled = Clone();
            scaled.HitCount = Math.Max(HitCount, 1) * safeHitCount;
            scaled.DamageBeforeSave *= safeHitCount;
            scaled.DamageAfterSaveEstimate *= safeHitCount;
            scaled.DamageOnSaveFailure *= safeHitCount;
            scaled.DamageOnSaveSuccess *= safeHitCount;
            return scaled;
        }

        internal Dictionary<string, object> ToTraceDictionary()
        {
            return new Dictionary<string, object>(System.StringComparer.Ordinal)
            {
                ["has_save"] = HasSave,
                ["damage_before_save"] = DamageBeforeSave,
                ["damage_after_save_estimate"] = DamageAfterSaveEstimate,
                ["damage_on_save_failure"] = DamageOnSaveFailure,
                ["damage_on_save_success"] = DamageOnSaveSuccess,
                ["save_partial_on_success"] = SavePartialOnSuccess,
                ["save_success_probability_basis_points"] = SaveSuccessProbabilityBasisPoints,
                ["save_success_rate_percent"] = SaveSuccessRatePercent,
                ["save_failure_probability_basis_points"] = SaveFailureProbabilityBasisPoints,
                ["dc"] = Dc,
                ["ability"] = Ability ?? "",
                ["save_tag"] = SaveTag ?? "",
                ["advantage_state"] = AdvantageState ?? "",
                ["ability_value"] = AbilityValue,
                ["ability_modifier"] = AbilityModifier,
                ["bonus"] = Bonus,
                ["immune"] = Immune,
                ["hit_count"] = Math.Max(HitCount, 1),
            };
        }

        public static DamageSaveEstimate FromPreviewSaveEstimate(
            BattleDamagePreviewSaveEstimate source
        )
        {
            if (source == null || !source.HasSave)
            {
                return null;
            }
            return new DamageSaveEstimate
            {
                HasSave = true,
                DamageBeforeSave = source.DamageBeforeSave,
                DamageAfterSaveEstimate = source.DamageAfterSaveEstimate,
                DamageOnSaveFailure = source.DamageOnSaveFailure,
                DamageOnSaveSuccess = source.DamageOnSaveSuccess,
                SavePartialOnSuccess = source.SavePartialOnSuccess,
                SaveSuccessProbabilityBasisPoints = source.SaveSuccessProbabilityBasisPoints,
                SaveSuccessRatePercent = source.SaveSuccessRatePercent,
                SaveFailureProbabilityBasisPoints = source.SaveFailureProbabilityBasisPoints,
                Dc = source.Dc,
                Ability = source.Ability,
                SaveTag = source.SaveTag,
                AdvantageState = source.AdvantageState,
                AbilityValue = source.AbilityValue,
                AbilityModifier = source.AbilityModifier,
                Bonus = source.Bonus,
                Immune = source.Immune,
                HitCount = 1,
            };
        }
    }

    public sealed class DamageEstimateBreakdown
    {
        public int HpDamage;
        public int Damage;
        public int PostSaveDamage;
        public int IncomingBudgetDamage;
        public int ShieldAbsorbed;
        public bool ShieldBroken;
        public bool StableLethal;
        public int LethalProbabilityBasisPoints;
        public List<DamageSaveEstimate> SaveEstimates = new();
        public List<object> DamageEvents = new();
        public List<object> Diagnostics = new();

        public DamageEstimateBreakdown Clone()
        {
            return new DamageEstimateBreakdown
            {
                HpDamage = HpDamage,
                Damage = Damage,
                PostSaveDamage = PostSaveDamage,
                IncomingBudgetDamage = IncomingBudgetDamage,
                ShieldAbsorbed = ShieldAbsorbed,
                ShieldBroken = ShieldBroken,
                StableLethal = StableLethal,
                LethalProbabilityBasisPoints = LethalProbabilityBasisPoints,
                SaveEstimates = CloneSaveEstimates(SaveEstimates),
                DamageEvents = CloneTraceObjectList(DamageEvents),
                Diagnostics = CloneTraceObjectList(Diagnostics),
            };
        }

        internal Dictionary<string, object> ToTraceDictionary()
        {
            return new Dictionary<string, object>(System.StringComparer.Ordinal)
            {
                ["hp_damage"] = HpDamage,
                ["damage"] = Damage,
                ["post_save_damage"] = PostSaveDamage,
                ["incoming_budget_damage"] = IncomingBudgetDamage,
                ["shield_absorbed"] = ShieldAbsorbed,
                ["shield_broken"] = ShieldBroken,
                ["stable_lethal"] = StableLethal,
                ["lethal_probability_basis_points"] = LethalProbabilityBasisPoints,
                ["save_estimates"] = SaveEstimatesToTraceList(SaveEstimates),
                ["damage_events"] = CloneTraceObjectList(DamageEvents),
                ["diagnostics"] = CloneTraceObjectList(Diagnostics),
            };
        }
    }

    private sealed class DamagePreviewSnapshot
    {
        public int HpDamage;
        public int Damage;
        public int PostSaveDamage;
        public int IncomingBudgetDamage;
        public int ShieldAbsorbed;
        public bool ShieldBroken;
        public bool StableLethal;
        public int LethalProbabilityBasisPoints;
        public DamageSaveEstimate PrimarySaveEstimate;
        public List<DamageSaveEstimate> SaveEstimates = new();
        public List<object> DamageEvents = new();
        public List<object> Diagnostics = new();
        public BattleUnitState TargetPreviewAfter;

        public static DamagePreviewSnapshot FromPreviewResult(BattleDamagePreviewResult preview)
        {
            if (preview == null)
            {
                return new DamagePreviewSnapshot();
            }
            int hpDamage = preview.HpDamage;
            DamageSaveEstimate primarySaveEstimate = DamageSaveEstimate.FromPreviewSaveEstimate(
                preview.SaveEstimate
            );
            List<DamageSaveEstimate> saveEstimates = ReadSaveEstimatesFromPreviewList(
                preview.SaveEstimates
            );
            if (saveEstimates.Count == 0 && primarySaveEstimate != null)
            {
                saveEstimates.Add(primarySaveEstimate);
            }
            return new DamagePreviewSnapshot
            {
                HpDamage = hpDamage,
                Damage = hpDamage,
                PostSaveDamage = preview.PostSaveDamage,
                IncomingBudgetDamage = preview.IncomingBudgetDamage,
                ShieldAbsorbed = preview.ShieldAbsorbed,
                ShieldBroken = preview.ShieldBroken,
                StableLethal = preview.StableLethal,
                LethalProbabilityBasisPoints = preview.LethalProbabilityBasisPoints,
                PrimarySaveEstimate = primarySaveEstimate,
                SaveEstimates = saveEstimates,
                DamageEvents = CloneTraceObjectList(preview.DamageEvents),
                Diagnostics = CloneTraceObjectList(preview.Diagnostics),
                TargetPreviewAfter = preview.TargetPreviewAfter,
            };
        }
    }

    private sealed class PathStepHitCountEntry
    {
        public StringName UnitId;
        public int HitCount;
    }

    private static List<DamageSaveEstimate> CloneSaveEstimates(
        IEnumerable<DamageSaveEstimate> estimates
    )
    {
        var result = new List<DamageSaveEstimate>();
        foreach (DamageSaveEstimate estimate in estimates ?? System.Array.Empty<DamageSaveEstimate>())
        {
            if (estimate != null)
            {
                result.Add(estimate.Clone());
            }
        }
        return result;
    }

    private static List<DamageEstimateBreakdown> CloneDamageEstimates(
        IEnumerable<DamageEstimateBreakdown> estimates
    )
    {
        var result = new List<DamageEstimateBreakdown>();
        foreach (
            DamageEstimateBreakdown estimate in estimates
                ?? System.Array.Empty<DamageEstimateBreakdown>()
        )
        {
            if (estimate != null)
            {
                result.Add(estimate.Clone());
            }
        }
        return result;
    }

    private static List<object> CloneTraceObjectList(IEnumerable<object> values)
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (object value in values)
        {
            result.Add(CloneTraceObject(value));
        }
        return result;
    }

    private static object CloneTraceObject(object value)
    {
        switch (value)
        {
            case null:
                return "";
            case IReadOnlyDictionary<string, object> dictionary:
                return CloneTraceDictionary(dictionary);
            case System.Collections.IDictionary dictionary:
                return CloneUntypedTraceDictionary(dictionary);
            case IEnumerable<object> values:
                return CloneTraceObjectList(values);
            case System.Collections.IEnumerable values when value is not string:
                return CloneTraceEnumerable(values);
            default:
                return value;
        }
    }

    private static Dictionary<string, object> CloneTraceDictionary(
        IReadOnlyDictionary<string, object> values
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (values == null)
        {
            return result;
        }
        foreach (KeyValuePair<string, object> entry in values)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                result[entry.Key] = CloneTraceObject(entry.Value);
            }
        }
        return result;
    }

    private static Dictionary<string, object> CloneUntypedTraceDictionary(
        System.Collections.IDictionary values
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (values == null)
        {
            return result;
        }
        foreach (System.Collections.DictionaryEntry entry in values)
        {
            string key = entry.Key?.ToString() ?? "";
            if (!string.IsNullOrEmpty(key))
            {
                result[key] = CloneTraceObject(entry.Value);
            }
        }
        return result;
    }

    private static List<object> CloneTraceEnumerable(System.Collections.IEnumerable values)
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (object value in values)
        {
            result.Add(CloneTraceObject(value));
        }
        return result;
    }

    private static List<DamageSaveEstimate> ReadSaveEstimatesFromPreviewList(
        IReadOnlyList<BattleDamagePreviewSaveEstimate> estimates
    )
    {
        var result = new List<DamageSaveEstimate>();
        if (estimates == null)
        {
            return result;
        }
        foreach (BattleDamagePreviewSaveEstimate estimateData in estimates)
        {
            DamageSaveEstimate estimate = DamageSaveEstimate.FromPreviewSaveEstimate(estimateData);
            if (estimate != null)
            {
                result.Add(estimate);
            }
        }
        return result;
    }

    private void PopulatePathStepAoeMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        ScorePathStepAoeMetadata metadata
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || state == null || actor == null)
        {
            return;
        }
        List<PathStepHitCountEntry> hitCountEntries =
            metadata?.HitCounts ?? new List<PathStepHitCountEntry>();
        if (hitCountEntries.Count == 0)
        {
            return;
        }
        CombatEffectDefinition pathStepEffect = FindPathStepAoeEffect(effectDefinitions);
        if (pathStepEffect == null && metadata?.Effect != null)
        {
            pathStepEffect = metadata.Effect;
        }
        if (pathStepEffect == null)
        {
            return;
        }
        CombatEffectDefinition damageEffect = BuildPathStepDamageEffect(pathStepEffect);
        if (damageEffect == null)
        {
            return;
        }
        int rawPayoff = 0;
        int rawTargetPriority = 0;
        int rawStatusPayoff = 0;
        int totalHitCount = 0;
        int uniqueTargetCount = 0;
        foreach (PathStepHitCountEntry entry in hitCountEntries)
        {
            StringName targetUnitId = entry.UnitId;
            int hitCount = Math.Max(entry.HitCount, 0);
            if (IsEmpty(targetUnitId) || hitCount <= 0)
            {
                continue;
            }
            BattleUnitState targetUnit = GetUnit(state, targetUnitId);
            if (targetUnit == null)
            {
                continue;
            }
            totalHitCount += hitCount;
            uniqueTargetCount += 1;
            DamageEstimateResult estimateResult = EstimateDamageForTargetResult(
                actor,
                RepeatEffectDefinitions(new[] { damageEffect }, hitCount),
                targetUnit,
                ResolveSkillId(skillDefinition)
            );
            int estimatedDamage = estimateResult.Damage;
            int estimatedShieldAbsorbed = estimateResult.ShieldAbsorbed;
            AppendSaveEstimatesForTarget(scoreInput, targetUnit, estimateResult.SaveEstimates);
            AppendDamageEstimatesForTarget(scoreInput, targetUnit, estimateResult.DamageEstimates);
            scoreInput.estimated_damage += estimatedDamage;
            scoreInput.estimated_post_save_damage += estimateResult.PostSaveDamage;
            scoreInput.estimated_shield_absorbed += estimatedShieldAbsorbed;
            if (targetUnit.faction_id == actor.faction_id)
            {
                rawPayoff -= estimatedDamage * _scoreProfile.DamageWeight;
                continue;
            }
            rawPayoff += estimatedDamage * _scoreProfile.DamageWeight;
            rawPayoff += estimatedShieldAbsorbed * Math.Max(_scoreProfile.ShieldAbsorbedWeight, 0);
            int targetPriorityBonus = ResolveTargetRoleThreatBonus(
                context,
                targetUnit,
                estimatedDamage,
                0,
                0,
                0
            );
            rawTargetPriority += targetPriorityBonus;
            rawPayoff += targetPriorityBonus;
            int lethalBonus =
                estimateResult.StableLethal || estimatedDamage >= Math.Max(targetUnit.current_hp, 1)
                ? ResolveLethalTargetBonus(scoreInput, context, targetUnit, estimatedDamage)
                : 0;
            rawTargetPriority += lethalBonus;
            rawPayoff += lethalBonus;
            if (
                PathStepRepeatStatusApplies(
                    context,
                    skillDefinition,
                    pathStepEffect,
                    hitCount
                )
            )
            {
                rawStatusPayoff += _scoreProfile.StatusWeight;
            }
        }
        rawPayoff += rawStatusPayoff;
        double hitRateMultiplier = scoreInput.estimated_hit_rate_percent / 100.0;
        scoreInput.path_step_hit_count = totalHitCount;
        scoreInput.path_step_unique_target_count = uniqueTargetCount;
        scoreInput.path_step_hit_counts_by_unit_id = PathStepHitCountsToDictionary(hitCountEntries);
        scoreInput.path_step_payoff_score = RoundToInt(rawPayoff * hitRateMultiplier);
        scoreInput.hit_payoff_score += scoreInput.path_step_payoff_score;
        scoreInput.target_priority_score += RoundToInt(rawTargetPriority * hitRateMultiplier);
        if (scoreInput.target_count < uniqueTargetCount)
        {
            scoreInput.target_count = uniqueTargetCount;
        }
    }

    private static List<PathStepHitCountEntry> ReadPathStepHitCountEntries(
        IReadOnlyDictionary<string, object> metadata
    )
    {
        var result = new List<PathStepHitCountEntry>();
        if (
            metadata == null
            || !metadata.TryGetValue("path_step_hit_counts_by_unit_id", out object rawHitCounts)
            || rawHitCounts is not IReadOnlyDictionary<StringName, int> hitCounts
        )
        {
            return result;
        }
        foreach (KeyValuePair<StringName, int> entry in hitCounts)
        {
            int hitCount = Math.Max(entry.Value, 0);
            if (!IsEmpty(entry.Key) && hitCount > 0)
            {
                result.Add(new PathStepHitCountEntry { UnitId = entry.Key, HitCount = hitCount });
            }
        }
        return result;
    }

    private static List<object> SaveEstimatesToTraceList(
        IEnumerable<DamageSaveEstimate> estimates
    )
    {
        var result = new List<object>();
        foreach (DamageSaveEstimate estimate in estimates ?? System.Array.Empty<DamageSaveEstimate>())
        {
            if (estimate != null && estimate.HasSave)
            {
                result.Add(estimate.ToTraceDictionary());
            }
        }
        return result;
    }

    private static Dictionary<StringName, int> PathStepHitCountsToDictionary(
        IEnumerable<PathStepHitCountEntry> entries
    )
    {
        var result = new Dictionary<StringName, int>();
        foreach (PathStepHitCountEntry entry in entries ?? System.Array.Empty<PathStepHitCountEntry>())
        {
            if (entry != null && !IsEmpty(entry.UnitId) && entry.HitCount > 0)
            {
                result[ProgressionDataUtils.to_string_name(entry.UnitId)] = entry.HitCount;
            }
        }
        return result;
    }

    private static CombatEffectDefinition FindPathStepAoeEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition != null
                && effectDefinition.EffectKind == BattleEffectKind.PathStepAoe
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private static CombatEffectDefinition BuildPathStepDamageEffect(
        CombatEffectDefinition pathStepEffect
    )
    {
        return pathStepEffect?.WithEffectType(BattleTypedNames.ToStringName(BattleEffectKind.Damage));
    }

    private static bool PathStepRepeatStatusApplies(
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition,
        CombatEffectDefinition pathStepEffect,
        int hitCount
    )
    {
        if (context == null || pathStepEffect == null || hitCount <= 0)
        {
            return false;
        }
        StringName statusId = ReadStringNameParameter(pathStepEffect, "repeat_hit_status_id");
        if (IsEmpty(statusId))
        {
            return false;
        }
        int threshold = Math.Max(
            ReadIntParameter(pathStepEffect, "repeat_hit_status_threshold", 1),
            1
        );
        if (hitCount < threshold)
        {
            return false;
        }
        int durationTu = ReadIntParameter(pathStepEffect, "repeat_hit_status_duration_tu", 0);
        if (durationTu <= 0)
        {
            return false;
        }
        int minSkillLevel = Math.Max(
            ReadIntParameter(pathStepEffect, "repeat_hit_status_min_skill_level", 0),
            0
        );
        StringName skillId = ResolveSkillId(skillDefinition);
        return GetContextSkillLevel(context, skillId) >= minSkillLevel;
    }

    private static List<CombatEffectDefinition> FilterEffectDefinitionsForContext(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition
    )
    {
        var filteredEffectDefinitions = new List<CombatEffectDefinition>();
        bool shouldFilter = ContextUnitState(context) != null;
        int skillLevel = GetContextSkillLevel(context, ResolveSkillId(skillDefinition));
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
            {
                continue;
            }
            if (!IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel, shouldFilter))
            {
                continue;
            }
            filteredEffectDefinitions.Add(effectDefinition);
        }
        return filteredEffectDefinitions;
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDefinition effectDefinition,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        if (!shouldFilter)
        {
            return true;
        }
        int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
        int maxLevel = effectDefinition.MaxSkillLevel;
        if (skillLevel < minLevel)
        {
            return false;
        }
        return maxLevel < 0 || skillLevel <= maxLevel;
    }

    private DamageEstimateResult EstimateDamageForTargetResult(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleUnitState targetUnit,
        StringName skillId = default
    )
    {
        int total = 0;
        int postSaveTotal = 0;
        var saveEstimates = new List<DamageSaveEstimate>();
        var damageEstimates = new List<DamageEstimateBreakdown>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null || effectDefinition.EffectKind != BattleEffectKind.Damage)
            {
                continue;
            }
            int baseDamage = EstimateDamageFromDefinition(effectDefinition, sourceUnit);
            int bonusDamage = EstimateConditionalBonusDamage(effectDefinition, targetUnit);
            double multiplier = ResolveEffectDamageMultiplier(effectDefinition, targetUnit);
            int preSaveDamage = Math.Max(
                RoundToInt((baseDamage + bonusDamage) * multiplier),
                0
            );
            DamageSaveEstimate saveEstimate = BuildDamageSaveEstimate(
                sourceUnit,
                targetUnit,
                effectDefinition,
                preSaveDamage,
                skillId
            );
            int adjustedDamage = saveEstimate?.DamageAfterSaveEstimate ?? preSaveDamage;
            total += adjustedDamage;
            postSaveTotal += adjustedDamage;
            bool stableLethal = adjustedDamage >= Math.Max(targetUnit?.current_hp ?? 1, 1);
            damageEstimates.Add(
                new DamageEstimateBreakdown
                {
                    HpDamage = adjustedDamage,
                    Damage = adjustedDamage,
                    PostSaveDamage = adjustedDamage,
                    IncomingBudgetDamage = adjustedDamage,
                    ShieldAbsorbed = 0,
                    StableLethal = stableLethal,
                    LethalProbabilityBasisPoints = stableLethal ? 10000 : 0,
                    SaveEstimates = new List<DamageSaveEstimate>(),
                }
            );
            if (saveEstimate != null && saveEstimate.HasSave)
            {
                saveEstimates.Add(saveEstimate);
            }
        }
        return new DamageEstimateResult
        {
            Damage = total,
            HpDamage = total,
            PostSaveDamage = postSaveTotal,
            IncomingBudgetDamage = postSaveTotal,
            ShieldAbsorbed = 0,
            StableLethal = total >= Math.Max(targetUnit?.current_hp ?? 1, 1),
            LethalProbabilityBasisPoints =
                total >= Math.Max(targetUnit?.current_hp ?? 1, 1) ? 10000 : 0,
            SaveEstimates = saveEstimates,
            DamageEstimates = damageEstimates,
        };
    }

    private static int EstimateDamageFromDefinition(
        CombatEffectDefinition effectDefinition,
        BattleUnitState sourceUnit
    )
    {
        if (effectDefinition == null)
        {
            return 0;
        }
        int damage = Math.Max(effectDefinition.Power, 0);
        int diceSides =
            effectDefinition.DiceCount > 0 && effectDefinition.DiceSidesBase > 0
                ? EstimateAttributeScaledDiceSides(effectDefinition, sourceUnit)
                : Math.Max(effectDefinition.DiceSides, 0);
        damage += EstimateAverageDiceDamage(
            effectDefinition.DiceCount,
            diceSides,
            effectDefinition.DiceBonus
        );
        if (effectDefinition.AddWeaponDice)
        {
            WeaponDice weaponDice = sourceUnit?.GetActiveWeaponDiceTyped();
            if (weaponDice != null && !weaponDice.IsEmpty())
            {
                damage += EstimateAverageDiceDamage(
                    weaponDice.dice_count,
                    weaponDice.dice_sides,
                    weaponDice.flat_bonus
                );
            }
        }
        return Math.Max(damage, 0);
    }

    private static int EstimateAverageDiceDamage(int diceCount, int diceSides, int diceBonus)
    {
        int normalizedCount = Math.Max(diceCount, 0);
        int normalizedSides = Math.Max(diceSides, 0);
        if (normalizedCount <= 0 || normalizedSides <= 0)
        {
            return 0;
        }
        return Math.Max(RoundToInt(normalizedCount * (normalizedSides + 1) / 2.0 + diceBonus), 0);
    }

    private static DamageEstimateResult NormalizeDamageSequenceEstimate(
        DamagePreviewSnapshot preview
    )
    {
        preview ??= new DamagePreviewSnapshot();
        var damageEstimate = new DamageEstimateBreakdown
        {
            HpDamage = preview.HpDamage,
            Damage = preview.Damage,
            PostSaveDamage = preview.PostSaveDamage,
            IncomingBudgetDamage = preview.IncomingBudgetDamage,
            ShieldAbsorbed = preview.ShieldAbsorbed,
            ShieldBroken = preview.ShieldBroken,
            StableLethal = preview.StableLethal,
            LethalProbabilityBasisPoints = preview.LethalProbabilityBasisPoints,
            SaveEstimates = CloneSaveEstimates(preview.SaveEstimates),
            DamageEvents = CloneTraceObjectList(preview.DamageEvents),
            Diagnostics = CloneTraceObjectList(preview.Diagnostics),
        };
        return new DamageEstimateResult
        {
            Damage = preview.Damage,
            HpDamage = preview.HpDamage,
            PostSaveDamage = preview.PostSaveDamage,
            IncomingBudgetDamage = preview.IncomingBudgetDamage,
            ShieldAbsorbed = preview.ShieldAbsorbed,
            ShieldBroken = preview.ShieldBroken,
            StableLethal = preview.StableLethal,
            LethalProbabilityBasisPoints = preview.LethalProbabilityBasisPoints,
            SaveEstimates = CloneSaveEstimates(preview.SaveEstimates),
            DamageEstimates = new List<DamageEstimateBreakdown> { damageEstimate },
        };
    }

    private static void ApplyBranchLethalEstimate(
        DamageEstimateResult normalized,
        DamagePreviewSnapshot preview,
        int targetHpBefore
    )
    {
        DamageSaveEstimate saveEstimate = preview?.PrimarySaveEstimate;
        if (saveEstimate == null)
        {
            return;
        }
        int damageOnFailure = saveEstimate.DamageOnSaveFailure;
        int damageOnSuccess = saveEstimate.DamageOnSaveSuccess;
        int failureBasisPoints = saveEstimate.SaveFailureProbabilityBasisPoints;
        bool failureKills = damageOnFailure >= targetHpBefore;
        bool successKills = damageOnSuccess >= targetHpBefore;
        normalized.StableLethal = failureKills && successKills;
        normalized.LethalProbabilityBasisPoints = failureKills
            ? (successKills ? 10000 : Math.Max(failureBasisPoints, 0))
            : 0;
        if (failureKills && !successKills && normalized.Damage >= targetHpBefore)
        {
            normalized.Damage = Math.Max(damageOnSuccess, 0);
            normalized.HpDamage = Math.Max(damageOnSuccess, 0);
            if (normalized.DamageEstimates.Count > 0)
            {
                DamageEstimateBreakdown estimate = normalized.DamageEstimates[0];
                estimate.Damage = Math.Max(damageOnSuccess, 0);
                estimate.HpDamage = Math.Max(damageOnSuccess, 0);
                estimate.StableLethal = false;
                estimate.LethalProbabilityBasisPoints = Math.Max(failureBasisPoints, 0);
            }
        }
    }

    private static DamageSaveEstimate BuildDamageSaveEstimate(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        int damageBeforeSave,
        StringName skillId
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit,
                targetUnit,
                effectDefinition,
                BattleSaveContext.ForSkill(skillId)
            );
        if (!probability.HasSave)
        {
            return new DamageSaveEstimate
            {
                HasSave = false,
                DamageBeforeSave = damageBeforeSave,
                DamageAfterSaveEstimate = damageBeforeSave,
                DamageOnSaveFailure = damageBeforeSave,
                DamageOnSaveSuccess = damageBeforeSave,
                HitCount = 1,
            };
        }
        int successBasisPoints = Mathf.Clamp(
            probability.SuccessProbabilityBasisPoints,
            0,
            10000
        );
        int failureBasisPoints = Mathf.Clamp(probability.FailureProbabilityBasisPoints, 0, 10000);
        int damageOnSaveSuccess = 0;
        if (effectDefinition.SavePartialOnSuccess && !probability.Immune)
        {
            damageOnSaveSuccess = damageBeforeSave / 2;
        }
        int expectedDamage = RoundToInt(
            (
                (double)damageBeforeSave * failureBasisPoints
                + (double)damageOnSaveSuccess * successBasisPoints
            ) / 10000.0
        );
        return new DamageSaveEstimate
        {
            HasSave = true,
            DamageBeforeSave = damageBeforeSave,
            DamageAfterSaveEstimate = Math.Max(expectedDamage, 0),
            DamageOnSaveFailure = damageBeforeSave,
            DamageOnSaveSuccess = damageOnSaveSuccess,
            SavePartialOnSuccess = effectDefinition.SavePartialOnSuccess,
            SaveSuccessProbabilityBasisPoints = successBasisPoints,
            SaveSuccessRatePercent = RoundToInt(successBasisPoints / 100.0),
            SaveFailureProbabilityBasisPoints = failureBasisPoints,
            Dc = probability.Dc,
            Ability = probability.Ability.ToString(),
            SaveTag = probability.SaveTag.ToString(),
            AdvantageState = probability.AdvantageState.ToString(),
            AbilityValue = probability.AbilityValue,
            AbilityModifier = probability.AbilityModifier,
            Bonus = probability.Bonus,
            Immune = probability.Immune,
            HitCount = 1,
        };
    }

    private static List<DamageSaveEstimate> ScaleSaveEstimates(
        IReadOnlyList<DamageSaveEstimate> estimates,
        int hitCount
    )
    {
        var result = new List<DamageSaveEstimate>();
        if (estimates == null || estimates.Count == 0)
        {
            return result;
        }
        foreach (DamageSaveEstimate estimate in estimates)
        {
            if (estimate == null)
            {
                continue;
            }
            result.Add(estimate.Scaled(hitCount));
        }
        return result;
    }

    private static void AppendSaveEstimatesForTarget(
        BattleAiScoreInput scoreInput,
        BattleUnitState targetUnit,
        IReadOnlyList<DamageSaveEstimate> estimates
    )
    {
        if (
            scoreInput == null
            || targetUnit == null
            || estimates == null
            || estimates.Count == 0
        )
        {
            return;
        }
        StringName targetKey = ProgressionDataUtils.to_string_name(targetUnit.unit_id);
        if (
            !scoreInput.save_estimates_by_target_id.TryGetValue(
                targetKey,
                out List<DamageSaveEstimate> existing
            )
            || existing == null
        )
        {
            existing = new List<DamageSaveEstimate>();
        }
        foreach (DamageSaveEstimate estimate in estimates)
        {
            if (estimate != null && estimate.HasSave)
            {
                existing.Add(estimate.Clone());
            }
        }
        scoreInput.save_estimates_by_target_id[targetKey] = existing;
    }

    private static void AppendDamageEstimatesForTarget(
        BattleAiScoreInput scoreInput,
        BattleUnitState targetUnit,
        IReadOnlyList<DamageEstimateBreakdown> estimates
    )
    {
        if (
            scoreInput == null
            || targetUnit == null
            || estimates == null
            || estimates.Count == 0
        )
        {
            return;
        }
        StringName targetKey = ProgressionDataUtils.to_string_name(targetUnit.unit_id);
        if (
            !scoreInput.damage_estimates_by_target_id.TryGetValue(
                targetKey,
                out List<DamageEstimateBreakdown> existing
            )
            || existing == null
        )
        {
            existing = new List<DamageEstimateBreakdown>();
        }
        foreach (DamageEstimateBreakdown estimate in estimates)
        {
            if (estimate != null)
            {
                existing.Add(estimate.Clone());
            }
        }
        scoreInput.damage_estimates_by_target_id[targetKey] = existing;
    }

    private static StringName ResolveSkillId(SkillDefinition skillDefinition)
    {
        return skillDefinition != null ? skillDefinition.SkillId : "";
    }

    private static int EstimateDamageFromPreview(
        BattleDamagePreviewRangeService.SkillDamagePreview damagePreview
    )
    {
        if (!damagePreview.HasDamage)
        {
            return 0;
        }
        return Math.Max(
            RoundToInt((damagePreview.MinDamage + damagePreview.MaxDamage) / 2.0),
            0
        );
    }

    private double ResolveEffectDamageMultiplier(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        if (effectDefinition == null)
        {
            return 1.0;
        }
        double multiplier = GetPreResistanceDamageMultiplier(effectDefinition);
        if (HasBonusCondition(effectDefinition, targetUnit))
        {
            multiplier *= GetDamageRatioMultiplier(effectDefinition);
        }
        return Math.Max(multiplier, 0.0);
    }

    private int ResolveTargetRoleThreatBonus(
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        int estimatedDamage,
        int estimatedStatusCount,
        int estimatedTerrainEffectCount,
        int estimatedHeightDelta
    )
    {
        int multiplierBasisPoints = ResolveTargetRoleThreatMultiplierBasisPoints(
            context,
            targetUnit
        );
        if (multiplierBasisPoints <= ThreatMultiplierBasisPointsDenominator)
        {
            return 0;
        }
        int basePayoff =
            Math.Max(estimatedDamage, 0) * _scoreProfile.DamageWeight
            + Math.Max(estimatedStatusCount, 0) * _scoreProfile.StatusWeight
            + Math.Max(estimatedTerrainEffectCount, 0) * _scoreProfile.TerrainWeight
            + Math.Max(estimatedHeightDelta, 0) * _scoreProfile.HeightWeight;
        if (basePayoff <= 0)
        {
            return 0;
        }
        int bonusBasisPoints = multiplierBasisPoints - ThreatMultiplierBasisPointsDenominator;
        int roundedBonus =
            (basePayoff * bonusBasisPoints + ThreatMultiplierBasisPointsDenominator / 2)
            / ThreatMultiplierBasisPointsDenominator;
        return Math.Max(roundedBonus, 0);
    }

    private int ResolveLethalTargetBonus(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        int estimatedDamage
    )
    {
        if (scoreInput == null || targetUnit == null || !targetUnit.is_alive)
        {
            return 0;
        }
        if (estimatedDamage < Math.Max(targetUnit.current_hp, 1))
        {
            return 0;
        }
        scoreInput.estimated_lethal_target_count += 1;
        AppendUniqueStringName(scoreInput.estimated_lethal_target_ids, targetUnit.unit_id);
        int bonus = Math.Max(_scoreProfile.LethalTargetWeight, 0);
        if (IsPriorityThreatTarget(context, targetUnit))
        {
            scoreInput.estimated_lethal_threat_target_count += 1;
            AppendUniqueStringName(
                scoreInput.estimated_lethal_threat_target_ids,
                targetUnit.unit_id
            );
            bonus += Math.Max(_scoreProfile.LethalThreatTargetWeight, 0);
        }
        return bonus;
    }

    private int ResolveExecuteLethalBonusFromBasisPoints(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        int killProbabilityBasisPoints
    )
    {
        if (scoreInput == null || targetUnit == null || !targetUnit.is_alive)
        {
            return 0;
        }
        int killBps = Mathf.Clamp(killProbabilityBasisPoints, 0, 10000);
        if (killBps <= 0)
        {
            return 0;
        }

        int baseBonus = Math.Max(_scoreProfile.LethalTargetWeight, 0);
        int threatBonus = 0;
        bool isPriorityThreat = IsPriorityThreatTarget(context, targetUnit);
        if (isPriorityThreat)
        {
            threatBonus = Math.Max(_scoreProfile.LethalThreatTargetWeight, 0);
        }

        if (killBps >= 10000)
        {
            scoreInput.estimated_lethal_target_count += 1;
            AppendUniqueStringName(scoreInput.estimated_lethal_target_ids, targetUnit.unit_id);
            if (isPriorityThreat)
            {
                scoreInput.estimated_lethal_threat_target_count += 1;
                AppendUniqueStringName(
                    scoreInput.estimated_lethal_threat_target_ids,
                    targetUnit.unit_id
                );
            }
        }

        return RoundToInt((baseBonus + threatBonus) * (double)killBps / 10000.0);
    }

    private bool IsPriorityThreatTarget(IBattleAiScoreContext context, BattleUnitState targetUnit)
    {
        if (
            ResolveTargetRoleThreatMultiplierBasisPoints(context, targetUnit)
            > ThreatMultiplierBasisPointsDenominator
        )
        {
            return true;
        }
        return IsTargetCurrentlyThreateningAlly(context, targetUnit);
    }

    private bool IsTargetCurrentlyThreateningAlly(IBattleAiScoreContext context, BattleUnitState targetUnit)
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (
            state == null
            || ContextGridService(context) == null
            || actor == null
            || targetUnit == null
        )
        {
            return false;
        }
        int threatRange = ResolveUnitEffectiveHostileThreatRange(context, targetUnit);
        if (threatRange <= 0)
        {
            return false;
        }
        foreach (BattleUnitState allyUnit in state.GetUnitsTyped())
        {
            if (allyUnit == null || !allyUnit.is_alive || allyUnit.faction_id != actor.faction_id)
            {
                continue;
            }
            if (DistanceBetweenUnits(context, targetUnit, allyUnit) <= threatRange)
            {
                return true;
            }
        }
        return false;
    }

    private int ResolveUnitEffectiveHostileThreatRange(
        IBattleAiScoreContext context,
        BattleUnitState threatUnit
    )
    {
        if (context == null || threatUnit == null)
        {
            return 0;
        }
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            ContextSkillDefinitions(context);
        int bestRange = 0;
        foreach (StringName skillId in threatUnit.known_active_skill_ids)
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (IsEmpty(normalizedSkillId))
            {
                continue;
            }
            SkillDefinition skillDefinition = GetSkillDefinition(
                skillDefinitions,
                normalizedSkillId
            );
            if (skillDefinition == null || skillDefinition.CombatProfile == null)
            {
                continue;
            }
            if (
                BattleTypedNames.ToTargetFilter(skillDefinition.CombatProfile.TargetTeamFilter)
                == BattleTargetFilter.Ally
            )
            {
                continue;
            }
            List<CombatEffectDefinition> effectDefinitions = CollectRoleThreatEffectDefinitions(
                threatUnit,
                skillDefinition,
                ContextSkillCatalog(context)
            );
            if (!IsDamageSkill(effectDefinitions) && !IsControlSkill(effectDefinitions))
            {
                continue;
            }
            bestRange = Math.Max(
                bestRange,
                BattleRangeService.GetEffectiveSkillThreatRange(
                    threatUnit,
                    skillDefinition,
                    ContextSkillCatalog(context)
                )
            );
        }
        if (bestRange <= 0)
        {
            bestRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        }
        return bestRange;
    }

    private static int DistanceBetweenUnits(
        IBattleAiScoreContext context,
        BattleUnitState firstUnit,
        BattleUnitState secondUnit
    )
    {
        BattleGridService gridService = ContextGridService(context);
        if (gridService == null || firstUnit == null || secondUnit == null)
        {
            return 999999;
        }
        firstUnit.RefreshFootprint();
        secondUnit.RefreshFootprint();
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in firstUnit.occupied_coords)
        {
            foreach (Vector2I secondCoord in secondUnit.occupied_coords)
            {
                bestDistance = Math.Min(
                    bestDistance,
                    gridService.GetDistance(firstCoord, secondCoord)
                );
            }
        }
        return bestDistance;
    }
}
