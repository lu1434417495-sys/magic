using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleHudAdapter : RefCounted
{
    private const int QUEUE_ENTRY_LIMIT = 7;
    private const int SKILL_GRID_SIZE = 20;
    private const int CHANGE_EQUIPMENT_AP_COST = 2;
    private const string EquipmentPreviewDefaultFailureMessage = "该实例当前不能装备。";

    private static readonly StringName TARGET_SELECTION_MULTI_UNIT = "multi_unit";

    private readonly HashSet<StringName> _queueReadyLookup = new();
    private string _equipmentPreviewCacheSignature = "";
    private readonly Dictionary<string, EquipmentPreviewRule> _equipmentPreviewCache = new();
    private readonly BattleGridService _gridService = new();
    private readonly BattleHitResolver _hitResolver = new();
    private readonly BattleAttackCheckPolicyService _attackCheckPolicyService = new();
    private readonly BattleSkillResolutionRules _skillResolutionRules = new();
    private GameRuntimeFacade _runtime;
    private GameSession _gameSession;

    private sealed class EquipmentPreviewRule
    {
        public bool allowed;
        public StringName slot_id = "";
        public string message = "";
    }

    public static string EQUIPMENT_PREVIEW_DEFAULT_FAILURE_MESSAGE() =>
        EquipmentPreviewDefaultFailureMessage;

    public void setup_runtime_context(GameRuntimeFacade runtime, GameSession gameSession = null)
    {
        _runtime = runtime;
        _gameSession = gameSession;
    }

    public GDictionary build_snapshot(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id = default,
        string selected_skill_name = "",
        string selected_skill_variant_name = "",
        GVector2IArray selected_skill_target_coords = null,
        int selected_skill_required_coord_count = 0,
        GStringNameArray selected_skill_target_unit_ids = null,
        StringName selected_skill_variant_id = default,
        string encounter_display_name = "",
        BattlePreview selected_skill_runtime_preview = null
    )
    {
        selected_skill_id = NormalizeStringName(selected_skill_id);
        selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        if (battle_state == null)
            return new GDictionary();

        GVector2IArray targetCoords = CloneVector2IArray(selected_skill_target_coords);
        GStringNameArray targetUnitIds = CloneStringNameArray(selected_skill_target_unit_ids);
        BattleUnitState activeUnit = GetUnit(battle_state, battle_state.active_unit_id);
        BattleCellState selectedCell = GetCell(battle_state, selected_coord);
        BattleUnitState selectedUnit = GetUnitAtCoord(battle_state, selected_coord);
        BattleUnitState focusUnit = selectedUnit ?? activeUnit;
        int selectedTargetCount = targetCoords.Count;
        BattlePreview runtimePreview =
            selected_skill_runtime_preview
            ?? BuildSelectedSkillRuntimePreview(
                battle_state,
                activeUnit,
                selected_coord,
                selected_skill_id,
                targetCoords,
                targetUnitIds,
                selected_skill_variant_id
            );
        GDictionary selectionInfo = BuildSkillTargetSelectionInfo(
            battle_state,
            activeUnit,
            selected_skill_id,
            selectedTargetCount
        );
        AttackPreviewData hitPreview = BuildSelectedSkillHitPreview(
            battle_state,
            activeUnit,
            selected_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id,
            runtimePreview
        );
        GDictionary damagePreview = BuildSelectedSkillDamagePreview(
            battle_state,
            activeUnit,
            selected_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id
        );
        GDictionary fatePreview = BuildSelectedSkillFatePreview(
            battle_state,
            activeUnit,
            selected_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id
        );
        string tooltipText = BuildSelectedSkillPreviewTooltip(
            hitPreview,
            fatePreview,
            damagePreview
        );
        string headerTitle = !string.IsNullOrWhiteSpace(encounter_display_name)
            ? encounter_display_name
            : "战斗地图";

        return new GDictionary
        {
            ["header_title"] = headerTitle,
            ["header_subtitle"] = BuildHeaderSubtitle(battle_state, activeUnit),
            ["round_badge"] = BuildRoundBadge(battle_state),
            ["mode_text"] = FormatControlMode(
                activeUnit != null ? activeUnit.control_mode : new StringName("manual")
            ),
            ["queue_entries"] = BuildQueueEntries(battle_state),
            ["focus_unit"] = BuildFocusUnitSnapshot(focusUnit, battle_state),
            ["skill_title"] = BuildSkillTitle(selected_skill_name, selected_skill_variant_name),
            ["skill_subtitle"] = BuildSkillSubtitle(
                activeUnit,
                selected_skill_name,
                selected_skill_variant_name,
                selectedTargetCount,
                selected_skill_required_coord_count,
                selectionInfo,
                hitPreview,
                damagePreview
            ),
            ["skill_slots"] = BuildSkillSlots(activeUnit, selected_skill_id),
            ["tile_text"] = BuildTileText(selected_coord, selectedCell, selectedUnit),
            ["selected_skill_hit_preview_text"] = hitPreview?.SummaryText ?? "",
            ["selected_skill_hit_preview_payload"] = hitPreview ?? new Variant(),
            ["selected_skill_hit_badge_text"] = BuildSelectedSkillHitBadgeText(hitPreview),
            ["selected_skill_hit_stage_rates"] = hitPreview?.StageSuccessRates?.Duplicate(true)
                ?? new GIntArray(),
            ["selected_skill_damage_preview_text"] = DictString(damagePreview, "summary_text"),
            ["selected_skill_damage_min"] = DictInt(damagePreview, "min_damage"),
            ["selected_skill_damage_max"] = DictInt(damagePreview, "max_damage"),
            ["selected_skill_fate_preview_text"] = DictString(fatePreview, "summary_text"),
            ["selected_skill_fate_badges"] = DictArray(fatePreview, "badges").Duplicate(true),
            ["selected_skill_preview_tooltip_text"] = tooltipText,
            ["selected_skill_target_selection_mode"] = DictStringName(
                    selectionInfo,
                    "selection_mode",
                    "single_unit"
                )
                .ToString(),
            ["selected_skill_target_min_count"] = DictInt(selectionInfo, "min_target_count", 1),
            ["selected_skill_target_max_count"] = DictInt(selectionInfo, "max_target_count", 1),
            ["selected_skill_target_count"] = selectedTargetCount,
            ["selected_skill_confirm_ready"] = DictBool(selectionInfo, "confirm_ready"),
            ["selected_skill_auto_cast_ready"] = DictBool(selectionInfo, "auto_cast_ready"),
            ["equipment_panel"] = BuildEquipmentPanelSnapshot(
                battle_state,
                activeUnit
            ),
        };
    }

    public GDictionary build_hover_preview(
        BattleState battle_state,
        Vector2I hover_coord,
        StringName selected_skill_id = default,
        StringName selected_skill_variant_id = default,
        GVector2IArray valid_target_coords = null
    )
    {
        selected_skill_id = NormalizeStringName(selected_skill_id);
        selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        var result = new GDictionary
        {
            ["hover_coord"] = hover_coord,
            ["hover_is_valid_target"] = false,
            ["has_selected_skill"] = !IsEmpty(selected_skill_id),
            ["hit_preview"] = new GDictionary(),
            ["hit_stage_rates"] = new GArray(),
            ["hit_badge_text"] = "",
            ["fate_badges"] = new GArray(),
            ["damage_min"] = 0,
            ["damage_max"] = 0,
            ["damage_text"] = "",
            ["target_unit"] = new GDictionary(),
        };
        if (battle_state == null || !battle_state.cells.ContainsKey(hover_coord))
            return result;

        BattleUnitState hoveredUnit = GetUnitAtCoord(battle_state, hover_coord);
        if (hoveredUnit != null)
            result["target_unit"] = BuildHoverTargetUnitSnapshot(hoveredUnit, battle_state);

        if (IsEmpty(selected_skill_id))
            return result;

        GVector2IArray normalizedValid = CloneVector2IArray(valid_target_coords);
        bool isValidTarget = normalizedValid.Contains(hover_coord);
        result["hover_is_valid_target"] = isValidTarget;
        if (!isValidTarget)
            return result;

        BattleUnitState activeUnit = GetUnit(battle_state, battle_state.active_unit_id);
        if (activeUnit == null)
            return result;

        var targetCoords = new GVector2IArray { hover_coord };
        var targetUnitIds = new GStringNameArray();
        if (hoveredUnit != null)
            targetUnitIds.Add(hoveredUnit.unit_id);
        BattlePreview runtimePreview = BuildSelectedSkillRuntimePreview(
            battle_state,
            activeUnit,
            hover_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id
        );
        AttackPreviewData hitPreview = BuildSelectedSkillHitPreview(
            battle_state,
            activeUnit,
            hover_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id,
            runtimePreview
        );
        GDictionary damagePreview = BuildSelectedSkillDamagePreview(
            battle_state,
            activeUnit,
            hover_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id
        );
        GDictionary fatePreview = BuildSelectedSkillFatePreview(
            battle_state,
            activeUnit,
            hover_coord,
            selected_skill_id,
            targetCoords,
            targetUnitIds,
            selected_skill_variant_id
        );

        result["hit_preview"] = hitPreview ?? new Variant();
        result["hit_stage_rates"] = hitPreview?.StageSuccessRates?.Duplicate(true) ?? new GIntArray();
        result["hit_badge_text"] = BuildSelectedSkillHitBadgeText(hitPreview);
        result["fate_badges"] = DictArray(fatePreview, "badges").Duplicate(true);
        result["damage_min"] = DictInt(damagePreview, "min_damage");
        result["damage_max"] = DictInt(damagePreview, "max_damage");
        result["damage_text"] = DictString(damagePreview, "summary_text");
        return result;
    }

    public string _build_selected_skill_hit_badge_text(AttackPreviewData hit_preview)
    {
        return BuildSelectedSkillHitBadgeText(hit_preview);
    }

    private GDictionary BuildHoverTargetUnitSnapshot(
        BattleUnitState unitState,
        BattleState battleState
    )
    {
        if (unitState == null)
            return new GDictionary();

        GDictionary portraitData = BuildPortraitData(unitState, battleState);
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
        return new GDictionary
        {
            ["unit_id"] = unitState.unit_id,
            ["name"] = FormatUnitName(unitState, "单位"),
            ["glyph"] = portraitData.GetValueOrDefault("glyph", "?"),
            ["portrait_key"] = portraitData.GetValueOrDefault("portrait_key", ""),
            ["primary_color"] = DictColor(
                portraitData,
                "primary_color",
                new Color(0.62f, 0.47f, 0.32f, 1.0f)
            ),
            ["edge_color"] = DictColor(
                portraitData,
                "edge_color",
                new Color(0.93f, 0.77f, 0.5f, 1.0f)
            ),
            ["hp_current"] = unitState.current_hp,
            ["hp_max"] = Mathf.Max(hpMax, 1),
            ["mp_current"] = unitState.current_mp,
            ["mp_max"] = Mathf.Max(mpMax, 1),
            ["mp_visible"] = IsResourceUnlocked(unitState, BattleUnitState.COMBAT_RESOURCE_MP()),
            ["stamina_current"] = unitState.current_stamina,
            ["stamina_max"] = Mathf.Max(staminaMax, 1),
            ["aura_current"] = unitState.current_aura,
            ["aura_max"] = Mathf.Max(auraMax, 1),
            ["aura_visible"] = IsResourceUnlocked(
                unitState,
                BattleUnitState.COMBAT_RESOURCE_AURA()
            ),
            ["ap_current"] = unitState.current_ap,
            ["ap_max"] = Mathf.Max(apMax, 1),
            ["is_enemy"] = isEnemy,
            ["is_self"] = isSelf,
        };
    }

    private string BuildHeaderSubtitle(BattleState battleState, BattleUnitState activeUnit)
    {
        return $"阶段 {FormatPhase(battleState.phase)}  |  友军 {battleState.ally_unit_ids.Count}  |  敌军 {battleState.enemy_unit_ids.Count}  |  当前 {FormatUnitName(activeUnit, "无")}";
    }

    private GDictionary BuildRoundBadge(BattleState battleState)
    {
        if (battleState.timeline == null)
            return new GDictionary { ["tu_text"] = "TU --", ["ready_text"] = "READY 0" };
        return new GDictionary
        {
            ["tu_text"] = $"TU {battleState.timeline.current_tu}",
            ["ready_text"] = $"READY {battleState.timeline.ready_unit_ids.Count}",
        };
    }

    private GArray BuildQueueEntries(BattleState battleState)
    {
        var queueEntries = new GArray();
        if (battleState == null)
            return queueEntries;

        _queueReadyLookup.Clear();
        if (battleState.timeline != null)
        {
            foreach (StringName unitId in battleState.timeline.ready_unit_ids)
                _queueReadyLookup.Add(unitId);
        }

        var orderedIds = new GStringNameArray();
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
        foreach (var unitValue in battleState.units.Values)
        {
            BattleUnitState unitState = unitValue.AsGodotObject() as BattleUnitState;
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
            GDictionary portraitData = BuildPortraitData(unitState, battleState);
            int hpMax = GetSnapshotValue(unitState, "hp_max", 1);
            queueEntries.Add(
                new GDictionary
                {
                    ["slot_index"] = index + 1,
                    ["name"] = FormatUnitName(unitState, "单位"),
                    ["glyph"] = portraitData.GetValueOrDefault("glyph", "?"),
                    ["portrait_key"] = portraitData.GetValueOrDefault("portrait_key", ""),
                    ["primary_color"] = DictColor(
                        portraitData,
                        "primary_color",
                        new Color(0.62f, 0.47f, 0.32f, 1.0f)
                    ),
                    ["secondary_color"] = DictColor(
                        portraitData,
                        "secondary_color",
                        new Color(0.2f, 0.12f, 0.08f, 1.0f)
                    ),
                    ["edge_color"] = DictColor(
                        portraitData,
                        "edge_color",
                        new Color(0.93f, 0.77f, 0.5f, 1.0f)
                    ),
                    ["hp_ratio"] = GetRatio(unitState.current_hp, hpMax),
                    ["hp_text"] = $"HP {unitState.current_hp}/{hpMax}",
                    ["ap_text"] =
                        $"AP {unitState.current_ap} / 行动 {unitState.current_move_points}",
                    ["is_active"] = unitId == battleState.active_unit_id,
                    ["is_ready"] = _queueReadyLookup.Contains(unitId),
                    ["is_enemy"] = battleState.enemy_unit_ids.Contains(unitId),
                }
            );
        }

        if (orderedIds.Count > QUEUE_ENTRY_LIMIT)
        {
            queueEntries.Add(
                new GDictionary
                {
                    ["is_overflow"] = true,
                    ["overflow_text"] = $"+{orderedIds.Count - QUEUE_ENTRY_LIMIT}",
                }
            );
        }
        return queueEntries;
    }

    private GDictionary BuildFocusUnitSnapshot(BattleUnitState unitState, BattleState battleState)
    {
        if (unitState == null)
        {
            return new GDictionary
            {
                ["name"] = "待命",
                ["role_text"] = "未选中单位",
                ["resource_info"] = BuildResourceInfo(null),
                ["glyph"] = "?",
                ["portrait_key"] = "",
                ["primary_color"] = new Color(0.42f, 0.3f, 0.22f, 1.0f),
                ["secondary_color"] = new Color(0.16f, 0.1f, 0.07f, 1.0f),
                ["edge_color"] = new Color(0.88f, 0.72f, 0.48f, 1.0f),
                ["hp_current"] = 0,
                ["hp_max"] = 1,
                ["mp_current"] = 0,
                ["mp_max"] = 1,
                ["stamina_current"] = 0,
                ["stamina_max"] = 1,
                ["aura_current"] = 0,
                ["aura_max"] = 1,
                ["ap_current"] = 0,
                ["ap_max"] = 1,
                ["move_current"] = 0,
                ["move_max"] = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            };
        }

        GDictionary portraitData = BuildPortraitData(unitState, battleState);
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
        int moveMax = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();
        return new GDictionary
        {
            ["name"] = FormatUnitName(unitState, "单位"),
            ["role_text"] = BuildFocusRoleText(unitState, battleState),
            ["resource_info"] = BuildResourceInfo(unitState),
            ["glyph"] = portraitData.GetValueOrDefault("glyph", "?"),
            ["portrait_key"] = portraitData.GetValueOrDefault("portrait_key", ""),
            ["primary_color"] = DictColor(
                portraitData,
                "primary_color",
                new Color(0.62f, 0.47f, 0.32f, 1.0f)
            ),
            ["secondary_color"] = DictColor(
                portraitData,
                "secondary_color",
                new Color(0.2f, 0.12f, 0.08f, 1.0f)
            ),
            ["edge_color"] = DictColor(
                portraitData,
                "edge_color",
                new Color(0.93f, 0.77f, 0.5f, 1.0f)
            ),
            ["hp_current"] = unitState.current_hp,
            ["hp_max"] = Mathf.Max(hpMax, 1),
            ["mp_current"] = unitState.current_mp,
            ["mp_max"] = Mathf.Max(mpMax, 1),
            ["stamina_current"] = unitState.current_stamina,
            ["stamina_max"] = Mathf.Max(staminaMax, 1),
            ["aura_current"] = unitState.current_aura,
            ["aura_max"] = Mathf.Max(auraMax, 1),
            ["ap_current"] = unitState.current_ap,
            ["ap_max"] = Mathf.Max(apMax, 1),
            ["move_current"] = unitState.current_move_points,
            ["move_max"] = moveMax,
        };
    }

    private GDictionary BuildResourceInfo(BattleUnitState unitState)
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
        int moveMax = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();
        return new GDictionary
        {
            ["hp"] = ResourceLine(hpCurrent, Mathf.Max(hpMax, 1), "HP", true),
            ["mp"] = ResourceLine(
                mpCurrent,
                Mathf.Max(mpMax, 1),
                "MP",
                IsResourceUnlocked(unitState, BattleUnitState.COMBAT_RESOURCE_MP())
            ),
            ["stamina"] = ResourceLine(staminaCurrent, Mathf.Max(staminaMax, 1), "ST", true),
            ["aura"] = ResourceLine(
                auraCurrent,
                Mathf.Max(auraMax, 1),
                "AU",
                IsResourceUnlocked(unitState, BattleUnitState.COMBAT_RESOURCE_AURA())
            ),
            ["ap"] = ResourceLine(apCurrent, Mathf.Max(apMax, 1), "AP", true),
            ["move"] = ResourceLine(moveCurrent, moveMax, "MOVE", true),
        };
    }

    private GDictionary ResourceLine(int current, int max, string label, bool visible)
    {
        return new GDictionary
        {
            ["current"] = current,
            ["max"] = Mathf.Max(max, 1),
            ["ratio"] = GetRatio(current, max),
            ["label"] = label,
            ["visible"] = visible,
        };
    }

    private bool IsResourceUnlocked(BattleUnitState unitState, StringName resourceId)
    {
        return unitState != null && unitState.has_combat_resource_unlocked(resourceId);
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
        GDictionary selectionInfo,
        AttackPreviewData hitPreview,
        GDictionary damagePreview
    )
    {
        if (activeUnit == null)
            return "无可行动单位";
        if (string.IsNullOrEmpty(selectedSkillName))
            return $"当前单位 {FormatUnitName(activeUnit, "单位")}  ·  已装备技能 {activeUnit.known_active_skill_ids.Count}";
        string title = BuildSkillTitle(selectedSkillName, selectedSkillVariantName);
        if (DictBool(selectionInfo, "is_multi_unit"))
        {
            int minTargetCount = DictInt(selectionInfo, "min_target_count", 1);
            int maxTargetCount = DictInt(
                selectionInfo,
                "max_target_count",
                Mathf.Max(requiredCount, 1)
            );
            if (selectedCount <= 0)
                return $"当前技能 {title}  ·  左键逐个点选目标单位";
            if (selectedCount < minTargetCount)
                return $"当前技能 {title}  ·  已锁定 {selectedCount} 个目标，仍未达到最少 {minTargetCount} 个，继续点选";
            if (selectedCount < maxTargetCount)
                return $"当前技能 {title}  ·  已锁定 {selectedCount} 个目标，最少 {minTargetCount} / 最多 {maxTargetCount} 个，已满足最小数量，可点击自己或空地确认；继续点选将自动施放";
            return $"当前技能 {title}  ·  已锁定 {selectedCount} 个目标，已达到上限 {maxTargetCount} 个，将自动施放";
        }

        var previewParts = new List<string>();
        string hitPreviewText = hitPreview?.SummaryText ?? "";
        if (!string.IsNullOrEmpty(hitPreviewText))
            previewParts.Add(hitPreviewText);
        string damagePreviewText = DictString(damagePreview, "summary_text");
        if (!string.IsNullOrEmpty(damagePreviewText))
            previewParts.Add(damagePreviewText);
        if (previewParts.Count > 0)
            return $"当前技能 {title}  ·  {string.Join("  ·  ", previewParts)}";
        if (requiredCount <= 1)
            return $"当前技能 {title}  ·  左键选择目标格释放";
        return $"当前技能 {title}  ·  选点 {selectedCount}/{requiredCount}";
    }

    private GArray BuildSkillSlots(BattleUnitState activeUnit, StringName selectedSkillId)
    {
        var skillSlots = new GArray();
        GDictionary skillDefs = GetSkillDefs();
        if (activeUnit != null)
        {
            int count = Mathf.Min(activeUnit.known_active_skill_ids.Count, SKILL_GRID_SIZE);
            for (int index = 0; index < count; index++)
            {
                StringName skillId = activeUnit.known_active_skill_ids[index];
                SkillDef skillDef = GetSkillDef(skillDefs, skillId);
                string displayName = GetSkillDisplayName(skillDef, skillId);
                string iconKey = GetSkillIconKey(skillDef, skillId);
                Color accentColor = BuildSkillColor(iconKey, displayName);
                GDictionary slotState = BuildSkillSlotState(activeUnit, skillDef, skillId);
                string description = skillDef != null ? skillDef.description.StripEdges() : "";
                skillSlots.Add(
                    new GDictionary
                    {
                        ["index"] = index,
                        ["is_empty"] = false,
                        ["display_name"] = displayName,
                        ["short_name"] = BuildSkillShortName(displayName),
                        ["description"] = description,
                        ["icon_key"] = iconKey,
                        ["hotkey"] = index < 9 ? (index + 1).ToString() : "",
                        ["footer_text"] = DictString(slotState, "footer_text"),
                        ["is_selected"] = skillId == selectedSkillId,
                        ["is_disabled"] = DictBool(slotState, "is_disabled"),
                        ["accent_color"] = accentColor,
                        ["accent_dark"] = accentColor.Darkened(0.48f),
                        ["edge_color"] = accentColor.Lightened(0.16f),
                        ["cooldown"] = DictInt(slotState, "cooldown"),
                        ["disabled_reason"] = DictString(slotState, "disabled_reason"),
                    }
                );
            }
        }

        for (int index = skillSlots.Count; index < SKILL_GRID_SIZE; index++)
            skillSlots.Add(new GDictionary { ["index"] = index, ["is_empty"] = true });
        return skillSlots;
    }

    private GDictionary BuildEquipmentPanelSnapshot(
        BattleState battleState,
        BattleUnitState activeUnit
    )
    {
        var snapshot = new GDictionary
        {
            ["title"] = "队伍共享背包（战斗局部）",
            ["meta"] = "仅显示本场战斗复制出的队伍共享背包；据点共享仓库入口战中不可用。",
            ["active_unit_id"] = "",
            ["active_unit_name"] = "无当前行动单位",
            ["ap_cost"] = CHANGE_EQUIPMENT_AP_COST,
            ["can_change_equipment"] = false,
            ["disabled_reason"] = "当前没有可换装单位。",
            ["slots"] = new GArray(),
            ["backpack_entries"] = new GArray(),
            ["summary_text"] = "battle-local view 尚未就绪。",
        };
        if (battleState == null)
            return snapshot;
        if (activeUnit != null)
        {
            snapshot["active_unit_id"] = activeUnit.unit_id.ToString();
            snapshot["active_unit_name"] = FormatUnitName(activeUnit, "当前行动单位");
        }

        string disabledReason = GetChangeEquipmentDisabledReason(battleState, activeUnit);
        snapshot["can_change_equipment"] = string.IsNullOrEmpty(disabledReason);
        snapshot["disabled_reason"] = disabledReason;
        snapshot["slots"] = BuildEquipmentSlotEntries(activeUnit);
        snapshot["backpack_entries"] = BuildBackpackEquipmentEntries(
            battleState,
            activeUnit,
            disabledReason
        );
        snapshot["summary_text"] =
            $"当前行动单位：{DictString(snapshot, "active_unit_name", "无")}  |  换装消耗 {CHANGE_EQUIPMENT_AP_COST} AP  |  背包装备实例 {DictArray(snapshot, "backpack_entries").Count} 件";
        return snapshot;
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
        if (battleState.phase != new StringName("unit_acting"))
            return "当前阶段不能换装。";
        if (!IsEmpty(battleState.modal_state))
            return "当前有待处理的战斗流程，暂时无法换装。";
        if (activeUnit.control_mode != new StringName("manual"))
            return "当前行动单位不是手动控制，不能换装。";
        if (activeUnit.current_ap < CHANGE_EQUIPMENT_AP_COST)
            return $"AP不足，换装需要 {CHANGE_EQUIPMENT_AP_COST} 点 AP。";
        return "";
    }

    private GArray BuildEquipmentSlotEntries(BattleUnitState activeUnit)
    {
        var entries = new GArray();
        GDictionary itemDefs = GetItemDefs();
        EquipmentState equipmentView = activeUnit?.get_equipment_view() as EquipmentState;
        foreach (StringName slotId in EquipmentRules.get_all_slot_ids())
        {
            var slotEntry = new GDictionary
            {
                ["slot_id"] = slotId.ToString(),
                ["slot_label"] = EquipmentRules.get_slot_label(slotId),
                ["is_filled"] = false,
                ["is_entry_slot"] = false,
                ["entry_slot_id"] = "",
                ["item_id"] = "",
                ["item_display_name"] = "空",
                ["instance_id"] = "",
                ["occupied_slot_ids"] = new GStringArray(),
                ["occupied_slot_labels"] = new GStringArray(),
                ["can_unequip"] = false,
                ["disabled_reason"] = "",
            };
            if (equipmentView == null)
            {
                entries.Add(slotEntry);
                continue;
            }

            StringName itemId = ProgressionDataUtils.to_string_name(
                equipmentView.get_equipped_item_id(slotId)
            );
            StringName entrySlotId = ProgressionDataUtils.to_string_name(
                equipmentView.get_entry_slot_for_slot(slotId)
            );
            if (IsEmpty(itemId) || IsEmpty(entrySlotId))
            {
                entries.Add(slotEntry);
                continue;
            }
            GStringNameArray occupiedSlotIds = equipmentView.get_occupied_slot_ids_for_entry(
                entrySlotId
            );
            slotEntry["is_filled"] = true;
            slotEntry["is_entry_slot"] = entrySlotId == slotId;
            slotEntry["entry_slot_id"] = entrySlotId.ToString();
            slotEntry["item_id"] = itemId.ToString();
            slotEntry["item_display_name"] = GetItemDisplayName(itemDefs, itemId);
            slotEntry["instance_id"] = ProgressionDataUtils
                .to_string_name(equipmentView.get_equipped_instance_id(slotId))
                .ToString();
            slotEntry["occupied_slot_ids"] = StringifyStringNameArray(occupiedSlotIds);
            slotEntry["occupied_slot_labels"] = BuildSlotLabels(occupiedSlotIds);
            slotEntry["can_unequip"] = entrySlotId == slotId;
            if (entrySlotId != slotId)
                slotEntry["disabled_reason"] =
                    $"该槽位由 {EquipmentRules.get_slot_label(entrySlotId)} 占用，请从入口槽卸下。";
            entries.Add(slotEntry);
        }
        return entries;
    }

    private GArray BuildBackpackEquipmentEntries(
        BattleState battleState,
        BattleUnitState activeUnit,
        string disabledReason
    )
    {
        var entries = new GArray();
        GDictionary itemDefs = GetItemDefs();
        WarehouseState backpackView = battleState?.get_party_backpack_view();
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

        foreach (EquipmentInstanceState instance in backpackView.get_non_empty_instances())
        {
            if (instance == null)
                continue;
            StringName itemId = ProgressionDataUtils.to_string_name(instance.item_id);
            StringName instanceId = ProgressionDataUtils.to_string_name(instance.instance_id);
            ItemDef itemDef = GetItemDef(itemDefs, itemId);
            var allowedSlotIds = new GStringNameArray();
            string entryDisabledReason = disabledReason;
            if (itemDef == null)
            {
                if (string.IsNullOrEmpty(entryDisabledReason))
                    entryDisabledReason = $"找不到装备定义：{itemId}。";
            }
            else if (!itemDef.is_equipment())
            {
                if (string.IsNullOrEmpty(entryDisabledReason))
                    entryDisabledReason =
                        $"{GetItemDisplayName(itemDefs, itemId)} 不是可装备物品。";
            }
            else
            {
                allowedSlotIds = itemDef.get_equipment_slot_ids();
                if (allowedSlotIds.Count == 0 && string.IsNullOrEmpty(entryDisabledReason))
                    entryDisabledReason =
                        $"{GetItemDisplayName(itemDefs, itemId)} 当前没有可用装备槽。";
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
                new GDictionary
                {
                    ["instance_id"] = instanceId.ToString(),
                    ["item_id"] = itemId.ToString(),
                    ["display_name"] = GetItemDisplayName(itemDefs, itemId),
                    ["description"] = GetItemDescription(itemDef),
                    ["icon"] = GetItemIcon(itemDef),
                    ["allowed_slot_ids"] = StringifyStringNameArray(allowedSlotIds),
                    ["allowed_slot_labels"] = BuildSlotLabels(allowedSlotIds),
                    ["default_slot_id"] = defaultSlot.ToString(),
                    ["occupied_slot_ids_by_default"] = StringifyStringNameArray(
                        GetFinalOccupiedSlotIds(itemDef, defaultSlot)
                    ),
                    ["can_equip"] = string.IsNullOrEmpty(entryDisabledReason),
                    ["disabled_reason"] = entryDisabledReason,
                }
            );
        }
        return entries;
    }

    private EquipmentPreviewRule PreviewBackpackEquipmentEntryChange(
        BattleUnitState activeUnit,
        StringName itemId,
        StringName instanceId,
        GStringNameArray allowedSlotIds,
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
            BattlePreview preview = _runtime.preview_battle_command(command);
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
        GStringNameArray allowedSlotIds
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
            BuildEquipmentViewSignature(activeUnit?.get_equipment_view() as EquipmentState),
            BuildBackpackViewSignature(backpackView),
            _runtime != null ? _runtime.GetInstanceId().ToString() : "",
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
            foreach (string key in ProgressionDataUtils.sorted_string_keys(progression.professions))
            {
                StringName professionId = ProgressionDataUtils.to_string_name(key);
                UnitProfessionProgress profession = progression.get_profession_progress(
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
        foreach (StringName slotId in EquipmentRules.get_all_slot_ids())
        {
            var occupiedSlotIds = new GStringNameArray();
            StringName entrySlotId = ProgressionDataUtils.to_string_name(
                equipmentView.get_entry_slot_for_slot(slotId)
            );
            if (!IsEmpty(entrySlotId))
                occupiedSlotIds = equipmentView.get_occupied_slot_ids_for_entry(entrySlotId);
            parts.Add(
                $"{slotId}:{ProgressionDataUtils.to_string_name(equipmentView.get_equipped_item_id(slotId))}:{ProgressionDataUtils.to_string_name(equipmentView.get_equipped_instance_id(slotId))}:{JoinStringNameArray(occupiedSlotIds)}"
            );
        }
        return string.Join(";", parts);
    }

    private static string BuildBackpackViewSignature(WarehouseState backpackView)
    {
        if (backpackView == null)
            return "-";
        var parts = new List<string>();
        foreach (EquipmentInstanceState instance in backpackView.get_non_empty_instances())
        {
            if (instance == null)
                continue;
            parts.Add(
                $"{ProgressionDataUtils.to_string_name(instance.instance_id)}:{ProgressionDataUtils.to_string_name(instance.item_id)}:{instance.rarity}:{instance.current_durability}"
            );
        }
        return string.Join(";", parts);
    }

    private static string JoinStringNameArray(GStringNameArray values)
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
            command_type = BattleCommand.TYPE_CHANGE_EQUIPMENT(),
            unit_id = activeUnit.unit_id,
            target_unit_id = activeUnit.unit_id,
            equipment_operation = BattleCommand.EQUIPMENT_OPERATION_EQUIP(),
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
        return preview.log_lines.Count > 0 ? preview.log_lines[^1].ToString() : "";
    }

    private static GStringNameArray GetFinalOccupiedSlotIds(ItemDef itemDef, StringName entrySlotId)
    {
        if (itemDef == null || IsEmpty(entrySlotId))
            return new GStringNameArray();
        return itemDef.get_final_occupied_slot_ids(entrySlotId);
    }

    private GDictionary GetItemDefs()
    {
        if (_runtime != null)
            return _runtime.get_item_defs();
        return _gameSession != null ? _gameSession.get_item_defs() : new GDictionary();
    }

    private static string GetItemDisplayName(GDictionary itemDefs, StringName itemId)
    {
        ItemDef itemDef = GetItemDef(itemDefs, itemId);
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.display_name))
            return itemDef.display_name;
        return itemId.ToString();
    }

    private static string GetItemDescription(ItemDef itemDef)
    {
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.description))
            return itemDef.description;
        return "暂无说明。";
    }

    private static string GetItemIcon(ItemDef itemDef)
    {
        return itemDef != null ? itemDef.icon : "";
    }

    private static GStringArray BuildSlotLabels(GStringNameArray slotIds)
    {
        var labels = new GStringArray();
        if (slotIds != null)
        {
            foreach (StringName slotId in slotIds)
                labels.Add(EquipmentRules.get_slot_label(slotId));
        }
        return labels;
    }

    private static GStringArray StringifyStringNameArray(GStringNameArray values)
    {
        var result = new GStringArray();
        if (values != null)
        {
            foreach (StringName value in values)
                result.Add(value.ToString());
        }
        return result;
    }

    private GDictionary BuildSkillSlotState(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        StringName skillId
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        GDictionary costs = GetEffectiveSkillCosts(activeUnit, skillDef);
        int apCost = DictInt(costs, "ap_cost", combatProfile?.ap_cost ?? 0);
        int mpCost = DictInt(costs, "mp_cost", combatProfile?.mp_cost ?? 0);
        int staminaCost = DictInt(costs, "stamina_cost", combatProfile?.stamina_cost ?? 0);
        int auraCost = DictInt(costs, "aura_cost", combatProfile?.aura_cost ?? 0);
        int cooldown = activeUnit != null ? DictionaryInt(activeUnit.cooldowns, skillId, 0) : 0;
        if (cooldown > 0)
        {
            return new GDictionary
            {
                ["footer_text"] = $"CD {cooldown}",
                ["is_disabled"] = true,
                ["cooldown"] = cooldown,
                ["disabled_reason"] = $"冷却中（{cooldown}）",
            };
        }

        if (activeUnit != null)
        {
            string lockedReason = GetLockedCombatResourceBlockReason(activeUnit, costs);
            if (!string.IsNullOrEmpty(lockedReason))
            {
                return new GDictionary
                {
                    ["footer_text"] = GetLockedCombatResourceFooterText(activeUnit, costs),
                    ["is_disabled"] = true,
                    ["cooldown"] = cooldown,
                    ["disabled_reason"] = lockedReason,
                };
            }
            if (activeUnit.current_ap < apCost)
                return DisabledSkillSlot("AP不足", cooldown, "AP不足");
            if (activeUnit.current_mp < mpCost)
                return DisabledSkillSlot("MP不足", cooldown, "法力不足");
            if (activeUnit.current_stamina < staminaCost)
                return DisabledSkillSlot("ST不足", cooldown, "体力不足");
            if (activeUnit.current_aura < auraCost)
                return DisabledSkillSlot("AU不足", cooldown, "斗气不足");
        }

        return new GDictionary
        {
            ["footer_text"] = BuildSkillFooter(apCost, mpCost, staminaCost, auraCost, cooldown),
            ["is_disabled"] = false,
            ["cooldown"] = cooldown,
            ["disabled_reason"] = "",
        };
    }

    private static GDictionary DisabledSkillSlot(string footerText, int cooldown, string reason)
    {
        return new GDictionary
        {
            ["footer_text"] = footerText,
            ["is_disabled"] = true,
            ["cooldown"] = cooldown,
            ["disabled_reason"] = reason,
        };
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

    private GDictionary BuildPortraitData(BattleUnitState unitState, BattleState battleState)
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
        GDictionary palette = BuildPortraitPalette(portraitKey, isEnemy);
        return new GDictionary
        {
            ["portrait_key"] = portraitKey,
            ["glyph"] = BuildUnitGlyph(unitState),
            ["primary_color"] = DictColor(
                palette,
                "primary_color",
                new Color(0.62f, 0.47f, 0.32f, 1.0f)
            ),
            ["secondary_color"] = DictColor(
                palette,
                "secondary_color",
                new Color(0.2f, 0.12f, 0.08f, 1.0f)
            ),
            ["edge_color"] = DictColor(palette, "edge_color", new Color(0.93f, 0.77f, 0.5f, 1.0f)),
        };
    }

    private static GDictionary BuildPortraitPalette(string portraitKey, bool isEnemy)
    {
        string normalizedKey = portraitKey.ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        if (normalizedKey.Contains("sword"))
        {
            return new GDictionary
            {
                ["primary_color"] = new Color(0.28f, 0.55f, 0.85f, 1.0f),
                ["secondary_color"] = new Color(0.1f, 0.18f, 0.32f, 1.0f),
                ["edge_color"] = new Color(0.96f, 0.83f, 0.54f, 1.0f),
            };
        }
        if (normalizedKey.Contains("axe"))
        {
            return new GDictionary
            {
                ["primary_color"] = new Color(0.78f, 0.34f, 0.22f, 1.0f),
                ["secondary_color"] = new Color(0.28f, 0.09f, 0.05f, 1.0f),
                ["edge_color"] = new Color(0.98f, 0.77f, 0.44f, 1.0f),
            };
        }
        if (normalizedKey.Contains("spear"))
        {
            return new GDictionary
            {
                ["primary_color"] = new Color(0.24f, 0.72f, 0.53f, 1.0f),
                ["secondary_color"] = new Color(0.07f, 0.2f, 0.14f, 1.0f),
                ["edge_color"] = new Color(0.96f, 0.85f, 0.52f, 1.0f),
            };
        }

        int hashValue = Math.Abs((int)StringExtensions.Hash(normalizedKey));
        float hue = (hashValue % 360) / 360.0f;
        Color baseColor = Color.FromHsv(
            hue,
            isEnemy ? 0.72f : 0.46f,
            isEnemy ? 0.82f : 0.88f,
            1.0f
        );
        return new GDictionary
        {
            ["primary_color"] = baseColor,
            ["secondary_color"] = baseColor.Darkened(0.62f),
            ["edge_color"] = isEnemy
                ? new Color(0.9f, 0.46f, 0.3f, 1.0f)
                : new Color(0.94f, 0.79f, 0.5f, 1.0f),
        };
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

    private static string GetSkillDisplayName(SkillDef skillDef, StringName skillId)
    {
        if (skillDef != null && !string.IsNullOrEmpty(skillDef.display_name))
            return skillDef.display_name;
        return skillId.ToString();
    }

    private static string GetSkillIconKey(SkillDef skillDef, StringName skillId)
    {
        if (skillDef != null && !IsEmpty(skillDef.icon_id))
            return skillDef.icon_id.ToString();
        return skillId.ToString();
    }

    private GDictionary GetSkillDefs()
    {
        if (_runtime != null)
            return _runtime.get_skill_defs();
        return _gameSession != null ? _gameSession.get_skill_defs() : new GDictionary();
    }

    private BattlePreview BuildSelectedSkillRuntimePreview(
        BattleState battleState,
        BattleUnitState activeUnit,
        Vector2I selectedCoord,
        StringName selectedSkillId,
        GVector2IArray selectedSkillTargetCoords,
        GStringNameArray selectedSkillTargetUnitIds,
        StringName selectedSkillVariantId
    )
    {
        if (
            battleState == null
            || activeUnit == null
            || IsEmpty(selectedSkillId)
            || _runtime == null
        )
            return null;

        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = activeUnit.unit_id,
            skill_id = selectedSkillId,
            skill_variant_id = selectedSkillVariantId,
            target_coord = selectedCoord,
            target_coords = selectedSkillTargetCoords.Duplicate(),
            target_unit_ids = selectedSkillTargetUnitIds.Duplicate(),
        };
        if (command.target_unit_ids.Count == 1)
            command.target_unit_id = command.target_unit_ids[0];
        return _runtime.preview_battle_command(command);
    }

    private AttackPreviewData BuildSelectedSkillHitPreview(
        BattleState battleState,
        BattleUnitState activeUnit,
        Vector2I selectedCoord,
        StringName selectedSkillId,
        GVector2IArray selectedSkillTargetCoords,
        GStringNameArray selectedSkillTargetUnitIds,
        StringName selectedSkillVariantId,
        BattlePreview selectedSkillPreview = null
    )
    {
        if (battleState == null || activeUnit == null || IsEmpty(selectedSkillId))
            return null;

        _attackCheckPolicyService.setup(null, _hitResolver, null);
        if (selectedSkillPreview?.special_profile_preview_facts != null)
        {
            BattleSpecialProfilePreviewFacts facts =
                selectedSkillPreview.special_profile_preview_facts;
            GDictionary factsPayload = facts.to_dict();
            string summaryText = selectedSkillPreview.hit_preview?.SummaryText;
            if (string.IsNullOrEmpty(summaryText))
            {
                summaryText =
                    $"陨星雨影响 {DictInt(factsPayload, "impact_count", selectedSkillPreview.target_coords.Count)} 格、预计波及 {DictInt(factsPayload, "expected_target_count", selectedSkillPreview.target_unit_ids.Count)} 个单位。";
            }
            return new AttackPreviewData
            {
                SummaryText = summaryText,
                Source = "special_profile_preview_facts",
                AttackRollModifierBreakdown = (GArray)facts.attack_roll_modifier_breakdown.Duplicate(true),
            };
        }
        if (selectedSkillPreview != null && selectedSkillPreview.hit_preview != null && !selectedSkillPreview.hit_preview.IsEmpty)
            return selectedSkillPreview.hit_preview;

        SkillDef skillDef = GetSkillDef(GetSkillDefs(), selectedSkillId);
        if (skillDef?.combat_profile == null)
            return null;
        BattleUnitState targetUnit = ResolveSelectedSkillPreviewTargetUnit(
            battleState,
            activeUnit,
            selectedCoord,
            selectedSkillTargetCoords,
            selectedSkillTargetUnitIds,
            skillDef
        );
        if (targetUnit == null)
            return null;
        GStringNameArray previewTargetUnitIds = BuildSelectedSkillPreviewTargetUnitIds(
            selectedSkillTargetUnitIds,
            targetUnit,
            skillDef
        );
        GDictionary resolutionPolicy = _skillResolutionRules.build_skill_resolution_policy(
            skillDef,
            activeUnit,
            selectedSkillVariantId,
            previewTargetUnitIds,
            targetUnit
        );
        if (!DictBool(resolutionPolicy, "routes_to_unit_targeting"))
            return null;

        GCombatEffectArray effectDefs = CollectCombatEffectDefs(
            DictArray(resolutionPolicy, "effect_defs")
        );
        CombatEffectDef repeatAttackEffect =
            _skillResolutionRules.find_repeat_attack_effect(effectDefs);
        if (repeatAttackEffect == null)
        {
            if (!DictBool(resolutionPolicy, "uses_fate_attack"))
                return null;
            BattleAttackCheckPolicyContext attackContext =
                _attackCheckPolicyService.build_attack_context(
                    battleState,
                    activeUnit,
                    targetUnit,
                    skillDef,
                    "skill_attack_preview",
                    "hud_preview",
                    DictBool(resolutionPolicy, "force_hit_no_crit")
                );
            return _attackCheckPolicyService.build_attack_preview(attackContext);
        }

        List<BattleRepeatAttackStageSpec> stageSpecs =
            BattleRepeatAttackResolver.build_stage_specs_from_repeat_attack_effect(
                activeUnit,
                skillDef,
                repeatAttackEffect,
                -1,
                true
            );
        BattleAttackCheckPolicyContext repeatContext =
            _attackCheckPolicyService.build_repeat_attack_stage_context(
                battleState,
                activeUnit,
                targetUnit,
                skillDef,
                default,
                "repeat_attack_preview",
                "hud_preview"
            );
        return _attackCheckPolicyService.build_repeat_attack_preview(repeatContext, stageSpecs);
    }

    private GDictionary BuildSelectedSkillDamagePreview(
        BattleState battleState,
        BattleUnitState activeUnit,
        Vector2I selectedCoord,
        StringName selectedSkillId,
        GVector2IArray selectedSkillTargetCoords,
        GStringNameArray selectedSkillTargetUnitIds,
        StringName selectedSkillVariantId
    )
    {
        if (battleState == null || activeUnit == null || IsEmpty(selectedSkillId))
            return new GDictionary();
        SkillDef skillDef = GetSkillDef(GetSkillDefs(), selectedSkillId);
        if (skillDef?.combat_profile == null)
            return new GDictionary();
        BattleUnitState targetUnit = ResolveSelectedSkillPreviewTargetUnit(
            battleState,
            activeUnit,
            selectedCoord,
            selectedSkillTargetCoords,
            selectedSkillTargetUnitIds,
            skillDef
        );
        if (targetUnit == null)
            return new GDictionary();
        GStringNameArray previewTargetUnitIds = BuildSelectedSkillPreviewTargetUnitIds(
            selectedSkillTargetUnitIds,
            targetUnit,
            skillDef
        );
        GDictionary resolutionPolicy = _skillResolutionRules.build_skill_resolution_policy(
            skillDef,
            activeUnit,
            selectedSkillVariantId,
            previewTargetUnitIds,
            targetUnit
        );
        return BattleDamagePreviewRangeService.build_skill_damage_preview(
            activeUnit,
            ToUntypedCombatEffectArray(CollectCombatEffectDefs(DictArray(resolutionPolicy, "effect_defs")))
        );
    }

    private GDictionary BuildSelectedSkillFatePreview(
        BattleState battleState,
        BattleUnitState activeUnit,
        Vector2I selectedCoord,
        StringName selectedSkillId,
        GVector2IArray selectedSkillTargetCoords,
        GStringNameArray selectedSkillTargetUnitIds,
        StringName selectedSkillVariantId
    )
    {
        if (battleState == null || activeUnit == null || IsEmpty(selectedSkillId))
            return new GDictionary();
        SkillDef skillDef = GetSkillDef(GetSkillDefs(), selectedSkillId);
        if (skillDef?.combat_profile == null)
            return new GDictionary();
        BattleUnitState targetUnit = ResolveSelectedSkillPreviewTargetUnit(
            battleState,
            activeUnit,
            selectedCoord,
            selectedSkillTargetCoords,
            selectedSkillTargetUnitIds,
            skillDef
        );
        if (targetUnit == null)
            return new GDictionary();
        GStringNameArray previewTargetUnitIds = BuildSelectedSkillPreviewTargetUnitIds(
            selectedSkillTargetUnitIds,
            targetUnit,
            skillDef
        );
        GDictionary resolutionPolicy = _skillResolutionRules.build_skill_resolution_policy(
            skillDef,
            activeUnit,
            selectedSkillVariantId,
            previewTargetUnitIds,
            targetUnit
        );
        if (!DictBool(resolutionPolicy, "uses_fate_attack"))
            return new GDictionary();
        StringName previewMode = DictStringName(resolutionPolicy, "fate_preview_mode");
        if (previewMode == BattleSkillResolutionRules.FATE_PREVIEW_MODE_FORCE_HIT_NO_CRIT())
            return BuildForceHitNoCritFatePreview();
        return BuildStandardFatePreview(battleState, activeUnit, targetUnit);
    }

    private GDictionary BuildStandardFatePreview(
        BattleState battleState,
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        if (battleState == null || activeUnit == null || targetUnit == null)
            return new GDictionary();

        int effectiveLuck = GetEffectiveLuck(activeUnit);
        bool isDisadvantage = battleState.is_attack_disadvantage(activeUnit, targetUnit);
        int critGateDie = FateAttackFormula.CalcCritGateDieSize(effectiveLuck, isDisadvantage);
        int fumbleLowEnd = FateAttackFormula.CalcFumbleLowEnd(effectiveLuck);
        int critThreshold = FateAttackFormula.CalcCritThreshold(
            GetHiddenLuckAtBirth(activeUnit),
            GetFaithLuckBonus(activeUnit)
        );
        bool mercyActive = effectiveLuck <= -5 && isDisadvantage;
        var badges = new GArray
        {
            new GDictionary
            {
                ["text"] = isDisadvantage ? "劣势" : "未陷劣势",
                ["tone"] = isDisadvantage ? new StringName("warning") : new StringName("calm"),
                ["tooltip_text"] =
                    $"当前命中与命运骰按{(isDisadvantage ? "劣势取低" : "正常单骰")}口径结算。",
            },
            new GDictionary
            {
                ["text"] = $"暴击门 d{critGateDie}",
                ["tone"] = new StringName("gate"),
                ["tooltip_text"] = $"命运暴击门尺寸：d{critGateDie}。",
            },
            new GDictionary
            {
                ["text"] = fumbleLowEnd <= 1 ? "大失败 1" : $"大失败 1-{fumbleLowEnd}",
                ["tone"] = new StringName("danger"),
                ["tooltip_text"] = $"当前大失败区间：1-{fumbleLowEnd}。",
            },
        };
        var detailLines = new List<string>
        {
            "命运判定概览",
            $"状态：{(isDisadvantage ? "劣势中" : "未陷劣势")}",
            $"暴击门：d{critGateDie}",
            $"大失败：1-{fumbleLowEnd}",
        };
        if (critGateDie == 20)
        {
            string highThreatText = $"高位大成功 {critThreshold}-20";
            badges.Add(
                new GDictionary
                {
                    ["text"] = highThreatText,
                    ["tone"] = new StringName("high_threat"),
                    ["tooltip_text"] = $"当前高位大成功区间：{critThreshold}-20。",
                }
            );
            detailLines.Add($"高位大成功：{critThreshold}-20");
        }
        if (mercyActive)
        {
            badges.Add(
                new GDictionary
                {
                    ["text"] = "命运的怜悯",
                    ["tone"] = new StringName("mercy"),
                    ["tooltip_text"] = "effective_luck<=-5 且处于劣势时，暴击门只额外放大一档。",
                }
            );
            detailLines.Add("命运的怜悯：已生效");
        }

        return new GDictionary
        {
            ["summary_text"] = BuildFatePreviewSummaryText(badges),
            ["tooltip_text"] = string.Join("\n", detailLines),
            ["badges"] = badges,
            ["is_disadvantage"] = isDisadvantage,
            ["effective_luck"] = effectiveLuck,
            ["crit_gate_die"] = critGateDie,
            ["fumble_low_end"] = fumbleLowEnd,
            ["crit_threshold"] = critThreshold,
            ["mercy_active"] = mercyActive,
        };
    }

    private GDictionary BuildForceHitNoCritFatePreview()
    {
        var badges = new GArray
        {
            new GDictionary
            {
                ["text"] = "必定命中",
                ["tone"] = new StringName("calm"),
                ["tooltip_text"] = "这次攻击不会再进行命中骰判定，直接视为命中。",
            },
            new GDictionary
            {
                ["text"] = "禁暴击",
                ["tone"] = new StringName("warning"),
                ["tooltip_text"] = "这次攻击不会触发暴击。",
            },
            new GDictionary
            {
                ["text"] = "摆幅压低",
                ["tone"] = new StringName("gate"),
                ["tooltip_text"] = "这次攻击的命运摆幅已被压低，不再展示标准 crit/fumble 区间。",
            },
        };
        return new GDictionary
        {
            ["summary_text"] = BuildFatePreviewSummaryText(badges),
            ["tooltip_text"] =
                "命运判定概览\n状态：强制命中\n暴击：已封锁\n说明：这次攻击不再走标准命中/暴击/大失败骰。",
            ["badges"] = badges,
            ["force_hit_no_crit"] = true,
        };
    }

    private BattleUnitState ResolveSelectedSkillPreviewTargetUnit(
        BattleState battleState,
        BattleUnitState activeUnit,
        Vector2I selectedCoord,
        GVector2IArray selectedSkillTargetCoords,
        GStringNameArray selectedSkillTargetUnitIds,
        SkillDef skillDef
    )
    {
        BattleUnitState focusedTarget = GetUnitAtCoord(battleState, selectedCoord);
        if (CanPreviewSkillTargetUnit(activeUnit, focusedTarget, skillDef))
            return focusedTarget;
        foreach (StringName targetUnitId in selectedSkillTargetUnitIds)
        {
            BattleUnitState queuedTarget = GetUnit(battleState, targetUnitId);
            if (CanPreviewSkillTargetUnit(activeUnit, queuedTarget, skillDef))
                return queuedTarget;
        }
        foreach (Vector2I targetCoord in selectedSkillTargetCoords)
        {
            BattleUnitState queuedCoordTarget = GetUnitAtCoord(battleState, targetCoord);
            if (CanPreviewSkillTargetUnit(activeUnit, queuedCoordTarget, skillDef))
                return queuedCoordTarget;
        }
        return null;
    }

    private GStringNameArray BuildSelectedSkillPreviewTargetUnitIds(
        GStringNameArray selectedSkillTargetUnitIds,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        var targetUnitIds = selectedSkillTargetUnitIds?.Duplicate() ?? new GStringNameArray();
        if (targetUnit == null || skillDef?.combat_profile == null)
            return targetUnitIds;
        if (skillDef.combat_profile.target_selection_mode != TARGET_SELECTION_MULTI_UNIT)
            return targetUnitIds;
        if (targetUnitIds.Contains(targetUnit.unit_id))
            return targetUnitIds;
        targetUnitIds.Insert(0, targetUnit.unit_id);
        return targetUnitIds;
    }

    private bool CanPreviewSkillTargetUnit(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (activeUnit == null || targetUnit == null || skillDef?.combat_profile == null)
            return false;
        if (!targetUnit.is_alive || targetUnit.unit_id == activeUnit.unit_id)
            return false;
        if (!string.IsNullOrEmpty(GetSkillCastBlockReason(activeUnit, skillDef)))
            return false;
        if (
            !SkillTargetFilterMatchesUnit(
                activeUnit,
                targetUnit,
                skillDef.combat_profile.target_team_filter
            )
        )
            return false;
        return _gridService.get_distance_between_units(activeUnit, targetUnit)
            <= GetEffectiveSkillRange(activeUnit, skillDef);
    }

    private static string BuildFatePreviewSummaryText(GArray badges)
    {
        var parts = new List<string>();
        foreach (GDictionary badge in ReadDictionaryItems(badges))
        {
            parts.Add(DictString(badge, "text"));
        }
        return string.Join("  ·  ", parts);
    }

    private static string BuildSelectedSkillPreviewTooltip(
        AttackPreviewData hitPreview,
        GDictionary fatePreview,
        GDictionary damagePreview
    )
    {
        var sections = new List<string>();
        string hitText = hitPreview?.SummaryText ?? "";
        if (!string.IsNullOrEmpty(hitText))
            sections.Add(hitText);
        string damageText = DictString(damagePreview, "summary_text");
        if (!string.IsNullOrEmpty(damageText))
            sections.Add(damageText);
        string fateTooltip = DictString(fatePreview, "tooltip_text");
        if (!string.IsNullOrEmpty(fateTooltip))
            sections.Add(fateTooltip);
        return string.Join("\n\n", sections);
    }

    private static string BuildSelectedSkillHitBadgeText(AttackPreviewData hitPreview)
    {
        if (hitPreview == null)
            return "";
        int successRate = hitPreview.SuccessRatePercent;
        if (successRate <= 0 && hitPreview.StageSuccessRates.Count > 0)
            successRate = hitPreview.StageSuccessRates[0];
        if (successRate <= 0)
            return "";
        return $"命中 {Mathf.Clamp(successRate, 0, 100)}%";
    }

    private static int GetHiddenLuckAtBirth(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
            return 0;
        return unitState.attribute_snapshot.get_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH());
    }

    private static int GetFaithLuckBonus(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
            return 0;
        return unitState.attribute_snapshot.get_value(UnitBaseAttributes.FAITH_LUCK_BONUS());
    }

    private static int GetEffectiveLuck(BattleUnitState unitState)
    {
        return Mathf.Clamp(
            GetHiddenLuckAtBirth(unitState) + GetFaithLuckBonus(unitState),
            UnitBaseAttributes.EFFECTIVE_LUCK_MIN(),
            UnitBaseAttributes.EFFECTIVE_LUCK_MAX()
        );
    }

    private GDictionary BuildSkillTargetSelectionInfo(
        BattleState battleState,
        BattleUnitState activeUnit,
        StringName selectedSkillId,
        int selectedCount
    )
    {
        var defaultInfo = new GDictionary
        {
            ["selection_mode"] = new StringName("single_unit"),
            ["is_multi_unit"] = false,
            ["min_target_count"] = 1,
            ["max_target_count"] = Mathf.Max(selectedCount, 1),
            ["confirm_ready"] = false,
            ["auto_cast_ready"] = false,
        };
        if (battleState == null || activeUnit == null || IsEmpty(selectedSkillId))
            return defaultInfo;
        SkillDef skillDef = GetSkillDef(GetSkillDefs(), selectedSkillId);
        if (skillDef?.combat_profile == null)
            return defaultInfo;

        CombatSkillDef combatProfile = skillDef.combat_profile;
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        StringName selectionMode = combatProfile.target_selection_mode;
        if (IsEmpty(selectionMode))
            selectionMode = "single_unit";
        int minTargetCount = Mathf.Max(combatProfile.min_target_count, 1);
        int maxTargetCount = Mathf.Max(
            combatProfile.get_effective_max_target_count(skillLevel),
            minTargetCount
        );
        bool isMultiUnit = selectionMode == TARGET_SELECTION_MULTI_UNIT;
        bool confirmReady =
            isMultiUnit && selectedCount >= minTargetCount && selectedCount < maxTargetCount;
        bool autoCastReady = isMultiUnit && selectedCount >= maxTargetCount;
        return new GDictionary
        {
            ["selection_mode"] = selectionMode,
            ["is_multi_unit"] = isMultiUnit,
            ["min_target_count"] = minTargetCount,
            ["max_target_count"] = maxTargetCount,
            ["confirm_ready"] = confirmReady,
            ["auto_cast_ready"] = autoCastReady,
        };
    }

    private string GetSkillCastBlockReason(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (activeUnit == null || skillDef?.combat_profile == null)
            return "技能或目标无效。";
        CombatSkillDef combatProfile = skillDef.combat_profile;
        GDictionary costs = GetEffectiveSkillCosts(activeUnit, skillDef);
        int cooldown = DictionaryInt(activeUnit.cooldowns, skillDef.skill_id, 0);
        if (cooldown > 0)
            return $"{skillDef.display_name} 仍在冷却中（{cooldown}）。";
        string lockedReason = GetLockedCombatResourceBlockReason(activeUnit, costs);
        if (!string.IsNullOrEmpty(lockedReason))
            return lockedReason;
        if (activeUnit.current_ap < DictInt(costs, "ap_cost", combatProfile.ap_cost))
            return "AP不足，无法施放该技能。";
        if (activeUnit.current_mp < DictInt(costs, "mp_cost", combatProfile.mp_cost))
            return "法力不足，无法施放该技能。";
        if (activeUnit.current_stamina < DictInt(costs, "stamina_cost", combatProfile.stamina_cost))
            return "体力不足，无法施放该技能。";
        if (activeUnit.current_aura < DictInt(costs, "aura_cost", combatProfile.aura_cost))
            return "斗气不足，无法施放该技能。";
        return "";
    }

    private static string GetLockedCombatResourceBlockReason(
        BattleUnitState activeUnit,
        GDictionary costs
    )
    {
        if (activeUnit == null)
            return "技能施放者无效。";
        if (
            DictInt(costs, "mp_cost") > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP())
        )
            return "法力尚未解锁，无法施放该技能。";
        if (
            DictInt(costs, "stamina_cost") > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA())
        )
            return "体力尚未解锁，无法施放该技能。";
        if (
            DictInt(costs, "aura_cost") > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA())
        )
            return "斗气尚未解锁，无法施放该技能。";
        return "";
    }

    private static string GetLockedCombatResourceFooterText(
        BattleUnitState activeUnit,
        GDictionary costs
    )
    {
        if (activeUnit == null)
            return "资源未解锁";
        if (
            DictInt(costs, "mp_cost") > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP())
        )
            return "MP未解锁";
        if (
            DictInt(costs, "stamina_cost") > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA())
        )
            return "ST未解锁";
        if (
            DictInt(costs, "aura_cost") > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA())
        )
            return "AU未解锁";
        return "资源未解锁";
    }

    private static GDictionary GetEffectiveSkillCosts(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (skillDef?.combat_profile == null)
            return new GDictionary();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        return skillDef.combat_profile.get_effective_resource_costs(skillLevel);
    }

    private static int GetUnitSkillLevel(BattleUnitState activeUnit, StringName skillId)
    {
        if (activeUnit == null || IsEmpty(skillId))
            return 0;
        if (activeUnit.known_skill_level_map.ContainsKey(skillId))
            return activeUnit.known_skill_level_map[skillId].AsInt32();
        return activeUnit.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private static bool SkillTargetFilterMatchesUnit(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    )
    {
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            activeUnit,
            targetUnit,
            targetTeamFilter,
            default
        );
    }

    private static int GetEffectiveSkillRange(BattleUnitState activeUnit, SkillDef skillDef)
    {
        return BattleRangeService.get_effective_skill_range(activeUnit, skillDef);
    }

    private bool SkillHasTag(SkillDef skillDef, StringName expectedTag)
    {
        if (skillDef == null || IsEmpty(expectedTag))
            return false;
        foreach (StringName tag in skillDef.tags)
        {
            if (ProgressionDataUtils.to_string_name(tag) == expectedTag)
                return true;
        }
        return false;
    }

    private PartyMemberState GetPartyMemberState(StringName memberId)
    {
        if (IsEmpty(memberId))
            return null;
        PartyMemberState memberState = _runtime?.get_party_state()?.get_member_state(memberId);
        if (memberState != null)
            return memberState;
        return _gameSession?.get_party_member_state(memberId);
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
        return unitState.attribute_snapshot.get_value(attributeId);
    }

    private static float GetRatio(int currentValue, int maxValue)
    {
        return Mathf.Clamp(currentValue / (float)Mathf.Max(maxValue, 1), 0.0f, 1.0f);
    }

    private static BattleUnitState GetUnitAtCoord(BattleState battleState, Vector2I coord)
    {
        if (battleState == null)
            return null;
        foreach (var unitValue in battleState.units.Values)
        {
            BattleUnitState unitState = unitValue.AsGodotObject() as BattleUnitState;
            if (unitState != null && unitState.is_alive && unitState.occupies_coord(coord))
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
        return BattleTerrainRules.get_display_name(cell.base_terrain);
    }

    private static GCombatEffectArray CollectCombatEffectDefs(GArray values)
    {
        var result = new GCombatEffectArray();
        foreach (var value in values)
        {
            CombatEffectDef effectDef = value.AsGodotObject() as CombatEffectDef;
            if (effectDef != null)
                result.Add(effectDef);
        }
        return result;
    }

    private static GArray ToUntypedCombatEffectArray(GCombatEffectArray values)
    {
        var result = new GArray();
        foreach (CombatEffectDef value in values ?? new GCombatEffectArray())
        {
            result.Add(value);
        }
        return result;
    }

    private static BattleUnitState GetUnit(BattleState battleState, StringName unitId)
    {
        if (battleState == null || IsEmpty(unitId))
            return null;
        if (battleState.units.ContainsKey(unitId))
            return battleState.units[unitId].AsGodotObject() as BattleUnitState;
        string unitKey = unitId.ToString();
        if (battleState.units.ContainsKey(unitKey))
            return battleState.units[unitKey].AsGodotObject() as BattleUnitState;
        return null;
    }

    private static BattleCellState GetCell(BattleState battleState, Vector2I coord)
    {
        if (battleState == null || !battleState.cells.ContainsKey(coord))
            return null;
        return battleState.cells[coord].AsGodotObject() as BattleCellState;
    }

    private static SkillDef GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || IsEmpty(skillId))
            return null;
        if (skillDefs.ContainsKey(skillId))
            return skillDefs[skillId].AsGodotObject() as SkillDef;
        string stringKey = skillId.ToString();
        return skillDefs.ContainsKey(stringKey)
            ? skillDefs[stringKey].AsGodotObject() as SkillDef
            : null;
    }

    private static ItemDef GetItemDef(GDictionary itemDefs, StringName itemId)
    {
        if (itemDefs == null || IsEmpty(itemId))
            return null;
        if (TryRead(itemDefs, itemId, out Variant itemValue))
            return itemValue.AsGodotObject() as ItemDef;
        return null;
    }

    private static int DictionaryInt(GDictionary dict, object key, int fallback = 0)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : fallback;
    }

    private static string DictString(GDictionary dict, object key, string fallback = "")
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static int DictInt(GDictionary dict, object key, int fallback = 0)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : fallback;
    }

    private static bool DictBool(GDictionary dict, object key, bool fallback = false)
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static StringName DictStringName(
        GDictionary dict,
        object key,
        StringName fallback = default
    )
    {
        if (!TryRead(dict, key, out Variant value))
            return NormalizeStringName(fallback);
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => NormalizeStringName(fallback),
        };
    }

    private static GArray DictArray(GDictionary dict, object key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static Color DictColor(GDictionary dict, object key, Color fallback)
    {
        return
            TryRead(dict, key, out Variant value)
            && value.VariantType == Variant.Type.Color
            ? value.AsColor()
            : fallback;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool TryRead(GDictionary dict, object key, out Variant value)
    {
        if (dict == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = KeyToVariant(key);
        if (dict.ContainsKey(variantKey))
        {
            value = dict[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string stringKey = stringNameKey.ToString();
            if (dict.ContainsKey(stringKey))
            {
                value = dict[stringKey];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            StringName alternateStringNameKey = new(stringKey);
            if (dict.ContainsKey(alternateStringNameKey))
            {
                value = dict[alternateStringNameKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant KeyToVariant(object key)
    {
        return key switch
        {
            Variant variantKey => variantKey,
            StringName stringNameKey => stringNameKey,
            string stringKey => stringKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
    }

    private static GVector2IArray CloneVector2IArray(GVector2IArray source)
    {
        var result = new GVector2IArray();
        if (source == null)
            return result;
        foreach (Vector2I coord in source)
            result.Add(coord);
        return result;
    }

    private static GStringNameArray CloneStringNameArray(GStringNameArray source)
    {
        var result = new GStringNameArray();
        if (source == null)
            return result;
        foreach (StringName id in source)
            if (!IsEmpty(id))
                result.Add(id);
        return result;
    }

    private static GArray ToUntypedStringNameArray(GStringNameArray source)
    {
        var result = new GArray();
        if (source == null)
            return result;
        foreach (StringName id in source)
            if (!IsEmpty(id))
                result.Add(id);
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
