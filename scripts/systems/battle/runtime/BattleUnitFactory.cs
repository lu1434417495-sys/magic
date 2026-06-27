using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

internal sealed class BattleUnitFactory
{
    private static readonly StringName BASIC_ATTACK_SKILL_ID = "basic_attack";
    private static readonly StringName DEFAULT_ENEMY_MELEE_DAMAGE_TAG = "physical_slash";
    private BattleRuntimeModule _runtime;

    private static AttributeSnapshot _snap(BattleUnitState us) =>
        us?.attribute_snapshot as AttributeSnapshot;

    private static int _gv(BattleUnitState us, StringName k) => _snap(us)?.GetValue(k) ?? 0;

    private static void _sv(BattleUnitState us, StringName k, int v)
    {
        var s = _snap(us);
        if (s != null)
            s.SetValue(k, v);
    }

    private sealed class AllyUnitDefaults
    {
        public IReadOnlyDictionary<StringName, int> BaseAttributes { get; }
        public int HpMax { get; }
        public int MpMax { get; }
        public int StaminaMax { get; }
        public int AuraMax { get; }
        public int ActionPoints { get; }
        public int ActionThreshold { get; }
        public int AttackBonus { get; }
        public int ArmorAcBonus { get; }
        public int ShieldAcBonus { get; }
        public int DodgeBonus { get; }
        public int DeflectionBonus { get; }

        public AllyUnitDefaults(Godot.Collections.Dictionary context)
        {
            BaseAttributes = ReadBaseAttributeDefaults(context, "default_ally", 10);
            HpMax = ReadInt(context, "default_ally_hp", 24, 1);
            MpMax = ReadInt(context, "default_ally_mp", 0, 0);
            StaminaMax = ReadInt(context, "default_ally_stamina", 0, 0);
            AuraMax = ReadInt(context, "default_ally_aura", 0, 0);
            ActionPoints = ReadInt(context, "default_ally_ap", 6, 1);
            ActionThreshold = ReadInt(
                context,
                "default_ally_action_threshold",
                AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD,
                1
            );
            AttackBonus = ReadInt(context, "default_ally_attack_bonus", 4);
            ArmorAcBonus = ReadInt(context, "default_ally_armor_ac_bonus", 0);
            ShieldAcBonus = ReadInt(context, "default_ally_shield_ac_bonus", 0);
            DodgeBonus = ReadInt(context, "default_ally_dodge_bonus", 0);
            DeflectionBonus = ReadInt(context, "default_ally_deflection_bonus", 0);
        }
    }

    private sealed class EnemyUnitDefaults
    {
        public IReadOnlyDictionary<StringName, int> BaseAttributes { get; }
        public int HpMax { get; }
        public int MpMax { get; }
        public int StaminaMax { get; }
        public int ActionPoints { get; }
        public int AttackBonus { get; }
        public int ArmorAcBonus { get; }
        public int ShieldAcBonus { get; }
        public int DodgeBonus { get; }
        public int DeflectionBonus { get; }
        public int SpellProficiencyBonus { get; }
        public int ActionThreshold { get; }
        public EnemyWeaponDefaults Weapon { get; }

        public EnemyUnitDefaults(Godot.Collections.Dictionary context)
        {
            BaseAttributes = ReadBaseAttributeDefaults(context, "default_enemy", 4);
            HpMax = ReadInt(context, "default_enemy_hp", 12, 1);
            MpMax = ReadInt(context, "default_enemy_mp", 0, 0);
            StaminaMax = ReadInt(context, "default_enemy_stamina", 0, 0);
            ActionPoints = ReadInt(context, "default_enemy_ap", 1, 1);
            AttackBonus = ReadInt(context, "default_enemy_attack_bonus", 4);
            ArmorAcBonus = ReadInt(context, "default_enemy_armor_ac_bonus", 0);
            ShieldAcBonus = ReadInt(context, "default_enemy_shield_ac_bonus", 0);
            DodgeBonus = ReadInt(context, "default_enemy_dodge_bonus", 0);
            DeflectionBonus = ReadInt(context, "default_enemy_deflection_bonus", 0);
            int characterLevel = ReadInt(context, "default_enemy_character_level", 0);
            SpellProficiencyBonus = context != null
                && context.ContainsKey("default_enemy_spell_proficiency_bonus")
                ? ReadInt(context, "default_enemy_spell_proficiency_bonus", 0)
                : AttributeSnapshot.CalculateSpellProficiencyBonus(characterLevel);
            ActionThreshold = ReadInt(
                context,
                "default_enemy_action_threshold",
                BattleUnitState.DefaultActionThreshold
            );
            Weapon = new EnemyWeaponDefaults(context);
        }
    }

    private sealed class EnemyWeaponDefaults
    {
        public bool HasExplicitProjection { get; }
        public int AttackRange { get; }
        public StringName ProfileTypeId { get; }
        public StringName PhysicalDamageTag { get; }
        public StringName WeaponFamily { get; }

        public EnemyWeaponDefaults(Godot.Collections.Dictionary context)
        {
            HasExplicitProjection =
                context != null
                && (
                    context.ContainsKey("default_enemy_weapon_attack_range")
                    || context.ContainsKey("default_enemy_weapon_profile_type_id")
                    || context.ContainsKey("default_enemy_weapon_physical_damage_tag")
                );
            AttackRange = ReadInt(context, "default_enemy_weapon_attack_range", 1, 0);
            ProfileTypeId = ReadStringName(
                context,
                "default_enemy_weapon_profile_type_id",
                "natural_weapon"
            );
            PhysicalDamageTag = ReadStringName(
                context,
                "default_enemy_weapon_physical_damage_tag",
                DEFAULT_ENEMY_MELEE_DAMAGE_TAG
            );
            WeaponFamily = ReadStringName(context, "default_enemy_weapon_family", "");
        }
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
    }

    private IBattleRuntimeCharacterGateway GetCharacterGateway() =>
        _runtime?.GetCharacterGatewayTyped();

