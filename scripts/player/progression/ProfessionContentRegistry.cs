using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class ProfessionContentRegistry : RefCounted
{
    private const string ProfessionConfigDirectory = "res://data/configs/professions";
    private static readonly StringName GateContextUnlock = "unlock";
    private static readonly StringName GateContextRank = "rank";

    private readonly record struct TransitionNode(string Node, Array<string> Deps);
    private readonly record struct GateRecord(string ProfessionId, string Node, string ContextLabel, string CheckMode);

    public Dictionary _profession_defs { get; set; } = new();
    public Array<string> _validation_errors { get; set; } = new();
    public Dictionary _skill_defs { get; set; } = new();
    private bool _disposed;

    public ProfessionContentRegistry()
    {
        Setup(new Dictionary());
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeManagedRegistry();
        }
        base.Dispose(disposing);
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        System.GC.SuppressFinalize(this);
        GodotRefCountedDisposer.KeepBorrowedResourceGraphsAlive(_profession_defs);
        GodotRefCountedDisposer.KeepBorrowedResourceGraphsAlive(_skill_defs);
        _profession_defs.Clear();
        _validation_errors.Clear();
        _skill_defs = new Dictionary();
    }

    public void Setup(Dictionary skillDefs = null)
    {
        GodotRefCountedDisposer.KeepBorrowedResourceGraphsAlive(_skill_defs);
        _skill_defs = skillDefs ?? new Dictionary();
        Rebuild();
    }

    public void Rebuild()
    {
        LoadFromDirectory(ProfessionConfigDirectory);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        GodotRefCountedDisposer.KeepBorrowedResourceGraphsAlive(_profession_defs);
        _profession_defs.Clear();
        _validation_errors.Clear();
        ScanDirectory(directoryPath);
        AppendArray(_validation_errors, CollectValidationErrors());
    }

    public IReadOnlyDictionary<StringName, ProfessionDef> GetProfessionDefsTyped()
    {
        var result = new System.Collections.Generic.Dictionary<StringName, ProfessionDef>();
        foreach (string professionKey in ProgressionDataUtils.sorted_string_keys(_profession_defs))
        {
            var professionId = new StringName(professionKey);
            ProfessionDef professionDef = GetTyped<ProfessionDef>(_profession_defs, professionId);
            if (professionDef == null || professionDef.profession_id == "")
                continue;
            result[professionDef.profession_id] = professionDef;
        }
        return result;
    }

    public Array<string> Validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validation_errors)
            copy.Add(error);
        return copy;
    }

    private void ScanDirectory(string directoryPath)
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
                ScanDirectory(entryPath);
                continue;
            }
            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;
            RegisterProfessionResource(entryPath);
        }
        directory.ListDirEnd();
    }

    private void RegisterProfessionResource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load profession config {resourcePath}.");
            return;
        }
        if (resource is not ProfessionDef professionDef)
        {
            GodotRefCountedDisposer.KeepBorrowedResourceGraphAlive(resource);
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

    private Array<string> CollectValidationErrors()
    {
        var errors = new Array<string>();
        foreach (string professionKey in ProgressionDataUtils.sorted_string_keys(_profession_defs))
        {
            var professionId = new StringName(professionKey);
            ProfessionDef professionDef = GetTyped<ProfessionDef>(_profession_defs, professionId);
            if (professionDef == null)
                continue;
            AppendProfessionValidationErrors(errors, professionId, professionDef);
        }

        AppendProfessionRankReachabilityErrors(errors);
        return errors;
    }

    private void AppendProfessionValidationErrors(
        Array<string> errors,
        StringName professionId,
        ProfessionDef professionDef
    )
    {
        if (professionDef.max_rank <= 0)
            errors.Add($"Profession {professionId} must have max_rank >= 1.");

        if (professionDef.hit_die_sides <= 0)
            errors.Add($"Profession {professionId} must have hit_die_sides >= 1.");

        if (professionDef.BabProgressionKind == ProfessionBaseAttackProgression.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported bab_progression {professionDef.bab_progression}."
            );

        if (professionDef.RequiresKnowledgeUnlock() && professionDef.unlock_knowledge_id == "")
            errors.Add($"Profession {professionId} is missing unlock_knowledge_id.");

        if (professionDef.ReactivationModeKind == ProfessionReactivationMode.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported reactivation_mode {professionDef.reactivation_mode}."
            );

        if (
            professionDef.DependencyVisibilityModeKind
            == ProfessionDependencyVisibilityMode.Unknown
        )
            errors.Add(
                $"Profession {professionId} uses unsupported dependency_visibility_mode {professionDef.dependency_visibility_mode}."
            );

        AppendUnlockRequirementErrors(errors, professionId, professionDef.unlock_requirement);
        AppendGrantedSkillErrors(errors, professionId, professionDef);
        AppendActiveConditionErrors(errors, professionId, professionDef.active_conditions);
        AppendRankRequirementErrors(errors, professionId, professionDef);
    }

    private void AppendUnlockRequirementErrors(
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
            if (_skill_defs.Count > 0 && !ContainsKeyExact(_skill_defs, requiredSkillId))
                errors.Add(
                    $"Profession {professionId} references missing skill {requiredSkillId} in unlock.required_skill_ids."
                );
        }

        AppendProfessionGateErrors(
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
            AppendTagRuleErrors(errors, professionId, tagRule, "unlock.required_tag_rules");
    }

    private void AppendRankRequirementErrors(
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
                AppendTagRuleErrors(
                    errors,
                    professionId,
                    tagRule,
                    $"rank_{rankRequirement.target_rank}.required_tag_rules"
                );

            AppendProfessionGateErrors(
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

    private void AppendGrantedSkillErrors(
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
            if (_skill_defs.Count > 0 && !ContainsKeyExact(_skill_defs, grantedSkill.skill_id))
            {
                errors.Add(
                    $"Profession {professionId} grants missing skill {grantedSkill.skill_id}."
                );
            }
            else if (_skill_defs.Count > 0)
            {
                SkillDef skillDef = GetTyped<SkillDef>(_skill_defs, grantedSkill.skill_id);
                if (
                    skillDef != null
                    && skillDef.LearnSourceKind != SkillLearnSourceKind.Profession
                )
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

    private void AppendActiveConditionErrors(
        Array<string> errors,
        StringName professionId,
        Array<ProfessionActiveCondition> activeConditions
    )
    {
        foreach (ProfessionActiveCondition activeCondition in activeConditions)
        {
            if (activeCondition == null)
                continue;

            if (
                activeCondition.ConditionKind
                == ProfessionActiveConditionKind.AttributeRange
            )
            {
                if (activeCondition.attribute_id == "")
                    errors.Add(
                        $"Profession {professionId} has an attribute_range active condition without attribute_id."
                    );
            }
            else if (
                activeCondition.ConditionKind
                == ProfessionActiveConditionKind.ReputationRange
            )
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

    private void AppendTagRuleErrors(
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

        if (tagRule.SkillStateKind == TagRequirementSkillState.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported skill_state {tagRule.skill_state} in {contextLabel}."
            );

        if (tagRule.OriginFilterKind == TagRequirementOriginFilter.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported origin_filter {tagRule.origin_filter} in {contextLabel}."
            );

        if (tagRule.SelectionRoleKind == TagRequirementSelectionRole.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported selection_role {tagRule.selection_role} in {contextLabel}."
            );
    }

    private void AppendProfessionGateErrors(
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
            if (!ContainsKeyExact(_profession_defs, gate.profession_id))
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
            if (
                gate.check_mode != ""
                && gate.CheckModeKind == ProfessionGateCheckMode.Unknown
            )
                errors.Add(
                    $"Profession {professionId} uses unsupported gate check_mode {gate.check_mode} in {contextLabel}."
                );
        }
    }

    private void AppendProfessionRankReachabilityErrors(Array<string> errors)
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
                    ProfessionRankNode(professionId, 1),
                    CollectProfessionGateDependencyNodes(
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
                    ProfessionRankNode(professionId, targetRank - 1),
                };
                foreach (
                    string dep in CollectProfessionGateDependencyNodes(
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
                        ProfessionRankNode(professionId, targetRank),
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

    private Array<string> CollectProfessionGateDependencyNodes(
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
                !IsProfessionGateValidForReachability(
                    professionId,
                    gate,
                    contextKind,
                    targetRank
                )
            )
                continue;
            string dependencyNode = ProfessionRankNode(gate.profession_id, gate.min_rank);
            if (seenNodes.Add(dependencyNode))
                dependencyNodes.Add(dependencyNode);
            gateRecords.Add(
                new GateRecord(
                    professionId.ToString(),
                    dependencyNode,
                    contextLabel,
                    ProfessionRankGate
                        .ToStringName(ResolveGateCheckModeForValidation(gate))
                        .ToString()
                )
            );
        }
        return dependencyNodes;
    }

    private bool IsProfessionGateValidForReachability(
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
        if (
            gate.check_mode != ""
            && gate.CheckModeKind == ProfessionGateCheckMode.Unknown
        )
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

    private string ProfessionRankNode(StringName professionId, int rank)
    {
        return $"{professionId}@{rank}";
    }

    private ProfessionGateCheckMode ResolveGateCheckModeForValidation(ProfessionRankGate gate)
    {
        if (gate == null)
            return ProfessionGateCheckMode.Historical;
        if (gate.check_mode != "")
            return gate.CheckModeKind;
        ProfessionDef sourceProfessionDef = GetTyped<ProfessionDef>(
            _profession_defs,
            gate.profession_id
        );
        if (sourceProfessionDef == null)
            return ProfessionGateCheckMode.Historical;
        if (
            sourceProfessionDef.DependencyVisibilityModeKind
            == ProfessionDependencyVisibilityMode.IgnoreWhenHidden
        )
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Historical;
    }

    private static T GetTyped<T>(Dictionary dictionary, StringName key)
        where T : class
    {
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotObject() as T;
        return null;
    }

    private static bool ContainsKeyExact(Dictionary dictionary, StringName key) =>
        dictionary.ContainsKey(key);

    private static void AppendArray(Array<string> target, Array<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }
}
