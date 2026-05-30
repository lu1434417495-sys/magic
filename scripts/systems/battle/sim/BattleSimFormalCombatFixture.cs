using Godot;

[GlobalClass]
public partial class BattleSimFormalCombatFixture : RefCounted, IBattleRuntimeCharacterGateway
{
    public static readonly StringName ROSTER_MIXED_2S1A = "mixed_2sword_1arch_mirror_simulation";
    public static readonly StringName ROSTER_MIXED_6V12 = "mixed_6v12_mirror_simulation";
    public const string ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID = "main_character_member_id";
    public const string ROSTER_OPTION_LEADER_MEMBER_ID = "leader_member_id";
    public const string ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT = "main_character_reroll_count";
    public const string ROSTER_OPTION_ATTRIBUTE_ROLL_SEED = "attribute_roll_seed";
    private const int HP_ROLL_SEED_OFFSET = 104729;
    private const int ATTRIBUTE_ROLL_DICE_COUNT = 5;
    private const int ATTRIBUTE_ROLL_DICE_SIDES = 3;
    private const int ATTRIBUTE_ROLL_OFFSET = -1;
    private const int ATTRIBUTE_ROLL_VALUE_FLOOR = 4;
    private const int DEFAULT_ATTRIBUTE_ROLL_SEED = 101;
    private const int USE_DEFAULT_ACTION_THRESHOLD = -1;
    private static readonly StringName WARRIOR_BODY_ARMOR_ITEM_ID = "iron_scale_mail";
    private static readonly StringName ARCHER_BODY_ARMOR_ITEM_ID = "leather_jerkin";
    private static readonly Godot.Collections.Array<StringName> ATTRIBUTE_ROLL_IDS = new()
    {
        UnitBaseAttributes.STRENGTH(),
        UnitBaseAttributes.AGILITY(),
        UnitBaseAttributes.CONSTITUTION(),
        UnitBaseAttributes.PERCEPTION(),
        UnitBaseAttributes.INTELLIGENCE(),
        UnitBaseAttributes.WILLPOWER(),
    };

    public static StringName ROSTER_MIXED_2S1A_VALUE() => ROSTER_MIXED_2S1A;

    public static StringName ROSTER_MIXED_6V12_VALUE() => ROSTER_MIXED_6V12;

    public static string ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID_VALUE() =>
        ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID;

    public static string ROSTER_OPTION_LEADER_MEMBER_ID_VALUE() => ROSTER_OPTION_LEADER_MEMBER_ID;

    public static string ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT_VALUE() =>
        ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT;

    public static string ROSTER_OPTION_ATTRIBUTE_ROLL_SEED_VALUE() =>
        ROSTER_OPTION_ATTRIBUTE_ROLL_SEED;

    public static int HP_ROLL_SEED_OFFSET_VALUE() => HP_ROLL_SEED_OFFSET;

    public PartyState party_state;
    public CharacterManagementModule character_management;
    public Godot.Collections.Array<StringName> ally_member_ids = new();
    public Godot.Collections.Array<StringName> hostile_member_ids = new();

    public int charge_mastery;
    public int heavy_mastery;
    public int aimed_mastery;
    public int multishot_mastery;
    public int basic_mastery;

    private Godot.Collections.Dictionary _skill_defs = new();
    private Godot.Collections.Dictionary _profession_defs = new();
    private Godot.Collections.Dictionary _achievement_defs = new();
    private Godot.Collections.Dictionary _item_defs = new();
    private Godot.Collections.Dictionary _progression_content_bundle = new();
    private Godot.Collections.Dictionary _ai_brain_by_member_id = new();
    private Godot.Collections.Dictionary _ai_state_by_member_id = new();
    private Godot.Collections.Dictionary _roster_options = new();
    private RandomNumberGenerator _attribute_roll_rng = new();
    private RandomNumberGenerator _hp_roll_rng = new();

    public void setup_content(Godot.Collections.Dictionary content)
    {
        _skill_defs = _safe_dict(content, "skill_defs");
        _profession_defs = _safe_dict(content, "profession_defs");
        _achievement_defs = _safe_dict(content, "achievement_defs");
        _item_defs = _safe_dict(content, "item_defs");
        _progression_content_bundle = _safe_dict(content, "progression_content_bundle");
        _setup_character_management();
    }

    public bool build_roster(StringName roster_id, Godot.Collections.Dictionary options = null)
    {
        _reset_roster();
        _roster_options =
            options?.Duplicate(true)?.AsGodotDictionary() ?? new Godot.Collections.Dictionary();
        _setup_attribute_roll_rng();
        string rs = roster_id;
        if (rs == "mixed_2sword_1arch_mirror_simulation")
            _build_mixed_2s1a_roster();
        else if (rs == "mixed_6v12_mirror_simulation")
            _build_mixed_6v12_roster();
        else
            return false;
        _finalize_roster_identity();
        _setup_character_management();
        _restore_all_members_to_full_hp();
        return true;
    }

