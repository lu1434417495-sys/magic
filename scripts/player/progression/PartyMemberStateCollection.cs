using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class PartyMemberStateCollection
{
    private readonly Dictionary<StringName, PartyMemberState> _members = new();

    public IReadOnlyDictionary<StringName, PartyMemberState> Members => _members;
    public int Count => _members.Count;

    public Godot.Collections.Array Keys
    {
        get
        {
            var result = new Godot.Collections.Array();
            foreach (StringName memberId in GetSortedIds())
                result.Add(memberId);
            return result;
        }
    }

    public Godot.Collections.Array Values
    {
        get
        {
            var result = new Godot.Collections.Array();
            foreach (StringName memberId in GetSortedIds())
                result.Add(_members[memberId]);
            return result;
        }
    }

    public PartyMemberState Get(StringName id)
    {
        StringName memberId = ProgressionDataUtils.to_string_name(id);
        return memberId != "" && _members.TryGetValue(memberId, out PartyMemberState value)
            ? value
            : null;
    }

    public bool ContainsKey(StringName id) => Get(id) != null;

    public void Set(PartyMemberState member)
    {
        if (member == null || member.member_id == "")
            throw new ArgumentException("member_id is required", nameof(member));
        _members[member.member_id] = member;
    }

    public bool Remove(StringName id)
    {
        StringName memberId = ProgressionDataUtils.to_string_name(id);
        return memberId != "" && _members.Remove(memberId);
    }

    public void Clear() => _members.Clear();

    public void DisposeOwnedMembers()
    {
        foreach (PartyMemberState member in _members.Values)
            GodotRefCountedDisposer.DisposeIfValid(member);
        _members.Clear();
    }

    public List<StringName> GetSortedIds()
    {
        var result = new List<StringName>(_members.Keys);
        result.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        return result;
    }

    public List<string> GetSortedIdStrings()
    {
        var result = new List<string>();
        foreach (StringName memberId in GetSortedIds())
            result.Add(memberId.ToString());
        return result;
    }

    public List<PartyMemberState> GetValuesTyped()
    {
        var result = new List<PartyMemberState>();
        foreach (StringName memberId in GetSortedIds())
            result.Add(_members[memberId]);
        return result;
    }

    public PartyMemberStateCollection DuplicateState()
    {
        var result = new PartyMemberStateCollection();
        foreach (StringName memberId in GetSortedIds())
        {
            PartyMemberState duplicate = _members[memberId]?.DuplicateState();
            if (duplicate != null)
                result.Set(duplicate);
        }
        return result;
    }

    public GDictionary ToDictionary()
    {
        var payload = new GDictionary();
        foreach (StringName memberId in GetSortedIds())
            payload[memberId.ToString()] = _members[memberId];
        return payload;
    }

    public GDictionary ToSaveDictionary()
    {
        var payload = new GDictionary();
        foreach (StringName memberId in GetSortedIds())
        {
            PartyMemberState member = _members[memberId];
            if (member != null)
                payload[memberId.ToString()] = member.ToDictionary();
        }
        return payload;
    }

    public static PartyMemberStateCollection FromDictionary(GDictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("member_states payload is required", nameof(payload));

        var result = new PartyMemberStateCollection();
        foreach (Variant rawKey in payload.Keys)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(rawKey);
            if (memberId == "")
                throw new ArgumentException("member_states contains an empty member id");
            if (result.ContainsKey(memberId))
                throw new ArgumentException($"member_states contains duplicate member id {memberId}");

            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Object)
                throw new ArgumentException($"member_states[{memberId}] is not a PartyMemberState object");

            var member = rawValue.AsGodotObject() as PartyMemberState;
            if (member == null || member.member_id != memberId)
                throw new ArgumentException($"member_states[{memberId}] has mismatched member state");
            result.Set(member);
        }
        return result;
    }

    public static PartyMemberStateCollection FromSaveDictionary(GDictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("member_states payload is required", nameof(payload));

        var result = new PartyMemberStateCollection();
        try
        {
            foreach (Variant rawKey in payload.Keys)
            {
                StringName memberId = ProgressionDataUtils.to_string_name(rawKey);
                if (memberId == "")
                    throw new ArgumentException("member_states contains an empty member id");
                if (result.ContainsKey(memberId))
                    throw new ArgumentException($"member_states contains duplicate member id {memberId}");

                Variant rawValue = payload[rawKey];
                if (rawValue.VariantType != Variant.Type.Dictionary)
                    throw new ArgumentException($"member_states[{memberId}] is not a save dictionary");

                PartyMemberState member = PartyMemberState.FromDictionary(rawValue.AsGodotDictionary());
                if (member == null || member.member_id != memberId)
                    throw new ArgumentException($"member_states[{memberId}] has invalid member payload");
                result.Set(member);
            }
        }
        catch
        {
            result.DisposeOwnedMembers();
            throw;
        }
        return result;
    }
}
