using Godot.Collections;

internal static class GodotDictionaryExtensions
{
    internal static Dictionary AsGodotDictionary(this Dictionary dictionary)
    {
        return dictionary ?? new Dictionary();
    }

}
