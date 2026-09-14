internal static class SaveSchemaVersions
{
    // 21: 晋升历史明确记录唯一成长技能；移除活跃触发、重复锁定和待选缓存。
    //     旧存档有意拒绝，不提供兼容读取或迁移。
    internal const int SaveVersion = 21;
    internal const int SaveIndexVersion = 5;
    internal const int MaxActiveMemberCount = 4;
}
