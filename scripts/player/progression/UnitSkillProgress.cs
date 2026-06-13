using System.Collections.Generic;
using Godot;

internal enum UnitSkillGrantSourceType
{
    Unknown,
    Player,
    Profession,
    Race,
    Subrace,
    Ascension,
    Bloodline,
}

[GlobalClass]
public partial class UnitSkillProgress : RefCounted
{
    private static readonly StringName GrantedSourcePlayer = "player";
    private static readonly StringName GrantedSourceProfession = "profession";
    private static readonly StringName GrantedSourceRace = "race";
    private static readonly StringName GrantedSourceSubrace = "subrace";
    private static readonly StringName GrantedSourceAscension = "ascension";
    private static readonly StringName GrantedSourceBloodline = "bloodline";

    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
    {
        "skill_id",
        "is_learned",
        "skill_level",
        "current_mastery",
        "total_mastery_earned",
        "is_core",
        "assigned_profession_id",
        "merged_from_skill_ids",
        "mastery_from_training",
        "mastery_from_battle",
        "profession_granted_by",
        "granted_source_type",
        "granted_source_id",
        "core_max_growth_claimed",
        "is_level_trigger_active",
        "is_level_trigger_locked",
        "bonus_to_hit_from_lock",
    };

    public StringName skill_id = "";

    public bool is_learned;

    public int skill_level;

    public int current_mastery;

    public int total_mastery_earned;

    public bool is_core;

    public StringName assigned_profession_id = "";

    public Godot.Collections.Array<StringName> merged_from_skill_ids = new();

    public int mastery_from_training;

    public int mastery_from_battle;

    public StringName profession_granted_by = "";

    public StringName granted_source_type = GrantedSourcePlayer;
    internal UnitSkillGrantSourceType GrantedSourceTypeKind
    {
        get => ToGrantSourceType(granted_source_type);
        set => granted_source_type = ToStringName(value);
    }

    public StringName granted_source_id = "";

    public bool core_max_growth_claimed;

    public bool is_level_trigger_active;

    public bool is_level_trigger_locked;

    public int bonus_to_hit_from_lock = 1;

    public bool IsMaxLevel(int maxLevel) => skill_level >= maxLevel;

    public void ClearProfessionAssignment() => assigned_profession_id = "";

