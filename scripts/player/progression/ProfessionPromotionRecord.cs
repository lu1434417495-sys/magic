using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public class ProfessionPromotionRecord
{
    private static readonly string[] ToDictFields =
    {
        "new_rank",
        "growth_trigger_skill_id",
        "growth_trigger_level",
        "consumed_skill_ids",
        "qualifier_skill_ids",
        "snapshot_unit_base_attributes",
        "timestamp",
    };

    public int new_rank;
    public StringName growth_trigger_skill_id = "";
    public int growth_trigger_level;
    public StringNameList consumed_skill_ids = new();
    public StringNameList qualifier_skill_ids = new();
    public UnitBaseAttributes snapshot_unit_base_attributes = new();
    public int timestamp;

    public ProfessionPromotionRecord DuplicateState()
    {
        return new ProfessionPromotionRecord
        {
            new_rank = new_rank,
            growth_trigger_skill_id = growth_trigger_skill_id,
            growth_trigger_level = growth_trigger_level,
            consumed_skill_ids = consumed_skill_ids?.Duplicate() ?? new StringNameList(),
            qualifier_skill_ids = qualifier_skill_ids?.Duplicate() ?? new StringNameList(),
            snapshot_unit_base_attributes =
                snapshot_unit_base_attributes?.DuplicateState() ?? new UnitBaseAttributes(),
            timestamp = timestamp,
        };
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["new_rank"] = new_rank,
            ["growth_trigger_skill_id"] = growth_trigger_skill_id.ToString(),
            ["growth_trigger_level"] = growth_trigger_level,
            ["consumed_skill_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                consumed_skill_ids
            ),
            ["qualifier_skill_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                qualifier_skill_ids
            ),
            ["snapshot_unit_base_attributes"] =
                snapshot_unit_base_attributes?.ToDictionary() ?? new GDictionary(),
            ["timestamp"] = timestamp,
        };
    }

    public static ProfessionPromotionRecord FromDictionary(GDictionary data)
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
        var triggerId = _parse_string_name_field(data["growth_trigger_skill_id"], out bool triggerOk);
        if (!triggerOk || data["growth_trigger_level"].VariantType != Variant.Type.Int
            || data["growth_trigger_level"].AsInt64() <= 0 || data["growth_trigger_level"].AsInt64() > int.MaxValue)
            return null;
        var newRankValue = data["new_rank"];
        if (newRankValue.VariantType != Variant.Type.Int || newRankValue.AsInt64() <= 0 || newRankValue.AsInt64() > int.MaxValue)
        {
            return null;
        }
        StringNameList consumedSkillIds = _parse_unique_string_name_array(
            data["consumed_skill_ids"].AsGodotArray()
        );
        if (consumedSkillIds == null)
        {
            return null;
        }
        StringNameList qualifierSkillIds = _parse_unique_string_name_array(
            data["qualifier_skill_ids"].AsGodotArray()
        );
        if (qualifierSkillIds == null)
        {
            return null;
        }
        if (!consumedSkillIds.Contains(triggerId) && !qualifierSkillIds.Contains(triggerId))
            return null;
        var timestampValue = data["timestamp"];
        if (timestampValue.VariantType != Variant.Type.Int || timestampValue.AsInt32() < 0)
        {
            return null;
        }
        UnitBaseAttributes snapshotUnitBaseAttributes = UnitBaseAttributes.FromDictionary(
            data["snapshot_unit_base_attributes"].AsGodotDictionary()
        );
        if (snapshotUnitBaseAttributes == null)
        {
            return null;
        }

        return new ProfessionPromotionRecord
        {
            new_rank = newRankValue.AsInt32(),
            growth_trigger_skill_id = triggerId,
            growth_trigger_level = data["growth_trigger_level"].AsInt32(),
            consumed_skill_ids = consumedSkillIds,
            qualifier_skill_ids = qualifierSkillIds,
            snapshot_unit_base_attributes = snapshotUnitBaseAttributes,
            timestamp = timestampValue.AsInt32(),
        };
    }

    private static bool _has_exact_fields(
        GDictionary data,
        IReadOnlyCollection<string> expectedFields
    )
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

    private static StringName _parse_string_name_field(object rawValue, out bool ok)
    {
        ok = false;
        if (rawValue is Variant value)
        {
            if (
                value.VariantType != Variant.Type.String
                && value.VariantType != Variant.Type.StringName
            )
            {
                return "";
            }
        }
        else if (rawValue is not string && rawValue is not StringName)
        {
            return "";
        }
        StringName parsedValue = ProgressionDataUtils.to_string_name(rawValue);
        if (parsedValue == (StringName)"")
        {
            return "";
        }
        ok = true;
        return parsedValue;
    }

    private static StringNameList _parse_unique_string_name_array(GArray values)
    {
        var parsedValues = new StringNameList();
        var seenValues = new HashSet<StringName>();
        foreach (var rawValue in values)
        {
            StringName parsedValue = _parse_string_name_field(rawValue, out bool ok);
            if (!ok || !seenValues.Add(parsedValue))
            {
                return null;
            }
            parsedValues.Add(parsedValue);
        }
        return parsedValues;
    }
}
