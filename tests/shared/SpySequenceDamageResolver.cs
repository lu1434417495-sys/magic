using Godot;
using System;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class SpySequenceDamageResolver : BattleDamageResolver
{
    public int sequence_preview_call_count = 0;
}
