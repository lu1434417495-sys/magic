using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSkillMasteryGrant
{
    public StringName MemberId = "";
    public StringName SkillId = "";
    public int Amount;
    public StringName SourceType = "";
    public string SourceLabel = "";
    public string ReasonText = "";
    public bool AllowUnlocks = true;
    public bool RecordNearDeathUnbrokenManual;

    public bool IsValid =>
        MemberId != ""
        && SkillId != ""
        && SourceType != ""
        && Amount > 0;

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["member_id"] = MemberId,
            ["skill_id"] = SkillId,
            ["amount"] = Amount,
            ["source_type"] = SourceType,
            ["source_label"] = SourceLabel,
            ["reason_text"] = ReasonText,
            ["allow_unlocks"] = AllowUnlocks,
            ["record_near_death_unbroken_manual"] = RecordNearDeathUnbrokenManual,
        };
    }

    public static BattleSkillMasteryGrant FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
        {
            return new BattleSkillMasteryGrant();
        }
        return new BattleSkillMasteryGrant
        {
            MemberId = ReadStringName(source, "member_id"),
            SkillId = ReadStringName(source, "skill_id"),
            Amount = ReadInt(source, "amount"),
            SourceType = ReadStringName(source, "source_type"),
            SourceLabel = ReadString(source, "source_label"),
            ReasonText = ReadString(source, "reason_text"),
            AllowUnlocks = ReadBool(source, "allow_unlocks", true),
            RecordNearDeathUnbrokenManual = ReadBool(
                source,
                "record_near_death_unbroken_manual"
            ),
        };
    }

    private static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (!TryRead(source, key, out Variant value))
            return fallback ?? "";
        return ProgressionDataUtils.to_string_name(value);
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static bool TryRead(GDictionary source, string key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        if (source.ContainsKey(key))
        {
            value = source[key];
            return value.VariantType != Variant.Type.Nil;
        }
        StringName stringNameKey = new(key);
        if (source.ContainsKey(stringNameKey))
        {
            value = source[stringNameKey];
            return value.VariantType != Variant.Type.Nil;
        }
        return false;
    }
}
