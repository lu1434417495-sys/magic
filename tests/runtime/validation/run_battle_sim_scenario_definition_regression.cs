using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_scenario_definition_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            AssertAuthoringProjectionIsDetachedAndSchemaStable();
            AssertStringNameKeyedSnapshotsRoundTrip();
            AssertFormalTerrainSkipsExplicitCellParsing();
            AssertRuntimeSignaturesRejectAuthoredResources();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle sim scenario definition regression"));
    }

    private void AssertAuthoringProjectionIsDetachedAndSchemaStable()
    {
        StringName skillId = "definition_probe_skill";
        var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "definition_probe_unit",
            display_name = "Definition Probe",
            coord = new Vector2I(1, 2),
            current_hp = 18,
            skill_ids = new GArray { skillId },
            skill_level_map = new GDictionary { [skillId] = 3 },
        };
        var allies = new GArray { unitSpec };
        allies.Add(default(Variant));
        var cellOverride = new GDictionary
        {
            ["coord"] = new Vector2I(0, 0),
            ["base_height"] = 8,
        };
        var scenario = new BattleSimScenarioDef
        {
            scenario_id = "definition_probe",
            display_name = "Definition Probe Scenario",
            description = "project once",
            map_size = new Vector2I(3, 2),
            terrain_profile_id = "definition_terrain",
            world_coord = new Vector2I(4, 5),
            ally_units = allies,
            enemy_units = new GArray(),
            cell_overrides = new Godot.Collections.Array<GDictionary> { cellOverride },
            timeline_ticks_per_step = 2,
            tu_per_tick = 7,
            max_iterations = 19,
            manual_policy = "wait",
            trace_enabled = false,
            seeds = new[] { 17, 23 },
        };

        BattleSimScenarioDefinition definition = scenario.ToDefinition();

        scenario.scenario_id = "mutated_scenario";
        scenario.seeds[0] = 999;
        scenario.ally_units.Clear();
        unitSpec.unit_id = "mutated_unit";
        unitSpec.coord = new Vector2I(9, 9);
        unitSpec.skill_level_map[skillId] = 99;
        cellOverride["base_height"] = 99;

        _test.Eq(definition.ScenarioId.ToString(), "definition_probe", "scenario id should be detached from its authored Resource");
        _test.Eq(definition.Seeds.Count, 2, "scenario seeds should preserve authored cardinality");
        _test.Eq(definition.Seeds[0], 17, "scenario seeds should be copied at projection time");
        _test.Eq(definition.AuthoringAllyUnitCount, 2, "report schema should retain the raw authored ally count, including Nil entries");
        _test.Eq(definition.AllyUnits.Count, 1, "runtime unit definitions should still skip authored Nil entries");

        BattleUnitState firstState = definition.AllyUnits[0].UnitDefinition.CreateRuntimeState();
        firstState.unit_id = "mutated_runtime_copy";
        firstState.SetKnownSkillLevelTyped(skillId, 44, preserveZero: true);
        BattleUnitState secondState = definition.AllyUnits[0].UnitDefinition.CreateRuntimeState();
        _test.False(ReferenceEquals(firstState, secondState), "each simulation run should receive a fresh BattleUnitState");
        _test.Eq(secondState.unit_id.ToString(), "definition_probe_unit", "runtime state should not observe authored or prior-run mutation");
        _test.Eq(secondState.coord, new Vector2I(1, 2), "runtime coord should remain the projected coord");
        _test.Eq(secondState.GetKnownSkillLevelTyped(skillId), 3, "nested skill maps should remain detached and stable");
        AssertPlainGraph(definition.AllyUnits[0].UnitDefinition.UnitSnapshot, "unit_snapshot");

        using GodotProjectionLease<GDictionary> contextLease =
            definition.BuildStartContextLease();
        GDictionary context = contextLease.Value;
        GArray battleParty = context["battle_party"].AsGodotArray();
        _test.Eq(battleParty.Count, 1, "runtime context should contain only valid projected units");
        GDictionary cells = context["cells"].AsGodotDictionary();
        GDictionary cell = cells[new Vector2I(0, 0)].AsGodotDictionary();
        _test.Eq(cell["base_height"].AsInt32(), 8, "cell snapshots should be deep-copied before authored mutation");

        Dictionary<string, object> fileFacts =
            BattleSimFilePayloadProjection.BuildScenarioFacts(definition);
        _test.Eq(Convert.ToInt32(fileFacts["ally_unit_count"]), 2, "file projection should preserve the authored ally count schema");
        using GodotProjectionLease<GDictionary> reportLease =
            BattleSimReportProjection.BuildScenarioLease(definition);
        _test.Eq(reportLease.Value["ally_unit_count"].AsInt32(), 2, "Godot report projection should preserve the authored ally count schema");
    }

    private void AssertStringNameKeyedSnapshotsRoundTrip()
    {
        StringName skillId = "definition_cooldown_probe";
        using var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "definition_cooldown_unit",
            display_name = "Definition Cooldown Unit",
            coord = new Vector2I(2, 1),
        };
        BattleUnitState state = unitSpec
            .ToDefinition("player", "manual")
            .CreateRuntimeState();
        state.SetCooldownTyped(skillId, 4);

        BattleSimUnitDefinition definition = BattleSimUnitDefinition.FromProjectedState(
            state,
            "definition_cooldown_probe"
        );
        _test.True(
            definition.UnitSnapshot["cooldowns"] is IReadOnlyDictionary<string, object>,
            "StringName-keyed nested maps should normalize to immutable string-keyed maps"
        );
        BattleUnitState roundTrip = definition.CreateRuntimeState();
        _test.Eq(roundTrip.GetCooldownTyped(skillId), 4, "non-empty StringName-keyed cooldowns should survive definition round-trip");
        AssertPlainGraph(definition.UnitSnapshot, "cooldown_snapshot");
    }

    private void AssertFormalTerrainSkipsExplicitCellParsing()
    {
        var scenario = new BattleSimScenarioDef
        {
            scenario_id = "formal_terrain_probe",
            use_formal_terrain_generation = true,
            map_size = new Vector2I(5, 4),
            cell_overrides = null,
        };

        BattleSimScenarioDefinition definition = scenario.ToDefinition();
        using GodotProjectionLease<GDictionary> contextLease =
            definition.BuildStartContextLease();
        _test.False(
            contextLease.Value.ContainsKey("cells"),
            "formal terrain projection should not parse or emit explicit cell overrides"
        );
        _test.Eq(
            contextLease.Value["battle_map_size"].AsVector2I(),
            new Vector2I(5, 4),
            "formal terrain projection should preserve battle_map_size"
        );
    }

    private void AssertRuntimeSignaturesRejectAuthoredResources()
    {
        Type[] runtimeOwners =
        {
            typeof(BattleSimScenarioDefinition),
            typeof(BattleSimUnitDefinition),
            typeof(BattleSimScenarioUnitEntry),
            typeof(BattleSimScenarioReport),
            typeof(BattleSimExecutionLoop),
            typeof(BattleSimRunner),
            typeof(BattleSimReportProjection),
            typeof(BattleSimFilePayloadProjection),
        };

        foreach (Type owner in runtimeOwners)
            AssertNoAuthoredResourceSignature(owner);
    }

    private void AssertNoAuthoredResourceSignature(Type owner)
    {
        const BindingFlags flags = BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in owner.GetFields(flags))
            AssertNotAuthoredType(field.FieldType, $"{owner.Name}.{field.Name}");
        foreach (PropertyInfo property in owner.GetProperties(flags))
        {
            AssertNotAuthoredType(property.PropertyType, $"{owner.Name}.{property.Name}");
            foreach (ParameterInfo parameter in property.GetIndexParameters())
                AssertNotAuthoredType(parameter.ParameterType, $"{owner.Name}.{property.Name}[index]");
        }
        foreach (ConstructorInfo constructor in owner.GetConstructors(flags))
        foreach (ParameterInfo parameter in constructor.GetParameters())
            AssertNotAuthoredType(parameter.ParameterType, $"{owner.Name}.ctor({parameter.Name})");
        foreach (MethodInfo method in owner.GetMethods(flags))
        {
            AssertNotAuthoredType(method.ReturnType, $"{owner.Name}.{method.Name} return");
            foreach (ParameterInfo parameter in method.GetParameters())
                AssertNotAuthoredType(parameter.ParameterType, $"{owner.Name}.{method.Name}({parameter.Name})");
        }
    }

    private void AssertNotAuthoredType(Type type, string path)
    {
        if (type == null)
            return;
        Type normalized = type.IsByRef || type.IsPointer || type.IsArray
            ? type.GetElementType()
            : type;
        if (normalized == typeof(BattleSimScenarioDef) || normalized == typeof(BattleSimUnitSpec))
        {
            _test.Fail($"{path} must not retain an authored simulation Resource signature");
            return;
        }
        if (normalized?.IsGenericType != true)
            return;
        foreach (Type argument in normalized.GetGenericArguments())
            AssertNotAuthoredType(argument, path);
    }

    private void AssertPlainGraph(object value, string path)
    {
        if (value == null)
            return;
        if (value is Variant || value is GodotObject || value is GDictionary || value is GArray)
        {
            _test.Fail($"{path} should contain only detached plain values, found {value.GetType().Name}");
            return;
        }
        if (value is IReadOnlyDictionary<string, object> dictionary)
        {
            foreach (KeyValuePair<string, object> entry in dictionary)
                AssertPlainGraph(entry.Value, $"{path}.{entry.Key}");
            return;
        }
        if (value is IReadOnlyList<object> list)
        {
            for (int index = 0; index < list.Count; index++)
                AssertPlainGraph(list[index], $"{path}[{index}]");
            return;
        }
        if (value is IDictionary || value is IList)
            _test.Fail($"{path} should not expose a mutable collection type {value.GetType().Name}");
    }
}
