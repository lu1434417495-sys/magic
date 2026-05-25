using Godot;

[GlobalClass]
public partial class BattleUnitConstants : RefCounted
{
    private static readonly StringName WeaponProfileKindNone = "none";
    private static readonly StringName WeaponProfileKindUnarmed = "unarmed";
    private static readonly StringName WeaponProfileKindNatural = "natural";
    private static readonly StringName WeaponProfileKindEquipped = "equipped";

    private static readonly StringName WeaponGripNone = "none";
    private static readonly StringName WeaponGripOneHanded = "one_handed";
    private static readonly StringName WeaponGripTwoHanded = "two_handed";

    private static readonly StringName CombatResourceHp = "hp";
    private static readonly StringName CombatResourceStamina = "stamina";
    private static readonly StringName CombatResourceMp = "mp";
    private static readonly StringName CombatResourceAura = "aura";

    public static int DEFAULT_MOVE_POINTS_PER_TURN() => 2;
    public static int DEFAULT_ACTION_THRESHOLD() => 120;

    public static StringName WEAPON_PROFILE_KIND_NONE() => WeaponProfileKindNone;
    public static StringName WEAPON_PROFILE_KIND_UNARMED() => WeaponProfileKindUnarmed;
    public static StringName WEAPON_PROFILE_KIND_NATURAL() => WeaponProfileKindNatural;
    public static StringName WEAPON_PROFILE_KIND_EQUIPPED() => WeaponProfileKindEquipped;

    public static StringName WEAPON_GRIP_NONE() => WeaponGripNone;
    public static StringName WEAPON_GRIP_ONE_HANDED() => WeaponGripOneHanded;
    public static StringName WEAPON_GRIP_TWO_HANDED() => WeaponGripTwoHanded;

    public static StringName COMBAT_RESOURCE_HP() => CombatResourceHp;
    public static StringName COMBAT_RESOURCE_STAMINA() => CombatResourceStamina;
    public static StringName COMBAT_RESOURCE_MP() => CombatResourceMp;
    public static StringName COMBAT_RESOURCE_AURA() => CombatResourceAura;

    public static int BODY_SIZE_TINY() => 1;
    public static int BODY_SIZE_SMALL() => 1;
    public static int BODY_SIZE_MEDIUM() => 2;
    public static int BODY_SIZE_LARGE() => 3;
    public static int BODY_SIZE_HUGE() => 4;
    public static int BODY_SIZE_GARGANTUAN() => 5;
    public static int BODY_SIZE_BOSS() => 6;

    public static Godot.Collections.Array<StringName> DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS()
    {
        return new Godot.Collections.Array<StringName>
        {
            CombatResourceHp,
            CombatResourceStamina,
        };
    }

    public static Godot.Collections.Array<StringName> VALID_COMBAT_RESOURCE_IDS()
    {
        return new Godot.Collections.Array<StringName>
        {
            CombatResourceHp,
            CombatResourceStamina,
            CombatResourceMp,
            CombatResourceAura,
        };
    }
}
