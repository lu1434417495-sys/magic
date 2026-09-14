#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal sealed record EnemyTemplateProjectionInput(
    int BodySize,
    int CreatureLevel,
    int HitDieSides,
    IReadOnlyList<StringName> Tags,
    StringName AttackEquipmentItemId,
    StringName NaturalWeaponDamageTag,
    int NaturalWeaponAttackRange,
    IReadOnlyDictionary<StringName, int> BaseAttributeOverrides
);

internal sealed record EnemyTemplateProjectionFacts(
    EnemyTemplateDefinition.EnemyWeaponProjectionDefinition Weapon,
    int DerivedHpMax,
    int DerivedAttackBonus
);

internal static class EnemyTemplateProjectionRules
{
    private static readonly StringName TagBeast = "beast";
    private static readonly StringName NaturalWeaponProfileTypeId = "natural_weapon";
    private static readonly StringName NaturalWeaponDefaultDamageTag = "physical_blunt";
    private static readonly StringName AbilityStrength = "strength";
    private static readonly StringName AbilityConstitution = "constitution";
    private static readonly StringName AbilityPerception = "perception";

    internal static EnemyTemplateProjectionFacts Project(
        EnemyTemplateProjectionInput source,
        IReadOnlyDictionary<StringName, ItemDefinition>? itemDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        EnemyTemplateDefinition.EnemyWeaponProjectionDefinition weapon = ProjectWeapon(
            source,
            itemDefinitions
        );
        return new EnemyTemplateProjectionFacts(
            weapon,
            DeriveHpMax(source),
            DeriveAttackBonus(source, weapon)
        );
    }

    internal static EnemyTemplateDefinition.EnemyWeaponProjectionDefinition ProjectWeapon(
        EnemyTemplateProjectionInput source,
        IReadOnlyDictionary<StringName, ItemDefinition>? itemDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (HasTag(source.Tags, TagBeast))
            return ProjectNaturalWeapon(source);

        EnemyTemplateDefinition.EnemyWeaponProjectionDefinition equipmentWeapon =
            ProjectAttackEquipmentWeapon(source, itemDefinitions);
        return !equipmentWeapon.IsEmpty() ? equipmentWeapon : ProjectUnarmedWeapon();
    }

    internal static EnemyTemplateDefinition.EnemyWeaponProjectionDefinition ProjectAttackEquipmentWeapon(
        EnemyTemplateProjectionInput source,
        IReadOnlyDictionary<StringName, ItemDefinition>? itemDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        StringName itemId = source.AttackEquipmentItemId ?? "";
        if (
            itemId == ""
            || itemDefinitions == null
            || !itemDefinitions.TryGetValue(itemId, out ItemDefinition? itemDefinition)
            || itemDefinition == null
            || !itemDefinition.IsWeapon()
            || itemDefinition.WeaponProfile == null
        )
        {
            return EmptyWeapon();
        }

        WeaponProfileDefinition profile = itemDefinition.WeaponProfile;
        EnemyTemplateDefinition.EnemyWeaponDiceDefinition oneHandedDice = ProjectDice(
            profile.OneHandedDice
        );
        EnemyTemplateDefinition.EnemyWeaponDiceDefinition twoHandedDice = ProjectDice(
            profile.TwoHandedDice
        );
        HashSet<StringName> properties = NormalizeProperties(profile.GetPropertiesTyped());
        bool isVersatile = properties.Contains("versatile");
        bool isHeavy = properties.Contains("heavy");
        bool usesTwoHands = ResolveWeaponUsesTwoHands(
            itemDefinition,
            oneHandedDice,
            twoHandedDice,
            isVersatile
        );

        return new EnemyTemplateDefinition.EnemyWeaponProjectionDefinition(
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            itemDefinition.ItemId,
            "",
            ProgressionDataUtils.to_string_name(profile.WeaponTypeId),
            itemDefinition.GetWeaponRangeType(),
            "",
            ResolveWeaponCurrentGrip(oneHandedDice, twoHandedDice, usesTwoHands),
            Math.Max(profile.AttackRange, 0),
            oneHandedDice,
            twoHandedDice,
            isVersatile,
            usesTwoHands,
            isHeavy,
            itemDefinition.GetWeaponPhysicalDamageTag()
        );
    }

    internal static EnemyTemplateDefinition.EnemyWeaponProjectionDefinition ProjectNaturalWeapon(
        EnemyTemplateProjectionInput source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!HasTag(source.Tags, TagBeast))
            return EmptyWeapon();

