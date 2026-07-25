using System;
using Godot;

internal readonly record struct BattleWeaponDiceValues(
    bool IsPresent,
    int DiceCount,
    int DiceSides,
    int FlatBonus
)
{
    internal static BattleWeaponDiceValues PresentEmpty =>
        new(true, 0, 0, 0);

    internal static BattleWeaponDiceValues FromRaw(WeaponDice dice) =>
        dice == null
            ? default
            : new BattleWeaponDiceValues(
                true,
                dice.dice_count,
                dice.dice_sides,
                dice.flat_bonus
            );

    internal bool HasUsableDice =>
        IsPresent
        && DiceCount > 0
        && DiceSides > 0;

    internal BattleWeaponDiceValues Normalize() =>
        HasUsableDice
            ? new BattleWeaponDiceValues(
                true,
                DiceCount,
                DiceSides,
                FlatBonus
            )
            : PresentEmpty;

    internal WeaponDice ToNormalizedDice()
    {
        BattleWeaponDiceValues normalized = Normalize();
        return normalized.HasUsableDice
            ? new WeaponDice
            {
                dice_count = normalized.DiceCount,
                dice_sides = normalized.DiceSides,
                flat_bonus = normalized.FlatBonus,
            }
            : new WeaponDice();
    }

    internal WeaponDice ToExactDice() =>
        !IsPresent
            ? null
            : new WeaponDice
            {
                dice_count = DiceCount,
                dice_sides = DiceSides,
                flat_bonus = FlatBonus,
            };
}

internal readonly record struct BattleWeaponProjectionValues(
    StringName ProfileKind,
    StringName ItemId,
    StringName ProfileTypeId,
    StringName RangeType,
    StringName Family,
    StringName CurrentGrip,
    int AttackRange,
    BattleWeaponDiceValues OneHandedDice,
    BattleWeaponDiceValues TwoHandedDice,
    bool IsVersatile,
    bool UsesTwoHands,
    StringName PhysicalDamageTag
)
{
    internal BattleWeaponDiceValues ActiveDice =>
        UsesTwoHands ? TwoHandedDice : OneHandedDice;

    internal static BattleWeaponProjectionValues Clear =>
        new(
            BattleUnitState.ToStringName(BattleWeaponProfileKind.None),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            new StringName(""),
            BattleUnitState.ToStringName(BattleWeaponGripKind.None),
            0,
            BattleWeaponDiceValues.PresentEmpty,
            BattleWeaponDiceValues.PresentEmpty,
            false,
            false,
            new StringName("")
        );
}

internal readonly record struct BattleUnitWeaponProjectionReadView(
    bool OwnerPresent,
    BattleWeaponProjectionValues Values
)
{
    internal static BattleUnitWeaponProjectionReadView MissingOwner =>
        new(false, default);

    internal static BattleUnitWeaponProjectionReadView Present(
        BattleWeaponProjectionValues values
    ) =>
        new(true, values);
}

internal readonly record struct BattleUnitWeaponProjectionSnapshot(
    bool OwnerPresent,
    BattleWeaponProjectionValues Values
)
{
    internal static BattleUnitWeaponProjectionSnapshot MissingOwner =>
        new(false, default);

    internal static BattleUnitWeaponProjectionSnapshot Present(
        BattleWeaponProjectionValues values
    ) =>
        new(true, values);
}

internal sealed class BattleUnitWeaponProjectionState
{
    private BattleWeaponProjectionValues _values =
        BattleWeaponProjectionValues.Clear;

    internal BattleUnitWeaponProjectionReadView GetReadView() =>
        BattleUnitWeaponProjectionReadView.Present(_values);

    internal void Clear()
    {
        _values = BattleWeaponProjectionValues.Clear;
    }

    internal void ApplyNormalized(WeaponProjection projection)
    {
        if (projection == null || projection.IsEmpty())
        {
            Clear();
            return;
        }

        StringName profileKind = NormalizeProfileKind(
            projection.weapon_profile_kind
        );
        if (
            profileKind
            == BattleUnitState.ToStringName(BattleWeaponProfileKind.None)
        )
        {
            Clear();
            return;
        }

        StringName currentGrip = NormalizeGrip(
            projection.weapon_current_grip
        );
        int attackRange = Math.Max(projection.weapon_attack_range, 0);
        BattleWeaponDiceValues oneHandedDice =
            BattleWeaponDiceValues
                .FromRaw(projection.weapon_one_handed_dice)
                .Normalize();
        BattleWeaponDiceValues twoHandedDice =
            BattleWeaponDiceValues
                .FromRaw(projection.weapon_two_handed_dice)
                .Normalize();
        bool usesTwoHands = projection.weapon_uses_two_hands;

        if (usesTwoHands)
        {
            currentGrip = BattleUnitState.ToStringName(
                BattleWeaponGripKind.TwoHanded
            );
        }
        else if (
            currentGrip
            == BattleUnitState.ToStringName(
                BattleWeaponGripKind.TwoHanded
            )
        )
        {
            currentGrip = oneHandedDice.HasUsableDice
                ? BattleUnitState.ToStringName(
                    BattleWeaponGripKind.OneHanded
                )
                : BattleUnitState.ToStringName(
                    BattleWeaponGripKind.None
                );
        }

        if (attackRange <= 0)
        {
            currentGrip = BattleUnitState.ToStringName(
                BattleWeaponGripKind.None
            );
            usesTwoHands = false;
        }

        _values = new BattleWeaponProjectionValues(
            profileKind,
            NormalizeStringName(projection.weapon_item_id),
            NormalizeStringName(projection.weapon_profile_type_id),
            NormalizeStringName(projection.weapon_range_type),
            NormalizeStringName(projection.weapon_family),
            currentGrip,
            attackRange,
            oneHandedDice,
            twoHandedDice,
            projection.weapon_is_versatile,
            usesTwoHands,
            NormalizeStringName(
                projection.weapon_physical_damage_tag
            )
        );
    }

