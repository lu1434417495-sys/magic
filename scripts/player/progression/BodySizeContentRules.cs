using Godot;

[GlobalClass]
public partial class BodySizeContentRules : RefCounted
{
    public static readonly StringName BODY_SIZE_CATEGORY_TINY = "tiny";

    public static readonly StringName BODY_SIZE_CATEGORY_SMALL = "small";

    public static readonly StringName BODY_SIZE_CATEGORY_MEDIUM = "medium";

    public static readonly StringName BODY_SIZE_CATEGORY_LARGE = "large";

    public static readonly StringName BODY_SIZE_CATEGORY_HUGE = "huge";

    public static readonly StringName BODY_SIZE_CATEGORY_GARGANTUAN = "gargantuan";

    public static readonly StringName BODY_SIZE_CATEGORY_BOSS = "boss";

    public const int BODY_SIZE_TINY = 1;

    public const int BODY_SIZE_SMALL = 1;

    public const int BODY_SIZE_MEDIUM = 2;

    public const int BODY_SIZE_LARGE = 3;

    public const int BODY_SIZE_HUGE = 4;

    public const int BODY_SIZE_GARGANTUAN = 5;

    public const int BODY_SIZE_BOSS = 6;

    private static readonly Godot.Collections.Dictionary VALID_BODY_SIZE_CATEGORIES = new()
    {
        { BODY_SIZE_CATEGORY_TINY, true },
        { BODY_SIZE_CATEGORY_SMALL, true },
        { BODY_SIZE_CATEGORY_MEDIUM, true },
        { BODY_SIZE_CATEGORY_LARGE, true },
        { BODY_SIZE_CATEGORY_HUGE, true },
        { BODY_SIZE_CATEGORY_GARGANTUAN, true },
        { BODY_SIZE_CATEGORY_BOSS, true },
    };

    private static readonly Godot.Collections.Dictionary VALID_BODY_SIZES = new()
    {
        { BODY_SIZE_TINY, true },
        { BODY_SIZE_MEDIUM, true },
        { BODY_SIZE_LARGE, true },
        { BODY_SIZE_HUGE, true },
        { BODY_SIZE_GARGANTUAN, true },
        { BODY_SIZE_BOSS, true },
    };

    private static readonly Godot.Collections.Dictionary CATEGORY_TO_BODY_SIZE = new()
    {
        { BODY_SIZE_CATEGORY_TINY, BODY_SIZE_TINY },
        { BODY_SIZE_CATEGORY_SMALL, BODY_SIZE_SMALL },
        { BODY_SIZE_CATEGORY_MEDIUM, BODY_SIZE_MEDIUM },
        { BODY_SIZE_CATEGORY_LARGE, BODY_SIZE_LARGE },
        { BODY_SIZE_CATEGORY_HUGE, BODY_SIZE_HUGE },
        { BODY_SIZE_CATEGORY_GARGANTUAN, BODY_SIZE_GARGANTUAN },
        { BODY_SIZE_CATEGORY_BOSS, BODY_SIZE_BOSS },
    };

    private static readonly Godot.Collections.Dictionary BODY_SIZE_TO_CATEGORY = new()
    {
        { BODY_SIZE_SMALL, BODY_SIZE_CATEGORY_SMALL },
        { BODY_SIZE_MEDIUM, BODY_SIZE_CATEGORY_MEDIUM },
        { BODY_SIZE_LARGE, BODY_SIZE_CATEGORY_LARGE },
        { BODY_SIZE_HUGE, BODY_SIZE_CATEGORY_HUGE },
        { BODY_SIZE_GARGANTUAN, BODY_SIZE_CATEGORY_GARGANTUAN },
        { BODY_SIZE_BOSS, BODY_SIZE_CATEGORY_BOSS },
    };

    private static readonly Godot.Collections.Dictionary BODY_SIZE_TO_FOOTPRINT = new()
    {
        { BODY_SIZE_TINY, Vector2I.One },
        { BODY_SIZE_MEDIUM, Vector2I.One },
        { BODY_SIZE_LARGE, new Vector2I(2, 2) },
        { BODY_SIZE_HUGE, new Vector2I(2, 2) },
        { BODY_SIZE_GARGANTUAN, new Vector2I(3, 3) },
        { BODY_SIZE_BOSS, new Vector2I(3, 3) },
    };

    public static bool is_valid_body_size_category(StringName category)
    {
        return VALID_BODY_SIZE_CATEGORIES.ContainsKey(category);
    }

    public static bool is_valid_body_size(int bodySize)
    {
        return VALID_BODY_SIZES.ContainsKey(bodySize);
    }

    public static int get_body_size_for_category(StringName category)
    {
        return CATEGORY_TO_BODY_SIZE.TryGetValue(category, out var result) ? result.AsInt32() : 0;
    }


    public static bool body_size_matches_category(StringName category, int bodySize)
    {
        return is_valid_body_size_category(category)
            && get_body_size_for_category(category) == bodySize;
    }

    public static Vector2I get_footprint_for_body_size(int bodySize)
    {
        return BODY_SIZE_TO_FOOTPRINT.TryGetValue(bodySize, out var result)
            ? result.AsVector2I()
            : Vector2I.One;
    }

    public static Vector2I get_footprint_for_category(StringName category)
    {
        return get_footprint_for_body_size(get_body_size_for_category(category));
    }
}
