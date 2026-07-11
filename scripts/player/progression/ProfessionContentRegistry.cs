using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using Godot.Collections;

public class ProfessionContentRegistry : System.IDisposable
{
    private const string ProfessionConfigDirectory = "res://data/configs/professions";
    private static readonly StringName GateContextUnlock = "unlock";
    private static readonly StringName GateContextRank = "rank";

    private readonly record struct TransitionNode(string Node, IReadOnlyList<string> Deps);
    private readonly record struct GateRecord(
        string ProfessionId,
        string Node,
        string ContextLabel,
        string CheckMode
    );

    private readonly System.Collections.Generic.Dictionary<StringName, ProfessionDefinition>
        _professionDefinitions = new();
    private readonly List<string> _validationErrors = new();
    public Array<string> _validation_errors
    {
        get => ToGodotStringArray(_validationErrors);
        set
        {
            _validationErrors.Clear();
            if (value == null)
                return;
            foreach (string error in value)
                _validationErrors.Add(error);
        }
    }
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions =
        SnapshotDefinitions<SkillDefinition>(null);
    private bool _disposed;

    public ProfessionContentRegistry()
    {
        Setup();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _professionDefinitions.Clear();
        _validationErrors.Clear();
        _skillDefinitions = SnapshotDefinitions<SkillDefinition>(null);
    }

    public void Setup(IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null)
    {
        _skillDefinitions = SnapshotDefinitions(skillDefinitions);
        Rebuild();
    }

    public void Rebuild()
    {
        LoadFromDirectory(ProfessionConfigDirectory);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        _professionDefinitions.Clear();
        _validationErrors.Clear();
        ScanDirectory(directoryPath);
        AppendArray(_validationErrors, CollectValidationErrors());
    }

    public IReadOnlyDictionary<StringName, ProfessionDefinition> GetProfessionDefsTyped() =>
        SnapshotDefinitions(_professionDefinitions);

