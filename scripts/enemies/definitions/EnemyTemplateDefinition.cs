using System;
using System.Collections.Generic;
using Godot;

internal sealed class EnemyTemplateDefinition
{
    internal sealed record EnemyWeaponDiceDefinition(int DiceCount, int DiceSides, int FlatBonus)
    {
        internal bool IsEmpty() => DiceCount <= 0 || DiceSides <= 0;

        internal WeaponDice ToRuntimeDice() => new()
        {
            dice_count = DiceCount,
            dice_sides = DiceSides,
            flat_bonus = FlatBonus,
        };
    }

    internal sealed class EnemyWeaponProjectionDefinition
    {
        internal EnemyWeaponProjectionDefinition(WeaponProjection source)
            : this(
                source?.weapon_profile_kind ?? "",
                source?.weapon_item_id ?? "",
                source?.weapon_instance_id ?? "",
                source?.weapon_profile_type_id ?? "",
                source?.weapon_range_type ?? "",
                source?.weapon_family ?? "",
                source?.weapon_current_grip ?? "",
                source?.weapon_attack_range ?? 0,
                CopyDice(source?.weapon_one_handed_dice),
                CopyDice(source?.weapon_two_handed_dice),
                source?.weapon_is_versatile ?? false,
                source?.weapon_uses_two_hands ?? false,
                source?.weapon_is_heavy ?? false,
                source?.weapon_physical_damage_tag ?? ""
            ) { }

        internal EnemyWeaponProjectionDefinition(
            StringName weaponProfileKind,
            StringName weaponItemId,
            StringName weaponInstanceId,
            StringName weaponProfileTypeId,
            StringName weaponRangeType,
            StringName weaponFamily,
            StringName weaponCurrentGrip,
            int weaponAttackRange,
            EnemyWeaponDiceDefinition weaponOneHandedDice,
            EnemyWeaponDiceDefinition weaponTwoHandedDice,
            bool weaponIsVersatile,
            bool weaponUsesTwoHands,
            bool weaponIsHeavy,
            StringName weaponPhysicalDamageTag
        )
        {
            WeaponProfileKind = weaponProfileKind ?? "";
            WeaponItemId = weaponItemId ?? "";
            WeaponInstanceId = weaponInstanceId ?? "";
            WeaponProfileTypeId = weaponProfileTypeId ?? "";
            WeaponRangeType = weaponRangeType ?? "";
            WeaponFamily = weaponFamily ?? "";
            WeaponCurrentGrip = weaponCurrentGrip ?? "";
            WeaponAttackRange = weaponAttackRange;
            WeaponOneHandedDice = weaponOneHandedDice ?? new EnemyWeaponDiceDefinition(0, 0, 0);
            WeaponTwoHandedDice = weaponTwoHandedDice ?? new EnemyWeaponDiceDefinition(0, 0, 0);
            WeaponIsVersatile = weaponIsVersatile;
            WeaponUsesTwoHands = weaponUsesTwoHands;
            WeaponIsHeavy = weaponIsHeavy;
            WeaponPhysicalDamageTag = weaponPhysicalDamageTag ?? "";
        }

        internal StringName WeaponProfileKind { get; }
        internal StringName WeaponItemId { get; }
        internal StringName WeaponInstanceId { get; }
        internal StringName WeaponProfileTypeId { get; }
        internal StringName WeaponRangeType { get; }
        internal StringName WeaponFamily { get; }
        internal StringName WeaponCurrentGrip { get; }
        internal int WeaponAttackRange { get; }
        internal EnemyWeaponDiceDefinition WeaponOneHandedDice { get; }
        internal EnemyWeaponDiceDefinition WeaponTwoHandedDice { get; }
        internal bool WeaponIsVersatile { get; }
        internal bool WeaponUsesTwoHands { get; }
        internal bool WeaponIsHeavy { get; }
        internal StringName WeaponPhysicalDamageTag { get; }

        internal bool IsEmpty() => WeaponProfileKind == "";

        internal WeaponProjection ToRuntimeProjection() => new()
        {
            weapon_profile_kind = WeaponProfileKind,
            weapon_item_id = WeaponItemId,
            weapon_instance_id = WeaponInstanceId,
            weapon_profile_type_id = WeaponProfileTypeId,
            weapon_range_type = WeaponRangeType,
            weapon_family = WeaponFamily,
            weapon_current_grip = WeaponCurrentGrip,
            weapon_attack_range = WeaponAttackRange,
            weapon_one_handed_dice = WeaponOneHandedDice?.ToRuntimeDice() ?? new WeaponDice(),
            weapon_two_handed_dice = WeaponTwoHandedDice?.ToRuntimeDice() ?? new WeaponDice(),
            weapon_is_versatile = WeaponIsVersatile,
            weapon_uses_two_hands = WeaponUsesTwoHands,
            weapon_is_heavy = WeaponIsHeavy,
            weapon_physical_damage_tag = WeaponPhysicalDamageTag,
        };

        private static EnemyWeaponDiceDefinition CopyDice(WeaponDice source) =>
            source == null
                ? null
                : new EnemyWeaponDiceDefinition(
                    source.dice_count,
                    source.dice_sides,
                    source.flat_bonus
                );
    }

