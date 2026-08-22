#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using Godot;

internal static class EnemyContentDefinitionProjector
{
    internal static EnemyAiBrainDefinition ProjectBrain(EnemyAiBrainImportModel source)
    {
        var states = new List<EnemyAiStateDefinition>();
        foreach (EnemyAiStateImportModel state in source.States)
        {
            var actions = new List<EnemyAiActionDefinition>();
            foreach (EnemyAiActionImportModel action in state.Actions)
                actions.Add(ProjectAction(action));
            var slots = new List<EnemyAiGenerationSlotDefinition>();
            foreach (EnemyAiGenerationSlotJsonDto slot in state.GenerationSlots)
            {
                slots.Add(new EnemyAiGenerationSlotDefinition(
                    slot.SlotId, slot.SlotRole, slot.Order, Names(slot.AllowedAffordances),
                    Names(slot.ActionFamilies), slot.StyleTemplateActionId, slot.ScoreBucketId,
                    slot.TargetSelector, slot.DesiredMinDistance, slot.DesiredMaxDistance,
                    slot.DistanceReference, slot.SuppressionPolicy
                ));
            }
            states.Add(new EnemyAiStateDefinition(state.StateId, actions, slots));
        }

        var transitions = new List<EnemyAiTransitionRuleDefinition>();
        foreach (EnemyAiTransitionRuleJsonDto rule in source.TransitionRules)
        {
            var conditions = new List<EnemyAiTransitionConditionDefinition>();
            foreach (EnemyAiTransitionConditionJsonDto condition in rule.Conditions)
            {
                conditions.Add(new EnemyAiTransitionConditionDefinition(
                    condition.Predicate, condition.BasisPoints, condition.MaxDistance,
                    Names(condition.StateIds), Names(condition.Affordances)
                ));
            }
            transitions.Add(new EnemyAiTransitionRuleDefinition(
                rule.RuleId, rule.Order, Names(rule.FromStateIds), rule.TargetStateId,
                conditions, rule.DesignerNote
            ));
        }
        return new EnemyAiBrainDefinition(
            source.BrainId, source.DefaultStateId, ProjectScoreProfile(source.ScoreProfile),
            states, transitions
        );
    }

    internal static EnemyTemplateDefinition ProjectTemplate(
        EnemyTemplateJsonDto source,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        // This transient rules object computes the same weapon and derived-stat facts;
        // it is never a content source and is not retained in the published graph.
        using var facts = new EnemyTemplateDef
        {
            template_id = source.TemplateId,
            display_name = source.DisplayName,
            battle_sprite_asset_id = source.BattleSpriteAssetId,
            brain_id = source.BrainId,
            initial_state_id = source.InitialStateId,
            enemy_count = source.EnemyCount,
            body_size = source.BodySize,
            creature_level = source.CreatureLevel,
            hit_die_sides = source.HitDieSides,
            cognition_kind = source.CognitionKind,
            tags = NameArray(source.Tags),
            save_advantage_tags = NameArray(source.SaveAdvantageTags),
            save_disadvantage_tags = NameArray(source.SaveDisadvantageTags),
            save_immunity_tags = NameArray(source.SaveImmunityTags),
            damage_resistances = VariantDictionary(source.DamageResistances),
            attack_equipment_item_id = source.AttackEquipmentItemId,
            natural_weapon_damage_tag = source.NaturalWeaponDamageTag,
            natural_weapon_attack_range = source.NaturalWeaponAttackRange,
            base_attribute_overrides = VariantDictionary(source.BaseAttributeOverrides),
            skill_ids = NameArray(source.SkillIds),
            skill_level_map = VariantDictionary(source.SkillLevelMap),
            generated_core_skill_count = source.GeneratedCoreSkillCount,
            attribute_overrides = VariantDictionary(source.AttributeOverrides),
            target_rank = source.TargetRank,
        };
        WeaponProjection weapon = facts.GetWeaponProjectionTyped(itemDefinitions);
        var equipment = new List<EnemyBattleEquipmentDefinition>();
        foreach (EnemyBattleEquipmentJsonDto entry in source.BattleEquipmentEntries)
            equipment.Add(new EnemyBattleEquipmentDefinition(entry.SlotId, entry.ItemId, entry.Rarity, entry.CurrentDurability));
        var drops = new List<DropEntryDefinition>();
        foreach (EnemyDropEntryJsonDto entry in source.DropEntries)
            drops.Add(new DropEntryDefinition(entry.DropEntryId, entry.DropType, entry.ItemId, entry.Quantity));

        return new EnemyTemplateDefinition(
            source.TemplateId, source.DisplayName, source.BattleSpriteAssetId,
            source.BrainId, source.InitialStateId, source.EnemyCount, source.BodySize,
            source.CreatureLevel, source.HitDieSides,
            BattleCognitionContentRules.ToKind(source.CognitionKind), Names(source.Tags),
            Names(source.SaveAdvantageTags), Names(source.SaveDisadvantageTags),
            Names(source.SaveImmunityTags), NameDictionary(source.DamageResistances),
            source.AttackEquipmentItemId, equipment, source.NaturalWeaponDamageTag,
            source.NaturalWeaponAttackRange, IntDictionary(source.BaseAttributeOverrides),
            Names(source.SkillIds), IntDictionary(source.SkillLevelMap),
            source.GeneratedCoreSkillCount, IntDictionary(source.AttributeOverrides),
            source.TargetRank, drops,
            new EnemyTemplateDefinition.EnemyWeaponProjectionDefinition(weapon),
            facts.GetDerivedHpMaxTyped(), facts.GetDerivedAttackBonusTyped(itemDefinitions)
        );
    }

