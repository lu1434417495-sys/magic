using System;
using Godot;

[GlobalClass]
public partial class BattleUnitFactory : RefCounted
{
    private static readonly StringName BASIC_ATTACK_SKILL_ID = "basic_attack";
    private static readonly StringName DEFAULT_ENEMY_MELEE_DAMAGE_TAG = "physical_slash";
    private GodotObject _runtime;

    private static AttributeSnapshot _snap(BattleUnitState us) =>
        us?.attribute_snapshot as AttributeSnapshot;

    private static AttributeSnapshot _snap(GodotObject o) => o as AttributeSnapshot;

    private static CombatSkillDef _csd(Resource r) => r as CombatSkillDef;

    private static int _gv(BattleUnitState us, StringName k) => _snap(us)?.get_value(k) ?? 0;

    private static void _sv(BattleUnitState us, StringName k, int v)
    {
        var s = _snap(us);
        if (s != null)
            s.set_value(k, v);
    }

    public void setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public Godot.Collections.Array build_ally_units(
        PartyState party_state,
        Godot.Collections.Dictionary context
    )
    {
        if (context.ContainsKey("battle_party"))
        {
            var bp = context["battle_party"];
            if (bp.VariantType == Variant.Type.Array && bp.AsGodotArray().Count > 0)
                return _normalize_unit_payloads(bp.AsGodotArray());
        }
        var member_ids = new Godot.Collections.Array();
        if (party_state?.active_member_ids != null && party_state.active_member_ids.Count > 0)
        {
            foreach (var memberId in party_state.active_member_ids)
                member_ids.Add(memberId);
        }
        if (member_ids.Count == 0)
            member_ids = _extract_ally_member_ids(context);
        var units = new Godot.Collections.Array();
        for (int i = 0; i < member_ids.Count; i++)
        {
            var mid = ProgressionDataUtils.to_string_name(member_ids[i]);
            var ms = party_state?.get_member_state(mid);
            if (ms != null && ms.progression == null)
                continue;
            var us = _build_runtime_ally_unit(mid, ms, i, context);
            if (us != null)
                units.Add(us);
        }
        return units;
    }

    public void refresh_battle_unit(BattleUnitState us)
    {
        if (us == null || (string)us.source_member_id == "" || _runtime == null)
            return;
        var cg = _runtime.Call("get_character_gateway").AsGodotObject();
        if (cg == null)
            return;
        var ms = cg.Call("get_member_state", us.source_member_id).AsGodotObject();
        if (ms == null)
            return;
        var ev = _ensure_unit_equipment_view(us, ms);
        var snap =
            _build_member_attribute_snapshot(
                ms,
                new Godot.Collections.Dictionary(),
                ev
            ) ?? new AttributeSnapshot();
        _apply_member_identity_projection(us, ms);
        us.attribute_snapshot = snap;
        refresh_weapon_projection(us);
        int hpMax = Mathf.Max(snap.get_value(AttributeService.HP_MAX), 1);
        int mpMax = Mathf.Max(snap.get_value(AttributeService.MP_MAX), 0);
        int stamMax = Mathf.Max(snap.get_value(AttributeService.STAMINA_MAX), 0);
        int auraMax = Mathf.Max(snap.get_value(AttributeService.AURA_MAX), 0);
        int apMax = Mathf.Max(snap.get_value(AttributeService.ACTION_POINTS), 1);
        us.current_hp = Mathf.Clamp(us.current_hp, 0, hpMax);
        us.current_mp = Mathf.Clamp(us.current_mp, 0, mpMax);
        us.current_stamina = Mathf.Clamp(us.current_stamina, 0, stamMax);
        us.current_aura = Mathf.Clamp(us.current_aura, 0, auraMax);
        us.current_ap = Mathf.Clamp(us.current_ap, 0, apMax);
        us.action_threshold = _resolve_action_threshold_from_snapshot(snap);
        us.current_move_points = Mathf.Clamp(
            us.current_move_points,
            0,
            BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN()
        );
        UnitProgress prog = ms.Get("progression").AsGodotObject() as UnitProgress;
        us.known_active_skill_ids = _collect_known_active_skill_ids(prog);
        us.known_skill_level_map = _collect_known_skill_level_map(prog);
        us.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(prog);
        _sync_unlocked_resources_from_progression(us, prog);
        _filter_skills_by_equipment_requirements(us);
        _ensure_basic_attack_skill(us);
        _sync_passive_battle_statuses(us, prog, ms);
        us.refresh_footprint();
    }

    public void refresh_known_skills(BattleUnitState us)
    {
        if (us == null || (string)us.source_member_id == "" || _runtime == null)
            return;
        var cg = _runtime.Call("get_character_gateway").AsGodotObject();
        if (cg == null)
            return;
        var ms = cg.Call("get_member_state", us.source_member_id).AsGodotObject();
        if (ms == null)
            return;
        var prog = ms.Get("progression").AsGodotObject();
        us.known_active_skill_ids = _collect_known_active_skill_ids(prog);
        us.known_skill_level_map = _collect_known_skill_level_map(prog);
        us.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(prog);
        _sync_unlocked_resources_from_progression(us, prog);
        _filter_skills_by_equipment_requirements(us);
        _ensure_basic_attack_skill(us);
        _sync_passive_battle_statuses(us, prog, ms);
    }

    public void refresh_weapon_projection(BattleUnitState us)
    {
        if (us == null)
            return;
        _apply_member_weapon_projection(us, us.source_member_id, us.get_equipment_view());
    }

