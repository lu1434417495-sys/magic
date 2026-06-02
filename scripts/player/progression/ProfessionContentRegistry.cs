using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class ProfessionContentRegistry : RefCounted
{
    private const string ProfessionConfigDirectory = "res://data/configs/professions";
    private static readonly StringName GateContextUnlock = "unlock";
    private static readonly StringName GateContextRank = "rank";

    private static readonly HashSet<StringName> ValidBabProgressions = new()
    {
        "full",
        "three_quarter",
        "half",
    };

    private static readonly HashSet<StringName> ValidReactivationModes = new() { "auto", "manual" };

    private static readonly HashSet<StringName> ValidDependencyVisibilityModes = new()
    {
        "count_when_hidden",
        "ignore_when_hidden",
    };

    private static readonly HashSet<StringName> ValidGateCheckModes = new()
    {
        "historical",
        "active_only",
    };

    private readonly record struct TransitionNode(string Node, Array<string> Deps);
    private readonly record struct GateRecord(string ProfessionId, string Node, string ContextLabel, string CheckMode);

    public Dictionary _profession_defs { get; set; } = new();
    public Array<string> _validation_errors { get; set; } = new();
    public Dictionary _skill_defs { get; set; } = new();

    public ProfessionContentRegistry()
    {
        System.GC.SuppressFinalize(this);
        setup(new Dictionary());
    }

    public static string profession_config_directory() => ProfessionConfigDirectory;

    public void setup(Dictionary skillDefs = null)
    {
        _skill_defs = skillDefs ?? new Dictionary();
        rebuild();
    }

    public void rebuild()
    {
        load_from_directory(ProfessionConfigDirectory);
    }

    public void load_from_directory(string directoryPath)
    {
        _profession_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
        AppendArray(_validation_errors, _collect_validation_errors());
    }

    public Dictionary get_profession_defs() => _profession_defs.Duplicate();

    public Array<string> validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validation_errors)
            copy.Add(error);
        return copy;
    }

    public void _scan_directory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validation_errors.Add($"ProfessionContentRegistry could not find {directoryPath}.");
            return;
        }

        using var directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validation_errors.Add($"ProfessionContentRegistry could not open {directoryPath}.");
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string entryName = directory.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;
            if (entryName == "." || entryName == "..")
                continue;

            string entryPath = $"{directoryPath}/{entryName}";
            if (directory.CurrentIsDir())
            {
                _scan_directory(entryPath);
                continue;
            }
            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;
            _register_profession_resource(entryPath);
        }
        directory.ListDirEnd();
    }

    public void _register_profession_resource(string resourcePath)
    {
        var resource = GodotContentResourceLifetime.Keep(GD.Load<Resource>(resourcePath));
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load profession config {resourcePath}.");
            return;
        }
        if (resource is not ProfessionDef professionDef)
        {
            _validation_errors.Add($"Profession config {resourcePath} is not a ProfessionDef.");
            return;
        }
        if (professionDef.profession_id == "")
        {
            _validation_errors.Add($"Profession config {resourcePath} is missing profession_id.");
            return;
        }
        if (_profession_defs.ContainsKey(professionDef.profession_id))
        {
            _validation_errors.Add(
                $"Duplicate profession_id registered: {professionDef.profession_id}"
            );
            return;
        }

        _profession_defs[professionDef.profession_id] = professionDef;
    }

    public Array<string> _collect_validation_errors()
    {
        var errors = new Array<string>();
        foreach (string professionKey in ProgressionDataUtils.sorted_string_keys(_profession_defs))
        {
            var professionId = new StringName(professionKey);
            ProfessionDef professionDef = GetTyped<ProfessionDef>(_profession_defs, professionId);
            if (professionDef == null)
                continue;
            _append_profession_validation_errors(errors, professionId, professionDef);
        }

        _append_profession_rank_reachability_errors(errors);
        return errors;
    }

    public void _append_profession_validation_errors(
        Array<string> errors,
        StringName professionId,
        ProfessionDef professionDef
    )
    {
        if (professionDef.max_rank <= 0)
            errors.Add($"Profession {professionId} must have max_rank >= 1.");

        if (professionDef.hit_die_sides <= 0)
            errors.Add($"Profession {professionId} must have hit_die_sides >= 1.");

        if (!ValidBabProgressions.Contains(professionDef.bab_progression))
            errors.Add(
                $"Profession {professionId} uses unsupported bab_progression {professionDef.bab_progression}."
            );

        if (professionDef.requires_knowledge_unlock() && professionDef.unlock_knowledge_id == "")
            errors.Add($"Profession {professionId} is missing unlock_knowledge_id.");

        if (!ValidReactivationModes.Contains(professionDef.reactivation_mode))
            errors.Add(
                $"Profession {professionId} uses unsupported reactivation_mode {professionDef.reactivation_mode}."
            );

        if (!ValidDependencyVisibilityModes.Contains(professionDef.dependency_visibility_mode))
            errors.Add(
                $"Profession {professionId} uses unsupported dependency_visibility_mode {professionDef.dependency_visibility_mode}."
            );

        _append_unlock_requirement_errors(errors, professionId, professionDef.unlock_requirement);
        _append_granted_skill_errors(errors, professionId, professionDef);
        _append_active_condition_errors(errors, professionId, professionDef.active_conditions);
        _append_rank_requirement_errors(errors, professionId, professionDef);
    }

    public void _append_unlock_requirement_errors(
        Array<string> errors,
        StringName professionId,
        ProfessionPromotionRequirement unlockRequirement
    )
    {
        if (unlockRequirement == null)
            return;

        foreach (StringName requiredSkillId in unlockRequirement.required_skill_ids)
        {
            if (requiredSkillId == "")
            {
                errors.Add($"Profession {professionId} has an empty required_skill_id in unlock.");
                continue;
            }
            if (_skill_defs.Count > 0 && !ContainsKeyFlexible(_skill_defs, requiredSkillId))
                errors.Add(
                    $"Profession {professionId} references missing skill {requiredSkillId} in unlock.required_skill_ids."
                );
        }

        _append_profession_gate_errors(
            errors,
            professionId,
            unlockRequirement.required_profession_ranks,
            "unlock.required_profession_ranks",
            GateContextUnlock
        );

        foreach (AttributeRequirement attributeRule in unlockRequirement.required_attribute_rules)
        {
            if (attributeRule == null)
                continue;
            if (attributeRule.attribute_id == "")
                errors.Add(
                    $"Profession {professionId} has an empty attribute_id in unlock.required_attribute_rules."
                );
        }

        foreach (
            ReputationRequirement reputationRule in unlockRequirement.required_reputation_rules
        )
        {
            if (reputationRule == null)
                continue;
            if (reputationRule.state_id == "")
                errors.Add(
                    $"Profession {professionId} has an empty state_id in unlock.required_reputation_rules."
                );
        }

        foreach (TagRequirement tagRule in unlockRequirement.required_tag_rules)
            _append_tag_rule_errors(errors, professionId, tagRule, "unlock.required_tag_rules");
    }

    public void _append_rank_requirement_errors(
        Array<string> errors,
        StringName professionId,
        ProfessionDef professionDef
    )
    {
        var seenTargetRanks = new HashSet<int>();
        foreach (ProfessionRankRequirement rankRequirement in professionDef.rank_requirements)
        {
            if (rankRequirement == null)
                continue;

            if (
                rankRequirement.target_rank < 2
                || rankRequirement.target_rank > professionDef.max_rank
            )
            {
                errors.Add(
                    $"Profession {professionId} declares invalid target_rank {rankRequirement.target_rank}."
                );
            }
            else if (!seenTargetRanks.Add(rankRequirement.target_rank))
            {
                errors.Add(
                    $"Profession {professionId} declares duplicate rank requirement for rank {rankRequirement.target_rank}."
                );
            }

            foreach (TagRequirement tagRule in rankRequirement.required_tag_rules)
                _append_tag_rule_errors(
                    errors,
                    professionId,
                    tagRule,
                    $"rank_{rankRequirement.target_rank}.required_tag_rules"
                );

            _append_profession_gate_errors(
                errors,
                professionId,
                rankRequirement.required_profession_ranks,
                $"rank_{rankRequirement.target_rank}.required_profession_ranks",
                GateContextRank,
                rankRequirement.target_rank
            );
        }

        for (int expectedRank = 2; expectedRank <= professionDef.max_rank; expectedRank++)
        {
            if (!seenTargetRanks.Contains(expectedRank))
                errors.Add(
                    $"Profession {professionId} is missing a rank requirement for rank {expectedRank}."
                );
        }
    }

    public void _append_granted_skill_errors(
        Array<string> errors,
        StringName professionId,
        ProfessionDef professionDef
    )
    {
        foreach (ProfessionGrantedSkill grantedSkill in professionDef.granted_skills)
        {
            if (grantedSkill == null)
                continue;
            if (grantedSkill.skill_id == "")
            {
                errors.Add($"Profession {professionId} has a granted skill without skill_id.");
                continue;
            }
            if (_skill_defs.Count > 0 && !ContainsKeyFlexible(_skill_defs, grantedSkill.skill_id))
            {
                errors.Add(
                    $"Profession {professionId} grants missing skill {grantedSkill.skill_id}."
                );
            }
            else if (_skill_defs.Count > 0)
            {
                SkillDef skillDef = GetTyped<SkillDef>(_skill_defs, grantedSkill.skill_id);
                if (skillDef != null && skillDef.learn_source != "profession")
                    errors.Add(
                        $"Profession {professionId} granted skill {grantedSkill.skill_id} learn_source must be profession, got {skillDef.learn_source}."
                    );
            }
            if (grantedSkill.unlock_rank <= 0 || grantedSkill.unlock_rank > professionDef.max_rank)
                errors.Add(
                    $"Profession {professionId} grants skill {grantedSkill.skill_id} at invalid unlock_rank {grantedSkill.unlock_rank}."
                );
        }
    }

    public void _append_active_condition_errors(
        Array<string> errors,
        StringName professionId,
        Array<ProfessionActiveCondition> activeConditions
    )
    {
        foreach (ProfessionActiveCondition activeCondition in activeConditions)
        {
            if (activeCondition == null)
                continue;

            if (activeCondition.condition_type == "attribute_range")
            {
                if (activeCondition.attribute_id == "")
                    errors.Add(
                        $"Profession {professionId} has an attribute_range active condition without attribute_id."
                    );
            }
            else if (activeCondition.condition_type == "reputation_range")
            {
                if (activeCondition.state_id == "")
                    errors.Add(
                        $"Profession {professionId} has a reputation_range active condition without state_id."
                    );
            }
            else
            {
                errors.Add(
                    $"Profession {professionId} uses unsupported active condition type {activeCondition.condition_type}."
                );
            }
        }
    }

    public void _append_tag_rule_errors(
        Array<string> errors,
        StringName professionId,
        TagRequirement tagRule,
        string contextLabel
    )
    {
        if (tagRule == null)
            return;
        if (tagRule.tag == "")
            errors.Add(
                $"Profession {professionId} has an empty tag requirement in {contextLabel}."
            );
        if (tagRule.count <= 0)
            errors.Add(
                $"Profession {professionId} has a non-positive tag count in {contextLabel} for tag {tagRule.tag}."
            );

        StringName normalizedSkillState = tagRule.get_normalized_skill_state();
        if (tagRule.skill_state != normalizedSkillState)
            errors.Add(
                $"Profession {professionId} uses unsupported skill_state {tagRule.skill_state} in {contextLabel}."
            );

        StringName normalizedOriginFilter = tagRule.get_normalized_origin_filter();
        if (tagRule.origin_filter != normalizedOriginFilter)
            errors.Add(
                $"Profession {professionId} uses unsupported origin_filter {tagRule.origin_filter} in {contextLabel}."
            );

        StringName normalizedSelectionRole = tagRule.get_normalized_selection_role();
        if (tagRule.selection_role != normalizedSelectionRole)
            errors.Add(
                $"Profession {professionId} uses unsupported selection_role {tagRule.selection_role} in {contextLabel}."
            );
    }

    public void _append_profession_gate_errors(
        Array<string> errors,
        StringName professionId,
        Array<ProfessionRankGate> gates,
        string contextLabel,
        StringName contextKind = default,
        int targetRank = 0
    )
    {
        foreach (ProfessionRankGate gate in gates)
        {
            if (gate == null)
                continue;

            ProfessionDef referencedProfessionDef = null;
            if (gate.profession_id == "")
            {
                errors.Add(
                    $"Profession {professionId} has an empty profession gate in {contextLabel}."
                );
                continue;
            }
            if (!ContainsKeyFlexible(_profession_defs, gate.profession_id))
            {
                errors.Add(
                    $"Profession {professionId} references missing profession {gate.profession_id} in {contextLabel}."
                );
            }
            else
            {
                referencedProfessionDef = GetTyped<ProfessionDef>(
                    _profession_defs,
                    gate.profession_id
                );
            }

            if (gate.min_rank <= 0)
            {
                errors.Add(
                    $"Profession {professionId} requires non-positive min_rank {gate.min_rank} for gate {gate.profession_id} in {contextLabel}."
                );
            }
            else if (
                referencedProfessionDef != null
                && gate.min_rank > referencedProfessionDef.max_rank
            )
            {
                errors.Add(
                    $"Profession {professionId} requires rank {gate.min_rank} for gate {gate.profession_id} but {gate.profession_id} max_rank is {referencedProfessionDef.max_rank} in {contextLabel}."
                );
            }
            if (contextKind == GateContextUnlock && gate.profession_id == professionId)
                errors.Add($"Profession {professionId} cannot require itself in {contextLabel}.");
            if (
                contextKind == GateContextRank
                && gate.profession_id == professionId
                && targetRank > 0
                && gate.min_rank >= targetRank
            )
                errors.Add(
                    $"Profession {professionId} {contextLabel} cannot require self rank {gate.min_rank}."
                );
            if (gate.check_mode != "" && !ValidGateCheckModes.Contains(gate.check_mode))
                errors.Add(
                    $"Profession {professionId} uses unsupported gate check_mode {gate.check_mode} in {contextLabel}."
                );
        }
    }

    public void _append_profession_rank_reachability_errors(Array<string> errors)
    {
        var transitions = new List<TransitionNode>();
        var gateRecords = new List<GateRecord>();

        foreach (string professionKey in ProgressionDataUtils.sorted_string_keys(_profession_defs))
        {
            var professionId = new StringName(professionKey);
            ProfessionDef professionDef = GetTyped<ProfessionDef>(_profession_defs, professionId);
            if (professionDef == null || professionDef.max_rank <= 0)
                continue;

            var unlockGates = new Array<ProfessionRankGate>();
            if (professionDef.unlock_requirement != null)
                unlockGates = professionDef.unlock_requirement.required_profession_ranks;

            transitions.Add(
                new TransitionNode(
                    _profession_rank_node(professionId, 1),
                    _collect_profession_gate_dependency_nodes(
                        professionId,
                        unlockGates,
                        "unlock.required_profession_ranks",
                        GateContextUnlock,
                        1,
                        gateRecords
                    )
                )
            );

            foreach (ProfessionRankRequirement rankRequirement in professionDef.rank_requirements)
            {
                if (rankRequirement == null)
                    continue;
                int targetRank = rankRequirement.target_rank;
                if (targetRank < 2 || targetRank > professionDef.max_rank)
                    continue;

                var deps = new Array<string>
                {
                    _profession_rank_node(professionId, targetRank - 1),
                };
                foreach (
                    string dep in _collect_profession_gate_dependency_nodes(
                        professionId,
                        rankRequirement.required_profession_ranks,
                        $"rank_{targetRank}.required_profession_ranks",
                        GateContextRank,
                        targetRank,
                        gateRecords
                    )
                )
                {
                    deps.Add(dep);
                }

                transitions.Add(
                    new TransitionNode(
                        _profession_rank_node(professionId, targetRank),
                        deps
                    )
                );
            }
        }

        var reachableNodes = new HashSet<string>();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (TransitionNode transition in transitions)
            {
                string node = transition.Node;
                if (node.Length == 0 || reachableNodes.Contains(node))
                    continue;
                if (!_are_profession_rank_dependencies_reachable(transition.Deps, reachableNodes))
                    continue;
                reachableNodes.Add(node);
                changed = true;
            }
        }

        foreach (GateRecord gateRecord in gateRecords)
        {
            string dependencyNode = gateRecord.Node;
            if (dependencyNode.Length == 0 || reachableNodes.Contains(dependencyNode))
                continue;
            errors.Add(
                $"Profession {gateRecord.ProfessionId} has structurally unreachable profession gate {dependencyNode} in {gateRecord.ContextLabel} (check_mode={gateRecord.CheckMode})."
            );
        }
    }

    private Array<string> _collect_profession_gate_dependency_nodes(
        StringName professionId,
        Array<ProfessionRankGate> gates,
        string contextLabel,
        StringName contextKind,
        int targetRank,
        List<GateRecord> gateRecords
    )
    {
        var seenNodes = new HashSet<string>();
        var dependencyNodes = new Array<string>();
        foreach (ProfessionRankGate gate in gates)
        {
            if (
                !_is_profession_gate_valid_for_reachability(
                    professionId,
                    gate,
                    contextKind,
                    targetRank
                )
            )
                continue;
            string dependencyNode = _profession_rank_node(gate.profession_id, gate.min_rank);
            if (seenNodes.Add(dependencyNode))
                dependencyNodes.Add(dependencyNode);
            gateRecords.Add(
                new GateRecord(
                    professionId.ToString(),
                    dependencyNode,
                    contextLabel,
                    _resolve_gate_check_mode_for_validation(gate).ToString()
                )
            );
        }
        return dependencyNodes;
    }

    public bool _is_profession_gate_valid_for_reachability(
        StringName professionId,
        ProfessionRankGate gate,
        StringName contextKind,
        int targetRank
    )
    {
        if (gate == null)
            return false;
        if (gate.profession_id == "")
            return false;
        if (gate.min_rank <= 0)
            return false;
        if (gate.check_mode != "" && !ValidGateCheckModes.Contains(gate.check_mode))
            return false;
        ProfessionDef referencedProfessionDef = GetTyped<ProfessionDef>(
            _profession_defs,
            gate.profession_id
        );
        if (referencedProfessionDef == null)
            return false;
        if (gate.min_rank > referencedProfessionDef.max_rank)
            return false;
        if (contextKind == GateContextUnlock && gate.profession_id == professionId)
            return false;
        if (
            contextKind == GateContextRank
            && gate.profession_id == professionId
            && targetRank > 0
            && gate.min_rank >= targetRank
        )
            return false;
        return true;
    }

    private bool _are_profession_rank_dependencies_reachable(
        Array<string> deps,
        HashSet<string> reachableNodes
    )
    {
        foreach (string dependencyNode in deps)
        {
            if (dependencyNode.Length == 0)
                continue;
            if (!reachableNodes.Contains(dependencyNode))
                return false;
        }
        return true;
    }

    public string _profession_rank_node(StringName professionId, int rank)
    {
        return $"{professionId}@{rank}";
    }

    public StringName _resolve_gate_check_mode_for_validation(ProfessionRankGate gate)
    {
        if (gate == null)
            return "historical";
        if (gate.check_mode != "")
            return gate.check_mode;
        ProfessionDef sourceProfessionDef = GetTyped<ProfessionDef>(
            _profession_defs,
            gate.profession_id
        );
        if (sourceProfessionDef == null)
            return "historical";
        if (sourceProfessionDef.dependency_visibility_mode == "ignore_when_hidden")
            return "active_only";
        return "historical";
    }

    private static T GetTyped<T>(Dictionary dictionary, StringName key)
        where T : class
    {
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotObject() as T;
        string keyText = key.ToString();
        if (dictionary.ContainsKey(keyText))
            return dictionary[keyText].AsGodotObject() as T;
        var keyName = new StringName(keyText);
        if (dictionary.ContainsKey(keyName))
            return dictionary[keyName].AsGodotObject() as T;
        return null;
    }

    private static bool ContainsKeyFlexible(Dictionary dictionary, StringName key)
    {
        if (dictionary.ContainsKey(key))
            return true;
        string keyText = key.ToString();
        return dictionary.ContainsKey(keyText) || dictionary.ContainsKey(new StringName(keyText));
    }

    private static void AppendArray(Array<string> target, Array<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }
}
