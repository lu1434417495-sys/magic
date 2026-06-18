using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal enum EncounterAnchorKind
{
    Unknown = 0,
    Single,
    Settlement,
}

[GlobalClass]
public partial class EncounterAnchorData : RefCounted
{
    private static readonly StringName EncounterKindSingle = "single";
    private static readonly StringName EncounterKindSettlement = "settlement";

    private static readonly string[] RequiredSerializedFields =
    {
        "entity_id",
        "display_name",
        "world_coord",
        "faction_id",
        "enemy_roster_template_id",
        "region_tag",
        "vision_range",
        "is_cleared",
        "encounter_kind",
        "encounter_profile_id",
        "growth_stage",
        "suppressed_until_step",
    };

    public StringName entity_id { get; set; } = "";
    public string display_name { get; set; } = "";
    public Vector2I world_coord { get; set; } = Vector2I.Zero;
    public StringName faction_id { get; set; } = "hostile";
    public StringName enemy_roster_template_id { get; set; } = "";
    public StringName region_tag { get; set; } = "";
    public int vision_range { get; set; }
    public bool is_cleared { get; set; }
    public StringName encounter_kind { get; set; } = ToStringName(EncounterAnchorKind.Single);
    public StringName encounter_profile_id { get; set; } = "";
    public int growth_stage { get; set; }
    public int suppressed_until_step { get; set; }

    internal static StringName ToStringName(EncounterAnchorKind kind)
    {
        return kind switch
        {
            EncounterAnchorKind.Single => EncounterKindSingle,
            EncounterAnchorKind.Settlement => EncounterKindSettlement,
            _ => new StringName(""),
        };
    }

    internal static EncounterAnchorKind ToEncounterKind(StringName value)
    {
        if (value == EncounterKindSingle)
            return EncounterAnchorKind.Single;
        if (value == EncounterKindSettlement)
            return EncounterAnchorKind.Settlement;
        return EncounterAnchorKind.Unknown;
    }

    public static EncounterAnchorData FromDictionary(GDictionary payload)
    {
        if (payload == null)
            return null;
        if (!HasExactSerializedFields(payload))
        {
            return null;
        }

        if (
            !TryParseStringNameField(payload, "entity_id", false, out StringName entityIdValue)
            || !TryParseStringNameField(
                payload,
                "faction_id",
                false,
                out StringName factionIdValue
            )
            || !TryParseStringNameField(
                payload,
                "enemy_roster_template_id",
                true,
                out StringName enemyRosterTemplateIdValue
            )
            || !TryParseStringNameField(
                payload,
                "region_tag",
                true,
                out StringName regionTagValue
            )
            || !TryParseStringNameField(
                payload,
                "encounter_kind",
                false,
                out StringName encounterKindValue
            )
            || !TryParseStringNameField(
                payload,
                "encounter_profile_id",
                true,
                out StringName encounterProfileIdValue
            )
        )
        {
            return null;
        }
        if (!IsValidEncounterKind(encounterKindValue))
        {
            return null;
        }

        var rawDisplayName = payload["display_name"];
        if (rawDisplayName.VariantType != Variant.Type.String)
        {
            return null;
        }
        string displayNameValue = rawDisplayName.AsString();
        if (string.IsNullOrEmpty(displayNameValue.Trim()))
        {
            return null;
        }

        var rawWorldCoord = payload["world_coord"];
        if (rawWorldCoord.VariantType != Variant.Type.Vector2I)
        {
            return null;
        }

        var rawVisionRange = payload["vision_range"];
        if (rawVisionRange.VariantType != Variant.Type.Int || rawVisionRange.AsInt32() < 0)
        {
            return null;
        }

        var rawIsCleared = payload["is_cleared"];
        if (rawIsCleared.VariantType != Variant.Type.Bool)
        {
            return null;
        }

        var rawGrowthStage = payload["growth_stage"];
        if (rawGrowthStage.VariantType != Variant.Type.Int || rawGrowthStage.AsInt32() < 0)
        {
            return null;
        }

        var rawSuppressedUntilStep = payload["suppressed_until_step"];
        if (
            rawSuppressedUntilStep.VariantType != Variant.Type.Int
            || rawSuppressedUntilStep.AsInt32() < 0
        )
        {
            return null;
        }

        return new EncounterAnchorData
        {
            entity_id = entityIdValue,
            display_name = displayNameValue,
            world_coord = rawWorldCoord.AsVector2I(),
            faction_id = factionIdValue,
            enemy_roster_template_id = enemyRosterTemplateIdValue,
            region_tag = regionTagValue,
            vision_range = rawVisionRange.AsInt32(),
            is_cleared = rawIsCleared.AsBool(),
            encounter_kind = encounterKindValue,
            encounter_profile_id = encounterProfileIdValue,
            growth_stage = rawGrowthStage.AsInt32(),
            suppressed_until_step = rawSuppressedUntilStep.AsInt32(),
        };
    }

    private static bool HasExactSerializedFields(GDictionary payload)
    {
        if (payload.Count != RequiredSerializedFields.Length)
        {
            return false;
        }
        foreach (string fieldName in RequiredSerializedFields)
        {
            if (!payload.ContainsKey(fieldName))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryParseStringNameField(
        GDictionary payload,
        string key,
        bool allowEmpty,
        out StringName parsed
    )
    {
        parsed = "";
        if (payload == null || !payload.ContainsKey(key))
        {
            return false;
        }
        var value = payload[key];
        if (
            value.VariantType != Variant.Type.String
            && value.VariantType != Variant.Type.StringName
        )
        {
            return false;
        }
        string text = value.AsString().Trim();
        if (string.IsNullOrEmpty(text) && !allowEmpty)
        {
            return false;
        }
        parsed = new StringName(text);
        return true;
    }

    private static bool IsValidEncounterKind(StringName value)
    {
        return ToEncounterKind(value) != EncounterAnchorKind.Unknown;
    }

}
