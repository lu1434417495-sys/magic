using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_unit_state_schema_contract_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestValidRoundtripPreservesCurrentPayload();
        TestClonePreservesEphemeralChargeState();
        TestExtendedBodySizeCategoriesRoundtrip();
        TestRejectsEmptyMissingAndExtraFields();
        TestRejectsWrongTopLevelTypes();
        TestRejectsStringNumericValues();
        TestRejectsBadStringNameArrays();
        TestRejectsBadIdentityProjectionFields();
        TestRejectsBadCombatResourceUnlocks();
        TestRejectsBadStatusEffectEntries();
        TestRejectsEquipmentViewBadPayload();
        TestRejectsBadWeaponDicePayloads();
        TestBodySizeRulesWrapperIsRemoved();

        Finish();
    }

    private void Finish()
    {
        if (_failures.Count == 0)
        {
            GD.Print("Battle unit state schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Battle unit state schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestValidRoundtripPreservesCurrentPayload()
    {
        BattleUnitState unit = BuildUnit();
        AssertTrue(unit != null, "BuildUnit 应返回单位。");
        GDictionary payload = unit.to_dict();
        BattleUnitState restored = BattleUnitState.from_dict(payload);
        AssertTrue(restored != null, "当前 to_dict payload 应可由 from_dict 恢复。");
        AssertEq(restored?.current_move_points ?? -1, 5, "current_move_points 应保留大于默认值的 int。");
        AssertEq(
            restored?.body_size_category.ToString() ?? "",
            "large",
            "body_size_category 应随 body_size round-trip。"
        );
        AssertVariantEq(
            restored?.vision_tags,
            new GStringNameArray { "darkvision" },
            "vision_tags 应 round-trip。"
        );
        AssertEq(
            ReadStringName(restored?.damage_resistances, "fire").ToString(),
            "half",
            "damage_resistances 应 round-trip。"
        );
        AssertVariantEq(
            restored?.to_dict(),
            payload,
            "BattleUnitState 应保持 to_dict/from_dict round-trip。"
        );
    }

    private void TestClonePreservesEphemeralChargeState()
    {
        BattleUnitState unit = BuildMinimalUnit();
        unit.current_move_points = 5;
        unit.per_battle_charges = new GDictionary { [new StringName("dragon_breath")] = 1 };
        unit.per_turn_charges = new GDictionary { [new StringName("nimble_escape")] = 1 };
        unit.per_turn_charge_limits = new GDictionary { [new StringName("nimble_escape")] = 1 };

        BattleUnitState cloned = unit.clone();
        AssertTrue(cloned != null, "BattleUnitState.clone() 应返回可用副本。");
        if (cloned == null)
            return;

        AssertVariantEq(cloned.to_dict(), unit.to_dict(), "clone 应保留序列化字段。");
        AssertEq(DictInt(cloned.per_battle_charges, "dragon_breath", -1), 1, "clone 应深拷贝 per_battle_charges。");
        AssertEq(DictInt(cloned.per_turn_charges, "nimble_escape", -1), 1, "clone 应深拷贝 per_turn_charges。");
        AssertEq(DictInt(cloned.per_turn_charge_limits, "nimble_escape", -1), 1, "clone 应深拷贝 per_turn_charge_limits。");

        cloned.per_battle_charges["dragon_breath"] = 0;
        cloned.per_turn_charges["nimble_escape"] = 0;
        cloned.per_turn_charge_limits["nimble_escape"] = 0;
        AssertEq(DictInt(unit.per_battle_charges, "dragon_breath", -1), 1, "clone 不应共享 per_battle_charges 字典。");
        AssertEq(DictInt(unit.per_turn_charges, "nimble_escape", -1), 1, "clone 不应共享 per_turn_charges 字典。");
        AssertEq(DictInt(unit.per_turn_charge_limits, "nimble_escape", -1), 1, "clone 不应共享 per_turn_charge_limits 字典。");
    }

    private void TestExtendedBodySizeCategoriesRoundtrip()
    {
        BattleUnitState tiny = BuildMinimalUnit();
        AssertTrue(tiny != null, "body size fixture 应可构建。");
        AssertTrue(tiny.set_body_size_category("tiny"), "tiny category 应可设置。");
        GDictionary tinyPayload = tiny.to_dict();
        AssertEq(DictString(tinyPayload, "body_size_category"), "tiny", "to_dict 应保留 tiny category。");
        AssertEq(DictInt(tinyPayload, "body_size"), BodySizeContentRules.BODY_SIZE_TINY, "tiny 应映射到 typed body-size int。");
        AssertEq(DictVector2I(tinyPayload, "footprint_size"), Vector2I.One, "tiny footprint 应为 1x1。");
        AssertTrue(BattleUnitState.from_dict(tinyPayload) != null, "tiny payload 应可 round-trip。");

        BattleUnitState gargantuan = BuildMinimalUnit();
        AssertTrue(
            gargantuan.set_body_size_category(BodySizeContentRules.BODY_SIZE_CATEGORY_GARGANTUAN),
            "gargantuan category 应可设置。"
        );
        GDictionary gargantuanPayload = gargantuan.to_dict();
        AssertEq(
            DictInt(gargantuanPayload, "body_size"),
            BodySizeContentRules.BODY_SIZE_GARGANTUAN,
            "gargantuan 应映射到 typed body-size int。"
        );
        AssertEq(
            DictVector2I(gargantuanPayload, "footprint_size"),
            new Vector2I(4, 4),
            "gargantuan footprint 应为 4x4。"
        );
        AssertEq(
            DictArray(gargantuanPayload, "occupied_coords").Count,
            16,
            "gargantuan 应占 16 格。"
        );
        AssertTrue(BattleUnitState.from_dict(gargantuanPayload) != null, "gargantuan payload 应可 round-trip。");

        BattleUnitState boss = BuildMinimalUnit();
        AssertTrue(boss.set_body_size_category(BodySizeContentRules.BODY_SIZE_CATEGORY_BOSS), "boss category 应可设置。");
        GDictionary bossPayload = boss.to_dict();
        AssertEq(DictInt(bossPayload, "body_size"), BodySizeContentRules.BODY_SIZE_BOSS, "boss 应映射到 typed body-size int。");
        AssertEq(DictVector2I(bossPayload, "footprint_size"), new Vector2I(5, 5), "boss footprint 应为 5x5。");
        AssertTrue(BattleUnitState.from_dict(bossPayload) != null, "boss payload 应可 round-trip。");
    }

    private void TestRejectsEmptyMissingAndExtraFields()
    {
        AssertTrue(BattleUnitState.from_dict(new GDictionary()) == null, "空 Dictionary payload 应拒绝。");

        GDictionary missing = Payload();
        missing.Remove("footprint_size");
        AssertRejected(missing, "缺少当前 to_dict 字段应拒绝。");

        GDictionary extra = Payload();
        extra["legacy_body_size"] = 1;
        AssertRejected(extra, "包含额外旧字段应拒绝。");
    }

    private void TestRejectsWrongTopLevelTypes()
    {
        GDictionary badCoord = Payload();
        badCoord["coord"] = "0,0";
        AssertRejected(badCoord, "coord 非 Vector2i 应拒绝。");

        GDictionary badFootprint = Payload();
        badFootprint["footprint_size"] = Vector2I.One;
        AssertRejected(badFootprint, "footprint_size 与 body_size 刷新结果不一致应拒绝。");

        GDictionary badOccupied = Payload();
        badOccupied["occupied_coords"] = new GArray { new Vector2I(9, 9) };
        AssertRejected(badOccupied, "occupied_coords 与 coord/body_size 刷新结果不一致应拒绝。");

        GDictionary badBool = Payload();
        badBool["is_alive"] = "true";
        AssertRejected(badBool, "bool 字段使用字符串应拒绝。");

        GDictionary badRequiredId = Payload();
        badRequiredId["unit_id"] = "";
        AssertRejected(badRequiredId, "必填 String/StringName 为空应拒绝。");

        GDictionary badWeaponFamily = Payload();
        badWeaponFamily["weapon_family"] = 7;
        AssertRejected(badWeaponFamily, "weapon_family 非 String/StringName 应拒绝。");
    }

    private void TestRejectsStringNumericValues()
    {
        foreach (string fieldName in new[] { "current_hp", "current_ap", "aura_max", "weapon_attack_range", "last_turn_tu" })
        {
            GDictionary payload = Payload();
            payload[fieldName] = "7";
            AssertRejected(payload, $"{fieldName} 使用字符串数字应拒绝。");
        }

        GDictionary badMovePoints = Payload();
        badMovePoints["current_move_points"] = -1;
        AssertRejected(badMovePoints, "current_move_points 负数应拒绝。");

        GDictionary badAttribute = Payload();
        DictDictionary(badAttribute, "attribute_snapshot")["strength"] = "3";
        AssertRejected(badAttribute, "attribute_snapshot value 非 int 应拒绝。");

        GDictionary badSkillLevel = Payload();
        DictDictionary(badSkillLevel, "known_skill_level_map")["slash"] = "2";
        AssertRejected(badSkillLevel, "known_skill_level_map value 非 int 应拒绝。");
    }

    private void TestRejectsBadStringNameArrays()
    {
        GDictionary emptySkillId = Payload();
        emptySkillId["known_active_skill_ids"] = new GArray { "slash", "" };
        AssertRejected(emptySkillId, "known_active_skill_ids 空元素应拒绝。");

        GDictionary duplicateSkillId = Payload();
        duplicateSkillId["known_active_skill_ids"] = new GArray { "slash", "slash" };
        AssertRejected(duplicateSkillId, "known_active_skill_ids 重复元素应拒绝。");

        GDictionary badMovementTag = Payload();
        badMovementTag["movement_tags"] = new GArray { "grounded", 3 };
        AssertRejected(badMovementTag, "movement_tags 非 String/StringName 元素应拒绝。");

        GDictionary duplicateTraitId = Payload();
        duplicateTraitId["race_trait_ids"] = new GArray { "brave", "brave" };
        AssertRejected(duplicateTraitId, "race_trait_ids 重复元素应拒绝。");

        GDictionary badSaveAdvantageTag = Payload();
        badSaveAdvantageTag["save_advantage_tags"] = new GArray { "charm", "" };
        AssertRejected(badSaveAdvantageTag, "save_advantage_tags 空元素应拒绝。");
    }

    private void TestRejectsBadIdentityProjectionFields()
    {
        GDictionary categoryMismatch = Payload();
        categoryMismatch["body_size_category"] = "medium";
        AssertRejected(categoryMismatch, "body_size_category 与 body_size 不一致应拒绝。");

        GDictionary invalidCategory = Payload();
        invalidCategory["body_size_category"] = "colossal";
        AssertRejected(invalidCategory, "非法 body_size_category 应拒绝。");

        GDictionary badDamageKey = Payload();
        DictDictionary(badDamageKey, "damage_resistances")[3] = "half";
        AssertRejected(badDamageKey, "damage_resistances 非字符串 key 应拒绝。");

        GDictionary badDamageValue = Payload();
        DictDictionary(badDamageValue, "damage_resistances")["fire"] = "quarter";
        AssertRejected(badDamageValue, "damage_resistances 非法 mitigation tier 应拒绝。");
    }

    private void TestRejectsBadCombatResourceUnlocks()
    {
        GDictionary missingHp = Payload();
        missingHp["unlocked_combat_resource_ids"] = new GArray { "stamina" };
        AssertRejected(missingHp, "unlocked_combat_resource_ids 缺 hp 应拒绝。");

        GDictionary missingStamina = Payload();
        missingStamina["unlocked_combat_resource_ids"] = new GArray { "hp" };
        AssertRejected(missingStamina, "unlocked_combat_resource_ids 缺 stamina 应拒绝。");

        GDictionary illegalResource = Payload();
        illegalResource["unlocked_combat_resource_ids"] = new GArray { "hp", "stamina", "rage" };
        AssertRejected(illegalResource, "unlocked_combat_resource_ids 含非法资源应拒绝。");

        GDictionary duplicateResource = Payload();
        duplicateResource["unlocked_combat_resource_ids"] = new GArray { "hp", "stamina", "hp" };
        AssertRejected(duplicateResource, "unlocked_combat_resource_ids 重复资源应拒绝。");
    }

    private void TestRejectsBadStatusEffectEntries()
    {
        GDictionary badEntry = Payload();
        DictDictionary(badEntry, "status_effects")["burning"] = "bad";
        AssertRejected(badEntry, "status_effects 坏 entry 应拒绝整份 unit payload。");

        GDictionary keyMismatch = Payload();
        DictDictionary(DictDictionary(keyMismatch, "status_effects"), "burning")["status_id"] = "slow";
        AssertRejected(keyMismatch, "status_effects key 与 payload.status_id 不一致应拒绝。");

        GDictionary emptyKey = Payload();
        GDictionary statusEffects = DictDictionary(emptyKey, "status_effects");
        statusEffects[""] = statusEffects["burning"];
        statusEffects.Remove("burning");
        AssertRejected(emptyKey, "status_effects 空 key 应拒绝。");
    }

    private void TestRejectsEquipmentViewBadPayload()
    {
        GDictionary payload = Payload();
        DictDictionary(payload, "equipment_view").Remove("equipped_slots");
        AssertRejected(payload, "equipment_view 无法由 EquipmentState.from_dict 恢复时应拒绝整份 payload。");
    }

    private void TestRejectsBadWeaponDicePayloads()
    {
        GDictionary stringDice = Payload();
        DictDictionary(stringDice, "weapon_one_handed_dice")["dice_count"] = "1";
        AssertRejected(stringDice, "weapon dice 字符串数字应拒绝。");

        GDictionary missingDiceField = Payload();
        DictDictionary(missingDiceField, "weapon_one_handed_dice").Remove("flat_bonus");
        AssertRejected(missingDiceField, "weapon dice 缺字段应拒绝。");

        GDictionary extraDiceField = Payload();
        DictDictionary(extraDiceField, "weapon_one_handed_dice")["legacy_bonus"] = 1;
        AssertRejected(extraDiceField, "weapon dice 旧额外字段应拒绝。");

        GDictionary invalidSides = Payload();
        DictDictionary(invalidSides, "weapon_two_handed_dice")["dice_sides"] = 0;
        AssertRejected(invalidSides, "weapon dice dice_sides <= 0 应拒绝。");

        GDictionary invalidKind = Payload();
        invalidKind["weapon_profile_kind"] = "legacy_weapon";
        AssertRejected(invalidKind, "非法 weapon_profile_kind 应拒绝。");

        GDictionary invalidGrip = Payload();
        invalidGrip["weapon_current_grip"] = "legacy_grip";
        AssertRejected(invalidGrip, "非法 weapon_current_grip 应拒绝。");
    }

    private void TestBodySizeRulesWrapperIsRemoved()
    {
        AssertTrue(
            FindLoadedType("BodySizeRules") == null,
            "BodySizeRules Godot wrapper 应删除，测试和生产路径应直接使用 BodySizeContentRules 或本地 helper。"
        );
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(typeof(BodySizeContentRules)),
            "BodySizeContentRules 不应继承 RefCounted。"
        );
        AssertTrue(
            typeof(BodySizeContentRules).GetCustomAttributes(typeof(GlobalClassAttribute), false).Length == 0,
            "BodySizeContentRules 不应注册为 Godot GlobalClass。"
        );
    }

    private static BattleUnitState BuildUnit()
    {
        BattleUnitState unit = new()
        {
            unit_id = "schema_unit",
            source_member_id = "member_1",
            display_name = "Schema Unit",
            faction_id = "player",
            control_mode = "manual",
            coord = new Vector2I(3, 4),
            body_size = 3,
            body_size_category = "large",
            current_hp = 21,
            current_mp = 4,
            current_stamina = 13,
            current_aura = 2,
            current_ap = 1,
            current_move_points = 5,
            unlocked_combat_resource_ids = new GStringNameArray { "hp", "stamina", "aura" },
            stamina_recovery_progress = 7,
            current_shield_hp = 4,
            shield_max_hp = 8,
            shield_duration = 30,
            shield_family = "ward",
            shield_source_unit_id = "schema_unit",
            shield_source_skill_id = "ward_skill",
            action_progress = 20,
            action_threshold = 140,
            known_active_skill_ids = new GStringNameArray { "slash" },
            known_skill_level_map = new GDictionary { [new StringName("slash")] = 2 },
            movement_tags = new GStringNameArray { "grounded" },
            vision_tags = new GStringNameArray { "darkvision" },
            proficiency_tags = new GStringNameArray { "light_armor" },
            save_advantage_tags = new GStringNameArray { "charm" },
            damage_resistances = new GDictionary { [new StringName("fire")] = "half" },
            race_trait_ids = new GStringNameArray { "brave" },
            subrace_trait_ids = new GStringNameArray { "fleet_of_foot" },
            ascension_trait_ids = new GStringNameArray { "dragon_breath" },
            bloodline_trait_ids = new GStringNameArray { "draconic_resilience" },
            versatility_pick = "strength",
            cooldowns = new GDictionary { [new StringName("slash")] = 12 },
            last_turn_tu = 50,
        };
        unit.attribute_snapshot.set_value("strength", 3);
        unit.attribute_snapshot.set_value("aura_max", 6);
        unit.apply_weapon_projection(
            new GDictionary
            {
                ["weapon_profile_kind"] = "equipped",
                ["weapon_item_id"] = "training_longsword",
                ["weapon_profile_type_id"] = "longsword",
                ["weapon_family"] = "sword",
                ["weapon_current_grip"] = "two_handed",
                ["weapon_attack_range"] = 2,
                ["weapon_one_handed_dice"] = new GDictionary
                {
                    ["dice_count"] = 1,
                    ["dice_sides"] = 8,
                    ["flat_bonus"] = 0,
                },
                ["weapon_two_handed_dice"] = new GDictionary
                {
                    ["dice_count"] = 1,
                    ["dice_sides"] = 10,
                    ["flat_bonus"] = 1,
                },
                ["weapon_is_versatile"] = true,
                ["weapon_uses_two_hands"] = true,
                ["weapon_physical_damage_tag"] = "physical_slash",
            }
        );
        BattleStatusEffectState effect = new()
        {
            status_id = "burning",
            source_unit_id = "source",
            power = 3,
            @params = new GDictionary { ["element"] = "fire" },
            stacks = 2,
            duration = 20,
        };
        unit.set_status_effect(effect);
        return unit;
    }

    private static GDictionary Payload() => BuildUnit().to_dict();

    private static BattleUnitState BuildMinimalUnit() =>
        new()
        {
            unit_id = "body_size_unit",
            display_name = "Body Size Unit",
            faction_id = "player",
            control_mode = "manual",
        };

    private void AssertRejected(GDictionary payload, string message)
    {
        AssertTrue(BattleUnitState.from_dict(payload) == null, message);
    }

    private static GDictionary DictDictionary(GDictionary data, string key)
    {
        return data[key].AsGodotDictionary();
    }

    private static GArray DictArray(GDictionary data, string key)
    {
        return data[key].AsGodotArray();
    }

    private static int DictInt(GDictionary data, string key, int defaultValue = 0)
    {
        return data.ContainsKey(key) ? data[key].AsInt32() : defaultValue;
    }

    private static string DictString(GDictionary data, string key)
    {
        return data.ContainsKey(key) ? ProgressionDataUtils.to_string_name(data[key]).ToString() : "";
    }

    private static Vector2I DictVector2I(GDictionary data, string key)
    {
        return data.ContainsKey(key) ? data[key].AsVector2I() : Vector2I.Zero;
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? ProgressionDataUtils.to_string_name(data[key]) : "";
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertVariantEq(object actual, object expected, string message)
    {
        string actualText = StableVariantText(actual);
        string expectedText = StableVariantText(expected);
        if (actualText != expectedText)
            _failures.Add($"{message} | actual={actualText} expected={expectedText}");
    }

    private static string StableVariantText(object value)
    {
        if (value == null)
            return "<null>";
        if (value is Variant variant)
            return StableVariantText(variant);
        if (value is GDictionary dictionary)
            return StableDictionaryText(dictionary);
        if (value is GArray array)
            return StableArrayText(array);
        if (value is GStringNameArray stringNameArray)
            return StableStringNameArrayText(stringNameArray);
        if (value is StringName stringName)
            return stringName.ToString();
        if (value is Vector2I vector)
            return StableVector2IText(vector);
        return value.ToString() ?? "";
    }

    private static string StableVariantText(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => "<nil>",
            Variant.Type.Bool => value.AsBool() ? "true" : "false",
            Variant.Type.Int => value.AsInt64().ToString(),
            Variant.Type.Float => value.AsDouble().ToString("R"),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            Variant.Type.Vector2I => StableVector2IText(value.AsVector2I()),
            Variant.Type.Dictionary => StableDictionaryText(value.AsGodotDictionary()),
            Variant.Type.Array => StableArrayText(value.AsGodotArray()),
            _ => value.ToString(),
        };
    }

    private static string StableDictionaryText(GDictionary dictionary)
    {
        var parts = new List<string>();
        foreach (Variant key in dictionary.Keys)
            parts.Add($"{StableVariantText(key)}:{StableVariantText(dictionary[key])}");
        parts.Sort(StringComparer.Ordinal);
        return "{" + string.Join(",", parts) + "}";
    }

    private static string StableArrayText(GArray array)
    {
        var parts = new List<string>();
        foreach (Variant value in array)
            parts.Add(StableVariantText(value));
        return "[" + string.Join(",", parts) + "]";
    }

    private static string StableStringNameArrayText(GStringNameArray array)
    {
        var parts = new List<string>();
        foreach (StringName value in array)
            parts.Add(value.ToString());
        return "[" + string.Join(",", parts) + "]";
    }

    private static string StableVector2IText(Vector2I vector)
    {
        return $"Vector2I({vector.X},{vector.Y})";
    }

    private static Type FindLoadedType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }
        return null;
    }
}