    public UnitSkillProgress DuplicateState()
    {
        return new UnitSkillProgress
        {
            skill_id = skill_id,
            is_learned = is_learned,
            skill_level = skill_level,
            current_mastery = current_mastery,
            total_mastery_earned = total_mastery_earned,
            is_core = is_core,
            assigned_profession_id = assigned_profession_id,
            merged_from_skill_ids = new Godot.Collections.Array<StringName>(merged_from_skill_ids),
            mastery_from_training = mastery_from_training,
            mastery_from_battle = mastery_from_battle,
            profession_granted_by = profession_granted_by,
            granted_source_type = granted_source_type,
            granted_source_id = granted_source_id,
            core_max_growth_claimed = core_max_growth_claimed,
            is_level_trigger_active = is_level_trigger_active,
            is_level_trigger_locked = is_level_trigger_locked,
            bonus_to_hit_from_lock = bonus_to_hit_from_lock,
        };
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "skill_id", (string)skill_id },
            { "is_learned", is_learned },
            { "skill_level", skill_level },
            { "current_mastery", current_mastery },
            { "total_mastery_earned", total_mastery_earned },
            { "is_core", is_core },
            { "assigned_profession_id", (string)assigned_profession_id },
            {
                "merged_from_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(merged_from_skill_ids)
            },
            { "mastery_from_training", mastery_from_training },
            { "mastery_from_battle", mastery_from_battle },
            { "profession_granted_by", (string)profession_granted_by },
            { "granted_source_type", (string)granted_source_type },
            { "granted_source_id", (string)granted_source_id },
            { "core_max_growth_claimed", core_max_growth_claimed },
            { "is_level_trigger_active", is_level_trigger_active },
            { "is_level_trigger_locked", is_level_trigger_locked },
            { "bonus_to_hit_from_lock", bonus_to_hit_from_lock },
        };
    }

    public static UnitSkillProgress FromDictionary(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return null;

        if (data["merged_from_skill_ids"].VariantType != Variant.Type.Array)
            return null;

        var skillId = _parse_string_name_field(data["skill_id"], false, out bool ok1);

        if (!ok1)
            return null;

        foreach (
            string bf in new[]
            {
                "is_learned",
                "is_core",
                "core_max_growth_claimed",
                "is_level_trigger_active",
                "is_level_trigger_locked",
            }
        )
            if (data[bf].VariantType != Variant.Type.Bool)
                return null;

        foreach (
            string inf in new[]
            {
                "skill_level",
                "current_mastery",
                "total_mastery_earned",
                "mastery_from_training",
                "mastery_from_battle",
                "bonus_to_hit_from_lock",
            }
        )
        {
            var iv = data[inf];

            if (iv.VariantType != Variant.Type.Int || iv.AsInt32() < 0)
                return null;
        }

        var assignedProf = _parse_string_name_field(
            data["assigned_profession_id"],
            true,
            out bool ok2
        );

        if (!ok2)
            return null;

        var profGrantedBy = _parse_string_name_field(
            data["profession_granted_by"],
            true,
            out bool ok3
        );

        if (!ok3)
            return null;

        var grantSourceType = _parse_string_name_field(
            data["granted_source_type"],
            false,
            out bool ok4
        );

        UnitSkillGrantSourceType grantSourceKind = ToGrantSourceType(grantSourceType);
        if (!ok4 || grantSourceKind == UnitSkillGrantSourceType.Unknown)
            return null;

        var grantSourceId = _parse_string_name_field(
            data["granted_source_id"],
            grantSourceKind == UnitSkillGrantSourceType.Player,
            out bool ok5
        );
        if (!ok5)
            return null;

        var mergedIds = _parse_unique_string_name_array(
            data["merged_from_skill_ids"].AsGodotArray()
        );

        if (mergedIds == null)
            return null;

        bool isLearned = _read_bool(data, "is_learned");

        if (!isLearned)
        {
            if (data["skill_level"].AsInt32() != 0)
                return null;

            if (data["current_mastery"].AsInt32() != 0)
                return null;

            if (data["total_mastery_earned"].AsInt32() != 0)
                return null;

            if (data["mastery_from_training"].AsInt32() != 0)
                return null;

            if (data["mastery_from_battle"].AsInt32() != 0)
                return null;

            if (_read_bool(data, "is_core"))
                return null;

            if (assignedProf != "")
                return null;

            if (mergedIds.Count > 0)
                return null;

            if (profGrantedBy != "")
                return null;

            if (_read_bool(data, "core_max_growth_claimed"))
                return null;
        }

        int curMastery = data["current_mastery"].AsInt32();

        int totalMastery = data["total_mastery_earned"].AsInt32();

        int mTraining = data["mastery_from_training"].AsInt32();

        int mBattle = data["mastery_from_battle"].AsInt32();

        if (curMastery > totalMastery)
            return null;

        if (mTraining + mBattle > totalMastery)
            return null;

        return new UnitSkillProgress
        {
            skill_id = skillId,
            is_learned = isLearned,
            skill_level = data["skill_level"].AsInt32(),

            current_mastery = curMastery,
            total_mastery_earned = totalMastery,
            is_core = _read_bool(data, "is_core"),

            assigned_profession_id = assignedProf,
            merged_from_skill_ids = mergedIds,

            mastery_from_training = mTraining,
            mastery_from_battle = mBattle,
            profession_granted_by = profGrantedBy,

            granted_source_type = grantSourceType,
            granted_source_id = grantSourceId,

            core_max_growth_claimed = _read_bool(data, "core_max_growth_claimed"),

            is_level_trigger_active = _read_bool(data, "is_level_trigger_active"),

            is_level_trigger_locked = _read_bool(data, "is_level_trigger_locked"),

            bonus_to_hit_from_lock = data["bonus_to_hit_from_lock"].AsInt32(),
        };
    }

    internal static UnitSkillGrantSourceType ToGrantSourceType(StringName sourceType)
    {
        if (sourceType == GrantedSourcePlayer)
            return UnitSkillGrantSourceType.Player;
        if (sourceType == GrantedSourceProfession)
            return UnitSkillGrantSourceType.Profession;
        if (sourceType == GrantedSourceRace)
            return UnitSkillGrantSourceType.Race;
        if (sourceType == GrantedSourceSubrace)
            return UnitSkillGrantSourceType.Subrace;
        if (sourceType == GrantedSourceAscension)
            return UnitSkillGrantSourceType.Ascension;
        if (sourceType == GrantedSourceBloodline)
            return UnitSkillGrantSourceType.Bloodline;
        return UnitSkillGrantSourceType.Unknown;
    }

    internal static StringName ToStringName(UnitSkillGrantSourceType sourceType) =>
        sourceType switch
        {
            UnitSkillGrantSourceType.Player => GrantedSourcePlayer,
            UnitSkillGrantSourceType.Profession => GrantedSourceProfession,
            UnitSkillGrantSourceType.Race => GrantedSourceRace,
            UnitSkillGrantSourceType.Subrace => GrantedSourceSubrace,
            UnitSkillGrantSourceType.Ascension => GrantedSourceAscension,
            UnitSkillGrantSourceType.Bloodline => GrantedSourceBloodline,
            _ => "",
        };

    private static StringName _parse_string_name_field(object rawValue, bool allowEmpty, out bool ok)
    {
        ok = false;
        if (rawValue is Variant value)
        {
            if (
                value.VariantType != Variant.Type.String
                && value.VariantType != Variant.Type.StringName
            )
                return new StringName("");
        }
        else if (rawValue is not string && rawValue is not StringName)
        {
            return new StringName("");
        }
        var p = ProgressionDataUtils.to_string_name(rawValue);
        if (p == "" && !allowEmpty)
            return new StringName("");
        ok = true;
        return p;
    }

    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(
        Godot.Collections.Array values
    )
    {
        var r = new Godot.Collections.Array<StringName>();
        var s = new Godot.Collections.Dictionary();
        foreach (var raw in values)
        {
            var p = _parse_string_name_field(raw, false, out bool o);
            if (!o || s.ContainsKey(p))
                return null;
            s[p] = true;
            r.Add(p);
        }
        return r;
    }

    private static bool _has_exact_fields(
        Godot.Collections.Dictionary data,
        Godot.Collections.Array<string> expected
    )
    {
        if (data.Count != expected.Count)
            return false;
        var expectedFields = new System.Collections.Generic.HashSet<string>();
        var seenFields = new System.Collections.Generic.HashSet<string>();
        foreach (string fn in expected)
            expectedFields.Add(fn);
        foreach (var key in data.Keys)
        {
            if (
                key.VariantType != Variant.Type.String
                && key.VariantType != Variant.Type.StringName
            )
                return false;
            string fieldName = key.AsString();
            if (!expectedFields.Contains(fieldName))
                return false;
            if (seenFields.Contains(fieldName))
                return false;
            seenFields.Add(fieldName);
        }
        return seenFields.Count == expectedFields.Count;
    }

    private static bool _read_bool(Godot.Collections.Dictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }
}
