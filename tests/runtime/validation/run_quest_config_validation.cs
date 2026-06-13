using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_quest_config_validation : SceneTree
{
    private static readonly string[] QuestConfigPaths =
    {
        "res://data/configs/quests/main_world_quests.json",
        "res://data/configs/quests/ashen_intersection_quests.json",
        "res://data/configs/quests/bounty_quests.json",
    };

    private readonly TestHarness _test = new();
    private int _passes;

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        using ItemContentRegistry itemRegistry = new();
        using SkillContentRegistry skillRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();

        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        Dictionary<StringName, SkillDef> skillDefs = new(skillRegistry.GetSkillDefsTyped());
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();

        foreach (string questConfigPath in QuestConfigPaths)
        {
            ValidateQuestFile(questConfigPath, itemDefs, skillDefs, enemyTemplates);
        }

        Quit(_test.Finish("Quest config validation"));
    }

    private void ValidateQuestFile(
        string path,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        if (!FileAccess.FileExists(path))
        {
            _test.Fail($"Quest config file not found: {path}");
            return;
        }

        string jsonText;
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read))
        {
            if (file == null)
            {
                _test.Fail($"Quest config file could not be opened: {path}");
                return;
            }
            jsonText = file.GetAsText();
        }

        using Json json = new();
        Error parseResult = json.Parse(jsonText);
        if (parseResult != Error.Ok)
        {
            _test.Fail(
                $"JSON parse error in {path}: {json.GetErrorMessage()} at line {json.GetErrorLine()}"
            );
            return;
        }

        Variant data = json.Data;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            _test.Fail($"Quest config root must be a Dictionary in {path}.");
            return;
        }

        GDictionary root = data.AsGodotDictionary();
        if (!root.ContainsKey("quests"))
        {
            _test.Fail($"Missing 'quests' array in {path}");
            return;
        }

        Variant questsValue = root["quests"];
        if (questsValue.VariantType != Variant.Type.Array)
        {
            _test.Fail($"Field 'quests' must be an Array in {path}");
            return;
        }

        GArray quests = questsValue.AsGodotArray();
        Dictionary<StringName, QuestDef> questDefs = new();
        List<QuestDef> loadedQuestDefs = new();
        try
        {
            for (int i = 0; i < quests.Count; i++)
            {
                Variant questValue = quests[i];
                if (questValue.VariantType != Variant.Type.Dictionary)
                {
                    _test.Fail($"[{path}] Quest entry {i} must be a Dictionary.");
                    continue;
                }

                GDictionary questDict = questValue.AsGodotDictionary();
                string questId = DictString(questDict, "quest_id", "unknown");

                Variant convertedValue = ConvertFloatsToInts(questValue);
                if (convertedValue.VariantType != Variant.Type.Dictionary)
                {
                    _test.Fail($"[{path}] Quest conversion failed for quest '{questId}'.");
                    continue;
                }
                GDictionary convertedQuest = convertedValue.AsGodotDictionary();

                QuestDef questDef = QuestDef.FromDictionary(convertedQuest);
                if (questDef == null)
                {
                    _test.Fail($"[{path}] QuestDef.from_dict returned null for quest '{questId}'");
                    continue;
                }

                loadedQuestDefs.Add(questDef);
                GStringArray schemaErrors = questDef.ValidateSchema();
                if (schemaErrors.Count > 0)
                {
                    foreach (string error in schemaErrors)
                    {
                        _test.Fail($"[{path}] Schema error in quest '{questId}': {error}");
                    }
                    continue;
                }

                questDefs[questDef.quest_id] = questDef;
                _passes++;
            }

            List<string> validationErrors = QuestContentValidator.ValidateTyped(
                questDefs,
                itemDefs,
                skillDefs,
                enemyTemplates,
                new List<string>()
            );
            foreach (string error in validationErrors)
            {
                _test.Fail($"[{path}] Validation error: {error}");
            }
        }
        finally
        {
            foreach (QuestDef questDef in loadedQuestDefs)
            {
                questDef.Dispose();
            }
        }
    }

    private static Variant ConvertFloatsToInts(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary => Variant.From(
                ConvertDictionaryFloatsToInts(value.AsGodotDictionary())
            ),
            Variant.Type.Array => Variant.From(ConvertArrayFloatsToInts(value.AsGodotArray())),
            Variant.Type.Float => Variant.From((int)value.AsDouble()),
            _ => value,
        };
    }

    private static GDictionary ConvertDictionaryFloatsToInts(GDictionary source)
    {
        GDictionary result = new();
        foreach (Variant key in source.Keys)
        {
            result[NormalizeKey(key)] = ConvertFloatsToInts(source[key]);
        }
        return result;
    }

    private static GArray ConvertArrayFloatsToInts(GArray source)
    {
        GArray result = new();
        foreach (Variant item in source)
        {
            result.Add(ConvertFloatsToInts(item));
        }
        return result;
    }

    private static Variant NormalizeKey(Variant key)
    {
        return key.VariantType switch
        {
            Variant.Type.String => Variant.From(key.AsString()),
            Variant.Type.StringName => Variant.From(key.AsStringName()),
            _ => key,
        };
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }

        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }
}
