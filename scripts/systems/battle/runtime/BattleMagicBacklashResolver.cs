using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleMagicBacklashResolver : RefCounted
{
    private static readonly StringName MpMax = "mp_max";
    private static readonly StringName SpellCriticalModeMpRefund = "mp_refund";

    public bool should_resolve_spell_control(SkillDef skill_def)
    {
        CombatSkillDef combatProfile = GetCombatProfile(skill_def);
        return combatProfile != null && combatProfile.has_spell_fate_control();
    }

    public GDictionary apply_spell_control_after_cost(
        BattleUnitState source_unit,
        SkillDef skill_def,
        int skill_level,
        int spent_mp,
        GDictionary control_metadata,
        BattleEventBatch batch = null)
    {
        GDictionary result = new()
        {
            ["skip_effects"] = false,
            ["backlash_triggered"] = false,
            ["fumble_protected"] = false,
            ["mp_refund"] = 0,
            ["extra_mp_drained"] = 0,
            ["spell_control"] = control_metadata?.Duplicate(true) ?? new GDictionary(),
        };

        CombatSkillDef combatProfile = GetCombatProfile(skill_def);
        if (source_unit == null || combatProfile == null || control_metadata == null || control_metadata.Count == 0)
            return result;

        if (GdInterop.GetBool(control_metadata, "reverse_fate_downgraded"))
        {
            AppendLog(batch, $"{UnitLabel(source_unit)} 的逆命护符压住了失控征兆，法术仍按原轨迹释放。");
            return result;
        }

        if (GdInterop.GetBool(control_metadata, "critical_hit"))
        {
            int refund = ApplySpellCriticalBonus(source_unit, skill_def, spent_mp);
            result["mp_refund"] = refund;
            if (refund > 0)
                AppendLog(batch, $"{UnitLabel(source_unit)} 的魔力回路大成功，返还 {refund} 点法力。");
            return result;
        }

        if (!GdInterop.GetBool(control_metadata, "critical_fail"))
            return result;

        int protectionLimit = combatProfile.get_fumble_protection_limit(skill_level);
        int protectionUsed = GetFumbleProtectionUsed(source_unit, skill_def.skill_id);
        if (protectionUsed < protectionLimit)
        {
            SetFumbleProtectionUsed(source_unit, skill_def.skill_id, protectionUsed + 1);
            int drained = ApplyFumbleProtectionMpDrain(source_unit, skill_def, spent_mp);
            result["skip_effects"] = true;
            result["fumble_protected"] = true;
            result["extra_mp_drained"] = drained;
            AppendLog(
                batch,
                $"{UnitLabel(source_unit)} 压制了魔力大失败，本场 {SkillLabel(skill_def)} 保护次数 {protectionUsed + 1}/{protectionLimit}，额外吞噬 {drained} 点法力。");
            return result;
        }

        result["backlash_triggered"] = true;
        AppendLog(batch, $"{UnitLabel(source_unit)} 的魔力控制大失败，法术落点开始偏移。");
        return result;
    }

    public GDictionary build_ground_backlash_target_coords(
        SkillDef skill_def,
        Godot.Collections.Array<Vector2I> target_coords,
        BattleState state,
        GodotObject grid_service,
        GDictionary control_context)
    {
        Godot.Collections.Array<Vector2I> safeTargetCoords = DuplicateCoords(target_coords);
        GDictionary result = new()
        {
            ["target_coords"] = safeTargetCoords,
            ["backlash_triggered"] = GdInterop.GetBool(control_context, "backlash_triggered"),
            ["original_target_coord"] = new Vector2I(-1, -1),
            ["resolved_target_coord"] = new Vector2I(-1, -1),
            ["offset_delta"] = Vector2I.Zero,
            ["backlash_offset_fallback"] = false,
        };

        if (!GdInterop.GetBool(result, "backlash_triggered"))
            return result;

        CombatSkillDef combatProfile = GetCombatProfile(skill_def);
        if (combatProfile == null || !combatProfile.uses_ground_anchor_drift_backlash())
            return result;

        if (state == null || grid_service == null || safeTargetCoords.Count != 1)
        {
            result["backlash_offset_fallback"] = true;
            return result;
        }

        int radius = Mathf.Max(combatProfile.backlash_offset_radius, 0);
        Vector2I originalCoord = safeTargetCoords[0];
        result["original_target_coord"] = originalCoord;
        if (radius <= 0)
        {
            result["resolved_target_coord"] = originalCoord;
            result["backlash_offset_fallback"] = true;
            return result;
        }

        List<Vector2I> candidates = CollectGroundAnchorDriftCandidates(state, grid_service, originalCoord, radius);
        if (candidates.Count == 0)
        {
            result["resolved_target_coord"] = originalCoord;
            result["backlash_offset_fallback"] = true;
            return result;
        }

        int pickedIndex = TrueRandomSeedService.randi_range(0, candidates.Count - 1);
        Vector2I resolvedCoord = candidates[pickedIndex];
        result["target_coords"] = new Godot.Collections.Array<Vector2I> { resolvedCoord };
        result["resolved_target_coord"] = resolvedCoord;
        result["offset_delta"] = resolvedCoord - originalCoord;
        return result;
    }

    public void append_ground_backlash_log(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GDictionary drift_context,
        BattleEventBatch batch)
    {
        if (batch == null || !GdInterop.GetBool(drift_context, "backlash_triggered"))
            return;

        Vector2I originalCoord = GdInterop.GetVector2I(drift_context, "original_target_coord", new Vector2I(-1, -1));
        Vector2I resolvedCoord = GdInterop.GetVector2I(drift_context, "resolved_target_coord", originalCoord);
        if (GdInterop.GetBool(drift_context, "backlash_offset_fallback") || originalCoord == resolvedCoord)
        {
            AppendLog(batch, $"{UnitLabel(source_unit)} 的 {SkillLabel(skill_def)} 未找到可偏移落点，失控魔力仍在原地爆发。");
            return;
        }

        AppendLog(
            batch,
            $"{UnitLabel(source_unit)} 的 {SkillLabel(skill_def)} 从 ({originalCoord.X}, {originalCoord.Y}) 偏移到 ({resolvedCoord.X}, {resolvedCoord.Y})。");
    }

    private static int ApplySpellCriticalBonus(BattleUnitState sourceUnit, SkillDef skillDef, int spentMp)
    {
        CombatSkillDef combatProfile = GetCombatProfile(skillDef);
        if (sourceUnit == null || combatProfile == null)
            return 0;
        if (combatProfile.spell_critical_mode != SpellCriticalModeMpRefund)
            return 0;

        int refundPercent = combatProfile.spell_critical_mp_refund_percent;
        if (refundPercent <= 0 || spentMp <= 0)
            return 0;

        int refund = Mathf.RoundToInt((float)spentMp * refundPercent / 100.0f);
        refund = Mathf.Clamp(Mathf.Max(refund, 1), 0, spentMp);
        int mpMax = GetMpMax(sourceUnit);
        if (mpMax > 0)
            refund = Mathf.Min(refund, Mathf.Max(mpMax - sourceUnit.current_mp, 0));
        if (refund <= 0)
            return 0;

        sourceUnit.current_mp += refund;
        return refund;
    }

    private static int ApplyFumbleProtectionMpDrain(BattleUnitState sourceUnit, SkillDef skillDef, int spentMp)
    {
        CombatSkillDef combatProfile = GetCombatProfile(skillDef);
        if (sourceUnit == null || combatProfile == null)
            return 0;

        int drainPercent = combatProfile.fumble_protection_extra_mp_percent;
        if (drainPercent <= 0 || spentMp <= 0)
            return 0;

        int drain = Mathf.RoundToInt((float)spentMp * drainPercent / 100.0f);
        drain = Mathf.Max(drain, 1);
        drain = Mathf.Min(drain, Mathf.Max(sourceUnit.current_mp, 0));
        sourceUnit.current_mp = Mathf.Max(sourceUnit.current_mp - drain, 0);
        return drain;
    }

    private static List<Vector2I> CollectGroundAnchorDriftCandidates(
        BattleState state,
        GodotObject gridService,
        Vector2I originalCoord,
        int radius)
    {
        List<Vector2I> candidates = new();
        for (int y = originalCoord.Y - radius; y <= originalCoord.Y + radius; y++)
        {
            for (int x = originalCoord.X - radius; x <= originalCoord.X + radius; x++)
            {
                Vector2I candidate = new(x, y);
                if (candidate == originalCoord)
                    continue;
                if (Mathf.Max(Mathf.Abs(candidate.X - originalCoord.X), Mathf.Abs(candidate.Y - originalCoord.Y)) > radius)
                    continue;
                if (!gridService.Call("is_inside", state, candidate).AsBool())
                    continue;
                candidates.Add(candidate);
            }
        }

        candidates.Sort((a, b) =>
        {
            int yCompare = a.Y.CompareTo(b.Y);
            return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
        });
        return candidates;
    }

    private static int GetFumbleProtectionUsed(BattleUnitState sourceUnit, StringName skillId)
    {
        if (sourceUnit == null || GdInterop.IsEmpty(skillId))
            return 0;
        return Mathf.Max(GdInterop.GetInt(sourceUnit.fumble_protection_used, skillId), 0);
    }

    private static void SetFumbleProtectionUsed(BattleUnitState sourceUnit, StringName skillId, int value)
    {
        if (sourceUnit == null || GdInterop.IsEmpty(skillId))
            return;
        sourceUnit.fumble_protection_used[skillId] = Mathf.Max(value, 0);
    }

    private static int GetMpMax(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null)
            return 0;
        return Mathf.Max(sourceUnit.attribute_snapshot.Call("get_value", MpMax).AsInt32(), 0);
    }

    private static void AppendLog(BattleEventBatch batch, string message)
    {
        if (batch == null || string.IsNullOrEmpty(message))
            return;
        batch.log_lines.Add(message);
    }

    private static string UnitLabel(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null || string.IsNullOrEmpty(sourceUnit.display_name))
            return "施法者";
        return sourceUnit.display_name;
    }

    private static string SkillLabel(SkillDef skillDef)
    {
        if (skillDef == null)
            return "法术";
        if (!string.IsNullOrEmpty(skillDef.display_name.StripEdges()))
            return skillDef.display_name;
        if (!GdInterop.IsEmpty(skillDef.skill_id))
            return skillDef.skill_id.ToString();
        return "法术";
    }

    private static CombatSkillDef GetCombatProfile(SkillDef skillDef)
    {
        return skillDef?.combat_profile as CombatSkillDef;
    }

    private static Godot.Collections.Array<Vector2I> DuplicateCoords(Godot.Collections.Array<Vector2I> values)
    {
        Godot.Collections.Array<Vector2I> result = new();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }
}
