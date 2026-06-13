using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleBarrierOutcomeState
{
    private const int DefaultFatalDamage = 99999;

    public StringName OutcomeType { get; set; } = "";
    internal BarrierOutcomeKind OutcomeKind
    {
        get => BarrierOutcomeDef.ToOutcomeKind(OutcomeType);
        set => OutcomeType = BarrierOutcomeDef.ToStringName(value);
    }
    public int Amount { get; set; }
    public StringName DamageTag { get; set; } = "";
    public bool HalfOnSuccess { get; set; }
    public int SuccessAmount { get; set; }
    public StringName SuccessDamageTag { get; set; } = "";
    public int FatalDamage { get; set; } = DefaultFatalDamage;
    public StringName StatusId { get; set; } = "";
    public StringName SaveAbility { get; set; } = "";
    public StringName SaveTag { get; set; } = "";
    public int SaveDc { get; set; }

    public bool IsEmpty => OutcomeKind == BarrierOutcomeKind.None;

    internal static BattleBarrierOutcomeState FromRuntimeDict(GDictionary source)
    {
        var outcome = new BattleBarrierOutcomeState();
        if (source == null || source.Count == 0)
        {
            return outcome;
        }

        outcome.OutcomeType = ReadStringName(source, "outcome_type");
        outcome.Amount = ReadInt(source, "amount", 0);
        outcome.DamageTag = ReadStringName(source, "damage_tag");
        outcome.HalfOnSuccess = ReadBool(source, "half_on_success", false);
        outcome.SuccessAmount = ReadInt(source, "success_amount", 0);
        outcome.SuccessDamageTag = ReadStringName(source, "success_damage_tag");
        outcome.FatalDamage = Mathf.Max(ReadInt(source, "fatal_damage", DefaultFatalDamage), 1);
        outcome.StatusId = ReadStringName(source, "status_id");
        outcome.SaveAbility = ReadStringName(source, "save_ability");
        outcome.SaveTag = ReadStringName(source, "save_tag");
        outcome.SaveDc = ReadInt(source, "save_dc", 0);
        return outcome;
    }

    internal GDictionary ToRuntimeDict()
    {
        return new GDictionary
        {
            ["outcome_type"] = OutcomeType.ToString(),
            ["amount"] = Amount,
            ["damage_tag"] = DamageTag.ToString(),
            ["half_on_success"] = HalfOnSuccess,
            ["success_amount"] = SuccessAmount,
            ["success_damage_tag"] = SuccessDamageTag.ToString(),
            ["fatal_damage"] = Mathf.Max(FatalDamage, 1),
            ["status_id"] = StatusId.ToString(),
            ["save_ability"] = SaveAbility.ToString(),
            ["save_tag"] = SaveTag.ToString(),
            ["save_dc"] = SaveDc,
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