    public Godot.Collections.Dictionary build_runtime_context(
        BattleRuntimeModule runtime,
        Godot.Collections.Dictionary base_context
    )
    {
        _restore_all_members_to_full_hp();
        var context = base_context.Duplicate(true).AsGodotDictionary();
        context["battle_party"] = new Godot.Collections.Array();
        context["ally_member_ids"] = new Godot.Collections.Array<StringName>(ally_member_ids);
        context["validate_spawn_reachability"] = true;
        context["validate_bidirectional_spawn_reachability"] = true;
        context["enforce_opposing_spawn_sides"] = true;
        var saved_active_ids = new Godot.Collections.Array<StringName>(
            party_state.active_member_ids
        );
        party_state.active_member_ids = new Godot.Collections.Array<StringName>(hostile_member_ids);
        var hostile_context = context.Duplicate(true).AsGodotDictionary();
        hostile_context["battle_party"] = new Godot.Collections.Array();
        hostile_context["ally_member_ids"] = new Godot.Collections.Array<StringName>(
            hostile_member_ids
        );
        var hostile_units =
            runtime?._unit_factory?.build_ally_units(party_state, hostile_context)
            ?? new Godot.Collections.Array();
        foreach (var unitV in hostile_units)
            _apply_unit_runtime_metadata(unitV.AsGodotObject(), "hostile");
        context["enemy_units"] = hostile_units;
        party_state.active_member_ids = new Godot.Collections.Array<StringName>(ally_member_ids);
        if (party_state.active_member_ids.Count == 0)
            party_state.active_member_ids = saved_active_ids;
        return context;
    }

    public void apply_started_battle_metadata(GodotObject state)
    {
        if (state == null)
            return;
        foreach (var ukV in state.Get("units").AsGodotDictionary().Keys)
        {
            var unitState = state.Get("units").AsGodotDictionary()[ukV].AsGodotObject();
            if (unitState == null)
                continue;
            if (
                ally_member_ids.Contains(
                    ProgressionDataUtils.to_string_name(unitState.Get("source_member_id"))
                )
            )
                _apply_unit_runtime_metadata(unitState, "player");
            else if (
                hostile_member_ids.Contains(
                    ProgressionDataUtils.to_string_name(unitState.Get("source_member_id"))
                )
            )
                _apply_unit_runtime_metadata(unitState, "hostile");
        }
    }

    public PartyState get_party_state() => party_state;

    public PartyMemberState get_member_state(StringName member_id) =>
        party_state?.get_member_state(member_id);

    public Godot.Collections.Dictionary get_item_defs() => _item_defs;

    public AttributeSnapshot get_member_attribute_snapshot_for_equipment_view(
        StringName member_id,
        EquipmentState equipment_view
    ) =>
        character_management?.get_member_attribute_snapshot_for_equipment_view(
            member_id,
            equipment_view
        );

    public Godot.Collections.Dictionary get_member_weapon_projection_for_equipment_view(
        StringName member_id,
        EquipmentState equipment_view
    ) =>
        character_management?.get_member_weapon_projection_for_equipment_view(
            member_id,
            equipment_view
        )
        ?? new Godot.Collections.Dictionary();

    public PassiveSourceContext build_passive_source_context(
        StringName member_id,
        UnitProgress progression_state = null
    ) =>
        character_management?.build_passive_source_context(member_id, progression_state);

    public CharacterProgressionDelta promote_profession(
        StringName member_id,
        StringName profession_id,
        Godot.Collections.Dictionary selection
    ) => character_management?.promote_profession(member_id, profession_id, selection)
        ?? new CharacterProgressionDelta { member_id = member_id };

