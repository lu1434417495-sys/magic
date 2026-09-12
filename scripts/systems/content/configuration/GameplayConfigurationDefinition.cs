#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

public sealed record SettlementShopItemDefinition(
    StringName ItemId,
    int MinQuantity,
    int MaxQuantity,
    int Weight,
    int PriceBasisPoints
);

public sealed class SettlementShopDefinition
{
    public SettlementShopDefinition(
        StringName interactionScriptId,
        StringName shopId,
        string title,
        int refreshIntervalSteps,
        IReadOnlyList<SettlementShopItemDefinition> guaranteedItems,
        IReadOnlyList<SettlementShopItemDefinition> randomPool,
        int maxRandomItems,
        int uniqueEquipmentOfferChancePercent,
        int defaultPriceBasisPoints
    )
    {
        InteractionScriptId = interactionScriptId;
        ShopId = shopId;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        RefreshIntervalSteps = refreshIntervalSteps;
        GuaranteedItems = Freeze(guaranteedItems);
        RandomPool = Freeze(randomPool);
        MaxRandomItems = maxRandomItems;
        UniqueEquipmentOfferChancePercent = uniqueEquipmentOfferChancePercent;
        DefaultPriceBasisPoints = defaultPriceBasisPoints;
    }

    public StringName InteractionScriptId { get; }
    public StringName ShopId { get; }
    public string Title { get; }
    public int RefreshIntervalSteps { get; }
    public IReadOnlyList<SettlementShopItemDefinition> GuaranteedItems { get; }
    public IReadOnlyList<SettlementShopItemDefinition> RandomPool { get; }
    public int MaxRandomItems { get; }
    public int UniqueEquipmentOfferChancePercent { get; }
    public int DefaultPriceBasisPoints { get; }

    private static IReadOnlyList<SettlementShopItemDefinition> Freeze(
        IReadOnlyList<SettlementShopItemDefinition>? source
    ) => Array.AsReadOnly((source ?? Array.Empty<SettlementShopItemDefinition>()).ToArray());
}

public sealed record NewGameStartingWeaponRuleDefinition(
    IReadOnlyList<StringName> RequiredSkillTagsAny,
    StringName ItemId
);

public sealed class NewGameMemberDefinition
{
    public NewGameMemberDefinition(
        StringName memberId,
        string displayName,
        StringName factionId,
        StringName portraitId,
        StringName controlMode,
        StringName raceId,
        StringName subraceId,
        int ageYears,
        StringName ageProfileId,
        StringName naturalAgeStageId,
        StringName effectiveAgeStageId,
        StringName bodySizeCategory,
        int currentMp,
        int storageSpace,
        IReadOnlyDictionary<StringName, int> baseAttributes,
        IReadOnlyList<StringName> startingSkillIds
    )
    {
        MemberId = memberId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        FactionId = factionId;
        PortraitId = portraitId;
        ControlMode = controlMode;
        RaceId = raceId;
        SubraceId = subraceId;
        AgeYears = ageYears;
        AgeProfileId = ageProfileId;
        NaturalAgeStageId = naturalAgeStageId;
        EffectiveAgeStageId = effectiveAgeStageId;
        BodySizeCategory = bodySizeCategory;
        CurrentMp = currentMp;
        StorageSpace = storageSpace;
        BaseAttributes = new ReadOnlyDictionary<StringName, int>(
            new Dictionary<StringName, int>(baseAttributes ?? new Dictionary<StringName, int>())
        );
        StartingSkillIds = Array.AsReadOnly(
            (startingSkillIds ?? Array.Empty<StringName>()).ToArray()
        );
    }

    public StringName MemberId { get; }
    public string DisplayName { get; }
    public StringName FactionId { get; }
    public StringName PortraitId { get; }
    public StringName ControlMode { get; }
    public StringName RaceId { get; }
    public StringName SubraceId { get; }
    public int AgeYears { get; }
    public StringName AgeProfileId { get; }
    public StringName NaturalAgeStageId { get; }
    public StringName EffectiveAgeStageId { get; }
    public StringName BodySizeCategory { get; }
    public int CurrentMp { get; }
    public int StorageSpace { get; }
    public IReadOnlyDictionary<StringName, int> BaseAttributes { get; }
    public IReadOnlyList<StringName> StartingSkillIds { get; }
}

