using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal interface IBattlePresentationSnapshotValue
{
    IReadOnlyDictionary<string, object> CanonicalFacts { get; }
}

internal sealed class BattlePresentationPayload : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyDictionary<string, object> _facts;

    internal static BattlePresentationPayload Empty { get; } =
        new(new Dictionary<string, object>(StringComparer.Ordinal));

    internal BattlePresentationPayload(IReadOnlyDictionary<string, object> facts)
    {
        _facts = BattlePresentationSnapshotFreeze.Dictionary(facts);
    }

    public IReadOnlyDictionary<string, object> CanonicalFacts => _facts;

    internal bool IsEmpty => _facts.Count == 0;

    internal string SummaryText => GetString("summary_text");

    internal string Source => GetString("source");

    private string GetString(string key, string fallback = "") =>
        _facts.TryGetValue(key, out object value)
            ? value switch
            {
                string text => text,
                StringName name => name.ToString(),
                _ => fallback,
            }
            : fallback;

    internal static BattlePresentationPayload FromAttackPreview(AttackPreviewData preview)
    {
        if (preview == null)
            return Empty;
        var modifiers = new List<object>();
        foreach (
            BattleAttackRollModifierSpec spec in
            preview.AttackRollModifierBreakdownTyped
                ?? Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (spec != null)
                modifiers.Add(spec.ToTraceDictionary());
        }
        var stageHitRates = new List<int>();
        var stageSuccessRates = new List<int>();
        var stageBaseHitRates = new List<int>();
        var stageRequiredRolls = new List<int>();
        var stagePreviewTexts = new List<string>();
        foreach (AttackPreviewStage stage in preview.Stages ?? new List<AttackPreviewStage>())
        {
            stageHitRates.Add(stage.HitRatePercent);
            stageSuccessRates.Add(stage.SuccessRatePercent);
            stageBaseHitRates.Add(stage.BaseHitRatePercent);
            stageRequiredRolls.Add(stage.DisplayRequiredRoll);
            stagePreviewTexts.Add(stage.PreviewText ?? "");
        }
        return new BattlePresentationPayload(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["summary_text"] = preview.SummaryText ?? "",
                ["source"] = preview.Source ?? "",
                ["hit_rate_percent"] = preview.HitRatePercent,
                ["success_rate_percent"] = preview.SuccessRatePercent,
                ["base_hit_rate_percent"] = preview.BaseHitRatePercent,
                ["force_hit_no_crit"] = preview.ForceHitNoCrit,
                ["force_critical_on_hit"] = preview.ForceCriticalOnHit,
                ["crit_locked"] = preview.CritLocked,
                ["stage_hit_rates"] = stageHitRates,
                ["stage_success_rates"] = stageSuccessRates,
                ["stage_base_hit_rates"] = stageBaseHitRates,
                ["stage_required_rolls"] = stageRequiredRolls,
                ["stage_preview_texts"] = stagePreviewTexts,
                ["attack_roll_modifier_breakdown"] = modifiers,
            }
        );
    }

    internal static BattlePresentationPayload FromSaveBranch(
        BattleSaveBranchPreviewData preview
    )
    {
        if (preview == null || preview.IsEmpty)
            return Empty;
        var facts = new Dictionary<string, object>(StringComparer.Ordinal);
        if (preview.HasKnownPayload())
        {
            facts["kind"] = preview.Kind;
            facts["branch"] = preview.Branch;
            facts["save_tag"] = preview.SaveTag;
            facts["save_ability"] = preview.SaveAbility;
            facts["save_dc"] = preview.SaveDc;
            facts["save_advantage_state"] = preview.SaveAdvantageState;
            facts["save_success_chance_basis_points"] =
                preview.SaveSuccessChanceBasisPoints;
            facts["hit_chance_basis_points"] = preview.HitChanceBasisPoints;
            facts["threshold"] = preview.Threshold;
            facts["current_hp"] = preview.CurrentHp;
            facts["max_hp"] = preview.MaxHp;
            facts["failure_branch_text"] = preview.FailureBranchText ?? "";
            facts["success_branch_text"] = preview.SuccessBranchText ?? "";
            facts["summary_text"] = preview.SummaryText ?? "";
        }
        foreach (KeyValuePair<string, object> entry in preview.ResidualValues)
        {
            if (
                !string.IsNullOrEmpty(entry.Key)
                && !BattleSaveBranchPreviewData.IsKnownKey(entry.Key)
            )
            {
                facts[entry.Key] = entry.Value;
            }
        }
        return new BattlePresentationPayload(facts);
    }
}

internal sealed record BattleHudRoundBadgeSnapshot(string TuText, string ReadyText)
    : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("tu_text", TuText ?? ""),
            ("ready_text", ReadyText ?? "")
        );
}

internal sealed record BattleHudResourceLineSnapshot(
    int Current,
    int Max,
    float Ratio,
    string Label,
    bool Visible
) : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("current", Current),
            ("max", Max),
            ("ratio", Ratio),
            ("label", Label ?? ""),
            ("visible", Visible)
        );
}

internal sealed class BattleHudResourceInfoSnapshot : IBattlePresentationSnapshotValue
{
    internal BattleHudResourceInfoSnapshot(
        BattleHudResourceLineSnapshot hp,
        BattleHudResourceLineSnapshot mp,
        BattleHudResourceLineSnapshot stamina,
        BattleHudResourceLineSnapshot aura,
        BattleHudResourceLineSnapshot ap,
        BattleHudResourceLineSnapshot move
    )
    {
        Hp = hp;
        Mp = mp;
        Stamina = stamina;
        Aura = aura;
        Ap = ap;
        Move = move;
    }

    internal BattleHudResourceLineSnapshot Hp { get; }
    internal BattleHudResourceLineSnapshot Mp { get; }
    internal BattleHudResourceLineSnapshot Stamina { get; }
    internal BattleHudResourceLineSnapshot Aura { get; }
    internal BattleHudResourceLineSnapshot Ap { get; }
    internal BattleHudResourceLineSnapshot Move { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("hp", Hp),
            ("mp", Mp),
            ("stamina", Stamina),
            ("aura", Aura),
            ("ap", Ap),
            ("move", Move)
        );
}

