using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class CharacterManagementModule : IBattleRuntimeCharacterGateway, IDisposable
{
    private static readonly StringName RewardTypeAchievement = "achievement";
    private static readonly StringName RewardTypeQuest = "quest";

    private static readonly Dictionary<StringName, int> RewardEntryOrder = new()
    {
        ["knowledge_unlock"] = 0,
        ["skill_unlock"] = 1,
        ["skill_mastery"] = 2,
        ["attribute_progress"] = 3,
        ["attribute_delta"] = 4,
    };

    internal sealed class LearnSkillOptionsData
    {
        public bool ConfirmPracticeReplacement { get; }

        public LearnSkillOptionsData(bool confirmPracticeReplacement = false)
        {
            ConfirmPracticeReplacement = confirmPracticeReplacement;
        }
    }

    private sealed class AttributeGrowthEntryData
    {
        public readonly StringName AttributeId;
        public readonly int Amount;

        public AttributeGrowthEntryData(StringName attributeId, int amount)
        {
            AttributeId = attributeId;
            Amount = amount;
        }
    }

    private sealed class CharacterTraitGatewayAdapter
        : CharacterTraitService.ICharacterTraitGateway
    {
        private readonly CharacterManagementModule _owner;

        public CharacterTraitGatewayAdapter(CharacterManagementModule owner)
        {
            _owner = owner;
        }

        public RaceDef GetRaceDefForTraitAggregation(StringName memberId) =>
            _owner?.GetRaceDefForMember(memberId);

        public SubraceDef GetSubraceDefForTraitAggregation(StringName memberId) =>
            _owner?.GetSubraceDefForMember(memberId);

        public BloodlineDef GetBloodlineDefForTraitAggregation(StringName memberId) =>
            _owner?.GetBloodlineDefForMember(memberId);

        public BloodlineStageDef GetBloodlineStageDefForTraitAggregation(StringName memberId) =>
            _owner?.GetBloodlineStageDefForMember(memberId);

        public AscensionDef GetAscensionDefForTraitAggregation(StringName memberId) =>
            _owner?.GetAscensionDefForMember(memberId);

        public AscensionStageDef GetAscensionStageDefForTraitAggregation(StringName memberId) =>
            _owner?.GetAscensionStageDefForMember(memberId);

        public PartyMemberState GetMemberStateForTraitAggregation(StringName memberId) =>
            _owner?.GetMemberState(memberId);

        public EquipmentState GetEquipmentStateForTraitAggregation(StringName memberId) =>
            _owner?.GetMemberState(memberId)?.equipment_state;

        public ItemDef GetItemDefForTraitAggregation(StringName itemId) =>
            _owner?.GetItemDef(itemId);
    }

    private sealed class AchievementProgressSummaryEntry
    {
        public readonly StringName AchievementId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int CurrentValue;
        public readonly int Threshold;
        public readonly float ProgressRatio;

        public AchievementProgressSummaryEntry(
            StringName achievementId,
            string displayName,
            string description,
            int currentValue,
            int threshold
        )
        {
            AchievementId = achievementId;
            DisplayName = displayName ?? "";
            Description = description ?? "";
            CurrentValue = currentValue;
            Threshold = threshold;
            ProgressRatio = (float)currentValue / Mathf.Max(threshold, 1);
        }

    }

    public sealed class DailyPracticeGrowthResult
    {
        private readonly List<StringName> _changedMemberIds;
        public bool Applied { get; }
        public int DaysElapsed { get; }
        public GStringNameArray ChangedMemberIds => ToStringNameArray(_changedMemberIds);

        public DailyPracticeGrowthResult(
            bool applied,
            int daysElapsed,
            IEnumerable<StringName> changedMemberIds
        )
        {
            Applied = applied;
            DaysElapsed = Mathf.Max(daysElapsed, 0);
            _changedMemberIds = CloneStringNameList(changedMemberIds);
        }

    }

    private PartyState _party_state = new();
    private ProgressionIdentityCatalogData _progression_identity_catalog = new();
    private bool _has_quest_def_catalog;
    private Dictionary<StringName, SkillDef> _skill_def_index = new();
    private Dictionary<StringName, ProfessionDef> _profession_def_index = new();
    private Dictionary<StringName, AchievementDef> _achievement_def_index = new();
    private Dictionary<StringName, ItemDef> _item_def_index = new();
    private Dictionary<StringName, QuestDef> _quest_def_index = new();
    private Dictionary<StringName, TraitDef> _trait_def_index = new();
    private Dictionary<StringName, RaceDef> _race_def_index = new();
    private Dictionary<StringName, SubraceDef> _subrace_def_index = new();
    private Dictionary<StringName, AgeProfileDef> _age_profile_def_index = new();
    private Dictionary<StringName, BloodlineDef> _bloodline_def_index = new();
    private Dictionary<StringName, BloodlineStageDef> _bloodline_stage_def_index = new();
    private Dictionary<StringName, AscensionDef> _ascension_def_index = new();
    private Dictionary<StringName, AscensionStageDef> _ascension_stage_def_index = new();
    private Dictionary<StringName, StageAdvancementModifier> _stage_advancement_modifier_index = new();
    private readonly BloodlineApplyService _bloodline_apply_service = new();
    private readonly AscensionApplyService _ascension_apply_service = new();
    private readonly StageAdvancementApplyService _stage_advancement_apply_service = new();
    private readonly PartyWarehouseService _party_warehouse_service = new();
    private readonly PartyContingencySetupService _party_contingency_setup_service = new();
    private readonly PartyEquipmentService _party_equipment_service = new();
    private readonly QuestProgressService _quest_progress_service = new();
    private readonly ProgressionServiceFactory _progression_service_factory = new();
    private readonly CharacterBattleWritebackService _battle_writeback_service = new();
    private CharacterTraitService _character_trait_service;
    private Func<StringName> _equipment_instance_id_allocator;
    private bool _disposed;

    public void Dispose()
    {
        DisposeManagedModule();
    }

    private void DisposeManagedModule()
    {
        if (_disposed)
            return;
        _disposed = true;
        GC.SuppressFinalize(this);
        _party_warehouse_service.Dispose();
        _party_equipment_service.Dispose();
        _quest_progress_service.Dispose();
        _battle_writeback_service.Clear();

        _party_state = null;
        _progression_identity_catalog = new ProgressionIdentityCatalogData();
        _has_quest_def_catalog = false;
        _skill_def_index.Clear();
        _profession_def_index.Clear();
        _achievement_def_index.Clear();
        _item_def_index.Clear();
        _quest_def_index.Clear();
        _trait_def_index.Clear();
        _race_def_index.Clear();
        _subrace_def_index.Clear();
        _age_profile_def_index.Clear();
        _bloodline_def_index.Clear();
        _bloodline_stage_def_index.Clear();
        _ascension_def_index.Clear();
        _ascension_stage_def_index.Clear();
        _stage_advancement_modifier_index.Clear();
        _character_trait_service = null;
        _equipment_instance_id_allocator = null;
    }

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            default,
            new ProgressionIdentityCatalogData()
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            new GDictionary(),
            new GDictionary(),
            default,
            new ProgressionIdentityCatalogData()
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            new GDictionary(),
            default,
            new ProgressionIdentityCatalogData()
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs,
        GDictionary quest_defs
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            quest_defs,
            default,
            new ProgressionIdentityCatalogData()
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs,
        GDictionary quest_defs,
        Func<StringName> equipment_instance_id_allocator
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            quest_defs,
            equipment_instance_id_allocator,
            new ProgressionIdentityCatalogData()
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs,
        GDictionary quest_defs,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            BuildQuestDefIndex(quest_defs),
            quest_defs != null && quest_defs.Count > 0,
            equipment_instance_id_allocator,
            progression_identity_catalog
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs,
        IReadOnlyDictionary<StringName, QuestDef> quest_defs,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            quest_defs,
            quest_defs != null && quest_defs.Count > 0,
            equipment_instance_id_allocator,
            progression_identity_catalog
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs,
        IReadOnlyDictionary<StringName, QuestDef> quest_defs,
        bool has_quest_def_catalog,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    ) =>
        setup(
            party_state,
            IndexContentDefs<SkillDef>(skill_defs, skillDef => skillDef.skill_id),
            IndexContentDefs<ProfessionDef>(profession_defs, professionDef => professionDef.profession_id),
            IndexAchievementDefs(achievement_defs),
            IndexContentDefs<ItemDef>(item_defs, itemDef => itemDef.item_id),
            quest_defs,
            has_quest_def_catalog,
            equipment_instance_id_allocator,
            progression_identity_catalog
        );

    public void setup(
        PartyState party_state,
        IReadOnlyDictionary<StringName, SkillDef> skill_defs,
        IReadOnlyDictionary<StringName, ProfessionDef> profession_defs,
        IReadOnlyDictionary<StringName, AchievementDef> achievement_defs,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, QuestDef> quest_defs,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            quest_defs,
            quest_defs != null && quest_defs.Count > 0,
            equipment_instance_id_allocator,
            progression_identity_catalog
        );

    public void setup(
        PartyState party_state,
        IReadOnlyDictionary<StringName, SkillDef> skill_defs,
        IReadOnlyDictionary<StringName, ProfessionDef> profession_defs,
        IReadOnlyDictionary<StringName, AchievementDef> achievement_defs,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, QuestDef> quest_defs,
        IReadOnlyDictionary<StringName, TraitDef> trait_defs,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            quest_defs,
            quest_defs != null && quest_defs.Count > 0,
            trait_defs,
            equipment_instance_id_allocator,
            progression_identity_catalog
        );

    public void setup(
        PartyState party_state,
        IReadOnlyDictionary<StringName, SkillDef> skill_defs,
        IReadOnlyDictionary<StringName, ProfessionDef> profession_defs,
        IReadOnlyDictionary<StringName, AchievementDef> achievement_defs,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, QuestDef> quest_defs,
        bool has_quest_def_catalog,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    ) =>
        setup(
            party_state,
            skill_defs,
            profession_defs,
            achievement_defs,
            item_defs,
            quest_defs,
            has_quest_def_catalog,
            new Dictionary<StringName, TraitDef>(),
            equipment_instance_id_allocator,
            progression_identity_catalog
        );

    public void setup(
        PartyState party_state,
        IReadOnlyDictionary<StringName, SkillDef> skill_defs,
        IReadOnlyDictionary<StringName, ProfessionDef> profession_defs,
        IReadOnlyDictionary<StringName, AchievementDef> achievement_defs,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, QuestDef> quest_defs,
        bool has_quest_def_catalog,
        IReadOnlyDictionary<StringName, TraitDef> trait_defs,
        Func<StringName> equipment_instance_id_allocator,
        ProgressionIdentityCatalogData progression_identity_catalog
    )
    {
        _party_state = party_state ?? new PartyState();
        _progression_identity_catalog = progression_identity_catalog ?? new ProgressionIdentityCatalogData();
        _skill_def_index = CloneContentDefIndex(skill_defs);
        _profession_def_index = CloneContentDefIndex(profession_defs);
        _achievement_def_index = CloneContentDefIndex(achievement_defs);
        _item_def_index = CloneContentDefIndex(item_defs);
        _has_quest_def_catalog = has_quest_def_catalog;
        _quest_def_index = CloneQuestDefIndex(quest_defs);
        _trait_def_index = CloneContentDefIndex(trait_defs);
        _race_def_index = new Dictionary<StringName, RaceDef>(_progression_identity_catalog.RaceDefs);
        _subrace_def_index = new Dictionary<StringName, SubraceDef>(_progression_identity_catalog.SubraceDefs);
        _age_profile_def_index = new Dictionary<StringName, AgeProfileDef>(_progression_identity_catalog.AgeProfileDefs);
        _bloodline_def_index = new Dictionary<StringName, BloodlineDef>(_progression_identity_catalog.BloodlineDefs);
        _bloodline_stage_def_index = new Dictionary<StringName, BloodlineStageDef>(_progression_identity_catalog.BloodlineStageDefs);
        _ascension_def_index = new Dictionary<StringName, AscensionDef>(_progression_identity_catalog.AscensionDefs);
        _ascension_stage_def_index = new Dictionary<StringName, AscensionStageDef>(_progression_identity_catalog.AscensionStageDefs);
        _stage_advancement_modifier_index = new Dictionary<StringName, StageAdvancementModifier>(
            _progression_identity_catalog.StageAdvancementDefs
        );
        _character_trait_service = new CharacterTraitService(
            _trait_def_index.Values,
            new CharacterTraitGatewayAdapter(this)
        );
        _equipment_instance_id_allocator = equipment_instance_id_allocator;
        _party_warehouse_service.Setup(
            _party_state,
            _item_def_index,
            _equipment_instance_id_allocator
        );
        SetupContingencySetupService(() => false);
        _party_equipment_service.Setup(
            _party_state,
            _item_def_index,
            _party_warehouse_service,
            _equipment_instance_id_allocator
        );
        _quest_progress_service.Setup(
            _party_state,
            _quest_def_index,
            _has_quest_def_catalog
        );
        _setup_battle_writeback_service();
        _setup_identity_apply_services();
    }

    public PartyState GetPartyState() => _party_state;

    public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() => _item_def_index;

    public bool HasItemDefCatalog() => _item_def_index.Count > 0;

    internal ContingencySetupMutationResult SaveContingencySetup(
        StringName member_id,
        ContingencyMatrixSetupState setup,
        Func<bool> battleMutationBlockedProvider = null
    )
    {
        SetupContingencySetupService(battleMutationBlockedProvider ?? (() => false));
        return _party_contingency_setup_service.SaveSetup(member_id, setup);
    }

    internal ContingencySetupMutationResult ChargeContingencySetup(
        StringName member_id,
        StringName setup_id,
        Func<bool> battleMutationBlockedProvider = null
    )
    {
        SetupContingencySetupService(battleMutationBlockedProvider ?? (() => false));
        return _party_contingency_setup_service.ChargeSetup(member_id, setup_id);
    }

    internal ContingencySetupMutationResult ClearContingencyCharge(
        StringName member_id,
        StringName setup_id,
        Func<bool> battleMutationBlockedProvider = null
    )
    {
        SetupContingencySetupService(battleMutationBlockedProvider ?? (() => false));
        return _party_contingency_setup_service.ClearCharge(member_id, setup_id);
    }

    internal ContingencySetupMutationResult BuildContingencySetupStatus(
        StringName member_id,
        StringName setup_id
    )
    {
        SetupContingencySetupService(() => false);
        return _party_contingency_setup_service.BuildStatus(member_id, setup_id);
    }

    public void SetPartyState(PartyState party_state)
    {
        _party_state = party_state ?? new PartyState();
        _party_warehouse_service.Setup(
            _party_state,
            _item_def_index,
            _equipment_instance_id_allocator
        );
        SetupContingencySetupService(() => false);
        _party_equipment_service.Setup(
            _party_state,
            _item_def_index,
            _party_warehouse_service,
            _equipment_instance_id_allocator
        );
        _quest_progress_service.Setup(
            _party_state,
            _quest_def_index,
            _has_quest_def_catalog
        );
        _setup_battle_writeback_service();
        _setup_identity_apply_services();
    }

    private void SetupContingencySetupService(Func<bool> battleMutationBlockedProvider)
    {
        _party_contingency_setup_service.Setup(
            _party_state,
            _party_warehouse_service,
            _skill_def_index,
            GetMemberAttributeSnapshot,
            battleMutationBlockedProvider ?? (() => false)
        );
    }

    private RaceDef GetRaceDefForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null ? null : GetRaceDef(member_state.race_id);
    }

    private SubraceDef GetSubraceDefForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null ? null : GetSubraceDef(member_state.subrace_id);
    }

    private BloodlineDef GetBloodlineDefForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null || member_state.bloodline_id == ""
            ? null
            : GetBloodlineDef(member_state.bloodline_id);
    }

    private BloodlineStageDef GetBloodlineStageDefForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null || member_state.bloodline_stage_id == ""
            ? null
            : GetBloodlineStageDef(member_state.bloodline_stage_id);
    }

    private AscensionDef GetAscensionDefForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null || member_state.ascension_id == ""
            ? null
            : GetAscensionDef(member_state.ascension_id);
    }

    private AscensionStageDef GetAscensionStageDefForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null || member_state.ascension_stage_id == ""
            ? null
            : GetAscensionStageDef(member_state.ascension_stage_id);
    }

    private AgeStageRule GetAgeStageRuleForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null)
            return null;
        var age_profile = GetAgeProfileDef(member_state.age_profile_id);
        if (age_profile == null)
            return null;
        var effective_stage_id = member_state.effective_age_stage_id;
        if (effective_stage_id == "")
            effective_stage_id = member_state.natural_age_stage_id;
        foreach (var stage_rule in age_profile.stage_rules)
            if (stage_rule != null && stage_rule.stage_id == effective_stage_id)
                return stage_rule;
        return null;
    }

    internal AttributeSourceContext build_attribute_source_context(StringName member_id) =>
        build_attribute_source_context(member_id, null);

    internal AttributeSourceContext build_attribute_source_context(
        StringName member_id,
        EquipmentState equipment_state_override
    )
    {
        var member_state = GetMemberState(member_id);
        var context = new AttributeSourceContext();
        if (member_state == null)
            return context;

        context.unit_progress = member_state.progression;
        context.skill_defs = new Dictionary<StringName, SkillDef>(_skill_def_index);
        context.profession_defs = new Dictionary<StringName, ProfessionDef>(_profession_def_index);
        context.race_def = GetRaceDefForMember(member_id);
        context.subrace_def = GetSubraceDefForMember(member_id);
        context.age_stage_rule = GetAgeStageRuleForMember(member_id);
        context.age_stage_source_type = member_state.effective_age_stage_source_type;
        context.age_stage_source_id = member_state.effective_age_stage_source_id;
        context.bloodline_def = GetBloodlineDefForMember(member_id);
        context.bloodline_stage_def = GetBloodlineStageDefForMember(member_id);
        context.ascension_def = GetAscensionDefForMember(member_id);
        context.ascension_stage_def = GetAscensionStageDefForMember(member_id);
        context.versatility_pick = member_state.versatility_pick;
        context.reserved_mp_max = member_state.GetTotalReservedMpMax();
        var equipment_state = equipment_state_override ?? member_state.equipment_state;
        context.equipment_state = _party_equipment_service.BuildAttributeModifiersTyped(
            equipment_state
        );
        if (_character_trait_service != null)
        {
            EffectiveTraitSet effectiveTraits = _character_trait_service.BuildEffectiveTraits(
                member_id,
                equipment_state
            );
            context.trait_attribute_modifiers =
                _character_trait_service.ResolveTraitAttributeModifiers(effectiveTraits);
        }
        context.stage_advancement_modifiers = _collect_active_stage_advancement_modifiers(
            member_state
        );
        return context;
    }

    PassiveSourceContext IBattleRuntimeCharacterGateway.BuildPassiveSourceContext(
        StringName member_id,
        UnitProgress progression_state
    ) => BuildPassiveSourceContext(member_id, progression_state);

    internal PassiveSourceContext BuildPassiveSourceContext(StringName member_id) =>
        BuildPassiveSourceContext(member_id, null);

    internal PassiveSourceContext BuildPassiveSourceContext(
        StringName member_id,
        UnitProgress progression_state
    )
    {
        var member_state = GetMemberState(member_id);
        var context = new PassiveSourceContext
        {
            member_state = member_state,
            unit_progress = progression_state ?? member_state?.progression,
        };
        context.race_def = GetRaceDefForMember(member_id);
        context.subrace_def = GetSubraceDefForMember(member_id);
        context.bloodline_def = GetBloodlineDefForMember(member_id);
        context.bloodline_stage_def = GetBloodlineStageDefForMember(member_id);
        context.ascension_def = GetAscensionDefForMember(member_id);
        context.ascension_stage_def = GetAscensionStageDefForMember(member_id);
        return context;
    }

    public GDictionary GetIdentitySummaryForMember(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null)
            return new GDictionary();
        var race_def = GetRaceDefForMember(member_id);
        var subrace_def = GetSubraceDefForMember(member_id);
        var bloodline_def = GetBloodlineDefForMember(member_id);
        var bloodline_stage_def = GetBloodlineStageDefForMember(member_id);
        var ascension_def = GetAscensionDefForMember(member_id);
        var ascension_stage_def = GetAscensionStageDefForMember(member_id);
        return new GDictionary
        {
            ["race_label"] = _identity_def_label(race_def, member_state.race_id),
            ["subrace_label"] = _identity_def_label(subrace_def, member_state.subrace_id),
            ["age_years"] = member_state.age_years,
            ["biological_age_years"] = member_state.biological_age_years,
            ["astral_memory_years"] = member_state.astral_memory_years,
            ["natural_age_stage_label"] = _get_age_stage_display_label(
                member_state.age_profile_id,
                member_state.natural_age_stage_id
            ),
            ["effective_age_stage_label"] = _get_age_stage_display_label(
                member_state.age_profile_id,
                member_state.effective_age_stage_id
            ),
            ["effective_age_stage_source_type"] = (string)
                member_state.effective_age_stage_source_type,
            ["effective_age_stage_source_id"] = (string)member_state.effective_age_stage_source_id,
            ["body_size"] = member_state.body_size,
            ["body_size_category"] = (string)member_state.body_size_category,
            ["bloodline_label"] = _identity_def_label(bloodline_def, member_state.bloodline_id),
            ["bloodline_stage_label"] = _identity_def_label(
                bloodline_stage_def,
                member_state.bloodline_stage_id
            ),
            ["ascension_label"] = _identity_def_label(ascension_def, member_state.ascension_id),
            ["ascension_stage_label"] = _identity_def_label(
                ascension_stage_def,
                member_state.ascension_stage_id
            ),
            ["trait_summary"] = _build_identity_trait_summary_lines(
                race_def,
                subrace_def,
                GetAgeStageRuleForMember(member_id),
                bloodline_def,
                bloodline_stage_def,
                ascension_def,
                ascension_stage_def
            ),
            ["damage_resistances"] = _collect_identity_damage_resistances(race_def, subrace_def),
            ["save_advantage_tags"] = _collect_identity_save_advantage_tags(race_def, subrace_def),
            ["racial_skill_lines"] = _build_identity_granted_skill_lines(
                race_def,
                subrace_def,
                bloodline_def,
                bloodline_stage_def,
                ascension_def,
                ascension_stage_def
            ),
        };
    }

    public bool ApplyBloodline(
        StringName member_id,
        StringName bloodline_id,
        StringName bloodline_stage_id
    )
    {
        var member_state = GetMemberState(member_id);
        if (
            !_bloodline_apply_service.ApplyBloodline(
                member_state,
                bloodline_id,
                bloodline_stage_id
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool RevokeBloodline(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        if (!_bloodline_apply_service.RevokeBloodline(member_state))
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool ApplyAscension(
        StringName member_id,
        StringName ascension_id,
        StringName ascension_stage_id,
        int current_world_step
    )
    {
        var member_state = GetMemberState(member_id);
        if (
            !_ascension_apply_service.ApplyAscension(
                member_state,
                ascension_id,
                ascension_stage_id,
                current_world_step
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool RevokeAscension(StringName member_id) => RevokeAscension(member_id, true);

    public bool RevokeAscension(StringName member_id, bool restore_original_race)
    {
        var member_state = GetMemberState(member_id);
        if (!_ascension_apply_service.RevokeAscension(member_state, restore_original_race))
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool AddStageAdvancementModifier(StringName member_id, StringName modifier_id)
    {
        var member_state = GetMemberState(member_id);
        if (
            !_stage_advancement_apply_service.AddStageAdvancementModifier(
                member_state,
                modifier_id
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool RemoveStageAdvancementModifier(StringName member_id, StringName modifier_id)
    {
        var member_state = GetMemberState(member_id);
        if (
            !_stage_advancement_apply_service.RemoveStageAdvancementModifier(
                member_state,
                modifier_id
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool GrantRacialSkill(
        StringName member_id,
        RacialGrantedSkill grant,
        StringName source_type,
        StringName source_id
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression == null)
            return false;
        return BuildProgressionService(member_state.progression)
            .GrantRacialSkill(grant, source_type, source_id);
    }

    public PartyMemberState GetMemberState(StringName member_id) =>
        _party_state?.GetMemberState(member_id);

    public void SetMemberState(PartyMemberState member_state)
    {
        _party_state ??= new PartyState();
        if (member_state != null)
            _party_state.SetMemberState(member_state);
    }

    public List<PendingCharacterReward> GetPendingCharacterRewards() =>
        ClonePendingCharacterRewardList(_party_state?.pending_character_rewards);

    public List<QuestState> GetActiveQuestStates() =>
        _quest_progress_service?.GetActiveQuestsTyped() ?? new List<QuestState>();

    public List<QuestState> GetClaimableQuestStates() =>
        _quest_progress_service?.GetClaimableQuestsTyped() ?? new List<QuestState>();

    public GStringNameArray GetClaimableQuestIds() =>
        ToStringNameArray(_quest_progress_service?.GetClaimableQuestIdsTyped());

    public GStringNameArray GetCompletedQuestIds() =>
        ToStringNameArray(_quest_progress_service?.GetCompletedQuestIdsTyped());

    public bool AcceptQuest(StringName quest_id) => AcceptQuest(quest_id, -1, false);

    public bool AcceptQuest(StringName quest_id, int world_step) =>
        AcceptQuest(quest_id, world_step, false);

    public bool AcceptQuest(StringName quest_id, int world_step, bool allow_reaccept)
    {
        if (_quest_progress_service == null)
            return false;
        var accepted = _quest_progress_service.AcceptQuest(quest_id, world_step, allow_reaccept);
        _party_state = _quest_progress_service.GetPartyState();
        return accepted;
    }

    public bool CompleteQuest(StringName quest_id) => CompleteQuest(quest_id, -1);

    public bool CompleteQuest(StringName quest_id, int world_step)
    {
        if (_quest_progress_service == null)
            return false;
        var completed = _quest_progress_service.CompleteQuest(quest_id, world_step);
        _party_state = _quest_progress_service.GetPartyState();
        return completed;
    }

    internal QuestSubmitItemResultData SubmitItemObjectiveTyped(
        StringName quest_id,
        StringName objective_id,
        int world_step
    )
    {
        var submission_preview = PreviewQuestSubmitItemObjective(quest_id, objective_id);
        if (!submission_preview.Ok)
        {
            return QuestSubmitItemResultData.Failed(
                string.IsNullOrEmpty(submission_preview.ErrorCode)
                    ? "objective_not_found"
                    : submission_preview.ErrorCode,
                submission_preview.ObjectiveId.ToString(),
                submission_preview.ItemId,
                submission_preview.TargetValue,
                submission_preview.RequiredQuantity
            );
        }

        var resolved_objective_id = submission_preview.ObjectiveId;
        var item_id = submission_preview.ItemId;
        var target_value = submission_preview.TargetValue;
        var required_quantity = submission_preview.RequiredQuantity;
        if (
            resolved_objective_id == ""
            || item_id == ""
            || target_value <= 0
            || required_quantity <= 0
        )
        {
            return QuestSubmitItemResultData.Failed(
                "invalid_submit_item_objective",
                resolved_objective_id.ToString(),
                item_id,
                target_value,
                required_quantity
            );
        }

        var warehouse_state_before =
            _party_state != null && _party_state.warehouse_state != null
                ? _party_state.warehouse_state.DuplicateState()
                : null;
        var withdraw_item_ids = _build_repeated_item_ids(item_id, required_quantity);
        var warehouse_commit = _party_warehouse_service.CommitBatchSwapTyped(
            withdraw_item_ids,
            new List<StringName>()
        );
        if (!warehouse_commit.Allowed)
        {
            string errorCode =
                warehouse_commit.ErrorCode == "warehouse_missing_item"
                    ? "submit_item_missing_inventory"
                    : "submit_item_commit_failed";
            return QuestSubmitItemResultData.Failed(
                errorCode,
                resolved_objective_id.ToString(),
                item_id,
                target_value,
                required_quantity
            );
        }

        QuestProgressApplyResultData summary = ApplyDirectQuestProgressTyped(
            quest_id,
            resolved_objective_id,
            required_quantity,
            world_step,
            true,
            target_value,
            new QuestProgressEventContextData
            {
                ItemId = item_id,
                SubmittedQuantity = required_quantity,
            }
        );
        if (!summary.ContainsProgressedQuest(quest_id))
        {
            if (_party_state != null)
                _party_state.warehouse_state = warehouse_state_before;
            _party_warehouse_service.Setup(
                _party_state,
                _item_def_index,
                _equipment_instance_id_allocator
            );
            _quest_progress_service.Setup(
                _party_state,
                _quest_def_index,
                _has_quest_def_catalog
            );
            return QuestSubmitItemResultData.Failed(
                "quest_progress_failed",
                resolved_objective_id.ToString(),
                item_id,
                target_value,
                required_quantity
            );
        }

        return QuestSubmitItemResultData.Success(
            item_id,
            resolved_objective_id.ToString(),
            target_value,
            required_quantity,
            required_quantity,
            summary.CloneAcceptedQuestIds(),
            summary.CloneProgressedQuestIds(),
            summary.CloneClaimableQuestIds(),
            summary.CloneCompletedQuestIds()
        );
    }

    internal QuestClaimResultData ClaimQuestRewardTyped(StringName quest_id, int world_step)
    {
        if (_party_state == null || quest_id == "")
            return QuestClaimResultData.Failed("invalid_quest_id");
        if (!_party_state.HasClaimableQuest(quest_id))
            return QuestClaimResultData.Failed("quest_not_claimable");
        var quest_reward_data = _resolve_quest_reward_data(quest_id);
        if (!quest_reward_data.Found)
            return QuestClaimResultData.Failed("quest_def_missing");
        if (!string.IsNullOrEmpty(quest_reward_data.ErrorCode))
            return QuestClaimResultData.Failed(quest_reward_data.ErrorCode);
        var reward_preview = _preview_quest_reward_claim(quest_id, quest_reward_data);
        if (!reward_preview.Ok)
        {
            return QuestClaimResultData.Failed(
                string.IsNullOrEmpty(reward_preview.ErrorCode)
                    ? "invalid_reward_entry"
                    : reward_preview.ErrorCode,
                reward_preview.CloneUnsupportedRewardTypes()
            );
        }

        var reward_item_ids = reward_preview.CloneWarehouseDepositItemIds();
        var warehouse_state_before = _party_state.warehouse_state?.DuplicateState();
        if (reward_item_ids.Count > 0)
        {
            var warehouse_commit = _party_warehouse_service.CommitBatchSwapTyped(
                new List<StringName>(),
                reward_item_ids
            );
            if (!warehouse_commit.Allowed)
            {
                return QuestClaimResultData.Failed(
                    _resolve_quest_reward_warehouse_error_code(
                    warehouse_commit.ErrorCode
                    )
                );
            }
        }

        var gold_delta = reward_preview.GoldDelta;
        var gold_before_claim = _party_state.GetGold();
        if (gold_delta > 0)
            _party_state.AddGold(gold_delta);
        if (!_party_state.MarkQuestRewardClaimed(quest_id, world_step))
        {
            if (gold_delta > 0)
                _party_state.SetGold(gold_before_claim);
            _party_state.warehouse_state = warehouse_state_before;
            return QuestClaimResultData.Failed("quest_claim_failed");
        }

        var pending_character_rewards = reward_preview.ClonePendingCharacterRewards();
        if (pending_character_rewards.Count > 0)
            EnqueuePendingCharacterRewardsTyped(pending_character_rewards);
        return QuestClaimResultData.Success(
            gold_delta,
            reward_preview.CloneItemRewards(),
            _pending_character_rewards_to_dicts(pending_character_rewards)
        );
    }

    internal QuestProgressApplyResultData ApplyDirectQuestProgressTyped(
        StringName quest_id,
        StringName objective_id,
        int progress_delta,
        int world_step,
        bool has_target_value,
        int target_value,
        QuestProgressEventContextData context_data
    )
    {
        if (_quest_progress_service == null)
            return new QuestProgressApplyResultData();
        QuestProgressApplyResultData summary = _quest_progress_service.ApplyDirectProgressTyped(
            quest_id,
            objective_id,
            progress_delta,
            world_step,
            has_target_value,
            target_value,
            context_data
        );
        _party_state = _quest_progress_service.GetPartyState();
        return summary;
    }

    internal QuestProgressApplyResultData ApplyQuestProgressEventsTyped(
        IEnumerable<QuestProgressService.QuestProgressEventData> event_options
    )
    {
        if (_quest_progress_service == null)
            return new QuestProgressApplyResultData();
        var summary = _quest_progress_service.ApplyQuestProgressEventsTyped(event_options);
        _party_state = _quest_progress_service.GetPartyState();
        return summary;
    }

    internal void EnqueuePendingCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        _party_state ??= new PartyState();
        if (rewards == null)
            return;
        foreach (PendingCharacterReward reward in rewards)
        {
            var normalizedReward = _normalize_pending_character_reward_option(reward);
            if (normalizedReward == null || normalizedReward.IsEmpty())
                continue;
            _party_state.EnqueuePendingCharacterReward(normalizedReward.DuplicateState());
        }
    }

    public AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null || member_state.progression == null
            ? new AttributeSnapshot()
            : _build_attribute_service(member_state).GetSnapshot();
    }

    public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
        StringName member_id,
        EquipmentState equipment_view
    )
    {
        var member_state = GetMemberState(member_id);
        return member_state == null || member_state.progression == null
            ? new AttributeSnapshot()
            : _build_attribute_service(member_state, equipment_view ?? new EquipmentState())
                .GetSnapshot();
    }

    internal WeaponProjection GetMemberWeaponProjectionTyped(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        return member_state == null
            ? new WeaponProjection()
            : GetMemberWeaponProjectionForEquipmentViewTyped(member_id, member_state.equipment_state);
    }

    public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
        StringName member_id,
        EquipmentState equipment_view
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null)
            return new WeaponProjection();
        var resolved_equipment_view = equipment_view ?? new EquipmentState();
        var weapon_item_id = ProgressionDataUtils.to_string_name(
            resolved_equipment_view.GetEquippedItemId("main_hand")
        );
        if (weapon_item_id == "")
            return _build_unarmed_weapon_projection();
        var item_def = GetItemDef(weapon_item_id);
        return item_def == null || !item_def.IsWeapon()
            ? new WeaponProjection()
            : _build_weapon_projection_from_item_def(item_def, resolved_equipment_view);
    }

    public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
        StringName member_id,
        EquipmentState equipment_view
    )
    {
        if (_character_trait_service == null || member_id == "")
            return BattleEffectiveTraitProjection.Empty;

        EffectiveTraitSet set = _character_trait_service.BuildEffectiveTraits(
            member_id,
            equipment_view
        );
        return new BattleEffectiveTraitProjection(set.ToBattleEffectiveInstances());
    }

    public StringName GetMemberWeaponPhysicalDamageTag(StringName member_id)
    {
        WeaponProjection projection = GetMemberWeaponProjectionTyped(member_id);
        if (projection == null || projection.IsEmpty())
            return "";
        return ProgressionDataUtils.to_string_name(projection.weapon_physical_damage_tag);
    }

    private WeaponProjection _build_weapon_projection_from_item_def(
        ItemDef item_def,
        EquipmentState equipment_state
    )
    {
        if (item_def == null || !item_def.IsWeapon())
            return new WeaponProjection();
        var profile = item_def.weapon_profile as WeaponProfileDef;
        if (profile == null)
            return new WeaponProjection();
        WeaponDice one_handed_dice = _weapon_dice_to_typed(profile.one_handed_dice);
        WeaponDice two_handed_dice = _weapon_dice_to_typed(profile.two_handed_dice);
        var properties = _weapon_profile_properties(profile);
        var is_versatile = properties.Contains(new StringName("versatile"));
        var uses_two_hands = _resolve_weapon_uses_two_hands(
            item_def,
            equipment_state,
            one_handed_dice,
            two_handed_dice,
            is_versatile
        );
        return new WeaponProjection
        {
            weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            weapon_item_id = item_def.item_id,
            weapon_profile_type_id = ProgressionDataUtils.to_string_name(profile.weapon_type_id),
            weapon_family = ProgressionDataUtils.to_string_name(profile.family),
            weapon_current_grip = _resolve_weapon_current_grip(
                one_handed_dice,
                two_handed_dice,
                uses_two_hands
            ),
            weapon_attack_range = Mathf.Max(profile.attack_range, 0),
            weapon_one_handed_dice = one_handed_dice,
            weapon_two_handed_dice = two_handed_dice,
            weapon_is_versatile = is_versatile,
            weapon_uses_two_hands = uses_two_hands,
            weapon_physical_damage_tag = item_def.GetWeaponPhysicalDamageTag(),
        };
    }

    private static WeaponProjection _build_unarmed_weapon_projection() =>
        new()
        {
            weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed),
            weapon_item_id = "",
            weapon_profile_type_id = "unarmed",
            weapon_family = "unarmed",
            weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
            weapon_attack_range = 1,
            weapon_one_handed_dice = new WeaponDice
            {
                dice_count = 1,
                dice_sides = 4,
                flat_bonus = 0,
            },
            weapon_two_handed_dice = new WeaponDice(),
            weapon_is_versatile = false,
            weapon_uses_two_hands = false,
            weapon_physical_damage_tag = ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Blunt),
        };

    private bool _resolve_weapon_uses_two_hands(
        ItemDef item_def,
        EquipmentState equipment_state,
        WeaponDice one_handed_dice,
        WeaponDice two_handed_dice,
        bool is_versatile
    )
    {
        if (item_def == null)
            return false;
        var occupied_slots = item_def.GetFinalOccupiedSlotIdsTyped("main_hand");
        if (occupied_slots.Contains(new StringName("off_hand")))
            return true;
        if ((one_handed_dice == null || one_handed_dice.IsEmpty()) && two_handed_dice != null && !two_handed_dice.IsEmpty())
            return true;
        if (is_versatile && two_handed_dice != null && !two_handed_dice.IsEmpty())
            return _is_off_hand_free_for_versatile(equipment_state);
        return false;
    }

    private static StringName _resolve_weapon_current_grip(
        WeaponDice one_handed_dice,
        WeaponDice two_handed_dice,
        bool uses_two_hands
    )
    {
        if (uses_two_hands)
            return BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded);
        if (one_handed_dice != null && !one_handed_dice.IsEmpty())
            return BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded);
        if (two_handed_dice != null && !two_handed_dice.IsEmpty())
            return BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded);
        return "none";
    }

    private static bool _is_off_hand_free_for_versatile(EquipmentState equipment_state) =>
        equipment_state == null
        || ProgressionDataUtils.to_string_name(equipment_state.GetEntrySlotForSlot("off_hand"))
            == "";

    private static List<StringName> _weapon_profile_properties(WeaponProfileDef profile) =>
        profile?.GetPropertiesTyped() ?? new List<StringName>();

    private static WeaponDice _weapon_dice_to_typed(WeaponDamageDiceDef dice_resource) =>
        WeaponDice.FromResource(dice_resource);

    public bool LearnSkill(StringName member_id, StringName skill_id) =>
        LearnSkillTyped(member_id, skill_id);

    internal bool LearnSkillTyped(
        StringName member_id,
        StringName skill_id,
        LearnSkillOptionsData options = null
    ) => _learn_skill_internal(member_id, skill_id, null, options ?? new LearnSkillOptionsData());

    public bool LearnKnowledge(StringName member_id, StringName knowledge_id) =>
        _learn_knowledge_internal(member_id, knowledge_id);

    public PracticeSkillLearnStatus GetPracticeSkillLearnStatusTyped(
        StringName member_id,
        StringName skill_id
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return PracticeSkillLearnStatus.NonPractice();
        return _build_practice_growth_service().GetSkillLearnedStatusTyped(skill_id, progression);
    }

    public LevelGrowthTriggerResult SetActiveLevelTriggerCoreSkillTyped(
        StringName member_id,
        StringName skill_id
    )
    {
        var member_state = GetMemberState(member_id);
        var service = new LevelGrowthEvaluationService();
        service.Setup(_skill_def_index);
        var result = service.SetActiveTriggerCoreSkillTyped(member_state, skill_id);
        if (result.Ok && member_state?.progression != null)
            BuildProgressionService(member_state.progression).RefreshRuntimeState();
        return result;
    }

    public LevelGrowthTriggerResult ClearActiveLevelTriggerCoreSkillTyped(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        var service = new LevelGrowthEvaluationService();
        service.Setup(_skill_def_index);
        var result = service.ClearActiveTriggerCoreSkillTyped(member_state);
        if (result.Ok && member_state?.progression != null)
            BuildProgressionService(member_state.progression).RefreshRuntimeState();
        return result;
    }

    public DailyPracticeGrowthResult ApplyDailyPracticeGrowthTyped(int days_elapsed)
    {
        if (_party_state == null || days_elapsed <= 0)
            return new DailyPracticeGrowthResult(false, days_elapsed, new GStringNameArray());
        var practice_service = _build_practice_growth_service();
        var changed_member_ids = new List<StringName>();
        foreach (
            string member_key in _party_state.member_states.GetSortedIdStrings()
        )
        {
            var member_id = new StringName(member_key);
            var member_state = _party_state.GetMemberState(member_id);
            if (member_state?.progression is not UnitProgress)
                continue;
            var before_snapshot = _capture_practice_resource_snapshot(member_state);
            practice_service.ApplyDailyGrowthToMember(member_state, days_elapsed);
            _clamp_member_vitals_to_current_attribute_snapshot(member_state);
            var after_snapshot = _capture_practice_resource_snapshot(member_state);
            if (!DictionariesEqual(before_snapshot, after_snapshot))
                changed_member_ids.Add(member_id);
        }
        return new DailyPracticeGrowthResult(
            changed_member_ids.Count > 0,
            days_elapsed,
            changed_member_ids
        );
    }

    private bool _learn_skill_internal(
        StringName member_id,
        StringName skill_id,
        CharacterProgressionDelta delta = null,
        LearnSkillOptionsData options = null
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return false;

        options ??= new LearnSkillOptionsData();
        var practice_service = _build_practice_growth_service();
        var practice_status = practice_service.GetSkillLearnedStatusTyped(skill_id, progression);
        var progression_service = BuildProgressionService(progression);
        if (practice_status.IsPracticeSkill)
        {
            if (practice_status.NeedsReplacement)
            {
                if (!options.ConfirmPracticeReplacement)
                    return false;
                if (!progression_service.CanLearnSkill(skill_id))
                    return false;
                if (!practice_service.ApplyReplacement(skill_id, progression, true))
                    return false;
                progression_service.RefreshRuntimeState();
                var replacement_achievement_ids = RecordAchievementEvent(
                    member_id,
                    "skill_learned",
                    1,
                    skill_id
                );
                delta?.AppendUnlockedAchievementIds(replacement_achievement_ids);
                return true;
            }
            if (!practice_status.CanLearn)
                return false;
        }

        if (!progression_service.LearnSkill(skill_id))
            return false;
        if (practice_status.IsPracticeSkill)
            practice_service.InjectFirstUnlockStartingValues(
                member_state,
                practice_status.TrackType
            );
        var achievement_ids = RecordAchievementEvent(member_id, "skill_learned", 1, skill_id);
        delta?.AppendUnlockedAchievementIds(achievement_ids);
        return true;
    }

    private bool _learn_knowledge_internal(
        StringName member_id,
        StringName knowledge_id,
        CharacterProgressionDelta delta = null
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return false;
        var progression_service = BuildProgressionService(progression);
        if (!progression_service.LearnKnowledge(knowledge_id))
            return false;
        var achievement_ids = RecordAchievementEvent(
            member_id,
            "knowledge_learned",
            1,
            knowledge_id
        );
        delta?.AppendUnlockedAchievementIds(achievement_ids);
        return true;
    }

    public CharacterProgressionDelta GrantBattleMastery(
        StringName member_id,
        StringName skill_id,
        int amount
    ) =>
        GrantSkillMasteryFromSource(
            member_id,
            skill_id,
            amount,
            "battle",
            _build_default_source_label("battle")
        );

    public CharacterProgressionDelta GrantSkillMasteryFromSource(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type
    ) => GrantSkillMasteryFromSource(member_id, skill_id, amount, source_type, "", "", true);

    public CharacterProgressionDelta GrantSkillMasteryFromSource(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label
    ) =>
        GrantSkillMasteryFromSource(
            member_id,
            skill_id,
            amount,
            source_type,
            source_label,
            "",
            true
        );

    public CharacterProgressionDelta GrantSkillMasteryFromSource(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text
    ) =>
        GrantSkillMasteryFromSource(
            member_id,
            skill_id,
            amount,
            source_type,
            source_label,
            reason_text,
            true
        );

    public CharacterProgressionDelta GrantSkillMasteryFromSource(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text,
        bool emit_achievement_event
    ) =>
        _grant_skill_mastery_internal(
            member_id,
            skill_id,
            amount,
            source_type,
            string.IsNullOrEmpty(source_label)
                ? _build_default_source_label(source_type)
                : source_label,
            reason_text,
            emit_achievement_event
        );

    public GStringNameArray RecordAchievementEvent(StringName member_id, StringName event_type) =>
        RecordAchievementEvent(member_id, event_type, 1, "", new GDictionary());

    public GStringNameArray RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount
    ) => RecordAchievementEvent(member_id, event_type, amount, "", new GDictionary());

    public GStringNameArray RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id
    ) => RecordAchievementEvent(member_id, event_type, amount, subject_id, new GDictionary());

    public GStringNameArray RecordAchievementEvent(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id,
        GDictionary meta
    )
    {
        var unlocked_ids = new GStringNameArray();
        if (member_id == "" || event_type == "" || amount <= 0)
            return unlocked_ids;
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return unlocked_ids;

        foreach (var achievement_def in _get_matching_achievement_defs(event_type, subject_id))
        {
            if (achievement_def == null)
                continue;
            var progress_state = progression.GetAchievementProgressState(
                achievement_def.achievement_id
            );
            if (progress_state == null)
                progress_state = new AchievementProgressState
                {
                    achievement_id = achievement_def.achievement_id,
                };
            if (progress_state.is_unlocked)
                continue;
            progress_state.current_value += amount;
            if (progress_state.current_value >= achievement_def.threshold)
            {
                _finalize_achievement_unlock(
                    member_state,
                    achievement_def,
                    progress_state,
                    meta ?? new GDictionary()
                );
                _append_unique_string_name(unlocked_ids, achievement_def.achievement_id);
            }
            progression.SetAchievementProgressState(progress_state);
        }
        return unlocked_ids;
    }

    public bool UnlockAchievement(StringName member_id, StringName achievement_id) =>
        UnlockAchievement(member_id, achievement_id, new GDictionary());

    public bool UnlockAchievement(
        StringName memberId,
        StringName achievementId,
        string summaryText = ""
    )
    {
        GDictionary meta = new();
        if (!string.IsNullOrEmpty(summaryText))
            meta["summary_text"] = summaryText;
        return UnlockAchievement(memberId, achievementId, meta);
    }

    public bool UnlockAchievement(
        StringName member_id,
        StringName achievement_id,
        GDictionary meta
    )
    {
        if (member_id == "" || achievement_id == "")
            return false;
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return false;
        var achievement_def = GetAchievementDef(achievement_id);
        if (achievement_def == null)
            return false;
        var progress_state = progression.GetAchievementProgressState(achievement_id);
        if (progress_state == null)
            progress_state = new AchievementProgressState { achievement_id = achievement_id };
        if (progress_state.is_unlocked)
        {
            progression.SetAchievementProgressState(progress_state);
            return false;
        }
        progress_state.current_value = Mathf.Max(
            progress_state.current_value,
            Mathf.Max(achievement_def.threshold, 1)
        );
        _finalize_achievement_unlock(
            member_state,
            achievement_def,
            progress_state,
            meta ?? new GDictionary()
        );
        progression.SetAchievementProgressState(progress_state);
        return true;
    }

    public PendingCharacterReward BuildPendingCharacterReward(
        StringName member_id,
        StringName reward_id,
        StringName source_type,
        StringName source_id,
        string source_label,
        IEnumerable<PendingCharacterRewardEntry> entry_options
    ) =>
        BuildPendingCharacterReward(
            member_id,
            reward_id,
            source_type,
            source_id,
            source_label,
            entry_options,
            ""
        );

    public PendingCharacterReward BuildPendingCharacterReward(
        StringName member_id,
        StringName reward_id,
        StringName source_type,
        StringName source_id,
        string source_label,
        IEnumerable<PendingCharacterRewardEntry> entry_options,
        string summary_text
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression == null)
            return null;
        if (_has_unsupported_pending_character_entry_type(entry_options))
            return null;

        var reward = new PendingCharacterReward
        {
            reward_id =
                reward_id != ""
                    ? reward_id
                    : _build_reward_id(member_id, source_id != "" ? source_id : source_type),
            member_id = member_id,
            member_name = !string.IsNullOrEmpty(member_state.display_name)
                ? member_state.display_name
                : (string)member_id,
            source_type = source_type,
            source_id = source_id != "" ? source_id : source_type,
            source_label = !string.IsNullOrEmpty(source_label)
                ? source_label
                : _build_default_source_label(source_type),
            summary_text = summary_text,
            entries = _normalize_pending_character_entries(entry_options),
        };
        return reward.IsEmpty() ? null : reward;
    }

    private void _finalize_achievement_unlock(
        PartyMemberState member_state,
        AchievementDef achievement_def,
        AchievementProgressState progress_state,
        GDictionary meta
    )
    {
        if (member_state == null || achievement_def == null || progress_state == null)
            return;
        progress_state.is_unlocked = true;
        progress_state.unlocked_at_unix_time = (int)Time.GetUnixTimeFromSystem();
        var reward = _build_achievement_pending_reward(
            member_state,
            achievement_def,
            meta ?? new GDictionary()
        );
        if (reward != null && !reward.IsEmpty())
            EnqueuePendingCharacterRewardsTyped(new[] { reward });
    }

    public PendingCharacterReward BuildPendingSkillMasteryReward(
        StringName member_id,
        StringName source_type,
        string source_label,
        IEnumerable<PendingCharacterRewardEntry> entry_options
    ) =>
        BuildPendingSkillMasteryReward(
            member_id,
            source_type,
            source_label,
            entry_options,
            ""
        );

    public PendingCharacterReward BuildPendingSkillMasteryReward(
        StringName member_id,
        StringName source_type,
        string source_label,
        IEnumerable<PendingCharacterRewardEntry> entry_options,
        string summary_text
    )
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return null;
        var reward = new PendingCharacterReward
        {
            reward_id = _build_reward_id(member_id, source_type),
            member_id = member_id,
            member_name = !string.IsNullOrEmpty(member_state.display_name)
                ? member_state.display_name
                : (string)member_id,
            source_type = source_type,
            source_id = source_type,
            source_label = !string.IsNullOrEmpty(source_label)
                ? source_label
                : _build_default_source_label(source_type),
            summary_text = summary_text,
            entries = _normalize_pending_skill_mastery_entries(
                progression,
                entry_options,
                source_type
            ),
        };
        return reward.IsEmpty() ? null : reward;
    }

    public CharacterProgressionDelta ApplyPendingCharacterReward(PendingCharacterReward reward)
    {
        var normalized_reward = _normalize_pending_character_reward_option(
            reward,
            true
        );
        var member_id = normalized_reward?.member_id ?? "";
        var delta = _new_delta(member_id);
        var member_state = GetMemberState(member_id);
        if (normalized_reward == null || normalized_reward.IsEmpty())
            return delta;
        if (member_state == null || member_state.progression is not UnitProgress progression)
        {
            _remove_pending_character_reward_if_present(normalized_reward.reward_id);
            return delta;
        }

        var before_skill_levels = _capture_skill_levels(progression);
        var before_granted_skill_ids = _capture_granted_skill_ids(progression);
        var before_profession_ranks = _capture_profession_ranks(progression);
        delta.character_level_before = progression.character_level;
        delta.AddUnlockedAchievementId(
            normalized_reward.source_type == RewardTypeAchievement
                ? normalized_reward.source_id
                : ""
        );

        var attribute_service = _build_attribute_service(member_state);
        var attribute_growth_service = new AttributeGrowthService();
        attribute_growth_service.Setup(progression);
        var mastery_source_type = _resolve_mastery_source_type(normalized_reward.source_type);
        var applied_any = false;

        foreach (var entry in _sort_pending_reward_entries(normalized_reward.entries))
        {
            if (entry == null || entry.IsEmpty())
                continue;
            if (entry.EntryKind == PendingCharacterRewardEntryKind.KnowledgeUnlock)
            {
                if (
                    _learn_knowledge_internal(
                        member_id,
                        entry.target_id,
                        delta
                    )
                )
                {
                    applied_any = true;
                    delta.AddKnowledgeChange(
                        new CharacterKnowledgeChangeFact(
                            entry.target_id,
                            _resolve_reward_target_label(
                                entry.entry_type,
                                entry.target_id,
                                entry.target_label
                            ),
                            entry.reason_text
                        )
                    );
                }
            }
            else if (entry.EntryKind == PendingCharacterRewardEntryKind.SkillUnlock)
            {
                if (
                    _learn_skill_internal(
                        member_id,
                        entry.target_id,
                        delta,
                        new LearnSkillOptionsData()
                    )
                )
                    applied_any = true;
            }
            else if (entry.EntryKind == PendingCharacterRewardEntryKind.SkillMastery)
            {
                var mastery_delta = _grant_skill_mastery_internal(
                    member_id,
                    entry.target_id,
                    entry.amount,
                    mastery_source_type,
                    normalized_reward.source_label,
                    entry.reason_text,
                    true
                );
                if (mastery_delta.MasteryChangesTyped.Count > 0)
                    applied_any = true;
                _merge_delta(delta, mastery_delta);
            }
            else if (entry.EntryKind == PendingCharacterRewardEntryKind.AttributeDelta)
            {
                if (
                    attribute_service.ApplyPermanentAttributeChange(
                        entry.target_id,
                        entry.amount,
                        new AttributePermanentChangeSource(
                            AttributePermanentChangeSourceKind.Unknown,
                            normalized_reward.source_id,
                            false
                        )
                    )
                )
                {
                    applied_any = true;
                    delta.AddAttributeChange(
                        CharacterAttributeChangeFact.PermanentDelta(
                            entry.target_id,
                            _resolve_reward_target_label(
                                entry.entry_type,
                                entry.target_id,
                                entry.target_label
                            ),
                            entry.amount,
                            entry.reason_text
                        )
                    );
                }
            }
            else if (entry.EntryKind == PendingCharacterRewardEntryKind.AttributeProgress)
            {
                var growth_result = attribute_growth_service.ApplyAttributeProgressTyped(
                    entry.target_id,
                    entry.amount,
                    entry.reason_text
                );
                if (growth_result.Applied)
                {
                    applied_any = true;
                    delta.AddAttributeChange(
                        new CharacterAttributeChangeFact(
                            entry.target_id,
                            _resolve_reward_target_label(
                                entry.entry_type,
                                entry.target_id,
                                entry.target_label
                            ),
                            growth_result.AttributeDelta,
                            entry.reason_text,
                            growth_result.ProgressDelta,
                            growth_result.ProgressBefore,
                            growth_result.ProgressAfter,
                            growth_result.AttributeBefore,
                            growth_result.AttributeAfter
                        )
                    );
                }
            }
            else
            {
                _log_unsupported_pending_character_reward_entry(normalized_reward, entry);
            }
        }

        _fill_delta_from_progression(
            delta,
            progression,
            before_skill_levels,
            before_granted_skill_ids,
            before_profession_ranks
        );
        if (!applied_any && delta.MasteryChangesTyped.Count == 0)
            delta.character_level_after = delta.character_level_before;

        _remove_pending_character_reward_if_present(normalized_reward.reward_id);
        return delta;
    }

    public GDictionary GetMemberAchievementSummary(StringName member_id)
    {
        var member_state = GetMemberState(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return new GDictionary
            {
                ["unlocked_count"] = 0,
                ["in_progress_count"] = 0,
                ["recent_unlocked_name"] = "",
                ["active_progress_entries"] = new GArray(),
            };

        var unlocked_count = 0;
        var in_progress_count = 0;
        var recent_unlocked_name = "";
        var recent_unlocked_time = 0;
        var active_entries = new List<AchievementProgressSummaryEntry>();

        foreach (StringName achievement_id in SortedContentKeys(_achievement_def_index))
        {
            var achievement_def = GetAchievementDef(achievement_id);
            if (achievement_def == null)
                continue;
            var progress_state = progression.GetAchievementProgressState(achievement_id);
            if (progress_state != null && progress_state.is_unlocked)
            {
                unlocked_count++;
                var unlocked_at = progress_state.unlocked_at_unix_time;
                if (unlocked_at >= recent_unlocked_time)
                {
                    recent_unlocked_time = unlocked_at;
                    recent_unlocked_name = achievement_def.display_name;
                }
                continue;
            }
            var current_value = progress_state?.current_value ?? 0;
            if (current_value <= 0)
                continue;
            in_progress_count++;
            active_entries.Add(
                new AchievementProgressSummaryEntry(
                    achievement_id,
                    achievement_def.display_name,
                    achievement_def.description,
                    current_value,
                    achievement_def.threshold
                )
            );
        }
        active_entries.Sort(CompareAchievementProgressEntry);
        var active_progress_entries = new GArray();
        foreach (var entry in active_entries)
            active_progress_entries.Add(ProjectAchievementProgressSummaryEntry(entry));
        return new GDictionary
        {
            ["unlocked_count"] = unlocked_count,
            ["in_progress_count"] = in_progress_count,
            ["recent_unlocked_name"] = recent_unlocked_name,
            ["active_progress_entries"] = active_progress_entries,
        };
    }

    public CharacterProgressionDelta PromoteProfession(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    )
    {
        var member_state = GetMemberState(member_id);
        var delta = _new_delta(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return delta;

        var before_skill_levels = _capture_skill_levels(progression);
        var before_granted_skill_ids = _capture_granted_skill_ids(progression);
        var before_profession_ranks = _capture_profession_ranks(progression);
        var trigger_skill_id = progression.active_level_trigger_core_skill_id;
        delta.character_level_before = progression.character_level;

        var progression_service = BuildProgressionService(progression);
        if (progression_service.PromoteProfession(profession_id, selection ?? PromotionSelectionData.Empty))
        {
            _apply_level_trigger_attribute_growth(member_state, trigger_skill_id, delta);
            _fill_delta_from_progression(
                delta,
                progression,
                before_skill_levels,
                before_granted_skill_ids,
                before_profession_ranks
            );
            delta.AppendUnlockedAchievementIds(
                RecordAchievementEvent(member_id, "profession_promoted", 1, profession_id)
            );
        }
        return delta;
    }

    public BattleResourceCommitResult CommitBattleResources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
    ) => _battle_writeback_service.CommitResources(
        member_id,
        current_hp,
        current_mp,
        current_aura
    );

    internal ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
        StringName member_id,
        IReadOnlyCollection<StringName> consumed_setup_ids
    ) =>
        _battle_writeback_service.CommitContingencyConsumedSetups(
            member_id,
            consumed_setup_ids
        );

    public void CommitBattleDeath(StringName member_id) =>
        _battle_writeback_service.CommitDeath(member_id);

    public void CommitBattleKo(StringName member_id) =>
        _battle_writeback_service.CommitKo(member_id);

    public int FlushAfterBattle() => _battle_writeback_service.FlushAfterBattle();

    private GStringNameArray CollectKnownActiveSkillIds(UnitProgress progression_state)
    {
        var skill_ids = new GStringNameArray();
        if (progression_state == null)
            return skill_ids;
        var progression = progression_state;
        foreach (StringName skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            var skill_def = GetSkillDef(skill_id);
            if (
                skill_progress == null
                || skill_def == null
                || !skill_progress.is_learned
                || skill_def.SkillTypeKind != SkillTypeKind.Active
                || !skill_def.CanUseInCombat()
            )
                continue;
            skill_ids.Add(skill_id);
        }
        return skill_ids;
    }

    private GDictionary CollectKnownSkillLevelMap(UnitProgress progression_state)
    {
        var skill_levels = new GDictionary();
        if (progression_state == null)
            return skill_levels;
        var progression = progression_state;
        foreach (StringName skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            var skill_def = GetSkillDef(skill_id);
            if (
                skill_progress == null
                || skill_def == null
                || !skill_progress.is_learned
                || skill_def.SkillTypeKind != SkillTypeKind.Active
            )
                continue;
            skill_levels[skill_id] = skill_progress.skill_level;
        }
        return skill_levels;
    }

    private void _setup_identity_apply_services()
    {
        _bloodline_apply_service.Setup(_progression_identity_catalog);
        _ascension_apply_service.Setup(_progression_identity_catalog);
        _stage_advancement_apply_service.Setup(_progression_identity_catalog);
    }

    private void _setup_battle_writeback_service()
    {
        _battle_writeback_service.Setup(
            _party_state,
            _party_warehouse_service,
            GetMemberAttributeSnapshot
        );
    }

    private void _refresh_member_identity_after_apply(PartyMemberState member_state)
    {
        if (member_state == null)
            return;
        _resolve_member_body_size(member_state);
        _resolve_member_effective_age_stage(member_state);
        _revoke_orphan_racial_skills_for_member(member_state);
        _backfill_racial_granted_skills_for_member(member_state);
    }

    private void _resolve_member_body_size(PartyMemberState member_state)
    {
        var category = _resolve_body_size_category_for_member(member_state);
        if (category == "")
            return;
        member_state.SetBodySizeCategory(category);
    }

    private StringName _resolve_body_size_category_for_member(PartyMemberState member_state)
    {
        if (member_state == null)
            return "";
        var ascension_stage_def = GetAscensionStageDefForMember(member_state.member_id);
        if (
            ascension_stage_def != null
            && ascension_stage_def.body_size_category_override != ""
            && BodySizeContentRules.IsValidBodySizeCategory(
                ascension_stage_def.body_size_category_override
            )
        )
            return ascension_stage_def.body_size_category_override;
        var subrace_def = GetSubraceDefForMember(member_state.member_id);
        if (
            subrace_def != null
            && subrace_def.body_size_category_override != ""
            && BodySizeContentRules.IsValidBodySizeCategory(subrace_def.body_size_category_override)
        )
            return subrace_def.body_size_category_override;
        var race_def = GetRaceDefForMember(member_state.member_id);
        if (
            race_def != null
            && BodySizeContentRules.IsValidBodySizeCategory(race_def.body_size_category)
        )
            return race_def.body_size_category;
        return "";
    }

    private void _resolve_member_effective_age_stage(PartyMemberState member_state)
    {
        if (member_state == null)
            return;
        var age_profile = GetAgeProfileDef(member_state.age_profile_id);
        var resolution = AgeStageResolver.ResolveEffectiveStage(
            member_state,
            age_profile,
            _collect_active_stage_advancement_modifiers(member_state),
            GetBloodlineDefForMember(member_state.member_id),
            GetBloodlineStageDefForMember(member_state.member_id),
            GetAscensionDefForMember(member_state.member_id),
            GetAscensionStageDefForMember(member_state.member_id)
        );
        var stage_id = resolution?.StageId ?? "";
        if (stage_id == "")
            stage_id =
                member_state.natural_age_stage_id != ""
                    ? member_state.natural_age_stage_id
                    : "adult";
        member_state.SetEffectiveAgeStage(
            stage_id,
            resolution?.SourceType ?? "",
            resolution?.SourceId ?? ""
        );
    }

    private List<StageAdvancementModifier> _collect_active_stage_advancement_modifiers(
        PartyMemberState member_state
    )
    {
        var modifiers = new List<StageAdvancementModifier>();
        if (member_state == null)
            return modifiers;
        foreach (var modifier_id in member_state.active_stage_advancement_modifier_ids)
        {
            if (_stage_advancement_modifier_index.TryGetValue(modifier_id, out var modifier))
                modifiers.Add(modifier);
        }
        return modifiers;
    }

    private bool _backfill_racial_granted_skills_for_member(PartyMemberState member_state) =>
        RacialSkillGrantService.BackfillMember(
            member_state,
            _progression_identity_catalog,
            _skill_def_index,
            _profession_def_index,
            BuildProgressionService
        );

    private bool _revoke_orphan_racial_skills_for_member(PartyMemberState member_state) =>
        RacialSkillGrantService.RevokeOrphanMember(
            member_state,
            _progression_identity_catalog,
            _skill_def_index,
            _profession_def_index,
            BuildProgressionService
        );

    private ProgressionService BuildProgressionService(UnitProgress progression)
    {
        return _progression_service_factory.Build(
            progression,
            _skill_def_index,
            _profession_def_index
        );
    }

    private PracticeGrowthService _build_practice_growth_service()
    {
        var service = new PracticeGrowthService();
        service.Setup(_skill_def_index, _profession_def_index);
        return service;
    }

    private GDictionary _capture_practice_resource_snapshot(PartyMemberState member_state)
    {
        var progression = member_state?.progression as UnitProgress;
        var base_attrs = progression?.unit_base_attributes;
        return new GDictionary
        {
            ["current_mp"] = member_state?.current_mp ?? 0,
            ["current_aura"] = member_state?.current_aura ?? 0,
            ["mp_max"] = base_attrs?.GetAttributeValue(AttributeService.ToStringName(AttributeIdKind.MpMax)) ?? 0,
            ["aura_max"] = base_attrs?.GetAttributeValue(AttributeService.ToStringName(AttributeIdKind.AuraMax)) ?? 0,
        };
    }

    private void _clamp_member_vitals_to_current_attribute_snapshot(PartyMemberState member_state)
    {
        if (member_state == null || member_state.member_id == "")
            return;
        AttributeSnapshot snapshot = GetMemberAttributeSnapshot(member_state.member_id);
        if (snapshot == null)
            return;
        member_state.ClampVitals(
            Mathf.Max(snapshot.GetValue(AttributeService.HP_MAX), 1),
            Mathf.Max(snapshot.GetValue(AttributeService.MP_MAX), 0),
            Mathf.Max(snapshot.GetValue(AttributeService.AURA_MAX), 0)
        );
    }

    private void _apply_level_trigger_attribute_growth(
        PartyMemberState member_state,
        StringName trigger_skill_id,
        CharacterProgressionDelta delta
    )
    {
        if (
            member_state == null
            || member_state.progression is not UnitProgress progression
            || trigger_skill_id == ""
        )
            return;
        var skill_def = GetSkillDef(trigger_skill_id);
        if (skill_def == null || skill_def.AttributeGrowthProgressTyped.Count == 0)
            return;
        var skill_progress = progression.GetSkillProgress(trigger_skill_id);
        if (skill_progress == null || skill_progress.core_max_growth_claimed)
            return;
        var growth_entries = _collect_attribute_growth_entries(skill_def);
        if (growth_entries.Count == 0)
            return;
        var attribute_growth_service = new AttributeGrowthService();
        attribute_growth_service.Setup(progression);
        var did_apply_growth = false;
        foreach (var entry in growth_entries)
        {
            var growth_result = attribute_growth_service.ApplyAttributeProgressTyped(
                entry.AttributeId,
                entry.Amount,
                $"{_resolve_skill_label(trigger_skill_id)} 锁定成长"
            );
            if (!growth_result.Applied)
                continue;
            did_apply_growth = true;
            delta?.AddAttributeChange(
                CharacterAttributeChangeFact.GrowthResult(
                    _resolve_attribute_label(entry.AttributeId),
                    growth_result
                )
            );
        }
        if (!did_apply_growth)
            return;
        skill_progress.core_max_growth_claimed = true;
        progression.SetSkillProgress(skill_progress);
    }

    private List<AttributeGrowthEntryData> _collect_attribute_growth_entries(
        SkillDef skill_def
    )
    {
        var entries = new List<AttributeGrowthEntryData>();
        if (skill_def == null)
            return entries;
        var attribute_entries = new List<(string key, int amount)>();
        foreach (KeyValuePair<StringName, int> entry in skill_def.AttributeGrowthProgressTyped)
        {
            if (entry.Key != "")
                attribute_entries.Add((entry.Key.ToString(), entry.Value));
        }
        attribute_entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        foreach (var (attributeKey, amount) in attribute_entries)
        {
            var attribute_id = ProgressionDataUtils.to_string_name(attributeKey);
            if (amount <= 0 || !AttributeGrowthService.IsValidAttributeId(attribute_id))
                continue;
            entries.Add(new AttributeGrowthEntryData(attribute_id, amount));
        }
        return entries;
    }

    private SkillDef GetSkillDef(StringName skillId) =>
        skillId != "" && _skill_def_index.TryGetValue(skillId, out var skillDef)
            ? skillDef
            : null;

    private AchievementDef GetAchievementDef(StringName achievementId) =>
        achievementId != ""
        && _achievement_def_index.TryGetValue(achievementId, out var achievementDef)
            ? achievementDef
            : null;

    public ItemDef GetItemDef(StringName itemId) =>
        itemId != "" && _item_def_index.TryGetValue(itemId, out var itemDef) ? itemDef : null;

    private RaceDef GetRaceDef(StringName raceId) =>
        raceId != "" && _race_def_index.TryGetValue(raceId, out var raceDef) ? raceDef : null;

    private SubraceDef GetSubraceDef(StringName subraceId) =>
        subraceId != "" && _subrace_def_index.TryGetValue(subraceId, out var subraceDef)
            ? subraceDef
            : null;

    private AgeProfileDef GetAgeProfileDef(StringName profileId) =>
        profileId != "" && _age_profile_def_index.TryGetValue(profileId, out var ageProfileDef)
            ? ageProfileDef
            : null;

    private BloodlineDef GetBloodlineDef(StringName bloodlineId) =>
        bloodlineId != ""
        && _bloodline_def_index.TryGetValue(bloodlineId, out var bloodlineDef)
            ? bloodlineDef
            : null;

    private BloodlineStageDef GetBloodlineStageDef(StringName stageId) =>
        stageId != ""
        && _bloodline_stage_def_index.TryGetValue(stageId, out var bloodlineStageDef)
            ? bloodlineStageDef
            : null;

    private AscensionDef GetAscensionDef(StringName ascensionId) =>
        ascensionId != ""
        && _ascension_def_index.TryGetValue(ascensionId, out var ascensionDef)
            ? ascensionDef
            : null;

    private AscensionStageDef GetAscensionStageDef(StringName stageId) =>
        stageId != ""
        && _ascension_stage_def_index.TryGetValue(stageId, out var ascensionStageDef)
            ? ascensionStageDef
            : null;

    private static Dictionary<StringName, T> IndexContentDefs<T>(
        GDictionary source,
        Func<T, StringName> idSelector
    )
        where T : class
    {
        var result = new Dictionary<StringName, T>();
        if (source == null)
            return result;
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Object || rawValue.AsGodotObject() is not T entry)
                continue;
            if (idSelector(entry) == "")
                continue;
            result[rawKey.AsStringName()] = entry;
        }
        return result;
    }

    private static string _identity_def_label(object definition, StringName fallback_id)
    {
        if (definition != null)
        {
            string displayName = definition switch
            {
                RaceDef raceDef => raceDef.display_name,
                SubraceDef subraceDef => subraceDef.display_name,
                BloodlineDef bloodlineDef => bloodlineDef.display_name,
                BloodlineStageDef bloodlineStageDef => bloodlineStageDef.display_name,
                AscensionDef ascensionDef => ascensionDef.display_name,
                AscensionStageDef ascensionStageDef => ascensionStageDef.display_name,
                _ => "",
            };
            displayName = displayName.StripEdges();
            if (displayName.Length > 0)
                return displayName;
        }
        return fallback_id != "" ? (string)fallback_id : "";
    }

    private string _get_age_stage_display_label(StringName age_profile_id, StringName stage_id)
    {
        if (stage_id == "")
            return "";
        var age_profile = GetAgeProfileDef(age_profile_id);
        if (age_profile != null)
        {
            foreach (var stage_rule in age_profile.stage_rules)
            {
                if (stage_rule == null || stage_rule.stage_id != stage_id)
                    continue;
                if (!string.IsNullOrEmpty(stage_rule.display_name))
                    return stage_rule.display_name;
                break;
            }
        }
        return (string)stage_id;
    }

    private GArray _build_identity_trait_summary_lines(
        RaceDef race_def,
        SubraceDef subrace_def,
        AgeStageRule age_stage_rule,
        BloodlineDef bloodline_def,
        BloodlineStageDef bloodline_stage_def,
        AscensionDef ascension_def,
        AscensionStageDef ascension_stage_def
    )
    {
        var lines = new GArray();
        if (race_def != null)
            _append_identity_text_lines(lines, race_def.racial_trait_summary);
        if (subrace_def != null)
            _append_identity_text_lines(lines, subrace_def.racial_trait_summary);
        if (age_stage_rule != null)
            _append_identity_text_lines(lines, age_stage_rule.trait_summary);
        if (bloodline_def != null)
            _append_identity_text_lines(lines, bloodline_def.trait_summary);
        if (bloodline_stage_def != null)
            _append_identity_text_lines(lines, bloodline_stage_def.trait_summary);
        if (ascension_def != null)
            _append_identity_text_lines(lines, ascension_def.trait_summary);
        if (ascension_stage_def != null)
            _append_identity_text_lines(lines, ascension_stage_def.trait_summary);
        return lines;
    }

    private static void _append_identity_text_lines(
        GArray target,
        Godot.Collections.Array<string> values
    )
    {
        foreach (var value in values)
        {
            var text = (value ?? "").StripEdges();
            if (text.Length == 0 || target.Contains(text))
                continue;
            target.Add(text);
        }
    }

    private static GDictionary _collect_identity_damage_resistances(
        RaceDef race_def,
        SubraceDef subrace_def
    )
    {
        var result = new GDictionary();
        if (race_def != null)
            _merge_identity_string_name_map(result, race_def.damage_resistances);
        if (subrace_def != null)
            _merge_identity_string_name_map(result, subrace_def.damage_resistances);
        return result;
    }

    private static void _merge_identity_string_name_map(GDictionary target, GDictionary source)
    {
        foreach (var raw_key in source.Keys)
        {
            if (raw_key.VariantType != Variant.Type.StringName)
                continue;
            Variant rawValue = source[raw_key];
            if (rawValue.VariantType != Variant.Type.StringName)
                continue;
            var key = raw_key.AsStringName();
            var value = rawValue.AsStringName();
            if (key == "" || value == "")
                continue;
            target[key] = value;
        }
    }

    private static GStringNameArray _collect_identity_save_advantage_tags(
        RaceDef race_def,
        SubraceDef subrace_def
    )
    {
        var tags = new GStringNameArray();
        if (race_def != null)
            _append_unique_string_names(tags, race_def.save_advantage_tags);
        if (subrace_def != null)
            _append_unique_string_names(tags, subrace_def.save_advantage_tags);
        return tags;
    }

    private GArray _build_identity_granted_skill_lines(
        RaceDef race_def,
        SubraceDef subrace_def,
        BloodlineDef bloodline_def,
        BloodlineStageDef bloodline_stage_def,
        AscensionDef ascension_def,
        AscensionStageDef ascension_stage_def
    )
    {
        var lines = new GArray();
        if (race_def != null)
            _append_identity_granted_skill_lines(
                lines,
                race_def.racial_granted_skills,
                _identity_def_label(race_def, race_def.race_id)
            );
        if (subrace_def != null)
            _append_identity_granted_skill_lines(
                lines,
                subrace_def.racial_granted_skills,
                _identity_def_label(subrace_def, subrace_def.subrace_id)
            );
        if (bloodline_def != null)
            _append_identity_granted_skill_lines(
                lines,
                bloodline_def.racial_granted_skills,
                _identity_def_label(bloodline_def, bloodline_def.bloodline_id)
            );
        if (bloodline_stage_def != null)
            _append_identity_granted_skill_lines(
                lines,
                bloodline_stage_def.racial_granted_skills,
                _identity_def_label(bloodline_stage_def, bloodline_stage_def.stage_id)
            );
        if (ascension_def != null)
            _append_identity_granted_skill_lines(
                lines,
                ascension_def.racial_granted_skills,
                _identity_def_label(ascension_def, ascension_def.ascension_id)
            );
        if (ascension_stage_def != null)
            _append_identity_granted_skill_lines(
                lines,
                ascension_stage_def.racial_granted_skills,
                _identity_def_label(ascension_stage_def, ascension_stage_def.stage_id)
            );
        return lines;
    }

    private void _append_identity_granted_skill_lines(
        GArray target,
        Godot.Collections.Array<Resource> grants,
        string source_label
    )
    {
        foreach (var grant_option in grants)
        {
            var grant = grant_option as RacialGrantedSkill;
            if (grant == null || grant.skill_id == "")
                continue;
            var line =
                $"{_resolve_skill_label(grant.skill_id)}（{source_label}，{_format_identity_grant_charges(grant)}）";
            if (!target.Contains(line))
                target.Add(line);
        }
    }

    private void _append_identity_granted_skill_lines(
        GArray target,
        Godot.Collections.Array<RacialGrantedSkill> grants,
        string source_label
    )
    {
        foreach (var grant in grants)
        {
            if (grant == null || grant.skill_id == "")
                continue;
            var line =
                $"{_resolve_skill_label(grant.skill_id)}（{source_label}，{_format_identity_grant_charges(grant)}）";
            if (!target.Contains(line))
                target.Add(line);
        }
    }

    private static string _format_identity_grant_charges(RacialGrantedSkill grant)
    {
        if (grant == null)
            return "无次数";
        return grant.ChargeKind switch
        {
            RacialSkillChargeKind.AtWill => "随意",
            RacialSkillChargeKind.PerTurn => $"每回合 {Mathf.Max(grant.charges, 0)} 次",
            RacialSkillChargeKind.PerBattle => $"每场战斗 {Mathf.Max(grant.charges, 0)} 次",
            _ => $"{(string)grant.charge_kind} {Mathf.Max(grant.charges, 0)}",
        };
    }

    private AttributeService _build_attribute_service(PartyMemberState member_state) =>
        _build_attribute_service(member_state, null);

    private AttributeService _build_attribute_service(
        PartyMemberState member_state,
        EquipmentState equipment_state_override
    )
    {
        var attribute_service = new AttributeService();
        attribute_service.SetupContext(
            build_attribute_source_context(member_state.member_id, equipment_state_override)
        );
        return attribute_service;
    }

    private CharacterProgressionDelta _grant_skill_mastery_internal(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text,
        bool emit_achievement_event
    )
    {
        var member_state = GetMemberState(member_id);
        var delta = _new_delta(member_id);
        if (
            member_state == null
            || member_state.progression is not UnitProgress progression
            || amount <= 0
        )
            return delta;

        var before_skill_levels = _capture_skill_levels(progression);
        var before_granted_skill_ids = _capture_granted_skill_ids(progression);
        var before_profession_ranks = _capture_profession_ranks(progression);
        delta.character_level_before = progression.character_level;

        var progression_service = BuildProgressionService(progression);
        var mastery_source_type = _resolve_mastery_source_type(source_type);
        if (!progression_service.GrantSkillMastery(skill_id, amount, mastery_source_type))
        {
            delta.character_level_after = delta.character_level_before;
            return delta;
        }

        delta.AddMasteryChange(
            new CharacterMasteryChangeFact(
                skill_id,
                _resolve_skill_label(skill_id),
                amount,
                source_type,
                !string.IsNullOrEmpty(source_label)
                    ? source_label
                    : _build_default_source_label(source_type),
                reason_text
            )
        );
        _fill_delta_from_progression(
            delta,
            progression,
            before_skill_levels,
            before_granted_skill_ids,
            before_profession_ranks
        );
        if (emit_achievement_event)
            delta.AppendUnlockedAchievementIds(
                RecordAchievementEvent(member_id, "skill_mastery_gained", amount, skill_id)
            );
        return delta;
    }

    private static Dictionary<StringName, int> _capture_skill_levels(UnitProgress progression)
    {
        var skill_levels = new Dictionary<StringName, int>();
        if (progression == null)
            return skill_levels;
        foreach (var skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            if (skill_progress != null)
                skill_levels[skill_id] = skill_progress.skill_level;
        }
        return skill_levels;
    }

    private static HashSet<StringName> _capture_granted_skill_ids(UnitProgress progression)
    {
        var granted_skill_ids = new HashSet<StringName>();
        if (progression == null)
            return granted_skill_ids;
        foreach (var skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            if (skill_progress != null && skill_progress.profession_granted_by != "")
                granted_skill_ids.Add(skill_id);
        }
        return granted_skill_ids;
    }

    private static Dictionary<StringName, int> _capture_profession_ranks(UnitProgress progression)
    {
        var ranks = new Dictionary<StringName, int>();
        if (progression == null)
            return ranks;
        foreach (var profession_id in progression.GetSortedProfessionIdsTyped())
        {
            var progress = progression.GetProfessionProgress(profession_id);
            if (progress != null)
                ranks[profession_id] = progress.rank;
        }
        return ranks;
    }

    private List<PendingCharacterRewardEntry> _normalize_pending_skill_mastery_entries(
        UnitProgress progression,
        IEnumerable<PendingCharacterRewardEntry> entry_options,
        StringName source_type
    )
    {
        var normalized_entries = new List<PendingCharacterRewardEntry>();
        if (progression == null)
            return normalized_entries;
        var entry_map = new Dictionary<StringName, PendingCharacterRewardEntry>();
        if (entry_options == null)
            return normalized_entries;
        foreach (PendingCharacterRewardEntry entry_option in entry_options)
        {
            PendingCharacterRewardEntryData entry_data =
                PendingCharacterRewardEntryData.FromEntry(
                    entry_option,
                    PendingCharacterRewardContentRules.ToStringName(
                        PendingCharacterRewardEntryKind.SkillMastery
                    ),
                    source_type
                );
            if (
                !entry_data.Exists
                || PendingCharacterRewardContentRules.ToEntryKind(entry_data.EntryType)
                    != PendingCharacterRewardEntryKind.SkillMastery
            )
                continue;
            var skill_id = entry_data.TargetId;
            var mastery_amount = entry_data.Amount;
            if (skill_id == "" || mastery_amount <= 0)
                continue;
            var mastery_source_type = _resolve_mastery_source_type(entry_data.MasterySourceType);
            var skill_progress = progression.GetSkillProgress(skill_id);
            var skill_def = GetSkillDef(skill_id);
            if (skill_progress == null || skill_def == null || !skill_progress.is_learned)
                continue;
            if (
                skill_def.MasterySourcesTyped.Count > 0
                && !HasStringName(skill_def.MasterySourcesTyped, mastery_source_type)
            )
                continue;
            if (!entry_map.TryGetValue(skill_id, out var reward_entry))
            {
                reward_entry = new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                    target_id = skill_id,
                    target_label = _resolve_skill_label(skill_id),
                    reason_text = entry_data.ReasonText,
                };
                entry_map[skill_id] = reward_entry;
                normalized_entries.Add(reward_entry);
            }
            reward_entry.amount += mastery_amount;
            if (string.IsNullOrEmpty(reward_entry.reason_text))
                reward_entry.reason_text = entry_data.ReasonText;
        }
        return normalized_entries;
    }

    private PendingCharacterReward _normalize_pending_character_reward_option(
        PendingCharacterReward raw_reward_option,
        bool allow_unsupported_entries = false
    )
    {
        if (raw_reward_option == null)
            return null;
        if (
            !allow_unsupported_entries
            && _has_unsupported_pending_character_entry_object(raw_reward_option.entries)
        )
            return null;
        if (raw_reward_option.reward_id == "")
            raw_reward_option.reward_id = _build_reward_id(
                raw_reward_option.member_id,
                raw_reward_option.source_id != ""
                    ? raw_reward_option.source_id
                    : raw_reward_option.source_type
            );
        return raw_reward_option.IsEmpty() ? null : raw_reward_option;
    }

    private QuestRewardData _resolve_quest_reward_data(StringName quest_id)
    {
        QuestDef questDef = GetQuestDef(quest_id);
        if (questDef == null)
            return QuestRewardData.Missing();
        return QuestRewardData.FromQuestDef(questDef);
    }

    private QuestSubmitItemPreviewData PreviewQuestSubmitItemObjective(
        StringName quest_id,
        StringName objective_id = default
    )
    {
        if (_party_state == null || quest_id == "")
        {
            return QuestSubmitItemPreviewData.Failed("invalid_quest_id");
        }
        var quest_state = _party_state.GetActiveQuestState(quest_id);
        if (quest_state == null)
        {
            return QuestSubmitItemPreviewData.Failed("quest_not_active");
        }
        QuestDef questDef = GetQuestDef(quest_id);
        if (questDef == null)
        {
            return QuestSubmitItemPreviewData.Failed("quest_def_missing");
        }

        IReadOnlyList<QuestObjectiveDefData> objective_defs = ReadQuestObjectiveDefs(questDef);

        var requested_objective_id = ProgressionDataUtils.to_string_name(objective_id);
        var found_completed_submit_item_objective = false;
        var completed_preview = QuestSubmitItemPreviewData.Failed("objective_already_complete");
        foreach (var objective_data in objective_defs)
        {
            if (
                !objective_data.Exists
                || QuestDef.ToObjectiveKind(objective_data.ObjectiveType) != QuestObjectiveKind.SubmitItem
            )
                continue;
            var current_objective_id = objective_data.ObjectiveId;
            if (requested_objective_id != "" && current_objective_id != requested_objective_id)
                continue;
            var item_id = objective_data.TargetId;
            var target_value = objective_data.TargetValue;
            var required_quantity = Mathf.Max(
                target_value - quest_state.GetObjectiveProgress(current_objective_id),
                0
            );
            if (current_objective_id == "" || item_id == "" || target_value <= 0)
            {
                return QuestSubmitItemPreviewData.Failed(
                    "invalid_submit_item_objective",
                    current_objective_id,
                    item_id,
                    target_value,
                    required_quantity
                );
            }
            if (quest_state.IsObjectiveComplete(current_objective_id, target_value))
            {
                found_completed_submit_item_objective = true;
                completed_preview = QuestSubmitItemPreviewData.Failed(
                    "objective_already_complete",
                    current_objective_id,
                    item_id,
                    target_value,
                    required_quantity
                );
                if (requested_objective_id != "")
                    return completed_preview;
                continue;
            }
            return QuestSubmitItemPreviewData.Success(
                current_objective_id,
                item_id,
                target_value,
                required_quantity
            );
        }
        return found_completed_submit_item_objective
            ? completed_preview
            : QuestSubmitItemPreviewData.Failed("objective_not_found");
    }

    private QuestRewardPreviewData _preview_quest_reward_claim(
        StringName quest_id,
        QuestRewardData quest_reward_data
    )
    {
        var quest_label = quest_reward_data.DisplayName.StripEdges();
        if (quest_label.Length == 0)
            return QuestRewardPreviewData.Failed("invalid_quest_display_name");
        var unsupported_reward_types = new GStringNameArray();
        var reward_item_entries = new GArray();
        var reward_item_ids = new List<StringName>();
        var pending_character_rewards = new List<PendingCharacterReward>();
        var gold_delta = 0;
        foreach (var reward_data in quest_reward_data.RewardEntries)
        {
            if (!reward_data.Exists)
                return QuestRewardPreviewData.Failed("invalid_reward_entry");
            var reward_type = reward_data.RewardType;
            if (reward_type == "")
                return QuestRewardPreviewData.Failed("invalid_reward_entry");
            if (QuestDef.ToRewardKind(reward_type) == QuestRewardKind.Gold)
            {
                var amount = reward_data.Amount;
                if (amount <= 0)
                    return QuestRewardPreviewData.Failed("invalid_gold_amount");
                gold_delta += amount;
            }
            else if (QuestDef.ToRewardKind(reward_type) == QuestRewardKind.Item)
            {
                var item_reward_result = _preview_quest_item_reward_entry(reward_data);
                if (!item_reward_result.Ok)
                    return QuestRewardPreviewData.Failed(item_reward_result.ErrorCode);
                var reward_item_entry = item_reward_result.CloneItemReward();
                if (reward_item_entry.Count > 0)
                    reward_item_entries.Add(reward_item_entry);
                foreach (var item_id in item_reward_result.CloneWarehouseDepositItemIds())
                    reward_item_ids.Add(item_id);
            }
            else if (QuestDef.ToRewardKind(reward_type) == QuestRewardKind.PendingCharacterReward)
            {
                var pending_reward_result = _preview_quest_pending_character_reward_entry(
                    quest_id,
                    quest_label,
                    reward_data
                );
                if (!pending_reward_result.Ok)
                    return QuestRewardPreviewData.Failed(pending_reward_result.ErrorCode);
                var reward = pending_reward_result.PendingReward;
                if (reward != null && !reward.IsEmpty())
                    pending_character_rewards.Add(reward);
            }
            else
            {
                _append_unique_string_name(unsupported_reward_types, reward_type);
            }
        }
        if (unsupported_reward_types.Count > 0)
        {
            return QuestRewardPreviewData.Failed(
                "unsupported_reward_types",
                unsupportedRewardTypes: unsupported_reward_types
            );
        }
        if (reward_item_ids.Count > 0)
        {
            var warehouse_preview = _party_warehouse_service.PreviewBatchSwapTyped(
                new List<StringName>(),
                reward_item_ids
            );
            if (!warehouse_preview.Allowed)
                return QuestRewardPreviewData.Failed(
                    _resolve_quest_reward_warehouse_error_code(warehouse_preview.ErrorCode)
                );
        }
        return QuestRewardPreviewData.Success(
            gold_delta,
            reward_item_entries,
            reward_item_ids,
            pending_character_rewards
        );
    }

    private QuestItemRewardPreviewData _preview_quest_item_reward_entry(
        QuestRewardEntryData reward_data
    )
    {
        var reward_item_id = reward_data.ItemId;
        var reward_quantity = reward_data.Quantity;
        if (reward_item_id == "" || reward_quantity <= 0)
            return QuestItemRewardPreviewData.Failed("invalid_item_reward");
        var itemDef = GetItemDef(reward_item_id);
        if (itemDef == null)
            return QuestItemRewardPreviewData.Failed("item_reward_missing_def");
        var item_display_name = itemDef.display_name.StripEdges();
        if (item_display_name.Length == 0)
            return QuestItemRewardPreviewData.Failed("invalid_item_display_name");
        return QuestItemRewardPreviewData.Success(
            reward_item_id,
            item_display_name,
            reward_quantity,
            _build_repeated_item_ids(reward_item_id, reward_quantity)
        );
    }

    private QuestPendingCharacterRewardPreviewData _preview_quest_pending_character_reward_entry(
        StringName quest_id,
        string quest_label,
        QuestRewardEntryData reward_data
    )
    {
        var member_id = reward_data.MemberId;
        var entry_options = reward_data.CloneEntries();
        if (
            member_id == ""
            || entry_options.Count == 0
        )
            return QuestPendingCharacterRewardPreviewData.Failed(
                "invalid_pending_character_reward"
            );
        var source_type = reward_data.SourceType != "" ? reward_data.SourceType : RewardTypeQuest;
        var source_id = reward_data.SourceId != "" ? reward_data.SourceId : quest_id;
        if (source_id == "")
            source_id = quest_id != "" ? quest_id : source_type;
        var source_label = reward_data.SourceLabel.StripEdges();
        if (source_label.Length == 0)
            source_label = quest_label;
        var summary_text = reward_data.SummaryText.StripEdges();
        var pending_reward = BuildPendingCharacterReward(
            member_id,
            reward_data.RewardId,
            source_type,
            source_id,
            source_label,
            entry_options,
            summary_text
        );
        return pending_reward == null || pending_reward.IsEmpty()
            ? QuestPendingCharacterRewardPreviewData.Failed("invalid_pending_character_reward")
            : QuestPendingCharacterRewardPreviewData.Success(pending_reward);
    }

    private QuestDef GetQuestDef(StringName questId)
    {
        return questId != "" && _quest_def_index.TryGetValue(questId, out QuestDef questDef)
            ? questDef
            : null;
    }

    private static Dictionary<StringName, QuestDef> BuildQuestDefIndex(GDictionary questDefs)
    {
        Dictionary<StringName, QuestDef> questDefIndex = new();
        if (questDefs == null)
            return questDefIndex;

        foreach (Variant rawKey in questDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            if (questDefs[rawKey].AsGodotObject() is not QuestDef questDef)
                continue;
            if (questDef.quest_id == "")
                continue;
            questDefIndex[rawKey.AsStringName()] = questDef;
        }
        return questDefIndex;
    }

    private static Dictionary<StringName, QuestDef> CloneQuestDefIndex(
        IReadOnlyDictionary<StringName, QuestDef> questDefs
    )
    {
        Dictionary<StringName, QuestDef> questDefIndex = new();
        if (questDefs == null)
            return questDefIndex;

        foreach ((StringName questId, QuestDef questDef) in questDefs)
        {
            if (questId == "" || questDef == null || questDef.quest_id == "")
                continue;
            questDefIndex[questId] = questDef;
        }
        return questDefIndex;
    }

    private static Dictionary<StringName, T> CloneContentDefIndex<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        Dictionary<StringName, T> result = new();
        if (source == null)
            return result;
        foreach ((StringName key, T value) in source)
        {
            if (key != "" && value != null)
                result[key] = value;
        }
        return result;
    }

    private static GDictionary ProjectContentDefs<T>(IReadOnlyDictionary<StringName, T> source)
        where T : class
    {
        GDictionary result = new();
        if (source == null)
            return result;
        foreach (StringName key in SortedContentKeys(source))
        {
            if (source.TryGetValue(key, out T value) && value != null)
                result[key] = Variant.From(value);
        }
        return result;
    }

    private static List<StringName> SortedContentKeys<T>(IReadOnlyDictionary<StringName, T> source)
    {
        List<StringName> result = new();
        if (source == null)
            return result;
        foreach (StringName key in source.Keys)
            result.Add(key);
        result.Sort((left, right) => string.CompareOrdinal((string)left, (string)right));
        return result;
    }

    private static IReadOnlyList<QuestObjectiveDefData> ReadQuestObjectiveDefs(QuestDef questDef)
    {
        List<QuestObjectiveDefData> objectiveDefs = new();
        if (questDef == null)
            return objectiveDefs;
        foreach (QuestDef.ObjectiveEntryData objectiveData in questDef.GetObjectiveEntriesTyped())
        {
            var objectiveDef = QuestObjectiveDefData.FromQuestObjectiveEntry(objectiveData);
            if (objectiveDef.Exists)
                objectiveDefs.Add(objectiveDef);
        }
        return objectiveDefs;
    }

    private static List<StringName> _build_repeated_item_ids(StringName item_id, int quantity)
    {
        var item_ids = new List<StringName>();
        for (var i = 0; i < Mathf.Max(quantity, 0); i++)
            item_ids.Add(item_id);
        return item_ids;
    }

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> source)
    {
        var result = new List<StringName>();
        if (source == null)
            return result;
        foreach (var value in source)
            result.Add(value);
        return result;
    }

    private static List<PendingCharacterRewardEntry> DuplicatePendingCharacterRewardEntries(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        var result = new List<PendingCharacterRewardEntry>();
        if (entries == null)
            return result;
        foreach (PendingCharacterRewardEntry entry in entries)
        {
            if (entry == null || entry.IsEmpty())
                continue;
            result.Add(entry.DuplicateState());
        }
        return result;
    }

    private static List<PendingCharacterRewardEntry> ClonePendingCharacterRewardEntryList(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        var result = new List<PendingCharacterRewardEntry>();
        if (entries == null)
            return result;
        foreach (PendingCharacterRewardEntry entry in entries)
        {
            if (entry == null || entry.IsEmpty())
                continue;
            result.Add(entry.DuplicateState());
        }
        return result;
    }

    private static List<PendingCharacterReward> ClonePendingCharacterRewardList(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        var result = new List<PendingCharacterReward>();
        if (rewards == null)
            return result;
        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward == null || reward.IsEmpty())
                continue;
            result.Add(reward.DuplicateState());
        }
        return result;
    }

    private static GStringNameArray CloneStringNameArray(GStringNameArray source)
    {
        var result = new GStringNameArray();
        if (source == null)
            return result;
        foreach (var value in source)
            result.Add(value);
        return result;
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> source)
    {
        var result = new GStringNameArray();
        if (source == null)
            return result;
        foreach (var value in source)
            result.Add(value);
        return result;
    }

    private sealed class PendingCharacterRewardEntryData
    {
        public readonly bool Exists;
        public readonly StringName EntryType;
        public readonly StringName TargetId;
        public readonly int Amount;
        public readonly string TargetLabel;
        public readonly string ReasonText;
        public readonly StringName SourceType;
        public readonly StringName MasterySourceType;

        private PendingCharacterRewardEntryData(
            bool exists,
            StringName entryType,
            StringName targetId,
            int amount,
            string targetLabel,
            string reasonText,
            StringName sourceType,
            StringName masterySourceType
        )
        {
            Exists = exists;
            EntryType = entryType;
            TargetId = targetId;
            Amount = amount;
            TargetLabel = targetLabel ?? "";
            ReasonText = reasonText ?? "";
            SourceType = sourceType;
            MasterySourceType = masterySourceType != "" ? masterySourceType : sourceType;
        }

        public static PendingCharacterRewardEntryData FromDictionary(
            GDictionary data,
            StringName defaultEntryType = default,
            StringName defaultSourceType = default
        )
        {
            if (data == null || data.Count == 0)
                return Missing();
            var entryType = CharacterQuestDataReader.ReadStringName(data, "entry_type");
            if (entryType == "")
                entryType = defaultEntryType;
            CharacterQuestDataReader.TryReadInt(data, "amount", out var amount);
            var sourceType = CharacterQuestDataReader.ReadStringName(data, "source_type");
            if (sourceType == "")
                sourceType = defaultSourceType;
            var masterySourceType = CharacterQuestDataReader.ReadStringName(
                data,
                "mastery_source_type"
            );
            if (masterySourceType == "")
                masterySourceType = sourceType;
            return new PendingCharacterRewardEntryData(
                true,
                entryType,
                CharacterQuestDataReader.ReadStringName(data, "target_id"),
                amount,
                CharacterQuestDataReader.ReadString(data, "target_label"),
                CharacterQuestDataReader.ReadString(data, "reason_text"),
                sourceType,
                masterySourceType
            );
        }

        public static PendingCharacterRewardEntryData FromEntry(
            PendingCharacterRewardEntry entry,
            StringName defaultEntryType,
            StringName defaultSourceType
        )
        {
            if (entry == null)
                return Missing();
            var entryType = entry.entry_type != "" ? entry.entry_type : defaultEntryType;
            return new PendingCharacterRewardEntryData(
                true,
                entryType,
                entry.target_id,
                entry.amount,
                entry.target_label,
                entry.reason_text,
                defaultSourceType,
                entry.mastery_source_type != "" ? entry.mastery_source_type : defaultSourceType
            );
        }

        private static PendingCharacterRewardEntryData Missing() =>
            new(false, "", "", 0, "", "", "", "");

        public PendingCharacterRewardEntry ToRewardEntry()
        {
            if (!Exists)
                return null;
            return new PendingCharacterRewardEntry
            {
                entry_type = EntryType,
                target_id = TargetId,
                target_label = TargetLabel,
                amount = Amount,
                reason_text = ReasonText,
            };
        }
    }

    private static string _resolve_quest_reward_warehouse_error_code(string warehouse_error_code) =>
        warehouse_error_code == "warehouse_blocked_swap"
            ? "reward_overflow"
            : "quest_reward_commit_failed";

    private sealed class QuestSubmitItemPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly StringName ObjectiveId;
        public readonly StringName ItemId;
        public readonly int TargetValue;
        public readonly int RequiredQuantity;

        private QuestSubmitItemPreviewData(
            bool ok,
            string errorCode,
            StringName objectiveId,
            StringName itemId,
            int targetValue,
            int requiredQuantity
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            ObjectiveId = objectiveId;
            ItemId = itemId;
            TargetValue = Mathf.Max(targetValue, 0);
            RequiredQuantity = Mathf.Max(requiredQuantity, 0);
        }

        public static QuestSubmitItemPreviewData Success(
            StringName objectiveId,
            StringName itemId,
            int targetValue,
            int requiredQuantity
        ) =>
            new(true, "", objectiveId, itemId, targetValue, requiredQuantity);

        public static QuestSubmitItemPreviewData Failed(
            string errorCode,
            StringName objectiveId = default,
            StringName itemId = default,
            int targetValue = 0,
            int requiredQuantity = 0
        ) =>
            new(false, errorCode, objectiveId, itemId, targetValue, requiredQuantity);
    }

    private sealed class QuestObjectiveDefData
    {
        public readonly bool Exists;
        public readonly StringName ObjectiveId;
        public readonly StringName ObjectiveType;
        public readonly StringName TargetId;
        public readonly int TargetValue;

        private QuestObjectiveDefData(
            bool exists,
            StringName objectiveId,
            StringName objectiveType,
            StringName targetId,
            int targetValue
        )
        {
            Exists = exists;
            ObjectiveId = objectiveId;
            ObjectiveType = objectiveType;
            TargetId = targetId;
            TargetValue = Mathf.Max(targetValue, 0);
        }

        public static QuestObjectiveDefData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Empty();
            return FromDictionary(value.AsGodotDictionary());
        }

        public static QuestObjectiveDefData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Empty();
            return new QuestObjectiveDefData(
                true,
                CharacterQuestDataReader.ReadStringName(data, "objective_id"),
                CharacterQuestDataReader.ReadStringName(data, "objective_type"),
                CharacterQuestDataReader.ReadStringName(data, "target_id"),
                CharacterQuestDataReader.TryReadInt(data, "target_value", out var targetValue)
                    ? targetValue
                    : 0
            );
        }

        public static QuestObjectiveDefData FromQuestObjectiveEntry(
            QuestDef.ObjectiveEntryData entry
        )
        {
            if (entry == null)
                return Empty();
            return new QuestObjectiveDefData(
                true,
                entry.ObjectiveId,
                entry.ObjectiveType,
                entry.TargetId,
                entry.HasStrictTargetValue ? entry.TargetValue : 0
            );
        }

        private static QuestObjectiveDefData Empty() => new(false, "", "", "", 0);
    }

    private sealed class QuestRewardData
    {
        public readonly bool Found;
        public readonly string ErrorCode;
        public readonly string DisplayName;
        public readonly IReadOnlyList<QuestRewardEntryData> RewardEntries;

        private QuestRewardData(
            bool found,
            string errorCode,
            string displayName,
            IReadOnlyList<QuestRewardEntryData> rewardEntries
        )
        {
            Found = found;
            ErrorCode = errorCode ?? "";
            DisplayName = displayName ?? "";
            RewardEntries = rewardEntries ?? new List<QuestRewardEntryData>();
        }

        public static QuestRewardData Missing() =>
            new(false, "quest_def_missing", "", new List<QuestRewardEntryData>());

        public static QuestRewardData FromDictionary(GDictionary questData)
        {
            string displayName = CharacterQuestDataReader.ReadTrimmedString(
                questData,
                "display_name"
            );
            string errorCode = displayName.Length == 0 ? "invalid_quest_display_name" : "";
            return new QuestRewardData(
                true,
                errorCode,
                displayName,
                QuestRewardEntryData.FromArray(
                    CharacterQuestDataReader.ReadArray(questData, "reward_entries")
                )
            );
        }

        public static QuestRewardData FromQuestDef(QuestDef questDef)
        {
            if (questDef == null)
                return Missing();
            string displayName = (questDef.display_name ?? "").StripEdges();
            string errorCode = displayName.Length == 0 ? "invalid_quest_display_name" : "";
            var rewardEntries = new List<QuestRewardEntryData>();
            foreach (QuestDef.RewardEntryData rewardEntry in questDef.GetRewardEntriesTyped())
                rewardEntries.Add(QuestRewardEntryData.FromQuestRewardEntry(rewardEntry));
            return new QuestRewardData(true, errorCode, displayName, rewardEntries);
        }
    }

    private sealed class QuestRewardEntryData
    {
        public readonly bool Exists;
        public readonly StringName RewardType;
        public readonly int Amount;
        public readonly StringName ItemId;
        public readonly int Quantity;
        public readonly StringName MemberId;
        public readonly StringName SourceType;
        public readonly StringName SourceId;
        public readonly string SourceLabel;
        public readonly string SummaryText;
        public readonly StringName RewardId;
        private readonly List<PendingCharacterRewardEntry> _entries;

        private QuestRewardEntryData(
            bool exists,
            StringName rewardType,
            int amount,
            StringName itemId,
            int quantity,
            StringName memberId,
            StringName sourceType,
            StringName sourceId,
            string sourceLabel,
            string summaryText,
            StringName rewardId,
            IEnumerable<PendingCharacterRewardEntry> entries
        )
        {
            Exists = exists;
            RewardType = rewardType;
            Amount = Mathf.Max(amount, 0);
            ItemId = itemId;
            Quantity = Mathf.Max(quantity, 0);
            MemberId = memberId;
            SourceType = sourceType;
            SourceId = sourceId;
            SourceLabel = sourceLabel ?? "";
            SummaryText = summaryText ?? "";
            RewardId = rewardId;
            _entries = ClonePendingCharacterRewardEntryList(entries);
        }

        internal List<PendingCharacterRewardEntry> CloneEntries() =>
            DuplicatePendingCharacterRewardEntries(_entries);

        public static IReadOnlyList<QuestRewardEntryData> FromArray(GArray rewardEntries)
        {
            var result = new List<QuestRewardEntryData>();
            if (rewardEntries == null)
                return result;
            foreach (Variant rewardEntry in rewardEntries)
                result.Add(FromVariant(rewardEntry));
            return result;
        }

        public static QuestRewardEntryData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Missing();
            return FromDictionary(value.AsGodotDictionary());
        }

        public static QuestRewardEntryData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Missing();
            CharacterQuestDataReader.TryReadInt(data, "amount", out var amount);
            CharacterQuestDataReader.TryReadInt(data, "quantity", out var quantity);
            return new QuestRewardEntryData(
                true,
                CharacterQuestDataReader.ReadStringName(data, "reward_type"),
                amount,
                CharacterQuestDataReader.ReadStringName(data, "item_id"),
                quantity,
                CharacterQuestDataReader.ReadStringName(data, "member_id"),
                CharacterQuestDataReader.ReadStringName(data, "source_type"),
                CharacterQuestDataReader.ReadStringName(data, "source_id"),
                CharacterQuestDataReader.ReadTrimmedString(data, "source_label"),
                CharacterQuestDataReader.ReadTrimmedString(data, "summary_text"),
                CharacterQuestDataReader.ReadStringName(data, "reward_id"),
                ProjectPendingRewardEntryDictionaries(
                    CharacterQuestDataReader.ReadArray(data, "entries")
                )
            );
        }

        public static QuestRewardEntryData FromQuestRewardEntry(QuestDef.RewardEntryData entry)
        {
            if (entry == null)
                return Missing();
            return new QuestRewardEntryData(
                true,
                entry.RewardType,
                entry.HasStrictGoldAmount ? entry.GoldAmount : 0,
                entry.ItemId,
                entry.HasStrictItemQuantity ? entry.ItemQuantity : 0,
                entry.PendingRewardMemberId,
                "",
                "",
                "",
                "",
                "",
                ProjectPendingRewardEntries(entry.PendingRewardEntries)
            );
        }

        private static QuestRewardEntryData Missing() =>
            new(false, "", 0, "", 0, "", "", "", "", "", "", new List<PendingCharacterRewardEntry>());

        private static List<PendingCharacterRewardEntry> ProjectPendingRewardEntries(
            IReadOnlyList<QuestDef.PendingRewardEntryData> entries
        )
        {
            var result = new List<PendingCharacterRewardEntry>();
            if (entries == null)
                return result;
            foreach (QuestDef.PendingRewardEntryData entry in entries)
            {
                if (entry == null || !entry.IsDictionaryEntry)
                    continue;
                result.Add(
                    new PendingCharacterRewardEntry
                    {
                        entry_type = entry.EntryType,
                        target_id = entry.TargetId,
                        amount = entry.HasStrictAmount ? entry.Amount : 0,
                    }
                );
            }
            return result;
        }

        private static List<PendingCharacterRewardEntry> ProjectPendingRewardEntryDictionaries(
            GArray entries
        )
        {
            var result = new List<PendingCharacterRewardEntry>();
            if (entries == null)
                return result;
            foreach (Variant entryValue in entries)
            {
                if (!entryValue.TryAsDictionary(out GDictionary entryData))
                    continue;
                PendingCharacterRewardEntryData entry = PendingCharacterRewardEntryData.FromDictionary(
                    entryData
                );
                PendingCharacterRewardEntry rewardEntry = entry.ToRewardEntry();
                if (rewardEntry != null)
                    result.Add(rewardEntry);
            }
            return result;
        }
    }

    private sealed class QuestRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly int GoldDelta;
        private readonly RuntimePayloadList _itemRewards;
        private readonly List<StringName> _warehouseDepositItemIds;
        private readonly List<PendingCharacterReward> _pendingCharacterRewards;
        private readonly List<StringName> _unsupportedRewardTypes;

        private QuestRewardPreviewData(
            bool ok,
            string errorCode,
            int goldDelta,
            GArray itemRewards,
            IReadOnlyList<StringName> warehouseDepositItemIds,
            IEnumerable<PendingCharacterReward> pendingCharacterRewards,
            GStringNameArray unsupportedRewardTypes
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            GoldDelta = Mathf.Max(goldDelta, 0);
            _itemRewards = new RuntimePayloadList(itemRewards);
            _warehouseDepositItemIds =
                warehouseDepositItemIds != null
                    ? CloneStringNameList(warehouseDepositItemIds)
                    : new List<StringName>();
            _pendingCharacterRewards = DuplicatePendingCharacterRewards(pendingCharacterRewards);
            _unsupportedRewardTypes =
                unsupportedRewardTypes != null
                    ? CloneStringNameList(unsupportedRewardTypes)
                    : new List<StringName>();
        }

        internal GArray CloneItemRewards() => _itemRewards.ToUntypedGodotArray();

        public List<StringName> CloneWarehouseDepositItemIds() =>
            CloneStringNameList(_warehouseDepositItemIds);

        internal List<PendingCharacterReward> ClonePendingCharacterRewards() =>
            DuplicatePendingCharacterRewards(_pendingCharacterRewards);

        public GStringNameArray CloneUnsupportedRewardTypes() =>
            ToStringNameArray(_unsupportedRewardTypes);

        public static QuestRewardPreviewData Success(
            int goldDelta,
            GArray itemRewards,
            IReadOnlyList<StringName> warehouseDepositItemIds,
            IEnumerable<PendingCharacterReward> pendingCharacterRewards
        ) =>
            new(
                true,
                "",
                goldDelta,
                itemRewards,
                warehouseDepositItemIds,
                pendingCharacterRewards,
                new GStringNameArray()
            );

        public static QuestRewardPreviewData Failed(
            string errorCode,
            GStringNameArray unsupportedRewardTypes = null
        ) =>
            new(
                false,
                errorCode,
                0,
                new GArray(),
                new GStringNameArray(),
                new List<PendingCharacterReward>(),
                unsupportedRewardTypes
            );

        private static List<PendingCharacterReward> DuplicatePendingCharacterRewards(
            IEnumerable<PendingCharacterReward> rewards
        )
        {
            var result = new List<PendingCharacterReward>();
            if (rewards == null)
                return result;
            foreach (PendingCharacterReward reward in rewards)
                if (reward != null && !reward.IsEmpty())
                    result.Add(reward.DuplicateState());
            return result;
        }
    }

    private sealed class QuestItemRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        private readonly StringName _itemId;
        private readonly string _displayName;
        private readonly int _quantity;
        private readonly List<StringName> _warehouseDepositItemIds;

        private QuestItemRewardPreviewData(
            bool ok,
            string errorCode,
            StringName itemId,
            string displayName,
            int quantity,
            IReadOnlyList<StringName> warehouseDepositItemIds
        )
        {
            Ok = ok;
            ErrorCode = ok
                ? ""
                : string.IsNullOrEmpty(errorCode)
                    ? "invalid_item_reward"
                    : errorCode;
            _itemId = itemId;
            _displayName = displayName ?? "";
            _quantity = Mathf.Max(quantity, 0);
            _warehouseDepositItemIds =
                warehouseDepositItemIds != null
                    ? CloneStringNameList(warehouseDepositItemIds)
                    : new List<StringName>();
        }

        internal GDictionary CloneItemReward()
        {
            if (_itemId == "" || _displayName.Length == 0 || _quantity <= 0)
                return new GDictionary();

            return new GDictionary
            {
                ["item_id"] = _itemId.ToString(),
                ["display_name"] = _displayName,
                ["quantity"] = _quantity,
            };
        }

        public List<StringName> CloneWarehouseDepositItemIds() =>
            CloneStringNameList(_warehouseDepositItemIds);

        public static QuestItemRewardPreviewData Success(
            StringName itemId,
            string displayName,
            int quantity,
            IReadOnlyList<StringName> warehouseDepositItemIds
        ) =>
            new(true, "", itemId, displayName, quantity, warehouseDepositItemIds);

        public static QuestItemRewardPreviewData Failed(string errorCode) =>
            new(false, errorCode, "", "", 0, new List<StringName>());
    }

    private sealed class QuestPendingCharacterRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly PendingCharacterReward PendingReward;

        private QuestPendingCharacterRewardPreviewData(
            bool ok,
            string errorCode,
            PendingCharacterReward pendingReward
        )
        {
            Ok = ok;
            ErrorCode = ok
                ? ""
                : string.IsNullOrEmpty(errorCode)
                    ? "invalid_pending_character_reward"
                    : errorCode;
            PendingReward = pendingReward;
        }

        public static QuestPendingCharacterRewardPreviewData Success(
            PendingCharacterReward pendingReward
        ) =>
            new(true, "", pendingReward);

        public static QuestPendingCharacterRewardPreviewData Failed(string errorCode) =>
            new(false, errorCode, null);
    }

    private static class CharacterQuestDataReader
    {
        internal static string ReadString(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return "";
            return value.VariantType switch
            {
                Variant.Type.String => value.AsString(),
                Variant.Type.StringName => value.AsStringName().ToString(),
                _ => "",
            };
        }

        internal static bool TryReadString(GDictionary data, string key, out string result)
        {
            if (!TryGet(data, key, out Variant value))
            {
                result = "";
                return false;
            }
            if (value.VariantType == Variant.Type.String)
            {
                result = value.AsString();
                return true;
            }
            if (value.VariantType == Variant.Type.StringName)
            {
                result = value.AsStringName().ToString();
                return true;
            }
            result = "";
            return false;
        }

        internal static string ReadTrimmedString(GDictionary data, string key) =>
            ReadString(data, key).StripEdges();

        internal static StringName ReadStringName(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return "";
            return value.VariantType switch
            {
                Variant.Type.StringName => value.AsStringName(),
                Variant.Type.String => new StringName(value.AsString().StripEdges()),
                _ => new StringName(""),
            };
        }

        internal static bool TryReadInt(GDictionary data, string key, out int result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Int)
            {
                result = 0;
                return false;
            }
            result = value.AsInt32();
            return true;
        }

        internal static GArray ReadArray(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return new GArray();
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
        }

        internal static GStringNameArray ReadStringNameArray(GDictionary data, string key)
        {
            GStringNameArray result = new();
            foreach (Variant value in ReadArray(data, key))
            {
                if (value.VariantType == Variant.Type.StringName)
                    result.Add(value.AsStringName());
                else if (value.VariantType == Variant.Type.String)
                    result.Add(new StringName(value.AsString()));
            }
            return result;
        }

        private static bool TryGet(GDictionary data, string key, out Variant value)
        {
            if (data == null || string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }
            foreach (Variant rawKey in data.Keys)
            {
                if (rawKey.VariantType == Variant.Type.String && rawKey.AsString() == key)
                {
                    value = data[rawKey];
                    return true;
                }
                if (
                    rawKey.VariantType == Variant.Type.StringName
                    && rawKey.AsStringName().ToString() == key
                )
                {
                    value = data[rawKey];
                    return true;
                }
            }
            value = default;
            return false;
        }
    }

    private GArray _pending_character_rewards_to_dicts(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        var reward_dicts = new GArray();
        if (rewards == null)
            return reward_dicts;
        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward == null || reward.IsEmpty())
                continue;
            reward_dicts.Add(PendingCharacterRewardPayload.Project(reward));
        }
        return reward_dicts;
    }

    private List<PendingCharacterRewardEntry> _normalize_pending_character_entries(
        IEnumerable<PendingCharacterRewardEntry> entry_options
    )
    {
        var entries = new List<PendingCharacterRewardEntry>();
        if (entry_options == null)
            return entries;
        foreach (PendingCharacterRewardEntry entry_option in entry_options)
        {
            PendingCharacterRewardEntry entry =
                _normalize_pending_character_entry(
                    PendingCharacterRewardEntryData.FromEntry(entry_option, default, default)
                );
            if (entry != null && !entry.IsEmpty())
                entries.Add(entry);
        }
        return entries;
    }

    private bool _has_unsupported_pending_character_entry_type(
        IEnumerable<PendingCharacterRewardEntry> entry_options
    )
    {
        if (entry_options == null)
            return false;
        foreach (PendingCharacterRewardEntry entry_option in entry_options)
        {
            PendingCharacterRewardEntryData entry_data =
                PendingCharacterRewardEntryData.FromEntry(entry_option, default, default);
            if (
                entry_data.Exists
                && _is_unsupported_pending_character_entry(
                    entry_data.EntryType,
                    entry_data.TargetId
                )
            )
                return true;
        }
        return false;
    }

    private bool _has_unsupported_pending_character_entry_object(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        foreach (var entry in entries)
            if (
                entry != null
                && _is_unsupported_pending_character_entry(entry.entry_type, entry.target_id)
            )
                return true;
        return false;
    }

    private static bool _is_unsupported_pending_character_entry(
        StringName entry_type,
        StringName target_id
    )
    {
        if (entry_type == "")
            return false;
        if (!PendingCharacterRewardContentRules.IsSupportedEntryType(entry_type))
            return true;
        if (
            PendingCharacterRewardContentRules.IsAttributeProgressEntry(entry_type)
            && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(target_id)
        )
            return true;
        return false;
    }

    private PendingCharacterRewardEntry _normalize_pending_character_entry(
        PendingCharacterRewardEntryData entry_data
    )
    {
        if (entry_data == null || !entry_data.Exists)
            return null;
        var entry_type = entry_data.EntryType;
        var target_id = entry_data.TargetId;
        var amount = entry_data.Amount;
        if (entry_type == "" || target_id == "" || amount == 0)
            return null;
        if (!PendingCharacterRewardContentRules.IsSupportedEntryType(entry_type))
            return null;
        if (
            PendingCharacterRewardContentRules.IsAttributeProgressEntry(entry_type)
            && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(target_id)
        )
            return null;
        var entry = new PendingCharacterRewardEntry
        {
            entry_type = entry_type,
            target_id = target_id,
            amount = amount,
            target_label = entry_data.TargetLabel,
            reason_text = entry_data.ReasonText,
        };
        if (string.IsNullOrEmpty(entry.target_label))
            entry.target_label = _resolve_reward_target_label(entry.entry_type, entry.target_id, "");
        return entry;
    }

    private PendingCharacterReward _build_achievement_pending_reward(
        PartyMemberState member_state,
        AchievementDef achievement_def,
        GDictionary meta
    )
    {
        if (member_state == null || achievement_def == null)
            return null;
        var reward = new PendingCharacterReward
        {
            reward_id = _build_reward_id(member_state.member_id, achievement_def.achievement_id),
            member_id = member_state.member_id,
            member_name = !string.IsNullOrEmpty(member_state.display_name)
                ? member_state.display_name
                : (string)member_state.member_id,
            source_type = RewardTypeAchievement,
            source_id = achievement_def.achievement_id,
            source_label = !string.IsNullOrEmpty(achievement_def.display_name)
                ? achievement_def.display_name
                : (string)achievement_def.achievement_id,
            summary_text = CharacterQuestDataReader.TryReadString(
                meta,
                "summary_text",
                out var summary_text
            )
                ? summary_text
                : achievement_def.description,
            entries = _build_achievement_reward_entries(achievement_def),
        };
        return reward.IsEmpty() ? null : reward;
    }

    private List<PendingCharacterRewardEntry> _build_achievement_reward_entries(
        AchievementDef achievement_def
    )
    {
        var entries = new List<PendingCharacterRewardEntry>();
        if (achievement_def == null)
            return entries;
        foreach (AchievementRewardDef reward_def in achievement_def.rewards)
        {
            if (reward_def == null || reward_def.IsEmpty())
                continue;
            if (!PendingCharacterRewardContentRules.IsSupportedEntryType(reward_def.reward_type))
            {
                GameLog.Error(
                    $"Achievement {(string)achievement_def.achievement_id} has unsupported pending reward entry_type {(string)reward_def.reward_type}.",
                    "progression.reward.unsupported_type",
                    "progression"
                );
                return new List<PendingCharacterRewardEntry>();
            }
            var entry = new PendingCharacterRewardEntry
            {
                entry_type = reward_def.reward_type,
                target_id = reward_def.target_id,
                target_label = _resolve_reward_target_label(
                    reward_def.reward_type,
                    reward_def.target_id,
                    reward_def.target_label
                ),
                amount = reward_def.amount,
                reason_text = !string.IsNullOrEmpty(reward_def.reason_text)
                    ? reward_def.reason_text
                    : achievement_def.display_name,
            };
            if (!entry.IsEmpty())
                entries.Add(entry);
        }
        return entries;
    }

    private List<AchievementDef> _get_matching_achievement_defs(
        StringName event_type,
        StringName subject_id
    )
    {
        var matches = new List<AchievementDef>();
        foreach (StringName achievement_id in SortedContentKeys(_achievement_def_index))
        {
            var achievement_def = GetAchievementDef(achievement_id);
            if (achievement_def != null && achievement_def.MatchesEvent(event_type, subject_id))
                matches.Add(achievement_def);
        }
        return matches;
    }

    private static Dictionary<StringName, AchievementDef> IndexAchievementDefs(
        GDictionary source
    )
    {
        var result = new Dictionary<StringName, AchievementDef>();
        if (source == null)
            return result;
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
                continue;
            AchievementDef entry = AchievementDef.FromDictionary(rawValue.AsGodotDictionary());
            if (entry == null || entry.achievement_id == "")
                continue;
            result[rawKey.AsStringName()] = entry;
        }
        return result;
    }

    private static List<PendingCharacterRewardEntry> _sort_pending_reward_entries(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        var list = new List<PendingCharacterRewardEntry>();
        foreach (var entry in entries)
            if (entry != null)
                list.Add(entry);
        list.Sort(
            (a, b) =>
            {
                var order_a = RewardEntryOrder.TryGetValue(a.entry_type, out var oa) ? oa : 99;
                var order_b = RewardEntryOrder.TryGetValue(b.entry_type, out var ob) ? ob : 99;
                if (order_a != order_b)
                    return order_a.CompareTo(order_b);
                var label_a = !string.IsNullOrEmpty(a.target_label)
                    ? a.target_label
                    : (string)a.target_id;
                var label_b = !string.IsNullOrEmpty(b.target_label)
                    ? b.target_label
                    : (string)b.target_id;
                return string.CompareOrdinal(label_a, label_b);
            }
        );
        var result = new List<PendingCharacterRewardEntry>();
        foreach (var entry in list)
            result.Add(entry);
        return result;
    }

    private static void _fill_delta_from_progression(
        CharacterProgressionDelta delta,
        UnitProgress progression,
        Dictionary<StringName, int> before_skill_levels,
        HashSet<StringName> before_granted_skill_ids,
        Dictionary<StringName, int> before_profession_ranks
    )
    {
        delta.character_level_after = progression.character_level;
        delta.SetPendingProfessionChoices(progression.PendingProfessionChoicesTyped);
        delta.needs_promotion_modal = delta.PendingProfessionChoicesTyped.Count > 0;
        foreach (var skill_id in progression.GetSortedSkillIdsTyped())
        {
            var skill_progress = progression.GetSkillProgress(skill_id);
            if (skill_progress == null)
                continue;
            var before_level = before_skill_levels.TryGetValue(skill_id, out var captured_level)
                ? captured_level
                : -1;
            if (before_level >= 0 && skill_progress.skill_level > before_level)
                delta.AddLeveledSkillId(skill_id);
            if (
                skill_progress.profession_granted_by != ""
                && !before_granted_skill_ids.Contains(skill_id)
            )
                delta.AddGrantedSkillId(skill_id);
        }
        foreach (var profession_id in progression.GetSortedProfessionIdsTyped())
        {
            var profession_progress = progression.GetProfessionProgress(profession_id);
            if (profession_progress == null)
                continue;
            var before_rank = before_profession_ranks.TryGetValue(
                profession_id,
                out var captured_rank
            )
                ? captured_rank
                : 0;
            if (profession_progress.rank != before_rank)
                delta.AddChangedProfessionId(profession_id);
        }
    }

    private static void _merge_delta(
        CharacterProgressionDelta target,
        CharacterProgressionDelta source
    )
    {
        if (target == null || source == null)
            return;
        target.AppendUnlockedAchievementIds(source.UnlockedAchievementIdsTyped);
        foreach (StringName skillId in source.LeveledSkillIdsTyped)
            target.AddLeveledSkillId(skillId);
        foreach (StringName skillId in source.GrantedSkillIdsTyped)
            target.AddGrantedSkillId(skillId);
        foreach (StringName professionId in source.ChangedProfessionIdsTyped)
            target.AddChangedProfessionId(professionId);
        target.AppendMasteryChanges(source.MasteryChangesTyped);
        target.AppendKnowledgeChanges(source.KnowledgeChangesTyped);
        target.AppendAttributeChanges(source.AttributeChangesTyped);
        if (source.PendingProfessionChoicesTyped.Count > 0)
            target.SetPendingProfessionChoices(source.PendingProfessionChoicesTyped);
        target.needs_promotion_modal = target.needs_promotion_modal || source.needs_promotion_modal;
        target.character_level_after = Mathf.Max(
            target.character_level_after,
            source.character_level_after
        );
    }

    private static CharacterProgressionDelta _new_delta(StringName member_id) =>
        new() { member_id = member_id };

    private void _remove_pending_character_reward_if_present(StringName reward_id)
    {
        if (_party_state == null || reward_id == "")
            return;
        _party_state.RemovePendingCharacterReward(reward_id);
    }

    private static void _log_unsupported_pending_character_reward_entry(
        PendingCharacterReward reward,
        PendingCharacterRewardEntry entry
    )
    {
        if (reward == null || entry == null)
            return;
        GameLog.Error(
            $"Unsupported pending character reward entry_type {(string)entry.entry_type}; reward_id={(string)reward.reward_id} member_id={(string)reward.member_id} source_type={(string)reward.source_type} source_id={(string)reward.source_id} target_id={(string)entry.target_id} amount={entry.amount}.",
            "progression.reward.unsupported_entry",
            "progression"
        );
    }

    private static StringName _build_reward_id(StringName member_id, StringName source_id) =>
        ProgressionDataUtils.to_string_name(
            $"{(string)member_id}_{(string)source_id}_{Time.GetTicksUsec()}"
        );

    private static void _append_unique_string_names(
        GStringNameArray target,
        GStringNameArray values
    )
    {
        foreach (var value in values)
            _append_unique_string_name(target, value);
    }

    private static void _append_unique_string_names(GStringNameArray target, GArray values)
    {
        foreach (var value in values)
            _append_unique_string_name(target, ProgressionDataUtils.to_string_name(value));
    }

    private static void _append_unique_string_name(GStringNameArray target, StringName value)
    {
        if (value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    private string _resolve_skill_label(StringName skill_id)
    {
        var skill_def = GetSkillDef(skill_id);
        return skill_def != null && !string.IsNullOrEmpty(skill_def.display_name)
            ? skill_def.display_name
            : (string)skill_id;
    }

    private string _resolve_reward_target_label(
        StringName entry_type,
        StringName target_id,
        string fallback_label
    )
    {
        if (!string.IsNullOrEmpty(fallback_label))
            return fallback_label;
        PendingCharacterRewardEntryKind entryKind =
            PendingCharacterRewardContentRules.ToEntryKind(entry_type);
        if (
            entryKind
            is PendingCharacterRewardEntryKind.SkillUnlock
                or PendingCharacterRewardEntryKind.SkillMastery
        )
            return _resolve_skill_label(target_id);
        if (
            entryKind
            is PendingCharacterRewardEntryKind.AttributeDelta
                or PendingCharacterRewardEntryKind.AttributeProgress
        )
            return _resolve_attribute_label(target_id);
        return (string)target_id;
    }

    private static string _resolve_attribute_label(StringName attribute_id)
    {
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength))
            return "力量";
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            return "敏捷";
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution))
            return "体质";
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception))
            return "感知";
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence))
            return "智力";
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower))
            return "意志";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.HpMax))
            return "生命上限";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus))
            return "人物生命加成%";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.MpMax))
            return "法力上限";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.StaminaMax))
            return "体力上限";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.ActionPoints))
            return "行动点";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.AttackBonus))
            return "攻击加值";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.ArmorClass))
            return "AC";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus))
            return "护甲 AC";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.ShieldAcBonus))
            return "盾牌 AC";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.DodgeBonus))
            return "闪避加值";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.DeflectionBonus))
            return "偏斜加值";
        if (attribute_id == AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus))
            return "护甲敏捷上限";
        return (string)attribute_id;
    }

    private static StringName _resolve_mastery_source_type(StringName source_type)
    {
        var normalized = ProgressionDataUtils.to_string_name(source_type);
        if (normalized == "battle" || normalized == "battle_rating")
            return "battle";
        if (
            normalized == "training"
            || normalized == "npc_teach"
            || normalized == "npc"
            || normalized == "teaching"
        )
            return "training";
        if (
            normalized == "heavy_hit_taken"
            || normalized == "max_damage_die_taken"
            || normalized == "elite_or_boss_damage_taken"
        )
            return normalized;
        if (normalized == "")
            return "training";
        return "training";
    }

    private static string _build_default_source_label(StringName source_type)
    {
        if (source_type == RewardTypeAchievement)
            return "成就奖励";
        if (source_type == "battle_rating")
            return "战斗结算";
        if (source_type == "battle")
            return "战斗奖励";
        if (source_type == "npc_teach" || source_type == "npc" || source_type == "teaching")
            return "NPC 传授";
        if (source_type == "training")
            return "训练收获";
        return "角色奖励";
    }

    private static int CompareAchievementProgressEntry(
        AchievementProgressSummaryEntry a,
        AchievementProgressSummaryEntry b
    )
    {
        if (Mathf.IsEqualApprox(a.ProgressRatio, b.ProgressRatio))
        {
            if (a.CurrentValue == b.CurrentValue)
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            return b.CurrentValue.CompareTo(a.CurrentValue);
        }
        return b.ProgressRatio.CompareTo(a.ProgressRatio);
    }

    private static GDictionary ProjectAchievementProgressSummaryEntry(
        AchievementProgressSummaryEntry entry
    )
    {
        if (entry == null)
            return new GDictionary();

        return new GDictionary
        {
            ["achievement_id"] = entry.AchievementId,
            ["display_name"] = entry.DisplayName,
            ["description"] = entry.Description,
            ["current_value"] = entry.CurrentValue,
            ["threshold"] = entry.Threshold,
            ["progress_ratio"] = entry.ProgressRatio,
        };
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        foreach (StringName value in values)
        {
            if (value == target)
                return true;
        }
        return false;
    }

    private static int GetIntParam(GDictionary dict, string key, int fallback = 0)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return (int)dict[key];
    }

    private static int GetIntParam(GDictionary dict, StringName key, int fallback = 0)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return (int)dict[key];
    }

    private static float GetFloatParam(GDictionary dict, string key, float fallback = 0.0f)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return (float)dict[key];
    }

    private static bool DictionariesEqual(GDictionary left, GDictionary right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (var key in left.Keys)
        {
            if (!right.ContainsKey(key) || !left[key].Equals(right[key]))
                return false;
        }
        return true;
    }

    private static GArray ToUntyped(IEnumerable<StageAdvancementModifier> values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntyped(Godot.Collections.Array<GDictionary> values)
    {
        var result = new GArray();
        foreach (var value in values)
            result.Add(value);
        return result;
    }
}
