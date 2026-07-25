using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSimFormalRosterOptionsData
{
    public StringName MainCharacterMemberId { get; init; } = "";
    public StringName LeaderMemberId { get; init; } = "";
    public int MainCharacterRerollCount { get; init; }
    public long AttributeRollSeed { get; init; } = 101;
}

internal sealed record BattleSimFormalCreationAttributesData(
    int Strength,
    int Agility,
    int Constitution,
    int Perception,
    int Intelligence,
    int Willpower
);

internal sealed record BattleSimFormalSkillConfigData(
    StringName SkillId,
    int Level,
    bool IsCore
);

internal sealed class BattleSimFormalRuntimeStartInput : IDisposable
{
    private GodotProjectionLease<Godot.Collections.Dictionary> _contextLease;
    private BattleStartUnitRoster _enemyUnitRoster;
    private bool _disposed;

    internal BattleSimFormalRuntimeStartInput(
        GodotProjectionLease<Godot.Collections.Dictionary> contextLease,
        BattleStartUnitRoster enemyUnitRoster
    )
    {
        _contextLease = contextLease ?? throw new ArgumentNullException(nameof(contextLease));
        _enemyUnitRoster =
            enemyUnitRoster ?? throw new ArgumentNullException(nameof(enemyUnitRoster));
    }

    internal Godot.Collections.Dictionary Context
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _contextLease.Value;
        }
    }

    internal BattleStartUnitRoster TakeEnemyUnitRoster()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        BattleStartUnitRoster roster = _enemyUnitRoster;
        if (roster == null)
            throw new InvalidOperationException("Formal runtime enemy roster was already transferred.");
        _enemyUnitRoster = null;
        return roster;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _enemyUnitRoster = null;
        _contextLease?.Dispose();
        _contextLease = null;
    }
}

public sealed class BattleSimFormalCombatFixture : IBattleRuntimeCharacterGateway, IDisposable
{
    internal static readonly StringName ROSTER_MIXED_2S1A =
        "mixed_2sword_1arch_mirror_simulation";
    internal static readonly StringName ROSTER_MIXED_6V12 = "mixed_6v12_mirror_simulation";
    private const string ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID = "main_character_member_id";
    private const string ROSTER_OPTION_LEADER_MEMBER_ID = "leader_member_id";
    internal const int HP_ROLL_SEED_OFFSET = 104729;
    private const int ATTRIBUTE_ROLL_DICE_COUNT = 5;
    private const int ATTRIBUTE_ROLL_DICE_SIDES = 3;
    private const int ATTRIBUTE_ROLL_OFFSET = -1;
    private const int ATTRIBUTE_ROLL_VALUE_FLOOR = 4;
    private const int DEFAULT_ATTRIBUTE_ROLL_SEED = 101;
    private const int USE_DEFAULT_ACTION_THRESHOLD = -1;
    private static readonly StringName WARRIOR_BODY_ARMOR_ITEM_ID = "iron_scale_mail";
    private static readonly StringName ARCHER_BODY_ARMOR_ITEM_ID = "leather_jerkin";
    public PartyState party_state;
    public CharacterManagementModule character_management;
    public List<StringName> ally_member_ids = new();
    public List<StringName> hostile_member_ids = new();

    public int charge_mastery;
    public int heavy_mastery;
    public int aimed_mastery;
    public int multishot_mastery;
    public int basic_mastery;

    private Dictionary<StringName, SkillDefinition> _skill_definition_index = new();
    private Dictionary<StringName, ProfessionDefinition> _profession_def_index = new();
    private Dictionary<StringName, AchievementDefinition> _achievement_def_index = new();
    private Dictionary<StringName, ItemDefinition> _item_def_index = new();
    private Dictionary<StringName, TraitDefinition> _trait_def_index = new();
    private ProgressionIdentityCatalogData _progression_identity_catalog = new();
    private readonly Dictionary<StringName, StringName> _ai_brain_by_member_id = new();
    private readonly Dictionary<StringName, StringName> _ai_state_by_member_id = new();
    private BattleSimFormalRosterOptionsData _roster_options = new();
    private RuntimeRandom _attribute_roll_rng = new(DEFAULT_ATTRIBUTE_ROLL_SEED);
    private RuntimeRandom _hp_roll_rng = new(DEFAULT_ATTRIBUTE_ROLL_SEED + HP_ROLL_SEED_OFFSET);

    public void Dispose()
    {
        System.GC.SuppressFinalize(this);
        _dispose_roster_state();
        _skill_definition_index.Clear();
        _profession_def_index.Clear();
        _achievement_def_index.Clear();
        _item_def_index.Clear();
        _trait_def_index.Clear();
        _progression_identity_catalog = new ProgressionIdentityCatalogData();
        _roster_options = new BattleSimFormalRosterOptionsData();
        _attribute_roll_rng = null;
        _hp_roll_rng = null;
    }

