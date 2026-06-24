using System;
using Godot;

internal sealed class RuntimeSkillDefFactory
{
    private readonly GodotTransientResourceScope _scope;
    private readonly string _ownerName;

    internal RuntimeSkillDefFactory(GodotTransientResourceScope scope, string ownerName)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _ownerName = string.IsNullOrEmpty(ownerName) ? "RuntimeSkillDefFactory" : ownerName;
    }

    internal SkillDef NewSkill(Action<SkillDef> configure, string reason)
    {
        var skill = new SkillDef();
        configure?.Invoke(skill);
        return _scope.Own(skill, Label(reason));
    }

    internal CombatSkillDef NewCombatProfile(Action<CombatSkillDef> configure, string reason)
    {
        var profile = new CombatSkillDef();
        configure?.Invoke(profile);
        return _scope.Own(profile, Label(reason));
    }

    internal CombatEffectDef NewEffect(Action<CombatEffectDef> configure, string reason)
    {
        var effect = new CombatEffectDef();
        configure?.Invoke(effect);
        return _scope.Own(effect, Label(reason));
    }

    internal CombatCastVariantDef NewCastVariant(Action<CombatCastVariantDef> configure, string reason)
    {
        var variant = new CombatCastVariantDef();
        configure?.Invoke(variant);
        return _scope.Own(variant, Label(reason));
    }

    internal CombatEffectDef DuplicateEffect(CombatEffectDef source, string reason)
    {
        return source?.DuplicateForRuntime(_scope, Label(reason));
    }

    private string Label(string reason) =>
        string.IsNullOrEmpty(reason) ? _ownerName : $"{_ownerName}:{reason}";
}

internal sealed class RuntimeEnemyAiResourceFactory
{
    private readonly GodotTransientResourceScope _scope;
    private readonly string _ownerName;

    internal RuntimeEnemyAiResourceFactory(GodotTransientResourceScope scope, string ownerName)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _ownerName = string.IsNullOrEmpty(ownerName) ? "RuntimeEnemyAiResourceFactory" : ownerName;
    }

    internal EnemyAiBrainDef NewBrain(Action<EnemyAiBrainDef> configure, string reason)
    {
        var brain = new EnemyAiBrainDef();
        configure?.Invoke(brain);
        return _scope.Own(brain, Label(reason));
    }

    internal EnemyAiStateDef NewState(Action<EnemyAiStateDef> configure, string reason)
    {
        var state = new EnemyAiStateDef();
        configure?.Invoke(state);
        return _scope.Own(state, Label(reason));
    }

    internal TAction NewAction<TAction>(Action<TAction> configure, string reason)
        where TAction : EnemyAiAction, new()
    {
        var action = new TAction();
        configure?.Invoke(action);
        return _scope.Own(action, Label(reason));
    }

    internal EnemyAiAction OwnAction(EnemyAiAction action, string reason)
    {
        return action != null ? _scope.Own(action, Label(reason)) : null;
    }

    internal EnemyAiAction DuplicateAction(EnemyAiAction source, string reason)
    {
        if (source is Resource resource && resource.Duplicate(true) is EnemyAiAction clone)
            return _scope.Own(clone, Label(reason));
        return source;
    }

    private string Label(string reason) =>
        string.IsNullOrEmpty(reason) ? _ownerName : $"{_ownerName}:{reason}";
}
