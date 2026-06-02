using System.Collections.Generic;
using Godot;

public static class BodySizeContentRules
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

    public static readonly IReadOnlySet<StringName> ValidBodySizeCategories =
        new HashSet<StringName>
        {
            BODY_SIZE_CATEGORY_TINY,
            BODY_SIZE_CATEGORY_SMALL,
            BODY_SIZE_CATEGORY_MEDIUM,
            BODY_SIZE_CATEGORY_LARGE,
            BODY_SIZE_CATEGORY_HUGE,
            BODY_SIZE_CATEGORY_GARGANTUAN,
            BODY_SIZE_CATEGORY_BOSS,
        };

    public static readonly IReadOnlySet<int> ValidBodySizes =
        new HashSet<int>
        {
            BODY_SIZE_TINY,
            BODY_SIZE_MEDIUM,
            BODY_SIZE_LARGE,
            BODY_SIZE_HUGE,
            BODY_SIZE_GARGANTUAN,
            BODY_SIZE_BOSS,
        };

    public static readonly IReadOnlyDictionary<StringName, int> CategoryToBodySize =
        new Dictionary<StringName, int>
        {
            [BODY_SIZE_CATEGORY_TINY] = BODY_SIZE_TINY,
            [BODY_SIZE_CATEGORY_SMALL] = BODY_SIZE_SMALL,
            [BODY_SIZE_CATEGORY_MEDIUM] = BODY_SIZE_MEDIUM,
            [BODY_SIZE_CATEGORY_LARGE] = BODY_SIZE_LARGE,
            [BODY_SIZE_CATEGORY_HUGE] = BODY_SIZE_HUGE,
            [BODY_SIZE_CATEGORY_GARGANTUAN] = BODY_SIZE_GARGANTUAN,
            [BODY_SIZE_CATEGORY_BOSS] = BODY_SIZE_BOSS,
        };

    public static readonly IReadOnlyDictionary<int, StringName> BodySizeToCategory =
        new Dictionary<int, StringName>
        {
            [BODY_SIZE_SMALL] = BODY_SIZE_CATEGORY_SMALL,
            [BODY_SIZE_MEDIUM] = BODY_SIZE_CATEGORY_MEDIUM,
            [BODY_SIZE_LARGE] = BODY_SIZE_CATEGORY_LARGE,
            [BODY_SIZE_HUGE] = BODY_SIZE_CATEGORY_HUGE,
            [BODY_SIZE_GARGANTUAN] = BODY_SIZE_CATEGORY_GARGANTUAN,
            [BODY_SIZE_BOSS] = BODY_SIZE_CATEGORY_BOSS,
        };

    public static readonly IReadOnlyDictionary<int, Vector2I> BodySizeToFootprint =
        new Dictionary<int, Vector2I>
        {
            [BODY_SIZE_TINY] = Vector2I.One,
            [BODY_SIZE_MEDIUM] = Vector2I.One,
            [BODY_SIZE_LARGE] = new Vector2I(2, 2),
            [BODY_SIZE_HUGE] = new Vector2I(2, 2),
            [BODY_SIZE_GARGANTUAN] = new Vector2I(3, 3),
            [BODY_SIZE_BOSS] = new Vector2I(3, 3),
        };

    public static bool IsValidBodySizeCategory(StringName category) =>
        ValidBodySizeCategories.Contains(category);

    public static bool is_valid_body_size_category(StringName category) =>
        IsValidBodySizeCategory(category);

    public static bool IsValidBodySize(int bodySize) => ValidBodySizes.Contains(bodySize);

    public static bool is_valid_body_size(int bodySize) => IsValidBodySize(bodySize);

    public static int GetBodySizeForCategory(StringName category) =>
        CategoryToBodySize.TryGetValue(category, out int result) ? result : 0;

    public static int get_body_size_for_category(StringName category) =>
        GetBodySizeForCategory(category);

    public static bool BodySizeMatchesCategory(StringName category, int bodySize) =>
        IsValidBodySizeCategory(category) && GetBodySizeForCategory(category) == bodySize;

    public static bool body_size_matches_category(StringName category, int bodySize) =>
        BodySizeMatchesCategory(category, bodySize);

    public static Vector2I GetFootprintForBodySize(int bodySize) =>
        BodySizeToFootprint.TryGetValue(bodySize, out Vector2I result)
            ? result
            : Vector2I.One;

    public static Vector2I get_footprint_for_body_size(int bodySize) =>
        GetFootprintForBodySize(bodySize);

    public static Vector2I GetFootprintForCategory(StringName category) =>
        GetFootprintForBodySize(GetBodySizeForCategory(category));

    public static Vector2I get_footprint_for_category(StringName category) =>
        GetFootprintForCategory(category);
}