internal sealed record BattleHudStatusEffectSnapshot(
    string StatusId,
    string Label,
    int Stacks,
    int RemainingTu,
    bool IsDebuff,
    string TooltipText
) : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("status_id", StatusId ?? ""),
            ("label", Label ?? ""),
            ("stacks", Stacks),
            ("remaining_tu", RemainingTu),
            ("is_debuff", IsDebuff),
            ("tooltip_text", TooltipText ?? "")
        );
}

internal sealed record BattleHudFocusUnitSnapshot(
    string Name,
    string RoleText,
    BattleHudResourceInfoSnapshot ResourceInfo,
    string Glyph,
    string PortraitKey,
    Color PrimaryColor,
    Color SecondaryColor,
    Color EdgeColor,
    int HpCurrent,
    int HpMax,
    int MpCurrent,
    int MpMax,
    int StaminaCurrent,
    int StaminaMax,
    int AuraCurrent,
    int AuraMax,
    int ApCurrent,
    int ApMax,
    int MoveCurrent,
    int MoveMax,
    IReadOnlyList<BattleHudStatusEffectSnapshot> StatusEffects = null
) : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("name", Name ?? ""),
            ("role_text", RoleText ?? ""),
            ("resource_info", ResourceInfo),
            ("glyph", Glyph ?? ""),
            ("portrait_key", PortraitKey ?? ""),
            ("primary_color", PrimaryColor),
            ("secondary_color", SecondaryColor),
            ("edge_color", EdgeColor),
            ("hp_current", HpCurrent),
            ("hp_max", HpMax),
            ("mp_current", MpCurrent),
            ("mp_max", MpMax),
            ("stamina_current", StaminaCurrent),
            ("stamina_max", StaminaMax),
            ("aura_current", AuraCurrent),
            ("aura_max", AuraMax),
            ("ap_current", ApCurrent),
            ("ap_max", ApMax),
            ("move_current", MoveCurrent),
            ("move_max", MoveMax),
            (
                "status_effects",
                (object)StatusEffects ?? Array.Empty<BattleHudStatusEffectSnapshot>()
            )
        );
}

internal sealed class BattleHudQueueEntrySnapshot : IBattlePresentationSnapshotValue
{
    internal static BattleHudQueueEntrySnapshot Overflow(string text) =>
        new() { IsOverflow = true, OverflowText = text ?? "" };

    internal int SlotIndex { get; init; }
    internal string Name { get; init; } = "";
    internal string Glyph { get; init; } = "";
    internal string PortraitKey { get; init; } = "";
    internal Color PrimaryColor { get; init; }
    internal Color SecondaryColor { get; init; }
    internal Color EdgeColor { get; init; }
    internal float HpRatio { get; init; }
    internal string HpText { get; init; } = "";
    internal string ApText { get; init; } = "";
    internal bool IsActive { get; init; }
    internal bool IsReady { get; init; }
    internal bool IsEnemy { get; init; }
    internal bool IsOverflow { get; init; }
    internal string OverflowText { get; init; } = "";

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        IsOverflow
            ? BattlePresentationSnapshotFacts.Map(
                ("is_overflow", true),
                ("overflow_text", OverflowText ?? "")
            )
            : BattlePresentationSnapshotFacts.Map(
                ("slot_index", SlotIndex),
                ("name", Name ?? ""),
                ("glyph", Glyph ?? ""),
                ("portrait_key", PortraitKey ?? ""),
                ("primary_color", PrimaryColor),
                ("secondary_color", SecondaryColor),
                ("edge_color", EdgeColor),
                ("hp_ratio", HpRatio),
                ("hp_text", HpText ?? ""),
                ("ap_text", ApText ?? ""),
                ("is_active", IsActive),
                ("is_ready", IsReady),
                ("is_enemy", IsEnemy)
            );
}

internal sealed class BattleHudSkillSlotSnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<StringName> _suppressedSourceKeys;

    internal BattleHudSkillSlotSnapshot(
        int index,
        bool isEmpty,
        string skillEntryId = "",
        string skillId = "",
        string sourceKind = "",
        string sourceLabelKey = "",
        int skillLevel = 0,
        bool isBattleOnly = false,
        IEnumerable<StringName> suppressedSourceKeys = null,
        string displayName = "",
        string shortName = "",
        string description = "",
        string iconKey = "",
        string hotkey = "",
        string footerText = "",
        bool isSelected = false,
        bool isDisabled = false,
        Color accentColor = default,
        Color accentDark = default,
        Color edgeColor = default,
        int cooldown = 0,
        string disabledReason = ""
    )
    {
        Index = index;
        IsEmpty = isEmpty;
        SkillEntryId = skillEntryId ?? "";
        SkillId = skillId ?? "";
        SourceKind = sourceKind ?? "";
        SourceLabelKey = sourceLabelKey ?? "";
        SkillLevel = skillLevel;
        IsBattleOnly = isBattleOnly;
        _suppressedSourceKeys = new List<StringName>(
            suppressedSourceKeys ?? Array.Empty<StringName>()
        ).AsReadOnly();
        DisplayName = displayName ?? "";
        ShortName = shortName ?? "";
        Description = description ?? "";
        IconKey = iconKey ?? "";
        Hotkey = hotkey ?? "";
        FooterText = footerText ?? "";
        IsSelected = isSelected;
        IsDisabled = isDisabled;
        AccentColor = accentColor;
        AccentDark = accentDark;
        EdgeColor = edgeColor;
        Cooldown = cooldown;
        DisabledReason = disabledReason ?? "";
    }

    internal int Index { get; }
    internal bool IsEmpty { get; }
    internal string SkillEntryId { get; }
    internal string SkillId { get; }
    internal string SourceKind { get; }
    internal string SourceLabelKey { get; }
    internal int SkillLevel { get; }
    internal bool IsBattleOnly { get; }
    internal IReadOnlyList<StringName> SuppressedSourceKeys => _suppressedSourceKeys;
    internal string DisplayName { get; }
    internal string ShortName { get; }
    internal string Description { get; }
    internal string IconKey { get; }
    internal string Hotkey { get; }
    internal string FooterText { get; }
    internal bool IsSelected { get; }
    internal bool IsDisabled { get; }
    internal Color AccentColor { get; }
    internal Color AccentDark { get; }
    internal Color EdgeColor { get; }
    internal int Cooldown { get; }
    internal string DisabledReason { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        IsEmpty
            ? BattlePresentationSnapshotFacts.Map(("index", Index), ("is_empty", true))
            : BattlePresentationSnapshotFacts.Map(
                ("index", Index),
                ("is_empty", false),
                ("skill_entry_id", SkillEntryId),
                ("skill_id", SkillId),
                ("source_kind", SourceKind),
                ("source_label_key", SourceLabelKey),
                ("skill_level", SkillLevel),
                ("is_battle_only", IsBattleOnly),
                ("suppressed_source_keys", _suppressedSourceKeys),
                ("display_name", DisplayName),
                ("short_name", ShortName),
                ("description", Description),
                ("icon_key", IconKey),
                ("hotkey", Hotkey),
                ("footer_text", FooterText),
                ("is_selected", IsSelected),
                ("is_disabled", IsDisabled),
                ("accent_color", AccentColor),
                ("accent_dark", AccentDark),
                ("edge_color", EdgeColor),
                ("cooldown", Cooldown),
                ("disabled_reason", DisabledReason)
            );
}

