using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

/// <summary>
/// The single Phase 3 raw-content exception. The boundary may expose only the
/// Enemy/AI authored graph and the BattleSim profiles that embed that graph.
/// Phase 4 must replace every borrower below with immutable definitions and
/// delete this record as one unit.
/// </summary>
internal static class LegacyEnemyContentDebt
{
    internal const string OwnerId = "ProcessContentHost.LegacyEnemyContentRegistry";
    internal const string OwnerDomain = "ProcessContent";
    internal const int DeletePhase = 4;

    internal static LifecycleLegacyDebtSnapshot Record { get; } =
        new(
            OwnerId,
            "scripts/systems/content/ILegacyEnemyContentCatalog.cs",
            OwnerDomain,
            DeletePhase
        );

    internal static IReadOnlyList<string> BorrowerOwners { get; } = Array.AsReadOnly(
        new[]
        {
            "scripts/player/progression/QuestContentValidator.cs",
            "scripts/systems/battle/ai/BattleAiActionAssembler.cs",
            "scripts/systems/battle/ai/BattleAiActionIntent.cs",
            "scripts/systems/battle/ai/BattleAiContext.cs",
            "scripts/systems/battle/ai/BattleAiDecisionEngine.cs",
            "scripts/systems/battle/ai/BattleAiMutationGuard.cs",
            "scripts/systems/battle/ai/BattleAiMutationViolation.cs",
            "scripts/systems/battle/ai/BattleAiRuntimeActionEntry.cs",
            "scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs",
            "scripts/systems/battle/ai/BattleAiScoreProjection.cs",
            "scripts/systems/battle/ai/BattleAiScoreService.cs",
            "scripts/systems/battle/ai/BattleAiService.cs",
            "scripts/systems/battle/ai/BattleAiStateResolver.cs",
            "scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs",
            "scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs",
            "scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs",
            "scripts/systems/battle/runtime/BattleRuntimeModule.cs",
            "scripts/systems/battle/sim/BattleSimContentProvider.cs",
            "scripts/systems/battle/sim/BattleSimFilePayloadProjection.cs",
            "scripts/systems/battle/sim/BattleSimOverrideApplier.cs",
            "scripts/systems/battle/sim/BattleSimOverrideApplyResult.cs",
            "scripts/systems/battle/sim/BattleSimProfileReportEntry.cs",
            "scripts/systems/battle/sim/BattleSimReportBuilder.cs",
            "scripts/systems/battle/sim/BattleSimReportProjection.cs",
            "scripts/systems/battle/sim/BattleSimRunner.cs",
            "scripts/systems/content/GameContentCatalog.cs",
            "scripts/systems/content/GameRoot.cs",
            "scripts/systems/game_runtime/GameRuntimeFacade.cs",
            "scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs",
            "scripts/systems/persistence/GameSession.ContentValidation.cs",
            "scripts/systems/persistence/GameSession.cs",
            "scripts/systems/world/EncounterRosterBuilder.cs",
            "scripts/systems/world/WildEncounterGrowthSystem.cs",
            "scripts/utils/RuntimeResourceFactories.cs",
        }
    );

    // These files define, load, project, or audit the raw authored debt graph itself.
    // They are not runtime borrowers, but remain explicitly enumerated so the reverse
    // source scan cannot silently grow either side of the Phase 3 exception.
    internal static IReadOnlyList<string> AuthoringAndProjectionOwners { get; } =
        Array.AsReadOnly(
            new[]
            {
                "scripts/enemies/actions/MoveToAdvantagePositionAction.cs",
                "scripts/enemies/actions/MoveToRangeAction.cs",
                "scripts/enemies/actions/RetreatAction.cs",
                "scripts/enemies/actions/UseChargeAction.cs",
                "scripts/enemies/actions/UseChargePathAoeAction.cs",
                "scripts/enemies/actions/UseGroundRepositionSkillAction.cs",
                "scripts/enemies/actions/UseGroundSkillAction.cs",
                "scripts/enemies/actions/UseMultiUnitSkillAction.cs",
                "scripts/enemies/actions/UseRandomChainSkillAction.cs",
                "scripts/enemies/actions/UseUnitSkillAction.cs",
                "scripts/enemies/actions/WaitAction.cs",
                "scripts/enemies/DropEntryDef.cs",
                "scripts/enemies/EnemyAiAction.cs",
                "scripts/enemies/EnemyAiBrainDef.cs",
                "scripts/enemies/EnemyAiGenerationSlotDef.cs",
                "scripts/enemies/EnemyAiStateDef.cs",
                "scripts/enemies/EnemyAiTransitionConditionDef.cs",
                "scripts/enemies/EnemyAiTransitionRuleDef.cs",
                "scripts/enemies/EnemyContentRegistry.cs",
                "scripts/enemies/EnemyContentSeed.cs",
                "scripts/enemies/EnemyTemplateDef.cs",
                "scripts/enemies/WildEncounterRosterDef.cs",
                "scripts/enemies/WildEncounterRosterStageDef.cs",
                "scripts/enemies/WildEncounterRosterUnitEntryDef.cs",
                "scripts/systems/battle/ai/BattleAiScoreProfile.cs",
                "scripts/systems/battle/sim/BattleSimProfileDef.cs",
                "scripts/systems/content/ContentSnapshotBuilder.cs",
                "scripts/systems/content/ILegacyEnemyContentCatalog.cs",
                "scripts/systems/content/ProcessContentHost.cs",
                "scripts/utils/GodotTypedResourceGraphWalker.cs",
            }
        );

    private static int _registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;
        LifecycleAuditRegistry.Shared.RegisterLegacyDebt(Record);
    }
}

internal interface ILegacyEnemyContentCatalog
{
    IReadOnlyDictionary<StringName, EnemyTemplateDef> EnemyTemplates { get; }
    IReadOnlyDictionary<StringName, EnemyAiBrainDef> EnemyBrains { get; }
    IReadOnlyDictionary<StringName, WildEncounterRosterDef> EncounterRosters { get; }
    IReadOnlyDictionary<StringName, BattleSimProfileDef> SimulationProfiles { get; }
}