    public Array<string> Validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validationErrors)
            copy.Add(error);
        return copy;
    }

    private void ScanDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validationErrors.Add($"ProfessionContentRegistry could not find {directoryPath}.");
            return;
        }

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"ProfessionContentRegistry could not open {directoryPath}.");
            return;
        }

        try
        {
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
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterProfessionResource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load profession config {resourcePath}.");
            return;
        }
        GodotContentOwnership.RegisterBorrowedContent(resource, resourcePath);
        if (resource is not ProfessionDef professionDef)
        {
            _validationErrors.Add($"Profession config {resourcePath} is not a ProfessionDef.");
            return;
        }
        if (professionDef.profession_id == "")
        {
            _validationErrors.Add($"Profession config {resourcePath} is missing profession_id.");
            return;
        }
        if (_professionDefinitions.ContainsKey(professionDef.profession_id))
        {
            _validationErrors.Add(
                $"Duplicate profession_id registered: {professionDef.profession_id}"
            );
            return;
        }

        try
        {
            ProfessionDefinition professionDefinition = ProfessionDefinition.FromResource(
                professionDef
            );
            _professionDefinitions.Add(
                professionDefinition.ProfessionId,
                professionDefinition
            );
        }
        catch (InvalidDataException exception)
        {
            _validationErrors.Add(
                $"Profession config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private IReadOnlyList<string> CollectValidationErrors() =>
        ValidateDefinitions(_professionDefinitions, _skillDefinitions);

    internal static IReadOnlyList<string> ValidateDefinitions(
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(professionDefinitions);
        ArgumentNullException.ThrowIfNull(skillDefinitions);

        var errors = new List<string>();
        foreach (StringName professionId in SortedProfessionIds(professionDefinitions))
        {
            AppendProfessionValidationErrors(
                errors,
                professionId,
                professionDefinitions[professionId],
                professionDefinitions,
                skillDefinitions
            );
        }

        AppendProfessionRankReachabilityErrors(errors, professionDefinitions);
        return new ReadOnlyCollection<string>(errors);
    }

    private static void AppendProfessionValidationErrors(
        List<string> errors,
        StringName professionId,
        ProfessionDefinition professionDef,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (professionDef.MaxRank <= 0)
            errors.Add($"Profession {professionId} must have max_rank >= 1.");

        if (professionDef.HitDieSides <= 0)
            errors.Add($"Profession {professionId} must have hit_die_sides >= 1.");

        if (professionDef.BabProgressionKind == ProfessionBaseAttackProgression.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported bab_progression {professionDef.BabProgression}."
            );

        if (professionDef.RequiresKnowledgeUnlock() && professionDef.UnlockKnowledgeId == "")
            errors.Add($"Profession {professionId} is missing unlock_knowledge_id.");

        if (professionDef.ReactivationModeKind == ProfessionReactivationMode.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported reactivation_mode {professionDef.ReactivationMode}."
            );

        if (
            professionDef.DependencyVisibilityModeKind
            == ProfessionDependencyVisibilityMode.Unknown
        )
            errors.Add(
                $"Profession {professionId} uses unsupported dependency_visibility_mode {professionDef.DependencyVisibilityMode}."
            );

        AppendUnlockRequirementErrors(
            errors,
            professionId,
            professionDef.UnlockRequirement,
            professionDefinitions,
            skillDefinitions
        );
        AppendGrantedSkillErrors(errors, professionId, professionDef, skillDefinitions);
        AppendActiveConditionErrors(errors, professionId, professionDef.ActiveConditions);
        AppendRankRequirementErrors(
            errors,
            professionId,
            professionDef,
            professionDefinitions
        );
    }

    private static void AppendUnlockRequirementErrors(
        List<string> errors,
        StringName professionId,
        ProfessionPromotionRequirementDefinition unlockRequirement,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (unlockRequirement == null)
            return;

        foreach (StringName requiredSkillId in unlockRequirement.RequiredSkillIds)
        {
            if (requiredSkillId == "")
            {
                errors.Add($"Profession {professionId} has an empty required_skill_id in unlock.");
                continue;
            }
            if (
                skillDefinitions.Count > 0
                && !skillDefinitions.ContainsKey(requiredSkillId)
            )
                errors.Add(
                    $"Profession {professionId} references missing skill {requiredSkillId} in unlock.required_skill_ids."
                );
        }

        AppendProfessionGateErrors(
            errors,
            professionId,
            unlockRequirement.RequiredProfessionRanks,
            "unlock.required_profession_ranks",
            professionDefinitions,
            GateContextUnlock
        );

        foreach (
            AttributeRequirementDefinition attributeRule in unlockRequirement.RequiredAttributeRules
        )
        {
            if (attributeRule == null)
                continue;
            if (attributeRule.AttributeId == "")
                errors.Add(
                    $"Profession {professionId} has an empty attribute_id in unlock.required_attribute_rules."
                );
        }

        foreach (
            ReputationRequirementDefinition reputationRule in unlockRequirement.RequiredReputationRules
        )
        {
            if (reputationRule == null)
                continue;
            if (reputationRule.StateId == "")
                errors.Add(
                    $"Profession {professionId} has an empty state_id in unlock.required_reputation_rules."
                );
        }

        foreach (TagRequirementDefinition tagRule in unlockRequirement.RequiredTagRules)
            AppendTagRuleErrors(errors, professionId, tagRule, "unlock.required_tag_rules");
    }

    private static void AppendRankRequirementErrors(
        List<string> errors,
        StringName professionId,
        ProfessionDefinition professionDef,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions
    )
    {
        var seenTargetRanks = new HashSet<int>();
        foreach (
            ProfessionRankRequirementDefinition rankRequirement in professionDef.RankRequirements
        )
        {
            if (rankRequirement == null)
                continue;

            if (
                rankRequirement.TargetRank < 2
                || rankRequirement.TargetRank > professionDef.MaxRank
            )
            {
                errors.Add(
                    $"Profession {professionId} declares invalid target_rank {rankRequirement.TargetRank}."
                );
            }
            else if (!seenTargetRanks.Add(rankRequirement.TargetRank))
            {
                errors.Add(
                    $"Profession {professionId} declares duplicate rank requirement for rank {rankRequirement.TargetRank}."
                );
            }

            foreach (TagRequirementDefinition tagRule in rankRequirement.RequiredTagRules)
                AppendTagRuleErrors(
                    errors,
                    professionId,
                    tagRule,
                    $"rank_{rankRequirement.TargetRank}.required_tag_rules"
                );

            AppendProfessionGateErrors(
                errors,
                professionId,
                rankRequirement.RequiredProfessionRanks,
                $"rank_{rankRequirement.TargetRank}.required_profession_ranks",
                professionDefinitions,
                GateContextRank,
                rankRequirement.TargetRank
            );
        }

        for (int expectedRank = 2; expectedRank <= professionDef.MaxRank; expectedRank++)
        {
            if (!seenTargetRanks.Contains(expectedRank))
                errors.Add(
                    $"Profession {professionId} is missing a rank requirement for rank {expectedRank}."
                );
        }
    }

    private static void AppendGrantedSkillErrors(
        List<string> errors,
        StringName professionId,
        ProfessionDefinition professionDef,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (ProfessionGrantedSkillDefinition grantedSkill in professionDef.GrantedSkills)
        {
            if (grantedSkill == null)
                continue;
            if (grantedSkill.SkillId == "")
            {
                errors.Add($"Profession {professionId} has a granted skill without skill_id.");
                continue;
            }
            if (
                skillDefinitions.Count > 0
                && !skillDefinitions.ContainsKey(grantedSkill.SkillId)
            )
            {
                errors.Add(
                    $"Profession {professionId} grants missing skill {grantedSkill.SkillId}."
                );
            }
            else if (skillDefinitions.Count > 0)
            {
                skillDefinitions.TryGetValue(
                    grantedSkill.SkillId,
                    out SkillDefinition skillDefinition
                );
                if (
                    skillDefinition != null
                    && skillDefinition.LearnSourceKind != SkillLearnSourceKind.Profession
                )
                    errors.Add(
                        $"Profession {professionId} granted skill {grantedSkill.SkillId} learn_source must be profession, got {skillDefinition.LearnSource}."
                    );
            }
            if (grantedSkill.UnlockRank <= 0 || grantedSkill.UnlockRank > professionDef.MaxRank)
                errors.Add(
                    $"Profession {professionId} grants skill {grantedSkill.SkillId} at invalid unlock_rank {grantedSkill.UnlockRank}."
                );
        }
    }

    private static void AppendActiveConditionErrors(
        List<string> errors,
        StringName professionId,
        IReadOnlyList<ProfessionActiveConditionDefinition> activeConditions
    )
    {
        foreach (ProfessionActiveConditionDefinition activeCondition in activeConditions)
        {
            if (activeCondition == null)
                continue;

            if (
                activeCondition.ConditionKind
                == ProfessionActiveConditionKind.AttributeRange
            )
            {
                if (activeCondition.AttributeId == "")
                    errors.Add(
                        $"Profession {professionId} has an attribute_range active condition without attribute_id."
                    );
            }
            else if (
                activeCondition.ConditionKind
                == ProfessionActiveConditionKind.ReputationRange
            )
            {
                if (activeCondition.StateId == "")
                    errors.Add(
                        $"Profession {professionId} has a reputation_range active condition without state_id."
                    );
            }
            else
            {
                errors.Add(
                    $"Profession {professionId} uses unsupported active condition type {activeCondition.ConditionType}."
                );
            }
        }
    }

    private static void AppendTagRuleErrors(
        List<string> errors,
        StringName professionId,
        TagRequirementDefinition tagRule,
        string contextLabel
    )
    {
        if (tagRule == null)
            return;
        if (tagRule.Tag == "")
            errors.Add(
                $"Profession {professionId} has an empty tag requirement in {contextLabel}."
            );
        if (tagRule.Count <= 0)
            errors.Add(
                $"Profession {professionId} has a non-positive tag count in {contextLabel} for tag {tagRule.Tag}."
            );

        if (tagRule.SkillStateKind == TagRequirementSkillState.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported skill_state {tagRule.SkillState} in {contextLabel}."
            );

        if (tagRule.OriginFilterKind == TagRequirementOriginFilter.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported origin_filter {tagRule.OriginFilter} in {contextLabel}."
            );

        if (tagRule.SelectionRoleKind == TagRequirementSelectionRole.Unknown)
            errors.Add(
                $"Profession {professionId} uses unsupported selection_role {tagRule.SelectionRole} in {contextLabel}."
            );
    }

    private static void AppendProfessionGateErrors(
        List<string> errors,
        StringName professionId,
        IReadOnlyList<ProfessionRankGateDefinition> gates,
        string contextLabel,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions,
        StringName contextKind = default,
        int targetRank = 0
    )
    {
        foreach (ProfessionRankGateDefinition gate in gates)
        {
            if (gate == null)
                continue;

            ProfessionDefinition referencedProfessionDef = null;
            if (gate.ProfessionId == "")
            {
                errors.Add(
                    $"Profession {professionId} has an empty profession gate in {contextLabel}."
                );
                continue;
            }
            if (!professionDefinitions.TryGetValue(
                gate.ProfessionId,
                out referencedProfessionDef
            ))
            {
                errors.Add(
                    $"Profession {professionId} references missing profession {gate.ProfessionId} in {contextLabel}."
                );
            }

            if (gate.MinRank <= 0)
            {
                errors.Add(
                    $"Profession {professionId} requires non-positive min_rank {gate.MinRank} for gate {gate.ProfessionId} in {contextLabel}."
                );
            }
            else if (
                referencedProfessionDef != null
                && gate.MinRank > referencedProfessionDef.MaxRank
            )
            {
                errors.Add(
                    $"Profession {professionId} requires rank {gate.MinRank} for gate {gate.ProfessionId} but {gate.ProfessionId} max_rank is {referencedProfessionDef.MaxRank} in {contextLabel}."
                );
            }
            if (contextKind == GateContextUnlock && gate.ProfessionId == professionId)
                errors.Add($"Profession {professionId} cannot require itself in {contextLabel}.");
            if (
                contextKind == GateContextRank
                && gate.ProfessionId == professionId
                && targetRank > 0
                && gate.MinRank >= targetRank
            )
                errors.Add(
                    $"Profession {professionId} {contextLabel} cannot require self rank {gate.MinRank}."
                );
            if (
                gate.CheckMode != ""
                && gate.CheckModeKind == ProfessionGateCheckMode.Unknown
            )
                errors.Add(
                    $"Profession {professionId} uses unsupported gate check_mode {gate.CheckMode} in {contextLabel}."
                );
        }
    }

    private static void AppendProfessionRankReachabilityErrors(
        List<string> errors,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions
    )
    {
        var transitions = new List<TransitionNode>();
        var gateRecords = new List<GateRecord>();

        foreach (StringName professionId in SortedProfessionIds(professionDefinitions))
        {
            ProfessionDefinition professionDef = professionDefinitions[professionId];
            if (professionDef.MaxRank <= 0)
                continue;

            IReadOnlyList<ProfessionRankGateDefinition> unlockGates =
                professionDef.UnlockRequirement?.RequiredProfessionRanks
                ?? System.Array.Empty<ProfessionRankGateDefinition>();

            transitions.Add(
                new TransitionNode(
                    ProfessionRankNode(professionId, 1),
                    CollectProfessionGateDependencyNodes(
                        professionId,
                        unlockGates,
                        "unlock.required_profession_ranks",
                        GateContextUnlock,
                        1,
                        gateRecords,
                        professionDefinitions
                    )
                )
            );

            foreach (
                ProfessionRankRequirementDefinition rankRequirement in professionDef.RankRequirements
            )
            {
                if (rankRequirement == null)
                    continue;
                int targetRank = rankRequirement.TargetRank;
                if (targetRank < 2 || targetRank > professionDef.MaxRank)
                    continue;

                var deps = new List<string>
                {
                    ProfessionRankNode(professionId, targetRank - 1),
                };
                foreach (
                    string dep in CollectProfessionGateDependencyNodes(
                        professionId,
                        rankRequirement.RequiredProfessionRanks,
                        $"rank_{targetRank}.required_profession_ranks",
                        GateContextRank,
                        targetRank,
                        gateRecords,
                        professionDefinitions
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
                if (!AreProfessionRankDependenciesReachable(transition.Deps, reachableNodes))
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

    private static IReadOnlyList<string> CollectProfessionGateDependencyNodes(
        StringName professionId,
        IReadOnlyList<ProfessionRankGateDefinition> gates,
        string contextLabel,
        StringName contextKind,
        int targetRank,
        List<GateRecord> gateRecords,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions
    )
    {
        var seenNodes = new HashSet<string>();
        var dependencyNodes = new List<string>();
        foreach (ProfessionRankGateDefinition gate in gates)
        {
            if (
                !IsProfessionGateValidForReachability(
                    professionId,
                    gate,
                    contextKind,
                    targetRank,
                    professionDefinitions
                )
            )
                continue;
            string dependencyNode = ProfessionRankNode(gate.ProfessionId, gate.MinRank);
            if (seenNodes.Add(dependencyNode))
                dependencyNodes.Add(dependencyNode);
            gateRecords.Add(
                new GateRecord(
                    professionId.ToString(),
                    dependencyNode,
                    contextLabel,
                    GateCheckModeLabel(
                        ResolveGateCheckModeForValidation(gate, professionDefinitions)
                    )
                )
            );
        }
        return dependencyNodes;
    }

    private static bool IsProfessionGateValidForReachability(
        StringName professionId,
        ProfessionRankGateDefinition gate,
        StringName contextKind,
        int targetRank,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions
    )
    {
        if (gate == null)
            return false;
        if (gate.ProfessionId == "")
            return false;
        if (gate.MinRank <= 0)
            return false;
        if (
            gate.CheckMode != ""
            && gate.CheckModeKind == ProfessionGateCheckMode.Unknown
        )
            return false;
        if (!professionDefinitions.TryGetValue(
            gate.ProfessionId,
            out ProfessionDefinition referencedProfessionDef
        ))
            return false;
        if (gate.MinRank > referencedProfessionDef.MaxRank)
            return false;
        if (contextKind == GateContextUnlock && gate.ProfessionId == professionId)
            return false;
        if (
            contextKind == GateContextRank
            && gate.ProfessionId == professionId
            && targetRank > 0
            && gate.MinRank >= targetRank
        )
            return false;
        return true;
    }

    private static bool AreProfessionRankDependenciesReachable(
        IReadOnlyList<string> deps,
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

    private static string ProfessionRankNode(StringName professionId, int rank)
    {
        return $"{professionId}@{rank}";
    }

    private static ProfessionGateCheckMode ResolveGateCheckModeForValidation(
        ProfessionRankGateDefinition gate,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions
    )
    {
        if (gate == null)
            return ProfessionGateCheckMode.Historical;
        if (gate.CheckMode != "")
            return gate.CheckModeKind;
        if (!professionDefinitions.TryGetValue(
            gate.ProfessionId,
            out ProfessionDefinition sourceProfessionDef
        ))
            return ProfessionGateCheckMode.Historical;
        if (
            sourceProfessionDef.DependencyVisibilityModeKind
            == ProfessionDependencyVisibilityMode.IgnoreWhenHidden
        )
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Historical;
    }

    private static string GateCheckModeLabel(ProfessionGateCheckMode mode)
    {
        return mode switch
        {
            ProfessionGateCheckMode.Historical => "historical",
            ProfessionGateCheckMode.ActiveOnly => "active_only",
            _ => "",
        };
    }

    private static List<StringName> SortedProfessionIds(
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefinitions
    )
    {
        var sortedKeys = new List<string>();
        foreach (StringName professionId in professionDefinitions.Keys)
            sortedKeys.Add(professionId.ToString());
        sortedKeys.Sort(StringComparer.Ordinal);

        var sortedIds = new List<StringName>(sortedKeys.Count);
        foreach (string professionKey in sortedKeys)
            sortedIds.Add(new StringName(professionKey));
        return sortedIds;
    }

    private static IReadOnlyDictionary<StringName, T> SnapshotDefinitions<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        return new ReadOnlyDictionary<StringName, T>(
            source == null
                ? new System.Collections.Generic.Dictionary<StringName, T>()
                : new System.Collections.Generic.Dictionary<StringName, T>(source)
        );
    }

    private static void AppendArray(List<string> target, IEnumerable<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }

    private static Array<string> ToGodotStringArray(IEnumerable<string> values)
    {
        var result = new Array<string>();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value);
        return result;
    }
}