    internal static WildEncounterRosterDefinition ProjectRoster(EncounterRosterJsonDto source)
    {
        var stages = new List<WildEncounterRosterStageDefinition>();
        foreach (EncounterRosterStageJsonDto stage in source.Stages)
        {
            var units = new List<WildEncounterRosterUnitEntryDefinition>();
            foreach (EncounterRosterUnitJsonDto unit in stage.UnitEntries)
                units.Add(new WildEncounterRosterUnitEntryDefinition(unit.TemplateId, unit.Count, unit.DisplayName, unit.ActorId));
            stages.Add(new WildEncounterRosterStageDefinition(stage.Stage, units));
        }
        return new WildEncounterRosterDefinition(source.ProfileId, source.DisplayName, source.InitialStage, source.GrowthStepInterval, stages);
    }

    private static EnemyAiActionDefinition ProjectAction(EnemyAiActionImportModel action) =>
        action.Payload switch
        {
            MoveToAdvantagePositionActionPayloadJsonDto p => new MoveToAdvantagePositionActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, p.TargetSelector, p.DesiredMinDistance, p.DesiredMaxDistance, Names(p.RangeSkillIds), p.MinimumSafeDistance, p.SafeDistanceMargin, p.MinSurvivalMarginGainToEscape, p.MinDistanceProgressWhenBeyondBand, p.PositioningMode, p.HighGroundWeight, p.SafetyWeight, p.DistanceBandWeight, p.CandidateLimit),
            MoveToMultiUnitSkillPositionActionPayloadJsonDto p => new MoveToMultiUnitSkillPositionActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.TargetSelector, p.DesiredMinDistance, p.DesiredMaxDistance, p.DistanceReference, p.CandidatePoolLimit, p.CandidateGroupLimit, p.TargetCountWeight),
            MoveToRangeActionPayloadJsonDto p => new MoveToRangeActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, p.AiEvaluationMode, p.TargetSelector, p.DesiredMinDistance, p.DesiredMaxDistance, Names(p.RangeSkillIds), p.ScreeningMode, p.EnableAoeSetupPositioning, p.AoeSetupMinTargetCount, p.AoeSetupTargetCountWeight, p.AoeSetupImprovementWeight, p.AoeSetupFriendlyFirePenalty, p.ScreeningMinHpBasisPoints, p.ScreeningAllyMinAttackRange, p.ScreeningEnemyMaxContactRange, p.ScreeningThreatDistanceBuffer, p.ScreeningPathBonus),
            RetreatActionPayloadJsonDto p => new RetreatActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, p.TargetSelector, p.MinimumSafeDistance, p.UseDynamicThreatSafeDistance, p.SafeDistanceMargin),
            UseChargeActionPayloadJsonDto p => new UseChargeActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, p.SkillId, p.TargetSelector, p.MinimumChargeMoveDistance),
            UseChargePathAoeActionPayloadJsonDto p => new UseChargePathAoeActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.TargetSelector, p.MinimumHitCount, p.DesiredMinDistance, p.DesiredMaxDistance),
            UseGroundRepositionSkillActionPayloadJsonDto p => new UseGroundRepositionSkillActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.TargetSelector, p.MinimumSafeDistance, p.SafeDistanceMargin, p.DesiredMaxDistanceBonus, p.ActionBaseScore, p.MinSurvivalMarginGainToEscape, p.PositioningMode, p.HighGroundWeight),
            UseGroundSkillActionPayloadJsonDto p => new UseGroundSkillActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.MinimumHitCount, p.AllowEmptyGroundControl, p.AllowGroundControlSupplementPartialHits, p.MinimumGroundControlScore, p.MinimumAllyThreatHitCount, p.MaximumFriendlyFireTargetCount, p.AllowFriendlyLethal, p.ThreatMinimumSafeDistance, p.ThreatSafeDistanceMargin, p.DesiredMinDistance, p.DesiredMaxDistance, p.DistanceReference),
            UseMultiUnitSkillActionPayloadJsonDto p => new UseMultiUnitSkillActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.TargetSelector, p.DesiredMinDistance, p.DesiredMaxDistance, p.DistanceReference, p.CandidatePoolLimit, p.CandidateGroupLimit),
            UseRandomChainSkillActionPayloadJsonDto p => new UseRandomChainSkillActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.TargetSelector, p.DesiredMinDistance, p.DesiredMaxDistance, p.DistanceReference, p.MinimumCandidateCount),
            UseUnitSkillActionPayloadJsonDto p => new UseUnitSkillActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, Names(p.SkillIds), p.TargetSelector, p.MinimumEffectiveTargetCount, p.MaximumFriendlyFireTargetCount, p.AllowFriendlyLethal, p.DesiredMinDistance, p.DesiredMaxDistance, p.DistanceReference),
            WaitActionPayloadJsonDto p => new WaitActionDefinition(p.ActionId, p.ScoreBucketId, p.ActionIntent, p.ActiveRestActionBaseScore, p.ActiveRestMinStaminaResidue),
            _ => throw new InvalidOperationException($"Unprojectable enemy action kind '{action.Kind}'."),
        };

    private static BattleAiScoreProfileDefinition ProjectScoreProfile(BattleAiScoreProfileJsonDto? source)
    {
        if (source is null) return BattleAiScoreProfileDefinition.Default;
        BattleAiScoreProfileDefinition result = BattleAiScoreProfileDefinition.Default;
        foreach (PropertyInfo property in typeof(BattleAiScoreProfileJsonDto).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string path = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? "";
            object? value = property.GetValue(source);
            if (value is int scalar && result.TryWithScalar(path, scalar, out BattleAiScoreProfileDefinition patched)) result = patched;
        }
        return result with
        {
            MeteorFriendlyFireProfile = source.MeteorFriendlyFireProfile,
            ActionBaseScores = EnemyDefinitionCollections.FreezeDictionary(IntDictionary(source.ActionBaseScores)),
            BucketPriorities = EnemyDefinitionCollections.FreezeDictionary(IntDictionary(source.BucketPriorities)),
        };
    }

    private static List<StringName> Names(IEnumerable<string> values)
    {
        var result = new List<StringName>();
        foreach (string value in values ?? Array.Empty<string>()) result.Add(value);
        return result;
    }
    private static Godot.Collections.Array<StringName> NameArray(IEnumerable<string> values) => new(Names(values));
    private static Dictionary<StringName, int> IntDictionary(IReadOnlyDictionary<string, int> values)
    {
        var result = new Dictionary<StringName, int>();
        foreach ((string key, int value) in values) result[key] = value;
        return result;
    }
    private static Dictionary<StringName, StringName> NameDictionary(IReadOnlyDictionary<string, string> values)
    {
        var result = new Dictionary<StringName, StringName>();
        foreach ((string key, string value) in values) result[key] = value;
        return result;
    }
    private static Godot.Collections.Dictionary VariantDictionary<T>(IReadOnlyDictionary<string, T> values)
    {
        var result = new Godot.Collections.Dictionary();
        foreach ((string key, T value) in values) result[new StringName(key)] = Variant.From(value!);
        return result;
    }
}
