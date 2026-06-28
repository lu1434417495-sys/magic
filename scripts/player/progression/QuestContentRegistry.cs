using System.Collections.Generic;
using Godot;

internal sealed class QuestContentRegistry
{
    private const string QuestConfigDirectory = "res://data/configs/quests";

    private readonly Dictionary<StringName, QuestDef> _questDefs = new();
    private readonly List<string> _validationErrors = new();

    public void Rebuild()
    {
        LoadFromDirectory(QuestConfigDirectory);
    }

    internal void LoadFromDirectory(string directoryPath)
    {
        _questDefs.Clear();
        _validationErrors.Clear();

        string globalPath = ProjectSettings.GlobalizePath(directoryPath);
        if (!DirAccess.DirExistsAbsolute(globalPath))
        {
            _validationErrors.Add($"QuestContentRegistry could not find {directoryPath}.");
            return;
        }

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"QuestContentRegistry could not open {directoryPath}.");
            return;
        }

        try
        {
            string[] files = directory.GetFiles();
            foreach (string fileName in files)
            {
                if (!fileName.EndsWith(".tres"))
                    continue;

                string resourcePath = $"{directoryPath}/{fileName}";
                RegisterQuestResource(resourcePath);
            }
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterQuestResource(string resourcePath)
    {
        Resource resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"QuestContentRegistry failed to load {resourcePath}.");
            return;
        }

        if (resource is not QuestDef questDef)
        {
            _validationErrors.Add($"QuestContentRegistry: {resourcePath} is not a QuestDef.");
            return;
        }

        GodotContentOwnership.RegisterBorrowedContent(
            questDef,
            resourcePath
        );

        StringName questId = questDef.quest_id;
        if (questId == "")
        {
            _validationErrors.Add($"QuestContentRegistry: {resourcePath} is missing quest_id.");
            return;
        }

        if (_questDefs.ContainsKey(questId))
        {
            _validationErrors.Add($"QuestContentRegistry: duplicate quest_id '{questId}' (conflict with {_questDefs[questId]}).");
            return;
        }

        _questDefs[questId] = questDef;
    }

    internal IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped() => _questDefs;

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors;
}
