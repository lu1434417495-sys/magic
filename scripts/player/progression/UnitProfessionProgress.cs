using Godot;

[GlobalClass]
public partial class UnitProfessionProgress : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
    {
        "profession_id",
        "rank",
        "is_active",
        "is_hidden",
        "core_skill_ids",
        "granted_skill_ids",
        "promotion_history",
        "inactive_reason",
    };

    public StringName profession_id = "";

    public int rank;

    public bool is_active = true;

    public bool is_hidden;

    public Godot.Collections.Array<StringName> core_skill_ids = new();

    public Godot.Collections.Array<StringName> granted_skill_ids = new();

    public Godot.Collections.Array<ProfessionPromotionRecord> promotion_history = new();

    public StringName inactive_reason = "";
    private bool _disposed;

    public new void Dispose()
    {
        if (_disposed)
            return;
        System.GC.SuppressFinalize(this);
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeManagedState();
        base.Dispose(disposing);
    }

    private void DisposeManagedState()
    {
        if (_disposed)
            return;
        _disposed = true;
        GodotRefCountedDisposer.DisposeAll(promotion_history);
        promotion_history.Clear();
        core_skill_ids.Clear();
        granted_skill_ids.Clear();
    }

    public void AddCoreSkill(StringName skillId)
    {
        if (!core_skill_ids.Contains(skillId))
            core_skill_ids.Add(skillId);
    }

    public void RemoveCoreSkill(StringName skillId) => core_skill_ids.Remove(skillId);

    public void AddGrantedSkill(StringName skillId)
    {
        if (!granted_skill_ids.Contains(skillId))
            granted_skill_ids.Add(skillId);
    }

    public void AddPromotionRecord(ProfessionPromotionRecord record)
    {
        if (record != null)
            promotion_history.Add(record);
    }

    public UnitProfessionProgress DuplicateState()
    {
        var copy = new UnitProfessionProgress
        {
            profession_id = profession_id,
            rank = rank,
            is_active = is_active,
            is_hidden = is_hidden,
            core_skill_ids = new Godot.Collections.Array<StringName>(core_skill_ids),
            granted_skill_ids = new Godot.Collections.Array<StringName>(granted_skill_ids),
            inactive_reason = inactive_reason,
        };
        foreach (var record in promotion_history)
            if (record != null)
                copy.promotion_history.Add(record.DuplicateState());
        return copy;
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var promoData = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        foreach (var r in promotion_history)
        {
            if (r != null)
                promoData.Add(r.ToDictionary());
        }

        return new Godot.Collections.Dictionary
        {
            { "profession_id", (string)profession_id },
            { "rank", rank },
            { "is_active", is_active },
            { "is_hidden", is_hidden },
            {
                "core_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(core_skill_ids)
            },
            {
                "granted_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(granted_skill_ids)
            },
            { "promotion_history", promoData },
            { "inactive_reason", (string)inactive_reason },
        };
    }

    public static UnitProfessionProgress FromDictionary(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return null;

        if (data["core_skill_ids"].VariantType != Variant.Type.Array)
            return null;

        if (data["granted_skill_ids"].VariantType != Variant.Type.Array)
            return null;

        if (data["promotion_history"].VariantType != Variant.Type.Array)
            return null;

        var profId = _parse_string_name_field(data["profession_id"], false, out bool ok1);

        if (!ok1)
            return null;

        var rankVar = data["rank"];

        if (rankVar.VariantType != Variant.Type.Int || rankVar.AsInt32() < 0)
            return null;

        if (
            data["is_active"].VariantType != Variant.Type.Bool
            || data["is_hidden"].VariantType != Variant.Type.Bool
        )
            return null;

        var coreIds = _parse_unique_string_name_array(data["core_skill_ids"].AsGodotArray());

        if (coreIds == null)
            return null;

        var grantedIds = _parse_unique_string_name_array(data["granted_skill_ids"].AsGodotArray());

        if (grantedIds == null)
            return null;

        var inactiveReason = _parse_string_name_field(data["inactive_reason"], true, out bool ok2);

        if (!ok2)
            return null;

        var progress = new UnitProfessionProgress
        {
            profession_id = profId,
            rank = rankVar.AsInt32(),
            is_active = _read_bool(data, "is_active"),
            is_hidden = _read_bool(data, "is_hidden"),
            core_skill_ids = coreIds,
            granted_skill_ids = grantedIds,
            inactive_reason = inactiveReason,
        };
        foreach (var recordData in data["promotion_history"].AsGodotArray())
        {
            if (recordData.VariantType != Variant.Type.Dictionary)
                return null;

            var promoRecord = ProfessionPromotionRecord.FromDictionary(recordData.AsGodotDictionary());

            if (promoRecord == null)
                return null;

            progress.promotion_history.Add(promoRecord);
        }

        return progress;
    }

    private static bool _has_exact_fields(
        Godot.Collections.Dictionary data,
        Godot.Collections.Array<string> expected
    )
    {
        if (data.Count != expected.Count)
            return false;
        foreach (string fn in expected)
        {
            if (!data.ContainsKey(fn))
                return false;
        }
        return true;
    }

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
        var parsed = ProgressionDataUtils.to_string_name(rawValue);
        if (parsed == "" && !allowEmpty)
            return new StringName("");
        ok = true;
        return parsed;
    }

    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(
        Godot.Collections.Array values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary();
        foreach (var raw in values)
        {
            var parsed = _parse_string_name_field(raw, false, out bool ok);
            if (!ok || seen.ContainsKey(parsed))
                return null;
            seen[parsed] = true;
            result.Add(parsed);
        }
        return result;
    }

    private static bool _read_bool(Godot.Collections.Dictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }
}
