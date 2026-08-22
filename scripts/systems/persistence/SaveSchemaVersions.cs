internal static class SaveSchemaVersions
{
    // 20: root world 与 mounted submap 持久化切换为 world_generation_id；
    //     旧存档有意拒绝，不提供兼容读取或迁移。
    internal const int SaveVersion = 20;
    internal const int SaveIndexVersion = 5;
    internal const int MaxActiveMemberCount = 4;
}
