using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public interface IBattleRatingCharacterGateway
{
    GStringNameArray record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount
    );

    PendingCharacterReward build_pending_skill_mastery_reward(
        StringName member_id,
        StringName source_type,
        string source_label,
        GArray entry_options,
        string summary_text
    );
}

public interface IBattleRuntimeCharacterGateway : IBattleRatingCharacterGateway
{
    PartyState get_party_state();

    GDictionary get_item_defs();

    PartyMemberState get_member_state(StringName member_id);

    AttributeSnapshot get_member_attribute_snapshot_for_equipment_view(
        StringName member_id,
        EquipmentState equipment_view
    );

    GDictionary get_member_weapon_projection_for_equipment_view(
        StringName member_id,
        EquipmentState equipment_view
    );

    PassiveSourceContext build_passive_source_context(
        StringName member_id,
        UnitProgress progression_state
    );

    CharacterProgressionDelta promote_profession(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    );

    void commit_battle_resources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
    );

    void commit_battle_death(StringName member_id);

    int flush_after_battle();

    CharacterProgressionDelta grant_battle_mastery(
        StringName member_id,
        StringName skill_id,
        int amount
    );

    CharacterProgressionDelta grant_skill_mastery_from_source(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text,
        bool emit_achievement_event
    );

    GStringNameArray record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id,
        GDictionary meta
    );
}
