using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class AiAssemblerProbe
{
    private readonly BattleAiActionAssembler _assembler = new();

    public GDictionary StatsAssemble { get; private set; } = AiProbeStats.NewStats();

    public BattleAiRuntimeActionPlan BuildUnitActionPlan(
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        AiTraceRecorder.Enter("build_unit_action_plan");
        ulong start = Time.GetTicksUsec();
        BattleAiRuntimeActionPlan result = _assembler.BuildUnitActionPlan(
            unitState,
            brain,
            skillDefinitions
        );
        AiProbeStats.Record(StatsAssemble, (long)(Time.GetTicksUsec() - start));
        AiTraceRecorder.Exit("build_unit_action_plan");
        return result;
    }

    public void ResetStats()
    {
        StatsAssemble = AiProbeStats.NewStats();
    }
}
