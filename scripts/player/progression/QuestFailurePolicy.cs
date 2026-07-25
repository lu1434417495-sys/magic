using Godot;

internal enum QuestFailurePolicyKind
{
    Unknown = 0,
    Terminal,
    Restartable,
}

internal static class QuestFailurePolicyRules
{
    internal static readonly StringName TerminalId = "terminal";
    internal static readonly StringName RestartableId = "restartable";

    internal static StringName ToStringName(QuestFailurePolicyKind kind)
    {
        return kind switch
        {
            QuestFailurePolicyKind.Terminal => TerminalId,
            QuestFailurePolicyKind.Restartable => RestartableId,
            _ => "",
        };
    }

    internal static QuestFailurePolicyKind ToKind(StringName value)
    {
        if (value == TerminalId)
            return QuestFailurePolicyKind.Terminal;
        if (value == RestartableId)
            return QuestFailurePolicyKind.Restartable;
        return QuestFailurePolicyKind.Unknown;
    }
}
