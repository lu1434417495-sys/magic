using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class ProfessionPromotionRecord : RefCounted
{
    private static readonly Godot.Collections.Array<string> ToDictFields = new()
    {
        "new_rank",
        "consumed_skill_ids",
        "qualifier_skill_ids",
        "snapshot_unit_base_attributes",
        "timestamp",
    };

    public int new_rank;
    public GStringNameArray consumed_skill_ids = new();
    public GStringNameArray qualifier_skill_ids = new();
    public GDictionary snapshot_unit_base_attributes = new();
    public int timestamp;

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["new_rank"] = new_rank,
            ["consumed_skill_ids"] = ProgressionDataUtils.string_name_array_to_string_array(consumed_skill_ids),
            ["qualifier_skill_ids"] = ProgressionDataUtils.string_name_array_to_string_array(qualifier_skill_ids),
            ["snapshot_unit_base_attributes"] = snapshot_unit_base_attributes.Duplicate(true),
            ["timestamp"] = timestamp,
        };
    }

    public static ProfessionPromotionRecord from_dict(GDictionary data)
    {
        if (data == null || !_has_exact_fields(data, ToDictFields))
        {
            return null;
        }
        if (data["consumed_skill_ids"].VariantType != Variant.Type.Array)
        {
            return null;
        }
        if (data["qualifier_skill_ids"].VariantType != Variant.Type.Array)
        {
            return null;
        }
        if (data["snapshot_unit_base_attributes"].VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        Variant newRankVariant = data["new_rank"];
        if (newRankVariant.VariantType != Variant.Type.Int || newRankVariant.AsInt32() < 0)
        {
            return null;
        }
        GStringNameArray consumedSkillIds = _parse_unique_string_name_array(data["consumed_skill_ids"].AsGodotArray());
        if (consumedSkillIds == null)
        {
            return null;
        }
        GStringNameArray qualifierSkillIds = _parse_unique_string_name_array(data["qualifier_skill_ids"].AsGodotArray());
        if (qualifierSkillIds == null)
        {
            return null;
        }
        Variant timestampVariant = data["timestamp"];
        if (timestampVariant.VariantType != Variant.Type.Int || timestampVariant.AsInt32() < 0)
        {
            return null;
        }

        return new ProfessionPromotionRecord
        {
            new_rank = newRankVariant.AsInt32(),
            consumed_skill_ids = consumedSkillIds,
            qualifier_skill_ids = qualifierSkillIds,
            snapshot_unit_base_attributes = data["snapshot_unit_base_attributes"].AsGodotDictionary().Duplicate(true),
            timestamp = timestampVariant.AsInt32(),
        };
    }

    private static bool _has_exact_fields(GDictionary data, Godot.Collections.Array<string> expectedFields)
    {
        if (data.Count != expectedFields.Count)
        {
            return false;
        }
        foreach (string fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName))
            {
                return false;
            }
        }
        return true;
    }

    private static StringName _parse_string_name_field(Variant value, out bool ok)
    {
        ok = false;
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
        {
            return "";
        }
        StringName parsedValue = ProgressionDataUtils.to_string_name(value);
        if (parsedValue == (StringName)"")
        {
            return "";
        }
        ok = true;
        return parsedValue;
    }

    private static GStringNameArray _parse_unique_string_name_array(GArray values)
    {
        var parsedValues = new GStringNameArray();
        var seenValues = new GDictionary();
        foreach (Variant rawValue in values)
        {
            StringName parsedValue = _parse_string_name_field(rawValue, out bool ok);
            if (!ok || seenValues.ContainsKey(parsedValue))
            {
                return null;
            }
            seenValues[parsedValue] = true;
            parsedValues.Add(parsedValue);
        }
        return parsedValues;
    }
}
