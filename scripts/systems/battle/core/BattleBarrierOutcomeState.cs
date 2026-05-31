using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleBarrierOutcomeState : RefCounted
{
    private const int DefaultFatalDamage = 99999;

    public StringName outcome_type { get; set; } = "";
    public int amount { get; set; }
    public StringName damage_tag { get; set; } = "";
    public bool half_on_success { get; set; }
    public int success_amount { get; set; }
    public StringName success_damage_tag { get; set; } = "";
    public int fatal_damage { get; set; } = DefaultFatalDamage;
    public StringName status_id { get; set; } = "";
    public StringName save_ability { get; set; } = "";
    public StringName save_tag { get; set; } = "";
    public int save_dc { get; set; }
    public GDictionary @params { get; set; } = new();

    public bool IsEmpty => outcome_type == "";

    public static BattleBarrierOutcomeState from_runtime_dict(GDictionary source)
    {
        var outcome = new BattleBarrierOutcomeState();
        if (source == null || source.Count == 0)
        {
            return outcome;
        }

        outcome.outcome_type = ProgressionDataUtils.to_string_name(
            ReadStringName(
                source,
                "outcome_type",
                ReadStringName(source, "outcome")
            )
        );
        outcome.amount = ReadInt(source, "amount", 0);
        outcome.damage_tag = ReadStringName(source, "damage_tag");
        outcome.half_on_success = ReadBool(source, "half_on_success", false);
        outcome.success_amount = ReadInt(source, "success_amount", 0);
        outcome.success_damage_tag = ProgressionDataUtils.to_string_name(
            ReadStringName(source, "success_damage_tag")
        );
        outcome.fatal_damage = Mathf.Max(
            ReadInt(source, "fatal_damage", DefaultFatalDamage),
            1
        );
        outcome.status_id = ReadStringName(source, "status_id");
        outcome.save_ability = ReadStringName(source, "save_ability");
        outcome.save_tag = ReadStringName(source, "save_tag");
        outcome.save_dc = ReadInt(source, "save_dc", 0);
        outcome.@params = (GDictionary)ReadDictionary(source, "params").Duplicate(true);
        return outcome;
    }

    public GDictionary to_runtime_dict()
    {
        return new GDictionary
        {
            ["outcome_type"] = outcome_type.ToString(),
            ["amount"] = amount,
            ["damage_tag"] = damage_tag.ToString(),
            ["half_on_success"] = half_on_success,
            ["success_amount"] = success_amount,
            ["success_damage_tag"] = success_damage_tag.ToString(),
            ["fatal_damage"] = Mathf.Max(fatal_damage, 1),
            ["status_id"] = status_id.ToString(),
            ["save_ability"] = save_ability.ToString(),
            ["save_tag"] = save_tag.ToString(),
            ["save_dc"] = save_dc,
            ["params"] = @params?.Duplicate(true) ?? new GDictionary(),
        };
    }

    private static StringName ReadStringName(GDictionary data, string key, StringName fallback = default)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback == default ? new StringName("") : fallback;
        }
        Variant value = data[key];
        if (value.VariantType == Variant.Type.StringName)
        {
            return value.AsStringName();
        }
        if (value.VariantType == Variant.Type.String)
        {
            return new StringName(value.AsString());
        }
        return fallback == default ? new StringName("") : fallback;
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

}
