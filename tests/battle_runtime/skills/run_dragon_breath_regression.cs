using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_dragon_breath_regression : SceneTree
{
    private static readonly StringName DragonBreathFireCone = "dragon_breath_fire_cone";
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialDragonBreathSkillResourcesAreSchemaStable();
        TestRacialSkillPerBattleChargeBlocksAndConsumes();
        TestRacialSkillConsumesPerBattleAndPerTurnChargesTogether();
        TestRacialSkillPerTurnChargeRefreshesFromIdentityProjection();

        if (_failures.Count == 0)
        {
            GD.Print("Dragon breath regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Dragon breath regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestOfficialDragonBreathSkillResourcesAreSchemaStable()
    {
        SkillContentRegistry registry = new();
        AssertCurrentOfficialSkillValidationErrors(
            registry.validate(),
            "official skill registry should validate cleanly."
        );
        var expectedSpecs = new Dictionary<StringName, (StringName DamageTag, StringName AreaPattern)>
        {
            ["dragon_breath_fire_cone"] = ("fire", "cone"),
            ["dragon_breath_fire_line"] = ("fire", "line"),
            ["dragon_breath_freeze_cone"] = ("freeze", "cone"),
            ["dragon_breath_poison_cone"] = ("poison", "cone"),
            ["dragon_breath_acid_line"] = ("acid", "line"),
            ["dragon_breath_lightning_line"] = ("lightning", "line"),
        };
        GDictionary skillDefs = registry.get_skill_defs();
        foreach (var kvp in expectedSpecs)
        {
            StringName skillId = kvp.Key;
            SkillDef skillDef = skillDefs.ContainsKey(skillId) ? skillDefs[skillId].As<SkillDef>() : null;
            AssertTrue(
                skillDef != null,
                $"{skillId} should be registered as official skill content."
            );
            if (skillDef == null)
                continue;
            AssertEq(skillDef.learn_source, new StringName("subrace"), $"{skillId} should be granted by Dragonborn subrace content.");
            AssertTrue(skillDef.combat_profile != null, $"{skillId} should declare a combat profile.");
            if (skillDef.combat_profile == null)
                continue;
            AssertEq(skillDef.combat_profile.target_mode, new StringName("ground"), $"{skillId} should use ground targeting.");
            AssertEq(
                skillDef.combat_profile.area_pattern,
                kvp.Value.AreaPattern,
                $"{skillId} should keep its configured area pattern."
            );
            AssertEq(skillDef.combat_profile.ap_cost, 1, $"{skillId} should cost 1 AP before charge gating.");
            AssertTrue(
                skillDef.combat_profile.effect_defs.Count > 0,
                $"{skillId} should declare a damage effect."
            );
            if (skillDef.combat_profile.effect_defs.Count == 0)
                continue;
            CombatEffectDef effectDef = skillDef.combat_profile.effect_defs[0];
            AssertTrue(effectDef != null, $"{skillId} damage effect should be a CombatEffectDef.");
            if (effectDef == null)
                continue;
            AssertEq(effectDef.effect_type, new StringName("damage"), $"{skillId} should use the normal damage effect pipeline.");
            AssertEq(effectDef.damage_tag, kvp.Value.DamageTag, $"{skillId} should keep its damage tag.");
            AssertEq(effectDef.save_dc, 12, $"{skillId} should declare a dragon breath save DC.");
            AssertEq(effectDef.save_ability, new StringName("constitution"), $"{skillId} should use constitution saves.");
            AssertEq(effectDef.save_tag, new StringName("dragon_breath"), $"{skillId} should use the dragon_breath save tag.");
            AssertTrue(effectDef.save_partial_on_success, $"{skillId} should keep half damage on successful save.");
        }
        registry.dispose();
    }

    private void TestRacialSkillPerBattleChargeBlocksAndConsumes()
    {
        SkillDef skillDef = BuildDragonBreathSkill(DragonBreathFireCone, "fire", "cone");
        BattleRuntimeModule runtime = BuildRuntime(new GDictionary { [skillDef.skill_id] = skillDef });
        BattleState state = BuildState(new Vector2I(5, 3));
        runtime._state = state;
        BattleUnitState caster = BuildUnit(
            "dragon_breath_user",
            "player",
            new Vector2I(1, 1),
            new[] { skillDef.skill_id },
            2
        );
        BattleUnitState target = BuildUnit(
            "dragon_breath_target",
            "enemy",
            new Vector2I(2, 1),
            System.Array.Empty<StringName>(),
            0
        );
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.active_unit_id = caster.unit_id;

        BattleCommand command = BuildGroundSkillCommand(
            caster.unit_id,
            skillDef.skill_id,
            new Vector2I(2, 1)
        );
        caster.per_battle_charges[RacialSkillChargeKey(skillDef.skill_id)] = 0;
        BattlePreview blockedPreview = runtime.preview_command(command);
        AssertTrue(
            blockedPreview != null && !blockedPreview.allowed,
            "dragon breath should be blocked when per-battle charge is 0."
        );
        AssertLogContains(
            blockedPreview?.log_lines,
            "次数已用尽",
            "blocked preview should report spent identity skill charges."
        );

        caster.per_battle_charges[RacialSkillChargeKey(skillDef.skill_id)] = 1;
        BattlePreview allowedPreview = runtime.preview_command(command);
        AssertTrue(
            allowedPreview != null && allowedPreview.allowed,
            "dragon breath should preview as allowed while charge remains."
        );
        int hpBefore = target.current_hp;
        BattleEventBatch batch = runtime.issue_command(command);
        AssertTrue(
            target.current_hp < hpBefore,
            "dragon breath should resolve through the normal ground skill damage path."
        );
        AssertEq(
            GetInt(caster.per_battle_charges, RacialSkillChargeKey(skillDef.skill_id), -1),
            0,
            "dragon breath should consume its per-battle identity skill charge after execution starts."
        );
        AssertTrue(
            batch != null && batch.changed_unit_ids.Contains(caster.unit_id),
            "charge consumption should mark the caster changed through the normal skill command path."
        );

        state.phase = "unit_acting";
        state.active_unit_id = caster.unit_id;
        caster.current_ap = 1;
        BattlePreview secondPreview = runtime.preview_command(command);
        AssertTrue(
            secondPreview != null && !secondPreview.allowed,
            "spent dragon breath should block the second cast."
        );
        AssertLogContains(
            secondPreview?.log_lines,
            "次数已用尽",
            "second preview should keep the charge block reason."
        );
        runtime.dispose();
    }

    private void TestRacialSkillConsumesPerBattleAndPerTurnChargesTogether()
    {
        SkillDef skillDef = BuildDragonBreathSkill(
            "dragon_breath_dual_charge_contract",
            "fire",
            "cone"
        );
        BattleRuntimeModule runtime = BuildRuntime(new GDictionary { [skillDef.skill_id] = skillDef });
        BattleState state = BuildState(new Vector2I(5, 3));
        runtime._state = state;
        BattleUnitState caster = BuildUnit(
            "dragon_breath_dual_user",
            "player",
            new Vector2I(1, 1),
            new[] { skillDef.skill_id },
            2
        );
        BattleUnitState target = BuildUnit(
            "dragon_breath_dual_target",
            "enemy",
            new Vector2I(2, 1),
            System.Array.Empty<StringName>(),
            0
        );
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.active_unit_id = caster.unit_id;
        StringName chargeKey = RacialSkillChargeKey(skillDef.skill_id);
        caster.per_battle_charges[chargeKey] = 1;
        caster.per_turn_charges[chargeKey] = 1;

        BattleCommand command = BuildGroundSkillCommand(
            caster.unit_id,
            skillDef.skill_id,
            new Vector2I(2, 1)
        );
        BattleEventBatch batch = runtime.issue_command(command);

        AssertTrue(batch != null, "dual charge dragon breath should execute.");
        AssertEq(
            GetInt(caster.per_battle_charges, chargeKey, -1),
            0,
            "identity skill should consume per-battle charge when present."
        );
        AssertEq(
            GetInt(caster.per_turn_charges, chargeKey, -1),
            0,
            "identity skill should also consume per-turn charge when present."
        );
        runtime.dispose();
    }

    private void TestRacialSkillPerTurnChargeRefreshesFromIdentityProjection()
    {
        BattleUnitState unit = new();
        RacialGrantedSkill grant = new()
        {
            skill_id = "dragon_breath_freeze_cone",
            charge_kind = "per_turn",
            charges = 2,
        };
        RaceDef race = new()
        {
            race_id = "dragon_fixture",
            racial_granted_skills = new Godot.Collections.Array<RacialGrantedSkill> { grant },
        };
        PassiveSourceContext context = new() { race_def = race };
        RaceTraitResolver.apply_to_unit(unit, context);
        StringName chargeKey = RacialSkillChargeKey(grant.skill_id);
        AssertEq(
            GetInt(unit.per_turn_charges, chargeKey, -1),
            2,
            "race projection should initialize current per-turn racial skill charges."
        );
        AssertEq(
            GetInt(unit.per_turn_charge_limits, chargeKey, -1),
            2,
            "race projection should initialize per-turn racial skill charge limits."
        );
        unit.per_turn_charges[chargeKey] = 0;
        unit.reset_per_turn_charges();
        AssertEq(
            GetInt(unit.per_turn_charges, chargeKey, -1),
            2,
            "turn start reset should refresh per-turn racial skill charges from limits."
        );
    }

    private static BattleRuntimeModule BuildRuntime(GDictionary skillDefs)
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(null, skillDefs);
        return runtime;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "dragon_breath_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
            cells = new GDictionary(),
            units = new GDictionary(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.cells[coord] = BuildCell(coord);
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        BattleCellState cell = new()
        {
            coord = coord,
            base_terrain = BattleCellState.TERRAIN_LAND(),
            base_height = 4,
            height_offset = 0,
        };
        cell.recalculate_runtime_values();
        return cell;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        IEnumerable<StringName> skillIds,
        int currentAp
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_hp = 40,
            current_mp = 0,
            current_stamina = 20,
            current_ap = currentAp,
            is_alive = true,
        };
        foreach (StringName skillId in skillIds)
        {
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = 1;
        }
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 40);
        unit.attribute_snapshot.set_value("constitution", 0);
        unit.set_anchor_coord(coord);
        return unit;
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.place_unit(state, unit, unit.coord, true);
    }

    private static SkillDef BuildDragonBreathSkill(
        StringName skillId,
        StringName damageTag,
        StringName areaPattern
    )
    {
        CombatEffectDef effect = new()
        {
            effect_type = "damage",
            power = 12,
            damage_tag = damageTag,
            save_dc = 12,
            save_ability = "constitution",
            save_tag = "dragon_breath",
            save_partial_on_success = true,
        };
        CombatSkillDef combatProfile = new()
        {
            skill_id = skillId,
            target_mode = "ground",
            target_team_filter = "enemy",
            range_value = 3,
            area_pattern = areaPattern,
            area_value = 1,
            ap_cost = 1,
            cooldown_tu = 0,
            effect_defs = new Godot.Collections.Array<CombatEffectDef> { effect },
        };
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            icon_id = skillId,
            learn_source = "subrace",
            mastery_curve = new[] { 20 },
            tags = new Godot.Collections.Array<StringName> { "dragon_breath", damageTag },
            combat_profile = combatProfile,
        };
    }

    private static BattleCommand BuildGroundSkillCommand(
        StringName unitId,
        StringName skillId,
        Vector2I targetCoord
    )
    {
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = unitId,
            skill_id = skillId,
            target_coord = targetCoord,
        };
    }

    private static StringName RacialSkillChargeKey(StringName skillId) =>
        new($"racial_skill_{skillId}");

    private static int GetInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private void AssertCurrentOfficialSkillValidationErrors(IEnumerable<string> errors, string message)
    {
        List<string> errorList = new();
        foreach (string error in errors)
            errorList.Add(error);
        AssertTrue(errorList.Count == 0, $"{message} errors={string.Join(", ", errorList)}");
    }

    private void AssertLogContains(Godot.Collections.Array lines, string needle, string message)
    {
        if (lines != null)
        {
            foreach (Variant lineOption in lines)
            {
                if (lineOption.ToString().Contains(needle))
                    return;
            }
        }
        _failures.Add($"{message} log={FormatArray(lines)}");
    }

    private static string FormatArray(Godot.Collections.Array lines)
    {
        if (lines == null)
            return "<null>";
        List<string> values = new();
        foreach (Variant value in lines)
            values.Add(value.ToString());
        return string.Join(" | ", values);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} Expected {expected}, got {actual}.");
    }
}
