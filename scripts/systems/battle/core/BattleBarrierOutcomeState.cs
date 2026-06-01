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

    public bool IsEmpty => outcome_type == "";

    public static BattleBarrierOutcomeState from_runtime_dict(GDictionary source)
    {
        var outcome = new BattleBarrierOutcomeState();
        if (source == null || source.Count == 0)
        {
            return outcome;
        }

        outcome.outcome_type = ReadStringName(source, "outcome_type");
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
        };
    }

    private static StringName ReadStringName(GDictionary data, string key, StringName fallback = default)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback == default ? new StringName("") : fallback;
        }
        return ProgressionDataUtils.to_string_name(data[key]);
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsInt32();
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsBool();
    }

}