    public void SetupContent(
        ProgressionContentRegistry progression_registry,
        ItemContentRegistry item_registry
    )
    {
        ArgumentNullException.ThrowIfNull(progression_registry);
        ArgumentNullException.ThrowIfNull(item_registry);
        IReadOnlyDictionary<StringName, SkillDefinition> typedSkillDefinitions =
            progression_registry.GetSkillDefinitionsTyped();
        _apply_content_catalogs(
            typedSkillDefinitions,
            progression_registry.GetProfessionDefsTyped(),
            progression_registry.GetAchievementDefsTyped(),
            item_registry.GetItemDefsTyped(),
            progression_registry.GetTraitDefsTyped(),
            progression_registry.GetIdentityCatalogTyped()
        );
    }

    public void SetupContent(
        ProgressionContentRegistry progression_registry,
        ItemContentRegistry item_registry,
        IReadOnlyDictionary<StringName, SkillDefinition> skill_definitions_override
    )
    {
        ArgumentNullException.ThrowIfNull(progression_registry);
        ArgumentNullException.ThrowIfNull(item_registry);
        IReadOnlyDictionary<StringName, SkillDefinition> typedSkillDefinitions =
            skill_definitions_override != null && skill_definitions_override.Count > 0
                ? new Dictionary<StringName, SkillDefinition>(skill_definitions_override)
                : progression_registry.GetSkillDefinitionsTyped();
        _apply_content_catalogs(
            typedSkillDefinitions,
            progression_registry.GetProfessionDefsTyped(),
            progression_registry.GetAchievementDefsTyped(),
            item_registry.GetItemDefsTyped(),
            progression_registry.GetTraitDefsTyped(),
            progression_registry.GetIdentityCatalogTyped()
        );
    }

    internal void SetupContent(ContentSnapshot snapshot)
    {
        SetupContent(snapshot, null);
    }

    internal void SetupContent(
        ContentSnapshot snapshot,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitionsOverride
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            skillDefinitionsOverride != null && skillDefinitionsOverride.Count > 0
                ? new Dictionary<StringName, SkillDefinition>(skillDefinitionsOverride)
                : snapshot.Skills;
        _apply_content_catalogs(
            skillDefinitions,
            snapshot.Professions,
            snapshot.Achievements,
            snapshot.Items,
            snapshot.Traits,
            snapshot.IdentityCatalog
        );
    }

    public bool BuildRoster(StringName roster_id, BattleSimFormalRosterOptionsData options)
    {
        _reset_roster();
        _roster_options = options ?? new BattleSimFormalRosterOptionsData();
        _setup_attribute_roll_rng();
        string rs = roster_id;
        if (rs == "mixed_2sword_1arch_mirror_simulation")
            _build_mixed_2s1a_roster();
        else if (rs == "mixed_6v12_mirror_simulation")
            _build_mixed_6v12_roster();
        else if (rs == "mixed_6v12_two_archer")
            _build_mixed_6v12_two_archer_roster();
        else
            return false;
        _finalize_roster_identity();
        _setup_character_management();
        _restore_all_members_to_full_hp();
        return true;
    }

    private void _apply_content_catalogs(
        IReadOnlyDictionary<StringName, SkillDefinition> typed_skill_definitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> typed_profession_defs,
        IReadOnlyDictionary<StringName, AchievementDefinition> typed_achievement_defs,
        IReadOnlyDictionary<StringName, ItemDefinition> typed_item_defs,
        IReadOnlyDictionary<StringName, TraitDefinition> typed_trait_defs,
        ProgressionIdentityCatalogData progression_identity_catalog
    )
    {
        _skill_definition_index = typed_skill_definitions != null
            ? new Dictionary<StringName, SkillDefinition>(typed_skill_definitions)
            : new Dictionary<StringName, SkillDefinition>();
        _profession_def_index = typed_profession_defs != null
            ? new Dictionary<StringName, ProfessionDefinition>(typed_profession_defs)
            : new Dictionary<StringName, ProfessionDefinition>();
        _achievement_def_index = typed_achievement_defs != null
            ? new Dictionary<StringName, AchievementDefinition>(typed_achievement_defs)
            : new Dictionary<StringName, AchievementDefinition>();
        _item_def_index = typed_item_defs != null
            ? new Dictionary<StringName, ItemDefinition>(typed_item_defs)
            : new Dictionary<StringName, ItemDefinition>();
        _trait_def_index = typed_trait_defs != null
            ? new Dictionary<StringName, TraitDefinition>(typed_trait_defs)
            : new Dictionary<StringName, TraitDefinition>();
        _progression_identity_catalog =
            progression_identity_catalog ?? new ProgressionIdentityCatalogData();
        _setup_character_management();
    }

