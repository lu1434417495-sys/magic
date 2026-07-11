using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class ProfessionDefinition
{
    private static readonly StringName ReactivationAuto = "auto";
    private static readonly StringName ReactivationManual = "manual";
    private static readonly StringName DependencyCountWhenHidden = "count_when_hidden";
    private static readonly StringName DependencyIgnoreWhenHidden = "ignore_when_hidden";
    private static readonly StringName BabProgressionFull = "full";
    private static readonly StringName BabProgressionThreeQuarter = "three_quarter";
    private static readonly StringName BabProgressionHalf = "half";

    public ProfessionDefinition(
        StringName professionId,
        string displayName,
        string description,
        int maxRank,
        int hitDieSides,
        StringName babProgression,
        bool isInitialProfession,
        StringName unlockKnowledgeId,
        ProfessionPromotionRequirementDefinition unlockRequirement,
        IReadOnlyList<ProfessionRankRequirementDefinition> rankRequirements,
        IReadOnlyList<ProfessionGrantedSkillDefinition> grantedSkills,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<ProfessionActiveConditionDefinition> activeConditions,
        StringName reactivationMode,
        StringName dependencyVisibilityMode
    )
    {
        ProfessionId = professionId;
        DisplayName = ProgressionDefinitionProjection.RequireString(
            displayName,
            "ProfessionDefinition.DisplayName"
        );
        Description = ProgressionDefinitionProjection.RequireString(
            description,
            "ProfessionDefinition.Description"
        );
        MaxRank = maxRank;
        HitDieSides = hitDieSides;
        BabProgression = babProgression;
        IsInitialProfession = isInitialProfession;
        UnlockKnowledgeId = unlockKnowledgeId;
        UnlockRequirement = unlockRequirement;
        RankRequirements = ProgressionDefinitionProjection.FreezeValues(
            rankRequirements,
            "ProfessionDefinition.RankRequirements"
        );
        GrantedSkills = ProgressionDefinitionProjection.FreezeValues(
            grantedSkills,
            "ProfessionDefinition.GrantedSkills"
        );
        AttributeModifiers = ProgressionDefinitionProjection.FreezeValues(
            attributeModifiers,
            "ProfessionDefinition.AttributeModifiers"
        );
        ActiveConditions = ProgressionDefinitionProjection.FreezeValues(
            activeConditions,
            "ProfessionDefinition.ActiveConditions"
        );
        ReactivationMode = reactivationMode;
        DependencyVisibilityMode = dependencyVisibilityMode;
    }

    public StringName ProfessionId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int MaxRank { get; }
    public int HitDieSides { get; }
    public StringName BabProgression { get; }
    public bool IsInitialProfession { get; }
    public StringName UnlockKnowledgeId { get; }
    public ProfessionPromotionRequirementDefinition UnlockRequirement { get; }
    public IReadOnlyList<ProfessionRankRequirementDefinition> RankRequirements { get; }
    public IReadOnlyList<ProfessionGrantedSkillDefinition> GrantedSkills { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<ProfessionActiveConditionDefinition> ActiveConditions { get; }
    public StringName ReactivationMode { get; }
    public StringName DependencyVisibilityMode { get; }

    internal ProfessionBaseAttackProgression BabProgressionKind =>
        ToBabProgression(BabProgression);
    internal ProfessionReactivationMode ReactivationModeKind =>
        ToReactivationMode(ReactivationMode);
    internal ProfessionDependencyVisibilityMode DependencyVisibilityModeKind =>
        ToDependencyVisibilityMode(DependencyVisibilityMode);

    public bool RequiresKnowledgeUnlock() => !IsInitialProfession;

    public ProfessionRankRequirementDefinition GetRankRequirement(int targetRank)
    {
        foreach (ProfessionRankRequirementDefinition requirement in RankRequirements)
        {
            if (requirement.TargetRank == targetRank)
                return requirement;
        }

        return null;
    }

    public IReadOnlyList<ProfessionGrantedSkillDefinition> GetGrantedSkillsForRank(
        int targetRank
    )
    {
        var result = new List<ProfessionGrantedSkillDefinition>();
        foreach (ProfessionGrantedSkillDefinition grantedSkill in GrantedSkills)
        {
            if (grantedSkill.UnlockRank == targetRank)
                result.Add(grantedSkill);
        }

        return new ReadOnlyCollection<ProfessionGrantedSkillDefinition>(result);
    }

    internal static ProfessionDefinition FromResource(ProfessionDef source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string path = $"profession.{ProgressionDefinitionProjection.PathId(source.profession_id)}";

        ProgressionDefinitionProjection.RequireKnown(
            source.BabProgressionKind != ProfessionBaseAttackProgression.Unknown,
            $"{path}.bab_progression",
            source.bab_progression
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.ReactivationModeKind != ProfessionReactivationMode.Unknown,
            $"{path}.reactivation_mode",
            source.reactivation_mode
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.DependencyVisibilityModeKind
                != ProfessionDependencyVisibilityMode.Unknown,
            $"{path}.dependency_visibility_mode",
            source.dependency_visibility_mode
        );

        return new ProfessionDefinition(
            source.profession_id,
            ProgressionDefinitionProjection.RequireString(
                source.display_name,
                $"{path}.display_name"
            ),
            ProgressionDefinitionProjection.RequireString(
                source.description,
                $"{path}.description"
            ),
            source.max_rank,
            source.hit_die_sides,
            source.bab_progression,
            source.is_initial_profession,
            source.unlock_knowledge_id,
            source.unlock_requirement == null
                ? null
                : ProfessionPromotionRequirementDefinition.FromResource(
                    source.unlock_requirement,
                    $"{path}.unlock_requirement"
                ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RankRequirementsProjectionBorrowed,
                $"{path}.rank_requirements",
                ProfessionRankRequirementDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.GrantedSkillsProjectionBorrowed,
                $"{path}.granted_skills",
                ProfessionGrantedSkillDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.AttributeModifiersProjectionBorrowed,
                $"{path}.attribute_modifiers",
                static (value, _) => AttributeModifierDefinition.FromResource(value)
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.ActiveConditionsProjectionBorrowed,
                $"{path}.active_conditions",
                ProfessionActiveConditionDefinition.FromResource
            ),
            source.reactivation_mode,
            source.dependency_visibility_mode
        );
    }

    private static ProfessionBaseAttackProgression ToBabProgression(StringName value)
    {
        if (value == BabProgressionFull)
            return ProfessionBaseAttackProgression.Full;
        if (value == BabProgressionThreeQuarter)
            return ProfessionBaseAttackProgression.ThreeQuarter;
        if (value == BabProgressionHalf)
            return ProfessionBaseAttackProgression.Half;
        return ProfessionBaseAttackProgression.Unknown;
    }

    private static ProfessionReactivationMode ToReactivationMode(StringName value)
    {
        if (value == ReactivationAuto)
            return ProfessionReactivationMode.Auto;
        if (value == ReactivationManual)
            return ProfessionReactivationMode.Manual;
        return ProfessionReactivationMode.Unknown;
    }

    private static ProfessionDependencyVisibilityMode ToDependencyVisibilityMode(
        StringName value
    )
    {
        if (value == DependencyCountWhenHidden)
            return ProfessionDependencyVisibilityMode.CountWhenHidden;
        if (value == DependencyIgnoreWhenHidden)
            return ProfessionDependencyVisibilityMode.IgnoreWhenHidden;
        return ProfessionDependencyVisibilityMode.Unknown;
    }
}
