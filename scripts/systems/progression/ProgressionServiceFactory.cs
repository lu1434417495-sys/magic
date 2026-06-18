using System.Collections.Generic;
using Godot;

internal sealed class ProgressionServiceFactory
{
    internal ProgressionService Build(
        UnitProgress progression,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs
    )
    {
        var assignmentService = new ProfessionAssignmentService();
        assignmentService.Setup(progression, skillDefs, professionDefs);

        var mergeService = new SkillMergeService();
        mergeService.Setup(progression, skillDefs, assignmentService);

        var ruleService = new ProfessionRuleService();
        ruleService.Setup(progression, skillDefs, professionDefs);

        var progressionService = new ProgressionService();
        progressionService.Setup(
            progression,
            skillDefs,
            professionDefs,
            ruleService,
            assignmentService,
            mergeService
        );
        return progressionService;
    }
}
