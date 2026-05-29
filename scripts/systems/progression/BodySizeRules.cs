using Godot;

[GlobalClass]
public partial class BodySizeRules : RefCounted
{
    public static StringName BODY_SIZE_CATEGORY_TINY() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_TINY;

    public static StringName BODY_SIZE_CATEGORY_SMALL() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_SMALL;

    public static StringName BODY_SIZE_CATEGORY_MEDIUM() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_MEDIUM;

    public static StringName BODY_SIZE_CATEGORY_LARGE() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_LARGE;

    public static StringName BODY_SIZE_CATEGORY_HUGE() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_HUGE;

    public static StringName BODY_SIZE_CATEGORY_GARGANTUAN() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_GARGANTUAN;

    public static StringName BODY_SIZE_CATEGORY_BOSS() =>
        BodySizeContentRules.BODY_SIZE_CATEGORY_BOSS;

    public static int BODY_SIZE_TINY() => BodySizeContentRules.BODY_SIZE_TINY;

    public static int BODY_SIZE_SMALL() => BodySizeContentRules.BODY_SIZE_SMALL;

    public static int BODY_SIZE_MEDIUM() => BodySizeContentRules.BODY_SIZE_MEDIUM;

    public static int BODY_SIZE_LARGE() => BodySizeContentRules.BODY_SIZE_LARGE;

    public static int BODY_SIZE_HUGE() => BodySizeContentRules.BODY_SIZE_HUGE;

    public static int BODY_SIZE_GARGANTUAN() => BodySizeContentRules.BODY_SIZE_GARGANTUAN;

    public static int BODY_SIZE_BOSS() => BodySizeContentRules.BODY_SIZE_BOSS;

    public static bool is_valid_body_size_category(StringName category) =>
        BodySizeContentRules.is_valid_body_size_category(category);

    public static bool is_valid_body_size(int bodySize) =>
        BodySizeContentRules.is_valid_body_size(bodySize);

    public static int get_body_size_for_category(StringName category) =>
        BodySizeContentRules.get_body_size_for_category(category);


    public static bool body_size_matches_category(StringName category, int bodySize) =>
        BodySizeContentRules.body_size_matches_category(category, bodySize);

    public static Vector2I get_footprint_for_body_size(int bodySize) =>
        BodySizeContentRules.get_footprint_for_body_size(bodySize);

    public static Vector2I get_footprint_for_category(StringName category) =>
        BodySizeContentRules.get_footprint_for_category(category);
}
