using Godot;

internal sealed record BattleWindupSnapshot(
    int Tier,
    int StrengthModifier,
    int ConstitutionModifier,
    int TuPerTier,
    int TotalWindupTu,
    int AdditionalStaminaCost,
    int WeaponDiceMultiplier,
    BattleWindupWeaponSignature WeaponSignature
);

internal readonly record struct BattleWindupWeaponSignature(
    StringName ProfileKind,
    StringName ItemId,
    StringName InstanceId,
    StringName ProfileTypeId,
    StringName RangeType,
    StringName Family,
    StringName CurrentGrip,
    int AttackRange,
    int DiceCount,
    int DiceSides,
    int FlatBonus,
    bool UsesTwoHands,
    bool IsHeavy,
    StringName PhysicalDamageTag
)
{
    internal static BattleWindupWeaponSignature Capture(BattleUnitState unitState)
    {
        BattleWeaponProjectionValues weapon =
            unitState?.GetWeaponProjectionReadViewTyped().Values
            ?? BattleWeaponProjectionValues.Clear;
        BattleWeaponDiceValues dice = weapon.ActiveDice;
        return new BattleWindupWeaponSignature(
            weapon.ProfileKind,
            weapon.ItemId,
            weapon.InstanceId,
            weapon.ProfileTypeId,
            weapon.RangeType,
            weapon.Family,
            weapon.CurrentGrip,
            weapon.AttackRange,
            dice.DiceCount,
            dice.DiceSides,
            dice.FlatBonus,
            weapon.UsesTwoHands,
            weapon.IsHeavy,
            weapon.PhysicalDamageTag
        );
    }

    internal bool Matches(BattleUnitState unitState) =>
        this == Capture(unitState);
}
