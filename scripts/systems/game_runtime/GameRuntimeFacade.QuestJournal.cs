using Godot.Collections;

public sealed partial class GameRuntimeFacade
{
    internal QuestJournalWindowData GetQuestJournalWindowDataTyped() =>
        GameRuntimeQuestJournalBuilder.Build(_party_state, _content_catalog, GetGenerationDefinition());

    internal RuntimeCommandResult CommandOpenQuestJournalTyped() => ExecuteLoggedCommandTyped(
        "quest.journal.open", "quest", new Dictionary(), () =>
        {
            if (_generation_definition == null || _party_state == null)
                return BuildCommandErrorResult("世界地图尚未初始化。");
            if (IsBattleActive() || IsModalWindowOpenInternal())
                return BuildCommandErrorResult("请先返回大地图，再查看任务日志。");
            SetRuntimeActiveModalKind(RuntimeModalKind.QuestJournal);
            UpdateStatusInternal("已打开任务日志。");
            return BuildCommandOkResult();
        });
}
