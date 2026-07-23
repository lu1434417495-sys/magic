using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class EnemyAiAction : Resource
{
    [Export]
    public StringName action_id { get; set; } = "";

    [Export]
    public StringName score_bucket_id { get; set; } = "";

    [Export]
    public StringName action_intent { get; set; } = "positioning";

    public virtual Godot.Collections.Array<string> ValidateSchema() =>
        _collect_base_validation_errors();

    internal Godot.Collections.Array<StringName> GetDeclaredSkillIds()
    {
        var result = new Godot.Collections.Array<StringName>();
        var seen = new HashSet<StringName>();
        _append_declared_skill_id(result, seen, Get("skill_id"));
        Variant skillIds = Get("skill_ids");
        if (skillIds.VariantType == Variant.Type.Array)
        {
            foreach (Variant rawSkillId in skillIds.AsGodotArray())
                _append_declared_skill_id(result, seen, rawSkillId);
        }
        Variant rangeSkillIds = Get("range_skill_ids");
        if (rangeSkillIds.VariantType == Variant.Type.Array)
        {
            foreach (Variant rawSkillId in rangeSkillIds.AsGodotArray())
                _append_declared_skill_id(result, seen, rawSkillId);
        }
        return result;
    }

    internal EnemyAiActionDefinition ToDefinition() =>
        EnemyAiActionDefinition.FromResource(this);

    internal Godot.Collections.Array<string> ValidateSkillReferences(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var errors = new Godot.Collections.Array<string>();
        skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();
        foreach (StringName skillId in GetDeclaredSkillIds())
        {
            if (skillId == "")
            {
                errors.Add($"AI action {action_id} references an empty skill_id.");
            }
            else if (
                !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
                || skillDefinition == null
            )
            {
                errors.Add($"AI action {action_id} references missing skill {skillId}.");
            }
            else
            {
                EnemyAiActionSkillCompatibilityResult compatibility =
                    EvaluateSkillCompatibility(skillDefinition);
                if (!compatibility.IsCompatible)
                {
                    errors.Add(
                        $"AI action {action_id} ({GetType().Name}) references incompatible "
                            + $"skill {skillId}: {compatibility.Reason}."
                    );
                }
            }
        }
        return errors;
    }

    internal EnemyAiActionSkillCompatibilityResult EvaluateSkillCompatibility(
        SkillDefinition skillDefinition,
        int? skillLevel = null
    ) =>
        EnemyAiActionSkillCompatibilityRules.Evaluate(
            GetActionKind(),
            skillDefinition,
            this is UseMultiUnitSkillAction multiUnitAction
                ? multiUnitAction.candidate_pool_limit
                : int.MaxValue,
            skillLevel
        );

    internal EnemyAiActionKind GetActionKind() =>
        this switch
        {
            MoveToMultiUnitSkillPositionAction => EnemyAiActionKind.MoveToMultiUnitSkillPosition,
            UseUnitSkillAction => EnemyAiActionKind.UseUnitSkill,
            UseGroundSkillAction => EnemyAiActionKind.UseGroundSkill,
            UseMultiUnitSkillAction => EnemyAiActionKind.UseMultiUnitSkill,
            UseRandomChainSkillAction => EnemyAiActionKind.UseRandomChainSkill,
            UseChargeAction => EnemyAiActionKind.UseCharge,
            UseChargePathAoeAction => EnemyAiActionKind.UseChargePathAoe,
            MoveToRangeAction => EnemyAiActionKind.MoveToRange,
            MoveToAdvantagePositionAction => EnemyAiActionKind.MoveToAdvantagePosition,
            UseGroundRepositionSkillAction => EnemyAiActionKind.UseGroundRepositionSkill,
            RetreatAction => EnemyAiActionKind.Retreat,
            WaitAction => EnemyAiActionKind.Wait,
            _ => throw new System.InvalidOperationException(
                $"Unsupported EnemyAiAction authoring type {GetType().Name}."
            ),
        };

    protected Godot.Collections.Array<string> _collect_base_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        if (action_id == "")
            errors.Add("AI action is missing action_id.");
        Variant targetSelector = Get("target_selector");
        if (
            targetSelector.VariantType == Variant.Type.String
            || targetSelector.VariantType == Variant.Type.StringName
        )
        {
            StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
            if (selector != "" && !EnemyAiTargetSelectorRules.IsSupportedSelector(selector))
            {
                errors.Add(
                    $"AI action {action_id} has unsupported target_selector {selector}."
                );
            }
        }
        return errors;
    }

    protected void _append_enemy_focus_target_selector_errors(
        Godot.Collections.Array<string> errors,
        string actionLabel,
        StringName selector
    )
    {
        if (!EnemyAiTargetSelectorRules.IsEnemyFocusSelector(selector))
            errors.Add($"{actionLabel} {action_id} has unsupported target_selector {selector}.");
    }

    protected static void _append_declared_skill_id(
        Godot.Collections.Array<StringName> results,
        HashSet<StringName> seen,
        Variant rawSkillId
    )
    {
        if (
            rawSkillId.VariantType != Variant.Type.String
            && rawSkillId.VariantType != Variant.Type.StringName
        )
        {
            return;
        }
        StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
        if (seen.Add(skillId))
            results.Add(skillId);
    }
}
