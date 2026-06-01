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
        BattleEventBatch batch = null
    )
    {
        return apply_spell_control_after_cost_result(
            source_unit,
            skill_def,
            skill_level,
            spent_mp,
            control_metadata,
            batch
        ).ToDictionary();
    }

    public BattleSpellControlResult apply_spell_control_after_cost_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        int skill_level,
        int spent_mp,
        GDictionary control_metadata,
        BattleEventBatch batch = null
    )
    {
        return apply_spell_control_after_cost_result(
            source_unit,
            skill_def,
            skill_level,
            spent_mp,
            BattleSpellControlMetadata.FromDictionary(control_metadata),
            batch
        );
    }

    public BattleSpellControlResult apply_spell_control_after_cost_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        int skill_level,
        int spent_mp,
        BattleSpellControlMetadata control_metadata,
        BattleEventBatch batch = null
    )
    {
        BattleSpellControlResult result = BattleSpellControlResult.None(control_metadata);

        CombatSkillDef combatProfile = GetCombatProfile(skill_def);
        if (
            source_unit == null
            || combatProfile == null
            || control_metadata == null
            || !control_metadata.HasPayload
        )
            return result;

        if (control_metadata.ReverseFateDowngraded)
        {
            AppendLog(
                batch,
                $"{UnitLabel(source_unit)} 的逆命护符压住了失控征兆，法术仍按原轨迹释放。"
            );
            return result;
        }

        if (control_metadata.CriticalHit)
        {
            int refund = ApplySpellCriticalBonus(source_unit, skill_def, spent_mp);
            if (refund > 0)
                AppendLog(
                    batch,
                    $"{UnitLabel(source_unit)} 的魔力回路大成功，返还 {refund} 点法力。"
                );
            return result with { MpRefund = refund };
        }

        if (!control_metadata.CriticalFail)
            return result;

        int protectionLimit = combatProfile.get_fumble_protection_limit(skill_level);
        int protectionUsed = GetFumbleProtectionUsed(source_unit, skill_def.skill_id);
        if (protectionUsed < protectionLimit)
        {
            SetFumbleProtectionUsed(source_unit, skill_def.skill_id, protectionUsed + 1);
            int drained = ApplyFumbleProtectionMpDrain(source_unit, skill_def, spent_mp);
            AppendLog(
                batch,
                $"{UnitLabel(source_unit)} 压制了魔力大失败，本场 {SkillLabel(skill_def)} 保护次数 {protectionUsed + 1}/{protectionLimit}，额外吞噬 {drained} 点法力。"
            );
            return result with
            {
                SkipEffects = true,
                FumbleProtected = true,
                ExtraMpDrained = drained,
            };
        }

        AppendLog(batch, $"{UnitLabel(source_unit)} 的魔力控制大失败，法术落点开始偏移。");
        return result with { BacklashTriggered = true };
    }

    public GDictionary build_ground_backlash_target_coords(
        SkillDef skill_def,
        Godot.Collections.Array<Vector2I> target_coords,
        BattleState state,
        BattleGridService grid_service,
        GDictionary control_context
    )
    {
        return build_ground_backlash_target_coords_result(
            skill_def,
            target_coords,
            state,
            grid_service,
            ToSpellControlResult(control_context)
        ).ToDictionary();
    }

    public BattleGroundBacklashTargetResult build_ground_backlash_target_coords_result(
        SkillDef skill_def,
        Godot.Collections.Array<Vector2I> target_coords,
        BattleState state,
        BattleGridService grid_service,
        BattleSpellControlResult control_context
    )
    {
        Godot.Collections.Array<Vector2I> safeTargetCoords = DuplicateCoords(target_coords);
        BattleGroundBacklashTargetResult result = new(
            ToVector2IList(safeTargetCoords),
            control_context.BacklashTriggered,
            new Vector2I(-1, -1),
            new Vector2I(-1, -1),
            Vector2I.Zero,
            false
        );

        if (!result.BacklashTriggered)
            return result;

        CombatSkillDef combatProfile = GetCombatProfile(skill_def);
        if (combatProfile == null || !combatProfile.uses_ground_anchor_drift_backlash())
            return result;

        if (state == null || grid_service == null || safeTargetCoords.Count != 1)
        {
            return result with { BacklashOffsetFallback = true };
        }

        int radius = Mathf.Max(combatProfile.backlash_offset_radius, 0);
        Vector2I originalCoord = safeTargetCoords[0];
        result = result with { OriginalTargetCoord = originalCoord };
        if (radius <= 0)
        {
            return result with
            {
                ResolvedTargetCoord = originalCoord,
                BacklashOffsetFallback = true,
            };
        }

        List<Vector2I> candidates = CollectGroundAnchorDriftCandidates(
            state,
            grid_service,
            originalCoord,
            radius
        );
        if (candidates.Count == 0)
        {
            return result with
            {
                ResolvedTargetCoord = originalCoord,
                BacklashOffsetFallback = true,
            };
        }

        int pickedIndex = TrueRandomSeedService.randi_range(0, candidates.Count - 1);
        Vector2I resolvedCoord = candidates[pickedIndex];
        return result with
        {
            TargetCoords = new[] { resolvedCoord },
            ResolvedTargetCoord = resolvedCoord,
            OffsetDelta = resolvedCoord - originalCoord,
        };
    }

    public void append_ground_backlash_log(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GDictionary drift_context,
        BattleEventBatch batch
    )
    {
        append_ground_backlash_log(
            source_unit,
            skill_def,
            ToGroundBacklashTargetResult(drift_context),
            batch
        );
    }

    public void append_ground_backlash_log(
        BattleUnitState source_unit,
        SkillDef skill_def,
        BattleGroundBacklashTargetResult drift_context,
        BattleEventBatch batch
    )
    {
        if (batch == null || !drift_context.BacklashTriggered)
            return;

        Vector2I originalCoord = drift_context.OriginalTargetCoord;
        Vector2I resolvedCoord =
            drift_context.ResolvedTargetCoord != new Vector2I(-1, -1)
                ? drift_context.ResolvedTargetCoord
                : originalCoord;
        if (
            drift_context.BacklashOffsetFallback
            || originalCoord == resolvedCoord
        )
        {
            AppendLog(
                batch,
                $"{UnitLabel(source_unit)} 的 {SkillLabel(skill_def)} 未找到可偏移落点，失控魔力仍在原地爆发。"
            );
            return;
        }

        AppendLog(
            batch,
            $"{UnitLabel(source_unit)} 的 {SkillLabel(skill_def)} 从 ({originalCoord.X}, {originalCoord.Y}) 偏移到 ({resolvedCoord.X}, {resolvedCoord.Y})。"
        );
    }

    private static int ApplySpellCriticalBonus(
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        int spentMp
    )
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

    private static int ApplyFumbleProtectionMpDrain(
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        int spentMp
    )
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
        BattleGridService gridService,
        Vector2I originalCoord,
        int radius
    )
    {
        List<Vector2I> candidates = new();
        for (int y = originalCoord.Y - radius; y <= originalCoord.Y + radius; y++)
        {
            for (int x = originalCoord.X - radius; x <= originalCoord.X + radius; x++)
            {
                Vector2I candidate = new(x, y);
                if (candidate == originalCoord)
                    continue;
                if (
                    Mathf.Max(
                        Mathf.Abs(candidate.X - originalCoord.X),
                        Mathf.Abs(candidate.Y - originalCoord.Y)
                    ) > radius
                )
                    continue;
                if (!gridService.is_inside(state, candidate))
                    continue;
                candidates.Add(candidate);
            }
        }

        candidates.Sort(
            (a, b) =>
            {
                int yCompare = a.Y.CompareTo(b.Y);
                return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
            }
        );
        return candidates;
    }

    private static int GetFumbleProtectionUsed(BattleUnitState sourceUnit, StringName skillId)
    {
        if (sourceUnit == null || IsEmpty(skillId))
            return 0;
        return Mathf.Max(ReadInt(sourceUnit.fumble_protection_used, skillId), 0);
    }

    private static void SetFumbleProtectionUsed(
        BattleUnitState sourceUnit,
        StringName skillId,
        int value
    )
    {
        if (sourceUnit == null || IsEmpty(skillId))
            return;
        sourceUnit.fumble_protection_used[skillId] = Mathf.Max(value, 0);
    }

    private static int GetMpMax(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null)
            return 0;
        return Mathf.Max(sourceUnit.attribute_snapshot.get_value(MpMax), 0);
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
        if (!IsEmpty(skillDef.skill_id))
            return skillDef.skill_id.ToString();
        return "法术";
    }

    private static CombatSkillDef GetCombatProfile(SkillDef skillDef)
    {
        return skillDef?.combat_profile as CombatSkillDef;
    }

    private static Godot.Collections.Array<Vector2I> DuplicateCoords(
        Godot.Collections.Array<Vector2I> values
    )
    {
        Godot.Collections.Array<Vector2I> result = new();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static List<Vector2I> ToVector2IList(Godot.Collections.Array<Vector2I> values)
    {
        var result = new List<Vector2I>();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static BattleSpellControlResult ToSpellControlResult(GDictionary data) =>
        BattleSpellControlResult.FromDictionary(data);

    private static BattleGroundBacklashTargetResult ToGroundBacklashTargetResult(
        GDictionary data
    ) => BattleGroundBacklashTargetResult.FromDictionary(data);

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }

    private static int ReadInt(GDictionary data, StringName key, int fallback = 0)
    {
        if (data == null || IsEmpty(key) || !data.ContainsKey(key))
            return fallback;
        return data[key].AsInt32();
    }

}
