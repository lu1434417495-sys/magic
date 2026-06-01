using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class CharacterManagementModule : RefCounted, IBattleRuntimeCharacterGateway
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

        public GDictionary ToDictionary() =>
            new()
            {
                ["achievement_id"] = AchievementId,
                ["display_name"] = DisplayName,
                ["description"] = Description,
                ["current_value"] = CurrentValue,
                ["threshold"] = Threshold,
                ["progress_ratio"] = ProgressRatio,
            };
    }

    public sealed class DailyPracticeGrowthResult
    {
        public bool Applied { get; }
        public int DaysElapsed { get; }
        public GStringNameArray ChangedMemberIds { get; }

        public DailyPracticeGrowthResult(
            bool applied,
            int daysElapsed,
            GStringNameArray changedMemberIds
        )
        {
            Applied = applied;
            DaysElapsed = Mathf.Max(daysElapsed, 0);
            ChangedMemberIds = changedMemberIds?.Duplicate() ?? new GStringNameArray();
        }

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["applied"] = Applied,
                ["days_elapsed"] = DaysElapsed,
                ["changed_member_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                    ChangedMemberIds
                ),
            };
        }
    }

    private PartyState _party_state = new();
    private GDictionary _skill_defs = new();
    private GDictionary _profession_defs = new();
    private GDictionary _achievement_defs = new();
    private GDictionary _item_defs = new();
    private GDictionary _quest_defs = new();
    private GDictionary _progression_content_bundle = new();
    private Dictionary<StringName, SkillDef> _skill_def_index = new();
    private Dictionary<StringName, AchievementDef> _achievement_def_index = new();
    private Dictionary<StringName, ItemDef> _item_def_index = new();
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
    private readonly PartyEquipmentService _party_equipment_service = new();
    private readonly QuestProgressService _quest_progress_service = new();
    private Func<StringName> _equipment_instance_id_allocator;

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
            new GDictionary()
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
            new GDictionary()
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
            new GDictionary()
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
            new GDictionary()
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
            new GDictionary()
        );

    public void setup(
        PartyState party_state,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GDictionary achievement_defs,
        GDictionary item_defs,
        GDictionary quest_defs,
        Func<StringName> equipment_instance_id_allocator,
        GDictionary progression_content_bundle
    )
    {
        _party_state = party_state ?? new PartyState();
        _skill_defs = skill_defs ?? new GDictionary();
        _profession_defs = profession_defs ?? new GDictionary();
        _achievement_defs = achievement_defs ?? new GDictionary();
        _item_defs = item_defs ?? new GDictionary();
        _quest_defs = quest_defs ?? new GDictionary();
        _progression_content_bundle = progression_content_bundle ?? new GDictionary();
        _skill_def_index = IndexContentDefs<SkillDef>(_skill_defs, skillDef => skillDef.skill_id);
        _achievement_def_index = IndexContentDefs<AchievementDef>(
            _achievement_defs,
            achievementDef => achievementDef.achievement_id
        );
        _item_def_index = IndexContentDefs<ItemDef>(_item_defs, itemDef => itemDef.item_id);
        _race_def_index = IndexContentDefs<RaceDef>(
            _get_content_bucket("race_defs", "race"),
            raceDef => raceDef.race_id
        );
        _subrace_def_index = IndexContentDefs<SubraceDef>(
            _get_content_bucket("subrace_defs", "subrace"),
            subraceDef => subraceDef.subrace_id
        );
        _age_profile_def_index = IndexContentDefs<AgeProfileDef>(
            _get_content_bucket("age_profile_defs", "age_profile"),
            ageProfileDef => ageProfileDef.profile_id
        );
        _bloodline_def_index = IndexContentDefs<BloodlineDef>(
            _get_content_bucket("bloodline_defs", "bloodline"),
            bloodlineDef => bloodlineDef.bloodline_id
        );
        _bloodline_stage_def_index = IndexContentDefs<BloodlineStageDef>(
            _get_content_bucket("bloodline_stage_defs", "bloodline_stage"),
            bloodlineStageDef => bloodlineStageDef.stage_id
        );
        _ascension_def_index = IndexContentDefs<AscensionDef>(
            _get_content_bucket("ascension_defs", "ascension"),
            ascensionDef => ascensionDef.ascension_id
        );
        _ascension_stage_def_index = IndexContentDefs<AscensionStageDef>(
            _get_content_bucket("ascension_stage_defs", "ascension_stage"),
            ascensionStageDef => ascensionStageDef.stage_id
        );
        _stage_advancement_modifier_index = IndexContentDefs<StageAdvancementModifier>(
            _get_content_bucket("stage_advancement_defs", "stage_advancement"),
            modifier => modifier.modifier_id
        );
        _equipment_instance_id_allocator = equipment_instance_id_allocator;
        _party_warehouse_service.setup(_party_state, _item_defs, _equipment_instance_id_allocator);
        _party_equipment_service.setup(
            _party_state,
            _item_defs,
            _party_warehouse_service,
            _equipment_instance_id_allocator
        );
        _quest_progress_service.setup(_party_state, _quest_defs);
        _setup_identity_apply_services();
    }

    public PartyState get_party_state() => _party_state;

    public GDictionary get_item_defs() => _item_defs;

    public bool has_item_def_catalog() => _item_def_index.Count > 0;

    public ItemDef get_item_def(StringName item_id) => GetItemDef(item_id);

    public void set_party_state(PartyState party_state)
    {
        _party_state = party_state ?? new PartyState();
        _party_warehouse_service.setup(_party_state, _item_defs, _equipment_instance_id_allocator);
        _party_equipment_service.setup(
            _party_state,
            _item_defs,
            _party_warehouse_service,
            _equipment_instance_id_allocator
        );
        _quest_progress_service.setup(_party_state, _quest_defs);
        _setup_identity_apply_services();
    }

    public RaceDef get_race_def_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null ? null : GetRaceDef(member_state.race_id);
    }

    public SubraceDef get_subrace_def_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null ? null : GetSubraceDef(member_state.subrace_id);
    }

    public BloodlineDef get_bloodline_def_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null || member_state.bloodline_id == ""
            ? null
            : GetBloodlineDef(member_state.bloodline_id);
    }

    public BloodlineStageDef get_bloodline_stage_def_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null || member_state.bloodline_stage_id == ""
            ? null
            : GetBloodlineStageDef(member_state.bloodline_stage_id);
    }

    public AscensionDef get_ascension_def_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null || member_state.ascension_id == ""
            ? null
            : GetAscensionDef(member_state.ascension_id);
    }

    public AscensionStageDef get_ascension_stage_def_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null || member_state.ascension_stage_id == ""
            ? null
            : GetAscensionStageDef(member_state.ascension_stage_id);
    }

    public AgeStageRule get_age_stage_rule_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
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

    public AttributeSourceContext build_attribute_source_context(StringName member_id) =>
        build_attribute_source_context(member_id, null);

    public AttributeSourceContext build_attribute_source_context(
        StringName member_id,
        EquipmentState equipment_state_override
    )
    {
        var member_state = get_member_state(member_id);
        var context = new AttributeSourceContext();
        if (member_state == null)
            return context;

        context.unit_progress = member_state.progression;
        context.skill_defs = _skill_defs;
        context.profession_defs = _profession_defs;
        context.race_def = get_race_def_for_member(member_id);
        context.subrace_def = get_subrace_def_for_member(member_id);
        context.age_stage_rule = get_age_stage_rule_for_member(member_id);
        context.age_stage_source_type = member_state.effective_age_stage_source_type;
        context.age_stage_source_id = member_state.effective_age_stage_source_id;
        context.bloodline_def = get_bloodline_def_for_member(member_id);
        context.bloodline_stage_def = get_bloodline_stage_def_for_member(member_id);
        context.ascension_def = get_ascension_def_for_member(member_id);
        context.ascension_stage_def = get_ascension_stage_def_for_member(member_id);
        context.versatility_pick = member_state.versatility_pick;
        var equipment_state = equipment_state_override ?? member_state.equipment_state;
        context.equipment_state = ToUntyped(
            _party_equipment_service.build_attribute_modifiers(equipment_state)
        );
        context.stage_advancement_modifiers = ToUntyped(
            _collect_active_stage_advancement_modifiers(member_state)
        );
        return context;
    }

    public PassiveSourceContext build_passive_source_context(StringName member_id) =>
        build_passive_source_context(member_id, null);

    public PassiveSourceContext build_passive_source_context(
        StringName member_id,
        UnitProgress progression_state
    )
    {
        var member_state = get_member_state(member_id);
        var context = new PassiveSourceContext
        {
            member_state = member_state,
            unit_progress = progression_state ?? member_state?.progression,
        };
        if (context.unit_progress != null)
            context.skill_progress_by_id = context.unit_progress.skills;
        context.race_def = get_race_def_for_member(member_id);
        context.subrace_def = get_subrace_def_for_member(member_id);
        context.trait_defs = _get_content_bucket("race_trait_defs", "race_trait");
        context.bloodline_def = get_bloodline_def_for_member(member_id);
        context.bloodline_stage_def = get_bloodline_stage_def_for_member(member_id);
        context.ascension_def = get_ascension_def_for_member(member_id);
        context.ascension_stage_def = get_ascension_stage_def_for_member(member_id);
        context.stage_advancement_modifiers = ToUntyped(
            _collect_active_stage_advancement_modifiers(member_state)
        );
        return context;
    }

    public GDictionary get_identity_summary_for_member(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        if (member_state == null)
            return new GDictionary();
        var race_def = get_race_def_for_member(member_id);
        var subrace_def = get_subrace_def_for_member(member_id);
        var bloodline_def = get_bloodline_def_for_member(member_id);
        var bloodline_stage_def = get_bloodline_stage_def_for_member(member_id);
        var ascension_def = get_ascension_def_for_member(member_id);
        var ascension_stage_def = get_ascension_stage_def_for_member(member_id);
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
                get_age_stage_rule_for_member(member_id),
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

    public bool apply_bloodline(
        StringName member_id,
        StringName bloodline_id,
        StringName bloodline_stage_id
    )
    {
        var member_state = get_member_state(member_id);
        if (
            !_bloodline_apply_service.apply_bloodline(
                member_state,
                bloodline_id,
                bloodline_stage_id
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool revoke_bloodline(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        if (!_bloodline_apply_service.revoke_bloodline(member_state))
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool apply_ascension(
        StringName member_id,
        StringName ascension_id,
        StringName ascension_stage_id,
        int current_world_step
    )
    {
        var member_state = get_member_state(member_id);
        if (
            !_ascension_apply_service.apply_ascension(
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

    public bool revoke_ascension(StringName member_id) => revoke_ascension(member_id, true);

    public bool revoke_ascension(StringName member_id, bool restore_original_race)
    {
        var member_state = get_member_state(member_id);
        if (!_ascension_apply_service.revoke_ascension(member_state, restore_original_race))
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool add_stage_advancement_modifier(StringName member_id, StringName modifier_id)
    {
        var member_state = get_member_state(member_id);
        if (
            !_stage_advancement_apply_service.add_stage_advancement_modifier(
                member_state,
                modifier_id
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool remove_stage_advancement_modifier(StringName member_id, StringName modifier_id)
    {
        var member_state = get_member_state(member_id);
        if (
            !_stage_advancement_apply_service.remove_stage_advancement_modifier(
                member_state,
                modifier_id
            )
        )
            return false;
        _refresh_member_identity_after_apply(member_state);
        return true;
    }

    public bool grant_racial_skill(
        StringName member_id,
        RacialGrantedSkill grant,
        StringName source_type,
        StringName source_id
    )
    {
        var member_state = get_member_state(member_id);
        if (member_state == null || member_state.progression == null)
            return false;
        return _build_progression_service(member_state.progression)
            .grant_racial_skill(grant, source_type, source_id);
    }

    public PartyMemberState get_member_state(StringName member_id) =>
        _party_state?.get_member_state(member_id);

    public void set_member_state(PartyMemberState member_state)
    {
        _party_state ??= new PartyState();
        if (member_state != null)
            _party_state.set_member_state(member_state);
    }

    public Godot.Collections.Array<PendingCharacterReward> get_pending_character_rewards() =>
        _party_state == null
            ? new Godot.Collections.Array<PendingCharacterReward>()
            : _party_state.pending_character_rewards.Duplicate();

    public Godot.Collections.Array<QuestState> get_active_quest_states() =>
        _quest_progress_service?.get_active_quests() ?? new Godot.Collections.Array<QuestState>();

    public Godot.Collections.Array<QuestState> get_claimable_quest_states() =>
        _quest_progress_service?.get_claimable_quests()
        ?? new Godot.Collections.Array<QuestState>();

    public GStringNameArray get_claimable_quest_ids() =>
        _quest_progress_service?.get_claimable_quest_ids() ?? new GStringNameArray();

    public GStringNameArray get_completed_quest_ids() =>
        _quest_progress_service?.get_completed_quest_ids() ?? new GStringNameArray();

    public bool accept_quest(StringName quest_id) => accept_quest(quest_id, -1, false);

    public bool accept_quest(StringName quest_id, int world_step) =>
        accept_quest(quest_id, world_step, false);

    public bool accept_quest(StringName quest_id, int world_step, bool allow_reaccept)
    {
        if (_quest_progress_service == null)
            return false;
        var accepted = _quest_progress_service.accept_quest(quest_id, world_step, allow_reaccept);
        _party_state = _quest_progress_service.get_party_state();
        return accepted;
    }

    public bool complete_quest(StringName quest_id) => complete_quest(quest_id, -1);

    public bool complete_quest(StringName quest_id, int world_step)
    {
        if (_quest_progress_service == null)
            return false;
        var completed = _quest_progress_service.complete_quest(quest_id, world_step);
        _party_state = _quest_progress_service.get_party_state();
        return completed;
    }

    public GDictionary submit_item_objective(StringName quest_id) =>
        submit_item_objective(quest_id, "", -1);

    public GDictionary submit_item_objective(StringName quest_id, StringName objective_id) =>
        submit_item_objective(quest_id, objective_id, -1);

    public GDictionary submit_item_objective(
        StringName quest_id,
        StringName objective_id,
        int world_step
    ) => SubmitItemObjectiveTyped(quest_id, objective_id, world_step).ToDictionary();

    internal QuestSubmitItemResultData SubmitItemObjectiveTyped(
        StringName quest_id,
        StringName objective_id,
        int world_step
    )
    {
        var submission_preview = _preview_quest_submit_item_objective(quest_id, objective_id);
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
                ? _party_state.warehouse_state.duplicate_state()
                : null;
        var withdraw_item_ids = _build_repeated_item_ids(item_id, required_quantity);
        var warehouse_commit = _party_warehouse_service.CommitBatchSwapTyped(
            withdraw_item_ids,
            new GStringNameArray()
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

        var event_payload = new GDictionary
        {
            ["event_type"] = "progress",
            ["quest_id"] = (string)quest_id,
            ["objective_id"] = (string)resolved_objective_id,
            ["objective_type"] = (string)QuestDef.OBJECTIVE_SUBMIT_ITEM(),
            ["target_id"] = (string)item_id,
            ["target_value"] = target_value,
            ["progress_delta"] = required_quantity,
            ["world_step"] = world_step,
            ["item_id"] = (string)item_id,
            ["quantity"] = required_quantity,
            ["context"] = new GDictionary
            {
                ["item_id"] = (string)item_id,
                ["submitted_quantity"] = required_quantity,
            },
        };
        var summary = QuestProgressApplySummaryData.FromDictionary(
            apply_quest_progress_events(new GArray { event_payload }, world_step)
        );
        if (!summary.ContainsProgressedQuest(quest_id))
        {
            if (_party_state != null)
                _party_state.warehouse_state = warehouse_state_before;
            _party_warehouse_service.setup(
                _party_state,
                _item_defs,
                _equipment_instance_id_allocator
            );
            _quest_progress_service.setup(_party_state, _quest_defs);
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

    public GDictionary claim_quest_reward(StringName quest_id) => claim_quest_reward(quest_id, -1);

    public GDictionary claim_quest_reward(StringName quest_id, int world_step) =>
        ClaimQuestRewardTyped(quest_id, world_step).ToDictionary();

    internal QuestClaimResultData ClaimQuestRewardTyped(StringName quest_id, int world_step)
    {
        if (_party_state == null || quest_id == "")
            return QuestClaimResultData.Failed("invalid_quest_id");
        if (!_party_state.has_claimable_quest(quest_id))
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
        var warehouse_state_before = _party_state.warehouse_state?.duplicate_state();
        if (reward_item_ids.Count > 0)
        {
            var warehouse_commit = _party_warehouse_service.CommitBatchSwapTyped(
                new GStringNameArray(),
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
        var gold_before_claim = _party_state.get_gold();
        if (gold_delta > 0)
            _party_state.add_gold(gold_delta);
        if (!_party_state.mark_quest_reward_claimed(quest_id, world_step))
        {
            if (gold_delta > 0)
                _party_state.set_gold(gold_before_claim);
            _party_state.warehouse_state = warehouse_state_before;
            return QuestClaimResultData.Failed("quest_claim_failed");
        }

        var pending_character_rewards = reward_preview.ClonePendingCharacterRewards();
        if (pending_character_rewards.Count > 0)
            enqueue_pending_character_rewards(pending_character_rewards);
        return QuestClaimResultData.Success(
            gold_delta,
            reward_preview.CloneItemRewards(),
            _pending_character_reward_options_to_dicts(pending_character_rewards)
        );
    }

    public GDictionary apply_quest_progress_events(GArray event_options) =>
        apply_quest_progress_events(event_options, -1);

    public GDictionary apply_quest_progress_events(GArray event_options, int world_step)
    {
        if (_quest_progress_service == null)
            return new GDictionary
            {
                ["accepted_quest_ids"] = new GStringNameArray(),
                ["progressed_quest_ids"] = new GStringNameArray(),
                ["claimable_quest_ids"] = new GStringNameArray(),
                ["completed_quest_ids"] = new GStringNameArray(),
            };
        var summary = _quest_progress_service.apply_quest_progress_events(
            event_options,
            world_step
        );
        _party_state = _quest_progress_service.get_party_state();
        return summary;
    }

    public void enqueue_pending_character_rewards(GArray reward_options)
    {
        _party_state ??= new PartyState();
        foreach (var reward_option in reward_options)
        {
            var reward = _normalize_pending_character_reward_option(reward_option);
            if (reward == null || reward.is_empty())
                continue;
            _party_state.enqueue_pending_character_reward(reward);
        }
    }

    public AttributeSnapshot get_member_attribute_snapshot(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null || member_state.progression == null
            ? new AttributeSnapshot()
            : _build_attribute_service(member_state).get_snapshot();
    }

    public AttributeSnapshot get_member_attribute_snapshot_for_equipment_view(
        StringName member_id,
        EquipmentState equipment_view
    )
    {
        var member_state = get_member_state(member_id);
        return member_state == null || member_state.progression == null
            ? new AttributeSnapshot()
            : _build_attribute_service(member_state, equipment_view ?? new EquipmentState())
                .get_snapshot();
    }

    public GDictionary get_member_weapon_projection(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        return member_state == null
            ? new GDictionary()
            : get_member_weapon_projection_for_equipment_view(
                member_id,
                member_state.equipment_state
            );
    }

    public GDictionary get_member_weapon_projection_for_equipment_view(
        StringName member_id,
        EquipmentState equipment_view
    )
    {
        var member_state = get_member_state(member_id);
        if (member_state == null)
            return new GDictionary();
        var resolved_equipment_view = equipment_view ?? new EquipmentState();
        if (resolved_equipment_view == null)
            return _build_unarmed_weapon_projection();
        var weapon_item_id = ProgressionDataUtils.to_string_name(
            resolved_equipment_view.get_equipped_item_id("main_hand")
        );
        if (weapon_item_id == "")
            return _build_unarmed_weapon_projection();
        var item_def = GetItemDef(weapon_item_id);
        return item_def == null || !item_def.is_weapon()
            ? new GDictionary()
            : _build_weapon_projection_from_item_def(item_def, resolved_equipment_view);
    }

    public StringName get_member_weapon_physical_damage_tag(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        if (member_state == null)
            return "";
        var equipment_state = member_state.equipment_state ?? new EquipmentState();
        var weapon_item_id = ProgressionDataUtils.to_string_name(
            equipment_state.get_equipped_item_id("main_hand")
        );
        if (weapon_item_id == "")
            return ItemDef.DAMAGE_TAG_PHYSICAL_BLUNT();
        var item_def = GetItemDef(weapon_item_id);
        if (item_def == null || !item_def.is_weapon())
            return "";
        return item_def.get_weapon_physical_damage_tag();
    }

    private GDictionary _build_weapon_projection_from_item_def(
        ItemDef item_def,
        EquipmentState equipment_state
    )
    {
        if (item_def == null || !item_def.is_weapon())
            return new GDictionary();
        var profile = item_def.weapon_profile as WeaponProfileDef;
        if (profile == null)
            return new GDictionary();
        var one_handed_dice = _weapon_dice_to_dict(profile.one_handed_dice);
        var two_handed_dice = _weapon_dice_to_dict(profile.two_handed_dice);
        var properties = _weapon_profile_properties(profile);
        var is_versatile = properties.Contains("versatile");
        var uses_two_hands = _resolve_weapon_uses_two_hands(
            item_def,
            equipment_state,
            one_handed_dice,
            two_handed_dice,
            is_versatile
        );
        return new GDictionary
        {
            ["weapon_profile_kind"] = "equipped",
            ["weapon_item_id"] = (string)item_def.item_id,
            ["weapon_profile_type_id"] = (string)
                ProgressionDataUtils.to_string_name(profile.weapon_type_id),
            ["weapon_family"] = (string)ProgressionDataUtils.to_string_name(profile.family),
            ["weapon_current_grip"] = (string)_resolve_weapon_current_grip(
                one_handed_dice,
                two_handed_dice,
                uses_two_hands
            ),
            ["weapon_attack_range"] = Mathf.Max(profile.attack_range, 0),
            ["weapon_one_handed_dice"] = one_handed_dice,
            ["weapon_two_handed_dice"] = two_handed_dice,
            ["weapon_is_versatile"] = is_versatile,
            ["weapon_uses_two_hands"] = uses_two_hands,
            ["weapon_physical_damage_tag"] = (string)item_def.get_weapon_physical_damage_tag(),
        };
    }

    private static GDictionary _build_unarmed_weapon_projection() =>
        new()
        {
            ["weapon_profile_kind"] = "unarmed",
            ["weapon_item_id"] = "",
            ["weapon_profile_type_id"] = "unarmed",
            ["weapon_family"] = "unarmed",
            ["weapon_current_grip"] = "one_handed",
            ["weapon_attack_range"] = 1,
            ["weapon_one_handed_dice"] = new GDictionary
            {
                ["dice_count"] = 1,
                ["dice_sides"] = 4,
                ["flat_bonus"] = 0,
            },
            ["weapon_two_handed_dice"] = new GDictionary(),
            ["weapon_is_versatile"] = false,
            ["weapon_uses_two_hands"] = false,
            ["weapon_physical_damage_tag"] = "physical_blunt",
        };

    private bool _resolve_weapon_uses_two_hands(
        ItemDef item_def,
        EquipmentState equipment_state,
        GDictionary one_handed_dice,
        GDictionary two_handed_dice,
        bool is_versatile
    )
    {
        if (item_def == null)
            return false;
        var occupied_slots = item_def.get_final_occupied_slot_ids("main_hand");
        if (occupied_slots.Contains("off_hand"))
            return true;
        if (one_handed_dice.Count == 0 && two_handed_dice.Count > 0)
            return true;
        if (is_versatile && two_handed_dice.Count > 0)
            return _is_off_hand_free_for_versatile(equipment_state);
        return false;
    }

    private static StringName _resolve_weapon_current_grip(
        GDictionary one_handed_dice,
        GDictionary two_handed_dice,
        bool uses_two_hands
    )
    {
        if (uses_two_hands)
            return "two_handed";
        if (one_handed_dice.Count > 0)
            return "one_handed";
        if (two_handed_dice.Count > 0)
            return "two_handed";
        return "none";
    }

    private static bool _is_off_hand_free_for_versatile(EquipmentState equipment_state) =>
        equipment_state == null
        || ProgressionDataUtils.to_string_name(equipment_state.get_entry_slot_for_slot("off_hand"))
            == "";

    private static GStringNameArray _weapon_profile_properties(WeaponProfileDef profile) =>
        profile?.get_properties() ?? new GStringNameArray();

    private static GDictionary _weapon_dice_to_dict(WeaponDamageDiceDef dice_resource)
    {
        if (dice_resource == null)
            return new GDictionary();
        var dice_count = dice_resource.dice_count;
        var dice_sides = dice_resource.dice_sides;
        return dice_count <= 0 || dice_sides <= 0
            ? new GDictionary()
            : new GDictionary
            {
                ["dice_count"] = dice_count,
                ["dice_sides"] = dice_sides,
                ["flat_bonus"] = dice_resource.flat_bonus,
            };
    }

    public bool learn_skill(StringName member_id, StringName skill_id) =>
        _learn_skill_internal(member_id, skill_id, null, new GDictionary());

    public bool learn_skill(StringName member_id, StringName skill_id, GDictionary options) =>
        _learn_skill_internal(member_id, skill_id, null, options ?? new GDictionary());

    public bool learn_knowledge(StringName member_id, StringName knowledge_id) =>
        _learn_knowledge_internal(member_id, knowledge_id);

    public GDictionary get_practice_skill_learn_status(StringName member_id, StringName skill_id)
    {
        return GetPracticeSkillLearnStatusTyped(member_id, skill_id).ToLearnedStatusDictionary();
    }

    public PracticeSkillLearnStatus GetPracticeSkillLearnStatusTyped(
        StringName member_id,
        StringName skill_id
    )
    {
        var member_state = get_member_state(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return PracticeSkillLearnStatus.NonPractice();
        return _build_practice_growth_service().get_skill_learned_status_typed(skill_id, progression);
    }

    public GDictionary set_active_level_trigger_core_skill(
        StringName member_id,
        StringName skill_id
    )
    {
        return SetActiveLevelTriggerCoreSkillTyped(member_id, skill_id).ToDictionary();
    }

    public LevelGrowthTriggerResult SetActiveLevelTriggerCoreSkillTyped(
        StringName member_id,
        StringName skill_id
    )
    {
        var member_state = get_member_state(member_id);
        var service = new LevelGrowthEvaluationService();
        service.setup(_skill_defs);
        var result = service.set_active_trigger_core_skill_typed(member_state, skill_id);
        if (result.Ok && member_state?.progression != null)
            _build_progression_service(member_state.progression).refresh_runtime_state();
        return result;
    }

    public GDictionary clear_active_level_trigger_core_skill(StringName member_id)
    {
        return ClearActiveLevelTriggerCoreSkillTyped(member_id).ToDictionary();
    }

    public LevelGrowthTriggerResult ClearActiveLevelTriggerCoreSkillTyped(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        var service = new LevelGrowthEvaluationService();
        service.setup(_skill_defs);
        var result = service.clear_active_trigger_core_skill_typed(member_state);
        if (result.Ok && member_state?.progression != null)
            _build_progression_service(member_state.progression).refresh_runtime_state();
        return result;
    }

    public GDictionary apply_daily_practice_growth(int days_elapsed)
    {
        return ApplyDailyPracticeGrowthTyped(days_elapsed).ToDictionary();
    }

    public DailyPracticeGrowthResult ApplyDailyPracticeGrowthTyped(int days_elapsed)
    {
        if (_party_state == null || days_elapsed <= 0)
            return new DailyPracticeGrowthResult(false, days_elapsed, new GStringNameArray());
        var practice_service = _build_practice_growth_service();
        var changed_member_ids = new GStringNameArray();
        foreach (
            string member_key in ProgressionDataUtils.sorted_string_keys(_party_state.member_states)
        )
        {
            var member_id = new StringName(member_key);
            var member_state = _party_state.get_member_state(member_id);
            if (member_state?.progression is not UnitProgress)
                continue;
            var before_snapshot = _capture_practice_resource_snapshot(member_state);
            practice_service.apply_daily_growth_to_member(member_state, days_elapsed);
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
        GStringNameArray unlocked_ids = null,
        GDictionary options = null
    )
    {
        var member_state = get_member_state(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return false;

        options ??= new GDictionary();
        var practice_service = _build_practice_growth_service();
        var practice_status = practice_service.get_skill_learned_status_typed(skill_id, progression);
        var progression_service = _build_progression_service(progression);
        if (practice_status.IsPracticeSkill)
        {
            if (practice_status.NeedsReplacement)
            {
                if (!HasConfirmedPracticeReplacement(options))
                    return false;
                if (!progression_service.can_learn_skill(skill_id))
                    return false;
                if (!practice_service.apply_replacement(skill_id, progression, true))
                    return false;
                progression_service.refresh_runtime_state();
                var replacement_achievement_ids = record_achievement_event(
                    member_id,
                    "skill_learned",
                    1,
                    skill_id
                );
                if (unlocked_ids != null)
                    _append_unique_string_names(unlocked_ids, replacement_achievement_ids);
                return true;
            }
            if (!practice_status.CanLearn)
                return false;
        }

        if (!progression_service.learn_skill(skill_id))
            return false;
        if (practice_status.IsPracticeSkill)
            practice_service.inject_first_unlock_starting_values(
                member_state,
                practice_status.TrackType
            );
        var achievement_ids = record_achievement_event(member_id, "skill_learned", 1, skill_id);
        if (unlocked_ids != null)
            _append_unique_string_names(unlocked_ids, achievement_ids);
        return true;
    }

    private bool _learn_knowledge_internal(
        StringName member_id,
        StringName knowledge_id,
        GStringNameArray unlocked_ids = null
    )
    {
        var member_state = get_member_state(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return false;
        var progression_service = _build_progression_service(progression);
        if (!progression_service.learn_knowledge(knowledge_id))
            return false;
        var achievement_ids = record_achievement_event(
            member_id,
            "knowledge_learned",
            1,
            knowledge_id
        );
        if (unlocked_ids != null)
            _append_unique_string_names(unlocked_ids, achievement_ids);
        return true;
    }

    public CharacterProgressionDelta grant_battle_mastery(
        StringName member_id,
        StringName skill_id,
        int amount
    ) =>
        grant_skill_mastery_from_source(
            member_id,
            skill_id,
            amount,
            "battle",
            _build_default_source_label("battle")
        );

    public CharacterProgressionDelta grant_skill_mastery_from_source(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type
    ) => grant_skill_mastery_from_source(member_id, skill_id, amount, source_type, "", "", true);

    public CharacterProgressionDelta grant_skill_mastery_from_source(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label
    ) =>
        grant_skill_mastery_from_source(
            member_id,
            skill_id,
            amount,
            source_type,
            source_label,
            "",
            true
        );

    public CharacterProgressionDelta grant_skill_mastery_from_source(
        StringName member_id,
        StringName skill_id,
        int amount,
        StringName source_type,
        string source_label,
        string reason_text
    ) =>
        grant_skill_mastery_from_source(
            member_id,
            skill_id,
            amount,
            source_type,
            source_label,
            reason_text,
            true
        );

    public CharacterProgressionDelta grant_skill_mastery_from_source(
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

    public GStringNameArray record_achievement_event(StringName member_id, StringName event_type) =>
        record_achievement_event(member_id, event_type, 1, "", new GDictionary());

    public GStringNameArray record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount
    ) => record_achievement_event(member_id, event_type, amount, "", new GDictionary());

    public GStringNameArray record_achievement_event(
        StringName member_id,
        StringName event_type,
        int amount,
        StringName subject_id
    ) => record_achievement_event(member_id, event_type, amount, subject_id, new GDictionary());

    public GStringNameArray record_achievement_event(
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
        var member_state = get_member_state(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return unlocked_ids;

        foreach (var achievement_def in _get_matching_achievement_defs(event_type, subject_id))
        {
            if (achievement_def == null)
                continue;
            var progress_state = progression.get_achievement_progress_state(
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
            progression.set_achievement_progress_state(progress_state);
        }
        return unlocked_ids;
    }

    public bool unlock_achievement(StringName member_id, StringName achievement_id) =>
        unlock_achievement(member_id, achievement_id, new GDictionary());

    public bool unlock_achievement(
        StringName member_id,
        StringName achievement_id,
        GDictionary meta
    )
    {
        if (member_id == "" || achievement_id == "")
            return false;
        var member_state = get_member_state(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return false;
        var achievement_def = GetAchievementDef(achievement_id);
        if (achievement_def == null)
            return false;
        var progress_state = progression.get_achievement_progress_state(achievement_id);
        if (progress_state == null)
            progress_state = new AchievementProgressState { achievement_id = achievement_id };
        if (progress_state.is_unlocked)
        {
            progression.set_achievement_progress_state(progress_state);
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
        progression.set_achievement_progress_state(progress_state);
        return true;
    }

    public PendingCharacterReward build_pending_character_reward(
        StringName member_id,
        StringName reward_id,
        StringName source_type,
        StringName source_id,
        string source_label,
        GArray entry_options
    ) =>
        build_pending_character_reward(
            member_id,
            reward_id,
            source_type,
            source_id,
            source_label,
            entry_options,
            ""
        );

    public PendingCharacterReward build_pending_character_reward(
        StringName member_id,
        StringName reward_id,
        StringName source_type,
        StringName source_id,
        string source_label,
        GArray entry_options,
        string summary_text
    )
    {
        var member_state = get_member_state(member_id);
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
        return reward.is_empty() ? null : reward;
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
        if (reward != null && !reward.is_empty())
            enqueue_pending_character_rewards(new GArray { reward });
    }

    public PendingCharacterReward build_pending_skill_mastery_reward(
        StringName member_id,
        StringName source_type,
        string source_label,
        GArray entry_options
    ) =>
        build_pending_skill_mastery_reward(
            member_id,
            source_type,
            source_label,
            entry_options,
            ""
        );

    public PendingCharacterReward build_pending_skill_mastery_reward(
        StringName member_id,
        StringName source_type,
        string source_label,
        GArray entry_options,
        string summary_text
    )
    {
        var member_state = get_member_state(member_id);
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
        return reward.is_empty() ? null : reward;
    }

    public CharacterProgressionDelta apply_pending_character_reward(PendingCharacterReward reward)
    {
        var normalized_reward = _normalize_pending_character_reward_option(
            reward,
            true
        );
        var member_id = normalized_reward?.member_id ?? "";
        var delta = _new_delta(member_id);
        var member_state = get_member_state(member_id);
        if (normalized_reward == null || normalized_reward.is_empty())
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
        _append_unique_string_name(
            delta.unlocked_achievement_ids,
            normalized_reward.source_type == RewardTypeAchievement
                ? normalized_reward.source_id
                : ""
        );

        var attribute_service = _build_attribute_service(member_state);
        var attribute_growth_service = new AttributeGrowthService();
        attribute_growth_service.setup(progression);
        var mastery_source_type = _resolve_mastery_source_type(normalized_reward.source_type);
        var applied_any = false;

        foreach (var entry in _sort_pending_reward_entries(normalized_reward.entries))
        {
            if (entry == null || entry.is_empty())
                continue;
            if (entry.entry_type == "knowledge_unlock")
            {
                if (
                    _learn_knowledge_internal(
                        member_id,
                        entry.target_id,
                        delta.unlocked_achievement_ids
                    )
                )
                {
                    applied_any = true;
                    delta.knowledge_changes.Add(
                        new GDictionary
                        {
                            ["knowledge_id"] = entry.target_id,
                            ["knowledge_label"] = _resolve_reward_target_label(
                                entry.entry_type,
                                entry.target_id,
                                entry.target_label
                            ),
                            ["reason_text"] = entry.reason_text,
                        }
                    );
                }
            }
            else if (entry.entry_type == "skill_unlock")
            {
                if (
                    _learn_skill_internal(
                        member_id,
                        entry.target_id,
                        delta.unlocked_achievement_ids,
                        new GDictionary()
                    )
                )
                    applied_any = true;
            }
            else if (entry.entry_type == "skill_mastery")
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
                if (mastery_delta.mastery_changes.Count > 0)
                    applied_any = true;
                _merge_delta(delta, mastery_delta);
            }
            else if (entry.entry_type == "attribute_delta")
            {
                if (
                    attribute_service.apply_permanent_attribute_change(
                        entry.target_id,
                        entry.amount,
                        new GDictionary
                        {
                            ["source_type"] = normalized_reward.source_type,
                            ["source_id"] = normalized_reward.source_id,
                        }
                    )
                )
                {
                    applied_any = true;
                    delta.attribute_changes.Add(
                        new GDictionary
                        {
                            ["attribute_id"] = entry.target_id,
                            ["attribute_label"] = _resolve_reward_target_label(
                                entry.entry_type,
                                entry.target_id,
                                entry.target_label
                            ),
                            ["delta"] = entry.amount,
                            ["reason_text"] = entry.reason_text,
                        }
                    );
                }
            }
            else if (entry.entry_type == "attribute_progress")
            {
                var growth_result = attribute_growth_service.apply_attribute_progress_typed(
                    entry.target_id,
                    entry.amount,
                    entry.reason_text
                );
                if (growth_result.Applied)
                {
                    applied_any = true;
                    delta.attribute_changes.Add(
                        new GDictionary
                        {
                            ["attribute_id"] = entry.target_id,
                            ["attribute_label"] = _resolve_reward_target_label(
                                entry.entry_type,
                                entry.target_id,
                                entry.target_label
                            ),
                            ["progress_delta"] = growth_result.ProgressDelta,
                            ["progress_before"] = growth_result.ProgressBefore,
                            ["progress_after"] = growth_result.ProgressAfter,
                            ["delta"] = growth_result.AttributeDelta,
                            ["attribute_before"] = growth_result.AttributeBefore,
                            ["attribute_after"] = growth_result.AttributeAfter,
                            ["reason_text"] = entry.reason_text,
                        }
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
        if (!applied_any && delta.mastery_changes.Count == 0)
            delta.character_level_after = delta.character_level_before;

        _remove_pending_character_reward_if_present(normalized_reward.reward_id);
        return delta;
    }

    public GDictionary get_member_achievement_summary(StringName member_id)
    {
        var member_state = get_member_state(member_id);
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

        foreach (
            string achievement_key in ProgressionDataUtils.sorted_string_keys(_achievement_defs)
        )
        {
            var achievement_id = new StringName(achievement_key);
            var achievement_def = GetAchievementDef(achievement_id);
            if (achievement_def == null)
                continue;
            var progress_state = progression.get_achievement_progress_state(achievement_id);
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
            active_progress_entries.Add(entry.ToDictionary());
        return new GDictionary
        {
            ["unlocked_count"] = unlocked_count,
            ["in_progress_count"] = in_progress_count,
            ["recent_unlocked_name"] = recent_unlocked_name,
            ["active_progress_entries"] = active_progress_entries,
        };
    }

    public CharacterProgressionDelta promote_profession(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    )
    {
        var member_state = get_member_state(member_id);
        var delta = _new_delta(member_id);
        if (member_state == null || member_state.progression is not UnitProgress progression)
            return delta;

        var before_skill_levels = _capture_skill_levels(progression);
        var before_granted_skill_ids = _capture_granted_skill_ids(progression);
        var before_profession_ranks = _capture_profession_ranks(progression);
        var trigger_skill_id = progression.active_level_trigger_core_skill_id;
        delta.character_level_before = progression.character_level;

        var progression_service = _build_progression_service(progression);
        if (progression_service.promote_profession(profession_id, selection ?? new GDictionary()))
        {
            _apply_level_trigger_attribute_growth(member_state, trigger_skill_id, delta);
            _fill_delta_from_progression(
                delta,
                progression,
                before_skill_levels,
                before_granted_skill_ids,
                before_profession_ranks
            );
            _append_unique_string_names(
                delta.unlocked_achievement_ids,
                record_achievement_event(member_id, "profession_promoted", 1, profession_id)
            );
        }
        return delta;
    }

    public void commit_battle_resources(
        StringName member_id,
        int current_hp,
        int current_mp,
        int current_aura
    )
    {
        var member_state = get_member_state(member_id);
        if (member_state == null)
            return;
        var snapshot = get_member_attribute_snapshot(member_id);
        member_state.current_hp = Mathf.Clamp(
            current_hp,
            0,
            Mathf.Max(snapshot.get_value(AttributeService.HP_MAX_ID()), 1)
        );
        member_state.current_mp = Mathf.Clamp(
            current_mp,
            0,
            Mathf.Max(snapshot.get_value(AttributeService.MP_MAX_ID()), 0)
        );
        member_state.current_aura = Mathf.Clamp(
            current_aura,
            0,
            Mathf.Max(snapshot.get_value(AttributeService.AURA_MAX_ID()), 0)
        );
        member_state.is_dead = false;
    }

    public void commit_battle_death(StringName member_id)
    {
        var member_state = get_member_state(member_id);
        if (member_state == null)
            return;
        _salvage_member_equipment(member_state);
        member_state.current_hp = 0;
        member_state.current_mp = 0;
        member_state.current_aura = 0;
        member_state.is_dead = true;
        _party_state?.remove_member_from_rosters(member_id);
    }

    public void commit_battle_ko(StringName member_id) => commit_battle_death(member_id);

    public int flush_after_battle() => (int)Error.Ok;

    private void _salvage_member_equipment(PartyMemberState member_state)
    {
        var equipment_state = member_state?.equipment_state;
        if (equipment_state == null)
            return;
        var entry_slot_ids = ProgressionDataUtils.to_string_name_array(
            equipment_state.get_entry_slot_ids()
        );
        foreach (var entry_slot_id in entry_slot_ids)
        {
            if (
                equipment_state.pop_equipped_instance(entry_slot_id)
                is EquipmentInstanceState equipped_instance
            )
                _party_warehouse_service.deposit_equipment_instance(equipped_instance);
        }
    }

    public GStringNameArray _collect_known_active_skill_ids(UnitProgress progression_state)
    {
        var skill_ids = new GStringNameArray();
        if (progression_state == null)
            return skill_ids;
        var progression = progression_state;
        foreach (string skill_key in ProgressionDataUtils.sorted_string_keys(progression.skills))
        {
            var skill_id = new StringName(skill_key);
            var skill_progress = progression.get_skill_progress(skill_id);
            var skill_def = GetSkillDef(skill_id);
            if (
                skill_progress == null
                || skill_def == null
                || !skill_progress.is_learned
                || skill_def.skill_type != "active"
                || !skill_def.can_use_in_combat()
            )
                continue;
            skill_ids.Add(skill_id);
        }
        return skill_ids;
    }

    public GDictionary _collect_known_skill_level_map(UnitProgress progression_state)
    {
        var skill_levels = new GDictionary();
        if (progression_state == null)
            return skill_levels;
        var progression = progression_state;
        foreach (string skill_key in ProgressionDataUtils.sorted_string_keys(progression.skills))
        {
            var skill_id = new StringName(skill_key);
            var skill_progress = progression.get_skill_progress(skill_id);
            var skill_def = GetSkillDef(skill_id);
            if (
                skill_progress == null
                || skill_def == null
                || !skill_progress.is_learned
                || skill_def.skill_type != "active"
            )
                continue;
            skill_levels[skill_id] = skill_progress.skill_level;
        }
        return skill_levels;
    }

    private void _setup_identity_apply_services()
    {
        _bloodline_apply_service.setup(_progression_content_bundle);
        _ascension_apply_service.setup(_progression_content_bundle);
        _stage_advancement_apply_service.setup(_progression_content_bundle);
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
        member_state.body_size_category = category;
        member_state.body_size = BodySizeRules.get_body_size_for_category(category);
    }

    private StringName _resolve_body_size_category_for_member(PartyMemberState member_state)
    {
        if (member_state == null)
            return "";
        var ascension_stage_def = get_ascension_stage_def_for_member(member_state.member_id);
        if (
            ascension_stage_def != null
            && ascension_stage_def.body_size_category_override != ""
            && BodySizeRules.is_valid_body_size_category(
                ascension_stage_def.body_size_category_override
            )
        )
            return ascension_stage_def.body_size_category_override;
        var subrace_def = get_subrace_def_for_member(member_state.member_id);
        if (
            subrace_def != null
            && subrace_def.body_size_category_override != ""
            && BodySizeRules.is_valid_body_size_category(subrace_def.body_size_category_override)
        )
            return subrace_def.body_size_category_override;
        var race_def = get_race_def_for_member(member_state.member_id);
        if (
            race_def != null
            && BodySizeRules.is_valid_body_size_category(race_def.body_size_category)
        )
            return race_def.body_size_category;
        return "";
    }

    private void _resolve_member_effective_age_stage(PartyMemberState member_state)
    {
        if (member_state == null)
            return;
        var age_profile = GetAgeProfileDef(member_state.age_profile_id);
        var resolution = AgeStageResolver.resolve_effective_stage(
            member_state,
            age_profile,
            _collect_active_stage_advancement_modifiers(member_state),
            get_bloodline_def_for_member(member_state.member_id),
            get_bloodline_stage_def_for_member(member_state.member_id),
            get_ascension_def_for_member(member_state.member_id),
            get_ascension_stage_def_for_member(member_state.member_id)
        );
        var stage_id = resolution?.StageId ?? "";
        if (stage_id == "")
            stage_id =
                member_state.natural_age_stage_id != ""
                    ? member_state.natural_age_stage_id
                    : "adult";
        member_state.effective_age_stage_id = stage_id;
        member_state.effective_age_stage_source_type = resolution?.SourceType ?? "";
        member_state.effective_age_stage_source_id = resolution?.SourceId ?? "";
    }

    private Godot.Collections.Array<StageAdvancementModifier> _collect_active_stage_advancement_modifiers(
        PartyMemberState member_state
    )
    {
        var modifiers = new Godot.Collections.Array<StageAdvancementModifier>();
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
        RacialSkillGrantService.backfill_member(
            member_state,
            _progression_content_bundle,
            _skill_defs,
            _profession_defs,
            _build_progression_service
        );

    private bool _revoke_orphan_racial_skills_for_member(PartyMemberState member_state) =>
        RacialSkillGrantService.revoke_orphan_member(
            member_state,
            _progression_content_bundle,
            _skill_defs,
            _profession_defs,
            _build_progression_service
        );

    public ProgressionService _build_progression_service(UnitProgress progression)
    {
        var assignment_service = new ProfessionAssignmentService();
        assignment_service.setup(progression, _skill_defs, _profession_defs);
        var merge_service = new SkillMergeService();
        merge_service.setup(progression, _skill_defs, assignment_service);
        var rule_service = new ProfessionRuleService();
        rule_service.setup(progression, _skill_defs, _profession_defs);
        var progression_service = new ProgressionService();
        progression_service.setup(
            progression,
            _skill_defs,
            _profession_defs,
            rule_service,
            assignment_service,
            merge_service
        );
        return progression_service;
    }

    private PracticeGrowthService _build_practice_growth_service()
    {
        var service = new PracticeGrowthService();
        service.setup(_skill_defs, _profession_defs);
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
            ["mp_max"] = base_attrs?.get_attribute_value(AttributeService.MP_MAX_ID()) ?? 0,
            ["aura_max"] = base_attrs?.get_attribute_value(AttributeService.AURA_MAX_ID()) ?? 0,
        };
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
        if (skill_def == null || skill_def.attribute_growth_progress.Count == 0)
            return;
        var skill_progress = progression.get_skill_progress(trigger_skill_id);
        if (skill_progress == null || skill_progress.core_max_growth_claimed)
            return;
        var growth_entries = _collect_attribute_growth_entries(skill_def);
        if (growth_entries.Count == 0)
            return;
        var attribute_growth_service = new AttributeGrowthService();
        attribute_growth_service.setup(progression);
        var did_apply_growth = false;
        foreach (var entry in growth_entries)
        {
            var growth_result = attribute_growth_service.apply_attribute_progress_typed(
                entry.AttributeId,
                entry.Amount,
                $"{_resolve_skill_label(trigger_skill_id)} 锁定成长"
            );
            if (!growth_result.Applied)
                continue;
            did_apply_growth = true;
            delta?.attribute_changes.Add(
                new GDictionary
                {
                    ["attribute_id"] = entry.AttributeId,
                    ["attribute_label"] = _resolve_attribute_label(entry.AttributeId),
                    ["progress_delta"] = growth_result.ProgressDelta,
                    ["progress_before"] = growth_result.ProgressBefore,
                    ["progress_after"] = growth_result.ProgressAfter,
                    ["delta"] = growth_result.AttributeDelta,
                    ["attribute_before"] = growth_result.AttributeBefore,
                    ["attribute_after"] = growth_result.AttributeAfter,
                    ["reason_text"] = growth_result.ReasonText,
                }
            );
        }
        if (!did_apply_growth)
            return;
        skill_progress.core_max_growth_claimed = true;
        progression.set_skill_progress(skill_progress);
    }

    private List<AttributeGrowthEntryData> _collect_attribute_growth_entries(
        SkillDef skill_def
    )
    {
        var entries = new List<AttributeGrowthEntryData>();
        if (skill_def == null)
            return entries;
        var attribute_entries = new List<(string key, int amount)>();
        foreach (Variant rawKey in skill_def.attribute_growth_progress.Keys)
        {
            if (
                rawKey.VariantType != Variant.Type.String
                && rawKey.VariantType != Variant.Type.StringName
            )
                continue;
            Variant rawAmount = skill_def.attribute_growth_progress[rawKey];
            if (rawAmount.VariantType != Variant.Type.Int)
                continue;
            var cleanKey = rawKey.AsString().StripEdges();
            if (cleanKey.Length > 0)
                attribute_entries.Add((cleanKey, rawAmount.AsInt32()));
        }
        attribute_entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        foreach (var (attributeKey, amount) in attribute_entries)
        {
            var attribute_id = ProgressionDataUtils.to_string_name(attributeKey);
            if (amount <= 0 || !AttributeGrowthService.is_valid_attribute_id(attribute_id))
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

    private ItemDef GetItemDef(StringName itemId) =>
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
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Object || rawValue.AsGodotObject() is not T entry)
                continue;
            var entryId = idSelector(entry);
            if (entryId == "")
                entryId = ProgressionDataUtils.to_string_name(rawKey);
            if (entryId != "")
                result[entryId] = entry;
        }
        return result;
    }

    private GDictionary _get_content_bucket(string primary_bucket, string alias_bucket)
    {
        var primary = GetDictionaryValue(_progression_content_bundle, primary_bucket);
        if (primary.Count > 0) return primary;
        return GetDictionaryValue(_progression_content_bundle, alias_bucket);
    }

    private static GDictionary GetDictionaryValue(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
            return new GDictionary();

        Variant stringKey = key;
        if (TryGetDictionaryValue(source, stringKey, out var stringValue))
            return stringValue;

        Variant stringNameKey = new StringName(key);
        return TryGetDictionaryValue(source, stringNameKey, out var stringNameValue)
            ? stringNameValue
            : new GDictionary();
    }

    private static bool TryGetDictionaryValue(
        GDictionary source,
        Variant key,
        out GDictionary value
    )
    {
        if (source.ContainsKey(key))
        {
            Variant rawValue = source[key];
            if (rawValue.VariantType == Variant.Type.Dictionary)
            {
                value = rawValue.AsGodotDictionary();
                return true;
            }
        }

        value = new GDictionary();
        return false;
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
            var key = ProgressionDataUtils.to_string_name(raw_key);
            var value = ProgressionDataUtils.to_string_name(source[raw_key]);
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
        if (grant.charge_kind == "at_will")
            return "随意";
        if (grant.charge_kind == "per_turn")
            return $"每回合 {Mathf.Max(grant.charges, 0)} 次";
        if (grant.charge_kind == "per_battle")
            return $"每场战斗 {Mathf.Max(grant.charges, 0)} 次";
        return $"{(string)grant.charge_kind} {Mathf.Max(grant.charges, 0)}";
    }

    private AttributeService _build_attribute_service(PartyMemberState member_state) =>
        _build_attribute_service(member_state, null);

    private AttributeService _build_attribute_service(
        PartyMemberState member_state,
        EquipmentState equipment_state_override
    )
    {
        var attribute_service = new AttributeService();
        attribute_service.setup_context(
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
        var member_state = get_member_state(member_id);
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

        var progression_service = _build_progression_service(progression);
        var mastery_source_type = _resolve_mastery_source_type(source_type);
        if (!progression_service.grant_skill_mastery(skill_id, amount, mastery_source_type))
        {
            delta.character_level_after = delta.character_level_before;
            return delta;
        }

        delta.mastery_changes.Add(
            new GDictionary
            {
                ["skill_id"] = skill_id,
                ["skill_name"] = _resolve_skill_label(skill_id),
                ["mastery_amount"] = amount,
                ["source_type"] = source_type,
                ["source_label"] = !string.IsNullOrEmpty(source_label)
                    ? source_label
                    : _build_default_source_label(source_type),
                ["reason_text"] = reason_text,
            }
        );
        _fill_delta_from_progression(
            delta,
            progression,
            before_skill_levels,
            before_granted_skill_ids,
            before_profession_ranks
        );
        if (emit_achievement_event)
            _append_unique_string_names(
                delta.unlocked_achievement_ids,
                record_achievement_event(member_id, "skill_mastery_gained", amount, skill_id)
            );
        return delta;
    }

    private static Dictionary<StringName, int> _capture_skill_levels(UnitProgress progression)
    {
        var skill_levels = new Dictionary<StringName, int>();
        if (progression == null)
            return skill_levels;
        foreach (var skill_key in progression.skills.Keys)
        {
            var skill_id = ProgressionDataUtils.to_string_name(skill_key);
            var skill_progress = progression.get_skill_progress(skill_id);
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
        foreach (var skill_key in progression.skills.Keys)
        {
            var skill_id = ProgressionDataUtils.to_string_name(skill_key);
            var skill_progress = progression.get_skill_progress(skill_id);
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
        foreach (var profession_key in progression.professions.Keys)
        {
            var profession_id = ProgressionDataUtils.to_string_name(profession_key);
            var progress = progression.get_profession_progress(profession_id);
            if (progress != null)
                ranks[profession_id] = progress.rank;
        }
        return ranks;
    }

    private Godot.Collections.Array<PendingCharacterRewardEntry> _normalize_pending_skill_mastery_entries(
        UnitProgress progression,
        GArray entry_options,
        StringName source_type
    )
    {
        var normalized_entries = new Godot.Collections.Array<PendingCharacterRewardEntry>();
        if (progression == null)
            return normalized_entries;
        var entry_map = new Dictionary<StringName, PendingCharacterRewardEntry>();
        if (entry_options == null)
            return normalized_entries;
        foreach (Variant entry_option in entry_options)
        {
            PendingCharacterRewardEntryData entry_data =
                PendingCharacterRewardEntryData.FromVariant(
                    entry_option,
                    PendingCharacterRewardContentRules.ENTRY_SKILL_MASTERY,
                    source_type
                );
            if (
                !entry_data.Exists
                || entry_data.EntryType != PendingCharacterRewardContentRules.ENTRY_SKILL_MASTERY
            )
                continue;
            var skill_id = entry_data.TargetId;
            var mastery_amount = entry_data.Amount;
            if (skill_id == "" || mastery_amount <= 0)
                continue;
            var mastery_source_type = _resolve_mastery_source_type(entry_data.MasterySourceType);
            var skill_progress = progression.get_skill_progress(skill_id);
            var skill_def = GetSkillDef(skill_id);
            if (skill_progress == null || skill_def == null || !skill_progress.is_learned)
                continue;
            if (
                skill_def.mastery_sources.Count > 0
                && !skill_def.mastery_sources.Contains(mastery_source_type)
            )
                continue;
            if (!entry_map.TryGetValue(skill_id, out var reward_entry))
            {
                reward_entry = new PendingCharacterRewardEntry
                {
                    entry_type = "skill_mastery",
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
        object raw_reward_option,
        bool allow_unsupported_entries = false
    )
    {
        if (raw_reward_option is PendingCharacterReward raw_typed_reward)
        {
            if (
                !allow_unsupported_entries
                && _has_unsupported_pending_character_entry_object(raw_typed_reward.entries)
            )
                return null;
            if (raw_typed_reward.reward_id == "")
                raw_typed_reward.reward_id = _build_reward_id(
                    raw_typed_reward.member_id,
                    raw_typed_reward.source_id != ""
                        ? raw_typed_reward.source_id
                        : raw_typed_reward.source_type
                );
            return raw_typed_reward.is_empty() ? null : raw_typed_reward;
        }
        var unboxedTyped = UnboxPendingCharacterReward(raw_reward_option);
        if (unboxedTyped != null)
        {
            if (!allow_unsupported_entries && _has_unsupported_pending_character_entry_object(unboxedTyped.entries))
                return null;
            if (unboxedTyped.reward_id == "")
                unboxedTyped.reward_id = _build_reward_id(
                    unboxedTyped.member_id,
                    unboxedTyped.source_id != "" ? unboxedTyped.source_id : unboxedTyped.source_type
            );
            return unboxedTyped.is_empty() ? null : unboxedTyped;
        }
        if (TryUnboxRewardDictionary(raw_reward_option, out var rewardDict))
            return _normalize_pending_character_reward_dictionary(rewardDict);
        return null;
    }

    private static PendingCharacterReward UnboxPendingCharacterReward(object raw_reward_option)
    {
        if (raw_reward_option is PendingCharacterReward typed_reward)
            return typed_reward;
        if (
            raw_reward_option is Variant variant_value
            && variant_value.TryAsObject<PendingCharacterReward>(out var variant_reward)
        )
            return variant_reward;
        return null;
    }

    private static bool TryUnboxRewardDictionary(
        object raw_reward_option,
        out GDictionary reward_data
    )
    {
        if (raw_reward_option is GDictionary dictionary_reward)
        {
            reward_data = dictionary_reward;
            return true;
        }
        if (
            raw_reward_option is Variant variant_value
            && variant_value.TryAsDictionary(out reward_data)
        )
            return true;
        reward_data = null;
        return false;
    }

    private PendingCharacterReward _normalize_pending_character_reward_dictionary(
        GDictionary reward_data
    )
    {
        var normalized_reward = PendingCharacterReward.from_dict(reward_data);
        if (normalized_reward == null || normalized_reward.is_empty())
            return null;
        if (normalized_reward.reward_id == "")
            normalized_reward.reward_id = _build_reward_id(
                normalized_reward.member_id,
                normalized_reward.source_id != ""
                    ? normalized_reward.source_id
                    : normalized_reward.source_type
            );
        return normalized_reward;
    }

    private QuestRewardData _resolve_quest_reward_data(StringName quest_id)
    {
        if (quest_id == "" || !TryGetExactStringNameKey(_quest_defs, quest_id, out var questVal))
            return QuestRewardData.Missing();
        if (questVal.TryAsDictionary(out var questDict))
            return QuestRewardData.FromDictionary(questDict);
        if (questVal.TryAsObject<QuestDef>(out var questDef))
            return QuestRewardData.FromQuestDef(questDef);
        return QuestRewardData.Missing();
    }

    private QuestSubmitItemPreviewData _preview_quest_submit_item_objective(
        StringName quest_id,
        StringName objective_id = default
    )
    {
        if (_party_state == null || quest_id == "")
        {
            return QuestSubmitItemPreviewData.Failed("invalid_quest_id");
        }
        var quest_state = _party_state.get_active_quest_state(quest_id);
        if (quest_state == null)
        {
            return QuestSubmitItemPreviewData.Failed("quest_not_active");
        }
        if (quest_id == "" || !TryGetExactStringNameKey(_quest_defs, quest_id, out var questVal2))
        {
            return QuestSubmitItemPreviewData.Failed("quest_def_missing");
        }

        if (!TryReadQuestObjectiveDefs(questVal2, out var objective_defs))
        {
            return QuestSubmitItemPreviewData.Failed("quest_def_missing");
        }

        var requested_objective_id = ProgressionDataUtils.to_string_name(objective_id);
        var found_completed_submit_item_objective = false;
        var completed_preview = QuestSubmitItemPreviewData.Failed("objective_already_complete");
        foreach (var objective_data in objective_defs)
        {
            if (
                !objective_data.Exists
                || objective_data.ObjectiveType != QuestDef.OBJECTIVE_SUBMIT_ITEM()
            )
                continue;
            var current_objective_id = objective_data.ObjectiveId;
            if (requested_objective_id != "" && current_objective_id != requested_objective_id)
                continue;
            var item_id = objective_data.TargetId;
            var target_value = objective_data.TargetValue;
            var required_quantity = Mathf.Max(
                target_value - quest_state.get_objective_progress(current_objective_id),
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
            if (quest_state.is_objective_complete(current_objective_id, target_value))
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
        var reward_item_ids = new GStringNameArray();
        var pending_character_rewards = new GArray();
        var gold_delta = 0;
        foreach (var reward_data in quest_reward_data.RewardEntries)
        {
            if (!reward_data.Exists)
                return QuestRewardPreviewData.Failed("invalid_reward_entry");
            var reward_type = reward_data.RewardType;
            if (reward_type == "")
                return QuestRewardPreviewData.Failed("invalid_reward_entry");
            if (reward_type == QuestDef.REWARD_GOLD())
            {
                var amount = reward_data.Amount;
                if (amount <= 0)
                    return QuestRewardPreviewData.Failed("invalid_gold_amount");
                gold_delta += amount;
            }
            else if (reward_type == QuestDef.REWARD_ITEM())
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
            else if (reward_type == QuestDef.REWARD_PENDING_CHARACTER_REWARD())
            {
                var pending_reward_result = _preview_quest_pending_character_reward_entry(
                    quest_id,
                    quest_label,
                    reward_data
                );
                if (!pending_reward_result.Ok)
                    return QuestRewardPreviewData.Failed(pending_reward_result.ErrorCode);
                var reward = pending_reward_result.PendingReward;
                if (reward != null && !reward.is_empty())
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
                new GStringNameArray(),
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
        if (_item_defs == null || !TryGetExactStringNameKey(_item_defs, reward_item_id, out var itemValue))
            return QuestItemRewardPreviewData.Failed("item_reward_missing_def");
        var item_display_name = "";
        if (itemValue.TryAsObject<ItemDef>(out var itemDef))
            item_display_name = itemDef.display_name.StripEdges();
        else if (itemValue.TryAsDictionary(out var itemData))
        {
            item_display_name = CharacterQuestDataReader.ReadTrimmedString(
                itemData,
                "display_name"
            );
        }
        else
            return QuestItemRewardPreviewData.Failed("item_reward_missing_def");
        if (item_display_name.Length == 0)
            return QuestItemRewardPreviewData.Failed("invalid_item_display_name");
        return QuestItemRewardPreviewData.Success(
            new GDictionary
            {
                ["item_id"] = reward_item_id.ToString(),
                ["display_name"] = item_display_name,
                ["quantity"] = reward_quantity,
            },
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
        var pending_reward = build_pending_character_reward(
            member_id,
            reward_data.RewardId,
            source_type,
            source_id,
            source_label,
            entry_options,
            summary_text
        );
        return pending_reward == null || pending_reward.is_empty()
            ? QuestPendingCharacterRewardPreviewData.Failed("invalid_pending_character_reward")
            : QuestPendingCharacterRewardPreviewData.Success(pending_reward);
    }

    private static bool TryReadQuestObjectiveDefs(
        Variant questDefValue,
        out List<QuestObjectiveDefData> objectiveDefs
    )
    {
        objectiveDefs = new List<QuestObjectiveDefData>();
        if (questDefValue.TryAsObject<QuestDef>(out var questDef))
        {
            foreach (var objectiveData in questDef.objective_defs)
            {
                var objectiveDef = QuestObjectiveDefData.FromDictionary(objectiveData);
                if (objectiveDef.Exists)
                    objectiveDefs.Add(objectiveDef);
            }
            return true;
        }
        if (questDefValue.TryAsDictionary(out var questDict))
        {
            foreach (var objectiveData in CharacterQuestDataReader.ReadArray(
                questDict,
                "objective_defs"
            ))
            {
                var objectiveDef = QuestObjectiveDefData.FromVariant(objectiveData);
                if (objectiveDef.Exists)
                    objectiveDefs.Add(objectiveDef);
            }
            return true;
        }
        return false;
    }

    private static GStringNameArray _build_repeated_item_ids(StringName item_id, int quantity)
    {
        var item_ids = new GStringNameArray();
        for (var i = 0; i < Mathf.Max(quantity, 0); i++)
            item_ids.Add(item_id);
        return item_ids;
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

        public static PendingCharacterRewardEntryData FromVariant(
            Variant value,
            StringName defaultEntryType = default,
            StringName defaultSourceType = default
        )
        {
            if (value.TryAsObject<PendingCharacterRewardEntry>(out var typedEntry))
                return FromEntry(typedEntry, defaultEntryType, defaultSourceType);
            if (value.TryAsDictionary(out var entryData))
                return FromDictionary(entryData, defaultEntryType, defaultSourceType);
            return Missing();
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

        private static PendingCharacterRewardEntryData FromEntry(
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
                defaultSourceType
            );
        }

        private static PendingCharacterRewardEntryData Missing() =>
            new(false, "", "", 0, "", "", "", "");
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
            foreach (var rewardEntry in questDef.reward_entries)
                rewardEntries.Add(QuestRewardEntryData.FromDictionary(rewardEntry));
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
        private readonly GArray _entries;

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
            GArray entries
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
            _entries = entries != null ? entries.Duplicate(true) : new GArray();
        }

        public GArray CloneEntries() => _entries.Duplicate(true);

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
                CharacterQuestDataReader.ReadArray(data, "entries")
            );
        }

        private static QuestRewardEntryData Missing() =>
            new(false, "", 0, "", 0, "", "", "", "", "", "", new GArray());
    }

    private sealed class QuestRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly int GoldDelta;
        private readonly GArray _itemRewards;
        private readonly GStringNameArray _warehouseDepositItemIds;
        private readonly GArray _pendingCharacterRewards;
        private readonly GStringNameArray _unsupportedRewardTypes;

        private QuestRewardPreviewData(
            bool ok,
            string errorCode,
            int goldDelta,
            GArray itemRewards,
            GStringNameArray warehouseDepositItemIds,
            GArray pendingCharacterRewards,
            GStringNameArray unsupportedRewardTypes
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            GoldDelta = Mathf.Max(goldDelta, 0);
            _itemRewards = itemRewards != null ? itemRewards.Duplicate(true) : new GArray();
            _warehouseDepositItemIds =
                warehouseDepositItemIds != null
                    ? CloneStringNameArray(warehouseDepositItemIds)
                    : new GStringNameArray();
            _pendingCharacterRewards =
                pendingCharacterRewards != null
                    ? pendingCharacterRewards.Duplicate(true)
                    : new GArray();
            _unsupportedRewardTypes =
                unsupportedRewardTypes != null
                    ? CloneStringNameArray(unsupportedRewardTypes)
                    : new GStringNameArray();
        }

        public GArray CloneItemRewards() => _itemRewards.Duplicate(true);

        public GStringNameArray CloneWarehouseDepositItemIds() =>
            CloneStringNameArray(_warehouseDepositItemIds);

        public GArray ClonePendingCharacterRewards() => _pendingCharacterRewards.Duplicate(true);

        public GStringNameArray CloneUnsupportedRewardTypes() =>
            CloneStringNameArray(_unsupportedRewardTypes);

        public static QuestRewardPreviewData Success(
            int goldDelta,
            GArray itemRewards,
            GStringNameArray warehouseDepositItemIds,
            GArray pendingCharacterRewards
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
                new GArray(),
                unsupportedRewardTypes
            );
    }

    private sealed class QuestItemRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        private readonly GDictionary _itemReward;
        private readonly GStringNameArray _warehouseDepositItemIds;

        private QuestItemRewardPreviewData(
            bool ok,
            string errorCode,
            GDictionary itemReward,
            GStringNameArray warehouseDepositItemIds
        )
        {
            Ok = ok;
            ErrorCode = ok
                ? ""
                : string.IsNullOrEmpty(errorCode)
                    ? "invalid_item_reward"
                    : errorCode;
            _itemReward = itemReward != null ? itemReward.Duplicate(true) : new GDictionary();
            _warehouseDepositItemIds =
                warehouseDepositItemIds != null
                    ? CloneStringNameArray(warehouseDepositItemIds)
                    : new GStringNameArray();
        }

        public GDictionary CloneItemReward() => _itemReward.Duplicate(true);

        public GStringNameArray CloneWarehouseDepositItemIds() =>
            CloneStringNameArray(_warehouseDepositItemIds);

        public static QuestItemRewardPreviewData Success(
            GDictionary itemReward,
            GStringNameArray warehouseDepositItemIds
        ) =>
            new(true, "", itemReward, warehouseDepositItemIds);

        public static QuestItemRewardPreviewData Failed(string errorCode) =>
            new(false, errorCode, new GDictionary(), new GStringNameArray());
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

    private sealed class QuestProgressApplySummaryData
    {
        private readonly GStringNameArray _acceptedQuestIds;
        private readonly GStringNameArray _progressedQuestIds;
        private readonly GStringNameArray _claimableQuestIds;
        private readonly GStringNameArray _completedQuestIds;

        private QuestProgressApplySummaryData(
            GStringNameArray acceptedQuestIds,
            GStringNameArray progressedQuestIds,
            GStringNameArray claimableQuestIds,
            GStringNameArray completedQuestIds
        )
        {
            _acceptedQuestIds = acceptedQuestIds;
            _progressedQuestIds = progressedQuestIds;
            _claimableQuestIds = claimableQuestIds;
            _completedQuestIds = completedQuestIds;
        }

        public bool ContainsProgressedQuest(StringName questId) =>
            questId != "" && _progressedQuestIds.Contains(questId);

        public GStringNameArray CloneAcceptedQuestIds() =>
            CloneStringNameArray(_acceptedQuestIds);

        public GStringNameArray CloneProgressedQuestIds() =>
            CloneStringNameArray(_progressedQuestIds);

        public GStringNameArray CloneClaimableQuestIds() =>
            CloneStringNameArray(_claimableQuestIds);

        public GStringNameArray CloneCompletedQuestIds() =>
            CloneStringNameArray(_completedQuestIds);

        public static QuestProgressApplySummaryData FromDictionary(GDictionary summary) =>
            new(
                CharacterQuestDataReader.ReadStringNameArray(summary, "accepted_quest_ids"),
                CharacterQuestDataReader.ReadStringNameArray(summary, "progressed_quest_ids"),
                CharacterQuestDataReader.ReadStringNameArray(summary, "claimable_quest_ids"),
                CharacterQuestDataReader.ReadStringNameArray(summary, "completed_quest_ids")
            );
    }

    private static class CharacterQuestDataReader
    {
        public static string ReadString(GDictionary data, string key)
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

        public static bool TryReadString(GDictionary data, string key, out string result)
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

        public static string ReadTrimmedString(GDictionary data, string key) =>
            ReadString(data, key).StripEdges();

        public static StringName ReadStringName(GDictionary data, string key)
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

        public static bool TryReadInt(GDictionary data, string key, out int result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Int)
            {
                result = 0;
                return false;
            }
            result = value.AsInt32();
            return true;
        }

        public static GArray ReadArray(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return new GArray();
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
        }

        public static GStringNameArray ReadStringNameArray(GDictionary data, string key)
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

    private GArray _pending_character_reward_options_to_dicts(GArray reward_options)
    {
        var reward_dicts = new GArray();
        foreach (var reward_option in reward_options)
        {
            var reward = _normalize_pending_character_reward_option(reward_option);
            if (reward == null || reward.is_empty())
                continue;
            reward_dicts.Add(reward.to_dict());
        }
        return reward_dicts;
    }

    private Godot.Collections.Array<PendingCharacterRewardEntry> _normalize_pending_character_entries(
        GArray entry_options
    )
    {
        var entries = new Godot.Collections.Array<PendingCharacterRewardEntry>();
        if (entry_options == null)
            return entries;
        foreach (Variant entry_option in entry_options)
        {
            PendingCharacterRewardEntry entry =
                _normalize_pending_character_entry(
                    PendingCharacterRewardEntryData.FromVariant(entry_option)
                );
            if (entry != null && !entry.is_empty())
                entries.Add(entry);
        }
        return entries;
    }

    private bool _has_unsupported_pending_character_entry_type(GArray entry_options)
    {
        if (entry_options == null)
            return false;
        foreach (Variant entry_option in entry_options)
        {
            PendingCharacterRewardEntryData entry_data =
                PendingCharacterRewardEntryData.FromVariant(entry_option);
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
        Godot.Collections.Array<PendingCharacterRewardEntry> entries
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
        if (!PendingCharacterRewardContentRules.is_supported_entry_type(entry_type))
            return true;
        if (
            PendingCharacterRewardContentRules.is_attribute_progress_entry(entry_type)
            && !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(target_id)
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
        if (!PendingCharacterRewardContentRules.is_supported_entry_type(entry_type))
            return null;
        if (
            PendingCharacterRewardContentRules.is_attribute_progress_entry(entry_type)
            && !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(target_id)
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
        return reward.is_empty() ? null : reward;
    }

    private Godot.Collections.Array<PendingCharacterRewardEntry> _build_achievement_reward_entries(
        AchievementDef achievement_def
    )
    {
        var entries = new Godot.Collections.Array<PendingCharacterRewardEntry>();
        if (achievement_def == null)
            return entries;
        foreach (var reward_def_object in achievement_def.rewards)
        {
            var reward_def = reward_def_object as AchievementRewardDef;
            if (reward_def == null || reward_def.is_empty())
                continue;
            if (!PendingCharacterRewardContentRules.is_supported_entry_type(reward_def.reward_type))
            {
                GameLog.Error(
                    $"Achievement {(string)achievement_def.achievement_id} has unsupported pending reward entry_type {(string)reward_def.reward_type}.",
                    "progression.reward.unsupported_type",
                    "progression"
                );
                return new Godot.Collections.Array<PendingCharacterRewardEntry>();
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
            if (!entry.is_empty())
                entries.Add(entry);
        }
        return entries;
    }

    private Godot.Collections.Array<AchievementDef> _get_matching_achievement_defs(
        StringName event_type,
        StringName subject_id
    )
    {
        var matches = new Godot.Collections.Array<AchievementDef>();
        foreach (
            string achievement_key in ProgressionDataUtils.sorted_string_keys(_achievement_defs)
        )
        {
            var achievement_id = new StringName(achievement_key);
            var achievement_def = GetAchievementDef(achievement_id);
            if (achievement_def != null && achievement_def.matches_event(event_type, subject_id))
                matches.Add(achievement_def);
        }
        return matches;
    }

    private static Godot.Collections.Array<PendingCharacterRewardEntry> _sort_pending_reward_entries(
        Godot.Collections.Array<PendingCharacterRewardEntry> entries
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
        var result = new Godot.Collections.Array<PendingCharacterRewardEntry>();
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
        delta.pending_profession_choices = ToUntyped(progression.pending_profession_choices);
        delta.needs_promotion_modal = delta.pending_profession_choices.Count > 0;
        foreach (var skill_key in progression.skills.Keys)
        {
            var skill_id = ProgressionDataUtils.to_string_name(skill_key);
            var skill_progress = progression.get_skill_progress(skill_id);
            if (skill_progress == null)
                continue;
            var before_level = before_skill_levels.TryGetValue(skill_id, out var captured_level)
                ? captured_level
                : -1;
            if (before_level >= 0 && skill_progress.skill_level > before_level)
                _append_unique_string_name(delta.leveled_skill_ids, skill_id);
            if (
                skill_progress.profession_granted_by != ""
                && !before_granted_skill_ids.Contains(skill_id)
            )
                _append_unique_string_name(delta.granted_skill_ids, skill_id);
        }
        foreach (var profession_key in progression.professions.Keys)
        {
            var profession_id = ProgressionDataUtils.to_string_name(profession_key);
            var profession_progress = progression.get_profession_progress(profession_id);
            if (profession_progress == null)
                continue;
            var before_rank = before_profession_ranks.TryGetValue(
                profession_id,
                out var captured_rank
            )
                ? captured_rank
                : 0;
            if (profession_progress.rank != before_rank)
                _append_unique_string_name(delta.changed_profession_ids, profession_id);
        }
    }

    private static void _merge_delta(
        CharacterProgressionDelta target,
        CharacterProgressionDelta source
    )
    {
        if (target == null || source == null)
            return;
        _append_unique_string_names(target.leveled_skill_ids, source.leveled_skill_ids);
        _append_unique_string_names(target.granted_skill_ids, source.granted_skill_ids);
        _append_unique_string_names(target.changed_profession_ids, source.changed_profession_ids);
        _append_unique_string_names(
            target.unlocked_achievement_ids,
            source.unlocked_achievement_ids
        );
        target.mastery_changes.AddRange(source.mastery_changes);
        target.knowledge_changes.AddRange(source.knowledge_changes);
        target.attribute_changes.AddRange(source.attribute_changes);
        if (source.pending_profession_choices.Count > 0)
            target.pending_profession_choices = source.pending_profession_choices;
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
        _party_state.remove_pending_character_reward(reward_id);
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
        if (entry_type == "skill_unlock" || entry_type == "skill_mastery")
            return _resolve_skill_label(target_id);
        if (entry_type == "attribute_delta" || entry_type == "attribute_progress")
            return _resolve_attribute_label(target_id);
        return (string)target_id;
    }

    private static string _resolve_attribute_label(StringName attribute_id)
    {
        if (attribute_id == UnitBaseAttributes.STRENGTH())
            return "力量";
        if (attribute_id == UnitBaseAttributes.AGILITY())
            return "敏捷";
        if (attribute_id == UnitBaseAttributes.CONSTITUTION())
            return "体质";
        if (attribute_id == UnitBaseAttributes.PERCEPTION())
            return "感知";
        if (attribute_id == UnitBaseAttributes.INTELLIGENCE())
            return "智力";
        if (attribute_id == UnitBaseAttributes.WILLPOWER())
            return "意志";
        if (attribute_id == AttributeService.HP_MAX_ID())
            return "生命上限";
        if (attribute_id == AttributeService.CHARACTER_HP_MAX_PERCENT_BONUS_ID())
            return "人物生命加成%";
        if (attribute_id == AttributeService.MP_MAX_ID())
            return "法力上限";
        if (attribute_id == AttributeService.STAMINA_MAX_ID())
            return "体力上限";
        if (attribute_id == AttributeService.ACTION_POINTS_ID())
            return "行动点";
        if (attribute_id == AttributeService.ATTACK_BONUS_ID())
            return "攻击加值";
        if (attribute_id == AttributeService.ARMOR_CLASS_ID())
            return "AC";
        if (attribute_id == AttributeService.ARMOR_AC_BONUS_ID())
            return "护甲 AC";
        if (attribute_id == AttributeService.SHIELD_AC_BONUS_ID())
            return "盾牌 AC";
        if (attribute_id == AttributeService.DODGE_BONUS_ID())
            return "闪避加值";
        if (attribute_id == AttributeService.DEFLECTION_BONUS_ID())
            return "偏斜加值";
        if (attribute_id == AttributeService.ARMOR_MAX_DEX_BONUS_ID())
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

    private static bool HasConfirmedPracticeReplacement(GDictionary options)
    {
        return TryGetExactBoolParam(options, "confirm_practice_replacement", out bool confirmed)
            && confirmed;
    }

    private static bool TryGetExactBoolParam(GDictionary dict, string key, out bool value)
    {
        value = false;
        if (dict == null || string.IsNullOrEmpty(key) || !dict.ContainsKey(key))
            return false;
        Variant rawValue = dict[key];
        if (rawValue.VariantType != Variant.Type.Bool)
            return false;
        value = rawValue.AsBool();
        return true;
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

    private static bool TryGetExactStringNameKey(
        GDictionary dictionary,
        StringName key,
        out Variant value
    )
    {
        if (dictionary == null || key == "")
        {
            value = default;
            return false;
        }
        foreach (Variant rawKey in dictionary.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            if (rawKey.AsStringName() != key)
                continue;
            value = dictionary[rawKey];
            return true;
        }
        value = default;
        return false;
    }

    private static GArray ToUntyped(Godot.Collections.Array<StageAdvancementModifier> values)
    {
        var result = new GArray();
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntyped(Godot.Collections.Array<AttributeModifier> values)
    {
        var result = new GArray();
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntyped(Godot.Collections.Array<PendingProfessionChoice> values)
    {
        var result = new GArray();
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
