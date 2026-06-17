using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_damage_resistance_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDamageResistanceHalvesMatchingDamageTag();
        TestDamageResistanceCancelsWithStatusVulnerability();
        TestDamageResistanceImmuneKeepsHighestPriority();
        TestMissingDamageTagFailsClosedWithoutPhysicalSlashFallback();
        TestMissingWeaponDamageProjectionFailsClosed();
        Quit(_test.Finish("Damage resistance regression"));
    }

    private void TestDamageResistanceHalvesMatchingDamageTag()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("resistance_source", "enemy");
        BattleUnitState target = MakeUnit("fire_resistant_target", "player");
        target.damage_resistances["fire"] = new StringName("half");
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new[] { MakeDamageEffect("fire", 10) }));

        _test.Eq(ReadInt(result, "damage", -1), 5, "damage_resistances fire=half should halve fire damage.");
        GDictionary @event = FirstDamageEvent(result);
        _test.Eq(ReadString(@event, "mitigation_tier"), "half", "damage event should record half mitigation tier.");
        _test.True(
            SourcesInclude(@event.ContainsKey("mitigation_sources") ? @event["mitigation_sources"].AsGodotArray() : new GArray(), "damage_resistance_fire", "damage_resistance", "half"),
            "damage event should record damage_resistance source."
        );
    }

    private void TestDamageResistanceCancelsWithStatusVulnerability()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("cancel_source", "enemy");
        BattleUnitState target = MakeUnit("cancel_target", "player");
        target.damage_resistances["fire"] = new StringName("half");
        SetStatus(target, "fire_vulnerability", new GDictionary { ["damage_tag"] = new StringName("fire") }, "double");

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new[] { MakeDamageEffect("fire", 10) }));
        _test.Eq(ReadInt(result, "damage", -1), 10, "damage_resistance half should cancel matching double status.");
        GDictionary @event = FirstDamageEvent(result);
        _test.Eq(ReadString(@event, "mitigation_tier"), "normal", "canceled half/double should record normal tier.");
        GArray sources = @event.ContainsKey("mitigation_sources") ? @event["mitigation_sources"].AsGodotArray() : new GArray();
        _test.True(SourcesInclude(sources, "damage_resistance_fire", "damage_resistance", "half"), "canceled sources should still include damage_resistance source.");
        _test.True(SourcesInclude(sources, "fire_vulnerability", "mitigation_tier", "double"), "canceled sources should still include status vulnerability source.");
    }

    private void TestDamageResistanceImmuneKeepsHighestPriority()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("immune_source", "enemy");
        BattleUnitState target = MakeUnit("immune_target", "player");
        target.damage_resistances["negative_energy"] = new StringName("immune");
        SetStatus(target, "negative_vulnerability", new GDictionary { ["damage_tag"] = new StringName("negative_energy") }, "double");

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new[] { MakeDamageEffect("negative_energy", 10) }));
        _test.Eq(ReadInt(result, "damage", -1), 0, "immune damage_resistance should override matching double status.");
        GDictionary @event = FirstDamageEvent(result);
        _test.Eq(ReadString(@event, "mitigation_tier"), "immune", "immune resistance should record immune tier.");
        GArray sources = @event.ContainsKey("mitigation_sources") ? @event["mitigation_sources"].AsGodotArray() : new GArray();
        _test.True(SourcesInclude(sources, "damage_resistance_negative_energy", "damage_resistance", "immune"), "immune source should come from damage_resistances.");
    }

    private void TestMissingDamageTagFailsClosedWithoutPhysicalSlashFallback()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("missing_tag_source", "enemy");
        BattleUnitState target = MakeUnit("missing_tag_target", "player");
        var effect = new CombatEffectDef { effect_type = "damage", power = 10 };

        int hpBefore = target.current_hp;
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new[] { effect }));
        _test.False(ReadBool(result, "applied", true), "缺少 damage_tag 的伤害效果不应被当作已应用。");
        _test.Eq(ReadInt(result, "damage", -1), 0, "缺少 damage_tag 的伤害效果不应通过 hidden physical_slash fallback 造成伤害。");
        _test.Eq(target.current_hp, hpBefore, "缺少 damage_tag 时目标 HP 不应变化。");
        _test.Eq((result.ContainsKey("damage_events") ? result["damage_events"].AsGodotArray().Count : 0), 0, "缺少 damage_tag 时不应生成普通零伤害事件。");
        _test.Eq(ReadString(result, "error_code"), "invalid_damage_tag", "缺少 damage_tag 应返回 invalid_damage_tag 诊断。");
    }

    private void TestMissingWeaponDamageProjectionFailsClosed()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("missing_weapon_projection_source", "enemy");
        source.weapon_physical_damage_tag = "";
        BattleUnitState target = MakeUnit("missing_weapon_projection_target", "player");
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            power = 10,
            use_weapon_physical_damage_tag = true,
        };

        int hpBefore = target.current_hp;
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new[] { effect }));
        _test.False(ReadBool(result, "applied", true), "武器伤害类型投影缺失时不应被当作已应用。");
        _test.Eq(ReadInt(result, "damage", -1), 0, "武器伤害类型投影缺失时不应 fallback 到 physical_slash。");
        _test.Eq(target.current_hp, hpBefore, "武器伤害类型投影缺失时目标 HP 不应变化。");
        _test.Eq((result.ContainsKey("damage_events") ? result["damage_events"].AsGodotArray().Count : 0), 0, "武器伤害类型投影缺失时不应生成普通零伤害事件。");
        _test.Eq(ReadString(result, "error_code"), "invalid_damage_tag", "武器伤害类型投影缺失应返回 invalid_damage_tag 诊断。");
    }

    private static CombatEffectDef MakeDamageEffect(StringName damageTag, int power) =>
        new() { effect_type = "damage", damage_tag = damageTag, power = power };

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 30,
            current_mp = 0,
            current_ap = 2,
            current_stamina = 20,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 0);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 0);
        return unit;
    }

    private static void SetStatus(BattleUnitState unit, StringName statusId, GDictionary parameters, StringName mitigationTier)
    {
        var status = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = unit.unit_id,
            power = 1,
            stacks = 1,
            duration = -1,
            @params = parameters.Duplicate(true),
            mitigation_tier = mitigationTier,
        };
        unit.SetStatusEffect(status);
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        if (!result.ContainsKey("damage_events"))
            return new GDictionary();
        GArray events = result["damage_events"].AsGodotArray();
        return events.Count > 0 ? events[0].AsGodotDictionary() : new GDictionary();
    }

    private static bool SourcesInclude(GArray sources, string statusId, string sourceType, string tier)
    {
        foreach (Variant sourceValue in sources)
        {
            GDictionary source = sourceValue.AsGodotDictionary();
            if (ReadString(source, "status_id") == statusId
                && ReadString(source, "type") == sourceType
                && ReadString(source, "tier") == tier)
            {
                return true;
            }
        }
        return false;
    }

    private static int ReadInt(GDictionary data, string key, int defaultValue) =>
        data.ContainsKey(key) ? data[key].AsInt32() : defaultValue;

    private static bool ReadBool(GDictionary data, string key, bool defaultValue) =>
        data.ContainsKey(key) ? data[key].AsBool() : defaultValue;

    private static string ReadString(GDictionary data, string key) =>
        data.ContainsKey(key) ? data[key].AsString() : "";
}

