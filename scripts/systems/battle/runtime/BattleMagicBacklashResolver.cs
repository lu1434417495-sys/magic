using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleMagicBacklashResolver
{
    private static readonly StringName MpMax = "mp_max";

    internal bool ShouldResolveSpellControl(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return combatProfile != null && combatProfile.HasSpellFateControl();
    }

    internal BattleSpellControlResult ApplySpellControlAfterCostResult(
        BattleUnitState source_unit,
        SkillDefinition skillDefinition,
        int skill_level,
        int spent_mp,
        BattleSpellControlMetadata control_metadata,
        BattleEventBatch batch = null
    )
    {
        BattleSpellControlResult result = BattleSpellControlResult.None(control_metadata);

        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            source_unit == null
            || combatProfile == null
            || control_metadata == null
            || !control_metadata.HasResolutionMetadata
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
            int refund = ApplySpellCriticalBonus(source_unit, skillDefinition, spent_mp);
            if (refund > 0)
                AppendLog(
                    batch,
                    $"{UnitLabel(source_unit)} 的魔力回路大成功，返还 {refund} 点法力。"
                );
            return result with { MpRefund = refund };
        }

        if (!control_metadata.CriticalFail)
            return result;

        int protectionLimit = combatProfile.GetFumbleProtectionLimit(skill_level);
        int protectionUsed = GetFumbleProtectionUsed(source_unit, skillDefinition.SkillId);
        if (protectionUsed < protectionLimit)
        {
            SetFumbleProtectionUsed(source_unit, skillDefinition.SkillId, protectionUsed + 1);
            int drained = ApplyFumbleProtectionMpDrain(source_unit, skillDefinition, spent_mp);
            AppendLog(
                batch,
                $"{UnitLabel(source_unit)} 压制了魔力大失败，本场 {SkillLabel(skillDefinition)} 保护次数 {protectionUsed + 1}/{protectionLimit}，额外吞噬 {drained} 点法力。"
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

    internal BattleGroundBacklashTargetResult BuildGroundBacklashTargetCoordsResult(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> target_coords,
        BattleState state,
        BattleGridService grid_service,
        BattleSpellControlResult control_context
    )
    {
        List<Vector2I> safeTargetCoords = DuplicateCoords(target_coords);
        BattleGroundBacklashTargetResult result = new(
            safeTargetCoords,
            control_context.BacklashTriggered,
            new Vector2I(-1, -1),
            new Vector2I(-1, -1),
            Vector2I.Zero,
            false
        );

        if (!result.BacklashTriggered)
            return result;

        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null || !combatProfile.UsesGroundAnchorDriftBacklash())
            return result;

        if (state == null || grid_service == null || safeTargetCoords.Count != 1)
        {
            return result with { BacklashOffsetFallback = true };
        }

        int radius = Mathf.Max(combatProfile.BacklashOffsetRadius, 0);
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

        int pickedIndex = TrueRandomSeedService.RandiRange(0, candidates.Count - 1);
        Vector2I resolvedCoord = candidates[pickedIndex];
        return result with
        {
            TargetCoords = new[] { resolvedCoord },
            ResolvedTargetCoord = resolvedCoord,
            OffsetDelta = resolvedCoord - originalCoord,
        };
    }

    internal void AppendGroundBacklashLog(
        BattleUnitState source_unit,
        SkillDefinition skillDefinition,
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
                $"{UnitLabel(source_unit)} 的 {SkillLabel(skillDefinition)} 未找到可偏移落点，失控魔力仍在原地爆发。"
            );
            return;
        }

        AppendLog(
            batch,
            $"{UnitLabel(source_unit)} 的 {SkillLabel(skillDefinition)} 从 ({originalCoord.X}, {originalCoord.Y}) 偏移到 ({resolvedCoord.X}, {resolvedCoord.Y})。"
        );
    }

    private static int ApplySpellCriticalBonus(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        int spentMp
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (sourceUnit == null || combatProfile == null)
            return 0;
        if (combatProfile.SpellCriticalModeKind != CombatSpellCriticalMode.MpRefund)
            return 0;

        int refundPercent = combatProfile.SpellCriticalMpRefundPercent;
        if (refundPercent <= 0 || spentMp <= 0)
            return 0;

        int refund = Mathf.RoundToInt((float)spentMp * refundPercent / 100.0f);
        refund = Mathf.Clamp(Mathf.Max(refund, 1), 0, spentMp);
        int mpMax = GetMpMax(sourceUnit);
        if (mpMax > 0)
            refund = Mathf.Min(refund, Mathf.Max(mpMax - sourceUnit.current_mp, 0));
        if (refund <= 0)
            return 0;

        sourceUnit.SetCurrentMp(sourceUnit.current_mp + refund);
        return refund;
    }

    private static int ApplyFumbleProtectionMpDrain(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        int spentMp
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (sourceUnit == null || combatProfile == null)
            return 0;

        int drainPercent = combatProfile.FumbleProtectionExtraMpPercent;
        if (drainPercent <= 0 || spentMp <= 0)
            return 0;

        int drain = Mathf.RoundToInt((float)spentMp * drainPercent / 100.0f);
        drain = Mathf.Max(drain, 1);
        drain = Mathf.Min(drain, Mathf.Max(sourceUnit.current_mp, 0));
        sourceUnit.SetCurrentMp(sourceUnit.current_mp - drain);
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
                if (!gridService.IsInside(state, candidate))
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
        return Mathf.Max(sourceUnit.GetFumbleProtectionUsedTyped(skillId), 0);
    }

    private static void SetFumbleProtectionUsed(
        BattleUnitState sourceUnit,
        StringName skillId,
        int value
    )
    {
        if (sourceUnit == null || IsEmpty(skillId))
            return;
        sourceUnit.SetFumbleProtectionUsedTyped(skillId, value);
    }

    private static int GetMpMax(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null)
            return 0;
        return Mathf.Max(sourceUnit.attribute_snapshot.GetValue(MpMax), 0);
    }

    private static void AppendLog(BattleEventBatch batch, string message)
    {
        if (batch == null || string.IsNullOrEmpty(message))
            return;
        batch.AddLogLine(message);
    }

    private static string UnitLabel(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null || string.IsNullOrEmpty(sourceUnit.display_name))
            return "施法者";
        return sourceUnit.display_name;
    }

    private static string SkillLabel(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null)
            return "法术";
        if (!string.IsNullOrEmpty(skillDefinition.DisplayName?.StripEdges()))
            return skillDefinition.DisplayName;
        if (!IsEmpty(skillDefinition.SkillId))
            return skillDefinition.SkillId.ToString();
        return "法术";
    }

    private static List<Vector2I> DuplicateCoords(
        IReadOnlyList<Vector2I> values
    )
    {
        var result = new List<Vector2I>();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }

}
