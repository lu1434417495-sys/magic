using Godot;

[GlobalClass]
public partial class BattleFateEventBus : RefCounted
{
    public static readonly StringName EVENT_CRITICAL_FAIL = "critical_fail";

    public static readonly StringName EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE =
        "critical_success_under_disadvantage";

    public static readonly StringName EVENT_HIGH_THREAT_CRITICAL_HIT = "high_threat_critical_hit";

    public static readonly StringName EVENT_ORDINARY_MISS = "ordinary_miss";

    public static readonly StringName EVENT_HARDSHIP_SURVIVAL = "hardship_survival";

    [Signal]
    public delegate void EventDispatchedEventHandler(
        StringName eventType,
        Godot.Collections.Dictionary payload
    );

    public void dispatch(StringName eventType, Godot.Collections.Dictionary payload = null)
    {
        if (eventType == "")
            return;

        var readonlyPayload = MakeReadOnlyValue(
            payload ?? new Godot.Collections.Dictionary()
        ) as Godot.Collections.Dictionary;

        EmitSignal(
            SignalName.EventDispatched,
            eventType,
            readonlyPayload ?? new Godot.Collections.Dictionary()
        );
    }

    private object MakeReadOnlyValue(object rawValue)
    {
        if (rawValue is Godot.Collections.Dictionary rawDictionary)
        {
            var readonlyDict = new Godot.Collections.Dictionary();
            foreach (var key in rawDictionary.Keys)
                readonlyDict[key] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    MakeReadOnlyValue(rawDictionary[key])
                );
            readonlyDict.MakeReadOnly();
            return readonlyDict;
        }
        if (rawValue is Godot.Collections.Array rawArray)
        {
            var readonlyArray = new Godot.Collections.Array();
            foreach (var entry in rawArray)
                readonlyArray.Add(GdInterop.GetValueOrDefault(null, "", MakeReadOnlyValue(entry)));
            readonlyArray.MakeReadOnly();
            return readonlyArray;
        }
        if (rawValue is not Variant value)
            return rawValue;

        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dict = value.AsGodotDictionary();

            var readonlyDict = new Godot.Collections.Dictionary();

            foreach (var key in dict.Keys)
                readonlyDict[key] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    MakeReadOnlyValue(dict[key])
                );

            readonlyDict.MakeReadOnly();

            return readonlyDict;
        }

        if (value.VariantType == Variant.Type.Array)
        {
            var arr = value.AsGodotArray();

            var readonlyArray = new Godot.Collections.Array();

            foreach (var entry in arr)
                readonlyArray.Add(GdInterop.GetValueOrDefault(null, "", MakeReadOnlyValue(entry)));

            readonlyArray.MakeReadOnly();

            return readonlyArray;
        }

        return value;
    }
}