internal sealed record BattleHudFateBadgeSnapshot(
    string Text,
    StringName Tone,
    string TooltipText
) : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("text", Text ?? ""),
            ("tone", Tone ?? new StringName("")),
            ("tooltip_text", TooltipText ?? "")
        );
}

internal sealed record BattleHudCommandDockSnapshot(
    bool ResolveEnabled,
    bool ClearSkillEnabled,
    bool PrevVariantEnabled,
    bool NextVariantEnabled
) : IBattlePresentationSnapshotValue
{
    internal static BattleHudCommandDockSnapshot Empty { get; } = new(false, false, false, false);

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("resolve_enabled", ResolveEnabled),
            ("clear_skill_enabled", ClearSkillEnabled),
            ("prev_variant_enabled", PrevVariantEnabled),
            ("next_variant_enabled", NextVariantEnabled)
        );
}

internal sealed class BattleHudEquipmentSlotSnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<string> _occupiedSlotIds;
    private readonly ReadOnlyCollection<string> _occupiedSlotLabels;

    internal BattleHudEquipmentSlotSnapshot(
        string slotId,
        string slotLabel,
        bool isFilled,
        bool isEntrySlot,
        string entrySlotId,
        string itemId,
        string itemDisplayName,
        string instanceId,
        IEnumerable<string> occupiedSlotIds,
        IEnumerable<string> occupiedSlotLabels,
        bool canUnequip,
        string disabledReason
    )
    {
        SlotId = slotId ?? "";
        SlotLabel = slotLabel ?? "";
        IsFilled = isFilled;
        IsEntrySlot = isEntrySlot;
        EntrySlotId = entrySlotId ?? "";
        ItemId = itemId ?? "";
        ItemDisplayName = itemDisplayName ?? "";
        InstanceId = instanceId ?? "";
        _occupiedSlotIds = new List<string>(occupiedSlotIds ?? Array.Empty<string>()).AsReadOnly();
        _occupiedSlotLabels = new List<string>(
            occupiedSlotLabels ?? Array.Empty<string>()
        ).AsReadOnly();
        CanUnequip = canUnequip;
        DisabledReason = disabledReason ?? "";
    }

    internal string SlotId { get; }
    internal string SlotLabel { get; }
    internal bool IsFilled { get; }
    internal bool IsEntrySlot { get; }
    internal string EntrySlotId { get; }
    internal string ItemId { get; }
    internal string ItemDisplayName { get; }
    internal string InstanceId { get; }
    internal IReadOnlyList<string> OccupiedSlotIds => _occupiedSlotIds;
    internal IReadOnlyList<string> OccupiedSlotLabels => _occupiedSlotLabels;
    internal bool CanUnequip { get; }
    internal string DisabledReason { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("slot_id", SlotId),
            ("slot_label", SlotLabel),
            ("is_filled", IsFilled),
            ("is_entry_slot", IsEntrySlot),
            ("entry_slot_id", EntrySlotId),
            ("item_id", ItemId),
            ("item_display_name", ItemDisplayName),
            ("instance_id", InstanceId),
            ("occupied_slot_ids", _occupiedSlotIds),
            ("occupied_slot_labels", _occupiedSlotLabels),
            ("can_unequip", CanUnequip),
            ("disabled_reason", DisabledReason)
        );
}

internal sealed class BattleHudBackpackEntrySnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<string> _allowedSlotIds;
    private readonly ReadOnlyCollection<string> _allowedSlotLabels;
    private readonly ReadOnlyCollection<string> _occupiedSlotIdsByDefault;

    internal BattleHudBackpackEntrySnapshot(
        string instanceId,
        string itemId,
        string displayName,
        string description,
        string icon,
        IEnumerable<string> allowedSlotIds,
        IEnumerable<string> allowedSlotLabels,
        string defaultSlotId,
        IEnumerable<string> occupiedSlotIdsByDefault,
        bool canEquip,
        string disabledReason
    )
    {
        InstanceId = instanceId ?? "";
        ItemId = itemId ?? "";
        DisplayName = displayName ?? "";
        Description = description ?? "";
        Icon = icon ?? "";
        _allowedSlotIds = new List<string>(allowedSlotIds ?? Array.Empty<string>()).AsReadOnly();
        _allowedSlotLabels = new List<string>(
            allowedSlotLabels ?? Array.Empty<string>()
        ).AsReadOnly();
        DefaultSlotId = defaultSlotId ?? "";
        _occupiedSlotIdsByDefault = new List<string>(
            occupiedSlotIdsByDefault ?? Array.Empty<string>()
        ).AsReadOnly();
        CanEquip = canEquip;
        DisabledReason = disabledReason ?? "";
    }

    internal string InstanceId { get; }
    internal string ItemId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal string Icon { get; }
    internal IReadOnlyList<string> AllowedSlotIds => _allowedSlotIds;
    internal IReadOnlyList<string> AllowedSlotLabels => _allowedSlotLabels;
    internal string DefaultSlotId { get; }
    internal IReadOnlyList<string> OccupiedSlotIdsByDefault => _occupiedSlotIdsByDefault;
    internal bool CanEquip { get; }
    internal string DisabledReason { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("instance_id", InstanceId),
            ("item_id", ItemId),
            ("display_name", DisplayName),
            ("description", Description),
            ("icon", Icon),
            ("allowed_slot_ids", _allowedSlotIds),
            ("allowed_slot_labels", _allowedSlotLabels),
            ("default_slot_id", DefaultSlotId),
            ("occupied_slot_ids_by_default", _occupiedSlotIdsByDefault),
            ("can_equip", CanEquip),
            ("disabled_reason", DisabledReason)
        );
}

internal sealed class BattleHudEquipmentPanelSnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<BattleHudEquipmentSlotSnapshot> _slots;
    private readonly ReadOnlyCollection<BattleHudBackpackEntrySnapshot> _backpackEntries;

    internal BattleHudEquipmentPanelSnapshot(
        string title,
        string meta,
        string activeUnitId,
        string activeUnitName,
        int apCost,
        bool canChangeEquipment,
        string disabledReason,
        IEnumerable<BattleHudEquipmentSlotSnapshot> slots,
        IEnumerable<BattleHudBackpackEntrySnapshot> backpackEntries,
        string summaryText
    )
    {
        Title = title ?? "";
        Meta = meta ?? "";
        ActiveUnitId = activeUnitId ?? "";
        ActiveUnitName = activeUnitName ?? "";
        ApCost = apCost;
        CanChangeEquipment = canChangeEquipment;
        DisabledReason = disabledReason ?? "";
        _slots = new List<BattleHudEquipmentSlotSnapshot>(
            slots ?? Array.Empty<BattleHudEquipmentSlotSnapshot>()
        ).AsReadOnly();
        _backpackEntries = new List<BattleHudBackpackEntrySnapshot>(
            backpackEntries ?? Array.Empty<BattleHudBackpackEntrySnapshot>()
        ).AsReadOnly();
        SummaryText = summaryText ?? "";
    }

    internal string Title { get; }
    internal string Meta { get; }
    internal string ActiveUnitId { get; }
    internal string ActiveUnitName { get; }
    internal int ApCost { get; }
    internal bool CanChangeEquipment { get; }
    internal string DisabledReason { get; }
    internal IReadOnlyList<BattleHudEquipmentSlotSnapshot> Slots => _slots;
    internal IReadOnlyList<BattleHudBackpackEntrySnapshot> BackpackEntries => _backpackEntries;
    internal string SummaryText { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("title", Title),
            ("meta", Meta),
            ("active_unit_id", ActiveUnitId),
            ("active_unit_name", ActiveUnitName),
            ("ap_cost", ApCost),
            ("can_change_equipment", CanChangeEquipment),
            ("disabled_reason", DisabledReason),
            ("slots", _slots),
            ("backpack_entries", _backpackEntries),
            ("summary_text", SummaryText)
        );
}

internal sealed class BattleHudBarrierSnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<string> _brokenLayerNames;

    internal BattleHudBarrierSnapshot(
        string barrierInstanceId,
        string profileId,
        string displayName,
        string sourceUnitId,
        string sourceSkillId,
        Vector2I anchorCoord,
        int radiusCells,
        string areaPattern,
        int remainingTu,
        string currentLayerId,
        string currentLayerName,
        int activeLayerCount,
        int brokenLayerCount,
        int totalLayerCount,
        IEnumerable<string> brokenLayerNames,
        string summaryText
    )
    {
        BarrierInstanceId = barrierInstanceId ?? "";
        ProfileId = profileId ?? "";
        DisplayName = displayName ?? "";
        SourceUnitId = sourceUnitId ?? "";
        SourceSkillId = sourceSkillId ?? "";
        AnchorCoord = anchorCoord;
        RadiusCells = radiusCells;
        AreaPattern = areaPattern ?? "";
        RemainingTu = remainingTu;
        CurrentLayerId = currentLayerId ?? "";
        CurrentLayerName = currentLayerName ?? "";
        ActiveLayerCount = activeLayerCount;
        BrokenLayerCount = brokenLayerCount;
        TotalLayerCount = totalLayerCount;
        _brokenLayerNames = new List<string>(
            brokenLayerNames ?? Array.Empty<string>()
        ).AsReadOnly();
        SummaryText = summaryText ?? "";
    }

    internal string BarrierInstanceId { get; }
    internal string ProfileId { get; }
    internal string DisplayName { get; }
    internal string SourceUnitId { get; }
    internal string SourceSkillId { get; }
    internal Vector2I AnchorCoord { get; }
    internal int RadiusCells { get; }
    internal string AreaPattern { get; }
    internal int RemainingTu { get; }
    internal string CurrentLayerId { get; }
    internal string CurrentLayerName { get; }
    internal int ActiveLayerCount { get; }
    internal int BrokenLayerCount { get; }
    internal int TotalLayerCount { get; }
    internal IReadOnlyList<string> BrokenLayerNames => _brokenLayerNames;
    internal string SummaryText { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("barrier_instance_id", BarrierInstanceId),
            ("profile_id", ProfileId),
            ("display_name", DisplayName),
            ("source_unit_id", SourceUnitId),
            ("source_skill_id", SourceSkillId),
            ("anchor_coord", AnchorCoord),
            ("radius_cells", RadiusCells),
            ("area_pattern", AreaPattern),
            ("remaining_tu", RemainingTu),
            ("current_layer_id", CurrentLayerId),
            ("current_layer_name", CurrentLayerName),
            ("active_layer_count", ActiveLayerCount),
            ("broken_layer_count", BrokenLayerCount),
            ("total_layer_count", TotalLayerCount),
            ("broken_layer_names", _brokenLayerNames),
            ("summary_text", SummaryText)
        );
}