    internal EnemyTemplateDefinition(
        StringName templateId,
        string displayName,
        StringName battleSpriteAssetId,
        StringName brainId,
        StringName initialStateId,
        int enemyCount,
        int bodySize,
        int creatureLevel,
        int hitDieSides,
        BattleCognitionKind cognitionKind,
        IReadOnlyList<StringName> tags,
        IReadOnlyList<StringName> saveAdvantageTags,
        IReadOnlyList<StringName> saveDisadvantageTags,
        IReadOnlyList<StringName> saveImmunityTags,
        IReadOnlyDictionary<StringName, StringName> damageResistances,
        StringName attackEquipmentItemId,
        IReadOnlyList<EnemyBattleEquipmentDefinition> battleEquipmentEntries,
        StringName naturalWeaponDamageTag,
        int naturalWeaponAttackRange,
        IReadOnlyDictionary<StringName, int> baseAttributeOverrides,
        IReadOnlyList<StringName> skillIds,
        IReadOnlyDictionary<StringName, int> skillLevels,
        int generatedCoreSkillCount,
        IReadOnlyDictionary<StringName, int> attributeOverrides,
        StringName targetRank,
        IReadOnlyList<DropEntryDefinition> dropEntries,
        EnemyWeaponProjectionDefinition weapon,
        int derivedHpMax,
        int derivedAttackBonus
    )
    {
        TemplateId = templateId;
        DisplayName = displayName ?? "";
        BattleSpriteAssetId = battleSpriteAssetId;
        BrainId = brainId;
        InitialStateId = initialStateId;
        EnemyCount = enemyCount;
        BodySize = bodySize;
        CreatureLevel = creatureLevel;
        HitDieSides = hitDieSides;
        CognitionKind = cognitionKind;
        Tags = EnemyDefinitionCollections.FreezeList(tags);
        SaveAdvantageTags = EnemyDefinitionCollections.FreezeList(saveAdvantageTags);
        SaveDisadvantageTags = EnemyDefinitionCollections.FreezeList(saveDisadvantageTags);
        SaveImmunityTags = EnemyDefinitionCollections.FreezeList(saveImmunityTags);
        DamageResistances = EnemyDefinitionCollections.FreezeDictionary(damageResistances);
        AttackEquipmentItemId = attackEquipmentItemId;
        BattleEquipmentEntries = EnemyDefinitionCollections.FreezeList(battleEquipmentEntries);
        NaturalWeaponDamageTag = naturalWeaponDamageTag;
        NaturalWeaponAttackRange = naturalWeaponAttackRange;
        BaseAttributeOverrides = EnemyDefinitionCollections.FreezeDictionary(baseAttributeOverrides);
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        SkillLevels = EnemyDefinitionCollections.FreezeDictionary(skillLevels);
        GeneratedCoreSkillCount = Math.Max(generatedCoreSkillCount, 0);
        AttributeOverrides = EnemyDefinitionCollections.FreezeDictionary(attributeOverrides);
        TargetRank = targetRank;
        DropEntries = EnemyDefinitionCollections.FreezeList(dropEntries);
        Weapon = weapon ?? new EnemyWeaponProjectionDefinition(new WeaponProjection());
        DerivedHpMax = derivedHpMax;
        DerivedAttackBonus = derivedAttackBonus;
    }

    internal StringName TemplateId { get; }
    internal string DisplayName { get; }
    internal StringName BattleSpriteAssetId { get; }
    internal StringName BrainId { get; }
    internal StringName InitialStateId { get; }
    internal int EnemyCount { get; }
    internal int BodySize { get; }
    internal int CreatureLevel { get; }
    internal int HitDieSides { get; }
    internal BattleCognitionKind CognitionKind { get; }
    internal IReadOnlyList<StringName> Tags { get; }
    internal IReadOnlyList<StringName> SaveAdvantageTags { get; }
    internal IReadOnlyList<StringName> SaveDisadvantageTags { get; }
    internal IReadOnlyList<StringName> SaveImmunityTags { get; }
    internal IReadOnlyDictionary<StringName, StringName> DamageResistances { get; }
    internal StringName AttackEquipmentItemId { get; }
    internal IReadOnlyList<EnemyBattleEquipmentDefinition> BattleEquipmentEntries { get; }
    internal StringName NaturalWeaponDamageTag { get; }
    internal int NaturalWeaponAttackRange { get; }
    internal IReadOnlyDictionary<StringName, int> BaseAttributeOverrides { get; }
    internal IReadOnlyList<StringName> SkillIds { get; }
    internal IReadOnlyDictionary<StringName, int> SkillLevels { get; }
    internal IReadOnlyDictionary<StringName, int> SkillLevelMap => SkillLevels;
    internal int GeneratedCoreSkillCount { get; }
    internal IReadOnlyDictionary<StringName, int> AttributeOverrides { get; }
    internal StringName TargetRank { get; }
    internal EnemyTargetRankKind TargetRankKind => BattleTypedNames.ToEnemyTargetRank(TargetRank);
    internal IReadOnlyList<DropEntryDefinition> DropEntries { get; }
    internal EnemyWeaponProjectionDefinition Weapon { get; }
    internal int DerivedHpMax { get; }
    internal int DerivedAttackBonus { get; }

    internal bool HasTag(StringName tag)
    {
        if (tag == "")
            return false;
        foreach (StringName value in Tags)
        {
            if (value == tag)
                return true;
        }
        return false;
    }

    internal StringName GetInitialStateId(EnemyAiBrainDefinition brain)
    {
        if (InitialStateId != "")
            return InitialStateId;
        return brain != null && brain.HasState(brain.DefaultStateId)
            ? brain.DefaultStateId
            : new StringName("engage");
    }

    internal int GetSkillLevel(StringName skillId, int fallback = 1) =>
        skillId != "" && SkillLevels.TryGetValue(skillId, out int value) ? value : fallback;

    internal int GetSkillLevelTyped(StringName skillId, int fallback = 1) =>
        GetSkillLevel(skillId, fallback);

}
