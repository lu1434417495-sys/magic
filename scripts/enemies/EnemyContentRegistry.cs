#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal sealed record EnemyContentDefinitionGraph(
    IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates,
    IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyBrains,
    IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> EncounterRosters
);

internal sealed class EnemyContentValidationContext
{
    internal EnemyContentValidationContext(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName basicAttackSkillId
    )
    {
        ItemDefinitions = itemDefinitions ?? throw new ArgumentNullException(nameof(itemDefinitions));
        SkillDefinitions = skillDefinitions ?? throw new ArgumentNullException(nameof(skillDefinitions));
        BasicAttackSkillId = basicAttackSkillId ?? "";
    }
    internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefinitions { get; }
    internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions { get; }
    internal StringName BasicAttackSkillId { get; }
}

public sealed class EnemyContentRegistry : IValidatableRegistry, IDisposable
{
    private readonly IContentJsonSourceReader _sourceReader;
    private readonly Dictionary<StringName, EnemyTemplateDefinition> _templates = new();
    private readonly Dictionary<StringName, EnemyAiBrainDefinition> _brains = new();
    private readonly Dictionary<StringName, WildEncounterRosterDefinition> _rosters = new();
    private readonly List<string> _validationErrors = new();
    private bool _disposed;

    internal EnemyContentRegistry(bool loadDefaultContent = true)
        : this(new GodotContentJsonSourceReader(), loadDefaultContent) { }