    public void commit_battle_resources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
    )
    {
        PartyMemberState memberState = get_member_state(member_id);
        if (memberState == null)
            return;
        memberState.current_hp = current_hp;
        memberState.current_mp = current_mp;
        memberState.current_aura = current_aura;
    }

    public void commit_battle_death(StringName member_id)
    {
        PartyMemberState memberState = get_member_state(member_id);
        if (memberState != null)
            memberState.is_dead = true;
    }

    public int flush_after_battle() => (int)Error.Ok;

    public CharacterProgressionDelta grant_battle_mastery(
        StringName member_id,
        StringName skill_id,
        int amount
    )
    {
        _record_mastery(skill_id, amount);
        var delta = new CharacterProgressionDelta();
        delta.member_id = member_id;
        delta.mastery_changes.Add(
            new Godot.Collections.Dictionary
            {
                { "skill_id", Variant.From(skill_id) },
                { "amount", amount },
                { "source_type", "battle" },
            }
        );
        return delta;
    }

    public CharacterProgressionDelta grant_skill_mastery_from_source(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label = "",
        string reason_text = "",
        bool emit_achievement_event = true
    )
    {
        _record_mastery(skill_id, amount);
        var delta = new CharacterProgressionDelta();
        delta.member_id = member_id;
        delta.mastery_changes.Add(
            new Godot.Collections.Dictionary
            {
                { "skill_id", Variant.From(skill_id) },
                { "amount", amount },
                { "source_type", Variant.From(source_type) },
            }
        );
        return delta;
    }

    public Godot.Collections.Array<StringName> record_achievement_event(
        StringName member_id,
        StringName event_type
    ) => record_achievement_event(member_id, event_type, 1, "", new Godot.Collections.Dictionary());

    public Godot.Collections.Array<StringName> record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount
    ) => record_achievement_event(member_id, event_type, amount, "", new Godot.Collections.Dictionary());

    public Godot.Collections.Array<StringName> record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id
    ) => record_achievement_event(member_id, event_type, amount, subject_id, new Godot.Collections.Dictionary());

    public Godot.Collections.Array<StringName> record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id,
        Godot.Collections.Dictionary meta
    ) => new Godot.Collections.Array<StringName>();

    public PendingCharacterReward build_pending_skill_mastery_reward(
        StringName member_id,
        StringName source_type,
        string source_label,
        Godot.Collections.Array entry_options,
        string summary_text
    ) => null;

    private void _reset_roster()
    {
        party_state = new PartyState();
        party_state.version = 3;
        party_state.gold = 0;
        ally_member_ids.Clear();
        hostile_member_ids.Clear();
        _ai_brain_by_member_id.Clear();
        _ai_state_by_member_id.Clear();
        _roster_options.Clear();
        charge_mastery = 0;
        heavy_mastery = 0;
        aimed_mastery = 0;
        multishot_mastery = 0;
        basic_mastery = 0;
        _attribute_roll_rng = new RandomNumberGenerator();
    }

    private void _build_mixed_2s1a_roster()
    {
        var sword_attrs = _attrs(14, 12, 14, 10, 8, 10);
        var archer_attrs = _attrs(10, 16, 12, 14, 8, 10);
        var sword_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("charge", 1, false),
            _sk("warrior_heavy_strike", 1, false),
        };
        var archer_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("basic_attack", 0, false),
            _sk("archer_aimed_shot", 1, false),
            _sk("archer_multishot", 1, false),
        };
        _add_member(
            "ally_longsword_01",
            "盟军长剑手01",
            "player",
            sword_attrs,
            30,
            sword_skills,
            "",
            0,
            "steel_longsword",
            WARRIOR_BODY_ARMOR_ITEM_ID,
            "melee_aggressor",
            "engage"
        );
        _add_member(
            "ally_longsword_02",
            "盟军长剑手02",
            "player",
            sword_attrs,
            30,
            sword_skills,
            "",
            0,
            "steel_longsword",
            WARRIOR_BODY_ARMOR_ITEM_ID,
            "melee_aggressor",
            "engage"
        );
        _add_member(
            "ally_archer_01",
            "盟军弓箭手",
            "player",
            archer_attrs,
            30,
            archer_skills,
            "",
            0,
            "ash_longbow",
            ARCHER_BODY_ARMOR_ITEM_ID,
            "ranged_archer",
            "pressure"
        );
        _add_member(
            "enemy_longsword_01",
            "敌军长剑手01",
            "hostile",
            sword_attrs,
            30,
            sword_skills,
            "",
            0,
            "steel_longsword",
            WARRIOR_BODY_ARMOR_ITEM_ID,
            "melee_aggressor",
            "engage"
        );
        _add_member(
            "enemy_longsword_02",
            "敌军长剑手02",
            "hostile",
            sword_attrs,
            30,
            sword_skills,
            "",
            0,
            "steel_longsword",
            WARRIOR_BODY_ARMOR_ITEM_ID,
            "melee_aggressor",
            "engage"
        );
        _add_member(
            "enemy_archer_01",
            "敌军弓箭手",
            "hostile",
            archer_attrs,
            30,
            archer_skills,
            "",
            0,
            "ash_longbow",
            ARCHER_BODY_ARMOR_ITEM_ID,
            "ranged_archer",
            "pressure"
        );
    }

    private void _build_mixed_6v12_roster()
    {
        var elite_sword_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("basic_attack", 0, false),
            _sk("charge", 7, true),
            _sk("warrior_heavy_strike", 5, true),
        };
        var elite_archer_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("basic_attack", 0, false),
            _sk("archer_aimed_shot", 3, true),
            _sk("archer_multishot", 7, true),
        };
        var elite_mage_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("basic_attack", 0, false),
            _sk("mage_fireball", 7, true),
            _sk("mage_cone_of_cold", 7, true),
            _sk("mage_blink", 7, true),
            _sk("mage_gust_of_wind", 7, true),
            _sk("mage_chain_lightning", 7, true),
        };
        var hostile_sword_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("basic_attack", 0, false),
            _sk("charge", 1, false),
            _sk("warrior_heavy_strike", 1, false),
        };
        var hostile_archer_skills = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            _sk("basic_attack", 0, false),
            _sk("archer_aimed_shot", 1, false),
            _sk("archer_multishot", 1, false),
        };
        for (int index = 0; index < 4; index++)
            _add_member(
                $"elite_sword_{index}",
                $"Elite Sword {index}",
                "player",
                _roll_creation_attributes(),
                USE_DEFAULT_ACTION_THRESHOLD,
                elite_sword_skills,
                "warrior",
                2,
                "steel_longsword",
                WARRIOR_BODY_ARMOR_ITEM_ID,
                "melee_aggressor",
                "engage"
            );
        _add_member(
            "elite_archer_0",
            "Elite Archer 0",
            "player",
            _roll_creation_attributes(),
            USE_DEFAULT_ACTION_THRESHOLD,
            elite_archer_skills,
            "archer",
            2,
            "ash_longbow",
            ARCHER_BODY_ARMOR_ITEM_ID,
            "ranged_archer",
            "pressure"
        );
        _add_member(
            "elite_mage_0",
            "Elite Mage 0",
            "player",
            _roll_creation_attributes(),
            USE_DEFAULT_ACTION_THRESHOLD,
            elite_mage_skills,
            "mage",
            5,
            "",
            ARCHER_BODY_ARMOR_ITEM_ID,
            "mage_controller",
            "pressure"
        );
        _set_member_mp_max("elite_mage_0", 1000);
        for (int index = 0; index < 6; index++)
            _add_member(
                $"hostile_sword_{index}",
                $"Hostile Elite Sword {index}",
                "hostile",
                _roll_creation_attributes(),
                USE_DEFAULT_ACTION_THRESHOLD,
                hostile_sword_skills,
                "warrior",
                2,
                "steel_longsword",
                WARRIOR_BODY_ARMOR_ITEM_ID,
                "melee_aggressor",
                "engage"
            );
        for (int index = 0; index < 6; index++)
            _add_member(
                $"hostile_archer_{index}",
                $"Hostile Archer {index}",
                "hostile",
                _roll_creation_attributes(),
                USE_DEFAULT_ACTION_THRESHOLD,
                hostile_archer_skills,
                "",
                0,
                "ash_longbow",
                ARCHER_BODY_ARMOR_ITEM_ID,
                "ranged_archer",
                "pressure"
            );
    }

    private void _add_member(
        StringName member_id,
        string display_name,
        StringName faction_id,
        Godot.Collections.Dictionary attrs,
        int action_threshold,
        Godot.Collections.Array<Godot.Collections.Dictionary> skill_configs,
        StringName profession_id,
        int profession_rank,
        StringName weapon_item_id,
        StringName body_armor_item_id,
        StringName ai_brain_id,
        StringName ai_state_id
    )
    {
        var payload = _build_creation_payload(display_name, attrs, action_threshold);
        var member_state = CharacterCreationService.create_member_from_character_creation_payload(
            member_id,
            payload,
            _progression_content_bundle
        );
        member_state.faction_id = faction_id;
        member_state.control_mode = "ai";
        _apply_skills(member_state, skill_configs);
        _apply_profession_rank(
            member_state,
            profession_id,
            profession_rank,
            _collect_core_skill_ids(skill_configs)
        );
        _equip_member(member_state, weapon_item_id, body_armor_item_id);
        party_state.set_member_state(member_state);
        if ((string)faction_id == "hostile")
            hostile_member_ids.Add(member_id);
        else
            ally_member_ids.Add(member_id);
        _ai_brain_by_member_id[member_id] = Variant.From(ai_brain_id);
        _ai_state_by_member_id[member_id] = Variant.From(ai_state_id);
    }

    private void _set_member_mp_max(StringName member_id, int mp_max)
    {
        var member_state = party_state.get_member_state(member_id);
        var attributes = _unit_base_attributes(member_state);
        if (attributes == null)
            return;
        attributes.set_attribute_value(AttributeService.MP_MAX, mp_max);
        member_state.current_mp = mp_max;
    }

    private void _finalize_roster_identity()
    {
        if (party_state == null)
            return;
        party_state.active_member_ids = new Godot.Collections.Array<StringName>(ally_member_ids);
        var fallback_main_id = _first_ally_member_id();
        if ((string)fallback_main_id == "")
            return;
        var main_member_id = _resolve_roster_variant_ally_member_id(
            ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID,
            fallback_main_id
        );
        party_state.main_character_member_id = main_member_id;
        party_state.leader_member_id = _resolve_roster_variant_ally_member_id(
            ROSTER_OPTION_LEADER_MEMBER_ID,
            main_member_id
        );
        _bake_main_character_reroll_luck();
    }

    private StringName _first_ally_member_id() =>
        ally_member_ids.Count > 0 ? ally_member_ids[0] : new StringName("");

    private StringName _resolve_roster_variant_ally_member_id(
        string option_key,
        StringName fallback_member_id
    )
    {
        var member_id = ProgressionDataUtils.to_string_name(
            _roster_options.GetValueOrDefault(option_key, "")
        );
        if ((string)member_id == "")
            return fallback_member_id;
        if (ally_member_ids.Contains(member_id))
            return member_id;
        GameLog.Warning(
            $"BattleSimFormalCombatFixture: roster option {option_key}={member_id} is not a valid ally member; using {fallback_member_id}.",
            "battlesim.fixture.invalid_ally_member",
            "battlesim"
        );
        return fallback_member_id;
    }

    private static UnitProgress _unit_progress(PartyMemberState member_state)
    {
        return member_state?.progression as UnitProgress;
    }

    private static UnitBaseAttributes _unit_base_attributes(PartyMemberState member_state)
    {
        return _unit_progress(member_state)?.unit_base_attributes;
    }

    private void _setup_attribute_roll_rng()
    {
        _attribute_roll_rng.Seed = (ulong)
            _roster_options
                .GetValueOrDefault(
                    ROSTER_OPTION_ATTRIBUTE_ROLL_SEED,
                    DEFAULT_ATTRIBUTE_ROLL_SEED
                )
                .AsInt32();
        _hp_roll_rng.Seed = _attribute_roll_rng.Seed + (ulong)HP_ROLL_SEED_OFFSET;
    }

    private Godot.Collections.Dictionary _roll_creation_attributes()
    {
        var attrs = new Godot.Collections.Dictionary();
        foreach (var attribute_id in ATTRIBUTE_ROLL_IDS)
            attrs[attribute_id] = _roll_creation_attribute_value();
        return attrs;
    }

    private int _roll_creation_attribute_value()
    {
        int total = ATTRIBUTE_ROLL_OFFSET;
        for (int ri = 0; ri < ATTRIBUTE_ROLL_DICE_COUNT; ri++)
            total += _attribute_roll_rng.RandiRange(1, ATTRIBUTE_ROLL_DICE_SIDES);
        return Mathf.Max(ATTRIBUTE_ROLL_VALUE_FLOOR, total);
    }

    private void _bake_main_character_reroll_luck()
    {
        if (party_state == null || (string)party_state.main_character_member_id == "")
            return;
        var member_state = party_state.get_member_state(party_state.main_character_member_id);
        if (member_state?.progression == null)
            return;
        var attribute_service = new AttributeService();
        attribute_service.setup(member_state.progression);
        var creation_service = new CharacterCreationService();
        int reroll_count = _roster_options
            .GetValueOrDefault(ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT, 0)
            .AsInt32();
        if (!creation_service.bake_hidden_luck_at_birth(attribute_service, reroll_count))
            GameLog.Warning(
                $"BattleSimFormalCombatFixture: failed to bake reroll luck for main character {party_state.main_character_member_id}.",
                "battlesim.fixture.bake_luck_failed",
                "battlesim"
            );
    }

    private void _apply_skills(
        PartyMemberState member_state,
        Godot.Collections.Array<Godot.Collections.Dictionary> skill_configs
    )
    {
        var unit_progress = _unit_progress(member_state);
        if (unit_progress == null)
            return;
        var progression_service = new ProgressionService();
        progression_service.setup(member_state.progression, _skill_defs, _profession_defs);
        foreach (var skill_config in skill_configs)
        {
            if (skill_config == null)
                continue;
            var skill_id = ProgressionDataUtils.to_string_name(
                skill_config.ContainsKey("skill_id") ? skill_config["skill_id"].AsStringName() : ""
            );
            int target_level = Mathf.Max(_d_int(skill_config, "level", 1), 0);
            bool is_core = skill_config.ContainsKey("is_core") && skill_config["is_core"].AsBool();
            if ((string)skill_id == "")
                continue;
            var skill_progress = unit_progress.get_skill_progress(skill_id);
            if (skill_progress == null || !skill_progress.is_learned)
                progression_service.learn_skill(skill_id);
            var skill_def = _dict_obj<SkillDef>(_skill_defs, skill_id);
            if (is_core)
            {
                progression_service.set_skill_core(skill_id, true);
                skill_progress = unit_progress.get_skill_progress(skill_id);
                _unlock_fixture_core_skill_level_cap(
                    unit_progress,
                    skill_progress,
                    skill_def,
                    target_level
                );
            }
            int mastery_amount = _calculate_mastery_for_level(skill_def, target_level);
            if (mastery_amount > 0)
                progression_service.grant_skill_mastery(skill_id, mastery_amount, "training");
            if (is_core)
            {
                progression_service.set_skill_core(skill_id, true);
                _apply_core_max_growth(member_state, skill_id, target_level);
            }
        }
        progression_service.refresh_runtime_state();
    }

    private void _unlock_fixture_core_skill_level_cap(
        UnitProgress unit_progress,
        UnitSkillProgress skill_progress,
        SkillDef skill_def,
        int target_level
    )
    {
        if (unit_progress == null || skill_progress == null || skill_def == null)
            return;
        int non_core_max_level = Mathf.Max(skill_def.non_core_max_level, 0);
        if (non_core_max_level <= 0 || target_level <= non_core_max_level)
            return;
        skill_progress.is_level_trigger_active = false;
        skill_progress.is_level_trigger_locked = true;
        if (!unit_progress.locked_level_trigger_skill_ids.Contains(skill_progress.skill_id))
            unit_progress.locked_level_trigger_skill_ids.Add(skill_progress.skill_id);
        if (unit_progress.active_level_trigger_core_skill_id == skill_progress.skill_id)
            unit_progress.active_level_trigger_core_skill_id = "";
        unit_progress.set_skill_progress(skill_progress);
    }

    private void _apply_core_max_growth(
        PartyMemberState member_state,
        StringName skill_id,
        int target_level
    )
    {
        var skill_def = _dict_obj<SkillDef>(_skill_defs, skill_id);
        var unit_progress = _unit_progress(member_state);
        var skill_progress = unit_progress?.get_skill_progress(skill_id);
        if (skill_def == null || skill_progress == null)
            return;
        if (skill_progress.core_max_growth_claimed)
            return;
        if (target_level < skill_def.max_level)
            return;
        var growth = skill_def.attribute_growth_progress ?? new Godot.Collections.Dictionary();
        if (growth.Count == 0)
        {
            skill_progress.core_max_growth_claimed = true;
            unit_progress.set_skill_progress(skill_progress);
            return;
        }
        var growth_service = new AttributeGrowthService();
        growth_service.setup(member_state.progression);
        foreach (var attr_key in growth.Keys)
        {
            var attr_id = ProgressionDataUtils.to_string_name(attr_key);
            growth_service.apply_attribute_progress(
                attr_id,
                growth.ContainsKey(attr_key) ? growth[attr_key].AsInt32() : 0,
                "battle_sim_fixture"
            );
        }
        skill_progress.core_max_growth_claimed = true;
        unit_progress.set_skill_progress(skill_progress);
    }

    private void _apply_profession_rank(
        PartyMemberState member_state,
        StringName profession_id,
        int rank,
        Godot.Collections.Array<StringName> core_skill_ids
    )
    {
        var unit_progress = _unit_progress(member_state);
        if (unit_progress == null || (string)profession_id == "" || rank <= 0)
            return;
        var profession_progress = new UnitProfessionProgress();
        profession_progress.profession_id = profession_id;
        profession_progress.rank = rank;
        profession_progress.is_active = true;
        foreach (var skill_id in core_skill_ids)
        {
            profession_progress.add_core_skill(skill_id);
            var sp = unit_progress.get_skill_progress(skill_id);
            if (sp != null)
            {
                sp.is_core = true;
                sp.assigned_profession_id = profession_id;
                unit_progress.set_skill_progress(sp);
            }
        }
        _apply_profession_granted_skills(member_state, profession_id, rank, profession_progress);
        unit_progress.set_profession_progress(profession_progress);
        int hp_gain_total = _calculate_profession_hp_gain_total(member_state, profession_id, rank);
        var attributes = unit_progress.unit_base_attributes;
        attributes.set_attribute_value(
            AttributeService.HP_MAX,
            attributes.get_attribute_value(AttributeService.HP_MAX) + hp_gain_total
        );
        member_state.current_hp = attributes.get_attribute_value(AttributeService.HP_MAX);
        var ps = new ProgressionService();
        ps.setup(member_state.progression, _skill_defs, _profession_defs);
        ps.refresh_runtime_state();
    }

    private void _apply_profession_granted_skills(
        PartyMemberState member_state,
        StringName profession_id,
        int rank,
        UnitProfessionProgress profession_progress
    )
    {
        var unit_progress = _unit_progress(member_state);
        if (unit_progress == null || (string)profession_id == "" || profession_progress == null)
            return;
        var profession_def = _dict_obj<ProfessionDef>(_profession_defs, profession_id);
        if (profession_def == null)
            return;
        for (int target_rank = 1; target_rank <= rank; target_rank++)
        {
            var granted_skills = profession_def.get_granted_skills_for_rank(target_rank);
            if (granted_skills == null)
                continue;
            foreach (ProfessionGrantedSkill granted_skill in granted_skills)
            {
                if (granted_skill == null || (string)granted_skill.skill_id == "")
                    continue;
                profession_progress.add_granted_skill(granted_skill.skill_id);
                var sp = unit_progress.get_skill_progress(granted_skill.skill_id);
                if (sp == null)
                {
                    sp = new UnitSkillProgress();
                    sp.skill_id = granted_skill.skill_id;
                }
                sp.is_learned = true;
                if ((string)sp.profession_granted_by == "")
                    sp.profession_granted_by = profession_id;
                sp.granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PROFESSION();
                sp.granted_source_id = profession_id;
                unit_progress.set_skill_progress(sp);
            }
        }
    }

    private int _calculate_profession_hp_gain_total(
        PartyMemberState member_state,
        StringName profession_id,
        int rank
    )
    {
        var attributes = _unit_base_attributes(member_state);
        if (attributes == null)
            return 0;
        var profession_def = _dict_obj<ProfessionDef>(_profession_defs, profession_id);
        if (profession_def == null)
            return 0;
        int constitution = attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION());
        int hit_die_sides = Mathf.Max(profession_def.hit_die_sides, 1);
        int total = 0;
        for (int ri = 0; ri < Mathf.Max(rank, 0); ri++)
        {
            int hp_roll = _hp_roll_rng.RandiRange(1, hit_die_sides);
            total += ProgressionService.calculate_profession_hit_point_gain(hp_roll, constitution);
        }
        return total;
    }

    private void _equip_member(
        PartyMemberState member_state,
        StringName weapon_item_id,
        StringName body_armor_item_id
    )
    {
        if (member_state == null)
            return;
        var equipment_state = new EquipmentState();
        bool equipped_any = false;
        equipped_any =
            _equip_item_into_slot(
                equipment_state,
                member_state.member_id,
                weapon_item_id,
                EquipmentRules.MAIN_HAND(),
                true,
                false
            ) || equipped_any;
        equipped_any =
            _equip_item_into_slot(
                equipment_state,
                member_state.member_id,
                body_armor_item_id,
                EquipmentRules.BODY(),
                false,
                true
            ) || equipped_any;
        if (equipped_any)
            member_state.equipment_state = equipment_state;
    }

    private bool _equip_item_into_slot(
        EquipmentState equipment_state,
        StringName member_id,
        StringName item_id,
        StringName entry_slot_id,
        bool require_weapon,
        bool require_armor
    )
    {
        if (equipment_state == null || (string)item_id == "")
            return false;
        var item_def = _dict_obj<ItemDef>(_item_defs, item_id);
        if (item_def == null || !item_def.is_equipment())
            return false;
        if (require_weapon && !item_def.is_weapon())
            return false;
        if (require_armor && !item_def.is_armor())
            return false;
        var slot_ids = item_def.get_equipment_slot_ids();
        if (slot_ids == null || !slot_ids.Contains(entry_slot_id))
            return false;
        var occupied_slots = item_def.get_final_occupied_slot_ids(entry_slot_id);
        if (occupied_slots == null)
            occupied_slots = new Godot.Collections.Array<StringName>();
        var occupied_sn = new Godot.Collections.Array<StringName>();
        foreach (var os in occupied_slots)
            occupied_sn.Add(ProgressionDataUtils.to_string_name(os));
        var instance_id = $"sim_{member_id}_{item_id}";
        var equipment_instance = EquipmentInstanceState.create(item_id, instance_id);
        return equipment_state.set_equipped_entry(
            entry_slot_id,
            item_id,
            occupied_sn,
            equipment_instance
        );
    }

    private void _setup_character_management()
    {
        if (party_state == null)
            party_state = new PartyState();
        character_management = new CharacterManagementModule();
        character_management.setup(
            party_state,
            _skill_defs,
            _profession_defs,
            _achievement_defs,
            _item_defs,
            new Godot.Collections.Dictionary(),
            null,
            _progression_content_bundle
        );
    }

    private void _restore_all_members_to_full_hp()
    {
        if (party_state == null || character_management == null)
            return;
        foreach (var mkv in party_state.member_states.Keys)
        {
            var member_id = ProgressionDataUtils.to_string_name(mkv);
            var member_state = party_state.get_member_state(member_id);
            if (member_state == null)
                continue;
            var attributes = _unit_base_attributes(member_state);
            member_state.current_hp = Mathf.Max(
                attributes?.get_attribute_value(AttributeService.HP_MAX) ?? 1,
                1
            );
        }
    }

    private void _apply_unit_runtime_metadata(
        GodotObject unit_state,
        StringName fallback_faction_id
    )
    {
        if (unit_state == null)
            return;
        var member_id = unit_state.Get("source_member_id").AsStringName();
        unit_state.Set("faction_id", Variant.From(fallback_faction_id));
        unit_state.Set("control_mode", "ai");
        unit_state.Set(
            "ai_brain_id",
            ProgressionDataUtils.to_string_name(
                _ai_brain_by_member_id.GetValueOrDefault(member_id, unit_state.Get("ai_brain_id"))
            )
        );
        unit_state.Set(
            "ai_state_id",
            ProgressionDataUtils.to_string_name(
                _ai_state_by_member_id.GetValueOrDefault(member_id, unit_state.Get("ai_state_id"))
            )
        );
    }

    private void _record_mastery(StringName skill_id, int amount)
    {
        switch ((string)skill_id)
        {
            case "charge":
                charge_mastery += amount;
                break;
            case "warrior_heavy_strike":
                heavy_mastery += amount;
                break;
            case "archer_aimed_shot":
                aimed_mastery += amount;
                break;
            case "archer_multishot":
                multishot_mastery += amount;
                break;
            case "basic_attack":
                basic_mastery += amount;
                break;
        }
    }

    private int _calculate_mastery_for_level(SkillDef skill_def, int target_level)
    {
        if (skill_def == null)
            return 0;
        int total = 0;
        for (int level = 0; level < target_level; level++)
            total += Mathf.Max(skill_def.get_mastery_required_for_level(level), 0);
        return total;
    }

    private Godot.Collections.Array<StringName> _collect_core_skill_ids(
        Godot.Collections.Array<Godot.Collections.Dictionary> skill_configs
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var sc in skill_configs)
        {
            if (sc == null || !(sc.ContainsKey("is_core") && sc["is_core"].AsBool()))
                continue;
            var sid = ProgressionDataUtils.to_string_name(
                sc.ContainsKey("skill_id") ? sc["skill_id"].AsStringName() : ""
            );
            if ((string)sid != "")
                result.Add(sid);
        }
        return result;
    }

    private Godot.Collections.Dictionary _build_creation_payload(
        string display_name,
        Godot.Collections.Dictionary attrs,
        int action_threshold
    )
    {
        var payload = new Godot.Collections.Dictionary
        {
            { "display_name", display_name },
            { "race_id", "human" },
            { "subrace_id", "common_human" },
            { "age_years", 24 },
            { "birth_at_world_step", 0 },
            { "age_profile_id", "human_age_profile" },
            { "natural_age_stage_id", "adult" },
            { "effective_age_stage_id", "adult" },
            { "body_size_category", "medium" },
            { "versatility_pick", "" },
            { "strength", _d_int(attrs, "strength", 10) },
            { "agility", _d_int(attrs, "agility", 10) },
            { "constitution", _d_int(attrs, "constitution", 10) },
            { "perception", _d_int(attrs, "perception", 10) },
            { "intelligence", _d_int(attrs, "intelligence", 10) },
            { "willpower", _d_int(attrs, "willpower", 10) },
        };
        if (action_threshold > 0)
            payload["action_threshold"] = action_threshold;
        return payload;
    }

    private static Godot.Collections.Dictionary _attrs(
        int strength,
        int agility,
        int constitution,
        int perception,
        int intelligence,
        int willpower
    ) =>
        new Godot.Collections.Dictionary
        {
            { "strength", strength },
            { "agility", agility },
            { "constitution", constitution },
            { "perception", perception },
            { "intelligence", intelligence },
            { "willpower", willpower },
        };

    private static Godot.Collections.Dictionary _sk(string skill_id, int level, bool is_core) =>
        new Godot.Collections.Dictionary
        {
            { "skill_id", skill_id },
            { "level", level },
            { "is_core", is_core },
        };

    // Helpers
    private static Godot.Collections.Dictionary _safe_dict(
        Godot.Collections.Dictionary src,
        string key
    )
    {
        if (src == null || !src.ContainsKey(key))
            return new Godot.Collections.Dictionary();
        return src[key].AsGodotDictionary() ?? new Godot.Collections.Dictionary();
    }

    private static T _dict_obj<T>(Godot.Collections.Dictionary d, StringName key)
        where T : GodotObject
    {
        if (d == null || !d.ContainsKey(key))
            return null;
        return d[key].AsGodotObject() as T;
    }

    private static int _d_int(Godot.Collections.Dictionary d, string key, int fallback) =>
        d != null && d.ContainsKey(key) ? d[key].AsInt32() : fallback;

    private static bool _array_contains_str(Godot.Collections.Array arr, StringName v)
    {
        if (arr == null)
            return false;
        foreach (var item in arr)
            if (ProgressionDataUtils.to_string_name(item) == v)
                return true;
        return false;
    }
}
