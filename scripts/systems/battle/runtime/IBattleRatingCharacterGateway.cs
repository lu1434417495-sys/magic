using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class BattleEffectiveTraitProjection
{
    public BattleEffectiveTraitProjection(
        Godot.Collections.Array<BattleEffectiveTraitInstanceState> effective_trait_instances = null)
    {
        EffectiveTraitInstances = BattleUnitState.DuplicateEffectiveTraitInstances(
            effective_trait_instances
                ?? new Godot.Collections.Array<BattleEffectiveTraitInstanceState>()
        );
        EffectiveTraitIds = BattleUnitState.DeriveEffectiveTraitIdsFromInstances(
            EffectiveTraitInstances
        );
    }

    public static BattleEffectiveTraitProjection Empty => new();

    public Godot.Collections.Array<BattleEffectiveTraitInstanceState> EffectiveTraitInstances { get; }
    public GStringNameArray EffectiveTraitIds { get; }
}

public interface IBattleRatingCharacterGateway
{
    GStringNameArray RecordAchievementEvent(
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

    IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped();

    bool HasItemDefCatalog();

    ItemDef GetItemDef(StringName item_id);

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

    void CommitBattleResources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
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

    GStringNameArray RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id,
        GDictionary meta
    );
}
