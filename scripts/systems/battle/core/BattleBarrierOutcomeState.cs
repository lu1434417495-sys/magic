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
            GdInterop.GetStringName(
                source,
                "outcome_type",
                GdInterop.GetStringName(source, "outcome", "")
            )
        );
        outcome.amount = GdInterop.GetInt(source, "amount", 0);
        outcome.damage_tag = GdInterop.GetStringName(source, "damage_tag", "");
        outcome.half_on_success = GdInterop.GetBool(source, "half_on_success", false);
        outcome.success_amount = GdInterop.GetInt(source, "success_amount", 0);
        outcome.success_damage_tag = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(source, "success_damage_tag", "")
        );
        outcome.fatal_damage = Mathf.Max(
            GdInterop.GetInt(source, "fatal_damage", DefaultFatalDamage),
            1
        );
        outcome.status_id = GdInterop.GetStringName(source, "status_id", "");
        outcome.save_ability = GdInterop.GetStringName(source, "save_ability", "");
        outcome.save_tag = GdInterop.GetStringName(source, "save_tag", "");
        outcome.save_dc = GdInterop.GetInt(source, "save_dc", 0);
        outcome.@params = (GDictionary)GdInterop.GetDictionary(source, "params").Duplicate(true);
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

}
