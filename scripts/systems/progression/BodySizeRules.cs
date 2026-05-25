using Godot;

[GlobalClass]
public partial class BodySizeRules : RefCounted
{
    private static readonly GDScript BodySizeContentRules = GD.Load<GDScript>("res://scripts/player/progression/body_size_content_rules.gd");

    public static StringName BODY_SIZE_CATEGORY_TINY() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_TINY").AsStringName();
    public static StringName BODY_SIZE_CATEGORY_SMALL() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_SMALL").AsStringName();
    public static StringName BODY_SIZE_CATEGORY_MEDIUM() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_MEDIUM").AsStringName();
    public static StringName BODY_SIZE_CATEGORY_LARGE() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_LARGE").AsStringName();
    public static StringName BODY_SIZE_CATEGORY_HUGE() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_HUGE").AsStringName();
    public static StringName BODY_SIZE_CATEGORY_GARGANTUAN() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_GARGANTUAN").AsStringName();
    public static StringName BODY_SIZE_CATEGORY_BOSS() => BodySizeContentRules.Get("BODY_SIZE_CATEGORY_BOSS").AsStringName();

    public static int BODY_SIZE_TINY() => BodySizeContentRules.Get("BODY_SIZE_TINY").AsInt32();
    public static int BODY_SIZE_SMALL() => BodySizeContentRules.Get("BODY_SIZE_SMALL").AsInt32();
    public static int BODY_SIZE_MEDIUM() => BodySizeContentRules.Get("BODY_SIZE_MEDIUM").AsInt32();
    public static int BODY_SIZE_LARGE() => BodySizeContentRules.Get("BODY_SIZE_LARGE").AsInt32();
    public static int BODY_SIZE_HUGE() => BodySizeContentRules.Get("BODY_SIZE_HUGE").AsInt32();
    public static int BODY_SIZE_GARGANTUAN() => BodySizeContentRules.Get("BODY_SIZE_GARGANTUAN").AsInt32();
    public static int BODY_SIZE_BOSS() => BodySizeContentRules.Get("BODY_SIZE_BOSS").AsInt32();

    public static bool is_valid_body_size_category(StringName category) => (bool)BodySizeContentRules.Call("is_valid_body_size_category", category);
    public static bool is_valid_body_size(int body_size) => (bool)BodySizeContentRules.Call("is_valid_body_size", body_size);
    public static int get_body_size_for_category(StringName category) => (int)BodySizeContentRules.Call("get_body_size_for_category", category);
    public static StringName get_category_for_body_size(int body_size) => (StringName)BodySizeContentRules.Call("get_category_for_body_size", body_size);
    public static bool body_size_matches_category(StringName category, int body_size) => (bool)BodySizeContentRules.Call("body_size_matches_category", category, body_size);
    public static Vector2I get_footprint_for_body_size(int body_size) => (Vector2I)BodySizeContentRules.Call("get_footprint_for_body_size", body_size);
    public static Vector2I get_footprint_for_category(StringName category) => (Vector2I)BodySizeContentRules.Call("get_footprint_for_category", category);
}
