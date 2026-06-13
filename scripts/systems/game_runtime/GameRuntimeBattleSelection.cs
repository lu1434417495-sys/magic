using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GCombatCastVariantDefArray = Godot.Collections.Array<CombatCastVariantDef>;
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
    private static readonly StringName GroundTargetMode = BattleTypedNames.TargetModeGround;
    private static readonly StringName SelfSelectionMode = BattleTypedNames.TargetSelectionSelf;
    private static readonly StringName EnemyFilter = "enemy";
    private readonly BattleTargetCollectionService _targetCollectionService = new();
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade Runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    internal void Setup(GameRuntimeFacade runtime)
    {
        Runtime = runtime;
    }

    internal new void Dispose()
    {
        Runtime = null;
    }

    internal string GetSelectedBattleSkillName()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        return skillDef?.display_name ?? "";
    }

    internal string GetSelectedBattleSkillVariantName()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        return castVariant?.display_name ?? "";
    }

    internal GVector2IArray GetSelectedBattleSkillTargetCoords()
    {
        return CollectSelectedBattleSkillTargetCoords();
    }

    internal GStringNameArray GetSelectedBattleSkillTargetUnitIds()
    {
        return DuplicateStringNameArray(GetTargetUnitIdsStateTyped());
    }

    internal GVector2IArray GetSelectedBattleSkillValidTargetCoords()
    {
        return CollectSelectedBattleSkillValidTargetCoords();
    }

    internal int GetSelectedBattleSkillRequiredCoordCount()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (skillDef?.combat_profile != null)
        {
            BattleTargetSelectionMode selectionMode =
                GetSelectedBattleSkillTargetSelectionModeKind(activeUnit);
            if (selectionMode == BattleTargetSelectionMode.MultiUnit)
            {
                SkillEffectiveCombatProfile effectiveProfile = GetEffectiveCombatProfileForUnit(
                    activeUnit,
                    skillDef
                );
                return Math.Max(
                    effectiveProfile.MaxTargetCount,
                    skillDef.combat_profile.min_target_count
                );
            }
            if (skillDef.combat_profile.TargetModeKind == BattleTargetMode.Unit)
            {
                return 1;
            }
        }
        return castVariant == null ? 0 : castVariant.required_coord_count;
    }

    internal GameRuntimeFacade.RuntimeCommandResult SelectBattleSkillSlotTyped(int index)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return SelectionErrorTyped("当前没有可手动操作的单位。");
        }
        if (index < 0 || index >= activeUnit.known_active_skill_ids.Count)
        {
            UpdateStatus("该技能栏当前没有技能。");
            return SelectionErrorTyped("该技能栏当前没有技能。");
        }

        StringName skillId = activeUnit.known_active_skill_ids[index];
        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef?.combat_profile == null)
        {
            UpdateStatus("该技能当前不可用于战斗。");
            return SelectionErrorTyped("该技能当前不可用于战斗。");
        }

        if (GetSelectedSkillId() == skillId)
        {
            ClearBattleSkillSelection(true);
            return SelectionOkTyped();
        }

        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return SelectionErrorTyped(blockReason);
        }

        SetSelectedSkillId(skillId);
        SetSelectedSkillVariantId("");
        ClearBattleSkillTargetSelection();

        if (skillDef.combat_profile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain)
        {
            var chainCommand = new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = activeUnit.unit_id,
                skill_id = skillId,
                skill_variant_id = GetDefaultUnitSkillVariantId(activeUnit, skillDef),
                target_unit_ids = new GStringNameArray(),
            };
            BattlePreview chainPreview = PreviewBattleCommand(chainCommand);
            if (chainPreview != null && chainPreview.allowed)
            {
                IssueBattleCommand(chainCommand);
                return SelectionOkTyped();
            }
            RefreshBattleSelectionState();
            UpdateStatus(
                chainPreview != null && chainPreview.LogLinesTyped.Count > 0
                    ? chainPreview.LogLinesTyped[chainPreview.LogLinesTyped.Count - 1]
                    : "无法执行连锁攻击。"
            );
            return SelectionErrorTyped("无法执行连锁攻击。");
        }

        GCombatCastVariantDefArray unlockedOptions = GetUnlockedCastVariants(activeUnit, skillDef);
        if (unlockedOptions.Count > 0 && unlockedOptions[0] is CombatCastVariantDef firstValue)
        {
            SetSelectedSkillVariantId(firstValue.variant_id);
        }
        RefreshBattleSelectionState();
        UpdateStatus(BuildBattleSkillSelectionStatus(skillDef, activeUnit));
        return SelectionOkTyped();
    }

    internal void CycleSelectedBattleSkillOption(int step)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return;
        }
        if (GetSelectedSkillId() == "")
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

        GCombatCastVariantDefArray unlockedOptions = GetUnlockedCastVariants(activeUnit, skillDef);
        if (unlockedOptions.Count == 0)
        {
            UpdateStatus("当前技能等级尚未解锁任何施法形态。");
            return;
        }

        int currentIndex = 0;
        for (int optionIndex = 0; optionIndex < unlockedOptions.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = unlockedOptions[optionIndex];
            if (castVariant != null && castVariant.variant_id == GetSelectedSkillVariantId())
            {
                currentIndex = optionIndex;
                break;
            }
        }

        int nextIndex = PosMod(currentIndex + step, unlockedOptions.Count);
        if (unlockedOptions[nextIndex] is CombatCastVariantDef nextValue)
        {
            SetSelectedSkillVariantId(nextValue.variant_id);
        }
        ClearBattleSkillTargetSelection();
        RefreshBattleSelectionState();
        UpdateStatus(BuildBattleSkillSelectionStatus(skillDef, activeUnit));
    }

    internal void ClearBattleSkillSelection(bool announce = false)
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

    internal void SyncSelectedBattleSkillState()
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
        if (activeUnit == null || GetSelectedSkillId() == "")
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

    internal BattleRefreshMode AttemptBattleMoveTo(Vector2I target_coord)
    {
        if (!IsBattleActive())
        {
            return BattleRefreshMode.Full;
        }

        SetBattleSelectedCoord(target_coord);
        BattleState battleState = GetBattleState();
        if (battleState == null || !battleState.TryGetCellTyped(target_coord, out _))
        {
            RefreshBattleSelectionState();
            UpdateStatus("该战斗格超出当前战场范围。");
            return BattleRefreshMode.Overlay;
        }

        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            RefreshBattleSelectionState();
            UpdateStatus("等待当前单位进入可操作状态。");
            return BattleRefreshMode.Overlay;
        }

        if (IsSelectedGroundSkillReady(activeUnit))
        {
            return HandleSelectedGroundSkillClick(activeUnit, target_coord);
        }

        BattleUnitState targetUnit = GetRuntimeUnitAtCoord(target_coord);
        if (targetUnit != null)
        {
            BattleRefreshMode selectedSkillResult = HandleSelectedUnitSkillClick(activeUnit, targetUnit);
            if (selectedSkillResult != BattleRefreshMode.None)
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
        else if (GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit)
        {
            SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
            if (skillDef?.combat_profile != null)
            {
                int minTargetCount = Math.Max(skillDef.combat_profile.min_target_count, 1);
                if (GetTargetUnitIdsStateTyped().Count >= minTargetCount)
                {
                    return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
                }
            }
        }

        if (activeUnit.OccupiesCoord(target_coord))
        {
            RefreshBattleSelectionState();
            UpdateStatus("已选中当前行动单位。");
            return BattleRefreshMode.Overlay;
        }

        var moveCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Move,
            unit_id = activeUnit.unit_id,
            target_coord = target_coord,
        };
        BattlePreview preview = PreviewBattleCommand(moveCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(moveCommand);
        }

        RefreshBattleSelectionState();
        if (preview != null && preview.LogLinesTyped.Count > 0)
        {
            UpdateStatus(preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]);
        }
        else
        {
            UpdateStatus($"已选中战斗格 {FormatCoord(target_coord)}。");
        }
        return BattleRefreshMode.Overlay;
    }

    internal BattleRefreshMode ResetBattleMovement()
    {
        if (!IsBattleActive())
        {
            return BattleRefreshMode.Full;
        }

        BattleUnitState activeUnit = GetRuntimeActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可聚焦的行动单位。");
            return BattleRefreshMode.Overlay;
        }

        SetBattleSelectedCoord(activeUnit.coord);
        RefreshBattleSelectionState();
        UpdateStatus("已聚焦当前行动单位。");
        return BattleRefreshMode.Overlay;
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
            if (skillDef.combat_profile.TargetModeKind != BattleTargetMode.Unit)
            {
                continue;
            }
            if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDef))
            {
                continue;
            }
            return new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
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
        if (activeUnit == null || targetUnit == null || GetSelectedSkillId() == "")
        {
            return null;
        }

        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            return null;
        }
        if (skillDef.combat_profile.TargetModeKind != BattleTargetMode.Unit)
        {
            return null;
        }
        if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDef))
        {
            return null;
        }
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
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
        foreach (CombatCastVariantDef castVariant in GetUnlockedCastVariants(activeUnit, skillDef))
        {
            if (castVariant != null && castVariant.TargetModeKind == BattleTargetMode.Unit)
            {
                return castVariant.variant_id;
            }
        }
        return "";
    }

    private bool IsSelectedGroundSkillReady(BattleUnitState activeUnit)
    {
        if (GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit)
        {
            return false;
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        return castVariant != null && castVariant.TargetModeKind == BattleTargetMode.Ground;
    }

    private BattleRefreshMode HandleSelectedGroundSkillClick(
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
            return BattleRefreshMode.Overlay;
        }

        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return BattleRefreshMode.Error;
        }

        int requiredCoordCount = Math.Max(castVariant.required_coord_count, 1);
        List<Vector2I> queuedTargetCoords = GetTargetCoordsStateTyped();
        List<Vector2I> previousTargets = new(queuedTargetCoords);
        int existingIndex = queuedTargetCoords.IndexOf(targetCoord);
        if (existingIndex >= 0)
        {
            queuedTargetCoords.RemoveAt(existingIndex);
            SetTargetCoordsStateTyped(queuedTargetCoords);
            RefreshBattleSelectionState();
            UpdateStatus($"已取消目标格 {FormatCoord(targetCoord)}。");
            return BattleRefreshMode.Overlay;
        }

        if (requiredCoordCount == 1)
        {
            SetTargetCoordsStateTyped(new[] { targetCoord });
        }
        else
        {
            if (queuedTargetCoords.Count >= requiredCoordCount)
            {
                UpdateStatus(
                    $"该技能形态最多选择 {requiredCoordCount} 个地格；点击已选地格可取消。"
                );
                return BattleRefreshMode.Overlay;
            }
            queuedTargetCoords.Add(targetCoord);
            SetTargetCoordsStateTyped(queuedTargetCoords);
        }

        List<Vector2I> resolvedTargetCoords = GetTargetCoordsStateTyped();
        if (resolvedTargetCoords.Count < requiredCoordCount)
        {
            RefreshBattleSelectionState();
            UpdateStatus(
                $"{BuildSkillVariantDisplayName(skillDef, castVariant)}：已选择 {resolvedTargetCoords.Count} / {requiredCoordCount} 个地格。"
            );
            return BattleRefreshMode.Overlay;
        }

        var skillCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
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

        SetTargetCoordsStateTyped(requiredCoordCount > 1 ? previousTargets : Array.Empty<Vector2I>());
        RefreshBattleSelectionState();
        if (preview != null && preview.LogLinesTyped.Count > 0)
        {
            UpdateStatus(preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]);
        }
        else
        {
            UpdateStatus("当前地面技能目标无效。");
        }
        return BattleRefreshMode.Overlay;
    }

    private BattleRefreshMode HandleSelectedUnitSkillClick(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        if (skillDef?.combat_profile == null)
        {
            return BattleRefreshMode.None;
        }
        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return BattleRefreshMode.Error;
        }

        BattleTargetSelectionMode selectionMode = skillDef.combat_profile.TargetSelectionModeKind;
        if (selectionMode == BattleTargetSelectionMode.MultiUnit)
        {
            return ToggleSelectedMultiUnitSkillTarget(activeUnit, targetUnit, skillDef);
        }
        if (skillDef.combat_profile.TargetModeKind != BattleTargetMode.Unit)
        {
            return BattleRefreshMode.None;
        }

        BattleCommand skillCommand = BuildSelectedSkillCommand(activeUnit, targetUnit);
        return skillCommand != null ? IssueBattleCommand(skillCommand) : BattleRefreshMode.None;
    }

    private SkillDef GetSelectedBattleSkillDef(BattleUnitState activeUnit)
    {
        if (activeUnit == null || GetSelectedSkillId() == "")
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
            return skillDef.combat_profile.TargetModeKind == BattleTargetMode.Ground
                ? BuildImplicitGroundCastVariant(skillDef)
                : null;
        }

        GCombatCastVariantDefArray unlockedOptions = GetUnlockedCastVariants(activeUnit, skillDef);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (GetSelectedSkillVariantId() == "")
        {
            return unlockedOptions[0];
        }
        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (castVariant != null && castVariant.variant_id == GetSelectedSkillVariantId())
            {
                return castVariant;
            }
        }
        return unlockedOptions[0];
    }

    private GCombatCastVariantDefArray GetUnlockedCastVariants(
        BattleUnitState activeUnit,
        SkillDef skillDef
    )
    {
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return new GCombatCastVariantDefArray();
        }
        SkillEffectiveCombatProfile effectiveProfile = GetEffectiveCombatProfileForUnit(
            activeUnit,
            skillDef
        );
        return SkillEffectiveCombatProfileResolver.ToGodotArray(
            effectiveProfile.UnlockedCastVariants
        );
    }

    private static CombatCastVariantDef BuildImplicitGroundCastVariant(SkillDef skillDef)
    {
        if (
            skillDef?.combat_profile == null
            || skillDef.combat_profile.TargetModeKind != BattleTargetMode.Ground
        )
        {
            return null;
        }
        return new CombatCastVariantDef
        {
            variant_id = "",
            display_name = "",
            TargetModeKind = BattleTargetMode.Ground,
            FootprintPatternKind = CombatCastFootprintPattern.Single,
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = Runtime.GetSkillDefsTyped();
        if (skillDefs == null || !skillDefs.TryGetValue(skillId, out SkillDef skillDef))
        {
            return null;
        }
        return skillDef;
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
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDef)))
        {
            return new GVector2IArray();
        }
        if (GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit)
        {
            return CollectValidUnitSkillTargetCoords(
                activeUnit,
                skillDef,
                GetTargetUnitIdsStateTyped()
            );
        }
        if (skillDef.combat_profile.TargetModeKind == BattleTargetMode.Unit)
        {
            return CollectValidUnitSkillTargetCoords(
                activeUnit,
                skillDef,
                GetTargetUnitIdsStateTyped()
            );
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (castVariant == null || castVariant.TargetModeKind != BattleTargetMode.Ground)
        {
            return new GVector2IArray();
        }
        return CollectValidGroundSkillTargetCoords(activeUnit, skillDef, castVariant);
    }

    private GVector2IArray CollectValidUnitSkillTargetCoords(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        IEnumerable<StringName> excludedUnitIds
    )
    {
        var coordSet = new HashSet<Vector2I>();
        BattleState battleState = GetBattleState();
        if (battleState == null || activeUnit == null || skillDef == null)
        {
            return new GVector2IArray();
        }

        var excludedUnitIdSet = new HashSet<StringName>();
        foreach (StringName excludedUnitId in excludedUnitIds ?? Array.Empty<StringName>())
        {
            excludedUnitIdSet.Add(excludedUnitId);
        }
        bool useAnchorCoords =
            GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit;
        foreach (BattleUnitState targetUnit in battleState.GetUnitsTyped())
        {
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
                targetUnit.RefreshFootprint();
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
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDef)))
        {
            return new GVector2IArray();
        }

        List<Vector2I> queuedCoords = GetTargetCoordsStateTyped();
        foreach (BattleState.BattleCellEntry cellEntry in battleState.GetCellEntriesTyped())
        {
            Vector2I targetCoord = cellEntry.Coord;
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
        return DuplicateVector2IArray(SortCoordsTyped(coordSet));
    }

    private bool IsNextGroundTargetCoordSelectable(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        IReadOnlyList<Vector2I> queuedCoords,
        Vector2I candidateCoord
    )
    {
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDef)))
        {
            return false;
        }
        var nextCoords = new List<Vector2I>(queuedCoords ?? Array.Empty<Vector2I>());
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
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Unordered)
        {
            return true;
        }
        foreach (IReadOnlyList<Vector2I> fullCoords in BuildGroundCompletionSets(castVariant, nextCoords))
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
        IReadOnlyList<Vector2I> targetCoords
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
        foreach (Vector2I coord in targetCoords ?? Array.Empty<Vector2I>())
        {
            if (!seenCoords.Add(coord))
            {
                return false;
            }
            int targetDistance =
                relocationEffectDef != null
                    ? battleGridService.GetChebyshevDistance(activeUnit.coord, coord)
                    : battleGridService.GetDistanceFromUnitToCoord(activeUnit, coord);
            if (targetDistance > GetEffectiveSkillRange(activeUnit, skillDef))
            {
                return false;
            }
            if (!battleState.TryGetCellTyped(coord, out BattleCellState cell))
            {
                return false;
            }
            if (castVariant.allowed_base_terrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.NormalizeTerrainId(
                    cell.base_terrain
                );
                foreach (StringName allowedTerrain in castVariant.allowed_base_terrains)
                {
                    if (
                        BattleTerrainRules.NormalizeTerrainId(allowedTerrain)
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
        if (effectDef == null || effectDef.EffectKind != BattleEffectKind.ForcedMove)
        {
            return false;
        }
        return effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Jump
            || effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Blink;
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
        if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Jump)
        {
            return battleGridService.CanJumpArc(battleState, activeUnit, coord, effectDef);
        }
        if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Blink)
        {
            return battleGridService.CanBlinkToCoord(battleState, activeUnit, coord, effectDef);
        }
        return false;
    }

    private bool IsGroundTargetComboAllowed(
        BattleUnitState activeUnit,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        if (activeUnit == null || skillDef == null || castVariant == null)
        {
            return false;
        }
        List<Vector2I> sortedTargetCoords = SortCoordsTyped(targetCoords);
        var skillCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_id = skillDef.skill_id,
            skill_variant_id = castVariant.variant_id,
            target_coords = DuplicateVector2IArray(sortedTargetCoords),
        };
        if (sortedTargetCoords.Count > 0)
        {
            skillCommand.target_coord = sortedTargetCoords[sortedTargetCoords.Count - 1];
        }
        BattlePreview preview = PreviewBattleCommand(skillCommand);
        return preview != null && preview.allowed;
    }

    private IEnumerable<List<Vector2I>> BuildGroundCompletionSets(
        CombatCastVariantDef castVariant,
        IReadOnlyList<Vector2I> partialCoords
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
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Single)
        {
            if (partialCoords.Count == 1)
            {
                yield return SortCoordsTyped(partialCoords);
            }
            yield break;
        }
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Line2)
        {
            foreach (List<Vector2I> completionSet in BuildLine2CompletionSets(partialCoords))
            {
                yield return completionSet;
            }
            yield break;
        }
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Square2)
        {
            foreach (List<Vector2I> completionSet in BuildSquare2CompletionSets(partialCoords))
            {
                yield return completionSet;
            }
            yield break;
        }
        if (
            castVariant.FootprintPatternKind == CombatCastFootprintPattern.Unordered
            && partialCoords.Count == requiredCoordCount
        )
        {
            yield return SortCoordsTyped(partialCoords);
        }
    }

    private IEnumerable<List<Vector2I>> BuildLine2CompletionSets(IReadOnlyList<Vector2I> partialCoords)
    {
        var seenSignatures = new HashSet<string>();
        Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        foreach (Vector2I origin in partialCoords)
        {
            foreach (Vector2I direction in directions)
            {
                List<Vector2I> candidatePair = SortCoordsTyped(new[] { origin, origin + direction });
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

    private IEnumerable<List<Vector2I>> BuildSquare2CompletionSets(IReadOnlyList<Vector2I> partialCoords)
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
            List<Vector2I> blockCoords = SortCoordsTyped(
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
        IReadOnlyCollection<Vector2I> fullCoords,
        IEnumerable<Vector2I> partialCoords
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

    private static string BuildCoordSignature(IEnumerable<Vector2I> targetCoords)
    {
        var segments = new List<string>();
        foreach (Vector2I coord in SortCoordsTyped(targetCoords))
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
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDef)))
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

        activeUnit.RefreshFootprint();
        targetUnit.RefreshFootprint();
        BattleGridService battleGridService = GetBattleGridService();
        if (battleGridService == null)
        {
            return false;
        }
        return battleGridService.GetDistanceBetweenUnits(activeUnit, targetUnit)
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
            && targetUnit.HasStatusEffect(StatusBlackStarBrandElite);
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
            ? snapshot.GetValue(attributeId)
            : 0;
    }

    private static bool SkillTargetFilterMatchesUnit(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    )
    {
        return BattleTargetTeamRules.IsUnitValidForFilter(
            activeUnit,
            targetUnit,
            targetTeamFilter,
            default
        );
    }

    private int GetEffectiveSkillRange(BattleUnitState activeUnit, SkillDef skillDef)
    {
        return BattleRangeService.GetEffectiveSkillRange(
            activeUnit,
            skillDef,
            GetSkillCatalog()
        );
    }

    private string GetSkillCastBlockMessage(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (Runtime != null)
        {
            return Runtime.GetBattleSkillCastBlockMessage(activeUnit, skillDef);
        }
        return "正式技能检查未绑定，无法施放该技能。";
    }

    private int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
        {
            return 0;
        }
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId, int.MinValue);
        if (knownSkillLevel != int.MinValue)
        {
            return knownSkillLevel;
        }
        SkillDef skillDef = GetSkillDef(skillId);
        if (IsLevelLessSkill(skillDef))
        {
            return 0;
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private SkillEffectiveCombatProfile GetEffectiveCombatProfileForUnit(
        BattleUnitState unitState,
        SkillDef skillDef
    )
    {
        if (skillDef == null)
        {
            return SkillEffectiveCombatProfileResolver.BuildMissing(0);
        }
        int skillLevel = GetUnitSkillLevel(unitState, skillDef.skill_id);
        return SkillEffectiveCombatProfileResolver.Resolve(
            GetSkillCatalog(),
            skillDef,
            skillLevel
        );
    }

    private static bool IsLevelLessSkill(SkillDef skillDef)
    {
        return skillDef != null
            && skillDef.max_level == 0
            && skillDef.dynamic_max_level_stat_id == "";
    }

    private string BuildBattleSkillSelectionStatus(SkillDef skillDef, BattleUnitState activeUnit)
    {
        if (skillDef == null)
        {
            return "当前技能不可用。";
        }

        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            return $"{blockReason}按 Esc 清除选择。";
        }
        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        StringName selectionMode = GetSelectedBattleSkillTargetSelectionMode(activeUnit);
        BattleTargetSelectionMode selectionModeKind = GetSelectedBattleSkillTargetSelectionModeKind(
            activeUnit
        );
        if (selectionModeKind == BattleTargetSelectionMode.RandomChain)
        {
            return $"已选择技能 {skillDef.display_name}，将自动攻击范围内随机敌军。Esc 清除选择。";
        }
        if (selectionModeKind == BattleTargetSelectionMode.MultiUnit)
        {
            int minTargetCount = Math.Max(skillDef.combat_profile.min_target_count, 1);
            SkillEffectiveCombatProfile effectiveProfile = GetEffectiveCombatProfileForUnit(
                activeUnit,
                skillDef
            );
            int maxTargetCount = Math.Max(
                effectiveProfile.MaxTargetCount,
                minTargetCount
            );
            return BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount);
        }
        if (castVariant == null)
        {
            if (
                skillDef.combat_profile.TargetModeKind == BattleTargetMode.Unit
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
        if (skillDef.combat_profile.TargetModeKind == BattleTargetMode.Unit)
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

    private BattleRefreshMode ToggleSelectedMultiUnitSkillTarget(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return BattleRefreshMode.Overlay;
        }
        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDef);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return BattleRefreshMode.Overlay;
        }

        int minTargetCount = Math.Max(skillDef.combat_profile.min_target_count, 1);
        SkillEffectiveCombatProfile effectiveProfile = GetEffectiveCombatProfileForUnit(
            activeUnit,
            skillDef
        );
        int maxTargetCount = Math.Max(
            effectiveProfile.MaxTargetCount,
            minTargetCount
        );
        List<StringName> queuedTargetUnitIds = GetTargetUnitIdsStateTyped();
        if (targetUnit == null)
        {
            if (queuedTargetUnitIds.Count >= minTargetCount)
            {
                return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
            }
            RefreshBattleSelectionState();
            UpdateStatus(BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount));
            return BattleRefreshMode.Overlay;
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
            SetTargetUnitIdsStateTyped(queuedTargetUnitIds);
            RefreshSelectedUnitTargetCoordsFromQueue();
            SyncMultiUnitConfirmFocus(activeUnit, minTargetCount, maxTargetCount);
            RefreshBattleSelectionState();
            UpdateStatus(BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount));
            return BattleRefreshMode.Overlay;
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
            return BattleRefreshMode.Overlay;
        }
        if (maxHitsPerTarget > 0 && existingCount >= maxHitsPerTarget)
        {
            UpdateStatus($"该目标已达到最大命中次数限制 ({maxHitsPerTarget} 次)。");
            return BattleRefreshMode.Overlay;
        }
        if (queuedTargetUnitIds.Count >= maxTargetCount)
        {
            string hint = allowRepeat ? "按 Esc 清除选择。" : "点击已选目标可取消。";
            UpdateStatus($"该技能最多选择 {maxTargetCount} 个单位目标；{hint}");
            return BattleRefreshMode.Overlay;
        }

        queuedTargetUnitIds.Add(targetUnitId);
        SetTargetUnitIdsStateTyped(queuedTargetUnitIds);
        RefreshSelectedUnitTargetCoordsFromQueue();
        if (queuedTargetUnitIds.Count >= maxTargetCount)
        {
            return IssueSelectedMultiUnitSkill(activeUnit, skillDef);
        }
        SyncMultiUnitConfirmFocus(activeUnit, minTargetCount, maxTargetCount);
        RefreshBattleSelectionState();
        UpdateStatus(BuildMultiUnitTargetStatus(skillDef, minTargetCount, maxTargetCount));
        return BattleRefreshMode.Overlay;
    }

    private BattleRefreshMode IssueSelectedMultiUnitSkill(BattleUnitState activeUnit, SkillDef skillDef)
    {
        if (activeUnit == null || skillDef == null)
        {
            return BattleRefreshMode.Overlay;
        }

        var skillCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_id = GetSelectedSkillId(),
            skill_variant_id = GetSelectedSkillVariantId(),
            target_unit_ids = DuplicateStringNameArray(GetTargetUnitIdsStateTyped()),
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
        if (preview != null && preview.LogLinesTyped.Count > 0)
        {
            UpdateStatus(preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]);
        }
        else
        {
            UpdateStatus("当前单位技能目标无效。");
        }
        return BattleRefreshMode.Overlay;
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
        int selectedCount = GetTargetUnitIdsStateTyped().Count;
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
        int selectedCount = GetTargetUnitIdsStateTyped().Count;
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
        var targetCoords = new List<Vector2I>();
        BattleState battleState = GetBattleState();
        if (battleState == null)
        {
            SetTargetCoordsStateTyped(targetCoords);
            return;
        }
        foreach (StringName targetUnitId in GetTargetUnitIdsStateTyped())
        {
            BattleUnitState targetUnit = GetBattleUnitById(targetUnitId);
            if (targetUnit != null)
            {
                targetCoords.Add(targetUnit.coord);
            }
        }
        SetTargetCoordsStateTyped(SortCoordsTyped(targetCoords));
    }

    private GVector2IArray CollectSelectedBattleSkillTargetCoords()
    {
        if (GetTargetUnitIdsStateTyped().Count > 0)
        {
            RefreshSelectedUnitTargetCoordsFromQueue();
        }

        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDef skillDef = GetSelectedBattleSkillDef(activeUnit);
        List<Vector2I> targetCoords = GetTargetCoordsStateTyped();
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return DuplicateVector2IArray(targetCoords);
        }

        CombatCastVariantDef castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (skillDef.combat_profile.TargetModeKind == BattleTargetMode.Ground)
        {
            if (castVariant == null || castVariant.TargetModeKind != BattleTargetMode.Ground)
            {
                return DuplicateVector2IArray(targetCoords);
            }
            if (targetCoords.Count < Math.Max(castVariant.required_coord_count, 1))
            {
                return DuplicateVector2IArray(targetCoords);
            }
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef.skill_id);
        BattleTargetCollectionResult collectedTargetCoords =
            _targetCollectionService.CollectSkillTargetCoords(
                GetBattleState(),
                GetBattleGridService(),
                activeUnit.coord,
                skillDef,
                targetCoords,
                activeUnit,
                CollectSelectedTargetUnits(activeUnit, skillDef),
                skillLevel,
                GetSkillCatalog()
            );
        if (collectedTargetCoords.Handled)
        {
            return DuplicateVector2IArray(SortCoordsTyped(collectedTargetCoords.TargetCoords));
        }
        return DuplicateVector2IArray(targetCoords);
    }

    private List<BattleUnitState> CollectSelectedTargetUnits(
        BattleUnitState activeUnit,
        SkillDef skillDef
    )
    {
        var targetUnits = new List<BattleUnitState>();
        if (activeUnit == null || skillDef?.combat_profile == null)
        {
            return targetUnits;
        }
        foreach (StringName targetUnitId in GetTargetUnitIdsStateTyped())
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
            return BattleTypedNames.TargetSelectionSingleUnit;
        }
        StringName selectionMode = BattleTypedNames.ToStringName(
            skillDef.combat_profile.TargetSelectionModeKind
        );
        return selectionMode == "" ? BattleTypedNames.TargetSelectionSingleUnit : selectionMode;
    }

    private BattleTargetSelectionMode GetSelectedBattleSkillTargetSelectionModeKind(
        BattleUnitState activeUnit
    )
    {
        return BattleTypedNames.ToTargetSelectionMode(
            GetSelectedBattleSkillTargetSelectionMode(activeUnit)
        );
    }

    private BattleUnitState GetManualActiveUnit()
    {
        return Runtime?.GetManualBattleUnit();
    }

    private BattleUnitState GetRuntimeActiveUnit()
    {
        return Runtime?.GetRuntimeBattleActiveUnit();
    }

    private BattleUnitState GetRuntimeUnitAtCoord(Vector2I coord)
    {
        return Runtime?.GetRuntimeBattleUnitAtCoord(coord);
    }

    private BattleUnitState GetBattleUnitById(StringName unitId)
    {
        return Runtime?.GetRuntimeBattleUnitById(unitId);
    }

    private BattleState GetBattleState()
    {
        return Runtime?.GetBattleState();
    }

    private BattleGridService GetBattleGridService()
    {
        return Runtime?.GetBattleGridService();
    }

    private ISkillCatalog GetSkillCatalog()
    {
        return Runtime?.GetSkillCatalogTyped();
    }

    private BattlePreview PreviewBattleCommand(BattleCommand command)
    {
        return Runtime?.PreviewBattleCommand(command);
    }

    private BattleRefreshMode IssueBattleCommand(BattleCommand command)
    {
        return Runtime?.IssueBattleCommand(command) ?? BattleRefreshMode.Overlay;
    }

    private void RefreshBattleSelectionState()
    {
        Runtime?.RefreshBattleSelectionState();
    }

    private void UpdateStatus(string message)
    {
        Runtime?.UpdateStatus(message);
    }

    private string FormatCoord(Vector2I coord)
    {
        return Runtime?.FormatCoord(coord) ?? $"({coord.X},{coord.Y})";
    }

    private bool IsBattleActive()
    {
        return Runtime?.IsBattleActive() ?? false;
    }

    private StringName GetSelectedSkillId()
    {
        return Runtime?.GetSelectedBattleSkillId() ?? new StringName("");
    }

    private void SetSelectedSkillId(StringName skillId)
    {
        Runtime?.SetBattleSelectionSkillId(skillId);
    }

    private StringName GetSelectedSkillVariantId()
    {
        return Runtime?.GetSelectedBattleSkillVariantId() ?? new StringName("");
    }

    private void SetSelectedSkillVariantId(StringName optionId)
    {
        Runtime?.SetBattleSelectionSkillVariantId(optionId);
    }

    private StringName GetLastManualUnitId()
    {
        return Runtime?.GetBattleSelectionLastManualUnitId() ?? new StringName("");
    }

    private void SetLastManualUnitId(StringName unitId)
    {
        Runtime?.SetBattleSelectionLastManualUnitId(unitId);
    }

    private List<Vector2I> GetTargetCoordsStateTyped()
    {
        if (Runtime == null)
        {
            return new List<Vector2I>();
        }
        return new List<Vector2I>(Runtime.GetBattleSelectionTargetCoordsStateTyped());
    }

    private void SetTargetCoordsStateTyped(IEnumerable<Vector2I> targetCoords)
    {
        Runtime?.SetBattleSelectionTargetCoordsStateTyped(targetCoords ?? Array.Empty<Vector2I>());
    }

    private void ClearTargetCoordsState()
    {
        SetTargetCoordsStateTyped(Array.Empty<Vector2I>());
    }

    private List<StringName> GetTargetUnitIdsStateTyped()
    {
        if (Runtime == null)
        {
            return new List<StringName>();
        }
        return new List<StringName>(Runtime.GetBattleSelectionTargetUnitIdsStateTyped());
    }

    private void SetTargetUnitIdsStateTyped(IEnumerable<StringName> targetUnitIds)
    {
        Runtime?.SetBattleSelectionTargetUnitIdsStateTyped(targetUnitIds ?? Array.Empty<StringName>());
    }

    private void ClearTargetUnitIdsState()
    {
        SetTargetUnitIdsStateTyped(Array.Empty<StringName>());
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        Runtime?.SetRuntimeBattleSelectedCoord(coord);
    }

    private static GDictionary SelectionOk() => SelectionOkTyped().ToDictionary();

    private static GDictionary SelectionError(string message) =>
        SelectionErrorTyped(message).ToDictionary();

    private static GameRuntimeFacade.RuntimeCommandResult SelectionOkTyped()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success();
    }

    private static GameRuntimeFacade.RuntimeCommandResult SelectionErrorTyped(string message)
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message,
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private static int PosMod(int value, int modulo)
    {
        return modulo <= 0 ? 0 : ((value % modulo) + modulo) % modulo;
    }

    private static List<Vector2I> SortCoordsTyped(IEnumerable<Vector2I> targetCoords)
    {
        var coords = new List<Vector2I>();
        if (targetCoords != null)
        {
            coords.AddRange(targetCoords);
        }
        coords.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return coords;
    }

    private static GVector2IArray SortCoords(IEnumerable<Vector2I> targetCoords)
    {
        return DuplicateVector2IArray(SortCoordsTyped(targetCoords));
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

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
        {
            return null;
        }
        return target;
    }
}