    internal EnemyContentRegistry(
        IContentJsonSourceReader sourceReader,
        bool loadDefaultContent = true
    )
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
        if (loadDefaultContent) Rebuild();
    }

    public void Rebuild() => RebuildCore(null);
    internal void Rebuild(EnemyContentValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);
        RebuildCore(validationContext);
    }

    private void RebuildCore(EnemyContentValidationContext? validationContext)
    {
        ThrowIfDisposed();
        _templates.Clear(); _brains.Clear(); _rosters.Clear(); _validationErrors.Clear();
        ContentImportBatch<EnemyAiBrainImportModel> brainBatch = EnemyContentJsonAuthoringDomains.CreateBrainDescriptor(EnemyContentJsonDomains.BrainDirectory, _sourceReader).Import();
        ContentImportBatch<EnemyTemplateJsonDto> templateBatch = EnemyContentJsonAuthoringDomains.CreateTemplateDescriptor(EnemyContentJsonDomains.TemplateDirectory, _sourceReader).Import();
        ContentImportBatch<EncounterRosterJsonDto> rosterBatch = EnemyContentJsonAuthoringDomains.CreateRosterDescriptor(EnemyContentJsonDomains.RosterDirectory, _sourceReader).Import();
        AppendDiagnostics(brainBatch.Diagnostics); AppendDiagnostics(templateBatch.Diagnostics); AppendDiagnostics(rosterBatch.Diagnostics);

        foreach (ContentImportEntry<EnemyAiBrainImportModel> entry in brainBatch.Entries)
        {
            try
            {
                EnemyAiBrainDefinition definition = EnemyContentDefinitionProjector.ProjectBrain(entry.Import);
                if (!_brains.TryAdd(definition.BrainId, definition)) _validationErrors.Add($"Duplicate enemy brain_id registered: {definition.BrainId}.");
            }
            catch (Exception exception) { _validationErrors.Add($"Enemy brain projection failed at {entry.Context.SourceLabel}: {exception.Message}"); }
        }
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = validationContext?.ItemDefinitions ?? EmptyItems;
        foreach (ContentImportEntry<EnemyTemplateJsonDto> entry in templateBatch.Entries)
        {
            try
            {
                EnemyTemplateDefinition definition = EnemyContentDefinitionProjector.ProjectTemplate(entry.Import, itemDefinitions);
                if (!_templates.TryAdd(definition.TemplateId, definition)) _validationErrors.Add($"Duplicate enemy template_id registered: {definition.TemplateId}.");
            }
            catch (Exception exception) { _validationErrors.Add($"Enemy template projection failed at {entry.Context.SourceLabel}: {exception.Message}"); }
        }
        foreach (ContentImportEntry<EncounterRosterJsonDto> entry in rosterBatch.Entries)
        {
            try
            {
                WildEncounterRosterDefinition definition = EnemyContentDefinitionProjector.ProjectRoster(entry.Import);
                if (!_rosters.TryAdd(definition.ProfileId, definition)) _validationErrors.Add($"Duplicate encounter roster profile_id registered: {definition.ProfileId}.");
            }
            catch (Exception exception) { _validationErrors.Add($"Encounter roster projection failed at {entry.Context.SourceLabel}: {exception.Message}"); }
        }
        ValidateDefinitionGraph(validationContext);
    }

    public Godot.Collections.Array<string> Validate() => new(_validationErrors);
    public IReadOnlyList<string> ValidateTyped() => _validationErrors;
    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition> GetEnemyTemplatesTyped() => _templates;
    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> GetEnemyAiBrainsTyped() => _brains;
    internal IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> GetWildEncounterRostersTyped() => _rosters;

    internal EnemyContentDefinitionGraph ProjectDefinitions(IReadOnlyDictionary<StringName, ItemDefinition> _)
    {
        if (_validationErrors.Count != 0) throw new System.IO.InvalidDataException("Enemy JSON content must validate before immutable graph publication: " + string.Join(" | ", _validationErrors));
        return new EnemyContentDefinitionGraph(
            new ReadOnlyDictionary<StringName, EnemyTemplateDefinition>(new Dictionary<StringName, EnemyTemplateDefinition>(_templates)),
            new ReadOnlyDictionary<StringName, EnemyAiBrainDefinition>(new Dictionary<StringName, EnemyAiBrainDefinition>(_brains)),
            new ReadOnlyDictionary<StringName, WildEncounterRosterDefinition>(new Dictionary<StringName, WildEncounterRosterDefinition>(_rosters))
        );
    }

    private void ValidateDefinitionGraph(EnemyContentValidationContext? context)
    {
        foreach ((StringName brainId, EnemyAiBrainDefinition brain) in _brains)
        {
            if (!brain.HasState(brain.DefaultStateId))
            {
                _validationErrors.Add(
                    $"Enemy brain {brainId} default_state_id {brain.DefaultStateId} is not declared."
                );
            }
            foreach (EnemyAiTransitionRuleDefinition rule in brain.TransitionRules)
            {
                if (!brain.HasState(rule.TargetStateId))
                {
                    _validationErrors.Add(
                        $"Enemy brain {brainId} transition {rule.RuleId} targets missing state {rule.TargetStateId}."
                    );
                }
                foreach (StringName fromStateId in rule.FromStateIds)
                {
                    if (!brain.HasState(fromStateId))
                    {
                        _validationErrors.Add(
                            $"Enemy brain {brainId} transition {rule.RuleId} references missing from state {fromStateId}."
                        );
                    }
                }
            }
            if (context is not null)
                ValidateBrainSkillCompatibility(brain, context.SkillDefinitions);
        }

        foreach ((StringName templateId, EnemyTemplateDefinition template) in _templates)
        {
            EnemyAiBrainDefinition? brain = null;
            if (!_brains.TryGetValue(template.BrainId, out brain))
            {
                _validationErrors.Add(
                    $"Enemy template {templateId} references missing brain {template.BrainId}."
                );
            }
            else if (template.InitialStateId != "" && !brain.HasState(template.InitialStateId))
            {
                _validationErrors.Add(
                    $"Enemy template {templateId} initial_state_id {template.InitialStateId} is not declared by brain {template.BrainId}."
                );
            }
            if (context is not null)
                ValidateTemplateReferences(template, brain, context);
        }

        foreach ((StringName rosterId, WildEncounterRosterDefinition roster) in _rosters)
        {
            bool initialFound = false;
            foreach (WildEncounterRosterStageDefinition stage in roster.Stages)
            {
                if (stage.Stage == roster.InitialStage)
                    initialFound = true;
                var actorIds = new HashSet<StringName>();
                foreach (WildEncounterRosterUnitEntryDefinition unit in stage.UnitEntries)
                {
                    if (!_templates.ContainsKey(unit.TemplateId))
                    {
                        _validationErrors.Add(
                            $"Encounter roster {rosterId} stage {stage.Stage} references missing template {unit.TemplateId}."
                        );
                    }
                    if (unit.Count <= 0)
                    {
                        _validationErrors.Add(
                            $"Encounter roster {rosterId} stage {stage.Stage} template {unit.TemplateId} must have count >= 1."
                        );
                    }
                    if (
                        unit.ActorId != ""
                        && (unit.Count != 1 || !actorIds.Add(unit.ActorId))
                    )
                    {
                        _validationErrors.Add(
                            $"Encounter roster {rosterId} stage {stage.Stage} actor_id {unit.ActorId} must be unique and have count == 1."
                        );
                    }
                }
            }
            if (!initialFound)
            {
                _validationErrors.Add(
                    $"Encounter roster {rosterId} initial_stage {roster.InitialStage} is not declared."
                );
            }
        }
    }

    private void ValidateBrainSkillCompatibility(
        EnemyAiBrainDefinition brain,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (EnemyAiStateDefinition state in brain.StateOrder)
        {
            foreach (EnemyAiActionDefinition action in state.Actions)
            {
                foreach (StringName skillId in action.DeclaredSkillIds)
                {
                    if (
                        !skillDefinitions.TryGetValue(skillId, out SkillDefinition? skill)
                        || skill is null
                    )
                    {
                        _validationErrors.Add(
                            $"Enemy brain {brain.BrainId} state {state.StateId} action {action.ActionId} references missing skill {skillId}."
                        );
                        continue;
                    }
                    EnemyAiActionSkillCompatibilityResult compatibility =
                        EnemyAiActionSkillCompatibilityRules.Evaluate(
                            action.Kind,
                            skill,
                            ResolveCandidatePoolLimit(action)
                        );
                    if (!compatibility.IsCompatible)
                    {
                        _validationErrors.Add(
                            $"Enemy brain {brain.BrainId} state {state.StateId} action {action.ActionId} references incompatible skill {skillId}: {compatibility.Reason}."
                        );
                    }
                }
            }
        }
    }

    private void ValidateTemplateReferences(
        EnemyTemplateDefinition template,
        EnemyAiBrainDefinition? brain,
        EnemyContentValidationContext context
    )
    {
        var declaredSkillIds = new HashSet<StringName>(template.SkillIds);
        int eligibleGeneratedSkillCount = 0;
        foreach (StringName skillId in template.SkillIds)
        {
            if (
                !context.SkillDefinitions.TryGetValue(skillId, out SkillDefinition? skill)
                || skill is null
            )
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} references missing skill {skillId}."
                );
                continue;
            }
            int skillLevel = template.GetSkillLevel(skillId);
            if (skillLevel < 1 || (skill.MaxLevel > 0 && skillLevel > skill.MaxLevel))
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} skill {skillId} level {skillLevel} is outside 1..{skill.MaxLevel}."
                );
            }
            if (
                (context.BasicAttackSkillId == "" || skillId != context.BasicAttackSkillId)
                && skill.MaxLevel > 0
            )
                eligibleGeneratedSkillCount += 1;
        }
        foreach ((StringName skillId, int _) in template.SkillLevels)
        {
            if (!declaredSkillIds.Contains(skillId))
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} skill_level_map key {skillId} is not declared in skill_ids."
                );
            }
        }
        if (template.GeneratedCoreSkillCount > eligibleGeneratedSkillCount)
        {
            _validationErrors.Add(
                $"Enemy template {template.TemplateId} generated_core_skill_count {template.GeneratedCoreSkillCount} exceeds eligible skill count {eligibleGeneratedSkillCount}."
            );
        }

        if (brain is not null)
        {
            foreach (EnemyAiStateDefinition state in brain.StateOrder)
            {
                foreach (EnemyAiActionDefinition action in state.Actions)
                {
                    foreach (StringName skillId in action.DeclaredSkillIds)
                    {
                        if (
                            !declaredSkillIds.Contains(skillId)
                            || !context.SkillDefinitions.TryGetValue(
                                skillId,
                                out SkillDefinition? skill
                            )
                            || skill is null
                        )
                        {
                            continue;
                        }
                        int skillLevel = template.GetSkillLevel(skillId);
                        EnemyAiActionSkillCompatibilityResult compatibility =
                            EnemyAiActionSkillCompatibilityRules.Evaluate(
                                action.Kind,
                                skill,
                                ResolveCandidatePoolLimit(action),
                                skillLevel
                            );
                        if (!compatibility.IsCompatible)
                        {
                            _validationErrors.Add(
                                $"Enemy template {template.TemplateId} brain {brain.BrainId} state {state.StateId} action {action.ActionId} references incompatible skill {skillId} at level {skillLevel}: {compatibility.Reason}."
                            );
                        }
                    }
                }
            }
        }

        foreach (DropEntryDefinition drop in template.DropEntries)
        {
            if (!context.ItemDefinitions.ContainsKey(drop.ItemId))
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} drop {drop.DropEntryId} references missing item {drop.ItemId}."
                );
            }
        }
        ValidateBattleEquipment(template, context.ItemDefinitions);

        bool isBeast = template.Tags.Contains(new StringName("beast"));
        if (!isBeast)
        {
            if (
                !context.ItemDefinitions.TryGetValue(
                    template.AttackEquipmentItemId,
                    out ItemDefinition? attackEquipment
                )
                || attackEquipment is null
            )
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} references missing attack equipment {template.AttackEquipmentItemId}."
                );
            }
            else if (
                !attackEquipment.IsWeapon()
                || attackEquipment.GetWeaponAttackRange() < 1
                || attackEquipment.GetWeaponPhysicalDamageTag() == ""
            )
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} attack equipment {template.AttackEquipmentItemId} must be a complete weapon definition."
                );
            }
        }
    }

    private void ValidateBattleEquipment(
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        var occupiedSlots = new HashSet<StringName>();
        foreach (EnemyBattleEquipmentDefinition equipment in template.BattleEquipmentEntries)
        {
            if (
                !itemDefinitions.TryGetValue(equipment.ItemId, out ItemDefinition? item)
                || item is null
                || !item.IsEquipment()
            )
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} equipment slot {equipment.SlotId} references non-equipment item {equipment.ItemId}."
                );
                continue;
            }
            if (!item.GetEquipmentSlotIdsTyped().Contains(equipment.SlotId))
            {
                _validationErrors.Add(
                    $"Enemy template {template.TemplateId} item {equipment.ItemId} cannot be equipped in {equipment.SlotId}."
                );
            }
            foreach (StringName occupiedSlot in item.GetFinalOccupiedSlotIdsTyped(equipment.SlotId))
            {
                if (!occupiedSlots.Add(occupiedSlot))
                {
                    _validationErrors.Add(
                        $"Enemy template {template.TemplateId} battle equipment overlaps occupied slot {occupiedSlot}."
                    );
                }
            }
        }
    }

    private static int ResolveCandidatePoolLimit(EnemyAiActionDefinition action) =>
        action switch
        {
            UseMultiUnitSkillActionDefinition value => value.CandidatePoolLimit,
            MoveToMultiUnitSkillPositionActionDefinition value => value.CandidatePoolLimit,
            _ => int.MaxValue,
        };

    private void AppendDiagnostics(IReadOnlyList<ContentJsonDiagnostic> diagnostics)
    {
        foreach (ContentJsonDiagnostic diagnostic in diagnostics) _validationErrors.Add($"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}");
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _templates.Clear(); _brains.Clear(); _rosters.Clear(); _validationErrors.Clear(); GC.SuppressFinalize(this);
    }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(EnemyContentRegistry)); }
    private static IReadOnlyDictionary<StringName, ItemDefinition> EmptyItems { get; } = new ReadOnlyDictionary<StringName, ItemDefinition>(new Dictionary<StringName, ItemDefinition>());
}
