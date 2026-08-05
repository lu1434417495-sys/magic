using System;
using System.Collections.Generic;
using Godot;

internal sealed class EnemyTemplateDefinition
{
    internal sealed record EnemyWeaponDiceDefinition(int DiceCount, int DiceSides, int FlatBonus)
    {
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
        {
            source ??= new WeaponProjection();
            WeaponProfileKind = source.weapon_profile_kind;
            WeaponItemId = source.weapon_item_id;
            WeaponInstanceId = source.weapon_instance_id;
            WeaponProfileTypeId = source.weapon_profile_type_id;
            WeaponRangeType = source.weapon_range_type;
            WeaponFamily = source.weapon_family;
            WeaponCurrentGrip = source.weapon_current_grip;
            WeaponAttackRange = source.weapon_attack_range;
            WeaponOneHandedDice = CopyDice(source.weapon_one_handed_dice);
            WeaponTwoHandedDice = CopyDice(source.weapon_two_handed_dice);
            WeaponIsVersatile = source.weapon_is_versatile;
            WeaponUsesTwoHands = source.weapon_uses_two_hands;
            WeaponIsHeavy = source.weapon_is_heavy;
            WeaponPhysicalDamageTag = source.weapon_physical_damage_tag;
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

    private EnemyTemplateDefinition(
        StringName templateId,
        string displayName,
        string battleSpriteTexturePath,
        long battleSpriteTextureUid,
        StringName brainId,
        StringName initialStateId,
        int enemyCount,
        int bodySize,
        int creatureLevel,
        int hitDieSides,
        int actionThreshold,
        BattleCognitionKind cognitionKind,
        IReadOnlyList<StringName> tags,
        IReadOnlyList<StringName> saveAdvantageTags,
        IReadOnlyList<StringName> saveDisadvantageTags,
        IReadOnlyList<StringName> saveImmunityTags,
        IReadOnlyDictionary<StringName, StringName> damageResistances,
        StringName attackEquipmentItemId,
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
        BattleSpriteTexturePath = battleSpriteTexturePath ?? "";
        BattleSpriteTextureUid = battleSpriteTextureUid;
        BrainId = brainId;
        InitialStateId = initialStateId;
        EnemyCount = enemyCount;
        BodySize = bodySize;
        CreatureLevel = creatureLevel;
        HitDieSides = hitDieSides;
        ActionThreshold = actionThreshold;
        CognitionKind = cognitionKind;
        Tags = EnemyDefinitionCollections.FreezeList(tags);
        SaveAdvantageTags = EnemyDefinitionCollections.FreezeList(saveAdvantageTags);
        SaveDisadvantageTags = EnemyDefinitionCollections.FreezeList(saveDisadvantageTags);
        SaveImmunityTags = EnemyDefinitionCollections.FreezeList(saveImmunityTags);
        DamageResistances = EnemyDefinitionCollections.FreezeDictionary(damageResistances);
        AttackEquipmentItemId = attackEquipmentItemId;
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
    internal string BattleSpriteTexturePath { get; }
    internal long BattleSpriteTextureUid { get; }
    internal StringName BrainId { get; }
    internal StringName InitialStateId { get; }
    internal int EnemyCount { get; }
    internal int BodySize { get; }
    internal int CreatureLevel { get; }
    internal int HitDieSides { get; }
    internal int ActionThreshold { get; }
    internal BattleCognitionKind CognitionKind { get; }
    internal IReadOnlyList<StringName> Tags { get; }
    internal IReadOnlyList<StringName> SaveAdvantageTags { get; }
    internal IReadOnlyList<StringName> SaveDisadvantageTags { get; }
    internal IReadOnlyList<StringName> SaveImmunityTags { get; }
    internal IReadOnlyDictionary<StringName, StringName> DamageResistances { get; }
    internal StringName AttackEquipmentItemId { get; }
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

    internal static EnemyTemplateDefinition FromResource(
        EnemyTemplateDef source,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        string texturePath = source.battle_sprite_texture?.ResourcePath ?? "";
        if (!string.IsNullOrWhiteSpace(texturePath))
            texturePath = ContentPathCanonicalizer.Canonicalize(texturePath);
        var skillLevels = new Dictionary<StringName, int>();
        if (source.skill_level_map != null)
        {
            foreach (Variant rawKey in source.skill_level_map.Keys)
            {
                if (rawKey.VariantType != Variant.Type.StringName)
                    continue;
                Variant rawValue = source.skill_level_map[rawKey];
                if (rawValue.VariantType == Variant.Type.Int)
                    skillLevels[rawKey.AsStringName()] = rawValue.AsInt32();
            }
        }
        var drops = new List<DropEntryDefinition>();
        foreach (DropEntryDef drop in source.drop_entries)
        {
            if (drop != null)
                drops.Add(drop.ToDefinition());
        }
        WeaponProjection weapon = source.GetWeaponProjectionTyped(itemDefinitions);
        return new EnemyTemplateDefinition(
            source.template_id,
            source.display_name,
            texturePath,
            EnemyDefinitionCollections.ResolveResourceUid(texturePath),
            source.brain_id,
            source.initial_state_id,
            source.enemy_count,
            source.body_size,
            source.creature_level,
            source.hit_die_sides,
            source.action_threshold,
            BattleCognitionContentRules.ToKind(
                source.cognition_kind
            ),
            source.tags,
            source.save_advantage_tags,
            source.save_disadvantage_tags,
            source.save_immunity_tags,
            source.GetDamageResistancesTyped(),
            source.attack_equipment_item_id,
            source.natural_weapon_damage_tag,
            source.natural_weapon_attack_range,
            source.GetBaseAttributeOverridesResolvedTyped(),
            source.skill_ids,
            skillLevels,
            source.generated_core_skill_count,
            source.GetAttributeOverridesTyped(),
            source.target_rank,
            drops,
            new EnemyWeaponProjectionDefinition(weapon),
            source.GetDerivedHpMaxTyped(),
            source.GetDerivedAttackBonusTyped(itemDefinitions)
        );
    }
}
