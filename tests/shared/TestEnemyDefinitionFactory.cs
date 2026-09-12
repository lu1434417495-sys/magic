using System;
using System.Collections.Generic;
using Godot;

internal static class TestEnemyDefinitionFactory
{
    internal static EnemyAiBrainDefinition Brain(
        StringName brainId,
        StringName defaultStateId,
        IReadOnlyList<EnemyAiStateDefinition> states,
        IReadOnlyList<EnemyAiTransitionRuleDefinition> transitionRules = null,
        BattleAiScoreProfileDefinition scoreProfile = null
    ) =>
        new(
            brainId ?? "",
            defaultStateId ?? "",
            scoreProfile ?? BattleAiScoreProfileDefinition.Default,
            states ?? Array.Empty<EnemyAiStateDefinition>(),
            transitionRules ?? Array.Empty<EnemyAiTransitionRuleDefinition>()
        );

    internal static EnemyAiBrainDefinition Brain(
        StringName brainId,
        StringName stateId,
        params EnemyAiActionDefinition[] actions
    ) =>
        Brain(
            brainId,
            stateId,
            new[] { State(stateId, actions) }
        );

    internal static EnemyAiStateDefinition State(
        StringName stateId,
        IReadOnlyList<EnemyAiActionDefinition> actions,
        IReadOnlyList<EnemyAiGenerationSlotDefinition> generationSlots = null
    ) =>
        new(
            stateId ?? "",
            actions ?? Array.Empty<EnemyAiActionDefinition>(),
            generationSlots ?? Array.Empty<EnemyAiGenerationSlotDefinition>()
        );

    internal static EnemyAiStateDefinition State(
        StringName stateId,
        params EnemyAiActionDefinition[] actions
    ) => State(stateId, actions, Array.Empty<EnemyAiGenerationSlotDefinition>());

    internal static EnemyAiGenerationSlotDefinition GenerationSlot(
        StringName slotId,
        StringName slotRole = default,
        int order = 0,
        IReadOnlyList<StringName> allowedAffordances = null,
        IReadOnlyList<StringName> actionFamilies = null,
        StringName styleTemplateActionId = default,
        StringName scoreBucketId = default,
        StringName targetSelector = default,
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1,
        StringName distanceReference = default,
        StringName suppressionPolicy = default
    ) =>
        new(
            slotId ?? "",
            OrDefault(slotRole, "offense"),
            order,
            allowedAffordances ?? Array.Empty<StringName>(),
            actionFamilies ?? Array.Empty<StringName>(),
            styleTemplateActionId ?? "",
            scoreBucketId ?? "",
            targetSelector ?? "",
            desiredMinDistance,
            desiredMaxDistance,
            distanceReference ?? "",
            OrDefault(suppressionPolicy, "suppress_matching_family")
        );

