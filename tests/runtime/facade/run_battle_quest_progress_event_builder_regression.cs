using System.Collections.Generic;
using Godot;

public partial class run_battle_quest_progress_event_builder_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestDefeatedEnemyTemplatesAreAggregated();
        TestNonPlayerVictoryDoesNotProgressQuests();

        RequestTestExit(_test.Finish("Battle quest progress event builder regression"));
    }

    private void TestDefeatedEnemyTemplatesAreAggregated()
    {
        BattleState battleState = BuildBattleState();
        EncounterAnchorData encounterAnchor = BuildEncounterAnchor();

        List<QuestProgressService.QuestProgressEventData> events =
            GameRuntimeFacade.BuildBattleDefeatQuestProgressEventsTyped(
                battleState,
                encounterAnchor,
                BattleOutcomeKind.PlayerSuccess,
                12
            );

        _test.Eq(events.Count, 2, "战斗任务进度应按实际击败的敌方模板聚合。");
        if (events.Count != 2)
            return;

        QuestProgressService.QuestProgressEventData alphaEvent = events[0];
        QuestProgressService.QuestProgressEventData wolfEvent = events[1];
        _test.Eq(alphaEvent.TargetId, new StringName("wolf_alpha"), "聚合结果应稳定按模板 ID 排序。");
        _test.Eq(alphaEvent.ProgressDelta, 1, "击败一只狼王应增加一点狼王目标进度。");
        _test.Eq(wolfEvent.TargetId, new StringName("wolf_pack"), "普通荒狼应形成独立目标事件。");
        _test.Eq(wolfEvent.ProgressDelta, 2, "只应统计两只已被击败的普通荒狼。");
        _test.Eq(wolfEvent.ContextData.EnemyTemplateId, new StringName("wolf_pack"), "事件上下文应保留实际敌方模板。");
        _test.Eq(wolfEvent.EncounterId, encounterAnchor.entity_id, "事件应保留来源遭遇 ID。");
        _test.Eq(wolfEvent.WorldStep, 12, "事件应保留结算时世界步数。");
    }

    private void TestNonPlayerVictoryDoesNotProgressQuests()
    {
        List<QuestProgressService.QuestProgressEventData> events =
            GameRuntimeFacade.BuildBattleDefeatQuestProgressEventsTyped(
                BuildBattleState(),
                BuildEncounterAnchor(),
                BattleOutcomeKind.PlayerFailure,
                12
            );

        _test.Eq(events.Count, 0, "玩家未获胜时不应生成击败敌人任务进度。");
    }

    private static BattleState BuildBattleState()
    {
        BattleUnitState deadWolfA = BuildEnemy("wolf_a", "wolf_pack", false);
        BattleUnitState deadWolfB = BuildEnemy("wolf_b", "wolf_pack", false);
        BattleUnitState livingWolf = BuildEnemy("wolf_c", "wolf_pack", true);
        BattleUnitState deadAlpha = BuildEnemy("alpha", "wolf_alpha", false);
        BattleState state = new();
        state.SetUnits(new[] { deadWolfA, deadWolfB, livingWolf, deadAlpha });
        state.enemy_unit_ids = new Godot.Collections.Array<StringName>
        {
            deadWolfA.unit_id,
            deadWolfB.unit_id,
            livingWolf.unit_id,
            deadAlpha.unit_id,
        };
        return state;
    }

    private static BattleUnitState BuildEnemy(
        StringName unitId,
        StringName enemyTemplateId,
        bool isAlive
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            enemy_template_id = enemyTemplateId,
            faction_id = "hostile",
        };
        unit.SetCurrentHp(isAlive ? 10 : 0);
        return unit;
    }

    private static EncounterAnchorData BuildEncounterAnchor() =>
        new()
        {
            entity_id = "wild_tutorial_wolves",
            encounter_profile_id = "wolf_wilds",
            encounter_kind = "single",
        };
}