internal sealed class BattleHudObjectiveProgressSnapshot
    : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<string> _requiredUnitIds;
    private readonly ReadOnlyCollection<string> _aliveRequiredUnitIds;
    private readonly ReadOnlyCollection<string> _reachedExitUnitIds;
    private readonly ReadOnlyCollection<Vector2I> _exitCoords;
    private readonly ReadOnlyCollection<BattleHudObjectiveNodeSnapshot>
        _operationNodes;

    internal static BattleHudObjectiveProgressSnapshot Empty { get; } =
        new(BattleObjectiveProgressSnapshot.Empty);

    internal BattleHudObjectiveProgressSnapshot(
        BattleObjectiveProgressSnapshot progress
    )
    {
        progress ??= BattleObjectiveProgressSnapshot.Empty;
        Mode = progress.Mode;
        ModeId = BattleObjectiveRuntimeCodec.ToWireValue(progress.Mode);
        Title = BuildTitle(progress.Mode);
        ProgressText = BuildProgressText(progress);
        TargetActorId = progress.TargetActorId.ToString();
        TargetUnitId = progress.TargetUnitId.ToString();
        TargetDisplayName = progress.TargetDisplayName;
        TargetAlive = progress.TargetAlive;
        TargetSecured = progress.TargetSecured;
        TargetReachedExit = progress.TargetReachedExit;
        ExitZoneId = progress.ExitZoneId.ToString();
        ExitEdge = progress.ExitEdgeWireValue;
        ExitDepth = progress.ExitDepth;
        _requiredUnitIds = CopyStringNames(progress.RequiredUnitIds);
        _aliveRequiredUnitIds = CopyStringNames(progress.AliveRequiredUnitIds);
        _reachedExitUnitIds = CopyStringNames(progress.ReachedExitUnitIds);
        _exitCoords = new List<Vector2I>(progress.ExitCoords).AsReadOnly();
        var operationNodes = new List<BattleHudObjectiveNodeSnapshot>();
        foreach (BattleObjectiveNodeProgressSnapshot node in progress.OperationNodes)
            operationNodes.Add(new BattleHudObjectiveNodeSnapshot(node));
        _operationNodes = operationNodes.AsReadOnly();
        EnemyUnitCount = progress.EnemyUnitCount;
        AliveEnemyUnitCount = progress.AliveEnemyUnitCount;
        CurrentTu = progress.CurrentTu;
        StartTu = progress.StartTu;
        DeadlineTu = progress.DeadlineTu;
        RemainingTu = progress.RemainingTu;
    }

    internal bool IsValid => Mode != BattleObjectiveMode.Unknown;
    internal BattleObjectiveMode Mode { get; }
    internal string ModeId { get; } = "";
    internal string Title { get; } = "";
    internal string ProgressText { get; } = "";
    internal string TargetActorId { get; } = "";
    internal string TargetUnitId { get; } = "";
    internal string TargetDisplayName { get; } = "";
    internal bool TargetAlive { get; }
    internal bool TargetSecured { get; }
    internal bool TargetReachedExit { get; }
    internal string ExitZoneId { get; } = "";
    internal string ExitEdge { get; } = "unknown";
    internal int ExitDepth { get; }
    internal IReadOnlyList<string> RequiredUnitIds => _requiredUnitIds;
    internal IReadOnlyList<string> AliveRequiredUnitIds => _aliveRequiredUnitIds;
    internal IReadOnlyList<string> ReachedExitUnitIds => _reachedExitUnitIds;
    internal IReadOnlyList<Vector2I> ExitCoords => _exitCoords;
    internal int RequiredUnitCount => _requiredUnitIds.Count;
    internal int AliveRequiredUnitCount => _aliveRequiredUnitIds.Count;
    internal int ReachedExitUnitCount => _reachedExitUnitIds.Count;
    internal int EnemyUnitCount { get; }
    internal int AliveEnemyUnitCount { get; }
    internal int CurrentTu { get; }
    internal int StartTu { get; }
    internal int DeadlineTu { get; }
    internal int RemainingTu { get; }
    internal IReadOnlyList<BattleHudObjectiveNodeSnapshot> OperationNodes =>
        _operationNodes;
    internal int OperationNodeCount => _operationNodes.Count;
    internal int CompletedOperationNodeCount
    {
        get
        {
            int count = 0;
            foreach (BattleHudObjectiveNodeSnapshot node in _operationNodes)
            {
                if (node.IsCompleted)
                    count++;
            }
            return count;
        }
    }
    internal int IncompleteOperationNodeCount =>
        OperationNodeCount - CompletedOperationNodeCount;

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("mode", ModeId),
            ("title", Title),
            ("progress_text", ProgressText),
            ("target_actor_id", TargetActorId),
            ("target_unit_id", TargetUnitId),
            ("target_display_name", TargetDisplayName),
            ("target_alive", TargetAlive),
            ("target_secured", TargetSecured),
            ("target_reached_exit", TargetReachedExit),
            ("required_unit_ids", _requiredUnitIds),
            ("alive_required_unit_ids", _aliveRequiredUnitIds),
            ("reached_exit_unit_ids", _reachedExitUnitIds),
            ("required_unit_count", RequiredUnitCount),
            ("alive_required_unit_count", AliveRequiredUnitCount),
            ("reached_exit_unit_count", ReachedExitUnitCount),
            ("exit_zone_id", ExitZoneId),
            ("exit_edge", ExitEdge),
            ("exit_depth", ExitDepth),
            ("exit_coords", _exitCoords),
            ("current_tu", CurrentTu),
            ("start_tu", StartTu),
            ("deadline_tu", DeadlineTu),
            ("remaining_tu", RemainingTu),
            ("enemy_unit_count", EnemyUnitCount),
            ("alive_enemy_unit_count", AliveEnemyUnitCount),
            ("operation_nodes", _operationNodes),
            ("operation_node_count", OperationNodeCount),
            ("completed_operation_node_count", CompletedOperationNodeCount),
            ("incomplete_operation_node_count", IncompleteOperationNodeCount)
        );

    private static string BuildTitle(BattleObjectiveMode mode) =>
        mode switch
        {
            BattleObjectiveMode.Elimination => "歼灭敌军",
            BattleObjectiveMode.Boss => "击败首领",
            BattleObjectiveMode.Rescue => "拯救目标",
            BattleObjectiveMode.Escape => "逃离战场",
            BattleObjectiveMode.Escort => "护送目标",
            BattleObjectiveMode.Defense => "坚守防线",
            BattleObjectiveMode.Intercept => "截击目标",
            BattleObjectiveMode.NodeOperation => "节点作业",
            _ => "",
        };

    private static string BuildProgressText(
        BattleObjectiveProgressSnapshot progress
    )
    {
        return progress.Mode switch
        {
            BattleObjectiveMode.Elimination =>
                $"敌军存活 {progress.AliveEnemyUnitCount}/{progress.EnemyUnitCount}",
            BattleObjectiveMode.Boss =>
                $"{ResolveTargetName(progress)}：{(progress.TargetAlive ? "存活" : "已击败")}"
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            BattleObjectiveMode.Rescue =>
                $"{ResolveTargetName(progress)}："
                + (
                    !progress.TargetAlive
                        ? "已倒下"
                        : progress.TargetSecured
                            ? "已获救"
                            : "等待相邻交互"
                )
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            BattleObjectiveMode.Escape =>
                $"{FormatExitEdge(progress.ExitEdge)}出口（纵深 {progress.ExitDepth} 格）"
                + $" · 已到达 {progress.ReachedExitUnitCount}/{progress.RequiredUnitCount}"
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            BattleObjectiveMode.Escort =>
                $"{ResolveTargetName(progress)}："
                + (
                    !progress.TargetAlive
                        ? "已倒下"
                        : progress.TargetReachedExit
                            ? "已抵达出口"
                            : "护送中"
                )
                + $" · {FormatExitEdge(progress.ExitEdge)}出口（纵深 {progress.ExitDepth} 格）"
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            BattleObjectiveMode.Defense =>
                $"{ResolveTargetName(progress)}："
                + (
                    !progress.TargetAlive
                        ? "已倒下"
                        : progress.RemainingTu <= 0
                            ? "已守住"
                            : "坚守中"
                )
                + $" · 剩余 {progress.RemainingTu} TU"
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            BattleObjectiveMode.Intercept =>
                $"{ResolveTargetName(progress)}："
                + (
                    !progress.TargetAlive
                        ? "已截停"
                        : progress.TargetReachedExit
                            ? "已逃脱"
                            : "突围中"
                )
                + $" · {FormatExitEdge(progress.ExitEdge)}逃脱区（纵深 {progress.ExitDepth} 格）"
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            BattleObjectiveMode.NodeOperation =>
                $"已完成 {progress.CompletedOperationNodeCount}/{progress.OperationNodeCount}"
                + $" · 队伍存活 {progress.AliveRequiredUnitCount}/{progress.RequiredUnitCount}",
            _ => "",
        };
    }

    private static string ResolveTargetName(
        BattleObjectiveProgressSnapshot progress
    )
    {
        if (!string.IsNullOrWhiteSpace(progress.TargetDisplayName))
            return progress.TargetDisplayName;
        if (progress.TargetUnitId != "")
            return progress.TargetUnitId.ToString();
        return "首领";
    }

    private static string FormatExitEdge(BattleMapEdge edge) =>
        edge switch
        {
            BattleMapEdge.Left => "左侧",
            BattleMapEdge.Right => "右侧",
            BattleMapEdge.Top => "上方",
            BattleMapEdge.Bottom => "下方",
            _ => "未知",
        };

    private static ReadOnlyCollection<string> CopyStringNames(
        IEnumerable<StringName> source
    )
    {
        var result = new List<string>();
        foreach (StringName value in source ?? Array.Empty<StringName>())
            result.Add(value.ToString());
        return result.AsReadOnly();
    }
}