    internal BattleSimFormalRuntimeStartInput BuildRuntimeStartInput(
        BattleRuntimeModule runtime,
        Godot.Collections.Dictionary base_context
    )
    {
        _restore_all_members_to_full_hp();
        StringNameList savedActiveIds = party_state.active_member_ids?.Duplicate() ?? new();
        Dictionary<string, object> contextPlain = RuntimePlainPayload.NormalizeDictionary(
            base_context,
            "BattleSimFormalCombatFixture.BuildRuntimeStartInput.base"
        );
        contextPlain.Remove("battle_party");
        contextPlain.Remove("enemy_units");
        contextPlain.Remove("ally_member_ids");
        GodotProjectionLease<Godot.Collections.Dictionary> contextLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                contextPlain,
                "battle-sim-formal-context",
                LifetimeDomain.Request,
                "BattleSimFormalCombatFixture.BuildRuntimeStartInput.context"
            );
        bool completed = false;
        try
        {
            Godot.Collections.Dictionary context = contextLease.Value;
            context["battle_party"] = contextLease.Own(
                new Godot.Collections.Array(),
                "BattleSimFormalCombatFixture.BuildRuntimeStartInput.battle_party"
            );
            context["ally_member_ids"] = ProjectStringNames(
                contextLease,
                ally_member_ids,
                "BattleSimFormalCombatFixture.BuildRuntimeStartInput.ally_member_ids"
            );
            context["validate_spawn_reachability"] = true;
            context["validate_bidirectional_spawn_reachability"] = true;
            context["enforce_opposing_spawn_sides"] = true;
            party_state.active_member_ids = new StringNameList(hostile_member_ids);
            Godot.Collections.Dictionary hostileContext = RuntimePayloadCopy.DictionaryInto(
                contextLease,
                context,
                "BattleSimFormalCombatFixture.BuildRuntimeStartInput.hostile_context"
            );
            hostileContext["battle_party"] = contextLease.Own(
                new Godot.Collections.Array(),
                "BattleSimFormalCombatFixture.BuildRuntimeStartInput.hostile_battle_party"
            );
            hostileContext["ally_member_ids"] = ProjectStringNames(
                contextLease,
                hostile_member_ids,
                "BattleSimFormalCombatFixture.BuildRuntimeStartInput.hostile_member_ids"
            );
            IReadOnlyList<BattleUnitState> hostileUnits =
                runtime?._unit_factory?.BuildAllyUnits(
                    party_state,
                    hostileContext,
                    contextRole: BattleStartContextReferenceRole.BorrowedForSynchronousStart
                )
                ?? System.Array.Empty<BattleUnitState>();
            foreach (BattleUnitState unit in hostileUnits)
                _apply_unit_runtime_metadata(unit, "hostile");
            BattleStartUnitRoster enemyUnitRoster = new(enemyUnits: hostileUnits);
            party_state.active_member_ids = new StringNameList(ally_member_ids);
            if (party_state.active_member_ids.Count == 0)
                party_state.active_member_ids = savedActiveIds.Duplicate();
            var startInput = new BattleSimFormalRuntimeStartInput(
                contextLease,
                enemyUnitRoster
            );
            completed = true;
            return startInput;
        }
        catch
        {
            contextLease.Dispose();
            throw;
        }
        finally
        {
            if (!completed && party_state != null)
                party_state.active_member_ids = savedActiveIds;
        }
    }

    private static Godot.Collections.Array ProjectStringNames(
        GodotProjectionLease<Godot.Collections.Dictionary> lease,
        IEnumerable<StringName> values,
        string reason
    )
    {
        Godot.Collections.Array result = lease.Own(
            new Godot.Collections.Array(),
            reason
        );
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
            result.Add(value);
        return result;
    }

    public void ApplyStartedBattleMetadata(BattleState state)
    {
        if (state == null)
            return;
        foreach (BattleUnitState unitState in state.Units())
        {
            if (unitState == null)
                continue;
            StringName memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
            if (ally_member_ids.Contains(memberId))
                _apply_unit_runtime_metadata(unitState, "player");
            else if (hostile_member_ids.Contains(memberId))
                _apply_unit_runtime_metadata(unitState, "hostile");
        }
    }

    public PartyState GetPartyState() => party_state;

    public PartyMemberState GetMemberState(StringName member_id) =>
        party_state?.GetMemberState(member_id);

    public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() =>
        _item_def_index;

    public bool HasItemDefCatalog() => _item_def_index.Count > 0;

    public ItemDefinition GetItemDef(StringName item_id) =>
        character_management?.GetItemDef(item_id) ?? GetIndexedItemDef(item_id);

    public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
        StringName member_id,
        EquipmentState equipment_view
    ) =>
        character_management?.GetMemberAttributeSnapshotForEquipmentView(
            member_id,
            equipment_view
        );

    public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
        StringName member_id,
        EquipmentState equipment_view
    ) => character_management?.GetMemberWeaponProjectionForEquipmentViewTyped(
            member_id,
            equipment_view
        ) ?? new WeaponProjection();

    public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
        StringName member_id,
        EquipmentState equipment_view
    ) => character_management?.BuildEffectiveTraitProjectionForEquipmentView(
            member_id,
            equipment_view
        ) ?? BattleEffectiveTraitProjection.Empty;

    PassiveSourceContext IBattleRuntimeCharacterGateway.BuildPassiveSourceContext(
        StringName member_id,
        UnitProgress progression_state
    ) => BuildPassiveSourceContext(member_id, progression_state);

    internal PassiveSourceContext BuildPassiveSourceContext(
        StringName member_id,
        UnitProgress progression_state = null
    ) =>
        character_management?.BuildPassiveSourceContext(member_id, progression_state);

    public CharacterProgressionDelta PromoteProfession(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    ) => character_management?.PromoteProfession(member_id, profession_id, selection)
        ?? new CharacterProgressionDelta { member_id = member_id };

    public BattleResourceCommitResult CommitBattleResources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
    )
    {
        PartyMemberState memberState = GetMemberState(member_id);
        if (memberState == null)
            return BattleResourceCommitResult.Failure("member_not_found", member_id);
        memberState.SetVitals(current_hp, current_mp, current_aura);
        return BattleResourceCommitResult.Success(member_id);
    }

    public ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(
        StringName member_id,
        IReadOnlyCollection<StringName> consumed_setup_ids
    )
    {
        var writebackService = new CharacterBattleWritebackService();
        writebackService.Setup(party_state, null, null);
        return writebackService.ValidateContingencyConsumedSetups(member_id, consumed_setup_ids);
    }

    public ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
        StringName member_id,
        IReadOnlyCollection<StringName> consumed_setup_ids
    )
    {
        var writebackService = new CharacterBattleWritebackService();
        writebackService.Setup(party_state, null, null);
        return writebackService.CommitContingencyConsumedSetups(member_id, consumed_setup_ids);
    }

    public void CommitBattleDeath(StringName member_id)
    {
        PartyMemberState memberState = GetMemberState(member_id);
        if (memberState != null)
            memberState.MarkDead();
    }

    public int FlushAfterBattle() => (int)Error.Ok;

    public CharacterProgressionDelta GrantBattleMastery(
        StringName member_id,
        StringName skill_id,
        int amount
    )
    {
        _record_mastery(skill_id, amount);
        var delta = new CharacterProgressionDelta();
        delta.member_id = member_id;
        delta.AddMasteryChange(
            new CharacterMasteryChangeFact(
                skill_id,
                skill_id.ToString(),
                amount,
                "battle",
                "battle",
                ""
            )
        );
        return delta;
    }

    public CharacterProgressionDelta GrantSkillMasteryFromSource(
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
        delta.AddMasteryChange(
            new CharacterMasteryChangeFact(
                skill_id,
                skill_id.ToString(),
                amount,
                source_type,
                source_label,
                reason_text
            )
        );
        return delta;
    }

    public IReadOnlyList<StringName> RecordAchievementEvent(
        StringName member_id,
        StringName event_type
    ) => System.Array.Empty<StringName>();

    public IReadOnlyList<StringName> RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount
    ) => System.Array.Empty<StringName>();

    public IReadOnlyList<StringName> RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id
    ) => System.Array.Empty<StringName>();

    public IReadOnlyList<StringName> RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id,
        Godot.Collections.Dictionary meta
    ) => System.Array.Empty<StringName>();

    public PendingCharacterReward BuildPendingSkillMasteryReward(
        StringName member_id,
        StringName source_type,
        string source_label,
        IEnumerable<PendingCharacterRewardEntry> entry_options,
        string summary_text
    ) => null;

    private void _reset_roster()
    {
        _dispose_roster_state();
        party_state = new PartyState();
        party_state.version = 3;
        party_state.gold = 0;
        ally_member_ids.Clear();
        hostile_member_ids.Clear();
        _ai_brain_by_member_id.Clear();
        _ai_state_by_member_id.Clear();
        _roster_options = new BattleSimFormalRosterOptionsData();
        charge_mastery = 0;
        heavy_mastery = 0;
        aimed_mastery = 0;
        multishot_mastery = 0;
        basic_mastery = 0;
        _attribute_roll_rng = new RuntimeRandom(DEFAULT_ATTRIBUTE_ROLL_SEED);
        _hp_roll_rng ??= new RuntimeRandom(DEFAULT_ATTRIBUTE_ROLL_SEED + HP_ROLL_SEED_OFFSET);
    }

    private void _dispose_roster_state()
    {
        character_management?.Dispose();
        character_management = null;
        DisposePartyState(party_state);
        party_state = null;
        ally_member_ids.Clear();
        hostile_member_ids.Clear();
        _ai_brain_by_member_id.Clear();
        _ai_state_by_member_id.Clear();
    }

    private static void DisposePartyState(PartyState state)
    {
        if (state == null)
            return;
        foreach (PartyMemberState memberState in state.GetMemberStates())
            DisposePartyMemberState(memberState);
        DisposeWarehouseState(state.warehouse_state);
        state.member_states.Clear();
        state.active_member_ids.Clear();
        state.reserve_member_ids.Clear();
        state.pending_character_rewards.Clear();
        state.ClearQuestJournal();
    }

    private static void DisposePartyMemberState(PartyMemberState memberState)
    {
        if (memberState == null)
            return;
        DisposeUnitProgress(memberState.progression);
        DisposeEquipmentState(memberState.equipment_state);
        memberState.active_stage_advancement_modifier_ids.Clear();
    }

    private static void DisposeUnitProgress(UnitProgress progress)
    {
        if (progress == null)
            return;
        foreach (UnitProfessionProgress professionProgress in progress.ProfessionsTyped.Values)
            professionProgress.promotion_history.Clear();
        progress.SetPendingProfessionChoices(null);
    }

    private static void DisposeEquipmentState(EquipmentState equipmentState)
    {
        if (equipmentState == null)
            return;
        foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            equipmentState.ClearEntrySlot(entrySlotId);
    }

    private static void DisposeWarehouseState(WarehouseState warehouseState)
    {
        if (warehouseState == null)
            return;
        warehouseState.stacks.Clear();
        warehouseState.equipment_instances.Clear();
    }

    private void _build_mixed_2s1a_roster()
    {
        var sword_attrs = _attrs(14, 12, 14, 10, 8, 10);
        var archer_attrs = _attrs(10, 16, 12, 14, 8, 10);
        BattleSimFormalSkillConfigData[] sword_skills =
        {
            _sk("charge", 1, false),
            _sk("warrior_heavy_strike", 1, false),
        };
        BattleSimFormalSkillConfigData[] archer_skills =
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
        BattleSimFormalSkillConfigData[] elite_sword_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("charge", 7, true),
            _sk("warrior_heavy_strike", 5, true),
        };
        BattleSimFormalSkillConfigData[] elite_archer_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("archer_aimed_shot", 3, true),
            _sk("archer_multishot", 7, true),
        };
        BattleSimFormalSkillConfigData[] elite_mage_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("mage_fireball", 7, true),
            _sk("mage_cone_of_cold", 7, true),
            _sk("mage_blink", 7, true),
            _sk("mage_gust_of_wind", 7, true),
            _sk("mage_chain_lightning", 7, true),
        };
        BattleSimFormalSkillConfigData[] hostile_sword_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("charge", 1, false),
            _sk("warrior_heavy_strike", 1, false),
        };
        BattleSimFormalSkillConfigData[] hostile_archer_skills =
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

    // 6v12 variant: the elite mage + 1 elite archer are replaced by 2 elite archers
    // (player = 4 sword + 2 archer, no mage). Reuses the exact 6v12 stat rolls / brains;
    // dropping the mage removes ~2/3 of the player's damage, making the matchup far less
    // of a blowout — a tuning arena with headroom. The 6v12 baseline is untouched.
    private void _build_mixed_6v12_two_archer_roster()
    {
        BattleSimFormalSkillConfigData[] elite_sword_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("charge", 7, true),
            _sk("warrior_heavy_strike", 5, true),
        };
        BattleSimFormalSkillConfigData[] elite_archer_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("archer_aimed_shot", 3, true),
            _sk("archer_multishot", 7, true),
        };
        BattleSimFormalSkillConfigData[] hostile_sword_skills =
        {
            _sk("basic_attack", 0, false),
            _sk("charge", 1, false),
            _sk("warrior_heavy_strike", 1, false),
        };
        BattleSimFormalSkillConfigData[] hostile_archer_skills =
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
        for (int index = 0; index < 2; index++)
            _add_member(
                $"elite_archer_{index}",
                $"Elite Archer {index}",
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
        for (int index = 0; index < 8; index++)
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
        for (int index = 0; index < 4; index++)
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
        BattleSimFormalCreationAttributesData attrs,
        int action_threshold,
        IReadOnlyList<BattleSimFormalSkillConfigData> skill_configs,
        StringName profession_id,
        int profession_rank,
        StringName weapon_item_id,
        StringName body_armor_item_id,
        StringName ai_brain_id,
        StringName ai_state_id
    )
    {
        IReadOnlyDictionary<string, object> payload = _build_creation_payload(
            display_name,
            attrs,
            action_threshold
        );
        using GodotProjectionLease<Godot.Collections.Dictionary> payloadLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                payload,
                $"battle-sim-formal-member:{member_id}",
                LifetimeDomain.Request,
                $"BattleSimFormalCombatFixture._add_member.{member_id}"
            );
        var member_state = CharacterCreationService.CreateMemberFromCharacterCreationPayloadForIdentityCatalog(
            member_id,
            payloadLease.Value,
            _progression_identity_catalog
        );
        member_state.faction_id = faction_id;
        member_state.ControlModeKind = BattleUnitControlMode.Ai;
        _apply_skills(member_state, skill_configs);
        _apply_profession_rank(
            member_state,
            profession_id,
            profession_rank,
            _collect_core_skill_ids(skill_configs)
        );
        _equip_member(member_state, weapon_item_id, body_armor_item_id);
        party_state.SetMemberState(member_state);
        if ((string)faction_id == "hostile")
            hostile_member_ids.Add(member_id);
        else
            ally_member_ids.Add(member_id);
        _ai_brain_by_member_id[member_id] = ai_brain_id;
        _ai_state_by_member_id[member_id] = ai_state_id;
    }

    private void _set_member_mp_max(StringName member_id, int mp_max)
    {
        var member_state = party_state.GetMemberState(member_id);
        var attributes = _unit_base_attributes(member_state);
        if (attributes == null)
            return;
        attributes.SetAttributeValue(AttributeService.MP_MAX, mp_max);
        member_state.SetCurrentMp(mp_max);
    }

    private void _finalize_roster_identity()
    {
        if (party_state == null)
            return;
        party_state.active_member_ids = new StringNameList(ally_member_ids);
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
        StringName member_id = option_key switch
        {
            ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID => _roster_options.MainCharacterMemberId,
            ROSTER_OPTION_LEADER_MEMBER_ID => _roster_options.LeaderMemberId,
            _ => "",
        };
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
        long attributeSeed = System.Math.Max(
            _roster_options.AttributeRollSeed,
            DEFAULT_ATTRIBUTE_ROLL_SEED
        );
        _attribute_roll_rng.Reseed(attributeSeed);
        _hp_roll_rng.Reseed(attributeSeed + HP_ROLL_SEED_OFFSET);
    }

    private BattleSimFormalCreationAttributesData _roll_creation_attributes() =>
        new(
            _roll_creation_attribute_value(),
            _roll_creation_attribute_value(),
            _roll_creation_attribute_value(),
            _roll_creation_attribute_value(),
            _roll_creation_attribute_value(),
            _roll_creation_attribute_value()
        );

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
        var member_state = party_state.GetMemberState(party_state.main_character_member_id);
        if (member_state?.progression == null)
            return;
        var attribute_service = new AttributeService();
        attribute_service.Setup(member_state.progression);
        var creation_service = new CharacterCreationService();
        int reroll_count = _roster_options.MainCharacterRerollCount;
        if (!creation_service.BakeHiddenLuckAtBirth(attribute_service, reroll_count))
            GameLog.Warning(
                $"BattleSimFormalCombatFixture: failed to bake reroll luck for main character {party_state.main_character_member_id}.",
                "battlesim.fixture.bake_luck_failed",
                "battlesim"
            );
    }

    private void _apply_skills(
        PartyMemberState member_state,
        IReadOnlyList<BattleSimFormalSkillConfigData> skill_configs
    )
    {
        var unit_progress = _unit_progress(member_state);
        if (unit_progress == null)
            return;
        var progression_service = new ProgressionService();
        progression_service.SetupDefinitions(
            member_state.progression,
            _skill_definition_index,
            _profession_def_index
        );
        foreach (var skill_config in skill_configs)
        {
            if (skill_config == null)
                continue;
            var skill_id = skill_config.SkillId;
            int target_level = Mathf.Max(skill_config.Level, 0);
            bool is_core = skill_config.IsCore;
            if ((string)skill_id == "")
                continue;
            var skill_progress = unit_progress.GetSkillProgress(skill_id);
            if (skill_progress == null || !skill_progress.is_learned)
                progression_service.LearnSkill(skill_id);
            var skill_definition = _get_skill_definition(skill_id);
            if (is_core)
            {
                progression_service.SetSkillCore(skill_id, true);
                skill_progress = unit_progress.GetSkillProgress(skill_id);
                _unlock_fixture_core_skill_level_cap(
                    unit_progress,
                    skill_progress,
                    skill_definition,
                    target_level
                );
            }
            int mastery_amount = _calculate_mastery_for_level(skill_definition, target_level);
            if (mastery_amount > 0)
                progression_service.GrantSkillMastery(skill_id, mastery_amount, "training");
            if (is_core)
            {
                progression_service.SetSkillCore(skill_id, true);
                _apply_core_max_growth(member_state, skill_id, target_level);
            }
        }
        progression_service.RefreshRuntimeState();
    }

    private void _unlock_fixture_core_skill_level_cap(
        UnitProgress unit_progress,
        UnitSkillProgress skill_progress,
        SkillDefinition skill_definition,
        int target_level
    )
    {
        if (unit_progress == null || skill_progress == null || skill_definition == null)
            return;
        int non_core_max_level = Mathf.Max(skill_definition.NonCoreMaxLevel, 0);
        if (non_core_max_level <= 0 || target_level <= non_core_max_level)
            return;
        skill_progress.is_level_trigger_active = false;
        skill_progress.is_level_trigger_locked = true;
        if (!unit_progress.HasLockedLevelTriggerSkillId(skill_progress.skill_id))
            unit_progress.AddLockedLevelTriggerSkillId(skill_progress.skill_id);
        if (unit_progress.active_level_trigger_core_skill_id == skill_progress.skill_id)
            unit_progress.active_level_trigger_core_skill_id = "";
        unit_progress.SetSkillProgress(skill_progress);
    }

    private void _apply_core_max_growth(
        PartyMemberState member_state,
        StringName skill_id,
        int target_level
    )
    {
        var skill_definition = _get_skill_definition(skill_id);
        var unit_progress = _unit_progress(member_state);
        var skill_progress = unit_progress?.GetSkillProgress(skill_id);
        if (skill_definition == null || skill_progress == null)
            return;
        if (skill_progress.core_max_growth_claimed)
            return;
        if (target_level < skill_definition.MaxLevel)
            return;
        IReadOnlyDictionary<StringName, int> growth = skill_definition.AttributeGrowthProgress;
        if (growth.Count == 0)
        {
            skill_progress.core_max_growth_claimed = true;
            unit_progress.SetSkillProgress(skill_progress);
            return;
        }
        var growth_service = new AttributeGrowthService();
        growth_service.Setup(member_state.progression);
        foreach (KeyValuePair<StringName, int> entry in growth)
        {
            growth_service.ApplyAttributeProgressTyped(
                entry.Key,
                entry.Value,
                "battle_sim_fixture"
            );
        }
        skill_progress.core_max_growth_claimed = true;
        unit_progress.SetSkillProgress(skill_progress);
    }

    private void _apply_profession_rank(
        PartyMemberState member_state,
        StringName profession_id,
        int rank,
        IEnumerable<StringName> core_skill_ids
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
            profession_progress.AddCoreSkill(skill_id);
            var sp = unit_progress.GetSkillProgress(skill_id);
            if (sp != null)
            {
                sp.is_core = true;
                sp.assigned_profession_id = profession_id;
                unit_progress.SetSkillProgress(sp);
            }
        }
        _apply_profession_granted_skills(member_state, profession_id, rank, profession_progress);
        unit_progress.SetProfessionProgress(profession_progress);
        int hp_gain_total = _calculate_profession_hp_gain_total(member_state, profession_id, rank);
        var attributes = unit_progress.unit_base_attributes;
        attributes.SetAttributeValue(
            AttributeService.HP_MAX,
            attributes.GetAttributeValue(AttributeService.HP_MAX) + hp_gain_total
        );
        member_state.SetCurrentHp(attributes.GetAttributeValue(AttributeService.HP_MAX));
        var ps = new ProgressionService();
        ps.SetupDefinitions(
            member_state.progression,
            _skill_definition_index,
            _profession_def_index
        );
        ps.RefreshRuntimeState();
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
        var profession_def = _get_profession_def(profession_id);
        if (profession_def == null)
            return;
        for (int target_rank = 1; target_rank <= rank; target_rank++)
        {
            var granted_skills = profession_def.GetGrantedSkillsForRank(target_rank);
            if (granted_skills == null)
                continue;
            foreach (ProfessionGrantedSkillDefinition granted_skill in granted_skills)
            {
                if (granted_skill == null || (string)granted_skill.SkillId == "")
                    continue;
                profession_progress.AddGrantedSkill(granted_skill.SkillId);
                var sp = unit_progress.GetSkillProgress(granted_skill.SkillId);
                if (sp == null)
                {
                    sp = new UnitSkillProgress();
                    sp.skill_id = granted_skill.SkillId;
                }
                sp.is_learned = true;
                if ((string)sp.profession_granted_by == "")
                    sp.profession_granted_by = profession_id;
                sp.granted_source_type = UnitSkillProgress.ToStringName(
                    UnitSkillGrantSourceType.Profession
                );
                sp.granted_source_id = profession_id;
                unit_progress.SetSkillProgress(sp);
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
        var profession_def = _get_profession_def(profession_id);
        if (profession_def == null)
            return 0;
        int constitution = attributes.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int hit_die_sides = Mathf.Max(profession_def.HitDieSides, 1);
        int total = 0;
        for (int ri = 0; ri < Mathf.Max(rank, 0); ri++)
        {
            int hp_roll = _hp_roll_rng.RandiRange(1, hit_die_sides);
            total += ProgressionService.CalculateProfessionHitPointGain(
                hp_roll,
                constitution
            );
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
                EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
                true,
                false
            ) || equipped_any;
        equipped_any =
            _equip_item_into_slot(
                equipment_state,
                member_state.member_id,
                body_armor_item_id,
                EquipmentRules.ToStringName(EquipmentSlotKind.Body),
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
        var item_def = GetIndexedItemDef(item_id);
        if (item_def == null || !item_def.IsEquipment())
            return false;
        if (require_weapon && !item_def.IsWeapon())
            return false;
        if (require_armor && !item_def.IsArmor())
            return false;
        var slot_ids = item_def.GetEquipmentSlotIdsTyped();
        if (slot_ids == null || !slot_ids.Contains(entry_slot_id))
            return false;
        List<StringName> occupied_slots = item_def.GetFinalOccupiedSlotIdsTyped(entry_slot_id);
        var instance_id = $"sim_{member_id}_{item_id}";
        var equipment_instance = EquipmentInstanceState.CreateInstance(item_id, instance_id);
        return equipment_state.SetEquippedEntry(
            entry_slot_id,
            item_id,
            occupied_slots,
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
            _skill_definition_index,
            _profession_def_index,
            _achievement_def_index,
            _item_def_index,
            new Dictionary<StringName, QuestDefinition>(),
            _trait_def_index,
            null,
            _progression_identity_catalog
        );
    }

    private void _restore_all_members_to_full_hp()
    {
        if (party_state == null || character_management == null)
            return;
        foreach (var mkv in party_state.member_states.Keys)
        {
            var member_id = ProgressionDataUtils.to_string_name(mkv);
            var member_state = party_state.GetMemberState(member_id);
            if (member_state == null)
                continue;
            var snapshot = character_management.GetMemberAttributeSnapshotForEquipmentView(
                member_id,
                member_state.equipment_state
            );
            member_state.SetCurrentHp(Mathf.Max(
                snapshot?.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)) ?? 1,
                1
            ));
        }
    }

    private void _apply_unit_runtime_metadata(
        BattleUnitState unit_state,
        StringName fallback_faction_id
    )
    {
        if (unit_state == null)
            return;
        var member_id = ProgressionDataUtils.to_string_name(unit_state.source_member_id);
        unit_state.faction_id = fallback_faction_id;
        unit_state.ControlModeKind = BattleUnitControlMode.Ai;
        unit_state.ai_brain_id =
            _ai_brain_by_member_id.TryGetValue(member_id, out StringName aiBrainId)
                ? aiBrainId
                : ProgressionDataUtils.to_string_name(unit_state.ai_brain_id);
        unit_state.ai_state_id =
            _ai_state_by_member_id.TryGetValue(member_id, out StringName aiStateId)
                ? aiStateId
                : ProgressionDataUtils.to_string_name(unit_state.ai_state_id);
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

    private int _calculate_mastery_for_level(SkillDefinition skill_definition, int target_level)
    {
        if (skill_definition == null)
            return 0;
        int total = 0;
        for (int level = 0; level < target_level; level++)
            total += Mathf.Max(skill_definition.GetMasteryRequiredForLevel(level), 0);
        return total;
    }

    private List<StringName> _collect_core_skill_ids(
        IReadOnlyList<BattleSimFormalSkillConfigData> skill_configs
    )
    {
        var result = new List<StringName>();
        foreach (var sc in skill_configs)
        {
            if (sc == null || !sc.IsCore)
                continue;
            var sid = sc.SkillId;
            if ((string)sid != "")
                result.Add(sid);
        }
        return result;
    }

    private Dictionary<string, object> _build_creation_payload(
        string display_name,
        BattleSimFormalCreationAttributesData attrs,
        int action_threshold
    )
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
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
            { "strength", attrs?.Strength ?? 10 },
            { "agility", attrs?.Agility ?? 10 },
            { "constitution", attrs?.Constitution ?? 10 },
            { "perception", attrs?.Perception ?? 10 },
            { "intelligence", attrs?.Intelligence ?? 10 },
            { "willpower", attrs?.Willpower ?? 10 },
        };
        if (action_threshold > 0)
            payload["action_threshold"] = action_threshold;
        return payload;
    }

    private static BattleSimFormalCreationAttributesData _attrs(
        int strength,
        int agility,
        int constitution,
        int perception,
        int intelligence,
        int willpower
    ) =>
        new(strength, agility, constitution, perception, intelligence, willpower);

    private static BattleSimFormalSkillConfigData _sk(
        string skill_id,
        int level,
        bool is_core
    ) => new(ProgressionDataUtils.to_string_name(skill_id), level, is_core);

    private SkillDefinition _get_skill_definition(StringName skill_id) =>
        _skill_definition_index.TryGetValue(skill_id, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;

    private ProfessionDefinition _get_profession_def(StringName profession_id) =>
        _profession_def_index.TryGetValue(profession_id, out ProfessionDefinition professionDef)
            ? professionDef
            : null;

    private ItemDefinition GetIndexedItemDef(StringName item_id) =>
        _item_def_index.TryGetValue(item_id, out ItemDefinition itemDefinition)
            ? itemDefinition
            : null;

}