    internal static EnemyAiTransitionRuleDefinition TransitionRule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        IReadOnlyList<EnemyAiTransitionConditionDefinition> conditions,
        IReadOnlyList<StringName> fromStateIds = null,
        string designerNote = ""
    ) =>
        new(
            ruleId ?? "",
            order,
            fromStateIds ?? Array.Empty<StringName>(),
            targetStateId ?? "",
            conditions ?? Array.Empty<EnemyAiTransitionConditionDefinition>(),
            designerNote
        );

    internal static EnemyAiTransitionConditionDefinition TransitionCondition(
        StringName predicate,
        int basisPoints = -1,
        int maxDistance = -1,
        IReadOnlyList<StringName> stateIds = null,
        IReadOnlyList<StringName> affordances = null
    ) =>
        new(
            predicate ?? "",
            basisPoints,
            maxDistance,
            stateIds ?? Array.Empty<StringName>(),
            affordances ?? Array.Empty<StringName>()
        );

    internal static MoveToAdvantagePositionActionDefinition MoveToAdvantagePosition(
        StringName actionId,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int desiredMinDistance = 3,
        int desiredMaxDistance = 5,
        IReadOnlyList<StringName> rangeSkillIds = null,
        int minimumSafeDistance = 3,
        int safeDistanceMargin = 1,
        int minSurvivalMarginGainToEscape = 1,
        int minDistanceProgressWhenBeyondBand = 1,
        StringName positioningMode = default,
        int highGroundWeight = 60,
        int safetyWeight = 50,
        int distanceBandWeight = 20,
        int candidateLimit = 96
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            OrDefault(targetSelector, "nearest_enemy"),
            desiredMinDistance,
            desiredMaxDistance,
            rangeSkillIds ?? Array.Empty<StringName>(),
            minimumSafeDistance,
            safeDistanceMargin,
            minSurvivalMarginGainToEscape,
            minDistanceProgressWhenBeyondBand,
            OrDefault(positioningMode, "advantage"),
            highGroundWeight,
            safetyWeight,
            distanceBandWeight,
            candidateLimit
        );

    internal static MoveToMultiUnitSkillPositionActionDefinition MoveToMultiUnitSkillPosition(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1,
        StringName distanceReference = default,
        int candidatePoolLimit = 6,
        int candidateGroupLimit = 12,
        int targetCountWeight = 40
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            OrDefault(targetSelector, "nearest_enemy"),
            desiredMinDistance,
            desiredMaxDistance,
            distanceReference ?? "",
            candidatePoolLimit,
            candidateGroupLimit,
            targetCountWeight
        );

    internal static MoveToRangeActionDefinition MoveToRange(
        StringName actionId,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName aiEvaluationMode = default,
        StringName targetSelector = default,
        int desiredMinDistance = 1,
        int desiredMaxDistance = 1,
        IReadOnlyList<StringName> rangeSkillIds = null,
        StringName screeningMode = default,
        bool enableAoeSetupPositioning = true,
        int aoeSetupMinTargetCount = 2,
        int aoeSetupTargetCountWeight = 140,
        int aoeSetupImprovementWeight = 220,
        int aoeSetupFriendlyFirePenalty = 1000,
        int screeningMinHpBasisPoints = 4000,
        int screeningAllyMinAttackRange = 4,
        int screeningEnemyMaxContactRange = 2,
        int screeningThreatDistanceBuffer = 2,
        int screeningPathBonus = 45
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            OrDefault(aiEvaluationMode, "inline_decide"),
            OrDefault(targetSelector, "nearest_enemy"),
            desiredMinDistance,
            desiredMaxDistance,
            rangeSkillIds ?? Array.Empty<StringName>(),
            OrDefault(screeningMode, "none"),
            enableAoeSetupPositioning,
            aoeSetupMinTargetCount,
            aoeSetupTargetCountWeight,
            aoeSetupImprovementWeight,
            aoeSetupFriendlyFirePenalty,
            screeningMinHpBasisPoints,
            screeningAllyMinAttackRange,
            screeningEnemyMaxContactRange,
            screeningThreatDistanceBuffer,
            screeningPathBonus
        );

    internal static RetreatActionDefinition Retreat(
        StringName actionId,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int minimumSafeDistance = 3,
        bool useDynamicThreatSafeDistance = false,
        int safeDistanceMargin = 1
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            OrDefault(targetSelector, "nearest_enemy"),
            minimumSafeDistance,
            useDynamicThreatSafeDistance,
            safeDistanceMargin
        );

    internal static UseChargeActionDefinition UseCharge(
        StringName actionId,
        StringName skillId = default,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int minimumChargeMoveDistance = 3
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            OrDefault(skillId, "charge"),
            OrDefault(targetSelector, "nearest_enemy"),
            minimumChargeMoveDistance
        );

    internal static UseChargePathAoeActionDefinition UseChargePathAoe(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int minimumHitCount = 1,
        int desiredMinDistance = 1,
        int desiredMaxDistance = 1
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            OrDefault(targetSelector, "nearest_enemy"),
            minimumHitCount,
            desiredMinDistance,
            desiredMaxDistance
        );

    internal static UseGroundRepositionSkillActionDefinition UseGroundRepositionSkill(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int minimumSafeDistance = 3,
        int safeDistanceMargin = 1,
        int desiredMaxDistanceBonus = 2,
        int actionBaseScore = 1500,
        int minSurvivalMarginGainToEscape = 1,
        StringName positioningMode = default,
        int highGroundWeight = 100
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            OrDefault(targetSelector, "nearest_enemy"),
            minimumSafeDistance,
            safeDistanceMargin,
            desiredMaxDistanceBonus,
            actionBaseScore,
            minSurvivalMarginGainToEscape,
            OrDefault(positioningMode, "escape"),
            highGroundWeight
        );

    internal static UseGroundSkillActionDefinition UseGroundSkill(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        int minimumHitCount = 1,
        bool allowEmptyGroundControl = false,
        bool allowGroundControlSupplementPartialHits = false,
        int minimumGroundControlScore = 1,
        int minimumAllyThreatHitCount = 0,
        int maximumFriendlyFireTargetCount = 0,
        bool allowFriendlyLethal = false,
        int threatMinimumSafeDistance = 0,
        int threatSafeDistanceMargin = 0,
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1,
        StringName distanceReference = default
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            minimumHitCount,
            allowEmptyGroundControl,
            allowGroundControlSupplementPartialHits,
            minimumGroundControlScore,
            minimumAllyThreatHitCount,
            maximumFriendlyFireTargetCount,
            allowFriendlyLethal,
            threatMinimumSafeDistance,
            threatSafeDistanceMargin,
            desiredMinDistance,
            desiredMaxDistance,
            distanceReference ?? ""
        );

    internal static UseMultiUnitSkillActionDefinition UseMultiUnitSkill(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1,
        StringName distanceReference = default,
        int candidatePoolLimit = 6,
        int candidateGroupLimit = 12
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            OrDefault(targetSelector, "nearest_enemy"),
            desiredMinDistance,
            desiredMaxDistance,
            distanceReference ?? "",
            candidatePoolLimit,
            candidateGroupLimit
        );

    internal static UseRandomChainSkillActionDefinition UseRandomChainSkill(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1,
        StringName distanceReference = default,
        int minimumCandidateCount = 1
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            OrDefault(targetSelector, "nearest_enemy"),
            desiredMinDistance,
            desiredMaxDistance,
            OrDefault(distanceReference, "candidate_pool"),
            minimumCandidateCount
        );

    internal static UseUnitSkillActionDefinition UseUnitSkill(
        StringName actionId,
        IReadOnlyList<StringName> skillIds = null,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        StringName targetSelector = default,
        int minimumEffectiveTargetCount = 1,
        int maximumFriendlyFireTargetCount = 0,
        bool allowFriendlyLethal = false,
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1,
        StringName distanceReference = default
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            skillIds ?? Array.Empty<StringName>(),
            OrDefault(targetSelector, "nearest_enemy"),
            minimumEffectiveTargetCount,
            maximumFriendlyFireTargetCount,
            allowFriendlyLethal,
            desiredMinDistance,
            desiredMaxDistance,
            distanceReference ?? ""
        );

    internal static WaitActionDefinition Wait(
        StringName actionId,
        StringName scoreBucketId = default,
        StringName actionIntent = default,
        int activeRestActionBaseScore = 10,
        int activeRestMinStaminaResidue = 1
    ) =>
        new(
            actionId ?? "",
            scoreBucketId ?? "",
            OrDefault(actionIntent, "positioning"),
            activeRestActionBaseScore,
            activeRestMinStaminaResidue
        );

    internal static WildEncounterRosterDefinition Roster(
        StringName profileId,
        IReadOnlyList<WildEncounterRosterStageDefinition> stages,
        string displayName = "",
        int initialStage = 0,
        int growthStepInterval = 1
    ) =>
        new(
            profileId ?? "",
            displayName ?? "",
            initialStage,
            growthStepInterval,
            stages ?? Array.Empty<WildEncounterRosterStageDefinition>()
        );

    internal static WildEncounterRosterStageDefinition RosterStage(
        int stage,
        params WildEncounterRosterUnitEntryDefinition[] unitEntries
    ) => new(stage, unitEntries ?? Array.Empty<WildEncounterRosterUnitEntryDefinition>());

    internal static WildEncounterRosterUnitEntryDefinition RosterUnit(
        StringName templateId,
        int count = 1,
        string displayName = "",
        StringName actorId = default
    ) => new(templateId ?? "", count, displayName ?? "", actorId ?? "");

    private static StringName OrDefault(StringName value, StringName fallback) =>
        value == null || value == "" ? fallback : value;
}

