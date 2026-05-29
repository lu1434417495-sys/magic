using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class GameRuntimeBattleSelection : RefCounted
{
    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName CrownBreakSkillId = "crown_break";
    private static readonly StringName DoomSentenceSkillId = "doom_sentence";
    private static readonly StringName DoomShiftSkillId = "doom_shift";
    private static readonly StringName BlackCrownSealSkillId = "black_crown_seal";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";
    private static readonly StringName Overlay = "overlay";
    private static readonly StringName Full = "full";
    private static readonly StringName Error = "error";
    private static readonly StringName UnitTargetMode = "unit";
    private static readonly StringName GroundTargetMode = "ground";
    private static readonly StringName MultiUnitSelectionMode = "multi_unit";
    private static readonly StringName RandomChainSelectionMode = "random_chain";
    private static readonly StringName SelfSelectionMode = "self";
    private static readonly StringName EnemyFilter = "enemy";
    private static readonly StringName ForcedMoveEffectType = "forced_move";
    private static readonly StringName JumpMode = "jump";
    private static readonly StringName BlinkMode = "blink";

    private readonly BattleTargetCollectionService _targetCollectionService = new();
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade Runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    public void setup(GameRuntimeFacade runtime)
    {
        Runtime = runtime;
    }

    public void dispose()
    {
        Runtime = null;
    }

    public string get_selected_battle_skill_name()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        return skillDef?.display_name ?? "";
    }

    public string get_selected_battle_skill_variant_name()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        return castVariant?.display_name ?? "";
    }

    public GVector2IArray get_selected_battle_skill_target_coords()
    {
        return CollectSelectedBattleSkillTargetCoords();
    }

    public GStringNameArray get_selected_battle_skill_target_unit_ids()
    {
        return DuplicateStringNameArray(GetTargetUnitIdsState());
    }

    public GVector2IArray get_selected_battle_skill_valid_target_coords()
    {
        return CollectSelectedBattleSkillValidTargetCoords();
    }

    public int get_selected_battle_skill_required_coord_count()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (skillDef?.combat_profile != null)
        {
            StringName selectionMode = GetSelectedBattleSkillTargetSelectionMode(activeUnit);
            if (selectionMode == MultiUnitSelectionMode)
            {
                int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
                return Math.Max(
                    skillDef.combat_profile.get_effective_max_target_count(skillLevel),
                    skillDef.combat_profile.min_target_count
                );
            }
            if (skillDef.combat_profile.target_mode == UnitTargetMode)
            {
                return 1;
            }
        }
        return castVariant == null ? 0 : castVariant.required_coord_count;
    }

    public GDictionary select_battle_skill_slot(int index)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return SelectionError("当前没有可手动操作的单位。");
        }
        if (index < 0 || index >= activeUnit.known_active_skill_ids.Count)
        {
            UpdateStatus("该技能栏当前没有技能。");
            return SelectionError("该技能栏当前没有技能。");
        }

        StringName skillId = activeUnit.known_active_skill_ids[index];
        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef?.combat_profile == null)
        {
            UpdateStatus("该技能当前不可用于战斗。");
            return SelectionError("该技能当前不可用于战斗。");
        }

        if (GetSelectedSkillId() == skillId)
        {
            clear_battle_skill_selection(true);
            return SelectionOk();
        }

        string blockReason = GetSkillCastBlockReason(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return SelectionError(blockReason);
        }

        SetSelectedSkillId(skillId);
        SetSelectedSkillVariantId("");
        ClearBattleSkillTargetSelection();

        if (skillDef.combat_profile.target_selection_mode == RandomChainSelectionMode)
        {
            var chainCommand = new BattleCommand
            {
                command_type = BattleCommand.TYPE_SKILL(),
                unit_id = activeUnit.unit_id,
                skill_id = skillId,
                skill_variant_id = GetDefaultUnitSkillVariantId(activeUnit, skillDef),
                target_unit_ids = new GStringNameArray(),
            };
            BattlePreview chainPreview = PreviewBattleCommand(chainCommand);
            if (chainPreview != null && chainPreview.allowed)
            {
                IssueBattleCommand(chainCommand);
                return SelectionOk();
            }
            RefreshBattleSelectionState();
            UpdateStatus(
                chainPreview != null && chainPreview.log_lines.Count > 0
                    ? chainPreview.log_lines[chainPreview.log_lines.Count - 1].ToString()
                    : "无法执行连锁攻击。"
            );
            return SelectionError("无法执行连锁攻击。");
        }

        GArray unlockedOptions = GetUnlockedCastVariants(activeUnit, skillDef);
        if (
            unlockedOptions.Count > 0
            && unlockedOptions[0].AsGodotObject() is CombatCastVariantDef firstValue
        )
        {
            SetSelectedSkillVariantId(firstValue.variant_id);
        }
        RefreshBattleSelectionState();
        UpdateStatus(BuildBattleSkillSelectionStatus(skillDef, activeUnit));
        return SelectionOk();
    }

    public void cycle_selected_battle_skill_option(int step)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return;
        }
        if (GdInterop.IsEmpty(GetSelectedSkillId()))
        {
            UpdateStatus("请先用数字键选择一个技能。");
            return;
        }

        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null || skillDef.combat_profile.cast_variants.Count == 0)
        {
            UpdateStatus("当前技能没有可切换的施法形态。");
            return;
        }

        GArray unlockedOptions = GetUnlockedCastVariants(activeUnit, skillDef);
        if (unlockedOptions.Count == 0)
        {
            UpdateStatus("当前技能等级尚未解锁任何施法形态。");
            return;
        }

        int currentIndex = 0;
        for (int optionIndex = 0; optionIndex < unlockedOptions.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant =
                unlockedOptions[optionIndex].AsGodotObject() as CombatCastVariantDef;
            if (castVariant != null && castVariant.variant_id == GetSelectedSkillVariantId())
            {
                currentIndex = optionIndex;
                break;
            }
        }

        int nextIndex = PosMod(currentIndex + step, unlockedOptions.Count);
        if (unlockedOptions[nextIndex].AsGodotObject() is CombatCastVariantDef nextValue)
        {
            SetSelectedSkillVariantId(nextValue.variant_id);
        }
        ClearBattleSkillTargetSelection();
        RefreshBattleSelectionState();
        UpdateStatus(BuildBattleSkillSelectionStatus(skillDef, activeUnit));
    }

    public void clear_battle_skill_selection(bool announce = false)
    {
        SetSelectedSkillId("");
        SetSelectedSkillVariantId("");
        ClearBattleSkillTargetSelection();
        SetLastManualUnitId("");
        if (IsBattleActive())
        {
            RefreshBattleSelectionState();
        }
        if (announce)
        {
            UpdateStatus("已清除当前战斗技能选择。");
        }
    }

    public void sync_selected_battle_skill_state()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        StringName activeUnitId = activeUnit?.unit_id ?? new StringName("");
        if (activeUnitId != GetLastManualUnitId())
        {
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
        }
        SetLastManualUnitId(activeUnitId);
        if (activeUnit == null || GdInterop.IsEmpty(GetSelectedSkillId()))
        {
            return;
        }
        if (!activeUnit.known_active_skill_ids.Contains(GetSelectedSkillId()))
        {
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }

        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }
        if (skillDef.combat_profile.cast_variants.Count == 0)
        {
            SetSelectedSkillVariantId("");
            return;
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (castVariant == null)
        {
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }
        SetSelectedSkillVariantId(castVariant.variant_id);
    }

    public StringName attempt_battle_move_to(Vector2I target_coord)
    {
        if (!IsBattleActive())
        {
            return Full;
        }

        SetBattleSelectedCoord(target_coord);
        BattleState battleState = GetBattleState();
        if (battleState == null || !battleState.cells.ContainsKey(target_coord))
        {
            RefreshBattleSelectionState();
            UpdateStatus("该战斗格超出当前战场范围。");
            return Overlay;
        }

        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            RefreshBattleSelectionState();
            UpdateStatus("等待当前单位进入可操作状态。");
            return Overlay;
        }

        if (IsSelectedGroundSkillReady(activeUnit))
        {
            return HandleSelectedGroundSkillClick(activeUnit, target_coord);
        }

        BattleUnitState targetUnit = GetRuntimeUnitAtCoord(target_coord);
        if (targetUnit != null)
        {
            StringName selectedSkillResult = HandleSelectedUnitSkillClick(activeUnit, targetUnit);
            if (!GdInterop.IsEmpty(selectedSkillResult))
            {
                return selectedSkillResult;
            }

            BattleCommand skillCommand = BuildSelectedSkillCommand(activeUnit, targetUnit);
            if (skillCommand != null)
            {
                return IssueBattleCommand(skillCommand);
            }

            skillCommand = BuildSkillCommand(activeUnit, targetUnit);
            if (skillCommand != null)
            {
                return IssueBattleCommand(skillCommand);
            }
        }
        else if (GetSelectedBattleSkillTargetSelectionMode(activeUnit) == MultiUnitSelectionMode)
        {
            SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
            if (skillDef?.combat_profile != null)
            {
                int minTargetCount = Math.Max(skillDef.combat_profile.min_target_count, 1);
                if (GetTargetUnitIdsState().Count >= minTargetCount)
                {
                    return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
                }
            }
        }

        if (activeUnit.occupies_coord(target_coord))
        {
            RefreshBattleSelectionState();
            UpdateStatus("已选中当前行动单位。");
            return Overlay;
        }

        var moveCommand = new BattleCommand
        {
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = activeUnit.unit_id,
            target_coord = target_coord,
        };
        BattlePreview preview = PreviewBattleCommand(moveCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(moveCommand);
        }

        RefreshBattleSelectionState();
        if (preview != null && preview.log_lines.Count > 0)
        {
            UpdateStatus(preview.log_lines[preview.log_lines.Count - 1].ToString());
        }
        else
        {
            UpdateStatus($"已选中战斗格 {FormatCoord(target_coord)}。");
        }
        return Overlay;
    }

    public StringName reset_battle_movement()
    {
        if (!IsBattleActive())
        {
            return Full;
        }

        BattleUnitState activeUnit = GetRuntimeActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可聚焦的行动单位。");
            return Overlay;
        }

        SetBattleSelectedCoord(activeUnit.coord);
        RefreshBattleSelectionState();
        UpdateStatus("已聚焦当前行动单位。");
        return Overlay;
    }

    private BattleCommand BuildSkillCommand(BattleUnitState activeUnit, BattleUnitState targetUnit)
    {
        if (activeUnit == null || targetUnit == null)
        {
            return null;
        }

        foreach (StringName skillId in activeUnit.known_active_skill_ids)
        {
            SkillDef skillDef = GetSkillDef(skillId);
            if (skillDef?.combat_profile == null)
            {
                continue;
            }
            if (skillDef.combat_profile.target_mode != UnitTargetMode)
            {
                continue;
            }
            if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDef))
            {
                continue;
            }
            return new BattleCommand
            {
                command_type = BattleCommand.TYPE_SKILL(),
                unit_id = activeUnit.unit_id,
                skill_id = skillId,
                skill_variant_id = GetDefaultUnitSkillVariantId(activeUnit, skillDef),
                target_unit_id = targetUnit.unit_id,
                target_coord = targetUnit.coord,
            };
        }
        return null;
    }

    private BattleCommand BuildSelectedSkillCommand(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        if (activeUnit == null || targetUnit == null || GdInterop.IsEmpty(GetSelectedSkillId()))
        {
            return null;
        }

        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            return null;
        }
        if (skillDef.combat_profile.target_mode != UnitTargetMode)
        {
            return null;
        }
        if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDef))
        {
            return null;
        }
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = activeUnit.unit_id,
            skill_id = GetSelectedSkillId(),
            skill_variant_id = GetSelectedSkillVariantId(),
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.coord,
        };
    }

    private StringName GetDefaultUnitSkillVariantId(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (skillDef?.combat_profile == null || skillDef.combat_profile.cast_variants.Count == 0)
        {
            return "";
        }
        foreach (var rawValue in GetUnlockedCastVariants(activeUnit, skillDef))
        {
            CombatCastVariantDef castVariant = rawValue.AsGodotObject() as CombatCastVariantDef;
            if (castVariant != null && castVariant.target_mode == UnitTargetMode)
            {
                return castVariant.variant_id;
            }
        }
        return "";
    }

    private bool IsSelectedGroundSkillReady(BattleUnitState activeUnit)
    {
        if (GetSelectedBattleSkillTargetSelectionMode(activeUnit) == MultiUnitSelectionMode)
        {
            return false;
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        return castVariant != null && castVariant.target_mode == GroundTargetMode;
    }

    private StringName HandleSelectedGroundSkillClick(
        BattleUnitState activeUnit,
        Vector2I targetCoord
    )
    {
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (castVariant == null || skillDef == null)
        {
            RefreshBattleSelectionState();
            UpdateStatus("当前地面技能形态不可用。");
            return Overlay;
        }

        string blockReason = GetSkillCastBlockReason(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return Error;
        }

        int requiredCoordCount = Math.Max(castVariant.required_coord_count, 1);
        GVector2IArray queuedTargetCoords = GetTargetCoordsState();
        GVector2IArray previousTargets = DuplicateVector2IArray(queuedTargetCoords);
        int existingIndex = queuedTargetCoords.IndexOf(targetCoord);
        if (existingIndex >= 0)
        {
            queuedTargetCoords.RemoveAt(existingIndex);
            SetTargetCoordsState(queuedTargetCoords);
            RefreshBattleSelectionState();
            UpdateStatus($"已取消目标格 {FormatCoord(targetCoord)}。");
            return Overlay;
        }

        if (requiredCoordCount == 1)
        {
            SetTargetCoordsState(new GVector2IArray { targetCoord });
        }
        else
        {
            if (queuedTargetCoords.Count >= requiredCoordCount)
            {
                UpdateStatus(
                    $"该技能形态最多选择 {requiredCoordCount} 个地格；点击已选地格可取消。"
                );
                return Overlay;
            }
            queuedTargetCoords.Add(targetCoord);
            SetTargetCoordsState(queuedTargetCoords);
        }

        GVector2IArray resolvedTargetCoords = GetTargetCoordsState();
        if (resolvedTargetCoords.Count < requiredCoordCount)
        {
            RefreshBattleSelectionState();
            UpdateStatus(
                $"{BuildSkillVariantDisplayName(skillDef, castVariant)}：已选择 {resolvedTargetCoords.Count} / {requiredCoordCount} 个地格。"
            );
            return Overlay;
        }

        var skillCommand = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = activeUnit.unit_id,
            skill_id = GetSelectedSkillId(),
            skill_variant_id = castVariant.variant_id,
            target_coords = DuplicateVector2IArray(resolvedTargetCoords),
            target_coord = targetCoord,
        };

        BattlePreview preview = PreviewBattleCommand(skillCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(skillCommand);
        }

        SetTargetCoordsState(requiredCoordCount > 1 ? previousTargets : new GVector2IArray());
        RefreshBattleSelectionState();
        if (preview != null && preview.log_lines.Count > 0)
        {
            UpdateStatus(preview.log_lines[preview.log_lines.Count - 1].ToString());
        }
        else
        {
            UpdateStatus("当前地面技能目标无效。");
        }
        return Overlay;
    }

    private StringName HandleSelectedUnitSkillClick(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            return "";
        }
        string blockReason = GetSkillCastBlockReason(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return Error;
        }

        StringName selectionMode = skillDef.combat_profile.target_selection_mode;
        if (selectionMode == MultiUnitSelectionMode)
        {
            return ToggleSelectedMultiUnitSkillTarget(activeUnit, targetUnit, skillDef);
        }
        if (skillDef.combat_profile.target_mode != UnitTargetMode)
        {
            return "";
        }

        BattleCommand skillCommand = BuildSelectedSkillCommand(activeUnit, targetUnit);
        return skillCommand != null ? IssueBattleCommand(skillCommand) : new StringName("");
    }

    private SkillDef GetSelectedBattleSkillDef(BattleUnitState activeUnit)
    {
        if (activeUnit == null || GdInterop.IsEmpty(GetSelectedSkillId()))
        {
            return null;
        }
        if (!activeUnit.known_active_skill_ids.Contains(GetSelectedSkillId()))
        {
            return null;
        }
        return GetSkillDef(GetSelectedSkillId());
    }

    private CombatCastVariantDef GetSelectedBattleSkillVariant(BattleUnitState activeUnit)
    {
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            return null;
        }
        if (skillDef.combat_profile.cast_variants.Count == 0)
        {
            return skillDef.combat_profile.target_mode == GroundTargetMode
                ? BuildImplicitGroundCastVariant(skillDef)
                : null;
        }

        GArray unlockedOptions = GetUnlockedCastVariants(activeUnit, skillDef);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (GdInterop.IsEmpty(GetSelectedSkillVariantId()))
        {
            return unlockedOptions[0].AsGodotObject() as CombatCastVariantDef;
        }
        foreach (var rawValue in unlockedOptions)
        {
            CombatCastVariantDef castVariant = rawValue.AsGodotObject() as CombatCastVariantDef;
            if (castVariant != null && castVariant.variant_id == GetSelectedSkillVariantId())
            {
                return castVariant;
            }
        }
        return unlockedOptions[0].AsGodotObject() as CombatCastVariantDef;
    }

    private GArray GetUnlockedCastVariants(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return new GArray();
        }
        int defaultSkillLevel = activeUnit.known_active_skill_ids.Contains(skillDef.skill_id)
            ? 1
            : 0;
        if (IsLevelLessSkill(skillDef))
        {
            defaultSkillLevel = 0;
        }
        int skillLevel = GdInterop.GetInt(
            activeUnit.known_skill_level_map,
            skillDef.skill_id,
            defaultSkillLevel
        );
        return ToUntypedArray(skillDef.combat_profile.get_unlocked_cast_variants(skillLevel));
    }

    private static CombatCastVariantDef BuildImplicitGroundCastVariant(SkillDef skillDef)
    {
        if (
            skillDef?.combat_profile == null
            || skillDef.combat_profile.target_mode != GroundTargetMode
        )
        {
            return null;
        }
        return new CombatCastVariantDef
        {
            variant_id = "",
            display_name = "",
            target_mode = GroundTargetMode,
            footprint_pattern = "single",
            required_coord_count = 1,
            effect_defs = new Godot.Collections.Array<CombatEffectDef>(
                skillDef.combat_profile.effect_defs
            ),
        };
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        if (Runtime == null)
        {
            return null;
        }
        GDictionary skillDefs = Runtime.get_skill_defs();
        return GdInterop.GetObject(skillDefs, skillId) as SkillDef;
    }

    private GVector2IArray CollectSelectedBattleSkillValidTargetCoords()
    {
        if (!IsBattleActive())
        {
            return new GVector2IArray();
        }
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return new GVector2IArray();
        }
        if (!string.IsNullOrEmpty(GetSkillCastBlockReason(activeUnit, skillDef)))
        {
            return new GVector2IArray();
        }
        if (GetSelectedBattleSkillTargetSelectionMode(activeUnit) == MultiUnitSelectionMode)
        {
            return CollectValidUnitSkillTargetCoords(activeUnit, skillDef, GetTargetUnitIdsState());
        }
        if (skillDef.combat_profile.target_mode == UnitTargetMode)
        {
            return CollectValidUnitSkillTargetCoords(activeUnit, skillDef, GetTargetUnitIdsState());
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (castVariant == null || castVariant.target_mode != GroundTargetMode)
        {
            return new GVector2IArray();
        }
        return CollectValidGroundSkillTargetCoords(activeUnit, skillDef, castVariant);
    }

    private GVector2IArray CollectValidUnitSkillTargetCoords(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        GStringNameArray excludedUnitIds
    )
    {
        var coordSet = new HashSet<Vector2I>();
        BattleState battleState = GetBattleState();
        if (battleState == null || activeUnit == null || skillDef == null)
        {
            return new GVector2IArray();
        }

        var excludedUnitIdSet = new HashSet<StringName>();
        foreach (StringName excludedUnitId in excludedUnitIds ?? new GStringNameArray())
        {
            excludedUnitIdSet.Add(excludedUnitId);
        }
        bool useAnchorCoords =
            GetSelectedBattleSkillTargetSelectionMode(activeUnit) == MultiUnitSelectionMode;
        foreach (var rawUnit in battleState.units.Values)
        {
            BattleUnitState targetUnit = rawUnit.AsGodotObject() as BattleUnitState;
            if (targetUnit == null || excludedUnitIdSet.Contains(targetUnit.unit_id))
            {
                continue;
            }
            if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDef))
            {
                continue;
            }
            if (useAnchorCoords)
            {
                coordSet.Add(targetUnit.coord);
            }
            else
            {
                targetUnit.refresh_footprint();
                foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
                {
                    coordSet.Add(occupiedCoord);
                }
            }
        }
        return SortCoords(coordSet);
    }

    private GVector2IArray CollectValidGroundSkillTargetCoords(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        var coordSet = new HashSet<Vector2I>();
        BattleState battleState = GetBattleState();
        if (battleState == null || activeUnit == null || skillDef == null || castVariant == null)
        {
            return new GVector2IArray();
        }
        if (!string.IsNullOrEmpty(GetSkillCastBlockReason(activeUnit, skillDef)))
        {
            return new GVector2IArray();
        }

        GVector2IArray queuedCoords = DuplicateVector2IArray(GetTargetCoordsState());
        foreach (var rawCoord in battleState.cells.Keys)
        {
            if (rawCoord.VariantType != Variant.Type.Vector2I)
            {
                continue;
            }
            Vector2I targetCoord = rawCoord.AsVector2I();
            if (queuedCoords.Contains(targetCoord))
            {
                continue;
            }
            if (
                !IsNextGroundTargetCoordSelectable(
                    activeUnit,
                    skillDef,
                    castVariant,
                    queuedCoords,
                    targetCoord
                )
            )
            {
                continue;
            }
            coordSet.Add(targetCoord);
        }
        return SortCoords(coordSet);
    }

    private bool IsNextGroundTargetCoordSelectable(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        GVector2IArray queuedCoords,
        Vector2I candidateCoord
    )
    {
        if (!string.IsNullOrEmpty(GetSkillCastBlockReason(activeUnit, skillDef)))
        {
            return false;
        }
        GVector2IArray nextCoords = DuplicateVector2IArray(queuedCoords);
        nextCoords.Add(candidateCoord);
        if (!AreGroundTargetCoordsIndividuallyValid(activeUnit, skillDef, castVariant, nextCoords))
        {
            return false;
        }
        int requiredCoordCount = Math.Max(castVariant.required_coord_count, 1);
        if (nextCoords.Count >= requiredCoordCount)
        {
            return IsGroundTargetComboAllowed(activeUnit, skillDef, castVariant, nextCoords);
        }
        if (castVariant.footprint_pattern == "unordered")
        {
            return true;
        }
        foreach (GVector2IArray fullCoords in BuildGroundCompletionSets(castVariant, nextCoords))
        {
            if (
                !AreGroundTargetCoordsIndividuallyValid(
                    activeUnit,
                    skillDef,
                    castVariant,
                    fullCoords
                )
            )
            {
                continue;
            }
            if (IsGroundTargetComboAllowed(activeUnit, skillDef, castVariant, fullCoords))
            {
                return true;
            }
        }
        return false;
    }

    private bool AreGroundTargetCoordsIndividuallyValid(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        GVector2IArray targetCoords
    )
    {
        BattleState battleState = GetBattleState();
        BattleGridService battleGridService = GetBattleGridService();
        if (
            battleState == null
            || battleGridService == null
            || activeUnit == null
            || skillDef?.combat_profile == null
            || castVariant == null
        )
        {
            return false;
        }

        CombatEffectDef relocationEffectDef = ResolveGroundRelocationEffectDef(
            skillDef,
            castVariant
        );
        var seenCoords = new HashSet<Vector2I>();
        foreach (Vector2I coord in targetCoords ?? new GVector2IArray())
        {
            if (!seenCoords.Add(coord))
            {
                return false;
            }
            if (!battleState.cells.ContainsKey(coord))
            {
                return false;
            }
            int targetDistance =
                relocationEffectDef != null
                    ? battleGridService.get_chebyshev_distance(activeUnit.coord, coord)
                    : battleGridService.get_distance_from_unit_to_coord(activeUnit, coord);
            if (targetDistance > GetEffectiveSkillRange(activeUnit, skillDef))
            {
                return false;
            }
            BattleCellState cell = GdInterop.GetObject(battleState.cells, coord) as BattleCellState;
            if (cell == null)
            {
                return false;
            }
            if (castVariant.allowed_base_terrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.normalize_terrain_id(
                    cell.base_terrain
                );
                foreach (StringName allowedTerrain in castVariant.allowed_base_terrains)
                {
                    if (
                        BattleTerrainRules.normalize_terrain_id(allowedTerrain)
                        == normalizedCellTerrain
                    )
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    return false;
                }
            }
            if (
                IsCrownBreakSkill(skillDef)
                && !IsCrownBreakTargetEligible(activeUnit, GetRuntimeUnitAtCoord(coord))
            )
            {
                return false;
            }
            if (
                relocationEffectDef != null
                && !CanUseGroundRelocation(
                    battleState,
                    battleGridService,
                    activeUnit,
                    coord,
                    relocationEffectDef
                )
            )
            {
                return false;
            }
        }
        return true;
    }

    private static CombatEffectDef ResolveGroundRelocationEffectDef(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (castVariant != null)
        {
            foreach (CombatEffectDef effectDef in castVariant.effect_defs)
            {
                if (IsGroundRelocationEffect(effectDef))
                {
                    return effectDef;
                }
            }
        }
        if (skillDef?.combat_profile != null)
        {
            foreach (CombatEffectDef effectDef in skillDef.combat_profile.effect_defs)
            {
                if (IsGroundRelocationEffect(effectDef))
                {
                    return effectDef;
                }
            }
        }
        return null;
    }

    private static bool IsGroundRelocationEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null || effectDef.effect_type != ForcedMoveEffectType)
        {
            return false;
        }
        return effectDef.forced_move_mode == JumpMode || effectDef.forced_move_mode == BlinkMode;
    }

    private static bool CanUseGroundRelocation(
        BattleState battleState,
        BattleGridService battleGridService,
        BattleUnitState activeUnit,
        Vector2I coord,
        CombatEffectDef effectDef
    )
    {
        if (effectDef == null)
        {
            return false;
        }
        if (effectDef.forced_move_mode == JumpMode)
        {
            return battleGridService.can_jump_arc(battleState, activeUnit, coord, effectDef);
        }
        if (effectDef.forced_move_mode == BlinkMode)
        {
            return battleGridService.can_blink_to_coord(battleState, activeUnit, coord, effectDef);
        }
        return false;
    }

    private bool IsGroundTargetComboAllowed(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        GVector2IArray targetCoords
    )
    {
        if (activeUnit == null || skillDef == null || castVariant == null)
        {
            return false;
        }
        GVector2IArray sortedTargetCoords = SortCoords(targetCoords);
        var skillCommand = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = activeUnit.unit_id,
            skill_id = skillDef.skill_id,
            skill_variant_id = castVariant.variant_id,
            target_coords = sortedTargetCoords,
        };
        if (sortedTargetCoords.Count > 0)
        {
            skillCommand.target_coord = sortedTargetCoords[sortedTargetCoords.Count - 1];
        }
        BattlePreview preview = PreviewBattleCommand(skillCommand);
        return preview != null && preview.allowed;
    }

    private IEnumerable<GVector2IArray> BuildGroundCompletionSets(
        CombatCastVariantDef castVariant,
        GVector2IArray partialCoords
    )
    {
        if (castVariant == null)
        {
            yield break;
        }
        int requiredCoordCount = Math.Max(castVariant.required_coord_count, 1);
        if (partialCoords.Count > requiredCoordCount)
        {
            yield break;
        }
        if (castVariant.footprint_pattern == "single")
        {
            if (partialCoords.Count == 1)
            {
                yield return SortCoords(partialCoords);
            }
            yield break;
        }
        if (castVariant.footprint_pattern == "line2")
        {
            foreach (GVector2IArray completionSet in BuildLine2CompletionSets(partialCoords))
            {
                yield return completionSet;
            }
            yield break;
        }
        if (castVariant.footprint_pattern == "square2")
        {
            foreach (GVector2IArray completionSet in BuildSquare2CompletionSets(partialCoords))
            {
                yield return completionSet;
            }
            yield break;
        }
        if (
            castVariant.footprint_pattern == "unordered"
            && partialCoords.Count == requiredCoordCount
        )
        {
            yield return SortCoords(partialCoords);
        }
    }

    private IEnumerable<GVector2IArray> BuildLine2CompletionSets(GVector2IArray partialCoords)
    {
        var seenSignatures = new HashSet<string>();
        Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        foreach (Vector2I origin in partialCoords)
        {
            foreach (Vector2I direction in directions)
            {
                GVector2IArray candidatePair = SortCoords(new[] { origin, origin + direction });
                if (!CoordArrayContainsAll(candidatePair, partialCoords))
                {
                    continue;
                }
                string signature = BuildCoordSignature(candidatePair);
                if (seenSignatures.Add(signature))
                {
                    yield return candidatePair;
                }
            }
        }
    }

    private IEnumerable<GVector2IArray> BuildSquare2CompletionSets(GVector2IArray partialCoords)
    {
        var seenSignatures = new HashSet<string>();
        var candidateOrigins = new HashSet<Vector2I>();
        foreach (Vector2I coord in partialCoords)
        {
            foreach (
                Vector2I offset in new[]
                {
                    Vector2I.Zero,
                    Vector2I.Left,
                    Vector2I.Up,
                    new Vector2I(-1, -1),
                }
            )
            {
                candidateOrigins.Add(coord + offset);
            }
        }
        foreach (Vector2I origin in candidateOrigins)
        {
            GVector2IArray blockCoords = SortCoords(
                new[]
                {
                    origin,
                    origin + Vector2I.Right,
                    origin + Vector2I.Down,
                    origin + Vector2I.One,
                }
            );
            if (!CoordArrayContainsAll(blockCoords, partialCoords))
            {
                continue;
            }
            string signature = BuildCoordSignature(blockCoords);
            if (seenSignatures.Add(signature))
            {
                yield return blockCoords;
            }
        }
    }

    private static bool CoordArrayContainsAll(
        GVector2IArray fullCoords,
        GVector2IArray partialCoords
    )
    {
        var coordSet = new HashSet<Vector2I>(fullCoords);
        foreach (Vector2I coord in partialCoords)
        {
            if (!coordSet.Contains(coord))
            {
                return false;
            }
        }
        return true;
    }

    private static string BuildCoordSignature(GVector2IArray targetCoords)
    {
        var segments = new List<string>();
        foreach (Vector2I coord in SortCoords(targetCoords))
        {
            segments.Add($"{coord.X}:{coord.Y}");
        }
        return string.Join("|", segments);
    }

    private bool CanSkillTargetUnit(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (activeUnit == null || targetUnit == null || skillDef?.combat_profile == null)
        {
            return false;
        }
        if (!targetUnit.is_alive)
        {
            return false;
        }
        if (!string.IsNullOrEmpty(GetSkillCastBlockReason(activeUnit, skillDef)))
        {
            return false;
        }
        if (activeUnit.current_ap < skillDef.combat_profile.ap_cost)
        {
            return false;
        }
        if (
            !SkillTargetFilterMatchesUnit(
                activeUnit,
                targetUnit,
                skillDef.combat_profile.target_team_filter
            )
        )
        {
            return false;
        }
        if (IsCrownBreakSkill(skillDef) && !IsCrownBreakTargetEligible(activeUnit, targetUnit))
        {
            return false;
        }
        if (IsDoomSentenceSkill(skillDef) && !IsDoomSentenceTargetEligible(activeUnit, targetUnit))
        {
            return false;
        }
        if (
            IsBlackCrownSealSkill(skillDef)
            && !IsBlackCrownSealTargetEligible(activeUnit, targetUnit)
        )
        {
            return false;
        }
        if (IsDoomShiftSkill(skillDef) && targetUnit.unit_id == activeUnit.unit_id)
        {
            return false;
        }

        activeUnit.refresh_footprint();
        targetUnit.refresh_footprint();
        BattleGridService battleGridService = GetBattleGridService();
        if (battleGridService == null)
        {
            return false;
        }
        return battleGridService.get_distance_between_units(activeUnit, targetUnit)
            <= GetEffectiveSkillRange(activeUnit, skillDef);
    }

    private static bool IsCrownBreakSkill(SkillDef skillDef)
    {
        return skillDef != null
            && ProgressionDataUtils.to_string_name(skillDef.skill_id) == CrownBreakSkillId;
    }

    private static bool IsDoomSentenceSkill(SkillDef skillDef)
    {
        return skillDef != null
            && ProgressionDataUtils.to_string_name(skillDef.skill_id) == DoomSentenceSkillId;
    }

    private static bool IsDoomShiftSkill(SkillDef skillDef)
    {
        return skillDef != null
            && ProgressionDataUtils.to_string_name(skillDef.skill_id) == DoomShiftSkillId;
    }

    private static bool IsBlackCrownSealSkill(SkillDef skillDef)
    {
        return skillDef != null
            && ProgressionDataUtils.to_string_name(skillDef.skill_id) == BlackCrownSealSkillId;
    }

    private static bool IsCrownBreakTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        return targetUnit != null
            && SkillTargetFilterMatchesUnit(activeUnit, targetUnit, EnemyFilter)
            && targetUnit.has_status_effect(StatusBlackStarBrandElite);
    }

    private static bool IsDoomSentenceTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        return targetUnit != null
            && SkillTargetFilterMatchesUnit(activeUnit, targetUnit, EnemyFilter)
            && IsEliteOrBossTarget(targetUnit);
    }

    private static bool IsBlackCrownSealTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        return targetUnit != null
            && SkillTargetFilterMatchesUnit(activeUnit, targetUnit, EnemyFilter)
            && IsBossTarget(targetUnit);
    }

    private static bool IsEliteOrBossTarget(BattleUnitState targetUnit)
    {
        return GetAttributeValue(targetUnit, FortuneMarkTargetStatId) > 0;
    }

    private static bool IsBossTarget(BattleUnitState targetUnit)
    {
        return GetAttributeValue(targetUnit, BossTargetStatId) > 0
            || GetAttributeValue(targetUnit, FortuneMarkTargetStatId) > 1;
    }

    private static int GetAttributeValue(BattleUnitState unitState, StringName attributeId)
    {
        return unitState?.attribute_snapshot is AttributeSnapshot snapshot
            ? snapshot.get_value(attributeId)
            : 0;
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

    private string GetSkillCastBlockReason(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (Runtime != null)
        {
            return Runtime.get_battle_skill_cast_block_reason(activeUnit, skillDef);
        }
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return "技能或目标无效。";
        }

        CombatSkillDef combatProfile = skillDef.combat_profile;
        GDictionary costs = combatProfile.get_effective_resource_costs(
            GetUnitSkillLevel(activeUnit, skillDef.skill_id)
        );
        int cooldown = GdInterop.GetInt(activeUnit.cooldowns, skillDef.skill_id, 0);
        if (cooldown > 0)
        {
            return $"{skillDef.display_name} 仍在冷却中（{cooldown}）。";
        }
        string lockedResourceBlockReason = GetLockedCombatResourceBlockReason(activeUnit, costs);
        if (!string.IsNullOrEmpty(lockedResourceBlockReason))
        {
            return lockedResourceBlockReason;
        }
        if (activeUnit.current_ap < GdInterop.GetInt(costs, "ap_cost", combatProfile.ap_cost))
        {
            return "AP不足，无法施放该技能。";
        }
        if (activeUnit.current_mp < GdInterop.GetInt(costs, "mp_cost", combatProfile.mp_cost))
        {
            return "法力不足，无法施放该技能。";
        }
        if (
            activeUnit.current_stamina
            < GdInterop.GetInt(costs, "stamina_cost", combatProfile.stamina_cost)
        )
        {
            return "体力不足，无法施放该技能。";
        }
        if (activeUnit.current_aura < GdInterop.GetInt(costs, "aura_cost", combatProfile.aura_cost))
        {
            return "斗气不足，无法施放该技能。";
        }
        return "";
    }

    private static string GetLockedCombatResourceBlockReason(
        BattleUnitState activeUnit,
        GDictionary costs
    )
    {
        if (activeUnit == null)
        {
            return "技能施放者无效。";
        }
        if (
            GdInterop.GetInt(costs, "mp_cost", 0) > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP())
        )
        {
            return "法力尚未解锁，无法施放该技能。";
        }
        if (
            GdInterop.GetInt(costs, "stamina_cost", 0) > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA())
        )
        {
            return "体力尚未解锁，无法施放该技能。";
        }
        if (
            GdInterop.GetInt(costs, "aura_cost", 0) > 0
            && !activeUnit.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA())
        )
        {
            return "斗气尚未解锁，无法施放该技能。";
        }
        return "";
    }

    private int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || GdInterop.IsEmpty(skillId))
        {
            return 0;
        }
        if (unitState.known_skill_level_map.ContainsKey(skillId))
        {
            return GdInterop.GetInt(unitState.known_skill_level_map, skillId, 0);
        }
        SkillDef skillDef = GetSkillDef(skillId);
        if (IsLevelLessSkill(skillDef))
        {
            return 0;
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private static bool IsLevelLessSkill(SkillDef skillDef)
    {
        return skillDef != null
            && skillDef.max_level == 0
            && GdInterop.IsEmpty(skillDef.dynamic_max_level_stat_id);
    }

    private string BuildBattleSkillSelectionStatus(SkillDef skillDef, BattleUnitState activeUnit)
    {
        if (skillDef == null)
        {
            return "当前技能不可用。";
        }

        string blockReason = GetSkillCastBlockReason(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            return $"{blockReason}按 Esc 清除选择。";
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        StringName selectionMode = GetSelectedBattleSkillTargetSelectionMode(activeUnit);
        if (selectionMode == RandomChainSelectionMode)
        {
            return $"已选择技能 {skillDef.display_name}，将自动攻击范围内随机敌军。Esc 清除选择。";
        }
        if (selectionMode == MultiUnitSelectionMode)
        {
            int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
            int minTargetCount = Math.Max(skillDef.combat_profile.min_target_count, 1);
            int maxTargetCount = Math.Max(
                skillDef.combat_profile.get_effective_max_target_count(skillLevel),
                minTargetCount
            );
            return BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount);
        }
        if (castVariant == null)
        {
            if (
                skillDef.combat_profile.target_mode == UnitTargetMode
                && (
                    selectionMode == SelfSelectionMode
                    || skillDef.combat_profile.target_team_filter == SelfSelectionMode
                )
            )
            {
                return $"已选择技能 {skillDef.display_name}。点击自身即可施放，Esc 清除选择。";
            }
            return $"已选择技能 {skillDef.display_name}。左键选择目标单位施放，Esc 清除选择。";
        }
        if (skillDef.combat_profile.target_mode == UnitTargetMode)
        {
            if (
                selectionMode == SelfSelectionMode
                || skillDef.combat_profile.target_team_filter == SelfSelectionMode
            )
            {
                return $"已选择 {BuildSkillVariantDisplayName(skillDef, castVariant)}，点击自身即可施放，Esc 清除选择。";
            }
            return $"已选择 {BuildSkillVariantDisplayName(skillDef, castVariant)}，左键选择目标单位施放，Esc 清除选择。";
        }
        return $"已选择 {BuildSkillVariantDisplayName(skillDef, castVariant)}，需目标 {castVariant.required_coord_count} 格。左键逐格选点，Q/E 切换形态，Esc 清除选择。";
    }

    private static string BuildSkillVariantDisplayName(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (skillDef == null)
        {
            return "技能";
        }
        if (castVariant == null || string.IsNullOrEmpty(castVariant.display_name))
        {
            return skillDef.display_name;
        }
        return $"{skillDef.display_name}·{castVariant.display_name}";
    }

    private StringName ToggleSelectedMultiUnitSkillTarget(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return Overlay;
        }
        string blockReason = GetSkillCastBlockReason(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return Overlay;
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        int minTargetCount = Math.Max(skillDef.combat_profile.min_target_count, 1);
        int maxTargetCount = Math.Max(
            skillDef.combat_profile.get_effective_max_target_count(skillLevel),
            minTargetCount
        );
        GStringNameArray queuedTargetUnitIds = GetTargetUnitIdsState();
        if (targetUnit == null)
        {
            if (queuedTargetUnitIds.Count >= minTargetCount)
            {
                return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
            }
            RefreshBattleSelectionState();
            UpdateStatus(BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount));
            return Overlay;
        }

        StringName targetUnitId = targetUnit.unit_id;
        int existingIndex = queuedTargetUnitIds.IndexOf(targetUnitId);
        bool allowRepeat = skillDef.combat_profile.allow_repeat_target;
        int maxHitsPerTarget = Math.Max(skillDef.combat_profile.max_hits_per_target, 0);
        int existingCount = 0;
        foreach (StringName queuedId in queuedTargetUnitIds)
        {
            if (queuedId == targetUnitId)
            {
                existingCount += 1;
            }
        }

        if (existingIndex >= 0 && !allowRepeat)
        {
            queuedTargetUnitIds.RemoveAt(existingIndex);
            SetTargetUnitIdsState(queuedTargetUnitIds);
            RefreshSelectedUnitTargetCoordsFromQueue();
            SyncMultiUnitConfirmFocus(activeUnit, minTargetCount, maxTargetCount);
            RefreshBattleSelectionState();
            UpdateStatus(BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount));
            return Overlay;
        }

        if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDef))
        {
            if (
                targetUnit.unit_id == activeUnit.unit_id
                && queuedTargetUnitIds.Count >= minTargetCount
            )
            {
                return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
            }
            RefreshBattleSelectionState();
            UpdateStatus("该单位不是当前技能的合法目标。");
            return Overlay;
        }
        if (maxHitsPerTarget > 0 && existingCount >= maxHitsPerTarget)
        {
            UpdateStatus($"该目标已达到最大命中次数限制 ({maxHitsPerTarget} 次)。");
            return Overlay;
        }
        if (queuedTargetUnitIds.Count >= maxTargetCount)
        {
            string hint = allowRepeat ? "按 Esc 清除选择。" : "点击已选目标可取消。";
            UpdateStatus($"该技能最多选择 {maxTargetCount} 个单位目标；{hint}");
            return Overlay;
        }

        queuedTargetUnitIds.Add(targetUnitId);
        SetTargetUnitIdsState(queuedTargetUnitIds);
        RefreshSelectedUnitTargetCoordsFromQueue();
        if (queuedTargetUnitIds.Count >= maxTargetCount)
        {
            return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
        }
        SyncMultiUnitConfirmFocus(activeUnit, minTargetCount, maxTargetCount);
        RefreshBattleSelectionState();
        UpdateStatus(BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount));
        return Overlay;
    }

    private StringName IssueSelectedMultiUnitSkill(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (activeUnit == null || skillDef == null)
        {
            return Overlay;
        }

        var skillCommand = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = activeUnit.unit_id,
            skill_id = GetSelectedSkillId(),
            skill_variant_id = GetSelectedSkillVariantId(),
            target_unit_ids = DuplicateStringNameArray(GetTargetUnitIdsState()),
        };
        if (skillCommand.target_unit_ids.Count > 0)
        {
            BattleUnitState firstTarget = GetBattleUnitById(skillCommand.target_unit_ids[0]);
            if (firstTarget != null)
            {
                skillCommand.target_coord = firstTarget.coord;
            }
        }

        BattlePreview preview = PreviewBattleCommand(skillCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(skillCommand);
        }
        RefreshBattleSelectionState();
        if (preview != null && preview.log_lines.Count > 0)
        {
            UpdateStatus(preview.log_lines[preview.log_lines.Count - 1].ToString());
        }
        else
        {
            UpdateStatus("当前单位技能目标无效。");
        }
        return Overlay;
    }

    private void SyncMultiUnitConfirmFocus(
        BattleUnitState activeUnit,
        int minTargetCount,
        int maxTargetCount
    )
    {
        if (activeUnit == null)
        {
            return;
        }
        int selectedCount = GetTargetUnitIdsState().Count;
        if (selectedCount >= minTargetCount && selectedCount < maxTargetCount)
        {
            SetBattleSelectedCoord(activeUnit.coord);
        }
    }

    private string BuildMultiUnitTargetStatus(
        SkillDef skillDef,
        int minTargetCount,
        int maxTargetCount
    )
    {
        int selectedCount = GetTargetUnitIdsState().Count;
        string title = skillDef?.display_name ?? "技能";
        bool allowRepeat =
            skillDef?.combat_profile != null && skillDef.combat_profile.allow_repeat_target;
        string cancelHint = allowRepeat ? "点击已选目标可追加" : "点击已选目标可取消";
        if (selectedCount <= 0)
        {
            return $"已选择技能 {title}。左键逐个点选单位目标，{cancelHint}，Esc 清除选择。";
        }
        if (selectedCount < minTargetCount)
        {
            return $"已选择 {title}，已选择 {selectedCount} / {minTargetCount} 个单位目标。继续点选，{cancelHint}，Esc 清除选择。";
        }
        if (selectedCount < maxTargetCount)
        {
            return $"已选择 {title}，已选择 {selectedCount} / {maxTargetCount} 个单位目标。还可继续添加，{cancelHint}，Esc 清除选择。";
        }
        string maxHint = allowRepeat ? "按 Esc 清除选择。" : "点击已选目标可取消，Esc 清除选择。";
        return $"已选择 {title}，已选择 {selectedCount} / {maxTargetCount} 个单位目标。已达到上限，{maxHint}";
    }

    private void RefreshSelectedUnitTargetCoordsFromQueue()
    {
        var targetCoords = new GVector2IArray();
        BattleState battleState = GetBattleState();
        if (battleState == null)
        {
            SetTargetCoordsState(targetCoords);
            return;
        }
        foreach (StringName targetUnitId in GetTargetUnitIdsState())
        {
            BattleUnitState targetUnit = GetBattleUnitById(targetUnitId);
            if (targetUnit != null)
            {
                targetCoords.Add(targetUnit.coord);
            }
        }
        SetTargetCoordsState(SortCoords(targetCoords));
    }

    private GVector2IArray CollectSelectedBattleSkillTargetCoords()
    {
        if (GetTargetUnitIdsState().Count > 0)
        {
            RefreshSelectedUnitTargetCoordsFromQueue();
        }

        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        GVector2IArray targetCoords = DuplicateVector2IArray(GetTargetCoordsState());
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return targetCoords;
        }

        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (skillDef.combat_profile.target_mode == GroundTargetMode)
        {
            if (castVariant == null || castVariant.target_mode != GroundTargetMode)
            {
                return targetCoords;
            }
            if (targetCoords.Count < Math.Max(castVariant.required_coord_count, 1))
            {
                return targetCoords;
            }
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        GDictionary collectedTargetCoords =
            _targetCollectionService.collect_combat_profile_target_coords(
                GetBattleState(),
                GetBattleGridService(),
                activeUnit.coord,
                skillDef.combat_profile,
                ToUntypedArray(targetCoords),
                activeUnit,
                CollectSelectedTargetUnits(activeUnit, skillDef),
                skillLevel
            );
        if (GdInterop.GetBool(collectedTargetCoords, "handled", false))
        {
            return SortCoords(GdInterop.GetArray(collectedTargetCoords, "target_coords"));
        }
        return targetCoords;
    }

    private GArray CollectSelectedTargetUnits(BattleUnitState activeUnit, SkillDef skillDef)
    {
        var targetUnits = new GArray();
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return targetUnits;
        }
        foreach (StringName targetUnitId in GetTargetUnitIdsState())
        {
            BattleUnitState targetUnit = GetBattleUnitById(targetUnitId);
            if (targetUnit != null)
            {
                targetUnits.Add(targetUnit);
            }
        }
        if (targetUnits.Count > 0)
        {
            return targetUnits;
        }
        if (
            GetSelectedBattleSkillTargetSelectionMode(activeUnit) == SelfSelectionMode
            || skillDef.combat_profile.target_team_filter == SelfSelectionMode
            || skillDef.combat_profile.area_pattern == SelfSelectionMode
        )
        {
            targetUnits.Add(activeUnit);
        }
        return targetUnits;
    }

    private void ClearBattleSkillTargetSelection()
    {
        ClearTargetCoordsState();
        ClearTargetUnitIdsState();
    }

    private StringName GetSelectedBattleSkillTargetSelectionMode(BattleUnitState activeUnit)
    {
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            return "single_unit";
        }
        StringName selectionMode = skillDef.combat_profile.target_selection_mode;
        return GdInterop.IsEmpty(selectionMode) ? new StringName("single_unit") : selectionMode;
    }

    private BattleUnitState GetManualActiveUnit()
    {
        return Runtime?.get_manual_battle_unit();
    }

    private BattleUnitState GetRuntimeActiveUnit()
    {
        return Runtime?.get_runtime_battle_active_unit();
    }

    private BattleUnitState GetRuntimeUnitAtCoord(Vector2I coord)
    {
        return Runtime?.get_runtime_battle_unit_at_coord(coord);
    }

    private BattleUnitState GetBattleUnitById(StringName unitId)
    {
        return Runtime?.get_runtime_battle_unit_by_id(unitId);
    }

    private BattleState GetBattleState()
    {
        return Runtime?.get_battle_state();
    }

    private BattleGridService GetBattleGridService()
    {
        return Runtime?.get_battle_grid_service();
    }

    private BattlePreview PreviewBattleCommand(BattleCommand command)
    {
        return Runtime?.preview_battle_command(command);
    }

    private StringName IssueBattleCommand(BattleCommand command)
    {
        return Runtime?.issue_battle_command(command) ?? Overlay;
    }

    private void RefreshBattleSelectionState()
    {
        Runtime?.refresh_battle_selection_state();
    }

    private void UpdateStatus(string message)
    {
        Runtime?.update_status(message);
    }

    private string FormatCoord(Vector2I coord)
    {
        return Runtime?.format_coord(coord) ?? $"({coord.X},{coord.Y})";
    }

    private bool IsBattleActive()
    {
        return Runtime?.is_battle_active() ?? false;
    }

    private StringName GetSelectedSkillId()
    {
        return Runtime?.get_selected_battle_skill_id() ?? new StringName("");
    }

    private void SetSelectedSkillId(StringName skillId)
    {
        Runtime?.set_battle_selection_skill_id(skillId);
    }

    private StringName GetSelectedSkillVariantId()
    {
        return Runtime?.get_selected_battle_skill_variant_id() ?? new StringName("");
    }

    private void SetSelectedSkillVariantId(StringName optionId)
    {
        Runtime?.set_battle_selection_skill_variant_id(optionId);
    }

    private StringName GetLastManualUnitId()
    {
        return Runtime?.get_battle_selection_last_manual_unit_id() ?? new StringName("");
    }

    private void SetLastManualUnitId(StringName unitId)
    {
        Runtime?.set_battle_selection_last_manual_unit_id(unitId);
    }

    private GVector2IArray GetTargetCoordsState()
    {
        if (Runtime == null)
        {
            return new GVector2IArray();
        }
        return DuplicateVector2IArray(Runtime.get_battle_selection_target_coords_state());
    }

    private void SetTargetCoordsState(GVector2IArray targetCoords)
    {
        Runtime?.set_battle_selection_target_coords_state(targetCoords ?? new GVector2IArray());
    }

    private void ClearTargetCoordsState()
    {
        SetTargetCoordsState(new GVector2IArray());
    }

    private GStringNameArray GetTargetUnitIdsState()
    {
        if (Runtime == null)
        {
            return new GStringNameArray();
        }
        return DuplicateStringNameArray(Runtime.get_battle_selection_target_unit_ids_state());
    }

    private void SetTargetUnitIdsState(GStringNameArray targetUnitIds)
    {
        Runtime?.set_battle_selection_target_unit_ids_state(targetUnitIds ?? new GStringNameArray());
    }

    private void ClearTargetUnitIdsState()
    {
        SetTargetUnitIdsState(new GStringNameArray());
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        Runtime?.set_runtime_battle_selected_coord(coord);
    }

    private static GDictionary SelectionOk()
    {
        return new GDictionary { ["ok"] = true };
    }

    private static GDictionary SelectionError(string message)
    {
        return new GDictionary { ["ok"] = false, ["message"] = message };
    }

    private static int PosMod(int value, int modulo)
    {
        return modulo <= 0 ? 0 : ((value % modulo) + modulo) % modulo;
    }

    private static GVector2IArray SortCoords(IEnumerable<Vector2I> targetCoords)
    {
        var coords = new List<Vector2I>();
        if (targetCoords != null)
        {
            coords.AddRange(targetCoords);
        }
        coords.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static GVector2IArray SortCoords(GArray targetCoords)
    {
        var coords = new List<Vector2I>();
        foreach (var rawCoord in targetCoords ?? new GArray())
        {
            if (rawCoord.VariantType == Variant.Type.Vector2I)
            {
                coords.Add(rawCoord.AsVector2I());
            }
        }
        return SortCoords(coords);
    }

    private static GVector2IArray DuplicateVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GVector2IArray();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray DuplicateStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GArray ToUntypedArray<T>(IEnumerable<T> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (T value in values)
        {
            result.Add(Variant.From(value));
        }
        return result;
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
        {
            return null;
        }
        return target;
    }
}
