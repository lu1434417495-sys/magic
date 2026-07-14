using Godot;

internal sealed class WarehouseBatchSwapResult
{
    public readonly bool Allowed;
    public readonly string ErrorCode;
    public readonly StringName BlockedItemId;
    public readonly StringName BlockedInstanceId;

    private WarehouseBatchSwapResult(
        bool allowed,
        string errorCode,
        StringName blockedItemId,
        StringName blockedInstanceId)
    {
        Allowed = allowed;
        ErrorCode = errorCode ?? "";
        BlockedItemId = ProgressionDataUtils.to_string_name(blockedItemId);
        BlockedInstanceId = ProgressionDataUtils.to_string_name(blockedInstanceId);
    }

    public static WarehouseBatchSwapResult Success() => new(true, "", "", "");

    public static WarehouseBatchSwapResult Blocked(
        string errorCode,
        StringName blockedItemId = default,
        StringName blockedInstanceId = default) =>
        new(false, errorCode, blockedItemId, blockedInstanceId);
}