    public void refresh_equipment_projection(BattleUnitState us)
    {
        if (us == null || (string)us.source_member_id == "" || _runtime == null)
            return;
        var cg = _runtime.Call("get_character_gateway").AsGodotObject();
        if (cg == null)
            return;
        var ms = cg.Call("get_member_state", us.source_member_id).AsGodotObject();
        if (ms == null)
            return;
        var snap =
            _build_member_attribute_snapshot(
                ms,
                new Godot.Collections.Dictionary(),
                us.get_equipment_view()
            ) ?? new AttributeSnapshot();
        var prevSnap = _snap(us);
        int prevHpMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.get_value(AttributeService.HP_MAX), 1)
                : Mathf.Max(snap.get_value(AttributeService.HP_MAX), 1);
        int prevMpMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.get_value(AttributeService.MP_MAX), 0)
                : Mathf.Max(snap.get_value(AttributeService.MP_MAX), 0);
        int prevStamMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.get_value(AttributeService.STAMINA_MAX), 0)
                : Mathf.Max(snap.get_value(AttributeService.STAMINA_MAX), 0);
        int prevAuraMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.get_value(AttributeService.AURA_MAX), 0)
                : Mathf.Max(snap.get_value(AttributeService.AURA_MAX), 0);
        us.attribute_snapshot = snap;
        refresh_weapon_projection(us);
        int hpMax = Mathf.Max(snap.get_value(AttributeService.HP_MAX), 1),
            mpMax = Mathf.Max(snap.get_value(AttributeService.MP_MAX), 0);
        int stamMax = Mathf.Max(snap.get_value(AttributeService.STAMINA_MAX), 0),
            auraMax = Mathf.Max(snap.get_value(AttributeService.AURA_MAX), 0);
        us.current_hp =
            hpMax < prevHpMax ? Mathf.Clamp(us.current_hp, 0, hpMax) : Mathf.Max(us.current_hp, 0);
        us.current_mp =
            mpMax < prevMpMax ? Mathf.Clamp(us.current_mp, 0, mpMax) : Mathf.Max(us.current_mp, 0);
        us.current_stamina =
            stamMax < prevStamMax
                ? Mathf.Clamp(us.current_stamina, 0, stamMax)
                : Mathf.Max(us.current_stamina, 0);
        us.current_aura =
            auraMax < prevAuraMax
                ? Mathf.Clamp(us.current_aura, 0, auraMax)
                : Mathf.Max(us.current_aura, 0);
        us.action_threshold = _resolve_action_threshold_from_snapshot(snap);
        var prog = ms.Get("progression").AsGodotObject();
        us.known_active_skill_ids = _collect_known_active_skill_ids(prog);
        us.known_skill_level_map = _collect_known_skill_level_map(prog);
        us.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(prog);
        _sync_unlocked_resources_from_progression(us, prog);
        _filter_skills_by_equipment_requirements(us);
        _ensure_basic_attack_skill(us);
        _sync_passive_battle_statuses(us, prog, ms);
        us.refresh_footprint();
    }

    public Godot.Collections.Array build_enemy_units(
        GodotObject enc,
        Godot.Collections.Dictionary ctx
    )
    {
        if (ctx.ContainsKey("enemy_units"))
        {
            var eu = ctx["enemy_units"];
            if (eu.VariantType == Variant.Type.Array && eu.AsGodotArray().Count > 0)
                return _normalize_unit_payloads(eu.AsGodotArray());
        }
        var aid = enc != null ? (string)enc.Get("entity_id").AsStringName() : "unknown";
        GameLog.Error($"BattleUnitFactory cannot build fallback enemy units for {aid}.", "battle.factory.fallback_failed", "battle");
        return new Godot.Collections.Array();
    }

    private Godot.Collections.Array _normalize_unit_payloads(Godot.Collections.Array pl)
    {
        var r = new Godot.Collections.Array();
        foreach (var v in pl)
        {
            if (v.VariantType == Variant.Type.Nil)
                continue;
            if (v.VariantType == Variant.Type.Dictionary)
                r.Add(BattleUnitState.from_dict(v.AsGodotDictionary()));
            else if (v.AsGodotObject() is BattleUnitState bs)
                r.Add(bs.clone());
            else if (v.AsGodotObject()?.HasMethod("to_dict") == true)
                r.Add(
                    BattleUnitState.from_dict(v.AsGodotObject().Call("to_dict").AsGodotDictionary())
                );
            else
                r.Add(v);
        }
        return r;
    }

    public Godot.Collections.Dictionary build_terrain_data(
        GodotObject enc,
        int seed,
        Godot.Collections.Dictionary ctx
    )
    {
        var tc = ctx.Duplicate(true);
        tc.Remove("map_size");
        if (_runtime != null)
        {
            var tg = _runtime.Call("get_terrain_generator").AsGodotObject();
            if (tg is BattleTerrainGenerator terrainGenerator)
            {
                return _atgo(terrainGenerator.generate(enc, seed, tc), tc);
            }
            if (tg != null)
                return _atgo(tg.Call("generate", enc, seed, tc).AsGodotDictionary(), tc);
        }
        return _atgo(new Godot.Collections.Dictionary(), tc);
    }

    private static Godot.Collections.Dictionary _atgo(
        Godot.Collections.Dictionary td,
        Godot.Collections.Dictionary ctx
    )
    {
        if (td == null || td.Count == 0)
            return new Godot.Collections.Dictionary();
        var tr = td.Duplicate(true);
        if (
            ctx.ContainsKey("ally_spawns")
            && ctx["ally_spawns"].VariantType == Variant.Type.Array
            && ctx["ally_spawns"].AsGodotArray().Count > 0
        )
            tr["ally_spawns"] = ctx["ally_spawns"].AsGodotArray().Duplicate(true);
        if (
            ctx.ContainsKey("enemy_spawns")
            && ctx["enemy_spawns"].VariantType == Variant.Type.Array
            && ctx["enemy_spawns"].AsGodotArray().Count > 0
        )
            tr["enemy_spawns"] = ctx["enemy_spawns"].AsGodotArray().Duplicate(true);
        return tr;
    }

    private BattleUnitState _build_runtime_ally_unit(
        StringName mid,
        GodotObject ms,
        int idx,
        Godot.Collections.Dictionary ctx
    )
    {
        var us = new BattleUnitState();
        us.unit_id = (string)mid != "" ? mid : $"ally_{idx + 1}";
        us.source_member_id = mid;
        us.display_name =
            ms != null && (string)ms.Get("display_name").AsString() != ""
                ? ms.Get("display_name").AsString()
                : $"队员{idx + 1}";
        us.faction_id = "player";
        us.control_mode =
            ms != null && (string)ms.Get("control_mode").AsStringName() != ""
                ? ms.Get("control_mode").AsStringName()
                : "manual";
        _apply_member_identity_projection(us, ms);
        us.set_equipment_view(_get_member_equipment_state(ms));
        var snap = _build_member_attribute_snapshot(ms, ctx, us.get_equipment_view());
        us.attribute_snapshot = snap;
        _apply_member_weapon_projection(us, mid, us.get_equipment_view());
        int hpMax = Mathf.Max(snap.get_value(AttributeService.HP_MAX), 1),
            mpMax = Mathf.Max(snap.get_value(AttributeService.MP_MAX), 0);
        int stamMax = Mathf.Max(snap.get_value(AttributeService.STAMINA_MAX), 0),
            auraMax = Mathf.Max(snap.get_value(AttributeService.AURA_MAX), 0);
        int ap = Mathf.Max(snap.get_value(AttributeService.ACTION_POINTS), 1);
        us.current_hp = Mathf.Clamp(ms != null ? ms.Get("current_hp").AsInt32() : hpMax, 0, hpMax);
        us.current_mp = Mathf.Clamp(ms != null ? ms.Get("current_mp").AsInt32() : mpMax, 0, mpMax);
        us.current_stamina = stamMax;
        us.current_aura = Mathf.Clamp(
            ms != null ? ms.Get("current_aura").AsInt32() : auraMax,
            0,
            auraMax
        );
        us.current_ap = ap;
        us.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();
        int fbAt = ctx.ContainsKey("default_ally_action_threshold")
            ? ctx["default_ally_action_threshold"].AsInt32()
            : BattleUnitState.DEFAULT_ACTION_THRESHOLD();
        us.action_threshold = _resolve_action_threshold_from_snapshot(snap, fbAt);
        var prog = ms?.Get("progression").AsGodotObject();
        us.known_active_skill_ids = _collect_known_active_skill_ids(prog);
        us.known_skill_level_map = _collect_known_skill_level_map(prog);
        us.known_skill_lock_hit_bonus_map = _collect_known_skill_lock_hit_bonus_map(prog);
        _sync_unlocked_resources_from_progression(us, prog);
        _sync_passive_battle_statuses(us, prog, ms);
        _filter_skills_by_equipment_requirements(us);
        us.movement_tags = _extract_movement_tags(
            ctx.ContainsKey("ally_movement_tags") ? ctx["ally_movement_tags"] : default
        );
        if (
            us.known_active_skill_ids.Count == 0
            && ctx.ContainsKey("default_active_skill_ids")
            && ctx["default_active_skill_ids"].VariantType == Variant.Type.Array
        )
            foreach (var sv in ctx["default_active_skill_ids"].AsGodotArray())
            {
                var ns = ProgressionDataUtils.to_string_name(sv);
                us.known_active_skill_ids.Add(ns);
                us.known_skill_level_map[ns] = 1;
            }
        _ensure_basic_attack_skill(us);
        us.is_alive = us.current_hp > 0;
        return us;
    }

    private BattleUnitState _build_runtime_enemy_unit(
        GodotObject enc,
        string mn,
        int idx,
        Godot.Collections.Dictionary ctx
    )
    {
        var us = new BattleUnitState();
        var aid = enc != null ? (string)enc.Get("entity_id").AsStringName() : "wild";
        us.unit_id = $"{aid}_{(idx + 1):D2}";
        us.source_member_id = "";
        us.display_name = idx == 0 ? mn : $"{mn}·从属{idx + 1}";
        us.faction_id =
            enc != null && (string)enc.Get("faction_id").AsStringName() != ""
                ? enc.Get("faction_id").AsStringName()
                : "hostile";
        us.control_mode = "ai";
        us.body_size = BattleUnitState.BODY_SIZE_MEDIUM();
        us.body_size_category = BodySizeRules.BODY_SIZE_CATEGORY_MEDIUM();
        us.refresh_footprint();
        int hpMax = Mathf.Max(
            ctx.ContainsKey("default_enemy_hp") ? ctx["default_enemy_hp"].AsInt32() : 12,
            1
        );
        int mpMax = Mathf.Max(
            ctx.ContainsKey("default_enemy_mp") ? ctx["default_enemy_mp"].AsInt32() : 0,
            0
        );
        int stamMax = Mathf.Max(
            ctx.ContainsKey("default_enemy_stamina") ? ctx["default_enemy_stamina"].AsInt32() : 0,
            0
        );
        int ap = Mathf.Max(
            ctx.ContainsKey("default_enemy_ap") ? ctx["default_enemy_ap"].AsInt32() : 1,
            1
        );
        foreach (var a2 in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
        {
            string ak = $"default_enemy_{(string)a2}";
            _sv(us, a2, ctx.ContainsKey(ak) ? ctx[ak].AsInt32() : 4);
        }
        _sv(us, "hp_max", hpMax);
        _sv(us, "mp_max", mpMax);
        _sv(us, "stamina_max", stamMax);
        _sv(us, "action_points", ap);
        _sv(
            us,
            AttributeService.ATTACK_BONUS,
            ctx.ContainsKey("default_enemy_attack_bonus")
                ? ctx["default_enemy_attack_bonus"].AsInt32()
                : 4
        );
        _sv(
            us,
            AttributeService.ARMOR_AC_BONUS,
            ctx.ContainsKey("default_enemy_armor_ac_bonus")
                ? ctx["default_enemy_armor_ac_bonus"].AsInt32()
                : 0
        );
        _sv(
            us,
            AttributeService.SHIELD_AC_BONUS,
            ctx.ContainsKey("default_enemy_shield_ac_bonus")
                ? ctx["default_enemy_shield_ac_bonus"].AsInt32()
                : 0
        );
        _sv(
            us,
            AttributeService.DODGE_BONUS,
            ctx.ContainsKey("default_enemy_dodge_bonus")
                ? ctx["default_enemy_dodge_bonus"].AsInt32()
                : 0
        );
        _sv(
            us,
            AttributeService.DEFLECTION_BONUS,
            ctx.ContainsKey("default_enemy_deflection_bonus")
                ? ctx["default_enemy_deflection_bonus"].AsInt32()
                : 0
        );
        _sv(us, AttributeService.ARMOR_CLASS, _resolve_snapshot_armor_class(_snap(us)));
        _sv(
            us,
            AttributeService.SPELL_PROFICIENCY_BONUS,
            ctx.ContainsKey("default_enemy_spell_proficiency_bonus")
                ? ctx["default_enemy_spell_proficiency_bonus"].AsInt32()
                : AttributeSnapshot.calculate_spell_proficiency_bonus(
                    ctx.ContainsKey("default_enemy_character_level")
                        ? ctx["default_enemy_character_level"].AsInt32()
                        : 0
                )
        );
        if (_hedwc(ctx))
        {
            int er = Mathf.Max(
                ctx.ContainsKey("default_enemy_weapon_attack_range")
                    ? ctx["default_enemy_weapon_attack_range"].AsInt32()
                    : 1,
                0
            );
            _aenwp(
                us,
                ProgressionDataUtils.to_string_name(
                    ctx.ContainsKey("default_enemy_weapon_profile_type_id")
                        ? ctx["default_enemy_weapon_profile_type_id"]
                        : "natural_weapon"
                ),
                ProgressionDataUtils.to_string_name(
                    ctx.ContainsKey("default_enemy_weapon_physical_damage_tag")
                        ? ctx["default_enemy_weapon_physical_damage_tag"]
                        : DEFAULT_ENEMY_MELEE_DAMAGE_TAG
                ),
                er,
                ProgressionDataUtils.to_string_name(
                    ctx.ContainsKey("default_enemy_weapon_family")
                        ? ctx["default_enemy_weapon_family"]
                        : ""
                )
            );
        }
        else
            us.set_unarmed_weapon_projection();
        us.action_threshold = ctx.ContainsKey("default_enemy_action_threshold")
            ? ctx["default_enemy_action_threshold"].AsInt32()
            : BattleUnitState.DEFAULT_ACTION_THRESHOLD();
        us.current_hp = hpMax;
        us.current_mp = mpMax;
        us.current_stamina = stamMax;
        us.current_ap = ap;
        us.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();
        us.is_alive = us.current_hp > 0;
        us.movement_tags = _extract_movement_tags(
            ctx.ContainsKey("enemy_movement_tags") ? ctx["enemy_movement_tags"] : default
        );
        if (
            ctx.ContainsKey("enemy_skill_ids")
            && ctx["enemy_skill_ids"].VariantType == Variant.Type.Array
        )
        {
            us.known_active_skill_ids.Clear();
            foreach (var sv in ctx["enemy_skill_ids"].AsGodotArray())
            {
                var ns = ProgressionDataUtils.to_string_name(sv);
                us.known_active_skill_ids.Add(ns);
                us.known_skill_level_map[ns] = 1;
            }
        }
        if (us.known_active_skill_ids.Count == 0)
        {
            us.known_active_skill_ids = _pick_default_enemy_skill_ids();
            foreach (var s in us.known_active_skill_ids)
                us.known_skill_level_map[s] = 1;
        }
        _ensure_basic_attack_skill(us);
        _ensure_enemy_basic_attack_affordability(us);
        _sync_enemy_unlocked_resources(us);
        return us;
    }

    private Godot.Collections.Array<StringName> _pick_default_enemy_skill_ids()
    {
        var pre = new Godot.Collections.Array<StringName>
        {
            BASIC_ATTACK_SKILL_ID,
            "warrior_heavy_strike",
            "warrior_combo_strike",
            "warrior_guard_break",
        };
        foreach (var p in pre)
            if (_is_valid_enemy_skill(_skill_def_from_runtime(p)))
                return new Godot.Collections.Array<StringName> { p };
        var sds =
            _runtime?.Call("get_skill_defs").AsGodotDictionary()
            ?? new Godot.Collections.Dictionary();
        foreach (var sk in ProgressionDataUtils.sorted_string_keys(sds))
        {
            var sid = new StringName(sk);
            if (_is_valid_enemy_skill(_skill_def_from_runtime(sid)))
                return new Godot.Collections.Array<StringName> { sid };
        }
        return new Godot.Collections.Array<StringName>();
    }

    private bool _is_valid_enemy_skill(SkillDef sd)
    {
        var cp = _csd(sd?.combat_profile);
        return sd != null
            && sd.skill_type == "active"
            && sd.can_use_in_combat()
            && cp != null
            && BattleTypedNames.ToTargetMode(cp.target_mode) == BattleTargetMode.Unit
            && BattleTargetTeamRules.is_enemy_filter(cp.target_team_filter);
    }

    private void _filter_skills_by_equipment_requirements(BattleUnitState us)
    {
        if (us == null)
            return;
        var f = new Godot.Collections.Array<StringName>();
        foreach (var sid in us.known_active_skill_ids)
        {
            var sd = _skill_def_from_runtime(sid);
            var cp = _csd(sd?.combat_profile);
            if (cp == null)
                continue;
            if (cp.requires_equipped_shield && !_unit_has_equipped_shield(us))
                continue;
            if (
                !BattleRangeService.unit_matches_required_weapon_families(
                    us,
                    cp.required_weapon_families ?? new Godot.Collections.Array<StringName>()
                )
            )
                continue;
            if (
                BattleRangeService.requires_current_melee_weapon(sd)
                && !BattleRangeService.unit_has_melee_weapon(us)
            )
                continue;
            f.Add(sid);
        }
        us.known_active_skill_ids = f;
    }

    private bool _unit_has_equipped_shield(BattleUnitState us)
    {
        var ev = us.get_equipment_view();
        if (ev == null)
            return false;
        var oid = ev.Call("get_equipped_item_id", EquipmentRules.OFF_HAND()).AsStringName();
        if ((string)oid == "")
            return false;
        var ids =
            _runtime?.Call("get_item_defs").AsGodotDictionary()
            ?? new Godot.Collections.Dictionary();
        var id = ids.ContainsKey(oid) ? ids[oid].AsGodotObject() as ItemDef : null;
        if (id == null)
            return false;
        return _arr_contains_str(id.get_tags(), "shield");
    }

    private static Godot.Collections.Array _extract_ally_member_ids(
        Godot.Collections.Dictionary ctx
    ) =>
        ctx.ContainsKey("ally_member_ids")
        && ctx["ally_member_ids"].VariantType == Variant.Type.Array
            ? ctx["ally_member_ids"].AsGodotArray()
            : new Godot.Collections.Array();

    private SkillDef _skill_def_from_runtime(StringName sid)
    {
        var sds = _runtime?.Call("get_skill_defs").AsGodotDictionary();
        return sds != null && sds.ContainsKey(sid) ? sds[sid].AsGodotObject() as SkillDef : null;
    }

    private AttributeSnapshot _build_member_attribute_snapshot(
        GodotObject ms,
        Godot.Collections.Dictionary ctx,
        EquipmentState ev = null
    )
    {
        var snap = new AttributeSnapshot();
        if (ms == null)
        {
            _seed_default_base_attributes(snap, ctx, "default_ally", 10);
            snap.set_value(
                AttributeService.HP_MAX,
                Mathf.Max(
                    ctx.ContainsKey("default_ally_hp") ? ctx["default_ally_hp"].AsInt32() : 24,
                    1
                )
            );
            snap.set_value(
                AttributeService.MP_MAX,
                Mathf.Max(
                    ctx.ContainsKey("default_ally_mp") ? ctx["default_ally_mp"].AsInt32() : 0,
                    0
                )
            );
            snap.set_value(
                AttributeService.STAMINA_MAX,
                Mathf.Max(
                    ctx.ContainsKey("default_ally_stamina")
                        ? ctx["default_ally_stamina"].AsInt32()
                        : 0,
                    0
                )
            );
            snap.set_value(
                AttributeService.AURA_MAX,
                Mathf.Max(
                    ctx.ContainsKey("default_ally_aura") ? ctx["default_ally_aura"].AsInt32() : 0,
                    0
                )
            );
            snap.set_value(
                AttributeService.ACTION_POINTS,
                Mathf.Max(
                    ctx.ContainsKey("default_ally_ap") ? ctx["default_ally_ap"].AsInt32() : 6,
                    1
                )
            );
            snap.set_value(
                AttributeService.ACTION_THRESHOLD,
                Mathf.Max(
                    ctx.ContainsKey("default_ally_action_threshold")
                        ? ctx["default_ally_action_threshold"].AsInt32()
                        : AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD_VALUE(),
                    1
                )
            );
            snap.set_value(
                AttributeService.ATTACK_BONUS,
                ctx.ContainsKey("default_ally_attack_bonus")
                    ? ctx["default_ally_attack_bonus"].AsInt32()
                    : 4
            );
            snap.set_value(
                AttributeService.ARMOR_AC_BONUS,
                ctx.ContainsKey("default_ally_armor_ac_bonus")
                    ? ctx["default_ally_armor_ac_bonus"].AsInt32()
                    : 0
            );
            snap.set_value(
                AttributeService.SHIELD_AC_BONUS,
                ctx.ContainsKey("default_ally_shield_ac_bonus")
                    ? ctx["default_ally_shield_ac_bonus"].AsInt32()
                    : 0
            );
            snap.set_value(
                AttributeService.DODGE_BONUS,
                ctx.ContainsKey("default_ally_dodge_bonus")
                    ? ctx["default_ally_dodge_bonus"].AsInt32()
                    : 0
            );
            snap.set_value(
                AttributeService.DEFLECTION_BONUS,
                ctx.ContainsKey("default_ally_deflection_bonus")
                    ? ctx["default_ally_deflection_bonus"].AsInt32()
                    : 0
            );
            snap.set_value(AttributeService.ARMOR_CLASS, _resolve_snapshot_armor_class(snap));
            return snap;
        }
        if (_runtime != null)
        {
            var cg = _runtime.Call("get_character_gateway").AsGodotObject();
            if (cg != null && cg.HasMethod("get_member_attribute_snapshot_for_equipment_view"))
            {
                var rs =
                    cg.Call(
                            "get_member_attribute_snapshot_for_equipment_view",
                            ms.Get("member_id").AsStringName(),
                            ev
                        )
                        .AsGodotObject() as AttributeSnapshot;
                if (rs != null)
                    return rs;
            }
        }
        var prog = ms.Get("progression").AsGodotObject() as UnitProgress;
        if (prog != null)
        {
            var asvc = new AttributeService();
            asvc.setup(
                prog,
                _runtime?.Call("get_skill_defs").AsGodotDictionary()
                    ?? new Godot.Collections.Dictionary(),
                default,
                new Godot.Collections.Array()
            );
            return asvc.get_snapshot();
        }
        _seed_default_base_attributes(snap, ctx, "default_ally", 10);
        snap.set_value(AttributeService.HP_MAX, Mathf.Max(ms.Get("current_hp").AsInt32(), 1));
        snap.set_value(AttributeService.MP_MAX, Mathf.Max(ms.Get("current_mp").AsInt32(), 0));
        snap.set_value(
            AttributeService.STAMINA_MAX,
            Mathf.Max(
                ctx.ContainsKey("default_ally_stamina") ? ctx["default_ally_stamina"].AsInt32() : 0,
                0
            )
        );
        snap.set_value(
            AttributeService.AURA_MAX,
            Mathf.Max(
                ctx.ContainsKey("default_ally_aura") ? ctx["default_ally_aura"].AsInt32() : 0,
                0
            )
        );
        snap.set_value(
            AttributeService.ACTION_POINTS,
            Mathf.Max(ctx.ContainsKey("default_ally_ap") ? ctx["default_ally_ap"].AsInt32() : 6, 1)
        );
        snap.set_value(
            AttributeService.ACTION_THRESHOLD,
            Mathf.Max(
                ctx.ContainsKey("default_ally_action_threshold")
                    ? ctx["default_ally_action_threshold"].AsInt32()
                    : AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD_VALUE(),
                1
            )
        );
        snap.set_value(
            AttributeService.ATTACK_BONUS,
            ctx.ContainsKey("default_ally_attack_bonus")
                ? ctx["default_ally_attack_bonus"].AsInt32()
                : 4
        );
        snap.set_value(
            AttributeService.ARMOR_AC_BONUS,
            ctx.ContainsKey("default_ally_armor_ac_bonus")
                ? ctx["default_ally_armor_ac_bonus"].AsInt32()
                : 0
        );
        snap.set_value(
            AttributeService.SHIELD_AC_BONUS,
            ctx.ContainsKey("default_ally_shield_ac_bonus")
                ? ctx["default_ally_shield_ac_bonus"].AsInt32()
                : 0
        );
        snap.set_value(
            AttributeService.DODGE_BONUS,
            ctx.ContainsKey("default_ally_dodge_bonus")
                ? ctx["default_ally_dodge_bonus"].AsInt32()
                : 0
        );
        snap.set_value(
            AttributeService.DEFLECTION_BONUS,
            ctx.ContainsKey("default_ally_deflection_bonus")
                ? ctx["default_ally_deflection_bonus"].AsInt32()
                : 0
        );
        snap.set_value(AttributeService.ARMOR_CLASS, _resolve_snapshot_armor_class(snap));
        return snap;
    }

    private static void _seed_default_base_attributes(
        AttributeSnapshot snap,
        Godot.Collections.Dictionary ctx,
        string kp,
        int dv
    )
    {
        if (snap == null)
            return;
        foreach (var a in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
        {
            string ak = $"{kp}_{(string)a}";
            snap.set_value(a, ctx.ContainsKey(ak) ? ctx[ak].AsInt32() : dv);
        }
    }

    private static int _resolve_snapshot_armor_class(AttributeSnapshot snap)
    {
        if (snap == null)
            return AttributeService.BASE_ARMOR_CLASS_VALUE();
        int t =
            AttributeService.BASE_ARMOR_CLASS_VALUE()
            + AttributeSnapshot.calculate_score_modifier(
                snap.get_value(UnitBaseAttributes.AGILITY())
            );
        foreach (var c in AttributeService.AC_COMPONENT_ATTRIBUTE_IDS)
            t += Mathf.Max(snap.get_value(c), 0);
        return Mathf.Clamp(t, 1, 99);
    }

    private void _apply_member_weapon_projection(
        BattleUnitState us,
        StringName mid,
        EquipmentState ev = null
    )
    {
        if (us == null)
            return;
        if ((string)mid == "" || _runtime == null)
        {
            us.clear_weapon_projection();
            return;
        }
        var cg = _runtime.Call("get_character_gateway").AsGodotObject();
        if (cg == null)
        {
            us.clear_weapon_projection();
            return;
        }
        if (cg.HasMethod("get_member_weapon_projection_for_equipment_view"))
        {
            var p = cg.Call("get_member_weapon_projection_for_equipment_view", mid, ev);
            us.apply_weapon_projection(
                p.VariantType == Variant.Type.Dictionary
                    ? p.AsGodotDictionary()
                    : new Godot.Collections.Dictionary()
            );
            return;
        }
        us.clear_weapon_projection();
    }

    private static void _apply_member_identity_projection(BattleUnitState us, GodotObject ms)
    {
        if (us == null)
            return;
        if (ms == null)
        {
            us.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_SMALL());
            us.versatility_pick = "";
            return;
        }
        var pc = ProgressionDataUtils.to_string_name(ms.Get("body_size_category"));
        if (!us.set_body_size_category(pc))
        {
            throw new InvalidOperationException(
                $"Member identity 的 body_size_category '{pc}' 非法。 " +
                $"合法值: tiny, small, medium, large, huge, gargantuan, boss"
            );
        }
        us.versatility_pick = ProgressionDataUtils.to_string_name(ms.Get("versatility_pick"));
    }

    private EquipmentState _ensure_unit_equipment_view(BattleUnitState us, GodotObject ms)
    {
        if (us == null)
            return new EquipmentState();
        if (!us.equipment_view_initialized)
            us.set_equipment_view(_get_member_equipment_state(ms));
        return us.get_equipment_view();
    }

    private static EquipmentState _get_member_equipment_state(GodotObject ms)
    {
        if (ms == null)
            return new EquipmentState();
        var memberState = ms as PartyMemberState;
        return memberState?.equipment_state ?? new EquipmentState();
    }

    private static void _aenwp(
        BattleUnitState us,
        StringName pt,
        StringName dt,
        int ar,
        StringName fam = default
    )
    {
        if (us == null)
            return;
        if (ar <= 0 && (string)dt == "")
        {
            us.clear_weapon_projection();
            return;
        }
        us.set_natural_weapon_projection(
            (string)pt != "" ? pt : "natural_weapon",
            dt,
            ar,
            new Godot.Collections.Dictionary(),
            fam
        );
    }

    private void _ensure_basic_attack_skill(BattleUnitState us)
    {
        if (us == null || !_runtime_has_skill(BASIC_ATTACK_SKILL_ID))
            return;
        if (!us.known_active_skill_ids.Contains(BASIC_ATTACK_SKILL_ID))
            us.known_active_skill_ids.Add(BASIC_ATTACK_SKILL_ID);
        us.known_skill_level_map[BASIC_ATTACK_SKILL_ID] = 0;
    }

    private void _ensure_enemy_basic_attack_affordability(BattleUnitState us)
    {
        if (us == null || !us.known_active_skill_ids.Contains(BASIC_ATTACK_SKILL_ID))
            return;
        var ba = _skill_def_from_runtime(BASIC_ATTACK_SKILL_ID);
        var cp = _csd(ba?.combat_profile);
        if (cp == null)
            return;
        int sl = Mathf.Max(
            us.known_skill_level_map.ContainsKey(BASIC_ATTACK_SKILL_ID)
                ? us.known_skill_level_map[BASIC_ATTACK_SKILL_ID].AsInt32()
                : 0,
            0
        );
        var costs = cp.get_effective_resource_costs(sl);
        int sc = Mathf.Max(
            costs.ContainsKey("stamina_cost") ? costs["stamina_cost"].AsInt32() : cp.stamina_cost,
            0
        );
        if (sc <= 0)
            return;
        if (_gv(us, AttributeService.STAMINA_MAX) < sc)
            _sv(us, AttributeService.STAMINA_MAX, sc);
        if (us.current_stamina < sc)
            us.current_stamina = sc;
    }

    private void _sync_unlocked_resources_from_progression(BattleUnitState us, GodotObject prog)
    {
        if (us == null)
            return;
        if (prog == null)
        {
            us.set_unlocked_combat_resource_ids(
                BattleUnitState.DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS()
            );
            return;
        }
        if (prog.HasMethod("sync_default_combat_resource_unlocks"))
            prog.Call("sync_default_combat_resource_unlocks");
        var rids = new Godot.Collections.Array<StringName>();
        foreach (var rv in prog.Get("unlocked_combat_resource_ids").AsGodotArray())
            rids.Add(ProgressionDataUtils.to_string_name(rv));
        us.set_unlocked_combat_resource_ids(rids);
    }

    private void _sync_enemy_unlocked_resources(BattleUnitState us)
    {
        if (us == null)
            return;
        us.sync_default_combat_resource_unlocks();
        var snap = _snap(us);
        int mM = snap != null ? snap.get_value(AttributeService.MP_MAX) : 0,
            aM = snap != null ? snap.get_value(AttributeService.AURA_MAX) : 0;
        if (us.current_mp > 0 || mM > 0)
            us.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        if (us.current_aura > 0 || aM > 0)
            us.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        foreach (var sid in us.known_active_skill_ids)
        {
            var sd = _skill_def_from_runtime(sid);
            var cp = _csd(sd?.combat_profile);
            if (cp == null)
                continue;
            int sl = Mathf.Max(
                us.known_skill_level_map.ContainsKey(sid)
                    ? us.known_skill_level_map[sid].AsInt32()
                    : 1,
                1
            );
            var costs = cp.get_effective_resource_costs(sl);
            if ((costs.ContainsKey("mp_cost") ? costs["mp_cost"].AsInt32() : 0) > 0)
                us.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
            if ((costs.ContainsKey("aura_cost") ? costs["aura_cost"].AsInt32() : 0) > 0)
                us.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        }
    }

    private bool _runtime_has_skill(StringName sid)
    {
        if ((string)sid == "" || _runtime == null)
            return false;
        var sds = _runtime.Call("get_skill_defs").AsGodotDictionary();
        return sds != null && sds.ContainsKey(sid);
    }

    private static bool _hedwc(Godot.Collections.Dictionary c) =>
        c.ContainsKey("default_enemy_weapon_attack_range")
        || c.ContainsKey("default_enemy_weapon_profile_type_id")
        || c.ContainsKey("default_enemy_weapon_physical_damage_tag");

    private static int _resolve_action_threshold_from_snapshot(AttributeSnapshot snap, int fb = -1)
    {
        int f = fb >= 0 ? fb : BattleUnitState.DEFAULT_ACTION_THRESHOLD();
        if (snap != null && snap.has_value(AttributeService.ACTION_THRESHOLD))
        {
            int s = snap.get_value(AttributeService.ACTION_THRESHOLD);
            if (s > 0)
                return s;
        }
        return f;
    }

    private Godot.Collections.Array<StringName> _collect_known_active_skill_ids(GodotObject prog)
    {
        var r = new Godot.Collections.Array<StringName>();
        if (prog == null)
            return r;
        foreach (
            var sk in ProgressionDataUtils.sorted_string_keys(
                prog.Get("skills").AsGodotDictionary()
            )
        )
        {
            var sid = new StringName(sk);
            var sp = prog.Call("get_skill_progress", sid);
            if (sp.VariantType == Variant.Type.Nil)
                continue;
            var sd = _skill_def_from_runtime(sid);
            if (sd == null || !sp.AsGodotObject().Get("is_learned").AsBool())
                continue;
            if (sd.skill_type != "active" || !sd.can_use_in_combat())
                continue;
            r.Add(sid);
        }
        return r;
    }

    private Godot.Collections.Dictionary _collect_known_skill_level_map(GodotObject prog)
    {
        var r = new Godot.Collections.Dictionary();
        if (prog == null)
            return r;
        foreach (
            var sk in ProgressionDataUtils.sorted_string_keys(
                prog.Get("skills").AsGodotDictionary()
            )
        )
        {
            var sid = new StringName(sk);
            var sp = prog.Call("get_skill_progress", sid);
            if (sp.VariantType == Variant.Type.Nil)
                continue;
            var sd = _skill_def_from_runtime(sid);
            if (sd == null || !sp.AsGodotObject().Get("is_learned").AsBool())
                continue;
            if (sd.skill_type != "active")
                continue;
            r[sid] = sp.AsGodotObject().Get("skill_level").AsInt32();
        }
        return r;
    }

    private Godot.Collections.Dictionary _collect_known_skill_lock_hit_bonus_map(GodotObject prog)
    {
        var r = new Godot.Collections.Dictionary();
        if (prog == null)
            return r;
        foreach (
            var sk in ProgressionDataUtils.sorted_string_keys(
                prog.Get("skills").AsGodotDictionary()
            )
        )
        {
            var sid = new StringName(sk);
            var sp = prog.Call("get_skill_progress", sid).AsGodotObject();
            var sd = _skill_def_from_runtime(sid);
            if (
                sp == null
                || sd == null
                || !sp.Get("is_learned").AsBool()
                || !sp.Get("is_level_trigger_locked").AsBool()
            )
                continue;
            int b = sp.Get("bonus_to_hit_from_lock").AsInt32();
            if (b <= 0)
                continue;
            r[sid] = b;
        }
        return r;
    }

    private void _sync_passive_battle_statuses(
        BattleUnitState us,
        GodotObject prog,
        GodotObject ms = null
    )
    {
        if (us == null)
            return;
        PassiveSourceContext ctx = null;
        var cg = _runtime?.Call("get_character_gateway").AsGodotObject();
        if (
            cg != null
            && (string)us.source_member_id != ""
            && cg.HasMethod("build_passive_source_context")
        )
            ctx =
                cg.Call("build_passive_source_context", us.source_member_id, prog).AsGodotObject()
                as PassiveSourceContext;
        if (ctx == null)
        {
            ctx = new PassiveSourceContext
            {
                member_state = ms as PartyMemberState,
                unit_progress = prog as UnitProgress,
            };
            if (ctx.unit_progress != null)
                ctx.skill_progress_by_id = ctx.unit_progress.skills;
        }
        PassiveStatusOrchestrator.apply_to_unit(
            us,
            ctx,
            _runtime?.Call("get_skill_defs").AsGodotDictionary()
                ?? new Godot.Collections.Dictionary()
        );
    }

    private static Godot.Collections.Array<StringName> _extract_movement_tags(object rawTags)
    {
        var t = new Godot.Collections.Array<StringName>();
        Godot.Collections.Array values = null;
        if (rawTags is Variant rt && rt.VariantType == Variant.Type.Array)
        {
            values = rt.AsGodotArray();
        }
        else if (rawTags is Godot.Collections.Array arrayTags)
        {
            values = arrayTags;
        }
        if (values == null)
            return t;
        foreach (var rv in values)
        {
            var n = ProgressionDataUtils.to_string_name(rv);
            if ((string)n == "" || t.Contains(n))
                continue;
            t.Add(n);
        }
        return t;
    }

    private static bool _arr_contains_str(Godot.Collections.Array<StringName> a, StringName v)
    {
        if (a == null)
            return false;
        foreach (var i in a)
            if (ProgressionDataUtils.to_string_name(i) == v)
                return true;
        return false;
    }
}