    internal void NormalizeCanonicalInPlace()
    {
        StringName profileKind = NormalizeProfileKind(
            _values.ProfileKind
        );
        if (
            profileKind
            == BattleUnitState.ToStringName(BattleWeaponProfileKind.None)
        )
        {
            Clear();
            return;
        }

        StringName currentGrip = NormalizeGrip(_values.CurrentGrip);
        int attackRange = Math.Max(_values.AttackRange, 0);
        BattleWeaponDiceValues oneHandedDice =
            _values.OneHandedDice.Normalize();
        BattleWeaponDiceValues twoHandedDice =
            _values.TwoHandedDice.Normalize();
        bool usesTwoHands = _values.UsesTwoHands;

        if (usesTwoHands)
        {
            currentGrip = BattleUnitState.ToStringName(
                BattleWeaponGripKind.TwoHanded
            );
        }
        else if (
            currentGrip
            == BattleUnitState.ToStringName(
                BattleWeaponGripKind.TwoHanded
            )
        )
        {
            currentGrip = oneHandedDice.HasUsableDice
                ? BattleUnitState.ToStringName(
                    BattleWeaponGripKind.OneHanded
                )
                : BattleUnitState.ToStringName(
                    BattleWeaponGripKind.None
                );
        }

        if (attackRange <= 0)
        {
            currentGrip = BattleUnitState.ToStringName(
                BattleWeaponGripKind.None
            );
            usesTwoHands = false;
        }

        _values = _values with
        {
            ProfileKind = profileKind,
            RangeType = NormalizeStringName(_values.RangeType),
            CurrentGrip = currentGrip,
            AttackRange = attackRange,
            OneHandedDice = oneHandedDice,
            TwoHandedDice = twoHandedDice,
            UsesTwoHands = usesTwoHands,
        };
    }

    internal int GetAttackRangeClamped() =>
        Math.Max(_values.AttackRange, 0);

    internal WeaponDice CopyOneHandedDice() =>
        _values.OneHandedDice.ToNormalizedDice();

    internal WeaponDice CopyTwoHandedDice() =>
        _values.TwoHandedDice.ToNormalizedDice();

    internal WeaponDice CopyActiveDice() =>
        _values.ActiveDice.ToNormalizedDice();

    internal BattleUnitWeaponProjectionSnapshot CaptureRaw() =>
        BattleUnitWeaponProjectionSnapshot.Present(_values);

    internal void RestoreRaw(
        BattleUnitWeaponProjectionSnapshot snapshot
    )
    {
        _values = snapshot.Values;
    }

    internal BattleUnitWeaponProjectionState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitWeaponProjectionState FromRaw(
        BattleUnitWeaponProjectionSnapshot snapshot
    )
    {
        var result = new BattleUnitWeaponProjectionState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static StringName NormalizeProfileKind(StringName value)
    {
        BattleWeaponProfileKind kind =
            BattleUnitState.ToWeaponProfileKind(value);
        return kind switch
        {
            BattleWeaponProfileKind.Unarmed
            or BattleWeaponProfileKind.Natural
            or BattleWeaponProfileKind.Equipped =>
                BattleUnitState.ToStringName(kind),
            _ => BattleUnitState.ToStringName(
                BattleWeaponProfileKind.None
            ),
        };
    }

    private static StringName NormalizeGrip(StringName value)
    {
        BattleWeaponGripKind kind =
            BattleUnitState.ToWeaponGripKind(value);
        return kind switch
        {
            BattleWeaponGripKind.OneHanded
            or BattleWeaponGripKind.TwoHanded =>
                BattleUnitState.ToStringName(kind),
            _ => BattleUnitState.ToStringName(
                BattleWeaponGripKind.None
            ),
        };
    }

    private static StringName NormalizeStringName(StringName value) =>
        value ?? new StringName("");
}
