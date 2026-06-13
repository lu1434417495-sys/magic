using Godot;

internal partial class BattleFateEventBus : RefCounted
{
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
                readonlyDict[key] = ToVariant(MakeReadOnlyValue(rawDictionary[key]));
            readonlyDict.MakeReadOnly();
            return readonlyDict;
        }
        if (rawValue is Godot.Collections.Array rawArray)
        {
            var readonlyArray = new Godot.Collections.Array();
            foreach (var entry in rawArray)
                readonlyArray.Add(ToVariant(MakeReadOnlyValue(entry)));
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
                readonlyDict[key] = ToVariant(MakeReadOnlyValue(dict[key]));

            readonlyDict.MakeReadOnly();

            return readonlyDict;
        }

        if (value.VariantType == Variant.Type.Array)
        {
            var arr = value.AsGodotArray();

            var readonlyArray = new Godot.Collections.Array();

            foreach (var entry in arr)
                readonlyArray.Add(ToVariant(MakeReadOnlyValue(entry)));

            readonlyArray.MakeReadOnly();

            return readonlyArray;
        }

        return value;
    }

    private static Variant ToVariant(object value)
    {
        return value switch
        {
            Variant variant => variant,
            string text => text,
            StringName stringName => stringName,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            Vector2I coord => coord,
            Godot.Collections.Dictionary dictionary => dictionary,
            Godot.Collections.Array array => array,
            GodotObject godotObject => godotObject,
            _ => default,
        };
    }
}
