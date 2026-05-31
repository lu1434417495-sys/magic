using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

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

    private sealed class DamageSaveEstimate
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

        public GDictionary ToDictionary()
        {
            return new GDictionary
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
                ["ability"] = Ability,
                ["save_tag"] = SaveTag,
                ["advantage_state"] = AdvantageState,
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

    private sealed class DamageEstimateBreakdown
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
        public GArray DamageEvents = new();
        public GArray Diagnostics = new();

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
                DamageEvents = DamageEvents?.Duplicate(true) ?? new GArray(),
                Diagnostics = Diagnostics?.Duplicate(true) ?? new GArray(),
            };
        }

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["hp_damage"] = HpDamage,
                ["damage"] = Damage,
                ["post_save_damage"] = PostSaveDamage,
                ["incoming_budget_damage"] = IncomingBudgetDamage,
                ["shield_absorbed"] = ShieldAbsorbed,
                ["shield_broken"] = ShieldBroken,
                ["stable_lethal"] = StableLethal,
                ["lethal_probability_basis_points"] = LethalProbabilityBasisPoints,
                ["save_estimates"] = SaveEstimatesToArray(SaveEstimates),
                ["damage_events"] = DamageEvents?.Duplicate(true) ?? new GArray(),
                ["diagnostics"] = Diagnostics?.Duplicate(true) ?? new GArray(),
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
        public GArray DamageEvents = new();
        public GArray Diagnostics = new();
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
                DamageEvents = preview.DamageEvents?.Duplicate(true) ?? new GArray(),
                Diagnostics = preview.Diagnostics?.Duplicate(true) ?? new GArray(),
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

    private static GArray SaveEstimatesToArray(IEnumerable<DamageSaveEstimate> estimates)
    {
        var result = new GArray();
        foreach (DamageSaveEstimate estimate in estimates ?? System.Array.Empty<DamageSaveEstimate>())
        {
            if (estimate != null && estimate.HasSave)
            {
                result.Add(estimate.ToDictionary());
            }
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
        IReadOnlyList<CombatEffectDef> effectDefs,
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
        CombatEffectDef pathStepEffect = FindPathStepAoeEffect(effectDefs);
        if (pathStepEffect == null)
        {
            pathStepEffect = metadata?.Effect;
        }
        if (pathStepEffect == null)
        {
            return;
        }
        CombatEffectDef damageEffect = BuildPathStepDamageEffect(pathStepEffect);
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
                RepeatEffects(new[] { damageEffect }, hitCount),
                targetUnit,
                ResolveSkillId(scoreInput.skill_def as SkillDef)
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
                rawPayoff -= estimatedDamage * _scoreProfile.damage_weight;
                continue;
            }
            rawPayoff += estimatedDamage * _scoreProfile.damage_weight;
            rawPayoff += estimatedShieldAbsorbed * Math.Max(_scoreProfile.damage_weight / 4, 1);
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
                    scoreInput.skill_def as SkillDef,
                    pathStepEffect,
                    hitCount
                )
            )
            {
                rawStatusPayoff += _scoreProfile.status_weight;
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

    private static List<PathStepHitCountEntry> ReadPathStepHitCountEntries(GDictionary metadata)
    {
        var result = new List<PathStepHitCountEntry>();
        GDictionary hitCounts = DictDictionary(
            metadata,
            "path_step_hit_counts_by_unit_id",
            new GDictionary()
        );
        foreach ((StringName unitId, int rawHitCount) in ReadStringNameIntEntries(hitCounts))
        {
            int hitCount = Math.Max(rawHitCount, 0);
            if (!IsEmpty(unitId) && hitCount > 0)
            {
                result.Add(new PathStepHitCountEntry { UnitId = unitId, HitCount = hitCount });
            }
        }
        return result;
    }

    private static GDictionary PathStepHitCountsToDictionary(
        IEnumerable<PathStepHitCountEntry> entries
    )
    {
        var result = new GDictionary();
        foreach (PathStepHitCountEntry entry in entries ?? System.Array.Empty<PathStepHitCountEntry>())
        {
            if (entry != null && !IsEmpty(entry.UnitId) && entry.HitCount > 0)
            {
                result[entry.UnitId] = entry.HitCount;
            }
        }
        return result;
    }

    private static CombatEffectDef FindPathStepAoeEffect(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null && effectDef.effect_type == PathStepAoeEffectType)
            {
                return effectDef;
            }
        }
        return null;
    }

    private static CombatEffectDef BuildPathStepDamageEffect(CombatEffectDef pathStepEffect)
    {
        if (pathStepEffect == null)
        {
            return null;
        }
        CombatEffectDef damageEffect = pathStepEffect.duplicate_for_runtime();
        if (damageEffect == null)
        {
            return null;
        }
        damageEffect.effect_type = "damage";
        return damageEffect;
    }

    private static bool PathStepRepeatStatusApplies(
        IBattleAiScoreContext context,
        SkillDef skillDef,
        CombatEffectDef pathStepEffect,
        int hitCount
    )
    {
        if (
            context == null
            || pathStepEffect == null
            || pathStepEffect.@params == null
            || hitCount <= 0
        )
        {
            return false;
        }
        StringName statusId = DictStringName(pathStepEffect.@params, "repeat_hit_status_id", "");
        if (IsEmpty(statusId))
        {
            return false;
        }
        int threshold = Math.Max(
            DictInt(pathStepEffect.@params, "repeat_hit_status_threshold", 1),
            1
        );
        if (hitCount < threshold)
        {
            return false;
        }
        int durationTu = DictInt(pathStepEffect.@params, "repeat_hit_status_duration_tu", 0);
        if (durationTu <= 0)
        {
            return false;
        }
        int minSkillLevel = Math.Max(
            DictInt(pathStepEffect.@params, "repeat_hit_status_min_skill_level", 0),
            0
        );
        StringName skillId = skillDef != null ? skillDef.skill_id : "";
        return GetContextSkillLevel(context, skillId) >= minSkillLevel;
    }

    private static List<CombatEffectDef> FilterEffectDefsForContext(
        IEnumerable<CombatEffectDef> effectDefs,
        IBattleAiScoreContext context,
        SkillDef skillDef
    )
    {
        var filteredEffectDefs = new List<CombatEffectDef>();
        bool shouldFilter = ContextUnitState(context) != null;
        int skillLevel = GetContextSkillLevel(context, skillDef != null ? skillDef.skill_id : "");
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef == null)
            {
                continue;
            }
            if (!IsEffectUnlockedForSkillLevel(effectDef, skillLevel, shouldFilter))
            {
                continue;
            }
            filteredEffectDefs.Add(effectDef);
        }
        return filteredEffectDefs;
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDef effectDef,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDef == null)
        {
            return false;
        }
        if (!shouldFilter)
        {
            return true;
        }
        int minLevel = Math.Max(effectDef.min_skill_level, 0);
        int maxLevel = effectDef.max_skill_level;
        if (skillLevel < minLevel)
        {
            return false;
        }
        return maxLevel < 0 || skillLevel <= maxLevel;
    }

    private int EstimateDamageForTarget(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        BattleUnitState targetUnit,
        StringName skillId = default
    )
    {
        return EstimateDamageForTargetResult(sourceUnit, effectDefs, targetUnit, skillId).Damage;
    }

    private DamageEstimateResult EstimateDamageForTargetResult(
        IBattleAiScoreContext context,
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        BattleUnitState targetUnit,
        StringName skillId = default
    )
    {
        string cacheKey = BuildDamageEstimateCacheKey(sourceUnit, effectDefs, targetUnit, skillId);
        Dictionary<string, object> cache = ContextScoreProjectionCache(context);
        if (
            !string.IsNullOrEmpty(cacheKey)
            && cache.TryGetValue(cacheKey, out object cached)
            && cached is DamageEstimateResult cachedEstimate
        )
        {
            return cachedEstimate.Clone();
        }

        DamageEstimateResult result = EstimateDamageForTargetResult(
            sourceUnit,
            effectDefs,
            targetUnit,
            skillId
        );
        if (!string.IsNullOrEmpty(cacheKey))
        {
            cache[cacheKey] = result.Clone();
        }
        return result;
    }

    private static string BuildDamageEstimateCacheKey(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        BattleUnitState targetUnit,
        StringName skillId
    )
    {
        if (sourceUnit == null || targetUnit == null || effectDefs == null)
        {
            return "";
        }

        var parts = new List<string>
        {
            "damage",
            sourceUnit.unit_id.ToString(),
            targetUnit.unit_id.ToString(),
            skillId.ToString(),
            targetUnit.current_hp.ToString(),
            targetUnit.current_shield_hp.ToString(),
            sourceUnit.current_hp.ToString(),
            sourceUnit.current_shield_hp.ToString(),
            effectDefs.Count.ToString(),
        };
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (effectDef == null)
            {
                parts.Add("null");
                continue;
            }
            parts.Add(effectDef.GetInstanceId().ToString());
            parts.Add(effectDef.effect_type.ToString());
            parts.Add(effectDef.damage_tag.ToString());
            parts.Add(effectDef.power.ToString());
            parts.Add(effectDef.damage_ratio_percent.ToString());
            parts.Add(effectDef.save_dc.ToString());
            parts.Add(effectDef.save_dc_mode.ToString());
            parts.Add(effectDef.save_dc_source_ability.ToString());
            parts.Add(effectDef.save_ability.ToString());
            parts.Add(effectDef.save_partial_on_success ? "1" : "0");
            parts.Add(effectDef.bonus_condition.ToString());
            parts.Add(effectDef.trigger_event.ToString());
            parts.Add(effectDef.trigger_condition.ToString());
            parts.Add(effectDef.trigger_status_id.ToString());
        }
        return string.Join("|", parts);
    }

    private DamageEstimateResult EstimateDamageForTargetResult(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        BattleUnitState targetUnit,
        StringName skillId = default
    )
    {
        if (_damageResolver != null)
        {
            var damageContext = new GDictionary();
            if (!IsEmpty(skillId))
            {
                damageContext["skill_id"] = skillId;
            }
            BattleDamagePreviewResult sequence = _damageResolver.preview_damage_sequence_typed(
                sourceUnit,
                targetUnit,
                ToEffectArray(effectDefs),
                damageContext,
                new GDictionary()
            );
            return NormalizeDamageSequenceEstimate(
                DamagePreviewSnapshot.FromPreviewResult(sequence)
            );
        }

        int total = 0;
        int postSaveTotal = 0;
        var saveEstimates = new List<DamageSaveEstimate>();
        var damageEstimates = new List<DamageEstimateBreakdown>();
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (effectDef == null || effectDef.effect_type != "damage")
            {
                continue;
            }
            BattleDamagePreviewRangeService.SkillDamagePreview damagePreview =
                BattleDamagePreviewRangeService.build_skill_damage_preview_typed(
                    sourceUnit,
                    new GArray { effectDef }
                );
            int baseDamage = EstimateDamageFromPreview(damagePreview);
            int bonusDamage = EstimateConditionalBonusDamage(effectDef, targetUnit);
            double multiplier = ResolveEffectDamageMultiplier(effectDef, targetUnit);
            int preSaveDamage = Math.Max(RoundToInt((baseDamage + bonusDamage) * multiplier), 0);
            DamageSaveEstimate saveEstimate = BuildDamageSaveEstimate(
                sourceUnit,
                targetUnit,
                effectDef,
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

    private DamageEstimateResult EstimateDamageForTargetWithPreviewEffect(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        BattleUnitState targetUnit,
        StringName skillId
    )
    {
        int totalHpDamage = 0;
        int totalPostSaveDamage = 0;
        int totalIncomingBudgetDamage = 0;
        int totalShieldAbsorbed = 0;
        bool stableLethal = false;
        int lethalProbabilityBasisPoints = 0;
        var saveEstimates = new List<DamageSaveEstimate>();
        var damageEstimates = new List<DamageEstimateBreakdown>();
        BattleUnitState workingTarget = targetUnit?.clone();
        if (workingTarget == null)
        {
            workingTarget = targetUnit;
        }
        var damageContext = new GDictionary();
        if (!IsEmpty(skillId))
        {
            damageContext["skill_id"] = skillId;
        }
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (effectDef == null || effectDef.effect_type != "damage")
            {
                continue;
            }
            int targetHpBefore = Math.Max(
                workingTarget?.current_hp ?? targetUnit?.current_hp ?? 1,
                1
            );
            BattleDamagePreviewResult effectPreview = _damageResolver.preview_damage_effect_typed(
                sourceUnit,
                workingTarget,
                effectDef,
                damageContext,
                new StringName("average"),
                new StringName("expected")
            );
            DamagePreviewSnapshot previewSnapshot = DamagePreviewSnapshot.FromPreviewResult(
                effectPreview
            );
            DamageEstimateResult normalized = NormalizeDamageSequenceEstimate(previewSnapshot);
            ApplyBranchLethalEstimate(normalized, previewSnapshot, targetHpBefore);
            int hpDamage = normalized.Damage;
            int postSaveDamage = normalized.PostSaveDamage;
            int incomingBudgetDamage = normalized.IncomingBudgetDamage;
            int shieldAbsorbed = normalized.ShieldAbsorbed;
            totalHpDamage += hpDamage;
            totalPostSaveDamage += postSaveDamage;
            totalIncomingBudgetDamage += incomingBudgetDamage;
            totalShieldAbsorbed += shieldAbsorbed;
            stableLethal = stableLethal || normalized.StableLethal;
            lethalProbabilityBasisPoints = Math.Max(
                lethalProbabilityBasisPoints,
                normalized.LethalProbabilityBasisPoints
            );
            saveEstimates.AddRange(CloneSaveEstimates(normalized.SaveEstimates));
            damageEstimates.AddRange(CloneDamageEstimates(normalized.DamageEstimates));
            if (previewSnapshot.TargetPreviewAfter is BattleUnitState nextTarget)
            {
                workingTarget = nextTarget;
            }
        }
        return new DamageEstimateResult
        {
            Damage = totalHpDamage,
            HpDamage = totalHpDamage,
            PostSaveDamage = totalPostSaveDamage,
            IncomingBudgetDamage = totalIncomingBudgetDamage,
            ShieldAbsorbed = totalShieldAbsorbed,
            StableLethal = stableLethal,
            LethalProbabilityBasisPoints = lethalProbabilityBasisPoints,
            SaveEstimates = saveEstimates,
            DamageEstimates = damageEstimates,
        };
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
            DamageEvents = preview.DamageEvents?.Duplicate(true) ?? new GArray(),
            Diagnostics = preview.Diagnostics?.Duplicate(true) ?? new GArray(),
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
        CombatEffectDef effectDef,
        int damageBeforeSave,
        StringName skillId
    )
    {
        var saveContext = new GDictionary();
        if (!IsEmpty(skillId))
        {
            saveContext["skill_id"] = skillId;
        }
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.estimate_save_success_probability_result(
            sourceUnit,
            targetUnit,
            effectDef,
            saveContext
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
        if (effectDef.save_partial_on_success && !probability.Immune)
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
            SavePartialOnSuccess = effectDef.save_partial_on_success,
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
        string targetKey = targetUnit.unit_id.ToString();
        GArray existing = DictArray(
            scoreInput.save_estimates_by_target_id,
            targetKey,
            new GArray()
        );
        foreach (DamageSaveEstimate estimate in estimates)
        {
            if (estimate != null && estimate.HasSave)
            {
                existing.Add(estimate.ToDictionary());
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
        string targetKey = targetUnit.unit_id.ToString();
        GArray existing = DictArray(
            scoreInput.damage_estimates_by_target_id,
            targetKey,
            new GArray()
        );
        foreach (DamageEstimateBreakdown estimate in estimates)
        {
            if (estimate != null)
            {
                existing.Add(estimate.ToDictionary());
            }
        }
        scoreInput.damage_estimates_by_target_id[targetKey] = existing;
    }

    private static StringName ResolveSkillId(SkillDef skillDef)
    {
        return skillDef != null ? skillDef.skill_id : "";
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
        CombatEffectDef effectDef,
        BattleUnitState targetUnit
    )
    {
        if (effectDef == null)
        {
            return 1.0;
        }
        double multiplier = GetPreResistanceDamageMultiplier(effectDef);
        if (HasBonusCondition(effectDef, targetUnit))
        {
            multiplier *= GetDamageRatioMultiplier(effectDef);
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
            Math.Max(estimatedDamage, 0) * _scoreProfile.damage_weight
            + Math.Max(estimatedStatusCount, 0) * _scoreProfile.status_weight
            + Math.Max(estimatedTerrainEffectCount, 0) * _scoreProfile.terrain_weight
            + Math.Max(estimatedHeightDelta, 0) * _scoreProfile.height_weight;
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
        int bonus = Math.Max(_scoreProfile.lethal_target_weight, 0);
        if (IsPriorityThreatTarget(context, targetUnit))
        {
            scoreInput.estimated_lethal_threat_target_count += 1;
            AppendUniqueStringName(
                scoreInput.estimated_lethal_threat_target_ids,
                targetUnit.unit_id
            );
            bonus += Math.Max(_scoreProfile.lethal_threat_target_weight, 0);
        }
        return bonus;
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
        GDictionary skillDefs = ContextSkillDefs(context);
        int bestRange = 0;
        foreach (StringName skillId in threatUnit.known_active_skill_ids)
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (IsEmpty(normalizedSkillId))
            {
                continue;
            }
            SkillDef skillDef = GetSkillDef(skillDefs, normalizedSkillId);
            if (skillDef == null || skillDef.combat_profile == null)
            {
                continue;
            }
            if (
                ProgressionDataUtils.to_string_name(skillDef.combat_profile.target_team_filter)
                == "ally"
            )
            {
                continue;
            }
            List<CombatEffectDef> effectDefs = CollectRoleThreatEffectDefs(
                threatUnit,
                skillDef
            );
            if (!IsDamageSkill(effectDefs) && !IsControlSkill(effectDefs))
            {
                continue;
            }
            bestRange = Math.Max(
                bestRange,
                BattleRangeService.get_effective_skill_threat_range(threatUnit, skillDef)
            );
        }
        if (bestRange <= 0)
        {
            bestRange = BattleRangeService.get_weapon_attack_range(threatUnit);
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
        firstUnit.refresh_footprint();
        secondUnit.refresh_footprint();
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in firstUnit.occupied_coords)
        {
            foreach (Vector2I secondCoord in secondUnit.occupied_coords)
            {
                bestDistance = Math.Min(
                    bestDistance,
                    gridService.get_distance(firstCoord, secondCoord)
                );
            }
        }
        return bestDistance;
    }
}