public sealed class NewGamePartyDefinition
{
    public NewGamePartyDefinition(
        int gold,
        StringName leaderMemberId,
        StringName mainCharacterMemberId,
        IReadOnlyList<StringName> activeMemberIds,
        IReadOnlyList<StringName> reserveMemberIds,
        IReadOnlyList<NewGameMemberDefinition> members,
        IReadOnlyList<NewGameStartingWeaponRuleDefinition> startingWeaponRules,
        StringName startingWeaponFallbackItemId
    )
    {
        Gold = gold;
        LeaderMemberId = leaderMemberId;
        MainCharacterMemberId = mainCharacterMemberId;
        ActiveMemberIds = Array.AsReadOnly((activeMemberIds ?? Array.Empty<StringName>()).ToArray());
        ReserveMemberIds = Array.AsReadOnly((reserveMemberIds ?? Array.Empty<StringName>()).ToArray());
        Members = Array.AsReadOnly((members ?? Array.Empty<NewGameMemberDefinition>()).ToArray());
        StartingWeaponRules = Array.AsReadOnly(
            (startingWeaponRules ?? Array.Empty<NewGameStartingWeaponRuleDefinition>()).ToArray()
        );
        StartingWeaponFallbackItemId = startingWeaponFallbackItemId;
    }

    public int Gold { get; }
    public StringName LeaderMemberId { get; }
    public StringName MainCharacterMemberId { get; }
    public IReadOnlyList<StringName> ActiveMemberIds { get; }
    public IReadOnlyList<StringName> ReserveMemberIds { get; }
    public IReadOnlyList<NewGameMemberDefinition> Members { get; }
    public IReadOnlyList<NewGameStartingWeaponRuleDefinition> StartingWeaponRules { get; }
    public StringName StartingWeaponFallbackItemId { get; }
}

public sealed record SkillGenerationBattleSimFixtureDefinition(
    StringName BasicAttackSkillId,
    StringName GroundBenchmarkSkillId,
    StringName MultiTargetBenchmarkSkillId,
    StringName RangedUnitBenchmarkSkillId,
    StringName MeleeBrainId,
    StringName MageBrainId,
    StringName RangedBrainId
);

public sealed record EquipmentGenerationBattleSimFixtureDefinition(
    StringName BasicAttackSkillId,
    StringName MeleeBrainId
);

public sealed record BattleSkillRoleDefinition(StringName BasicAttackSkillId);

public sealed class GameplayConfigurationDefinition
{
    internal GameplayConfigurationDefinition(
        StringName configurationId,
        IReadOnlyDictionary<StringName, AchievementDefinition> achievements,
        IReadOnlyList<SettlementShopDefinition> settlementShops,
        NewGamePartyDefinition newGameParty,
        BattleSkillRoleDefinition battleSkillRoles,
        SkillGenerationBattleSimFixtureDefinition skillGenerationBattleSim,
        EquipmentGenerationBattleSimFixtureDefinition equipmentGenerationBattleSim
    )
    {
        ConfigurationId = configurationId;
        Achievements = new ReadOnlyDictionary<StringName, AchievementDefinition>(
            new Dictionary<StringName, AchievementDefinition>(achievements)
        );
        SettlementShops = Array.AsReadOnly(settlementShops.ToArray());
        SettlementShopsByInteractionId = new ReadOnlyDictionary<StringName, SettlementShopDefinition>(
            settlementShops.ToDictionary(shop => shop.InteractionScriptId)
        );
        NewGameParty = newGameParty;
        BattleSkillRoles = battleSkillRoles;
        SkillGenerationBattleSim = skillGenerationBattleSim;
        EquipmentGenerationBattleSim = equipmentGenerationBattleSim;
    }

    public StringName ConfigurationId { get; }
    public IReadOnlyDictionary<StringName, AchievementDefinition> Achievements { get; }
    public IReadOnlyList<SettlementShopDefinition> SettlementShops { get; }
    public IReadOnlyDictionary<StringName, SettlementShopDefinition> SettlementShopsByInteractionId { get; }
    public NewGamePartyDefinition NewGameParty { get; }
    public BattleSkillRoleDefinition BattleSkillRoles { get; }
    public SkillGenerationBattleSimFixtureDefinition SkillGenerationBattleSim { get; }
    public EquipmentGenerationBattleSimFixtureDefinition EquipmentGenerationBattleSim { get; }
}
