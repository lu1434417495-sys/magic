using System.Collections.Generic;
using Godot;

internal static class BattleSpecialProfileGateResultProjection
{
    internal static Godot.Collections.Dictionary Project(BattleSpecialProfileGateResult result)
    {
        if (result == null)
            return new Godot.Collections.Dictionary();

        var details = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, object> entry in result.DebugDetails)
        {
            details[entry.Key] = ToVariantBoundaryValue(entry.Value);
        }

        return new Godot.Collections.Dictionary
        {
            ["allowed"] = result.Allowed,
            ["profile_id"] = result.ProfileId,
            ["skill_id"] = result.SkillId,
            ["block_code"] = result.BlockCode,
            ["player_message"] = result.PlayerMessage,
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