    private IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionIndex() =>
        _runtime?.GetSkillDefinitionIndexTyped() ?? new Dictionary<StringName, SkillDefinition>();

    private BattleTerrainGenerator GetTerrainGenerator() => _runtime?.GetTerrainGenerator();

    private Dictionary<StringName, ItemDef> BuildItemDefIndexSnapshotWithGatewayItem(
        StringName itemId
    )
    {
        Dictionary<StringName, ItemDef> itemDefs =
            _runtime?.BuildItemDefIndexSnapshotTyped() ?? new Dictionary<StringName, ItemDef>();
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId == "")
        {
            return itemDefs;
        }
        ItemDef gatewayItemDef = GetCharacterGateway()?.GetItemDef(normalizedItemId);
        if (gatewayItemDef == null)
        {
            return itemDefs;
        }
        itemDefs[normalizedItemId] = gatewayItemDef;
        StringName defItemId = ProgressionDataUtils.to_string_name(gatewayItemDef.item_id);
        if (defItemId != "")
        {
            itemDefs[defItemId] = gatewayItemDef;
        }
        return itemDefs;
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        IBattleRuntimeCharacterGateway gateway = GetCharacterGateway();
        if (gateway != null)
            return gateway.GetMemberState(memberId);
        return null;
    }

    private AttributeSnapshot GetMemberAttributeSnapshot(
        StringName memberId,
        EquipmentState equipmentView
    )
    {
        IBattleRuntimeCharacterGateway gateway = GetCharacterGateway();
        if (gateway != null)
            return gateway.GetMemberAttributeSnapshotForEquipmentView(memberId, equipmentView);
        return null;
    }

    private WeaponProjection GetMemberWeaponProjectionTyped(
        StringName memberId,
        EquipmentState equipmentView
    )
    {
        IBattleRuntimeCharacterGateway gateway = GetCharacterGateway();
        if (gateway != null)
            return gateway.GetMemberWeaponProjectionForEquipmentViewTyped(memberId, equipmentView)
            ?? new WeaponProjection();
        return new WeaponProjection();
    }

    private BattleEffectiveTraitProjection GetMemberEffectiveTraitProjection(
        StringName memberId,
        EquipmentState equipmentView
    )
    {
        IBattleRuntimeCharacterGateway gateway = GetCharacterGateway();
        return gateway?.BuildEffectiveTraitProjectionForEquipmentView(memberId, equipmentView)
            ?? BattleEffectiveTraitProjection.Empty;
    }

    private PassiveSourceContext BuildPassiveSourceContext(StringName memberId, UnitProgress progression)
    {
        IBattleRuntimeCharacterGateway gateway = GetCharacterGateway();
        return gateway?.BuildPassiveSourceContext(memberId, progression);
    }

    internal IReadOnlyList<BattleUnitState> BuildAllyUnits(
        PartyState party_state,
        Godot.Collections.Dictionary context,
        GodotTransientResourceScope owner = null
    )
    {
        GodotTransientResourceScope localScope = null;
        GodotTransientResourceScope contextScope = owner;
        if (contextScope == null)
        {
            localScope = new GodotTransientResourceScope("BattleUnitFactory.BuildAllyUnits");
            contextScope = localScope;
        }

        using (localScope)
        {
            context = contextScope.OwnWrapper(
                context ?? new Godot.Collections.Dictionary(),
                "context"
            );
            Godot.Collections.Array battleParty = ReadArray(context, "battle_party");
            if (battleParty.Count > 0)
            {
                return _normalize_unit_payloads(battleParty);
            }
            var member_ids = new Godot.Collections.Array();
            if (party_state?.active_member_ids != null && party_state.active_member_ids.Count > 0)
            {
                foreach (var memberId in party_state.active_member_ids)
                    member_ids.Add(memberId);
            }
            if (member_ids.Count == 0)
                member_ids = _extract_ally_member_ids(context);
            var units = new List<BattleUnitState>();
            for (int i = 0; i < member_ids.Count; i++)
            {
                var mid = ProgressionDataUtils.to_string_name(member_ids[i]);
                var ms = party_state?.GetMemberState(mid);
                if (ms != null && ms.progression == null)
                    continue;
                var us = _build_runtime_ally_unit(mid, ms, i, context);
                if (us != null)
                    units.Add(us);
            }
            return units;
        }
    }

    internal void RefreshBattleUnit(BattleUnitState us)
    {
        if (us == null || (string)us.source_member_id == "" || _runtime == null)
            return;
        PartyMemberState ms = GetMemberState(us.source_member_id);
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
        _apply_member_effective_trait_projection(us, us.source_member_id, ev);
        RefreshWeaponProjection(us);
        int hpMax = Mathf.Max(snap.GetValue(AttributeService.HP_MAX), 1);
        int mpMax = Mathf.Max(snap.GetValue(AttributeService.MP_MAX), 0);
        int stamMax = Mathf.Max(snap.GetValue(AttributeService.STAMINA_MAX), 0);
        int auraMax = Mathf.Max(snap.GetValue(AttributeService.AURA_MAX), 0);
        int apMax = Mathf.Max(snap.GetValue(AttributeService.ACTION_POINTS), 1);
        us.ClampCombatResources(
            new BattleResourceCaps(
                hpMax,
                mpMax,
                stamMax,
                auraMax,
                apMax,
                BattleUnitState.DefaultMovePointsPerTurn
            )
        );
        us.action_threshold = _resolve_action_threshold_from_snapshot(snap);
        UnitProgress prog = ms.progression;
        us.SetKnownActiveSkillIds(_collect_known_active_skill_ids(prog));
        us.SetKnownSkillLevelsTyped(_collect_known_skill_level_map(prog));
        us.SetKnownSkillLockHitBonusesTyped(_collect_known_skill_lock_hit_bonus_map(prog));
        _sync_unlocked_resources_from_progression(us, prog);
        _filter_skills_by_equipment_requirements(us);
        _ensure_basic_attack_skill(us);
        _sync_passive_battle_statuses(us, prog, ms);
        us.RefreshFootprint();
    }

    internal void RefreshKnownSkills(BattleUnitState us)
    {
        if (us == null || (string)us.source_member_id == "" || _runtime == null)
            return;
        PartyMemberState ms = GetMemberState(us.source_member_id);
        if (ms == null)
            return;
        UnitProgress prog = ms.progression;
        us.SetKnownActiveSkillIds(_collect_known_active_skill_ids(prog));
        us.SetKnownSkillLevelsTyped(_collect_known_skill_level_map(prog));
        us.SetKnownSkillLockHitBonusesTyped(_collect_known_skill_lock_hit_bonus_map(prog));
        _sync_unlocked_resources_from_progression(us, prog);
        _filter_skills_by_equipment_requirements(us);
        _ensure_basic_attack_skill(us);
        _sync_passive_battle_statuses(us, prog, ms);
    }

    internal void RefreshWeaponProjection(BattleUnitState us)
    {
        if (us == null)
            return;
        _apply_member_weapon_projection(us, us.source_member_id, us.GetEquipmentView());
    }

    internal void RefreshEquipmentProjection(BattleUnitState us)
    {
        if (us == null || (string)us.source_member_id == "" || _runtime == null)
            return;
        PartyMemberState ms = GetMemberState(us.source_member_id);
        if (ms == null)
            return;
        var snap =
            _build_member_attribute_snapshot(
                ms,
                new Godot.Collections.Dictionary(),
                us.GetEquipmentView()
            ) ?? new AttributeSnapshot();
        var prevSnap = _snap(us);
        int prevHpMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.GetValue(AttributeService.HP_MAX), 1)
                : Mathf.Max(snap.GetValue(AttributeService.HP_MAX), 1);
        int prevMpMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.GetValue(AttributeService.MP_MAX), 0)
                : Mathf.Max(snap.GetValue(AttributeService.MP_MAX), 0);
        int prevStamMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.GetValue(AttributeService.STAMINA_MAX), 0)
                : Mathf.Max(snap.GetValue(AttributeService.STAMINA_MAX), 0);
        int prevAuraMax =
            prevSnap != null
                ? Mathf.Max(prevSnap.GetValue(AttributeService.AURA_MAX), 0)
                : Mathf.Max(snap.GetValue(AttributeService.AURA_MAX), 0);
        List<BattleEffectiveTraitInstanceState> previousEffectiveTraitInstances = BattleUnitState.DuplicateEffectiveTraitInstances(
            us.effective_trait_instances
        );
        us.attribute_snapshot = snap;
        _apply_member_effective_trait_projection(us, us.source_member_id, us.GetEquipmentView());
        TraitTriggerHooks.ReconcileChargesAfterEffectiveTraitProjection(
            us,
            previousEffectiveTraitInstances
        );
        RefreshWeaponProjection(us);
        int hpMax = Mathf.Max(snap.GetValue(AttributeService.HP_MAX), 1),
            mpMax = Mathf.Max(snap.GetValue(AttributeService.MP_MAX), 0);
        int stamMax = Mathf.Max(snap.GetValue(AttributeService.STAMINA_MAX), 0),
            auraMax = Mathf.Max(snap.GetValue(AttributeService.AURA_MAX), 0);
        us.SetCurrentHp(hpMax < prevHpMax ? Mathf.Clamp(us.current_hp, 0, hpMax) : us.current_hp);
        us.SetCurrentMp(mpMax < prevMpMax ? Mathf.Clamp(us.current_mp, 0, mpMax) : us.current_mp);
        us.SetCurrentStamina(
            stamMax < prevStamMax ? Mathf.Clamp(us.current_stamina, 0, stamMax) : us.current_stamina
        );
        us.SetCurrentAura(auraMax < prevAuraMax ? Mathf.Clamp(us.current_aura, 0, auraMax) : us.current_aura);
        us.action_threshold = _resolve_action_threshold_from_snapshot(snap);
        UnitProgress prog = ms.progression;
        us.SetKnownActiveSkillIds(_collect_known_active_skill_ids(prog));
        us.SetKnownSkillLevelsTyped(_collect_known_skill_level_map(prog));
        us.SetKnownSkillLockHitBonusesTyped(_collect_known_skill_lock_hit_bonus_map(prog));
        _sync_unlocked_resources_from_progression(us, prog);
        _filter_skills_by_equipment_requirements(us);
        _ensure_basic_attack_skill(us);
        _sync_passive_battle_statuses(us, prog, ms);
        us.RefreshFootprint();
    }

    internal IReadOnlyList<BattleUnitState> BuildEnemyUnits(
        EncounterAnchorData enc,
        Godot.Collections.Dictionary ctx,
        GodotTransientResourceScope owner = null
    )
    {
        GodotTransientResourceScope localScope = null;
        GodotTransientResourceScope contextScope = owner;
        if (contextScope == null)
        {
            localScope = new GodotTransientResourceScope("BattleUnitFactory.BuildEnemyUnits");
            contextScope = localScope;
        }

        using (localScope)
        {
            ctx = contextScope.OwnWrapper(
                ctx ?? new Godot.Collections.Dictionary(),
                "context"
            );
            Godot.Collections.Array enemyUnits = ReadArray(ctx, "enemy_units");
            if (enemyUnits.Count > 0)
            {
                return _normalize_unit_payloads(enemyUnits);
            }
            var aid = enc != null ? (string)enc.entity_id : "unknown";
            GameLog.Warning($"BattleUnitFactory cannot build fallback enemy units for {aid}.", "battle.factory.fallback_failed", "battle");
            return Array.Empty<BattleUnitState>();
        }
    }

    private List<BattleUnitState> _normalize_unit_payloads(Godot.Collections.Array pl)
    {
        var r = new List<BattleUnitState>();
        foreach (var v in pl)
        {
            if (BattleUnitState.TryReadUnitPayload(v, out BattleUnitState bs) && bs != null)
            {
                r.Add(bs.clone());
                continue;
            }
        }
        return r;
    }

    internal Godot.Collections.Dictionary BuildTerrainData(
        EncounterAnchorData enc,
        long seed,
        Godot.Collections.Dictionary ctx,
        GodotTransientResourceScope owner
    )
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var tc = owner.NewDictionary("terrain-context");
        CopyTerrainContext(ctx, tc);
        tc.Remove("map_size");
        BattleTerrainGenerator terrainGenerator = GetTerrainGenerator();
        if (terrainGenerator != null)
        {
            Godot.Collections.Dictionary generatedTerrain = terrainGenerator.GenerateTyped(enc, seed, tc);
            Godot.Collections.Dictionary ownedTerrain = owner.OwnWrapper(
                generatedTerrain,
                "terrain-generator-output"
            );
            Godot.Collections.Dictionary result = _atgo(ownedTerrain, tc, owner);
            return result;
        }
        return _atgo(owner.NewDictionary("empty-terrain-generator-output"), tc, owner);
    }

    private static Godot.Collections.Dictionary _atgo(
        Godot.Collections.Dictionary td,
        Godot.Collections.Dictionary ctx,
        GodotTransientResourceScope owner
    )
    {
        if (td == null || td.Count == 0)
            return owner.NewDictionary("terrain-output-empty");
        var tr = RuntimePayloadCopy.Dictionary(td, "BattleUnitFactory._atgo.terrain");
        Godot.Collections.Array allySpawns = ReadArray(ctx, "ally_spawns");
        if (allySpawns.Count > 0)
            tr["ally_spawns"] = RuntimePayloadCopy.Array(
                allySpawns,
                "BattleUnitFactory._atgo.ally_spawns"
            );
        Godot.Collections.Array enemySpawns = ReadArray(ctx, "enemy_spawns");
        if (enemySpawns.Count > 0)
            tr["enemy_spawns"] = RuntimePayloadCopy.Array(
                enemySpawns,
                "BattleUnitFactory._atgo.enemy_spawns"
            );
        return owner.OwnWrapper(tr, "terrain-output");
    }

    private static void CopyTerrainContext(
        Godot.Collections.Dictionary source,
        Godot.Collections.Dictionary target
    )
    {
        if (source == null || target == null)
            return;
        CopyTerrainContextValue(source, target, "world_coord");
        CopyTerrainContextValue(source, target, "action_points");
        CopyTerrainContextValue(source, target, "battle_terrain_profile");
        CopyTerrainContextValue(source, target, "battle_map_size");
        CopyTerrainContextValue(source, target, "battle_test_vertical_slice");
        CopyTerrainContextValue(source, target, "ally_spawns");
        CopyTerrainContextValue(source, target, "enemy_spawns");
    }

    private static void CopyTerrainContextValue(
        Godot.Collections.Dictionary source,
        Godot.Collections.Dictionary target,
        string key
    )
    {
        if (!source.ContainsKey(key))
            return;
        target[key] = DuplicateTerrainContextVariant(source[key]);
    }

    private static Variant DuplicateTerrainContextVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary => RuntimePayloadCopy.CopyVariant(
                value,
                "BattleUnitFactory.CopyTerrainContextValue.dictionary"
            ),
            Variant.Type.Array => RuntimePayloadCopy.CopyVariant(
                value,
                "BattleUnitFactory.CopyTerrainContextValue.array"
            ),
            _ => value,
        };
    }

    private BattleUnitState _build_runtime_ally_unit(
        StringName mid,
        PartyMemberState ms,
        int idx,
        Godot.Collections.Dictionary ctx
    )
    {
        AllyUnitDefaults defaults = new(ctx);
        var us = new BattleUnitState();
        us.unit_id = (string)mid != "" ? mid : $"ally_{idx + 1}";
        us.source_member_id = mid;
        us.display_name =
            ms != null && !string.IsNullOrEmpty(ms.display_name)
                ? ms.display_name
                : $"队员{idx + 1}";
        us.faction_id = "player";
        us.ControlModeKind = ms != null ? ms.ControlModeKind : BattleUnitControlMode.Manual;
        _apply_member_identity_projection(us, ms);
        us.SetEquipmentView(_get_member_equipment_state(ms));
        var snap = _build_member_attribute_snapshot(ms, ctx, us.GetEquipmentView());
        us.attribute_snapshot = snap;
        _apply_member_weapon_projection(us, mid, us.GetEquipmentView());
        _apply_member_effective_trait_projection(us, mid, us.GetEquipmentView());
        int hpMax = Mathf.Max(snap.GetValue(AttributeService.HP_MAX), 1),
            mpMax = Mathf.Max(snap.GetValue(AttributeService.MP_MAX), 0);
        int stamMax = Mathf.Max(snap.GetValue(AttributeService.STAMINA_MAX), 0),
            auraMax = Mathf.Max(snap.GetValue(AttributeService.AURA_MAX), 0);
        int ap = Mathf.Max(snap.GetValue(AttributeService.ACTION_POINTS), 1);
        us.SetCombatResources(
            Mathf.Clamp(ms != null ? ms.current_hp : hpMax, 0, hpMax),
            Mathf.Clamp(ms != null ? ms.current_mp : mpMax, 0, mpMax),
            stamMax,
            Mathf.Clamp(
                ms != null ? ms.current_aura : auraMax,
                0,
                auraMax
            ),
            ap,
            BattleUnitState.DefaultMovePointsPerTurn
        );
        us.action_threshold = _resolve_action_threshold_from_snapshot(snap, defaults.ActionThreshold);
        UnitProgress prog = ms?.progression;
        us.SetKnownActiveSkillIds(_collect_known_active_skill_ids(prog));
        us.SetKnownSkillLevelsTyped(_collect_known_skill_level_map(prog));
        us.SetKnownSkillLockHitBonusesTyped(_collect_known_skill_lock_hit_bonus_map(prog));
        _sync_unlocked_resources_from_progression(us, prog);
        _sync_passive_battle_statuses(us, prog, ms);
        _filter_skills_by_equipment_requirements(us);
        us.movement_tags = _extract_movement_tags(ReadArray(ctx, "ally_movement_tags"));
        Godot.Collections.Array defaultActiveSkillIds = ReadArray(ctx, "default_active_skill_ids");
        if (us.known_active_skill_ids.Count == 0 && defaultActiveSkillIds.Count > 0)
            foreach (var sv in defaultActiveSkillIds)
            {
                var ns = ProgressionDataUtils.to_string_name(sv);
                us.AddKnownActiveSkill(ns);
                us.SetKnownSkillLevelTyped(ns, 1);
            }
        _ensure_basic_attack_skill(us);
        return us;
    }

    private BattleUnitState _build_runtime_enemy_unit(
        EncounterAnchorData enc,
        string mn,
        int idx,
        Godot.Collections.Dictionary ctx
    )
    {
        EnemyUnitDefaults defaults = new(ctx);
        var us = new BattleUnitState();
        var aid = enc != null ? (string)enc.entity_id : "wild";
        us.unit_id = $"{aid}_{(idx + 1):D2}";
        us.source_member_id = "";
        us.display_name = idx == 0 ? mn : $"{mn}·从属{idx + 1}";
        us.faction_id =
            enc != null && (string)enc.faction_id != ""
                ? enc.faction_id
                : "hostile";
        us.ControlModeKind = BattleUnitControlMode.Ai;
        us.SetBodySizeCategory(BodySizeContentRules.ToStringName(BodySizeCategoryKind.Medium));
        int hpMax = defaults.HpMax;
        int mpMax = defaults.MpMax;
        int stamMax = defaults.StaminaMax;
        int ap = defaults.ActionPoints;
        ApplyBaseAttributeDefaults(us, defaults.BaseAttributes);
        _sv(us, "hp_max", hpMax);
        _sv(us, "mp_max", mpMax);
        _sv(us, "stamina_max", stamMax);
        _sv(us, "action_points", ap);
        _sv(us, AttributeService.ATTACK_BONUS, defaults.AttackBonus);
        _sv(us, AttributeService.ARMOR_AC_BONUS, defaults.ArmorAcBonus);
        _sv(us, AttributeService.SHIELD_AC_BONUS, defaults.ShieldAcBonus);
        _sv(us, AttributeService.DODGE_BONUS, defaults.DodgeBonus);
        _sv(us, AttributeService.DEFLECTION_BONUS, defaults.DeflectionBonus);
        _sv(us, AttributeService.ARMOR_CLASS, _resolve_snapshot_armor_class(_snap(us)));
        _sv(us, AttributeService.SPELL_PROFICIENCY_BONUS, defaults.SpellProficiencyBonus);
        if (defaults.Weapon.HasExplicitProjection)
        {
            _aenwp(
                us,
                defaults.Weapon.ProfileTypeId,
                defaults.Weapon.PhysicalDamageTag,
                defaults.Weapon.AttackRange,
                defaults.Weapon.WeaponFamily
            );
        }
        else
            us.SetUnarmedWeaponProjection();
        us.action_threshold = defaults.ActionThreshold;
        us.SetCombatResources(hpMax, mpMax, stamMax, us.current_aura, ap, BattleUnitState.DefaultMovePointsPerTurn);
        us.movement_tags = _extract_movement_tags(ReadArray(ctx, "enemy_movement_tags"));
        Godot.Collections.Array enemySkillIds = ReadArray(ctx, "enemy_skill_ids");
        if (enemySkillIds.Count > 0)
        {
            var configuredSkillIds = new List<StringName>();
            foreach (var sv in enemySkillIds)
            {
                var ns = ProgressionDataUtils.to_string_name(sv);
                configuredSkillIds.Add(ns);
            }
            us.SetKnownActiveSkillIds(configuredSkillIds);
            foreach (StringName ns in configuredSkillIds)
                us.SetKnownSkillLevelTyped(ns, 1);
        }
        if (us.known_active_skill_ids.Count == 0)
        {
            us.SetKnownActiveSkillIds(_pick_default_enemy_skill_ids());
            foreach (var s in us.known_active_skill_ids)
                us.SetKnownSkillLevelTyped(s, 1);
        }
        _ensure_basic_attack_skill(us);
        _ensure_enemy_basic_attack_affordability(us);
        _sync_enemy_unlocked_resources(us);
        return us;
    }

    private StringNameList _pick_default_enemy_skill_ids()
    {
        var pre = new StringNameList
        {
            BASIC_ATTACK_SKILL_ID,
            "warrior_heavy_strike",
            "warrior_combo_strike",
            "warrior_guard_break",
        };
        foreach (var p in pre)
            if (_is_valid_enemy_skill(_skill_definition_from_runtime(p)))
                return new StringNameList { p };
        var sortedSkillIds = new List<StringName>(GetSkillDefinitionIndex().Keys);
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        foreach (var sid in sortedSkillIds)
        {
            if (_is_valid_enemy_skill(_skill_definition_from_runtime(sid)))
                return new StringNameList { sid };
        }
        return new StringNameList();
    }

    private bool _is_valid_enemy_skill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return skillDefinition != null
            && skillDefinition.SkillTypeKind == SkillTypeKind.Active
            && skillDefinition.CanUseInCombat()
            && combatProfile != null
            && combatProfile.TargetModeKind == BattleTargetMode.Unit
            && BattleTargetTeamRules.IsEnemyFilter(combatProfile.TargetTeamFilter);
    }

    private void _filter_skills_by_equipment_requirements(BattleUnitState us)
    {
        if (us == null)
            return;
        var f = new StringNameList();
        foreach (var sid in us.known_active_skill_ids)
        {
            SkillDefinition skillDefinition = _skill_definition_from_runtime(sid);
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile == null)
                continue;
            if (combatProfile.RequiresEquippedShield && !_unit_has_equipped_shield(us))
                continue;
            if (
                !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                    us,
                    combatProfile.RequiredWeaponFamilies
                )
            )
                continue;
            if (
                BattleRangeService.RequiresCurrentMeleeWeapon(skillDefinition)
                && !BattleRangeService.UnitHasMeleeWeapon(us)
            )
                continue;
            f.Add(sid);
        }
        us.SetKnownActiveSkillIds(f);
    }

    private bool _unit_has_equipped_shield(BattleUnitState us)
    {
        var ev = us?.GetEquipmentView();
        if (ev == null)
            return false;
        var oid = ev.GetEquippedItemId(EquipmentRules.ToStringName(EquipmentSlotKind.OffHand));
        return BattleEquipmentRequirementRules.UnitHasEquippedShield(
            us,
            BuildItemDefIndexSnapshotWithGatewayItem(oid)
        );
    }

    private static Godot.Collections.Array _extract_ally_member_ids(
        Godot.Collections.Dictionary ctx
    ) => ReadArray(ctx, "ally_member_ids");

    private SkillDefinition _skill_definition_from_runtime(StringName sid)
    {
        var skillDefinitions = GetSkillDefinitionIndex();
        return skillDefinitions.TryGetValue(sid, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private AttributeSnapshot _build_member_attribute_snapshot(
        PartyMemberState ms,
        Godot.Collections.Dictionary ctx,
        EquipmentState ev = null
    )
    {
        AllyUnitDefaults defaults = new(ctx);
        var snap = new AttributeSnapshot();
        if (ms == null)
        {
            ApplyBaseAttributeDefaults(snap, defaults.BaseAttributes);
            snap.SetValue(AttributeService.HP_MAX, defaults.HpMax);
            snap.SetValue(AttributeService.MP_MAX, defaults.MpMax);
            snap.SetValue(AttributeService.STAMINA_MAX, defaults.StaminaMax);
            snap.SetValue(AttributeService.AURA_MAX, defaults.AuraMax);
            snap.SetValue(AttributeService.ACTION_POINTS, defaults.ActionPoints);
            snap.SetValue(AttributeService.ACTION_THRESHOLD, defaults.ActionThreshold);
            snap.SetValue(AttributeService.ATTACK_BONUS, defaults.AttackBonus);
            snap.SetValue(AttributeService.ARMOR_AC_BONUS, defaults.ArmorAcBonus);
            snap.SetValue(AttributeService.SHIELD_AC_BONUS, defaults.ShieldAcBonus);
            snap.SetValue(AttributeService.DODGE_BONUS, defaults.DodgeBonus);
            snap.SetValue(AttributeService.DEFLECTION_BONUS, defaults.DeflectionBonus);
            snap.SetValue(AttributeService.ARMOR_CLASS, _resolve_snapshot_armor_class(snap));
            return snap;
        }
        AttributeSnapshot gatewaySnapshot = GetMemberAttributeSnapshot(ms.member_id, ev);
        if (gatewaySnapshot != null)
            return ApplyContingencyReservationOverlay(ms, gatewaySnapshot);
        UnitProgress prog = ms.progression;
        if (prog != null)
        {
            int reservedMpMax = _runtime?.GetContingencySystemTyped()
                ?.GetEffectiveReservedMpMaxForMember(ms.member_id, ms.GetTotalReservedMpMax())
                ?? ms.GetTotalReservedMpMax();
            var asvc = new AttributeService();
            asvc.SetupContext(
                new AttributeSourceContext
                {
                    unit_progress = prog,
                    skill_definitions = new Dictionary<StringName, SkillDefinition>(
                        GetSkillDefinitionIndex()
                    ),
                    reserved_mp_max = reservedMpMax,
                }
            );
            return asvc.GetSnapshot();
        }
        ApplyBaseAttributeDefaults(snap, defaults.BaseAttributes);
        snap.SetValue(AttributeService.HP_MAX, Mathf.Max(ms.current_hp, 1));
        snap.SetValue(AttributeService.MP_MAX, Mathf.Max(ms.current_mp, 0));
        snap.SetValue(AttributeService.STAMINA_MAX, defaults.StaminaMax);
        snap.SetValue(AttributeService.AURA_MAX, defaults.AuraMax);
        snap.SetValue(AttributeService.ACTION_POINTS, defaults.ActionPoints);
        snap.SetValue(AttributeService.ACTION_THRESHOLD, defaults.ActionThreshold);
        snap.SetValue(AttributeService.ATTACK_BONUS, defaults.AttackBonus);
        snap.SetValue(AttributeService.ARMOR_AC_BONUS, defaults.ArmorAcBonus);
        snap.SetValue(AttributeService.SHIELD_AC_BONUS, defaults.ShieldAcBonus);
        snap.SetValue(AttributeService.DODGE_BONUS, defaults.DodgeBonus);
        snap.SetValue(AttributeService.DEFLECTION_BONUS, defaults.DeflectionBonus);
        snap.SetValue(AttributeService.ARMOR_CLASS, _resolve_snapshot_armor_class(snap));
        return snap;
    }

    private AttributeSnapshot ApplyContingencyReservationOverlay(
        PartyMemberState memberState,
        AttributeSnapshot snapshot
    )
    {
        if (memberState == null || snapshot == null)
            return snapshot;
        BattleContingencySystem contingencySystem = _runtime?.GetContingencySystemTyped();
        if (contingencySystem == null)
            return snapshot;
        int persistentReserved = memberState.GetTotalReservedMpMax();
        int effectiveReserved = contingencySystem.GetEffectiveReservedMpMaxForMember(
            memberState.member_id,
            persistentReserved
        );
        if (effectiveReserved == persistentReserved)
            return snapshot;

        AttributeSnapshot copy = new();
        foreach (KeyValuePair<StringName, int> entry in snapshot.GetAllValuesTyped())
            copy.SetValue(entry.Key, entry.Value);
        int unreservedMpMax = snapshot.HasValue(AttributeService.MP_MAX_UNRESERVED)
            ? snapshot.GetValue(AttributeService.MP_MAX_UNRESERVED)
            : snapshot.GetValue(AttributeService.MP_MAX) + Mathf.Max(persistentReserved, 0);
        copy.SetValue(AttributeService.MP_MAX_UNRESERVED, Mathf.Max(unreservedMpMax, 0));
        copy.SetValue(AttributeService.RESERVED_MP_MAX, Mathf.Max(effectiveReserved, 0));
        copy.SetValue(
            AttributeService.MP_MAX,
            Mathf.Max(Mathf.Max(unreservedMpMax, 0) - Mathf.Max(effectiveReserved, 0), 0)
        );
        return copy;
    }

    private static IReadOnlyDictionary<StringName, int> ReadBaseAttributeDefaults(
        Godot.Collections.Dictionary context,
        string keyPrefix,
        int defaultValue
    )
    {
        Dictionary<StringName, int> values = new();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            string key = $"{keyPrefix}_{(string)attributeId}";
            values[attributeId] = ReadInt(context, key, defaultValue);
        }
        return values;
    }

    private static void ApplyBaseAttributeDefaults(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, int> values
    )
    {
        if (unitState == null || values == null)
            return;
        foreach (KeyValuePair<StringName, int> entry in values)
            _sv(unitState, entry.Key, entry.Value);
    }

    private static void ApplyBaseAttributeDefaults(
        AttributeSnapshot snapshot,
        IReadOnlyDictionary<StringName, int> values
    )
    {
        if (snapshot == null || values == null)
            return;
        foreach (KeyValuePair<StringName, int> entry in values)
            snapshot.SetValue(entry.Key, entry.Value);
    }

    private static int _resolve_snapshot_armor_class(AttributeSnapshot snap)
    {
        if (snap == null)
            return AttributeService.BASE_ARMOR_CLASS;
        int t =
            AttributeService.BASE_ARMOR_CLASS
            + AttributeSnapshot.CalculateScoreModifier(
                snap.GetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            );
        foreach (var c in AttributeService.AC_COMPONENT_ATTRIBUTE_IDS)
            t += Mathf.Max(snap.GetValue(c), 0);
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
            us.ClearWeaponProjection();
            return;
        }
        WeaponProjection projection = GetMemberWeaponProjectionTyped(mid, ev);
        if (projection == null || projection.IsEmpty())
        {
            us.ClearWeaponProjection();
            return;
        }
        us.ApplyWeaponProjectionTyped(projection);
    }

    private void _apply_member_effective_trait_projection(
        BattleUnitState us,
        StringName mid,
        EquipmentState ev = null
    )
    {
        if (us == null)
            return;
        if ((string)mid == "" || _runtime == null)
        {
            us.effective_trait_instances = new List<BattleEffectiveTraitInstanceState>();
            us.effective_trait_ids = new StringNameList();
            return;
        }

        BattleEffectiveTraitProjection projection = GetMemberEffectiveTraitProjection(mid, ev);
        us.effective_trait_instances = BattleUnitState.DuplicateEffectiveTraitInstances(
            projection?.EffectiveTraitInstances
        );
        us.effective_trait_ids = BattleUnitState.DeriveEffectiveTraitIdsFromInstances(
            us.effective_trait_instances
        );
    }

    private static void _apply_member_identity_projection(BattleUnitState us, PartyMemberState ms)
    {
        if (us == null)
            return;
        if (ms == null)
        {
            us.SetBodySizeCategory(BodySizeContentRules.ToStringName(BodySizeCategoryKind.Small));
            us.SetVersatilityPick("");
            return;
        }
        var pc = ProgressionDataUtils.to_string_name(ms.body_size_category);
        if (!us.SetBodySizeCategory(pc))
        {
            throw new InvalidOperationException(
                $"Member identity 的 body_size_category '{pc}' 非法。 " +
                $"合法值: tiny, small, medium, large, huge, gargantuan, boss"
            );
        }
        us.SetVersatilityPick(ProgressionDataUtils.to_string_name(ms.versatility_pick));
    }

    private EquipmentState _ensure_unit_equipment_view(BattleUnitState us, PartyMemberState ms)
    {
        if (us == null)
            return new EquipmentState();
        if (!us.equipment_view_initialized)
            us.SetEquipmentView(_get_member_equipment_state(ms));
        return us.GetEquipmentView();
    }

    private static EquipmentState _get_member_equipment_state(PartyMemberState ms)
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
            us.ClearWeaponProjection();
            return;
        }
        us.SetNaturalWeaponProjectionTyped(
            (string)pt != "" ? pt : "natural_weapon",
            dt,
            ar,
            null,
            fam
        );
    }

    private void _ensure_basic_attack_skill(BattleUnitState us)
    {
        if (us == null || !_runtime_has_skill(BASIC_ATTACK_SKILL_ID))
            return;
        us.AddKnownActiveSkill(BASIC_ATTACK_SKILL_ID);
        us.SetKnownSkillLevelTyped(BASIC_ATTACK_SKILL_ID, 0, preserveZero: true);
    }

    private void _ensure_enemy_basic_attack_affordability(BattleUnitState us)
    {
        if (us == null || !us.known_active_skill_ids.Contains(BASIC_ATTACK_SKILL_ID))
            return;
        SkillDefinition basicAttack = _skill_definition_from_runtime(BASIC_ATTACK_SKILL_ID);
        CombatSkillDefinition combatProfile = basicAttack?.CombatProfile;
        if (combatProfile == null)
            return;
        int sl = us.HasKnownSkillLevelTyped(BASIC_ATTACK_SKILL_ID)
            ? Mathf.Max(us.GetKnownSkillLevelTyped(BASIC_ATTACK_SKILL_ID), 0)
            : 0;
        CombatSkillResourceCosts costs = combatProfile.GetEffectiveResourceCostValues(sl);
        int sc = Mathf.Max(costs.StaminaCost, 0);
        if (sc <= 0)
            return;
        if (_gv(us, AttributeService.STAMINA_MAX) < sc)
            _sv(us, AttributeService.STAMINA_MAX, sc);
        if (us.current_stamina < sc)
            us.SetCurrentStamina(sc);
    }

    private void _sync_unlocked_resources_from_progression(BattleUnitState us, UnitProgress prog)
    {
        if (us == null)
            return;
        if (prog == null)
        {
            us.SetUnlockedCombatResourceIds(
                BattleUnitState.CreateDefaultUnlockedCombatResourceProjection()
            );
            return;
        }
        prog.SyncDefaultCombatResourceUnlocks();
        var rids = new StringNameList();
        foreach (var rv in prog.UnlockedCombatResourceIdsTyped)
            rids.Add(rv);
        us.SetUnlockedCombatResourceIds(rids);
    }

    private void _sync_enemy_unlocked_resources(BattleUnitState us)
    {
        if (us == null)
            return;
        us.SyncDefaultCombatResourceUnlocks();
        var snap = _snap(us);
        int mM = snap != null ? snap.GetValue(AttributeService.MP_MAX) : 0,
            aM = snap != null ? snap.GetValue(AttributeService.AURA_MAX) : 0;
        if (us.current_mp > 0 || mM > 0)
            us.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        if (us.current_aura > 0 || aM > 0)
            us.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        foreach (var sid in us.known_active_skill_ids)
        {
            SkillDefinition skillDefinition = _skill_definition_from_runtime(sid);
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile == null)
                continue;
            int sl = us.HasKnownSkillLevelTyped(sid)
                ? Mathf.Max(us.GetKnownSkillLevelTyped(sid), 1)
                : 1;
            CombatSkillResourceCosts costs = combatProfile.GetEffectiveResourceCostValues(sl);
            if (costs.MpCost > 0)
                us.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
            if (costs.AuraCost > 0)
                us.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        }
    }

    private bool _runtime_has_skill(StringName sid)
    {
        if ((string)sid == "")
            return false;
        return GetSkillDefinitionIndex().ContainsKey(sid);
    }

    private static int _resolve_action_threshold_from_snapshot(AttributeSnapshot snap, int fb = -1)
    {
        int f = fb >= 0 ? fb : BattleUnitState.DefaultActionThreshold;
        if (snap != null && snap.HasValue(AttributeService.ACTION_THRESHOLD))
        {
            int s = snap.GetValue(AttributeService.ACTION_THRESHOLD);
            if (s > 0)
                return s;
        }
        return f;
    }

    private StringNameList _collect_known_active_skill_ids(UnitProgress prog)
    {
        var r = new StringNameList();
        if (prog == null)
            return r;
        foreach (var sid in prog.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress sp = prog.GetSkillProgress(sid);
            if (sp == null)
                continue;
            SkillDefinition skillDefinition = _skill_definition_from_runtime(sid);
            if (skillDefinition == null || !sp.is_learned)
                continue;
            if (
                skillDefinition.SkillTypeKind != SkillTypeKind.Active
                || !skillDefinition.CanUseInCombat()
            )
                continue;
            r.Add(sid);
        }
        return r;
    }

    private Dictionary<StringName, int> _collect_known_skill_level_map(UnitProgress prog)
    {
        var r = new Dictionary<StringName, int>();
        if (prog == null)
            return r;
        foreach (var sid in prog.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress sp = prog.GetSkillProgress(sid);
            if (sp == null)
                continue;
            SkillDefinition skillDefinition = _skill_definition_from_runtime(sid);
            if (skillDefinition == null || !sp.is_learned)
                continue;
            if (skillDefinition.SkillTypeKind != SkillTypeKind.Active)
                continue;
            r[sid] = sp.skill_level;
        }
        return r;
    }

    private Dictionary<StringName, int> _collect_known_skill_lock_hit_bonus_map(UnitProgress prog)
    {
        var r = new Dictionary<StringName, int>();
        if (prog == null)
            return r;
        foreach (var sid in prog.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress sp = prog.GetSkillProgress(sid);
            SkillDefinition skillDefinition = _skill_definition_from_runtime(sid);
            if (
                sp == null
                || skillDefinition == null
                || !sp.is_learned
                || !sp.is_level_trigger_locked
            )
                continue;
            int b = sp.bonus_to_hit_from_lock;
            if (b <= 0)
                continue;
            r[sid] = b;
        }
        return r;
    }

    private void _sync_passive_battle_statuses(
        BattleUnitState us,
        UnitProgress prog,
        PartyMemberState ms = null
    )
    {
        if (us == null)
            return;
        PassiveSourceContext ctx = null;
        if ((string)us.source_member_id != "")
            ctx = BuildPassiveSourceContext(us.source_member_id, prog);
        if (ctx == null)
        {
            ctx = new PassiveSourceContext
            {
                member_state = ms,
                unit_progress = prog,
            };
        }
        PassiveStatusOrchestrator.ApplyToUnit(us, ctx, GetSkillDefinitionIndex());
    }

    private static StringNameList _extract_movement_tags(
        Godot.Collections.Array values
    )
    {
        var t = new StringNameList();
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

    private static Godot.Collections.Array ReadArray(
        Godot.Collections.Dictionary source,
        string key
    )
    {
        if (source == null || !source.ContainsKey(key))
            return new Godot.Collections.Array();
        return source[key].AsGodotArray();
    }

    private static int ReadInt(
        Godot.Collections.Dictionary source,
        string key,
        int fallback,
        int minValue = int.MinValue
    )
    {
        if (source == null || !source.ContainsKey(key))
            return Mathf.Max(fallback, minValue);
        return Mathf.Max(source[key].AsInt32(), minValue);
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary source,
        string key,
        StringName fallback
    )
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        StringName value = ProgressionDataUtils.to_string_name(source[key]);
        return value == "" ? fallback : value;
    }

}
