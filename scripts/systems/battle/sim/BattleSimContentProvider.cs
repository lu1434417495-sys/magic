using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class BattleSimContentProvider : IDisposable
{
    private ContentSnapshot _snapshot;
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions;
    private IReadOnlyDictionary<StringName, ItemDefinition> _itemDefinitions;
    private IReadOnlyDictionary<StringName, TraitDefinition> _traitDefinitions;
    private IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        _equipmentAbilityBindings;

    internal BattleSimContentProvider(ContentSnapshot snapshot)
        : this(snapshot, snapshot?.Skills)
    {
    }

    internal BattleSimContentProvider(
        ContentSnapshot snapshot,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
            equipmentAbilityBindings = null
    )
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        ArgumentNullException.ThrowIfNull(skillDefinitions);
        _skillDefinitions = new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>(skillDefinitions)
        );
        _itemDefinitions = Freeze(
            itemDefinitions ?? snapshot.Items,
            nameof(itemDefinitions)
        );
        _traitDefinitions = Freeze(
            traitDefinitions ?? snapshot.Traits,
            nameof(traitDefinitions)
        );
        _equipmentAbilityBindings = Freeze(
            equipmentAbilityBindings ?? snapshot.EquipmentAbilityBindings,
            nameof(equipmentAbilityBindings)
        );
    }

    public void Dispose()
    {
        _snapshot = null;
        _skillDefinitions = null;
        _itemDefinitions = null;
        _traitDefinitions = null;
        _equipmentAbilityBindings = null;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        _ = RequireSnapshot();
        return _skillDefinitions
            ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));
    }

    internal IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitionsTyped()
    {
        _ = RequireSnapshot();
        return _itemDefinitions
            ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));
    }

    internal IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefinitionsTyped()
    {
        _ = RequireSnapshot();
        return _traitDefinitions
            ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));
    }

    internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        GetEquipmentAbilityBindingsTyped()
    {
        _ = RequireSnapshot();
        return _equipmentAbilityBindings
            ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));
    }

    internal IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetBarrierProfileDefinitionsTyped()
    {
        return RequireSnapshot().BarrierProfiles;
    }

    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition> GetEnemyTemplatesTyped()
    {
        return RequireSnapshot().EnemyTemplates;
    }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> GetEnemyAiBrainsTyped()
    {
        return RequireSnapshot().EnemyBrains;
    }

    internal IReadOnlyDictionary<StringName, BattleSimProfileDefinition> GetBattleSimProfilesTyped()
    {
        return RequireSnapshot().BattleSimProfiles;
    }

    private ContentSnapshot RequireSnapshot() =>
        _snapshot ?? throw new ObjectDisposedException(nameof(BattleSimContentProvider));

    private static IReadOnlyDictionary<StringName, T> Freeze<T>(
        IReadOnlyDictionary<StringName, T> source,
        string parameterName
    )
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        return new ReadOnlyDictionary<StringName, T>(
            new Dictionary<StringName, T>(source)
        );
    }
}
