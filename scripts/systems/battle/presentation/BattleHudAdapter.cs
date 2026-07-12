using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleHudAdapter : IDisposable
{
    private const int QUEUE_ENTRY_LIMIT = 7;
    private const int SKILL_GRID_SIZE = 20;
    private const int RECENT_BATTLE_LOG_LINE_LIMIT = 3;
    private const int CHANGE_EQUIPMENT_AP_COST = 2;
    private const string EquipmentPreviewDefaultFailureMessage = "该实例当前不能装备。";

    private static readonly StringName TARGET_SELECTION_MULTI_UNIT = "multi_unit";

    private readonly HashSet<StringName> _queueReadyLookup = new();
    private string _equipmentPreviewCacheSignature = "";
    private readonly Dictionary<string, EquipmentPreviewRule> _equipmentPreviewCache = new();
    private GameRuntimeFacade _runtime;
    private GameSession _gameSession;

    private sealed class EquipmentPreviewRule
    {
        public bool allowed;
        public StringName slot_id = "";
        public string message = "";
    }

    private readonly record struct SelectionInfo(
        StringName SelectionMode,
        bool IsMultiUnit,
        int MinTargetCount,
        int MaxTargetCount,
        bool ConfirmReady,
        bool AutoCastReady
    );

    private readonly record struct DamagePreviewSummary(
        bool HasDamage,
        int MinDamage,
        int MaxDamage,
        string SummaryText
    )
    {
        internal static DamagePreviewSummary Empty => new(false, 0, 0, "");
    }

    private sealed record FatePreviewFacts(
        string SummaryText,
        string TooltipText,
        IReadOnlyList<BattleHudFateBadgeSnapshot> Badges
    )
    {
        internal static FatePreviewFacts Empty { get; } =
            new("", "", Array.Empty<BattleHudFateBadgeSnapshot>());
    }

    private readonly record struct PortraitData(
        string PortraitKey,
        string Glyph,
        Color PrimaryColor,
        Color SecondaryColor,
        Color EdgeColor
    );

    private readonly record struct PortraitPalette(
        Color PrimaryColor,
        Color SecondaryColor,
        Color EdgeColor
    );

    private readonly record struct SkillSlotState(
        string FooterText,
        bool IsDisabled,
        int Cooldown,
        string DisabledReason
    );

    public static string EQUIPMENT_PREVIEW_DEFAULT_FAILURE_MESSAGE() =>
        EquipmentPreviewDefaultFailureMessage;

    public void Dispose()
    {
        _runtime = null;
        _gameSession = null;
        _queueReadyLookup.Clear();
        _equipmentPreviewCacheSignature = "";
        _equipmentPreviewCache.Clear();
    }

    public void SetupRuntimeContext(GameRuntimeFacade runtime, GameSession gameSession = null)
    {
        _runtime = runtime;
        _gameSession = gameSession;
    }

    internal BattleHudSnapshot BuildSnapshot(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id,
        string encounter_display_name,
        BattlePreview selected_skill_runtime_preview,
        StringName selected_skill_entry_id = default
    )
    {
        selected_skill_id = NormalizeStringName(selected_skill_id);
        selected_skill_entry_id = NormalizeStringName(selected_skill_entry_id);
        selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        if (battle_state == null)
            return BattleHudSnapshot.Empty;

        List<Vector2I> targetCoords = CloneVector2IList(selected_skill_target_coords);
        List<StringName> targetUnitIds = CloneStringNameList(selected_skill_target_unit_ids);
        BattleUnitState activeUnit = GetUnit(battle_state, battle_state.active_unit_id);
        BattleCellState selectedCell = GetCell(battle_state, selected_coord);
        BattleUnitState selectedUnit = GetUnitAtCoord(battle_state, selected_coord);
        BattleUnitState focusUnit = selectedUnit ?? activeUnit;
        int selectedTargetCount = targetCoords.Count;
        BattlePreview runtimePreview = selected_skill_runtime_preview;
        SelectionInfo selectionInfo = BuildSkillTargetSelectionInfo(
            battle_state,
            activeUnit,
            selected_skill_id,
            selectedTargetCount
        );
        AttackPreviewData hitPreview = BuildSelectedSkillHitPreview(
            runtimePreview
        );
        BattlePresentationPayload saveBranchPreview = BuildSelectedSkillSaveBranchPreview(
            runtimePreview
        );
        DamagePreviewSummary damagePreview = BuildSelectedSkillDamagePreview(
            runtimePreview
        );
        if (!saveBranchPreview.IsEmpty)
            damagePreview = DamagePreviewSummary.Empty;
        FatePreviewFacts fatePreview = BuildSelectedSkillFatePreview(
            runtimePreview
        );
        string tooltipText = BuildSelectedSkillPreviewTooltip(
            hitPreview,
            fatePreview,
            damagePreview,
            saveBranchPreview
        );
        string headerTitle = !string.IsNullOrWhiteSpace(encounter_display_name)
            ? encounter_display_name
            : "战斗地图";

        return new BattleHudSnapshot(
            headerTitle: headerTitle,
            headerSubtitle: BuildHeaderSubtitle(battle_state, activeUnit),
            roundBadge: BuildRoundBadge(battle_state),
            modeText: FormatControlMode(
                activeUnit != null ? activeUnit.control_mode : new StringName("manual")
            ),
            queueEntries: BuildQueueEntries(battle_state),
            focusUnit: BuildFocusUnitSnapshot(focusUnit, battle_state),
            skillTitle: BuildSkillTitle(selected_skill_name, selected_skill_variant_name),
            selectedSkillVariantName: selected_skill_variant_name ?? "",
            skillSubtitle: BuildSkillSubtitle(
                activeUnit,
                selected_skill_name,
                selected_skill_variant_name,
                selectedTargetCount,
                selected_skill_required_coord_count,
                selectionInfo,
                hitPreview,
                damagePreview,
                saveBranchPreview
            ),
            skillSlots: BuildSkillSlots(activeUnit, selected_skill_entry_id),
            tileText: BuildTileText(selected_coord, selectedCell, selectedUnit),
            selectedSkillHitPreviewText: hitPreview?.SummaryText ?? "",
            hitPreviewPayload: BattlePresentationPayload.FromAttackPreview(hitPreview),
            selectedSkillHitBadgeText: BuildSelectedSkillHitBadgeText(hitPreview),
            selectedSkillHitStageRates: BuildStageSuccessRates(hitPreview),
            selectedSkillDamagePreviewText: damagePreview.SummaryText,
            selectedSkillDamageMin: damagePreview.MinDamage,
            selectedSkillDamageMax: damagePreview.MaxDamage,
            saveBranchPreviewPayload: saveBranchPreview,
            selectedSkillSaveBranchPreviewText: saveBranchPreview.SummaryText,
            selectedSkillFatePreviewText: fatePreview.SummaryText,
            selectedSkillFateBadges: fatePreview.Badges,
            selectedSkillPreviewTooltipText: tooltipText,
            selectedSkillTargetSelectionMode: selectionInfo.SelectionMode.ToString(),
            selectedSkillTargetMinCount: selectionInfo.MinTargetCount,
            selectedSkillTargetMaxCount: selectionInfo.MaxTargetCount,
            selectedSkillTargetCount: selectedTargetCount,
            selectedSkillConfirmReady: selectionInfo.ConfirmReady,
            selectedSkillAutoCastReady: selectionInfo.AutoCastReady,
            commandDock: BuildCommandDock(
                battle_state,
                activeUnit,
                selected_skill_id,
                selectedTargetCount
            ),
            hintText: BuildHintText(
                battle_state,
                activeUnit,
                selected_skill_id,
                selectedTargetCount,
                selectionInfo
            ),
            recentBattleLogLines: BuildRecentBattleLogLines(battle_state),
            equipmentPanel: BuildEquipmentPanelSnapshot(battle_state, activeUnit)
        );
    }

    internal BattleHoverSnapshot BuildHoverPreview(
        BattleState battle_state,
        Vector2I hover_coord,
        StringName selected_skill_id,
        StringName selected_skill_variant_id,
        IEnumerable<Vector2I> valid_target_coords,
        BattlePreview hover_runtime_preview
    )
    {
        selected_skill_id = NormalizeStringName(selected_skill_id);
        selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        bool hasSelectedSkill = !IsEmpty(selected_skill_id);
        BattleHoverTargetUnitSnapshot targetUnit = null;
        if (battle_state == null || !battle_state.ContainsCell(hover_coord))
            return EmptyHover(hover_coord, hasSelectedSkill, targetUnit);

        BattleUnitState hoveredUnit = GetUnitAtCoord(battle_state, hover_coord);
        if (hoveredUnit != null)
            targetUnit = BuildHoverTargetUnitSnapshot(hoveredUnit, battle_state);

        if (!hasSelectedSkill)
            return EmptyHover(hover_coord, false, targetUnit);

        List<Vector2I> normalizedValid = CloneVector2IList(valid_target_coords);
        bool isValidTarget = normalizedValid.Contains(hover_coord);
        if (!isValidTarget)
            return EmptyHover(hover_coord, true, targetUnit);

        AttackPreviewData hitPreview = BuildSelectedSkillHitPreview(
            hover_runtime_preview
        );
        BattlePresentationPayload saveBranchPreview = BuildSelectedSkillSaveBranchPreview(
            hover_runtime_preview
        );
        DamagePreviewSummary damagePreview = BuildSelectedSkillDamagePreview(
            hover_runtime_preview
        );
        if (!saveBranchPreview.IsEmpty)
            damagePreview = DamagePreviewSummary.Empty;
        FatePreviewFacts fatePreview = BuildSelectedSkillFatePreview(
            hover_runtime_preview
        );

        return new BattleHoverSnapshot(
            hoverCoord: hover_coord,
            hoverIsValidTarget: true,
            hasSelectedSkill: true,
            hitPreview: BattlePresentationPayload.FromAttackPreview(hitPreview),
            hitStageRates: BuildStageSuccessRates(hitPreview),
            hitBadgeText: BuildSelectedSkillHitBadgeText(hitPreview),
            fateBadges: fatePreview.Badges,
            saveBranchPreview: saveBranchPreview,
            saveBranchPreviewText: saveBranchPreview.SummaryText,
            damageMin: damagePreview.MinDamage,
            damageMax: damagePreview.MaxDamage,
            damageText: damagePreview.SummaryText,
            targetUnit: targetUnit
        );
    }

    public string FormatSelectedSkillHitBadgeText(AttackPreviewData hit_preview)
    {
        return BuildSelectedSkillHitBadgeText(hit_preview);
    }

    private BattleHoverTargetUnitSnapshot BuildHoverTargetUnitSnapshot(
        BattleUnitState unitState,
        BattleState battleState
    )
    {
        if (unitState == null)
            return null;

        PortraitData portraitData = BuildPortraitData(unitState, battleState);
        int hpMax = GetSnapshotValue(unitState, "hp_max", Mathf.Max(unitState.current_hp, 1));
        int mpMax = GetSnapshotValue(unitState, "mp_max", Mathf.Max(unitState.current_mp, 0));
        int staminaMax = GetSnapshotValue(
            unitState,
            "stamina_max",
            Mathf.Max(unitState.current_stamina, 0)
        );
        int auraMax = GetSnapshotValue(unitState, "aura_max", Mathf.Max(unitState.current_aura, 0));
        int apMax = GetSnapshotValue(
            unitState,
            "action_points",
            Mathf.Max(unitState.current_ap, 1)
        );
        bool isEnemy =
            battleState != null && battleState.enemy_unit_ids.Contains(unitState.unit_id);
        bool isSelf = battleState != null && unitState.unit_id == battleState.active_unit_id;
        return new BattleHoverTargetUnitSnapshot(
            UnitId: unitState.unit_id,
            Name: FormatUnitName(unitState, "单位"),
            Glyph: portraitData.Glyph,
            PortraitKey: portraitData.PortraitKey,
            PrimaryColor: portraitData.PrimaryColor,
            EdgeColor: portraitData.EdgeColor,
            HpCurrent: unitState.current_hp,
            HpMax: Mathf.Max(hpMax, 1),
            MpCurrent: unitState.current_mp,
            MpMax: Mathf.Max(mpMax, 1),
            MpVisible: IsResourceUnlocked(unitState, CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            StaminaCurrent: unitState.current_stamina,
            StaminaMax: Mathf.Max(staminaMax, 1),
            AuraCurrent: unitState.current_aura,
            AuraMax: Mathf.Max(auraMax, 1),
            AuraVisible: IsResourceUnlocked(
                unitState,
                CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)
            ),
            ApCurrent: unitState.current_ap,
            ApMax: Mathf.Max(apMax, 1),
            IsEnemy: isEnemy,
            IsSelf: isSelf
        );
    }

    private static BattleHoverSnapshot EmptyHover(
        Vector2I coord,
        bool hasSelectedSkill,
        BattleHoverTargetUnitSnapshot targetUnit
    ) =>
        new(
            coord,
            false,
            hasSelectedSkill,
            BattlePresentationPayload.Empty,
            Array.Empty<int>(),
            "",
            Array.Empty<BattleHudFateBadgeSnapshot>(),
            BattlePresentationPayload.Empty,
            "",
            0,
            0,
            "",
            targetUnit
        );

    private static IReadOnlyList<int> BuildStageSuccessRates(AttackPreviewData preview)
    {
        var result = new List<int>();
        foreach (AttackPreviewStage stage in preview?.Stages ?? new List<AttackPreviewStage>())
            result.Add(stage.SuccessRatePercent);
        return result.AsReadOnly();
    }

    private string BuildHeaderSubtitle(BattleState battleState, BattleUnitState activeUnit)
    {
        return $"阶段 {FormatPhase(battleState.phase)}  |  友军 {battleState.ally_unit_ids.Count}  |  敌军 {battleState.enemy_unit_ids.Count}  |  当前 {FormatUnitName(activeUnit, "无")}";
    }

    private BattleHudRoundBadgeSnapshot BuildRoundBadge(BattleState battleState)
    {
        if (battleState.timeline == null)
            return new BattleHudRoundBadgeSnapshot("TU --", "READY 0");
        return new BattleHudRoundBadgeSnapshot(
            $"TU {battleState.timeline.current_tu}",
            $"READY {battleState.timeline.ready_unit_ids.Count}"
        );
    }

    private IReadOnlyList<BattleHudQueueEntrySnapshot> BuildQueueEntries(
        BattleState battleState
    )
    {
        var queueEntries = new List<BattleHudQueueEntrySnapshot>();
        if (battleState == null)
            return queueEntries;

        _queueReadyLookup.Clear();
        if (battleState.timeline != null)
        {
            foreach (StringName unitId in battleState.timeline.ready_unit_ids)
                _queueReadyLookup.Add(unitId);
        }

        var orderedIds = new List<StringName>();
        var seenIds = new HashSet<StringName>();
        if (IsLivingUnit(battleState, battleState.active_unit_id))
        {
            orderedIds.Add(battleState.active_unit_id);
            seenIds.Add(battleState.active_unit_id);
        }

        if (battleState.timeline != null)
        {
            foreach (StringName readyUnitId in battleState.timeline.ready_unit_ids)
            {
                if (seenIds.Contains(readyUnitId) || !IsLivingUnit(battleState, readyUnitId))
                    continue;
                orderedIds.Add(readyUnitId);
                seenIds.Add(readyUnitId);
            }
        }

        var remainingUnits = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in battleState.Units())
        {
            if (unitState == null || !unitState.is_alive || seenIds.Contains(unitState.unit_id))
                continue;
            remainingUnits.Add(unitState);
        }
        remainingUnits.Sort(CompareQueueCandidates);

        foreach (BattleUnitState unitState in remainingUnits)
            orderedIds.Add(unitState.unit_id);

        int totalEntries = Mathf.Min(orderedIds.Count, QUEUE_ENTRY_LIMIT);
        for (int index = 0; index < totalEntries; index++)
        {
            StringName unitId = orderedIds[index];
            BattleUnitState unitState = GetUnit(battleState, unitId);
            if (unitState == null)
                continue;
            PortraitData portraitData = BuildPortraitData(unitState, battleState);
            int hpMax = GetSnapshotValue(unitState, "hp_max", 1);
            queueEntries.Add(
                new BattleHudQueueEntrySnapshot
                {
                    SlotIndex = index + 1,
                    Name = FormatUnitName(unitState, "单位"),
                    Glyph = portraitData.Glyph,
                    PortraitKey = portraitData.PortraitKey,
                    PrimaryColor = portraitData.PrimaryColor,
                    SecondaryColor = portraitData.SecondaryColor,
                    EdgeColor = portraitData.EdgeColor,
                    HpRatio = GetRatio(unitState.current_hp, hpMax),
                    HpText = $"HP {unitState.current_hp}/{hpMax}",
                    ApText =
                        $"AP {unitState.current_ap} / 行动 {unitState.current_move_points}",
                    IsActive = unitId == battleState.active_unit_id,
                    IsReady = _queueReadyLookup.Contains(unitId),
                    IsEnemy = battleState.enemy_unit_ids.Contains(unitId),
                }
            );
        }

        if (orderedIds.Count > QUEUE_ENTRY_LIMIT)
        {
            queueEntries.Add(
                BattleHudQueueEntrySnapshot.Overflow(
                    $"+{orderedIds.Count - QUEUE_ENTRY_LIMIT}"
                )
            );
        }
        return queueEntries.AsReadOnly();
    }

    private BattleHudFocusUnitSnapshot BuildFocusUnitSnapshot(
        BattleUnitState unitState,
        BattleState battleState
    )
    {
        if (unitState == null)
        {
            return new BattleHudFocusUnitSnapshot(
                "待命", "未选中单位", BuildResourceInfo(null), "?", "",
                new Color(0.42f, 0.3f, 0.22f, 1.0f),
                new Color(0.16f, 0.1f, 0.07f, 1.0f),
                new Color(0.88f, 0.72f, 0.48f, 1.0f),
                0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0,
                BattleUnitState.DefaultMovePointsPerTurn
            );
        }

        PortraitData portraitData = BuildPortraitData(unitState, battleState);
        int hpMax = GetSnapshotValue(unitState, "hp_max", Mathf.Max(unitState.current_hp, 1));
        int mpMax = GetSnapshotValue(unitState, "mp_max", Mathf.Max(unitState.current_mp, 0));
        int staminaMax = GetSnapshotValue(
            unitState,
            "stamina_max",
            Mathf.Max(unitState.current_stamina, 0)
        );
        int auraMax = GetSnapshotValue(unitState, "aura_max", Mathf.Max(unitState.current_aura, 0));
        int apMax = GetSnapshotValue(
            unitState,
            "action_points",
            Mathf.Max(unitState.current_ap, 1)
        );
        int moveMax = BattleUnitState.DefaultMovePointsPerTurn;
        return new BattleHudFocusUnitSnapshot(
            FormatUnitName(unitState, "单位"),
            BuildFocusRoleText(unitState, battleState),
            BuildResourceInfo(unitState),
            portraitData.Glyph,
            portraitData.PortraitKey,
            portraitData.PrimaryColor,
            portraitData.SecondaryColor,
            portraitData.EdgeColor,
            unitState.current_hp,
            Mathf.Max(hpMax, 1),
            unitState.current_mp,
            Mathf.Max(mpMax, 1),
            unitState.current_stamina,
            Mathf.Max(staminaMax, 1),
            unitState.current_aura,
            Mathf.Max(auraMax, 1),
            unitState.current_ap,
            Mathf.Max(apMax, 1),
            unitState.current_move_points,
            moveMax
        );
    }

    private BattleHudResourceInfoSnapshot BuildResourceInfo(BattleUnitState unitState)
    {
        int hpCurrent = unitState?.current_hp ?? 0;
        int mpCurrent = unitState?.current_mp ?? 0;
        int staminaCurrent = unitState?.current_stamina ?? 0;
        int auraCurrent = unitState?.current_aura ?? 0;
        int apCurrent = unitState?.current_ap ?? 0;
        int moveCurrent = unitState?.current_move_points ?? 0;
        int hpMax = GetSnapshotValue(unitState, "hp_max", Mathf.Max(hpCurrent, 1));
        int mpMax = GetSnapshotValue(unitState, "mp_max", Mathf.Max(mpCurrent, 0));
        int staminaMax = GetSnapshotValue(unitState, "stamina_max", Mathf.Max(staminaCurrent, 0));
        int auraMax = GetSnapshotValue(unitState, "aura_max", Mathf.Max(auraCurrent, 0));
        int apMax = GetSnapshotValue(unitState, "action_points", Mathf.Max(apCurrent, 1));
        int moveMax = BattleUnitState.DefaultMovePointsPerTurn;
        return new BattleHudResourceInfoSnapshot(
            ResourceLine(hpCurrent, Mathf.Max(hpMax, 1), "HP", true),
            ResourceLine(
                mpCurrent,
                Mathf.Max(mpMax, 1),
                "MP",
                IsResourceUnlocked(unitState, CombatResourceIds.ToStringName(CombatResourceIdKind.Mp))
            ),
            ResourceLine(staminaCurrent, Mathf.Max(staminaMax, 1), "ST", true),
            ResourceLine(
                auraCurrent,
                Mathf.Max(auraMax, 1),
                "AU",
                IsResourceUnlocked(unitState, CombatResourceIds.ToStringName(CombatResourceIdKind.Aura))
            ),
            ResourceLine(apCurrent, Mathf.Max(apMax, 1), "AP", true),
            ResourceLine(moveCurrent, moveMax, "MOVE", true)
        );
    }

    private BattleHudResourceLineSnapshot ResourceLine(
        int current,
        int max,
        string label,
        bool visible
    ) => new(current, Mathf.Max(max, 1), GetRatio(current, max), label, visible);

    private bool IsResourceUnlocked(BattleUnitState unitState, StringName resourceId)
    {
        return unitState != null && unitState.HasCombatResourceUnlocked(resourceId);
    }

    private string BuildFocusRoleText(BattleUnitState unitState, BattleState battleState)
    {
        string factionText =
            battleState != null && battleState.enemy_unit_ids.Contains(unitState.unit_id)
                ? "敌方"
                : "我方";
        return $"{factionText}  ·  {FormatControlMode(unitState.control_mode)}  ·  体型 {Mathf.Max(unitState.body_size, 1)}";
    }

    private static string BuildSkillTitle(string selectedSkillName, string selectedSkillVariantName)
    {
        if (string.IsNullOrEmpty(selectedSkillName))
            return "技能矩阵";
        if (string.IsNullOrEmpty(selectedSkillVariantName))
            return selectedSkillName;
        return $"{selectedSkillName} · {selectedSkillVariantName}";
    }

    private string BuildSkillSubtitle(
        BattleUnitState activeUnit,
        string selectedSkillName,
        string selectedSkillVariantName,
        int selectedCount,
        int requiredCount,
        SelectionInfo selectionInfo,
        AttackPreviewData hitPreview,
        DamagePreviewSummary damagePreview,
        BattlePresentationPayload saveBranchPreview
    )
    {
        if (activeUnit == null)
            return "无可行动单位";
        if (string.IsNullOrEmpty(selectedSkillName))
            return $"当前单位 {FormatUnitName(activeUnit, "单位")}  ·  已装备技能 {BuildSkillAvailabilityView(activeUnit).SkillEntries.Count}";
        string title = BuildSkillTitle(selectedSkillName, selectedSkillVariantName);
        if (selectionInfo.IsMultiUnit)
        {
            int minTargetCount = selectionInfo.MinTargetCount;
            int maxTargetCount = selectionInfo.MaxTargetCount > 0
                ? selectionInfo.MaxTargetCount
                : Mathf.Max(requiredCount, 1);
            if (selectedCount <= 0)
                return $"当前技能 {title}  ·  左键逐个点选目标单位";
            if (selectedCount < minTargetCount)
                return $"当前技能 {title}  ·  已锁定 {selectedCount} 个目标，仍未达到最少 {minTargetCount} 个，继续点选";
            if (selectedCount < maxTargetCount)
                return $"当前技能 {title}  ·  已锁定 {selectedCount} 个目标，最少 {minTargetCount} / 最多 {maxTargetCount} 个，已满足最小数量，可点击自己或空地确认；继续点选将自动施放";
            return $"当前技能 {title}  ·  已锁定 {selectedCount} 个目标，已达到上限 {maxTargetCount} 个，将自动施放";
        }

        var previewParts = new List<string>();
        string saveBranchText = saveBranchPreview.SummaryText;
        if (!string.IsNullOrEmpty(saveBranchText))
            previewParts.Add(saveBranchText);
        string hitPreviewText = hitPreview?.SummaryText ?? "";
        if (!string.IsNullOrEmpty(hitPreviewText))
            previewParts.Add(hitPreviewText);
        string damagePreviewText = damagePreview.SummaryText;
        if (!string.IsNullOrEmpty(damagePreviewText))
            previewParts.Add(damagePreviewText);
        if (previewParts.Count > 0)
            return $"当前技能 {title}  ·  {string.Join("  ·  ", previewParts)}";
        if (requiredCount <= 1)
            return $"当前技能 {title}  ·  左键选择目标格释放";
        return $"当前技能 {title}  ·  选点 {selectedCount}/{requiredCount}";
    }

    private IReadOnlyList<BattleHudSkillSlotSnapshot> BuildSkillSlots(
        BattleUnitState activeUnit,
        StringName selectedSkillEntryId
    )
    {
        var skillSlots = new List<BattleHudSkillSlotSnapshot>();
        if (activeUnit != null)
        {
            BattleSkillAvailabilityView availabilityView = BuildSkillAvailabilityView(activeUnit);
            int count = Mathf.Min(availabilityView.SkillEntries.Count, SKILL_GRID_SIZE);
            for (int index = 0; index < count; index++)
            {
                BattleAvailableSkillEntry entry = availabilityView.SkillEntries[index];
                if (entry == null)
                    continue;
                StringName skillId = entry.EntryRef.SkillId;
                SkillDefinition skillDefinition = entry.SkillDefinition;
                string displayName = GetSkillDisplayName(skillDefinition, skillId);
                string iconKey = GetSkillIconKey(skillDefinition, skillId);
                Color accentColor = BuildSkillColor(iconKey, displayName);
                SkillSlotState slotState = BuildSkillSlotState(activeUnit, skillDefinition, skillId);
                string description =
                    skillDefinition != null ? skillDefinition.Description.StripEdges() : "";
                skillSlots.Add(
                    new BattleHudSkillSlotSnapshot(
                        index,
                        false,
                        entry.EntryRef.SkillEntryId.ToString(),
                        skillId.ToString(),
                        FormatSkillEntrySourceKind(entry.EntryRef.SourceKind),
                        FormatSkillEntrySourceLabelKey(entry.EntryRef.SourceKind),
                        entry.SkillLevel,
                        entry.EntryRef.SourceKind != BattleSkillEntrySourceKind.KnownSkill,
                        entry.SuppressedSourceKeys,
                        displayName,
                        BuildSkillShortName(displayName),
                        description,
                        iconKey,
                        index < 9 ? (index + 1).ToString() : "",
                        slotState.FooterText,
                        entry.EntryRef.SkillEntryId == selectedSkillEntryId,
                        slotState.IsDisabled,
                        accentColor,
                        accentColor.Darkened(0.48f),
                        accentColor.Lightened(0.16f),
                        slotState.Cooldown,
                        slotState.DisabledReason
                    )
                );
            }
        }

        for (int index = skillSlots.Count; index < SKILL_GRID_SIZE; index++)
            skillSlots.Add(new BattleHudSkillSlotSnapshot(index, true));
        return skillSlots.AsReadOnly();
    }

    private BattleSkillAvailabilityView BuildSkillAvailabilityView(BattleUnitState activeUnit)
    {
        BattleSkillAvailabilityService service = new(
            GetSkillCatalog(),
            GetSkillDefinitions(),
            GetEquipmentAbilityBindings(),
            GetItemDefinitions()
        );
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = activeUnit,
                Consumer = BattleSkillAvailabilityConsumer.Hud,
                IncludeEquipmentSkills = true,
                WorldStep = GetBattleWorldStep(),
                BattleState = _runtime?.GetBattleRuntime()?.GetState(),
            }
        );
    }

    private IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindings()
    {
        return _runtime?.GetBattleRuntime()?.GetEquipmentAbilityBindingIndexTyped();
    }

    private int GetBattleWorldStep() =>
        _runtime?.GetBattleRuntime()?.GetBattleWorldStep()
        ?? _runtime?.GetWorldStep()
        ?? -1;

    private static string FormatSkillEntrySourceKind(BattleSkillEntrySourceKind sourceKind)
    {
        return sourceKind switch
        {
            BattleSkillEntrySourceKind.KnownSkill => "known_skill",
            BattleSkillEntrySourceKind.EquipmentSkill => "equipment_skill",
            BattleSkillEntrySourceKind.ScopedAutoCast => "scoped_auto_cast",
            _ => "",
        };
    }

    private static string FormatSkillEntrySourceLabelKey(BattleSkillEntrySourceKind sourceKind)
    {
        return sourceKind switch
        {
            BattleSkillEntrySourceKind.KnownSkill => "skill_source.known",
            BattleSkillEntrySourceKind.EquipmentSkill => "skill_source.equipment",
            BattleSkillEntrySourceKind.ScopedAutoCast => "skill_source.scoped_auto_cast",
            _ => "",
        };
    }

    private BattleHudEquipmentPanelSnapshot BuildEquipmentPanelSnapshot(
        BattleState battleState,
        BattleUnitState activeUnit
    )
    {
        const string title = "队伍共享背包（战斗局部）";
        const string meta = "仅显示本场战斗复制出的队伍共享背包；据点共享仓库入口战中不可用。";
        if (battleState == null)
        {
            return new BattleHudEquipmentPanelSnapshot(
                title,
                meta,
                "",
                "无当前行动单位",
                CHANGE_EQUIPMENT_AP_COST,
                false,
                "当前没有可换装单位。",
                Array.Empty<BattleHudEquipmentSlotSnapshot>(),
                Array.Empty<BattleHudBackpackEntrySnapshot>(),
                "battle-local view 尚未就绪。"
            );
        }

        string activeUnitId = activeUnit?.unit_id.ToString() ?? "";
        string activeUnitName = FormatUnitName(activeUnit, "无当前行动单位");
        string disabledReason = GetChangeEquipmentDisabledReason(battleState, activeUnit);
        IReadOnlyList<BattleHudEquipmentSlotSnapshot> slots =
            BuildEquipmentSlotEntries(activeUnit);
        IReadOnlyList<BattleHudBackpackEntrySnapshot> backpackEntries =
            BuildBackpackEquipmentEntries(
            battleState,
            activeUnit,
            disabledReason
        );
        return new BattleHudEquipmentPanelSnapshot(
            title,
            meta,
            activeUnitId,
            activeUnitName,
            CHANGE_EQUIPMENT_AP_COST,
            string.IsNullOrEmpty(disabledReason),
            disabledReason,
            slots,
            backpackEntries,
            $"当前行动单位：{activeUnitName}  |  换装消耗 {CHANGE_EQUIPMENT_AP_COST} AP  |  背包装备实例 {backpackEntries.Count} 件"
        );
    }

    private string GetChangeEquipmentDisabledReason(
        BattleState battleState,
        BattleUnitState activeUnit
    )
    {
        if (battleState == null)
            return "战斗状态不可用。";
        if (activeUnit == null)
            return "当前没有可换装单位。";
        if (battleState.active_unit_id != activeUnit.unit_id)
            return "只能为当前行动单位自己换装。";
        if (battleState.PhaseKind != BattlePhaseKind.UnitActing)
            return "当前阶段不能换装。";
        if (!IsEmpty(battleState.modal_state))
            return "当前有待处理的战斗流程，暂时无法换装。";
        if (activeUnit.ControlModeKind != BattleUnitControlMode.Manual)
            return "当前行动单位不是手动控制，不能换装。";
        if (activeUnit.current_ap < CHANGE_EQUIPMENT_AP_COST)
            return $"AP不足，换装需要 {CHANGE_EQUIPMENT_AP_COST} 点 AP。";
        return "";
    }

    private IReadOnlyList<BattleHudEquipmentSlotSnapshot> BuildEquipmentSlotEntries(
        BattleUnitState activeUnit
    )
    {
        var entries = new List<BattleHudEquipmentSlotSnapshot>();
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = GetItemDefinitions();
        EquipmentState equipmentView = activeUnit?.GetEquipmentView() as EquipmentState;
        foreach (StringName slotId in EquipmentRules.GetAllSlotIdsTyped())
        {
            StringName itemId = equipmentView != null
                ? ProgressionDataUtils.to_string_name(equipmentView.GetEquippedItemId(slotId))
                : new StringName("");
            StringName entrySlotId = equipmentView != null
                ? ProgressionDataUtils.to_string_name(equipmentView.GetEntrySlotForSlot(slotId))
                : new StringName("");
            bool isFilled = !IsEmpty(itemId) && !IsEmpty(entrySlotId);
            IReadOnlyList<StringName> occupiedSlotIds = isFilled
                ? equipmentView.GetOccupiedSlotIdsForEntryTyped(entrySlotId)
                : Array.Empty<StringName>();
            bool isEntrySlot = isFilled && entrySlotId == slotId;
            entries.Add(
                new BattleHudEquipmentSlotSnapshot(
                    slotId.ToString(),
                    EquipmentRules.GetSlotLabel(slotId),
                    isFilled,
                    isEntrySlot,
                    isFilled ? entrySlotId.ToString() : "",
                    isFilled ? itemId.ToString() : "",
                    isFilled ? GetItemDisplayName(itemDefinitions, itemId) : "空",
                    isFilled
                        ? ProgressionDataUtils
                            .to_string_name(equipmentView.GetEquippedInstanceId(slotId))
                            .ToString()
                        : "",
                    StringifyStringNameArray(occupiedSlotIds),
                    BuildSlotLabels(occupiedSlotIds),
                    isEntrySlot,
                    isFilled && !isEntrySlot
                        ? $"该槽位由 {EquipmentRules.GetSlotLabel(entrySlotId)} 占用，请从入口槽卸下。"
                        : ""
                )
            );
        }
        return entries;
    }

    private IReadOnlyList<BattleHudBackpackEntrySnapshot> BuildBackpackEquipmentEntries(
        BattleState battleState,
        BattleUnitState activeUnit,
        string disabledReason
    )
    {
        var entries = new List<BattleHudBackpackEntrySnapshot>();
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = GetItemDefinitions();
        WarehouseState backpackView = battleState?.GetPartyBackpackView();
        if (backpackView == null)
            return entries;

        string previewCacheSignature = "";
        if (_runtime != null)
        {
            previewCacheSignature = BuildEquipmentPreviewCacheSignature(
                battleState,
                activeUnit,
                backpackView
            );
            SyncEquipmentPreviewCacheSignature(previewCacheSignature);
        }
        else
        {
            ClearEquipmentPreviewCache();
        }

        foreach (EquipmentInstanceState instance in backpackView.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance == null)
                continue;
            StringName itemId = ProgressionDataUtils.to_string_name(instance.item_id);
            StringName instanceId = ProgressionDataUtils.to_string_name(instance.instance_id);
            ItemDefinition itemDefinition = GetItemDefinition(itemDefinitions, itemId);
            var allowedSlotIds = new List<StringName>();
            string entryDisabledReason = disabledReason;
            if (itemDefinition == null)
            {
                if (string.IsNullOrEmpty(entryDisabledReason))
                    entryDisabledReason = $"找不到装备定义：{itemId}。";
            }
            else if (!itemDefinition.IsEquipment())
            {
                if (string.IsNullOrEmpty(entryDisabledReason))
                    entryDisabledReason =
                        $"{GetItemDisplayName(itemDefinitions, itemId)} 不是可装备物品。";
            }
            else
            {
                allowedSlotIds = ToStringNameArray(
                    itemDefinition.GetEquipmentSlotIdsTyped()
                );
                if (allowedSlotIds.Count == 0 && string.IsNullOrEmpty(entryDisabledReason))
                    entryDisabledReason =
                        $"{GetItemDisplayName(itemDefinitions, itemId)} 当前没有可用装备槽。";
            }

            StringName defaultSlot =
                allowedSlotIds.Count > 0 ? allowedSlotIds[0] : new StringName("");
            if (
                string.IsNullOrEmpty(entryDisabledReason)
                && _runtime != null
            )
            {
                EquipmentPreviewRule previewRule = PreviewBackpackEquipmentEntryChange(
                    activeUnit,
                    itemId,
                    instanceId,
                    allowedSlotIds,
                    previewCacheSignature
                );
                if (previewRule != null)
                {
                    if (previewRule.allowed)
                        defaultSlot = previewRule.slot_id;
                    else
                        entryDisabledReason = ResolveEquipmentPreviewFailureMessage(
                            previewRule.message
                        );
                }
            }

            entries.Add(
                new BattleHudBackpackEntrySnapshot(
                    instanceId.ToString(),
                    itemId.ToString(),
                    GetItemDisplayName(itemDefinitions, itemId),
                    GetItemDescription(itemDefinition),
                    GetItemIcon(itemDefinition),
                    StringifyStringNameArray(allowedSlotIds),
                    BuildSlotLabels(allowedSlotIds),
                    defaultSlot.ToString(),
                    StringifyStringNameArray(
                        GetFinalOccupiedSlotIds(itemDefinition, defaultSlot)
                    ),
                    string.IsNullOrEmpty(entryDisabledReason),
                    entryDisabledReason
                )
            );
        }
        return entries;
    }

    private EquipmentPreviewRule PreviewBackpackEquipmentEntryChange(
        BattleUnitState activeUnit,
        StringName itemId,
        StringName instanceId,
        IReadOnlyList<StringName> allowedSlotIds,
        string previewCacheSignature = ""
    )
    {
        if (
            activeUnit == null
            || IsEmpty(activeUnit.unit_id)
            || _runtime == null
            || allowedSlotIds.Count == 0
        )
            return null;

        string cacheKey = BuildEquipmentPreviewCacheKey(
            activeUnit,
            itemId,
            instanceId,
            allowedSlotIds
        );
        if (
            !string.IsNullOrEmpty(previewCacheSignature)
            && _equipmentPreviewCache.TryGetValue(cacheKey, out EquipmentPreviewRule cachedRule)
        )
            return cachedRule;

        var firstFailure = new EquipmentPreviewRule
        {
            allowed = false,
            slot_id = allowedSlotIds[0],
            message = "",
        };
        foreach (StringName slotId in allowedSlotIds)
        {
            BattleCommand command = BuildChangeEquipmentPreviewCommand(
                activeUnit,
                itemId,
                instanceId,
                slotId
            );
            BattlePreview preview = _runtime.PreviewBattleCommand(command);
            if (IsBattlePreviewAllowed(preview))
            {
                var allowedRule = new EquipmentPreviewRule
                {
                    allowed = true,
                    slot_id = slotId,
                    message = GetBattlePreviewMessage(preview),
                };
                StoreEquipmentPreviewRule(previewCacheSignature, cacheKey, allowedRule);
                return allowedRule;
            }
            string previewMessage = GetBattlePreviewMessage(preview);
            if (
                !string.IsNullOrEmpty(previewMessage)
                && string.IsNullOrEmpty(firstFailure.message)
            )
                firstFailure.message = previewMessage;
        }
        firstFailure.message = ResolveEquipmentPreviewFailureMessage(firstFailure.message);
        StoreEquipmentPreviewRule(previewCacheSignature, cacheKey, firstFailure);
        return firstFailure;
    }

    private static string ResolveEquipmentPreviewFailureMessage(string message)
    {
        return !string.IsNullOrEmpty(message) ? message : EquipmentPreviewDefaultFailureMessage;
    }

    private void SyncEquipmentPreviewCacheSignature(string signature)
    {
        if (signature == _equipmentPreviewCacheSignature)
            return;
        _equipmentPreviewCacheSignature = signature;
        _equipmentPreviewCache.Clear();
    }

    private void ClearEquipmentPreviewCache()
    {
        _equipmentPreviewCacheSignature = "";
        _equipmentPreviewCache.Clear();
    }

    private void StoreEquipmentPreviewRule(
        string cacheSignature,
        string cacheKey,
        EquipmentPreviewRule rule
    )
    {
        if (string.IsNullOrEmpty(cacheSignature) || string.IsNullOrEmpty(cacheKey) || rule == null)
            return;
        _equipmentPreviewCache[cacheKey] = rule;
    }

    private static string BuildEquipmentPreviewCacheKey(
        BattleUnitState activeUnit,
        StringName itemId,
        StringName instanceId,
        IEnumerable<StringName> allowedSlotIds
    )
    {
        var slotParts = new List<string>();
        foreach (StringName slotId in allowedSlotIds)
            slotParts.Add(slotId.ToString());
        return $"{(activeUnit != null ? activeUnit.unit_id.ToString() : "")}:{itemId}:{instanceId}:{string.Join(",", slotParts)}";
    }

    private string BuildEquipmentPreviewCacheSignature(
        BattleState battleState,
        BattleUnitState activeUnit,
        WarehouseState backpackView
    )
    {
        var parts = new List<string>
        {
            battleState != null ? battleState.battle_id.ToString() : "",
            battleState != null ? battleState.phase.ToString() : "",
            battleState != null ? battleState.modal_state.ToString() : "",
            battleState != null ? battleState.active_unit_id.ToString() : "",
            activeUnit != null ? activeUnit.unit_id.ToString() : "",
            activeUnit != null ? activeUnit.source_member_id.ToString() : "",
            activeUnit != null ? activeUnit.control_mode.ToString() : "",
            (activeUnit?.body_size ?? 0).ToString(),
            (activeUnit?.current_ap ?? 0).ToString(),
            BuildPartyMemberRequirementSignature(
                activeUnit != null ? activeUnit.source_member_id : new StringName("")
            ),
            BuildEquipmentViewSignature(activeUnit?.GetEquipmentView() as EquipmentState),
            BuildBackpackViewSignature(backpackView),
            _runtime != null
                ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_runtime).ToString()
                : "",
        };
        return string.Join("|", parts);
    }

    private string BuildPartyMemberRequirementSignature(StringName memberId)
    {
        if (IsEmpty(memberId))
            return "-";
        PartyMemberState memberState = GetPartyMemberState(memberId);
        if (memberState == null)
            return $"{memberId}:-";
        var professionParts = new List<string>();
        UnitProgress progression = memberState.progression as UnitProgress;
        if (progression != null)
        {
            foreach (StringName professionId in progression.GetSortedProfessionIdsTyped())
            {
                UnitProfessionProgress profession = progression.GetProfessionProgress(
                    professionId
                );
                if (profession == null)
                    continue;
                professionParts.Add(
                    $"{ProgressionDataUtils.to_string_name(profession.profession_id)}:{profession.rank}:{(profession.is_active ? 1 : 0)}:{(profession.is_hidden ? 1 : 0)}"
                );
            }
        }
        return $"{memberId}:{memberState.body_size}:{string.Join(";", professionParts)}";
    }

    private static string BuildEquipmentViewSignature(EquipmentState equipmentView)
    {
        if (equipmentView == null)
            return "-";
        var parts = new List<string>();
        foreach (StringName slotId in EquipmentRules.GetAllSlotIdsTyped())
        {
            IReadOnlyList<StringName> occupiedSlotIds = Array.Empty<StringName>();
            StringName entrySlotId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEntrySlotForSlot(slotId)
            );
            if (!IsEmpty(entrySlotId))
                occupiedSlotIds = equipmentView.GetOccupiedSlotIdsForEntryTyped(entrySlotId);
            parts.Add(
                $"{slotId}:{ProgressionDataUtils.to_string_name(equipmentView.GetEquippedItemId(slotId))}:{ProgressionDataUtils.to_string_name(equipmentView.GetEquippedInstanceId(slotId))}:{JoinStringNameArray(occupiedSlotIds)}"
            );
        }
        return string.Join(";", parts);
    }

    private static string BuildBackpackViewSignature(WarehouseState backpackView)
    {
        if (backpackView == null)
            return "-";
        var parts = new List<string>();
        foreach (EquipmentInstanceState instance in backpackView.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance == null)
                continue;
            parts.Add(
                $"{ProgressionDataUtils.to_string_name(instance.instance_id)}:{ProgressionDataUtils.to_string_name(instance.item_id)}:{instance.rarity}:{instance.current_durability}"
            );
        }
        return string.Join(";", parts);
    }

    private static string JoinStringNameArray(IEnumerable<StringName> values)
    {
        var parts = new List<string>();
        if (values != null)
        {
            foreach (StringName value in values)
                parts.Add(value.ToString());
        }
        return string.Join(",", parts);
    }

    private static BattleCommand BuildChangeEquipmentPreviewCommand(
        BattleUnitState activeUnit,
        StringName itemId,
        StringName instanceId,
        StringName slotId
    )
    {
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.ChangeEquipment,
            unit_id = activeUnit.unit_id,
            target_unit_id = activeUnit.unit_id,
            EquipmentOperationKind = BattleEquipmentOperationKind.Equip,
            equipment_slot_id = slotId,
            equipment_item_id = itemId,
            equipment_instance_id = instanceId,
        };
    }

    private static bool IsBattlePreviewAllowed(BattlePreview preview)
    {
        return preview != null && preview.allowed;
    }

    private static string GetBattlePreviewMessage(BattlePreview preview)
    {
        if (preview == null)
            return "";
        return preview.LogLinesTyped.Count > 0 ? preview.LogLinesTyped[^1] : "";
    }

    private static IReadOnlyList<StringName> GetFinalOccupiedSlotIds(
        ItemDefinition itemDefinition,
        StringName entrySlotId
    )
    {
        if (itemDefinition == null || IsEmpty(entrySlotId))
            return Array.Empty<StringName>();
        return ToStringNameArray(
            itemDefinition.GetFinalOccupiedSlotIdsTyped(entrySlotId)
        );
    }

    private static List<StringName> ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitions()
    {
        if (_runtime != null)
            return _runtime.GetItemDefsTyped();
        return _gameSession != null
            ? _gameSession.GetItemDefsTyped()
            : new Dictionary<StringName, ItemDefinition>();
    }

    private static string GetItemDisplayName(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        StringName itemId
    )
    {
        ItemDefinition itemDefinition = GetItemDefinition(itemDefinitions, itemId);
        if (itemDefinition != null && !string.IsNullOrEmpty(itemDefinition.DisplayName))
            return itemDefinition.DisplayName;
        return itemId.ToString();
    }

    private static string GetItemDescription(ItemDefinition itemDefinition)
    {
        if (itemDefinition != null && !string.IsNullOrEmpty(itemDefinition.Description))
            return itemDefinition.Description;
        return "暂无说明。";
    }

    private static string GetItemIcon(ItemDefinition itemDefinition)
    {
        return itemDefinition != null ? itemDefinition.Icon : "";
    }

    private static IReadOnlyList<string> BuildSlotLabels(IEnumerable<StringName> slotIds)
    {
        var labels = new List<string>();
        if (slotIds != null)
        {
            foreach (StringName slotId in slotIds)
                labels.Add(EquipmentRules.GetSlotLabel(slotId));
        }
        return labels;
    }

    private static IReadOnlyList<string> StringifyStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new List<string>();
        if (values != null)
        {
            foreach (StringName value in values)
                result.Add(value.ToString());
        }
        return result;
    }

    private SkillSlotState BuildSkillSlotState(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        StringName skillId
    )
    {
        CombatSkillResourceCosts costs = GetEffectiveSkillCosts(activeUnit, skillDefinition);
        int apCost = costs.ApCost;
        int mpCost = costs.MpCost;
        int staminaCost = costs.StaminaCost;
        int auraCost = costs.AuraCost;
        int cooldown = activeUnit != null ? activeUnit.GetCooldownTyped(skillId, 0) : 0;

        // Skill slots only ever hold active combat skills — BattleUnitFactory
        // builds known_active_skill_ids with a SkillTypeKind.Active &&
        // CanUseInCombat() filter, so passives/weapon trainings never reach here
        // and every slot has a combat profile. Battle runtime is the
        // single source of truth for cast-gating
        // (cooldown, AP/MP/ST/AU, weapon family/type, required shield/melee
        // weapon, status locks), so mirror its verdict instead of re-deriving
        // any of it. Target validity is excluded — it can only be known once the
        // player picks a target.
        if (activeUnit != null && skillDefinition != null && _runtime != null)
        {
            string castBlockReason = _runtime.GetBattleSkillCastBlockMessage(activeUnit, skillId);
            if (!string.IsNullOrEmpty(castBlockReason))
                return DisabledSkillSlot(
                    cooldown > 0 ? $"CD {cooldown}" : "不可用",
                    cooldown,
                    castBlockReason
                );
        }

        return EnabledSkillSlot(apCost, mpCost, staminaCost, auraCost, cooldown);
    }

    private SkillSlotState EnabledSkillSlot(
        int apCost,
        int mpCost,
        int staminaCost,
        int auraCost,
        int cooldown
    )
    {
        return new SkillSlotState(
            BuildSkillFooter(apCost, mpCost, staminaCost, auraCost, cooldown),
            false,
            cooldown,
            ""
        );
    }

    private static SkillSlotState DisabledSkillSlot(
        string footerText,
        int cooldown,
        string reason
    )
    {
        return new SkillSlotState(footerText, true, cooldown, reason);
    }

    private static string BuildSkillFooter(
        int apCost,
        int mpCost,
        int staminaCost,
        int auraCost,
        int cooldown
    )
    {
        if (cooldown > 0)
            return $"CD {cooldown}";
        var parts = new List<string>();
        if (apCost > 0)
            parts.Add($"AP {apCost}");
        if (mpCost > 0)
            parts.Add($"MP {mpCost}");
        if (staminaCost > 0)
            parts.Add($"ST {staminaCost}");
        if (auraCost > 0)
            parts.Add($"AU {auraCost}");
        return parts.Count > 0 ? string.Join(" ", parts) : "READY";
    }

    private string BuildTileText(
        Vector2I selectedCoord,
        BattleCellState selectedCell,
        BattleUnitState selectedUnit
    )
    {
        return $"地格 {FormatCoord(selectedCoord)}  ·  {FormatTerrainName(selectedCell)}  ·  高度 {(selectedCell != null ? selectedCell.current_height : 0)}  ·  占位 {FormatUnitName(selectedUnit, "无")}";
    }

    private PortraitData BuildPortraitData(BattleUnitState unitState, BattleState battleState)
    {
        string portraitKey = "";
        if (unitState != null && !IsEmpty(unitState.source_member_id))
        {
            PartyMemberState memberState = GetPartyMemberState(unitState.source_member_id);
            if (memberState != null)
                portraitKey = memberState.portrait_id.ToString();
        }
        if (string.IsNullOrEmpty(portraitKey) && unitState != null)
            portraitKey = unitState.unit_id.ToString();

        bool isEnemy =
            battleState != null
            && unitState != null
            && battleState.enemy_unit_ids.Contains(unitState.unit_id);
        PortraitPalette palette = BuildPortraitPalette(portraitKey, isEnemy);
        return new PortraitData(
            portraitKey,
            BuildUnitGlyph(unitState),
            palette.PrimaryColor,
            palette.SecondaryColor,
            palette.EdgeColor
        );
    }

    private static PortraitPalette BuildPortraitPalette(string portraitKey, bool isEnemy)
    {
        string normalizedKey = portraitKey.ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        if (normalizedKey.Contains("sword"))
        {
            return new PortraitPalette(
                new Color(0.28f, 0.55f, 0.85f, 1.0f),
                new Color(0.1f, 0.18f, 0.32f, 1.0f),
                new Color(0.96f, 0.83f, 0.54f, 1.0f)
            );
        }
        if (normalizedKey.Contains("axe"))
        {
            return new PortraitPalette(
                new Color(0.78f, 0.34f, 0.22f, 1.0f),
                new Color(0.28f, 0.09f, 0.05f, 1.0f),
                new Color(0.98f, 0.77f, 0.44f, 1.0f)
            );
        }
        if (normalizedKey.Contains("spear"))
        {
            return new PortraitPalette(
                new Color(0.24f, 0.72f, 0.53f, 1.0f),
                new Color(0.07f, 0.2f, 0.14f, 1.0f),
                new Color(0.96f, 0.85f, 0.52f, 1.0f)
            );
        }

        int hashValue = Math.Abs((int)StringExtensions.Hash(normalizedKey));
        float hue = (hashValue % 360) / 360.0f;
        Color baseColor = Color.FromHsv(
            hue,
            isEnemy ? 0.72f : 0.46f,
            isEnemy ? 0.82f : 0.88f,
            1.0f
        );
        return new PortraitPalette(
            baseColor,
            baseColor.Darkened(0.62f),
            isEnemy
                ? new Color(0.9f, 0.46f, 0.3f, 1.0f)
                : new Color(0.94f, 0.79f, 0.5f, 1.0f)
        );
    }

    private static Color BuildSkillColor(string iconKey, string displayName)
    {
        string normalizedKey = iconKey.ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        if (normalizedKey.Contains("sword"))
            return new Color(0.98f, 0.84f, 0.36f, 1.0f);
        if (normalizedKey.Contains("axe"))
            return new Color(0.96f, 0.42f, 0.24f, 1.0f);
        if (normalizedKey.Contains("spear"))
            return new Color(0.34f, 0.82f, 0.7f, 1.0f);
        if (normalizedKey.Contains("charge"))
            return new Color(0.99f, 0.67f, 0.19f, 1.0f);
        if (normalizedKey.Contains("mud") || normalizedKey.Contains("fossil"))
            return new Color(0.78f, 0.58f, 0.28f, 1.0f);

        string hashSource = $"{iconKey}_{displayName}";
        int hashValue = Math.Abs((int)StringExtensions.Hash(hashSource));
        float hue = (hashValue % 360) / 360.0f;
        return Color.FromHsv(hue, 0.7f, 0.92f, 1.0f);
    }

    private string BuildUnitGlyph(BattleUnitState unitState)
    {
        if (unitState == null)
            return "?";
        string displayName = FormatUnitName(unitState, "?");
        return displayName.Length == 0
            ? "?"
            : displayName.Substring(0, Math.Min(displayName.Length, 1));
    }

    private static string BuildSkillShortName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
            return "--";
        return displayName.Substring(0, Math.Min(displayName.Length, 2));
    }

    private static string GetSkillDisplayName(SkillDefinition skillDefinition, StringName skillId)
    {
        if (skillDefinition != null && !string.IsNullOrEmpty(skillDefinition.DisplayName))
            return skillDefinition.DisplayName;
        return skillId.ToString();
    }

    private static string GetSkillIconKey(SkillDefinition skillDefinition, StringName skillId)
    {
        if (skillDefinition != null && !IsEmpty(skillDefinition.IconId))
            return skillDefinition.IconId.ToString();
        return skillId.ToString();
    }

    private IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitions()
    {
        if (_runtime != null)
            return _runtime.GetSkillDefinitionsTyped();
        return _gameSession != null
            ? _gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped()
            : new Dictionary<StringName, SkillDefinition>();
    }

    private AttackPreviewData BuildSelectedSkillHitPreview(BattlePreview selectedSkillPreview)
    {
        if (selectedSkillPreview == null)
            return null;
        if (selectedSkillPreview?.special_profile_preview_facts != null)
        {
            BattleSpecialProfilePreviewFacts facts =
                selectedSkillPreview.special_profile_preview_facts;
            MeteorSwarmPreviewFacts meteorFacts = facts as MeteorSwarmPreviewFacts;
            string summaryText = selectedSkillPreview.hit_preview?.SummaryText;
            if (string.IsNullOrEmpty(summaryText))
            {
                summaryText =
                    $"陨星雨影响 {meteorFacts?.impact_count ?? selectedSkillPreview.TargetCoordsTyped.Count} 格、预计波及 {meteorFacts?.expected_target_count ?? selectedSkillPreview.TargetUnitIdsTyped.Count} 个单位。";
            }
            var hitPreview = new AttackPreviewData
            {
                SummaryText = summaryText,
                Source = "special_profile_preview_facts",
            };
            hitPreview.SetAttackRollModifierBreakdown(
                facts.GetAttackRollModifierBreakdown()
            );
            return hitPreview;
        }
        if (selectedSkillPreview != null && selectedSkillPreview.hit_preview != null && !selectedSkillPreview.hit_preview.IsEmpty)
            return selectedSkillPreview.hit_preview;
        return null;
    }

    private static DamagePreviewSummary BuildSelectedSkillDamagePreview(
        BattlePreview selectedSkillPreview
    )
    {
        BattleDamagePreviewRangeService.SkillDamagePreview? damagePreview =
            selectedSkillPreview?.DamagePreviewTyped;
        if (!damagePreview.HasValue || !damagePreview.Value.HasDamage)
            return DamagePreviewSummary.Empty;
        BattleDamagePreviewRangeService.SkillDamagePreview value = damagePreview.Value;
        return new DamagePreviewSummary(
            true,
            value.MinDamage,
            value.MaxDamage,
            value.SummaryText
        );
    }

    private static BattlePresentationPayload BuildSelectedSkillSaveBranchPreview(
        BattlePreview selectedSkillPreview
    ) => BattlePresentationPayload.FromSaveBranch(selectedSkillPreview?.SaveBranchPreviewTyped);

    private FatePreviewFacts BuildSelectedSkillFatePreview(BattlePreview selectedSkillPreview)
    {
        BattleFatePreviewData fatePreview = selectedSkillPreview?.FatePreviewTyped;
        if (fatePreview?.ForceHitNoCrit == true)
            return BuildForceHitNoCritFatePreview();
        if (fatePreview?.UsesFateAttack == true)
            return BuildStandardFatePreview(fatePreview);
        if (selectedSkillPreview?.hit_preview?.ForceHitNoCrit == true)
            return BuildForceHitNoCritFatePreview();
        return FatePreviewFacts.Empty;
    }

    private FatePreviewFacts BuildStandardFatePreview(BattleFatePreviewData fatePreview)
    {
        if (fatePreview == null || !fatePreview.UsesFateAttack)
            return FatePreviewFacts.Empty;

        bool isDisadvantage = fatePreview.IsDisadvantage;
        int critGateDie = Mathf.Max(fatePreview.CritGateDie, 1);
        int fumbleLowEnd = Mathf.Max(fatePreview.FumbleLowEnd, 1);
        int critThreshold = Mathf.Clamp(fatePreview.CritThreshold, 1, 20);
        bool critLocked = fatePreview.CritLocked;
        bool mercyActive = fatePreview.MercyActive;
        var badges = new List<BattleHudFateBadgeSnapshot>
        {
            new(
                isDisadvantage ? "劣势" : "未陷劣势",
                isDisadvantage ? new StringName("warning") : new StringName("calm"),
                $"当前命中与命运骰按{(isDisadvantage ? "劣势取低" : "正常单骰")}口径结算。"
            ),
        };
        var detailLines = new List<string>
        {
            "命运判定概览",
            $"状态：{(isDisadvantage ? "劣势中" : "未陷劣势")}",
        };
        if (critLocked)
        {
            badges.Add(
                new BattleHudFateBadgeSnapshot(
                    "禁暴击",
                    new StringName("warning"),
                    "当前暴击已被锁定，不会触发暴击门或高位大成功。"
                )
            );
            detailLines.Add("暴击：已封锁");
        }
        else
        {
            badges.Add(
                new BattleHudFateBadgeSnapshot(
                    $"暴击门 d{critGateDie}",
                    new StringName("gate"),
                    $"命运暴击门尺寸：d{critGateDie}。"
                )
            );
            detailLines.Add($"暴击门：d{critGateDie}");
        }
        badges.Add(
            new BattleHudFateBadgeSnapshot(
                fumbleLowEnd <= 1 ? "大失败 1" : $"大失败 1-{fumbleLowEnd}",
                new StringName("danger"),
                $"当前大失败区间：1-{fumbleLowEnd}。"
            )
        );
        detailLines.Add($"大失败：1-{fumbleLowEnd}");
        if (!critLocked && critGateDie == 20)
        {
            string highThreatText = $"高位大成功 {critThreshold}-20";
            badges.Add(
                new BattleHudFateBadgeSnapshot(
                    highThreatText,
                    new StringName("high_threat"),
                    $"当前高位大成功区间：{critThreshold}-20。"
                )
            );
            detailLines.Add(highThreatText);
        }
        if (mercyActive)
        {
            badges.Add(
                new BattleHudFateBadgeSnapshot(
                    "命运的怜悯",
                    new StringName("mercy"),
                    "effective_luck<=-5 且处于劣势时，暴击门只额外放大一档。"
                )
            );
            detailLines.Add("命运的怜悯：已生效");
        }

        return new FatePreviewFacts(
            BuildFatePreviewSummaryText(badges),
            string.Join("\n", detailLines),
            badges
        );
    }

    private FatePreviewFacts BuildForceHitNoCritFatePreview()
    {
        var badges = new List<BattleHudFateBadgeSnapshot>
        {
            new("必定命中", new StringName("calm"), "这次攻击不会再进行命中骰判定，直接视为命中。"),
            new("禁暴击", new StringName("warning"), "这次攻击不会触发暴击。"),
            new("摆幅压低", new StringName("gate"), "这次攻击的命运摆幅已被压低，不再展示标准 crit/fumble 区间。"),
        };
        return new FatePreviewFacts(
            BuildFatePreviewSummaryText(badges),
            "命运判定概览\n状态：强制命中\n暴击：已封锁\n说明：这次攻击不再走标准命中/暴击/大失败骰。",
            badges
        );
    }

    private static string BuildFatePreviewSummaryText(
        IEnumerable<BattleHudFateBadgeSnapshot> badges
    )
    {
        var parts = new List<string>();
        foreach (BattleHudFateBadgeSnapshot badge in badges ?? Array.Empty<BattleHudFateBadgeSnapshot>())
            parts.Add(badge?.Text ?? "");
        return string.Join("  ·  ", parts);
    }

    private static string BuildSelectedSkillPreviewTooltip(
        AttackPreviewData hitPreview,
        FatePreviewFacts fatePreview,
        DamagePreviewSummary damagePreview,
        BattlePresentationPayload saveBranchPreview
    )
    {
        var sections = new List<string>();
        string saveBranchText = saveBranchPreview?.SummaryText ?? "";
        if (!string.IsNullOrEmpty(saveBranchText))
            sections.Add(saveBranchText);
        string hitText = hitPreview?.SummaryText ?? "";
        if (!string.IsNullOrEmpty(hitText))
            sections.Add(hitText);
        string damageText = damagePreview.SummaryText;
        if (!string.IsNullOrEmpty(damageText))
            sections.Add(damageText);
        string fateTooltip = fatePreview?.TooltipText ?? "";
        if (!string.IsNullOrEmpty(fateTooltip))
            sections.Add(fateTooltip);
        return string.Join("\n\n", sections);
    }

    private static string BuildSelectedSkillHitBadgeText(AttackPreviewData hitPreview)
    {
        if (hitPreview == null)
            return "";
        int successRate = hitPreview.SuccessRatePercent;
        if (successRate <= 0 && hitPreview.Stages.Count > 0)
            successRate = hitPreview.Stages[0].SuccessRatePercent;
        if (successRate <= 0)
            return "";
        return $"命中 {Mathf.Clamp(successRate, 0, 100)}%";
    }

    // Source of truth for command-dock button enable states. The risk note in the
    // A2 rebuild plan requires this to live ONLY here so the panel never shows a
    // button as enabled while the facade would reject the command. Definitions
    // mirror facade gating (manual active unit, UnitActing phase, no modal flow).
    private BattleHudCommandDockSnapshot BuildCommandDock(
        BattleState battleState,
        BattleUnitState activeUnit,
        StringName selectedSkillId,
        int selectedTargetCount
    )
    {
        bool unitActing = IsManualUnitActing(battleState, activeUnit);
        bool hasSkill = !IsEmpty(selectedSkillId);
        // Cast variants cycle with wraparound (CycleSelectedBattleSkillOption uses
        // PosMod), so prev/next share one enable condition: more than one unlocked
        // option to switch between. Keeping both keys lets the panel label them
        // independently while staying faithful to the keyboard Q/E behaviour.
        bool canCycleVariant = unitActing && hasSkill && GetUnlockedVariantCount(activeUnit, selectedSkillId) > 1;
        return new BattleHudCommandDockSnapshot(
            unitActing,
            unitActing && hasSkill,
            canCycleVariant,
            canCycleVariant
        );
    }

    // One-line "what can I do now" hint, covering the five states called out in the
    // A2 plan: modal block / no manual unit / auto mode / no skill selected /
    // multi-target selection progress / single-target ready.
    private string BuildHintText(
        BattleState battleState,
        BattleUnitState activeUnit,
        StringName selectedSkillId,
        int selectedTargetCount,
        SelectionInfo selectionInfo
    )
    {
        if (battleState == null)
            return "";
        if (!IsEmpty(battleState.modal_state))
            return "战斗结算中…请稍候";
        if (activeUnit == null || battleState.PhaseKind != BattlePhaseKind.UnitActing)
            return "等待行动单位";
        if (activeUnit.ControlModeKind != BattleUnitControlMode.Manual)
            return "自动模式：等待 AI 行动";
        if (IsEmpty(selectedSkillId))
            return "点选技能或移动；Enter 结束行动";
        if (selectionInfo.IsMultiUnit)
        {
            if (selectionInfo.AutoCastReady)
                return "已达目标上限，将自动施放";
            if (selectionInfo.ConfirmReady)
                return "已达最少目标；点击自己或空地结算，或继续点选";
            int remaining = Mathf.Max(
                selectionInfo.MinTargetCount - selectedTargetCount,
                0
            );
            return $"继续点选目标，还需 {remaining} 个；Esc 取消";
        }
        return "左键选择目标格释放；Esc 取消，Q/E 切换形态";
    }

    // Tail of the battle log, same source as RuntimeLogDock's battle view
    // (battle_state.log_entries), trimmed to the most recent N non-empty lines for
    // the in-panel LogLabel. Oldest-first so the panel can append top-to-bottom.
    internal static IReadOnlyList<string> BuildRecentBattleLogLines(BattleState battleState)
    {
        var lines = new List<string>();
        StringList logEntries = battleState?.log_entries;
        if (logEntries == null)
            return lines;
        var collected = new List<string>();
        for (
            int index = logEntries.Count - 1;
            index >= 0 && collected.Count < RECENT_BATTLE_LOG_LINE_LIMIT;
            index--
        )
        {
            string message = (logEntries[index] ?? "").StripEdges();
            if (string.IsNullOrEmpty(message))
                continue;
            collected.Add(message);
        }
        for (int index = collected.Count - 1; index >= 0; index--)
            lines.Add(collected[index]);
        return lines;
    }

    private bool IsManualUnitActing(BattleState battleState, BattleUnitState activeUnit)
    {
        return battleState != null
            && activeUnit != null
            && battleState.active_unit_id == activeUnit.unit_id
            && battleState.PhaseKind == BattlePhaseKind.UnitActing
            && IsEmpty(battleState.modal_state)
            && activeUnit.ControlModeKind == BattleUnitControlMode.Manual;
    }

    private int GetUnlockedVariantCount(BattleUnitState activeUnit, StringName selectedSkillId)
    {
        if (activeUnit == null || IsEmpty(selectedSkillId))
            return 0;
        SkillDefinition skillDefinition = GetSkillDefinition(GetSkillDefinitions(), selectedSkillId);
        if (skillDefinition?.CombatProfile == null)
            return 0;
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        return GetEffectiveCombatDefinition(skillDefinition, skillLevel).UnlockedCastVariants.Count;
    }

    private SelectionInfo BuildSkillTargetSelectionInfo(
        BattleState battleState,
        BattleUnitState activeUnit,
        StringName selectedSkillId,
        int selectedCount
    )
    {
        var defaultInfo = new SelectionInfo(
            new StringName("single_unit"),
            false,
            1,
            Mathf.Max(selectedCount, 1),
            false,
            false
        );
        if (battleState == null || activeUnit == null || IsEmpty(selectedSkillId))
            return defaultInfo;
        SkillDefinition skillDefinition = GetSkillDefinition(
            GetSkillDefinitions(),
            selectedSkillId
        );
        if (skillDefinition?.CombatProfile == null)
            return defaultInfo;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        StringName selectionMode = combatProfile.TargetSelectionMode;
        if (IsEmpty(selectionMode))
            selectionMode = "single_unit";
        int minTargetCount = Mathf.Max(combatProfile.MinTargetCount, 1);
        SkillEffectiveCombatDefinition effectiveDefinition =
            GetEffectiveCombatDefinition(skillDefinition, skillLevel);
        int maxTargetCount = Mathf.Max(
            effectiveDefinition.MaxTargetCount,
            minTargetCount
        );
        bool isMultiUnit = selectionMode == TARGET_SELECTION_MULTI_UNIT;
        bool confirmReady =
            isMultiUnit && selectedCount >= minTargetCount && selectedCount < maxTargetCount;
        bool autoCastReady = isMultiUnit && selectedCount >= maxTargetCount;
        return new SelectionInfo(
            selectionMode,
            isMultiUnit,
            minTargetCount,
            maxTargetCount,
            confirmReady,
            autoCastReady
        );
    }

    private CombatSkillResourceCosts GetEffectiveSkillCosts(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        if (skillDefinition?.CombatProfile == null)
            return CombatSkillResourceCosts.Zero;
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        return GetEffectiveCombatDefinition(skillDefinition, skillLevel).ResourceCosts;
    }

    private ISkillCatalog GetSkillCatalog()
    {
        if (_runtime != null)
            return _runtime.GetSkillCatalogTyped();
        return _gameSession?.GetContentCatalogTyped()?.GetSkillCatalogTyped();
    }

    private SkillEffectiveCombatDefinition GetEffectiveCombatDefinition(
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        if (skillDefinition?.CombatProfile == null)
            return SkillEffectiveCombatDefinition.BuildMissing(skillLevel);
        ISkillCatalog skillCatalog = GetSkillCatalog();
        if (skillCatalog != null && !IsEmpty(skillDefinition.SkillId))
            return skillCatalog.GetEffectiveCombatDefinition(
                skillDefinition.SkillId,
                skillLevel
            );
        return SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
    }

    private static int GetUnitSkillLevel(BattleUnitState activeUnit, StringName skillId)
    {
        if (activeUnit == null || IsEmpty(skillId))
            return 0;
        if (activeUnit.HasKnownSkillLevelTyped(skillId))
            return activeUnit.GetKnownSkillLevelTyped(skillId);
        return activeUnit.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private bool SkillHasTag(SkillDefinition skillDefinition, StringName expectedTag)
    {
        return skillDefinition != null
            && !IsEmpty(expectedTag)
            && skillDefinition.HasTag(expectedTag);
    }

    private PartyMemberState GetPartyMemberState(StringName memberId)
    {
        if (IsEmpty(memberId))
            return null;
        PartyMemberState memberState = _runtime?.GetPartyState()?.GetMemberState(memberId);
        if (memberState != null)
            return memberState;
        return _gameSession?.GetPartyMemberState(memberId);
    }

    private int CompareQueueCandidates(BattleUnitState a, BattleUnitState b)
    {
        if (a == null)
            return 1;
        if (b == null)
            return -1;
        bool aReady = _queueReadyLookup.Contains(a.unit_id);
        bool bReady = _queueReadyLookup.Contains(b.unit_id);
        if (aReady != bReady)
            return aReady ? -1 : 1;
        if (a.action_progress != b.action_progress)
            return b.action_progress.CompareTo(a.action_progress);
        if (a.current_ap != b.current_ap)
            return b.current_ap.CompareTo(a.current_ap);
        return string.Compare(a.unit_id.ToString(), b.unit_id.ToString(), StringComparison.Ordinal);
    }

    private static bool IsLivingUnit(BattleState battleState, StringName unitId)
    {
        if (battleState == null || IsEmpty(unitId))
            return false;
        BattleUnitState unitState = GetUnit(battleState, unitId);
        return unitState != null && unitState.is_alive;
    }

    private static int GetSnapshotValue(
        BattleUnitState unitState,
        StringName attributeId,
        int fallback
    )
    {
        if (unitState?.attribute_snapshot == null)
            return fallback;
        return unitState.attribute_snapshot.GetValue(attributeId);
    }

    private static float GetRatio(int currentValue, int maxValue)
    {
        return Mathf.Clamp(currentValue / (float)Mathf.Max(maxValue, 1), 0.0f, 1.0f);
    }

    private static BattleUnitState GetUnitAtCoord(BattleState battleState, Vector2I coord)
    {
        if (battleState == null)
            return null;
        foreach (BattleUnitState unitState in battleState.Units())
        {
            if (unitState != null && unitState.is_alive && unitState.OccupiesCoord(coord))
                return unitState;
        }
        return null;
    }

    private static string FormatPhase(StringName phase)
    {
        string phaseText = phase.ToString();
        if (string.IsNullOrEmpty(phaseText))
            return "无";
        return phaseText.Capitalize().Replace("_", " ");
    }

    private static string FormatControlMode(StringName controlMode)
    {
        if (controlMode == new StringName("manual"))
            return "手动";
        if (controlMode == new StringName("ai"))
            return "自动";
        return !IsEmpty(controlMode) ? controlMode.ToString() : "手动";
    }

    private static string FormatCoord(Vector2I coord)
    {
        return $"({coord.X}, {coord.Y})";
    }

    private static string FormatUnitName(BattleUnitState unitState, string fallbackText)
    {
        if (unitState == null)
            return fallbackText;
        if (!string.IsNullOrEmpty(unitState.display_name))
            return unitState.display_name;
        return unitState.unit_id.ToString();
    }

    private static string FormatTerrainName(BattleCellState cell)
    {
        if (cell == null)
            return "无";
        return BattleTerrainRules.GetDisplayName(cell.base_terrain);
    }

    private static BattleUnitState GetUnit(BattleState battleState, StringName unitId)
    {
        if (battleState == null || IsEmpty(unitId))
            return null;
        return battleState.GetUnit(unitId);
    }

    private static BattleCellState GetCell(BattleState battleState, Vector2I coord)
    {
        return battleState?.GetCell(coord);
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (skillDefinitions == null || IsEmpty(skillId))
            return null;
        return skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private static ItemDefinition GetItemDefinition(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        StringName itemId
    )
    {
        if (itemDefinitions == null || IsEmpty(itemId))
            return null;
        return itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
            ? itemDefinition
            : null;
    }

    private static List<Vector2I> CloneVector2IList(IEnumerable<Vector2I> source)
    {
        var result = new List<Vector2I>();
        if (source != null)
            result.AddRange(source);
        return result;
    }

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> source)
    {
        var result = new List<StringName>();
        if (source == null)
            return result;
        foreach (StringName id in source)
        {
            if (!IsEmpty(id))
                result.Add(id);
        }
        return result;
    }

    private static StringName NormalizeStringName(StringName value)
    {
        return value ?? new StringName("");
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
