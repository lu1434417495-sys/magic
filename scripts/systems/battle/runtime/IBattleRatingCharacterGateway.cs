using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class BattleEffectiveTraitProjection
{
    private readonly List<BattleEffectiveTraitInstanceState> _effectiveTraitInstances;
    private readonly StringNameList _effectiveTraitIds;

    public BattleEffectiveTraitProjection(
        IEnumerable<BattleEffectiveTraitInstanceState> effective_trait_instances = null)
    {
        _effectiveTraitInstances = BattleUnitEffectiveTraitState
            .DuplicateInstancesNormalized(
            effective_trait_instances ?? System.Array.Empty<BattleEffectiveTraitInstanceState>()
        );
        _effectiveTraitIds = BattleUnitEffectiveTraitState.DeriveTraitIds(
            _effectiveTraitInstances
        );
    }

    public static BattleEffectiveTraitProjection Empty => new();

    internal void ApplyTo(BattleUnitState unit) =>
        unit?.ReplaceEffectiveTraitsTyped(_effectiveTraitInstances);

    public GStringNameArray EffectiveTraitIds
    {
        get
        {
            var result = new GStringNameArray();
            foreach (StringName traitId in _effectiveTraitIds)
                result.Add(traitId);
            return result;
        }
    }
}

public sealed class BattleResourceCommitResult
{
    public bool Ok { get; init; }
    public string ErrorCode { get; init; } = "";
    public StringName MemberId { get; init; } = "";

    public static BattleResourceCommitResult Success(StringName memberId) =>
        new()
        {
            Ok = true,
            MemberId = ProgressionDataUtils.to_string_name(memberId),
        };

    public static BattleResourceCommitResult Failure(string errorCode, StringName memberId) =>
        new()
        {
            Ok = false,
            ErrorCode = errorCode ?? "",
            MemberId = ProgressionDataUtils.to_string_name(memberId),
        };
}

public interface IBattleRatingCharacterGateway
{
    IReadOnlyList<StringName> RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount
    );

    PendingCharacterReward BuildPendingSkillMasteryReward(
        StringName member_id,
        StringName source_type,
        string source_label,
        IEnumerable<PendingCharacterRewardEntry> entry_options,
        string summary_text
    );
}

public interface IBattleRuntimeCharacterGateway : IBattleRatingCharacterGateway
{
    PartyState GetPartyState();

    IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped();

    bool HasItemDefCatalog();

    ItemDefinition GetItemDef(StringName item_id);

    PartyMemberState GetMemberState(StringName member_id);

    AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
        StringName member_id,
        EquipmentState equipment_view
    );

    WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
        StringName member_id,
        EquipmentState equipment_view
    );

    BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
        StringName member_id,
        EquipmentState equipment_view
    );

    PassiveSourceContext BuildPassiveSourceContext(
        StringName member_id,
        UnitProgress progression_state
    );

    CharacterProgressionDelta PromoteProfession(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    );

    BattleResourceCommitResult CommitBattleResources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
    );

    ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(
        StringName member_id,
        IReadOnlyCollection<StringName> consumed_setup_ids
    );

    ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
        StringName member_id,
        IReadOnlyCollection<StringName> consumed_setup_ids
    );

    void CommitBattleDeath(StringName member_id);

    int FlushAfterBattle();

    CharacterProgressionDelta GrantBattleMastery(
        StringName member_id,
        StringName skill_id,
        int amount
    );

    CharacterProgressionDelta GrantSkillMasteryFromSource(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text,
        bool emit_achievement_event
    );

    IReadOnlyList<StringName> RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id,
        GDictionary meta
    );
}
