#nullable enable

using System;
using System.Collections.Generic;
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
        List<StringName> tags = Names(source.Tags);
        Dictionary<StringName, int> baseAttributeOverrides = IntDictionary(
            source.BaseAttributeOverrides
        );
        EnemyTemplateProjectionFacts projectedFacts = EnemyTemplateProjectionRules.Project(
            new EnemyTemplateProjectionInput(
                source.BodySize,
                source.CreatureLevel,
                source.HitDieSides,
                tags,
                source.AttackEquipmentItemId,
                source.NaturalWeaponDamageTag,
                source.NaturalWeaponAttackRange,
                baseAttributeOverrides
            ),
            itemDefinitions
        );
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
            BattleCognitionContentRules.ToKind(source.CognitionKind), tags,
            Names(source.SaveAdvantageTags), Names(source.SaveDisadvantageTags),
            Names(source.SaveImmunityTags), NameDictionary(source.DamageResistances),
            source.AttackEquipmentItemId, equipment, source.NaturalWeaponDamageTag,
            source.NaturalWeaponAttackRange, baseAttributeOverrides,
            Names(source.SkillIds), IntDictionary(source.SkillLevelMap),
            source.GeneratedCoreSkillCount, IntDictionary(source.AttributeOverrides),
            source.TargetRank, drops,
            projectedFacts.Weapon,
            projectedFacts.DerivedHpMax,
            projectedFacts.DerivedAttackBonus
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

    internal static BattleAiScoreProfileDefinition ProjectScoreProfile(
        BattleAiScoreProfileJsonDto? source
    )
    {
        if (source is null)
            return BattleAiScoreProfileDefinition.Default;

        return new BattleAiScoreProfileDefinition
        {
            DamageWeight = source.DamageWeight,
            HealWeight = source.HealWeight,
            StatusWeight = source.StatusWeight,
            TerrainWeight = source.TerrainWeight,
            HeightWeight = source.HeightWeight,
            LethalTargetWeight = source.LethalTargetWeight,
            LethalThreatTargetWeight = source.LethalThreatTargetWeight,
            TargetCountWeight = source.TargetCountWeight,
            FriendlyFireDamageWeight = source.FriendlyFireDamageWeight,
            FriendlyFireTargetWeight = source.FriendlyFireTargetWeight,
            FriendlyControlTargetWeight = source.FriendlyControlTargetWeight,
            FriendlyLethalTargetWeight = source.FriendlyLethalTargetWeight,
            ApCostWeight = source.ApCostWeight,
            MpCostWeight = source.MpCostWeight,
            StaminaCostWeight = source.StaminaCostWeight,
            AuraCostWeight = source.AuraCostWeight,
            CooldownWeight = source.CooldownWeight,
            DelayedResolutionCostPer5Tu = source.DelayedResolutionCostPer5Tu,
            MovementCostWeight = source.MovementCostWeight,
            MpReserveFloorBp = source.MpReserveFloorBp,
            MpReservePressureWeight = source.MpReservePressureWeight,
            MpReserveBreachPenalty = source.MpReserveBreachPenalty,
            StaminaReserveFloorBp = source.StaminaReserveFloorBp,
            StaminaReservePressureWeight = source.StaminaReservePressureWeight,
            StaminaReserveBreachPenalty = source.StaminaReserveBreachPenalty,
            AuraReserveFloorBp = source.AuraReserveFloorBp,
            AuraReservePressureWeight = source.AuraReservePressureWeight,
            AuraReserveBreachPenalty = source.AuraReserveBreachPenalty,
            ResourceConservationWeight = source.ResourceConservationWeight,
            PositionBaseScore = source.PositionBaseScore,
            PositionDistanceStep = source.PositionDistanceStep,
            PositionUndershootPenalty = source.PositionUndershootPenalty,
            PositionOvershootPenalty = source.PositionOvershootPenalty,
            SurvivalMarginGainWeight = source.SurvivalMarginGainWeight,
            PostActionThreatDamageWeight = source.PostActionThreatDamageWeight,
            PostActionThreatCountWeight = source.PostActionThreatCountWeight,
            LethalSurvivalRiskPenalty = source.LethalSurvivalRiskPenalty,
            IncomingThreatReliefWeight = source.IncomingThreatReliefWeight,
            LowHpUrgencyThresholdBp = source.LowHpUrgencyThresholdBp,
            LowHpUrgencyWeight = source.LowHpUrgencyWeight,
            ExecuteTargetHpThresholdBp = source.ExecuteTargetHpThresholdBp,
            ExecuteBonusWeight = source.ExecuteBonusWeight,
            OverkillDamagePenaltyWeight = source.OverkillDamagePenaltyWeight,
            RoleThreatMinEffectiveRange = source.RoleThreatMinEffectiveRange,
            RoleThreatDistanceWindow = source.RoleThreatDistanceWindow,
            RoleThreatMaxApproachDistance = source.RoleThreatMaxApproachDistance,
            RoleThreatMaxContactRange = source.RoleThreatMaxContactRange,
            RoleThreatInRangeScoreStep = source.RoleThreatInRangeScoreStep,
            EnemyTargetCountWeight = source.EnemyTargetCountWeight,
            ChainEnemyTargetWeight = source.ChainEnemyTargetWeight,
            FocusFireWoundedTargetWeight = source.FocusFireWoundedTargetWeight,
            HitRateReliabilityWeight = source.HitRateReliabilityWeight,
            SaveReliableDamageWeight = source.SaveReliableDamageWeight,
            ShieldAbsorbedWeight = source.ShieldAbsorbedWeight,
            ControlWeight = source.ControlWeight,
            GroundControlWeight = source.GroundControlWeight,
            StatusRedundancyPenalty = source.StatusRedundancyPenalty,
            PositionObjectiveWeight = source.PositionObjectiveWeight,
            SafeDistanceAdherenceWeight = source.SafeDistanceAdherenceWeight,
            ThreatHealerBiasBasisPoints = source.ThreatHealerBiasBasisPoints,
            ThreatControlBiasBasisPoints = source.ThreatControlBiasBasisPoints,
            ThreatRangedBiasBasisPoints = source.ThreatRangedBiasBasisPoints,
            ThreatRangeStepBiasBasisPoints = source.ThreatRangeStepBiasBasisPoints,
            ThreatMultiplierCapBasisPoints = source.ThreatMultiplierCapBasisPoints,
            MeteorHighPriorityThreatMultiplierBp =
                source.MeteorHighPriorityThreatMultiplierBp,
            MeteorHighPriorityDamageHpPercent = source.MeteorHighPriorityDamageHpPercent,
            MeteorHighPriorityTargetPriorityScore =
                source.MeteorHighPriorityTargetPriorityScore,
            MeteorTopThreatRank = source.MeteorTopThreatRank,
            MeteorFriendlyFireProfile = source.MeteorFriendlyFireProfile,
            MeteorFriendlyFireSoftExpectedHpPercent =
                source.MeteorFriendlyFireSoftExpectedHpPercent,
            MeteorFriendlyFireHardExpectedHpPercent =
                source.MeteorFriendlyFireHardExpectedHpPercent,
            MeteorFriendlyFireHardWorstCaseHpPercent =
                source.MeteorFriendlyFireHardWorstCaseHpPercent,
            ActionBaseScores = EnemyDefinitionCollections.FreezeDictionary(IntDictionary(source.ActionBaseScores)),
            DefaultBucketPriority = source.DefaultBucketPriority,
            BucketPriorities = EnemyDefinitionCollections.FreezeDictionary(IntDictionary(source.BucketPriorities)),
        };
    }

    private static List<StringName> Names(IEnumerable<string> values)
    {
        var result = new List<StringName>();
        foreach (string value in values ?? Array.Empty<string>()) result.Add(value);
        return result;
    }
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
}