internal sealed class TestEnemyTemplateDefinitionBuilder
{
    internal StringName TemplateId { get; set; } = "";
    internal string DisplayName { get; set; } = "";
    internal StringName BattleSpriteAssetId { get; set; } = "";
    internal StringName BrainId { get; set; } = "";
    internal StringName InitialStateId { get; set; } = "";
    internal int EnemyCount { get; set; } = 1;
    internal int BodySize { get; set; } = BattleUnitState.BodySizeMedium;
    internal int CreatureLevel { get; set; } = 1;
    internal int HitDieSides { get; set; } = 8;
    internal StringName CognitionKind { get; set; } = "";
    internal List<StringName> Tags { get; } = new();
    internal List<StringName> SaveAdvantageTags { get; } = new();
    internal List<StringName> SaveDisadvantageTags { get; } = new();
    internal List<StringName> SaveImmunityTags { get; } = new();
    internal Dictionary<StringName, StringName> DamageResistances { get; } = new();
    internal StringName AttackEquipmentItemId { get; set; } = "";
    internal List<EnemyBattleEquipmentDefinition> BattleEquipmentEntries { get; } = new();
    internal StringName NaturalWeaponDamageTag { get; set; } = "";
    internal int NaturalWeaponAttackRange { get; set; } = 1;
    internal Dictionary<StringName, int> BaseAttributeOverrides { get; } = new();
    internal List<StringName> SkillIds { get; } = new();
    internal Dictionary<StringName, int> SkillLevels { get; } = new();
    internal int GeneratedCoreSkillCount { get; set; }
    internal Dictionary<StringName, int> AttributeOverrides { get; } = new();
    internal StringName TargetRank { get; set; } = "normal";
    internal List<DropEntryDefinition> DropEntries { get; } = new();