internal sealed class BattleHudObjectiveNodeSnapshot
    : IBattlePresentationSnapshotValue
{
    internal BattleHudObjectiveNodeSnapshot(
        BattleObjectiveNodeProgressSnapshot node
    )
    {
        NodeId = node?.NodeId.ToString() ?? "";
        DisplayName = node?.DisplayName ?? "";
        ZoneId = node?.ZoneId.ToString() ?? "";
        Coord = node?.Coord ?? default;
        IsCompleted = node?.IsCompleted == true;
    }

    internal string NodeId { get; }
    internal string DisplayName { get; }
    internal string ZoneId { get; }
    internal Vector2I Coord { get; }
    internal bool IsCompleted { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("node_id", NodeId),
            ("display_name", DisplayName),
            ("zone_id", ZoneId),
            ("coord", Coord),
            ("is_completed", IsCompleted)
        );
}

internal sealed class BattleHudSnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<BattleHudQueueEntrySnapshot> _queueEntries;
    private readonly ReadOnlyCollection<BattleHudSkillSlotSnapshot> _skillSlots;
    private readonly ReadOnlyCollection<int> _selectedSkillHitStageRates;
    private readonly ReadOnlyCollection<BattleHudFateBadgeSnapshot> _selectedSkillFateBadges;
    private readonly ReadOnlyCollection<string> _recentBattleLogLines;
    private readonly ReadOnlyCollection<BattleHudBarrierSnapshot> _barriers;
    private readonly bool _isEmpty;

    internal static BattleHudSnapshot Empty { get; } = new();

    private BattleHudSnapshot()
    {
        _isEmpty = true;
        _queueEntries = new List<BattleHudQueueEntrySnapshot>().AsReadOnly();
        _skillSlots = new List<BattleHudSkillSlotSnapshot>().AsReadOnly();
        _selectedSkillHitStageRates = new List<int>().AsReadOnly();
        _selectedSkillFateBadges = new List<BattleHudFateBadgeSnapshot>().AsReadOnly();
        _recentBattleLogLines = new List<string>().AsReadOnly();
        _barriers = new List<BattleHudBarrierSnapshot>().AsReadOnly();
        RoundBadge = new BattleHudRoundBadgeSnapshot("TU --", "READY 0");
        HitPreviewPayload = BattlePresentationPayload.Empty;
        SaveBranchPreviewPayload = BattlePresentationPayload.Empty;
        CommandDock = BattleHudCommandDockSnapshot.Empty;
        EquipmentPanel = new BattleHudEquipmentPanelSnapshot(
            "", "", "", "", 0, false, "", null, null, ""
        );
    }

    internal BattleHudSnapshot(
        string headerTitle,
        string headerSubtitle,
        BattleHudRoundBadgeSnapshot roundBadge,
        string modeText,
        IEnumerable<BattleHudQueueEntrySnapshot> queueEntries,
        BattleHudFocusUnitSnapshot focusUnit,
        string skillTitle,
        string selectedSkillVariantName,
        string skillSubtitle,
        IEnumerable<BattleHudSkillSlotSnapshot> skillSlots,
        string tileText,
        string selectedSkillHitPreviewText,
        BattlePresentationPayload hitPreviewPayload,
        string selectedSkillHitBadgeText,
        IEnumerable<int> selectedSkillHitStageRates,
        string selectedSkillDamagePreviewText,
        int selectedSkillDamageMin,
        int selectedSkillDamageMax,
        BattlePresentationPayload saveBranchPreviewPayload,
        string selectedSkillSaveBranchPreviewText,
        string selectedSkillFatePreviewText,
        IEnumerable<BattleHudFateBadgeSnapshot> selectedSkillFateBadges,
        string selectedSkillPreviewTooltipText,
        string selectedSkillTargetSelectionMode,
        int selectedSkillTargetMinCount,
        int selectedSkillTargetMaxCount,
        int selectedSkillTargetCount,
        bool selectedSkillConfirmReady,
        bool selectedSkillAutoCastReady,
        BattleHudCommandDockSnapshot commandDock,
        string hintText,
        IEnumerable<string> recentBattleLogLines,
        BattleHudEquipmentPanelSnapshot equipmentPanel,
        IEnumerable<BattleHudBarrierSnapshot> barriers = null,
        string barrierSummaryText = "",
        BattleHudObjectiveProgressSnapshot objectiveProgress = null
    )
    {
        HeaderTitle = headerTitle ?? "";
        HeaderSubtitle = headerSubtitle ?? "";
        RoundBadge = roundBadge ?? new BattleHudRoundBadgeSnapshot("TU --", "READY 0");
        ModeText = modeText ?? "";
        _queueEntries = new List<BattleHudQueueEntrySnapshot>(
            queueEntries ?? Array.Empty<BattleHudQueueEntrySnapshot>()
        ).AsReadOnly();
        FocusUnit = focusUnit;
        SkillTitle = skillTitle ?? "";
        SelectedSkillVariantName = selectedSkillVariantName ?? "";
        SkillSubtitle = skillSubtitle ?? "";
        _skillSlots = new List<BattleHudSkillSlotSnapshot>(
            skillSlots ?? Array.Empty<BattleHudSkillSlotSnapshot>()
        ).AsReadOnly();
        TileText = tileText ?? "";
        SelectedSkillHitPreviewText = selectedSkillHitPreviewText ?? "";
        HitPreviewPayload = hitPreviewPayload ?? BattlePresentationPayload.Empty;
        SelectedSkillHitBadgeText = selectedSkillHitBadgeText ?? "";
        _selectedSkillHitStageRates = new List<int>(
            selectedSkillHitStageRates ?? Array.Empty<int>()
        ).AsReadOnly();
        SelectedSkillDamagePreviewText = selectedSkillDamagePreviewText ?? "";
        SelectedSkillDamageMin = selectedSkillDamageMin;
        SelectedSkillDamageMax = selectedSkillDamageMax;
        SaveBranchPreviewPayload =
            saveBranchPreviewPayload ?? BattlePresentationPayload.Empty;
        SelectedSkillSaveBranchPreviewText = selectedSkillSaveBranchPreviewText ?? "";
        SelectedSkillFatePreviewText = selectedSkillFatePreviewText ?? "";
        _selectedSkillFateBadges = new List<BattleHudFateBadgeSnapshot>(
            selectedSkillFateBadges ?? Array.Empty<BattleHudFateBadgeSnapshot>()
        ).AsReadOnly();
        SelectedSkillPreviewTooltipText = selectedSkillPreviewTooltipText ?? "";
        SelectedSkillTargetSelectionMode = selectedSkillTargetSelectionMode ?? "";
        SelectedSkillTargetMinCount = selectedSkillTargetMinCount;
        SelectedSkillTargetMaxCount = selectedSkillTargetMaxCount;
        SelectedSkillTargetCount = selectedSkillTargetCount;
        SelectedSkillConfirmReady = selectedSkillConfirmReady;
        SelectedSkillAutoCastReady = selectedSkillAutoCastReady;
        CommandDock = commandDock ?? BattleHudCommandDockSnapshot.Empty;
        HintText = hintText ?? "";
        _recentBattleLogLines = new List<string>(
            recentBattleLogLines ?? Array.Empty<string>()
        ).AsReadOnly();
        EquipmentPanel = equipmentPanel;
        _barriers = new List<BattleHudBarrierSnapshot>(
            barriers ?? Array.Empty<BattleHudBarrierSnapshot>()
        ).AsReadOnly();
        BarrierSummaryText = barrierSummaryText ?? "";
        ObjectiveProgress =
            objectiveProgress ?? BattleHudObjectiveProgressSnapshot.Empty;
    }

    internal bool IsEmpty => _isEmpty;
    internal string HeaderTitle { get; } = "";
    internal string HeaderSubtitle { get; } = "";
    internal BattleHudRoundBadgeSnapshot RoundBadge { get; }
    internal string ModeText { get; } = "";
    internal IReadOnlyList<BattleHudQueueEntrySnapshot> QueueEntries => _queueEntries;
    internal BattleHudFocusUnitSnapshot FocusUnit { get; }
    internal string SkillTitle { get; } = "";
    internal string SelectedSkillVariantName { get; } = "";
    internal string SkillSubtitle { get; } = "";
    internal IReadOnlyList<BattleHudSkillSlotSnapshot> SkillSlots => _skillSlots;
    internal string TileText { get; } = "";
    internal string SelectedSkillHitPreviewText { get; } = "";
    internal BattlePresentationPayload HitPreviewPayload { get; }
    internal string SelectedSkillHitBadgeText { get; } = "";
    internal IReadOnlyList<int> SelectedSkillHitStageRates => _selectedSkillHitStageRates;
    internal string SelectedSkillDamagePreviewText { get; } = "";
    internal int SelectedSkillDamageMin { get; }
    internal int SelectedSkillDamageMax { get; }
    internal BattlePresentationPayload SaveBranchPreviewPayload { get; }
    internal string SelectedSkillSaveBranchPreviewText { get; } = "";
    internal string SelectedSkillFatePreviewText { get; } = "";
    internal IReadOnlyList<BattleHudFateBadgeSnapshot> SelectedSkillFateBadges =>
        _selectedSkillFateBadges;
    internal string SelectedSkillPreviewTooltipText { get; } = "";
    internal string SelectedSkillTargetSelectionMode { get; } = "";
    internal int SelectedSkillTargetMinCount { get; }
    internal int SelectedSkillTargetMaxCount { get; }
    internal int SelectedSkillTargetCount { get; }
    internal bool SelectedSkillConfirmReady { get; }
    internal bool SelectedSkillAutoCastReady { get; }
    internal BattleHudCommandDockSnapshot CommandDock { get; }
    internal string HintText { get; } = "";
    internal IReadOnlyList<string> RecentBattleLogLines => _recentBattleLogLines;
    internal BattleHudEquipmentPanelSnapshot EquipmentPanel { get; }
    internal IReadOnlyList<BattleHudBarrierSnapshot> Barriers => _barriers;
    internal string BarrierSummaryText { get; } = "";
    internal BattleHudObjectiveProgressSnapshot ObjectiveProgress { get; } =
        BattleHudObjectiveProgressSnapshot.Empty;

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        _isEmpty
            ? BattlePresentationSnapshotFacts.Map()
            : BattlePresentationSnapshotFacts.Map(
                ("header_title", HeaderTitle),
                ("header_subtitle", HeaderSubtitle),
                ("objective_progress", ObjectiveProgress),
                ("round_badge", RoundBadge),
                ("mode_text", ModeText),
                ("queue_entries", _queueEntries),
                ("focus_unit", FocusUnit),
                ("skill_title", SkillTitle),
                ("selected_skill_variant_name", SelectedSkillVariantName),
                ("skill_subtitle", SkillSubtitle),
                ("skill_slots", _skillSlots),
                ("tile_text", TileText),
                ("selected_skill_hit_preview_text", SelectedSkillHitPreviewText),
                ("selected_skill_hit_preview_payload", HitPreviewPayload),
                ("selected_skill_hit_badge_text", SelectedSkillHitBadgeText),
                ("selected_skill_hit_stage_rates", _selectedSkillHitStageRates),
                ("selected_skill_damage_preview_text", SelectedSkillDamagePreviewText),
                ("selected_skill_damage_min", SelectedSkillDamageMin),
                ("selected_skill_damage_max", SelectedSkillDamageMax),
                ("selected_skill_save_branch_preview_payload", SaveBranchPreviewPayload),
                ("selected_skill_save_branch_preview_text", SelectedSkillSaveBranchPreviewText),
                ("selected_skill_fate_preview_text", SelectedSkillFatePreviewText),
                ("selected_skill_fate_badges", _selectedSkillFateBadges),
                ("selected_skill_preview_tooltip_text", SelectedSkillPreviewTooltipText),
                ("selected_skill_target_selection_mode", SelectedSkillTargetSelectionMode),
                ("selected_skill_target_min_count", SelectedSkillTargetMinCount),
                ("selected_skill_target_max_count", SelectedSkillTargetMaxCount),
                ("selected_skill_target_count", SelectedSkillTargetCount),
                ("selected_skill_confirm_ready", SelectedSkillConfirmReady),
                ("selected_skill_auto_cast_ready", SelectedSkillAutoCastReady),
                ("command_dock", CommandDock),
                ("hint_text", HintText),
                ("recent_battle_log_lines", _recentBattleLogLines),
                ("equipment_panel", EquipmentPanel),
                ("barriers", _barriers),
                ("barrier_summary_text", BarrierSummaryText)
            );

    internal GodotProjectionLease<GDictionary> BuildLease() =>
        BattlePresentationSnapshotProjection.BuildLease(
            this,
            "battle-hud-snapshot",
            "BattleHudSnapshot.root"
        );
}

