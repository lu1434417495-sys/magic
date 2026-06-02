using System.Collections.Generic;
using Godot;

public sealed class BattleSpecialProfileGateResult
{
    public bool Allowed { get; set; }
    public StringName ProfileId { get; set; } = "";
    public StringName SkillId { get; set; } = "";
    public StringName BlockCode { get; set; } = "";
    public string PlayerMessage { get; set; } = "";
    public Dictionary<string, object> DebugDetails { get; } = new(System.StringComparer.Ordinal);

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var details = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, object> entry in DebugDetails)
        {
            details[entry.Key] = ToVariantBoundaryValue(entry.Value);
        }

        return new Godot.Collections.Dictionary
        {
            ["allowed"] = Allowed,
            ["profile_id"] = ProfileId,
            ["skill_id"] = SkillId,
            ["block_code"] = BlockCode,
            ["player_message"] = PlayerMessage,
            ["debug_details"] = details,
        };
    }

    private static Variant ToVariantBoundaryValue(object value)
    {
        if (value is IReadOnlyList<string> strings)
        {
            return Variant.From(new Godot.Collections.Array<string>(strings));
        }
        return value switch
        {
            null => Variant.From(""),
            string text => Variant.From(text),
            StringName name => Variant.From(name),
            bool flag => Variant.From(flag),
            int number => Variant.From(number),
            float number => Variant.From(number),
            double number => Variant.From(number),
            _ => Variant.From(value.ToString() ?? ""),
        };
    }
}
