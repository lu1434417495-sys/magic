using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class ProgressionSerialization : RefCounted
{
    public static GDictionary serialize_unit_progress(GodotObject progress)
    {
        return ToDictionary(progress);
    }

    public static GodotObject deserialize_unit_progress(GDictionary data)
    {
        return UnitProgress.from_dict(data);
    }

    public static GDictionary serialize_unit_base_attributes(UnitBaseAttributes attributes)
    {
        return attributes?.to_dict() ?? new GDictionary();
    }

    public static UnitBaseAttributes deserialize_unit_base_attributes(GDictionary data)
    {
        return UnitBaseAttributes.from_dict(data);
    }

    public static GDictionary serialize_unit_skill_progress(UnitSkillProgress skill_progress)
    {
        return skill_progress?.to_dict() ?? new GDictionary();
    }

    public static UnitSkillProgress deserialize_unit_skill_progress(GDictionary data)
    {
        return UnitSkillProgress.from_dict(data);
    }

    public static GDictionary serialize_unit_profession_progress(
        UnitProfessionProgress profession_progress
    )
    {
        return profession_progress?.to_dict() ?? new GDictionary();
    }

    public static UnitProfessionProgress deserialize_unit_profession_progress(GDictionary data)
    {
        return UnitProfessionProgress.from_dict(data);
    }

    public static GDictionary serialize_unit_reputation_state(UnitReputationState state)
    {
        return state?.to_dict() ?? new GDictionary();
    }

    public static UnitReputationState deserialize_unit_reputation_state(GDictionary data)
    {
        return UnitReputationState.from_dict(data);
    }

    public static GDictionary serialize_achievement_progress_state(AchievementProgressState state)
    {
        return state?.to_dict() ?? new GDictionary();
    }

    public static AchievementProgressState deserialize_achievement_progress_state(GDictionary data)
    {
        return AchievementProgressState.from_dict(data);
    }

    public static GDictionary serialize_party_member_state(PartyMemberState member_state)
    {
        return member_state?.to_dict() ?? new GDictionary();
    }

    public static PartyMemberState deserialize_party_member_state(GDictionary data)
    {
        return PartyMemberState.from_dict(data);
    }

    public static GDictionary serialize_party_state(GodotObject party_state)
    {
        return ToDictionary(party_state);
    }

    public static PartyState deserialize_party_state(GDictionary data)
    {
        return PartyState.from_dict(data);
    }

    public static GDictionary serialize_pending_character_reward(PendingCharacterReward reward)
    {
        return reward?.to_dict() ?? new GDictionary();
    }

    public static PendingCharacterReward deserialize_pending_character_reward(GDictionary data)
    {
        return PendingCharacterReward.from_dict(data);
    }

    public static GDictionary serialize_pending_character_reward_entry(
        PendingCharacterRewardEntry entry
    )
    {
        return entry?.to_dict() ?? new GDictionary();
    }

    public static PendingCharacterRewardEntry deserialize_pending_character_reward_entry(
        GDictionary data
    )
    {
        return PendingCharacterRewardEntry.from_dict(data);
    }

    public static GDictionary serialize_encounter_anchor(EncounterAnchorData encounter_anchor)
    {
        return encounter_anchor?.to_dict() ?? new GDictionary();
    }

    public static EncounterAnchorData deserialize_encounter_anchor(GDictionary data)
    {
        return EncounterAnchorData.from_dict(data);
    }

    private static GDictionary ToDictionary(GodotObject value)
    {
        if (value == null || !value.HasMethod("to_dict"))
            return new GDictionary();
        var result = value.Call("to_dict");
        return result.VariantType == Variant.Type.Dictionary
            ? result.AsGodotDictionary()
            : new GDictionary();
    }
}