        return new EnemyTemplateDefinition.EnemyWeaponProjectionDefinition(
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Natural),
            "",
            "",
            NaturalWeaponProfileTypeId,
            BattleWeaponRangeTypeNames.ToStringName(BattleWeaponRangeTypeKind.Melee),
            "",
            BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
            Math.Max(source.NaturalWeaponAttackRange, 1),
            new EnemyTemplateDefinition.EnemyWeaponDiceDefinition(1, 6, 0),
            EmptyDice(),
            false,
            false,
            false,
            ResolveNaturalWeaponDamageTag(source)
        );
    }

    internal static EnemyTemplateDefinition.EnemyWeaponProjectionDefinition ProjectUnarmedWeapon() =>
        new(
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed),
            "",
            "",
            "unarmed",
            BattleWeaponRangeTypeNames.ToStringName(BattleWeaponRangeTypeKind.Melee),
            "",
            BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
            1,
            new EnemyTemplateDefinition.EnemyWeaponDiceDefinition(1, 4, 0),
            EmptyDice(),
            false,
            false,
            false,
            "physical_blunt"
        );

    internal static int ResolveFootprintCellCount(int bodySize)
    {
        Vector2I footprint = BattleUnitState.GetFootprintSizeForBodySize(
            Math.Max(bodySize, 1)
        );
        return Math.Max(footprint.X * footprint.Y, 1);
    }

    internal static int DeriveHpMax(EnemyTemplateProjectionInput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int level = Math.Max(source.CreatureLevel, 0);
        int sides = Math.Max(source.HitDieSides, 1);
        int constitution = ReadAttribute(source.BaseAttributeOverrides, AbilityConstitution);
        int constitutionModifier = AttributeSnapshot.CalculateScoreModifier(constitution);
        int firstLevelHalfPoints = Math.Max(2, (sides + constitutionModifier * 2) * 2);
        int perLevelHalfPoints = Math.Max(2, (sides + 1) + constitutionModifier * 4);
        int totalHalfPoints = firstLevelHalfPoints + perLevelHalfPoints * Math.Max(level - 1, 0);
        return (totalHalfPoints / 2) * ResolveFootprintCellCount(source.BodySize);
    }

    internal static int DeriveAttackBonus(
        EnemyTemplateProjectionInput source,
        EnemyTemplateDefinition.EnemyWeaponProjectionDefinition weapon
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        int attackRange = weapon != null && !weapon.IsEmpty() ? weapon.WeaponAttackRange : 1;
        StringName abilityId = attackRange > 2 ? AbilityPerception : AbilityStrength;
        return AttributeSnapshot.CalculateScoreModifier(
            ReadAttribute(source.BaseAttributeOverrides, abilityId)
        );
    }

    private static int ReadAttribute(
        IReadOnlyDictionary<StringName, int>? attributes,
        StringName attributeId
    ) =>
        attributes != null && attributes.TryGetValue(attributeId, out int value) ? value : 0;

    private static bool HasTag(IReadOnlyList<StringName>? tags, StringName expected)
    {
        if (tags == null || expected == "")
            return false;
        foreach (StringName tag in tags)
        {
            if (tag == expected)
                return true;
        }
        return false;
    }

    private static StringName ResolveNaturalWeaponDamageTag(
        EnemyTemplateProjectionInput source
    )
    {
        StringName explicitTag = source.NaturalWeaponDamageTag ?? "";
        if (explicitTag != "")
            return explicitTag;
        foreach (StringName tag in source.Tags ?? Array.Empty<StringName>())
        {
            StringName inferred = NaturalWeaponDamageTagForTemplateTag(tag);
            if (inferred != "")
                return inferred;
        }
        return NaturalWeaponDefaultDamageTag;
    }

    private static StringName NaturalWeaponDamageTagForTemplateTag(StringName tag)
    {
        string normalized = tag?.ToString() ?? "";
        if (normalized is "bite" or "sting" or "horn")
            return "physical_pierce";
        if (normalized is "claw" or "tear")
            return "physical_slash";
        if (normalized is "slam" or "charge" or "trample")
            return "physical_blunt";
        return "";
    }

    private static EnemyTemplateDefinition.EnemyWeaponDiceDefinition ProjectDice(
        WeaponDamageDiceDefinition? source
    ) =>
        source == null
            ? EmptyDice()
            : new EnemyTemplateDefinition.EnemyWeaponDiceDefinition(
                source.GetDiceCount(),
                source.GetDiceSides(),
                source.FlatBonus
            );

    private static EnemyTemplateDefinition.EnemyWeaponDiceDefinition EmptyDice() => new(0, 0, 0);

    private static EnemyTemplateDefinition.EnemyWeaponProjectionDefinition EmptyWeapon() =>
        new(
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            0,
            EmptyDice(),
            EmptyDice(),
            false,
            false,
            false,
            ""
        );

    private static HashSet<StringName> NormalizeProperties(IEnumerable<StringName> values)
    {
        var result = new HashSet<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static bool ResolveWeaponUsesTwoHands(
        ItemDefinition itemDefinition,
        EnemyTemplateDefinition.EnemyWeaponDiceDefinition oneHandedDice,
        EnemyTemplateDefinition.EnemyWeaponDiceDefinition twoHandedDice,
        bool isVersatile
    )
    {
        if (itemDefinition.GetFinalOccupiedSlotIdsTyped("main_hand").Contains("off_hand"))
            return true;
        if (oneHandedDice.IsEmpty() && !twoHandedDice.IsEmpty())
            return true;
        return isVersatile && !twoHandedDice.IsEmpty();
    }

    private static StringName ResolveWeaponCurrentGrip(
        EnemyTemplateDefinition.EnemyWeaponDiceDefinition oneHandedDice,
        EnemyTemplateDefinition.EnemyWeaponDiceDefinition twoHandedDice,
        bool usesTwoHands
    )
    {
        if (usesTwoHands)
            return BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded);
        if (!oneHandedDice.IsEmpty())
            return BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded);
        if (!twoHandedDice.IsEmpty())
            return BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded);
        return BattleUnitState.ToStringName(BattleWeaponGripKind.None);
    }
}
