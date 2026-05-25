using Godot;

[GlobalClass]
public partial class BattleFateEventBus : RefCounted
{
    public static readonly StringName EVENT_CRITICAL_FAIL = "critical_fail";
    public static readonly StringName EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE = "critical_success_under_disadvantage";
    public static readonly StringName EVENT_HIGH_THREAT_CRITICAL_HIT = "high_threat_critical_hit";
    public static readonly StringName EVENT_ORDINARY_MISS = "ordinary_miss";
    public static readonly StringName EVENT_HARDSHIP_SURVIVAL = "hardship_survival";

    [Signal]
    public delegate void EventDispatchedEventHandler(StringName eventType, Godot.Collections.Dictionary payload);

    public void dispatch(StringName eventType, Godot.Collections.Dictionary payload = null)
    {
        if (eventType == "")
            return;
        var readonlyPayload = _make_variant_read_only(payload ?? new Godot.Collections.Dictionary());
        EmitSignal(SignalName.EventDispatched, eventType, readonlyPayload);
    }

    private Variant _make_variant_read_only(Variant value)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dict = value.AsGodotDictionary();
            var readonlyDict = new Godot.Collections.Dictionary();
            foreach (var key in dict.Keys)
                readonlyDict[key] = _make_variant_read_only(dict[key]);
            readonlyDict.MakeReadOnly();
            return Variant.From(readonlyDict);
        }
        if (value.VariantType == Variant.Type.Array)
        {
            var arr = value.AsGodotArray();
            var readonlyArray = new Godot.Collections.Array();
            foreach (var entry in arr)
                readonlyArray.Add(_make_variant_read_only(entry));
            readonlyArray.MakeReadOnly();
            return Variant.From(readonlyArray);
        }
        return value;
    }
}
