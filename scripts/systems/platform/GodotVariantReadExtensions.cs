using GDictionary = Godot.Collections.Dictionary;
using Godot;

// Strict helper for the remaining real Godot Variant boundaries (save/schema and
// world-record decoders). Readers intentionally require the exact variant type.
internal static class GodotVariantReadExtensions
{
    internal static bool TryAsDictionary(this Variant value, out GDictionary result)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            result = value.AsGodotDictionary();
            return true;
        }
        result = null;
        return false;
    }
}