    internal EnemyTemplateDefinition Build(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null
    )
    {
        EnemyTemplateProjectionFacts facts = EnemyTemplateProjectionRules.Project(
            BuildProjectionInput(),
            itemDefinitions
        );
        return new EnemyTemplateDefinition(
            TemplateId,
            DisplayName,
            BattleSpriteAssetId,
            BrainId,
            InitialStateId,
            EnemyCount,
            BodySize,
            CreatureLevel,
            HitDieSides,
            BattleCognitionContentRules.ToKind(CognitionKind),
            Tags,
            SaveAdvantageTags,
            SaveDisadvantageTags,
            SaveImmunityTags,
            DamageResistances,
            AttackEquipmentItemId,
            BattleEquipmentEntries,
            NaturalWeaponDamageTag,
            NaturalWeaponAttackRange,
            BaseAttributeOverrides,
            SkillIds,
            SkillLevels,
            GeneratedCoreSkillCount,
            AttributeOverrides,
            TargetRank,
            DropEntries,
            facts.Weapon,
            facts.DerivedHpMax,
            facts.DerivedAttackBonus
        );
    }

    internal EnemyTemplateProjectionInput BuildProjectionInput() =>
        new(
            BodySize,
            CreatureLevel,
            HitDieSides,
            Tags,
            AttackEquipmentItemId,
            NaturalWeaponDamageTag,
            NaturalWeaponAttackRange,
            BaseAttributeOverrides
        );

    internal int GetDerivedHpMax() =>
        EnemyTemplateProjectionRules.DeriveHpMax(BuildProjectionInput());

    internal int GetDerivedAttackBonus(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null
    )
    {
        EnemyTemplateProjectionInput input = BuildProjectionInput();
        EnemyTemplateDefinition.EnemyWeaponProjectionDefinition weapon =
            EnemyTemplateProjectionRules.ProjectWeapon(input, itemDefinitions);
        return EnemyTemplateProjectionRules.DeriveAttackBonus(input, weapon);
    }
}
