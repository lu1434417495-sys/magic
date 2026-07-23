using System;
using System.Collections.Generic;
using Godot;

internal interface IRandomStartingManaPoolRoller
{
    int Roll();
}

internal sealed class TrueRandomStartingManaPoolRoller : IRandomStartingManaPoolRoller
{
    internal const int MinimumManaPool = 0;
    internal const int MaximumManaPool = 40;

    public int Roll() =>
        TrueRandomSeedService.RandiRange(MinimumManaPool, MaximumManaPool);
}

internal sealed class RandomStartingSkillResourceSupportService
{
    internal static readonly StringName BasicMeditationSkillId = "basic_meditation";

    private static readonly StringName MeditationTag = "meditation";
    private static readonly StringName MpMaxAttributeId = "mp_max";
    private readonly IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions;
    private readonly IRandomStartingManaPoolRoller _manaPoolRoller;

    internal RandomStartingSkillResourceSupportService(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
        : this(skillDefinitions, new TrueRandomStartingManaPoolRoller()) { }

    internal RandomStartingSkillResourceSupportService(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IRandomStartingManaPoolRoller manaPoolRoller
    )
    {
        _skillDefinitions =
            skillDefinitions
            ?? throw new ArgumentNullException(nameof(skillDefinitions));
        _manaPoolRoller =
            manaPoolRoller
            ?? throw new ArgumentNullException(nameof(manaPoolRoller));
    }

    internal int ApplyManaSupport(
        PartyMemberState memberState,
        SkillDefinition randomStartingSkillDefinition
    )
    {
        if (memberState?.progression == null || randomStartingSkillDefinition?.CombatProfile == null)
            return 0;

        UnitProgress progression = memberState.progression;
        UnitSkillProgress randomSkillProgress = progression.GetSkillProgress(
            randomStartingSkillDefinition.SkillId
        );
        if (randomSkillProgress == null || !randomSkillProgress.is_learned)
            return 0;

        int skillLevel = Mathf.Max(randomSkillProgress.skill_level, 0);
        int mpCost = randomStartingSkillDefinition
            .CombatProfile.GetEffectiveResourceCostValues(skillLevel)
            .MpCost;
        if (mpCost <= 0)
            return 0;

        ResolveBasicMeditationDefinition();
        UnitBaseAttributes baseAttributes =
            progression.unit_base_attributes
            ?? throw new InvalidOperationException(
                "Random starting mana support requires unit base attributes."
            );
        int rolledManaPool = _manaPoolRoller.Roll();
        if (
            rolledManaPool < TrueRandomStartingManaPoolRoller.MinimumManaPool
            || rolledManaPool > TrueRandomStartingManaPoolRoller.MaximumManaPool
        )
        {
            throw new InvalidOperationException(
                $"Random starting mana pool must be between {TrueRandomStartingManaPoolRoller.MinimumManaPool} and {TrueRandomStartingManaPoolRoller.MaximumManaPool}, got {rolledManaPool}."
            );
        }

        UnitSkillProgress meditationProgress = progression.GetSkillProgress(
            BasicMeditationSkillId
        );
        if (meditationProgress == null)
        {
            meditationProgress = new UnitSkillProgress
            {
                skill_id = BasicMeditationSkillId,
                is_learned = true,
                skill_level = 0,
                is_core = false,
                granted_source_type = UnitSkillProgress.ToStringName(
                    UnitSkillGrantSourceType.Player
                ),
                granted_source_id = randomStartingSkillDefinition.SkillId,
            };
        }
        else
        {
            meditationProgress.is_learned = true;
        }

        progression.SetSkillProgress(meditationProgress);
        progression.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );

        baseAttributes.SetAttributeValue(MpMaxAttributeId, rolledManaPool);
        memberState.SetCurrentMp(rolledManaPool);
        return rolledManaPool;
    }

    private SkillDefinition ResolveBasicMeditationDefinition()
    {
        if (
            !_skillDefinitions.TryGetValue(
                BasicMeditationSkillId,
                out SkillDefinition meditationDefinition
            )
            || meditationDefinition == null
            || meditationDefinition.SkillTypeKind != SkillTypeKind.Passive
            || meditationDefinition.PracticeTierKind != SkillPracticeTierKind.Basic
            || meditationDefinition.Tags.Count != 1
            || !meditationDefinition.HasTag(MeditationTag)
        )
        {
            throw new InvalidOperationException(
                $"Random starting MP skills require a valid basic meditation skill: {BasicMeditationSkillId}."
            );
        }

        return meditationDefinition;
    }
}
