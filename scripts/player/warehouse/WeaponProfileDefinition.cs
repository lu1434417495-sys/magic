using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class WeaponProfileDefinition
{
    private const int AttackRangeInherit = -1;

    public enum PropertyMergeMode
    {
        INHERIT = 0,
        REPLACE = 1,
        ADD = 2,
        REMOVE = 3,
    }

    public WeaponProfileDefinition(
        StringName weaponTypeId,
        StringName trainingGroup,
        StringName rangeType,
        StringName family,
        StringName damageTag,
        int attackRange,
        WeaponDamageDiceDefinition oneHandedDice,
        WeaponDamageDiceDefinition twoHandedDice,
        int propertiesMode,
        IReadOnlyList<StringName> properties
    )
    {
        WeaponTypeId = weaponTypeId;
        TrainingGroup = trainingGroup;
        RangeType = rangeType;
        Family = family;
        DamageTag = damageTag;
        AttackRange = attackRange;
        OneHandedDice = WeaponDamageDiceDefinition.CopyOf(oneHandedDice);
        TwoHandedDice = WeaponDamageDiceDefinition.CopyOf(twoHandedDice);
        PropertiesMode = propertiesMode;
        Properties = FreezeProperties(properties);
    }

    public StringName WeaponTypeId { get; }
    public StringName TrainingGroup { get; }
    public StringName RangeType { get; }
    public StringName Family { get; }
    public StringName DamageTag { get; }
    public int AttackRange { get; }
    public WeaponDamageDiceDefinition OneHandedDice { get; }
    public WeaponDamageDiceDefinition TwoHandedDice { get; }
    public int PropertiesMode { get; }
    public IReadOnlyList<StringName> Properties { get; }

    public bool HasAttackRangeOverride() => AttackRange != AttackRangeInherit;

    public List<StringName> GetPropertiesTyped() => new(NormalizeProperties(Properties));

    public static WeaponProfileDefinition Merge(
        WeaponProfileDefinition templateProfile,
        WeaponProfileDefinition instanceProfile
    )
    {
        if (templateProfile == null && instanceProfile == null)
            return null;
        if (templateProfile == null)
            return CopyAsResolved(instanceProfile);
        if (instanceProfile == null)
            return CopyAsResolved(templateProfile);

        return new WeaponProfileDefinition(
            InheritStringName(templateProfile.WeaponTypeId, instanceProfile.WeaponTypeId),
            InheritStringName(templateProfile.TrainingGroup, instanceProfile.TrainingGroup),
            InheritStringName(templateProfile.RangeType, instanceProfile.RangeType),
            InheritStringName(templateProfile.Family, instanceProfile.Family),
            InheritStringName(templateProfile.DamageTag, instanceProfile.DamageTag),
            instanceProfile.HasAttackRangeOverride()
                ? instanceProfile.AttackRange
                : templateProfile.AttackRange,
            InheritDice(templateProfile.OneHandedDice, instanceProfile.OneHandedDice),
            InheritDice(templateProfile.TwoHandedDice, instanceProfile.TwoHandedDice),
            (int)PropertyMergeMode.REPLACE,
            ResolveProperties(
                templateProfile.Properties,
                instanceProfile.Properties,
                instanceProfile.PropertiesMode
            )
        );
    }

    public static int NormalizePropertiesMode(int mode) =>
        IsValidPropertiesMode(mode) ? mode : (int)PropertyMergeMode.INHERIT;

    public static bool IsValidPropertiesMode(int mode) =>
        mode >= (int)PropertyMergeMode.INHERIT
        && mode <= (int)PropertyMergeMode.REMOVE;

    internal static WeaponProfileDefinition FromResource(WeaponProfileDef source)
    {
        if (source == null)
            return null;

        return new WeaponProfileDefinition(
            source.weapon_type_id,
            source.training_group,
            source.range_type,
            source.family,
            source.damage_tag,
            source.attack_range,
            WeaponDamageDiceDefinition.FromResource(source.OneHandedDiceProjectionBorrowed),
            WeaponDamageDiceDefinition.FromResource(source.TwoHandedDiceProjectionBorrowed),
            source.properties_mode,
            new List<StringName>(
                WarehouseDefinitionProjection.RequireCollection(
                    source.PropertiesProjectionBorrowed,
                    "weapon_profile.properties"
                )
            )
        );
    }

    internal static WeaponProfileDefinition CopyOf(WeaponProfileDefinition source)
    {
        if (source == null)
            return null;
        return new WeaponProfileDefinition(
            source.WeaponTypeId,
            source.TrainingGroup,
            source.RangeType,
            source.Family,
            source.DamageTag,
            source.AttackRange,
            source.OneHandedDice,
            source.TwoHandedDice,
            source.PropertiesMode,
            source.Properties
        );
    }

    private static WeaponProfileDefinition CopyAsResolved(WeaponProfileDefinition source) =>
        new(
            source.WeaponTypeId,
            source.TrainingGroup,
            source.RangeType,
            source.Family,
            source.DamageTag,
            source.AttackRange,
            CopyResolvedDice(source.OneHandedDice),
            CopyResolvedDice(source.TwoHandedDice),
            (int)PropertyMergeMode.REPLACE,
            NormalizeProperties(source.Properties)
        );

    private static StringName InheritStringName(StringName templateValue, StringName instanceValue) =>
        instanceValue != "" ? instanceValue : templateValue;

    private static WeaponDamageDiceDefinition InheritDice(
        WeaponDamageDiceDefinition templateDice,
        WeaponDamageDiceDefinition instanceDice
    ) => CopyResolvedDice(instanceDice ?? templateDice);

    private static WeaponDamageDiceDefinition CopyResolvedDice(
        WeaponDamageDiceDefinition source
    ) =>
        source == null
            ? null
            : new WeaponDamageDiceDefinition(
                source.DiceCount,
                source.DiceSides,
                source.FlatBonus
            );

    private static IReadOnlyList<StringName> ResolveProperties(
        IReadOnlyList<StringName> templateProperties,
        IReadOnlyList<StringName> instanceProperties,
        int mode
    )
    {
        return NormalizePropertiesMode(mode) switch
        {
            (int)PropertyMergeMode.REPLACE => NormalizeProperties(instanceProperties),
            (int)PropertyMergeMode.ADD => AddProperties(templateProperties, instanceProperties),
            (int)PropertyMergeMode.REMOVE => RemoveProperties(
                templateProperties,
                instanceProperties
            ),
            _ => NormalizeProperties(templateProperties),
        };
    }

    private static IReadOnlyList<StringName> AddProperties(
        IReadOnlyList<StringName> templateProperties,
        IReadOnlyList<StringName> instanceProperties
    )
    {
        var result = new List<StringName>(NormalizeProperties(templateProperties));
        var seen = new HashSet<StringName>(result);
        foreach (StringName rawValue in instanceProperties)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
            if (normalized != "" && seen.Add(normalized))
                result.Add(normalized);
        }
        return result;
    }

    private static IReadOnlyList<StringName> RemoveProperties(
        IReadOnlyList<StringName> templateProperties,
        IReadOnlyList<StringName> instanceProperties
    )
    {
        var removeSet = new HashSet<StringName>();
        foreach (StringName rawValue in instanceProperties)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
            if (normalized != "")
                removeSet.Add(normalized);
        }

        var result = new List<StringName>();
        foreach (StringName rawValue in templateProperties)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
            if (normalized != "" && !removeSet.Contains(normalized))
                result.Add(normalized);
        }
        return result;
    }

    private static IReadOnlyList<StringName> NormalizeProperties(
        IReadOnlyList<StringName> properties
    )
    {
        var result = new List<StringName>();
        var seen = new HashSet<StringName>();
        foreach (StringName rawValue in properties)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
            if (normalized != "" && seen.Add(normalized))
                result.Add(normalized);
        }
        return result;
    }

    private static IReadOnlyList<StringName> FreezeProperties(
        IReadOnlyList<StringName> properties
    )
    {
        ArgumentNullException.ThrowIfNull(properties);
        return new ReadOnlyCollection<StringName>(new List<StringName>(properties));
    }
}