internal static class BattlePresentationSnapshotFacts
{
    internal static IReadOnlyDictionary<string, object> Map(
        params (string Key, object Value)[] entries
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string key, object value) in entries ?? Array.Empty<(string, object)>())
        {
            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }
        return new ReadOnlyDictionary<string, object>(result);
    }
}

internal static class BattlePresentationSnapshotFreeze
{
    internal static ReadOnlyDictionary<string, object> Dictionary(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (KeyValuePair<string, object> entry in source)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                    result[entry.Key] = Value(entry.Value);
            }
        }
        return new ReadOnlyDictionary<string, object>(result);
    }

    private static object Value(object value)
    {
        if (value == null)
            return null;
        if (
            value is string
            || value is StringName
            || value is bool
            || value is int
            || value is long
            || value is float
            || value is double
            || value is Vector2I
            || value is Vector2
            || value is Color
            || value is IBattlePresentationSnapshotValue
        )
        {
            return value;
        }
        if (value is IReadOnlyDictionary<string, object> dictionary)
            return new BattlePresentationPayload(dictionary);
        if (value is IEnumerable values && value is not string)
        {
            var result = new List<object>();
            foreach (object entry in values)
                result.Add(Value(entry));
            return result.AsReadOnly();
        }
        throw new InvalidOperationException(
            $"Unsupported battle presentation snapshot value: {value.GetType().FullName}."
        );
    }
}

internal static class BattlePresentationSnapshotProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(
        IBattlePresentationSnapshotValue snapshot,
        string ownerId,
        string reason
    )
    {
        var root = new GDictionary();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                ownerId,
                LifetimeDomain.Request,
                reason
            );
        try
        {
            WriteDictionary(lease, root, snapshot?.CanonicalFacts, reason);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static void WriteDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        IReadOnlyDictionary<string, object> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (values == null)
            return;
        foreach (KeyValuePair<string, object> entry in values)
            target[entry.Key] = WriteValue(lease, entry.Value, $"{reason}.{entry.Key}");
    }

    private static Variant WriteValue<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        object value,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return value switch
        {
            null => default,
            string text => Variant.From(text),
            StringName name => Variant.From(name),
            bool flag => Variant.From(flag),
            int number => Variant.From(number),
            long number => Variant.From(number),
            float number => Variant.From(number),
            double number => Variant.From(number),
            Vector2I coord => Variant.From(coord),
            Vector2 coord => Variant.From(coord),
            Color color => Variant.From(color),
            IBattlePresentationSnapshotValue snapshot => Variant.From(
                WriteOwnedDictionary(lease, snapshot.CanonicalFacts, reason)
            ),
            IReadOnlyDictionary<string, object> dictionary => Variant.From(
                WriteOwnedDictionary(lease, dictionary, reason)
            ),
            IEnumerable values => Variant.From(WriteOwnedArray(lease, values, reason)),
            _ => throw new InvalidOperationException(
                $"Unsupported battle presentation projection value: {value.GetType().FullName} ({reason})."
            ),
        };
    }

    private static GDictionary WriteOwnedDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, object> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        WriteDictionary(lease, result, values, reason);
        return result;
    }

    private static GArray WriteOwnedArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (values == null)
            return result;
        int index = 0;
        foreach (object value in values)
        {
            result.Add(WriteValue(lease, value, $"{reason}[{index}]"));
            index++;
        }
        return result;
    }
}
