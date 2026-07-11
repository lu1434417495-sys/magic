using System.Collections.Generic;
using Godot;

internal sealed class ProgressionServiceFactory
{
    internal ProgressionService Build(
        UnitProgress progression,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefs
    )
    {
        var assignmentService = new ProfessionAssignmentService();
        assignmentService.Setup(progression, skillDefinitions, professionDefs);

        var mergeService = new SkillMergeService();
        mergeService.Setup(progression, skillDefinitions, assignmentService);

        var ruleService = new ProfessionRuleService();
        ruleService.Setup(progression, skillDefinitions, professionDefs);

        var progressionService = new ProgressionService();
        progressionService.SetupDefinitions(
            progression,
            skillDefinitions,
            professionDefs,
            ruleService,
            assignmentService,
            mergeService
        );
        return progressionService;
    }
}
