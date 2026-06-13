using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_dragon_breath_regression : SceneTree
{
    private static readonly StringName DragonBreathFireCone = "dragon_breath_fire_cone";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialDragonBreathSkillResourcesAreSchemaStable();
        TestSkillCastBlockReasonUsesTypedCooldown();
        TestRacialSkillPerBattleChargeBlocksAndConsumes();
        TestRacialSkillConsumesPerBattleAndPerTurnChargesTogether();
        TestRacialSkillPerTurnChargeRefreshesFromIdentityProjection();

        Quit(_test.Finish("Dragon breath regression"));
    }

    private void TestOfficialDragonBreathSkillResourcesAreSchemaStable()
    {
        SkillContentRegistry registry = new();
        AssertCurrentOfficialSkillValidationErrors(
            registry.Validate(),
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = registry.GetSkillDefsTyped();
        foreach (var kvp in expectedSpecs)
        {
            StringName skillId = kvp.Key;
            SkillDef skillDef = skillDefs.TryGetValue(skillId, out SkillDef loadedSkillDef)
                ? loadedSkillDef
                : null;
            _test.True(
                skillDef != null,
                $"{skillId} should be registered as official skill content."
            );
            if (skillDef == null)
                continue;
            _test.Eq(skillDef.learn_source, new StringName("subrace"), $"{skillId} should be granted by Dragonborn subrace content.");
            _test.True(skillDef.combat_profile != null, $"{skillId} should declare a combat profile.");
            if (skillDef.combat_profile == null)
                continue;
            _test.Eq(skillDef.combat_profile.target_mode, new StringName("ground"), $"{skillId} should use ground targeting.");
            _test.Eq(
                skillDef.combat_profile.area_pattern,
                kvp.Value.AreaPattern,
                $"{skillId} should keep its configured area pattern."
            );
            _test.Eq(skillDef.combat_profile.ap_cost, 1, $"{skillId} should cost 1 AP before charge gating.");
            _test.True(
                skillDef.combat_profile.effect_defs.Count > 0,
                $"{skillId} should declare a damage effect."
            );
            if (skillDef.combat_profile.effect_defs.Count == 0)
                continue;
            CombatEffectDef effectDef = skillDef.combat_profile.effect_defs[0];
            _test.True(effectDef != null, $"{skillId} damage effect should be a CombatEffectDef.");
            if (effectDef == null)
                continue;
            _test.Eq(effectDef.effect_type, new StringName("damage"), $"{skillId} should use the normal damage effect pipeline.");
            _test.Eq(effectDef.damage_tag, kvp.Value.DamageTag, $"{skillId} should keep its damage tag.");
            _test.Eq(effectDef.save_dc, 12, $"{skillId} should declare a dragon breath save DC.");
            _test.Eq(effectDef.save_ability, new StringName("constitution"), $"{skillId} should use constitution saves.");
            _test.Eq(effectDef.save_tag, new StringName("dragon_breath"), $"{skillId} should use the dragon_breath save tag.");
            _test.True(effectDef.save_partial_on_success, $"{skillId} should keep half damage on successful save.");
        }
        registry.Dispose();
    }

    private void TestSkillCastBlockReasonUsesTypedCooldown()
    {
        var resolver = new BattleRuntimeSkillTurnResolver();
        BattleUnitState unit = BuildUnit(
            "dragon_breath_cooldown_user",
            "player",
            new Vector2I(1, 1),
            new[] { DragonBreathFireCone },
            2
        );
        SkillDef skillDef = BuildDragonBreathSkill(DragonBreathFireCone, "fire", "cone");
        unit.SetCooldownTyped(skillDef.skill_id, 2);

        BattleSkillCastBlockReasonKind blockReason = resolver.GetSkillCastBlockReason(unit, skillDef);
        _test.Eq(
            blockReason,
            BattleSkillCastBlockReasonKind.Cooldown,
            "skill turn resolver 应通过 typed cooldown accessor 返回冷却 block reason。"
        );
    }

    private void TestRacialSkillPerBattleChargeBlocksAndConsumes()
    {
        SkillDef skillDef = BuildDragonBreathSkill(DragonBreathFireCone, "fire", "cone");
        BattleRuntimeModule runtime = BuildRuntime(
            new Dictionary<StringName, SkillDef> { [skillDef.skill_id] = skillDef }
        );
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
        BattlePreview blockedPreview = runtime.PreviewCommand(command);
        _test.True(
            blockedPreview != null && !blockedPreview.allowed,
            "dragon breath should be blocked when per-battle charge is 0."
        );
        _test.True(blockedPreview?.log_lines.Count > 0, "blocked preview should report a block reason.");

        caster.per_battle_charges[RacialSkillChargeKey(skillDef.skill_id)] = 1;
        BattlePreview allowedPreview = runtime.PreviewCommand(command);
        _test.True(
            allowedPreview != null && allowedPreview.allowed,
            "dragon breath should preview as allowed while charge remains."
        );
        int hpBefore = target.current_hp;
        BattleEventBatch batch = runtime.IssueCommand(command);
        _test.True(
            target.current_hp < hpBefore,
            "dragon breath should resolve through the normal ground skill damage path."
        );
        _test.Eq(
            GetInt(caster.per_battle_charges, RacialSkillChargeKey(skillDef.skill_id), -1),
            0,
            "dragon breath should consume its per-battle identity skill charge after execution starts."
        );
        _test.True(
            batch != null && batch.changed_unit_ids.Contains(caster.unit_id),
            "charge consumption should mark the caster changed through the normal skill command path."
        );

        state.phase = "unit_acting";
        state.active_unit_id = caster.unit_id;
        caster.current_ap = 1;
        BattlePreview secondPreview = runtime.PreviewCommand(command);
        _test.True(
            secondPreview != null && !secondPreview.allowed,
            "spent dragon breath should block the second cast."
        );
        _test.True(secondPreview?.log_lines.Count > 0, "second preview should keep a block reason.");
        runtime.Dispose();
    }

    private void TestRacialSkillConsumesPerBattleAndPerTurnChargesTogether()
    {
        SkillDef skillDef = BuildDragonBreathSkill(
            "dragon_breath_dual_charge_contract",
            "fire",
            "cone"
        );
        BattleRuntimeModule runtime = BuildRuntime(
            new Dictionary<StringName, SkillDef> { [skillDef.skill_id] = skillDef }
        );
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
        BattleEventBatch batch = runtime.IssueCommand(command);

        _test.True(batch != null, "dual charge dragon breath should execute.");
        _test.Eq(
            GetInt(caster.per_battle_charges, chargeKey, -1),
            0,
            "identity skill should consume per-battle charge when present."
        );
        _test.Eq(
            GetInt(caster.per_turn_charges, chargeKey, -1),
            0,
            "identity skill should also consume per-turn charge when present."
        );
        runtime.Dispose();
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
        RaceTraitResolver.ApplyToUnit(unit, context);
        StringName chargeKey = RacialSkillChargeKey(grant.skill_id);
        _test.Eq(
            unit.GetPerTurnChargeTyped(chargeKey, -1),
            2,
            "race projection should initialize current per-turn racial skill charges."
        );
        _test.Eq(
            unit.GetPerTurnChargeLimitTyped(chargeKey, -1),
            2,
            "race projection should initialize per-turn racial skill charge limits."
        );
        unit.SetPerTurnChargeTyped(chargeKey, 0);
        unit.ResetPerTurnCharges();
        _test.Eq(
            unit.GetPerTurnChargeTyped(chargeKey, -1),
            2,
            "turn start reset should refresh per-turn racial skill charges from limits."
        );
    }

    private static BattleRuntimeModule BuildRuntime(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
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
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        BattleCellState cell = new()
        {
            coord = coord,
            base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
            base_height = 4,
            height_offset = 0,
        };
        cell.RecalculateRuntimeValues();
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
        unit.attribute_snapshot.SetValue("constitution", 0);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
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
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
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
        _test.True(errorList.Count == 0, $"{message} errors={string.Join(", ", errorList)}");
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

}
