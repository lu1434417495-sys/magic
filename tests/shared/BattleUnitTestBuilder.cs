using Godot;

public sealed class BattleUnitTestBuilder
{
    private readonly BattleUnitState _unit;

    private BattleUnitTestBuilder(StringName unitId)
    {
        _unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
        };
        _unit.SyncDefaultCombatResourceUnlocks();
    }

    public static BattleUnitTestBuilder Create(StringName unitId) => new(unitId);

    public BattleUnitTestBuilder Named(string displayName)
    {
        _unit.display_name = displayName;
        return this;
    }

    public BattleUnitTestBuilder InFaction(StringName factionId)
    {
        _unit.faction_id = factionId;
        return this;
    }

    public BattleUnitTestBuilder At(Vector2I coord)
    {
        _unit.SetAnchorCoord(coord);
        return this;
    }

    public BattleUnitTestBuilder WithBodySize(StringName category)
    {
        _unit.SetBodySizeCategory(category);
        return this;
    }

    public BattleUnitTestBuilder WithAttribute(StringName attributeId, int value)
    {
        _unit.attribute_snapshot ??= new AttributeSnapshot();
        _unit.attribute_snapshot.SetValue(attributeId, value);
        return this;
    }

    public BattleUnitTestBuilder WithVitals(int hp, int hpMax = 0)
    {
        if (hpMax > 0)
        {
            WithAttribute(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
            _unit.SetCurrentHpClamped(hp, hpMax);
        }
        else
        {
            _unit.SetCurrentHp(hp);
        }
        return this;
    }

    public BattleUnitTestBuilder WithResources(
        int? mp = null,
        int? stamina = null,
        int? aura = null,
        int? ap = null,
        int? movePoints = null
    )
    {
        if (mp.HasValue)
            _unit.SetCurrentMp(mp.Value);
        if (stamina.HasValue)
            _unit.SetCurrentStamina(stamina.Value);
        if (aura.HasValue)
            _unit.SetCurrentAura(aura.Value);
        if (ap.HasValue)
            _unit.SetCurrentAp(ap.Value);
        if (movePoints.HasValue)
            _unit.SetCurrentMovePoints(movePoints.Value);
        return this;
    }

    public BattleUnitTestBuilder WithSkill(StringName skillId, int level = 1)
    {
        _unit.AddKnownActiveSkill(skillId);
        _unit.SetKnownSkillLevelTyped(skillId, level, preserveZero: true);
        return this;
    }

    public BattleUnitState Build() => _unit;
}
