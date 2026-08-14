/// 单位倒下结算的选项值对象。
///
/// 原先声明在 BattleRuntimeModule.cs 内，因而被 layer_rules 归为 composition；
/// 而它只是一组纯数据开关，被 runtime 与已解耦的 timeline driver 共同使用。
/// 2026-08-14 随 BattleTimelineDriver 解耦一并下移到 core（domain_state），
/// 与它的字段类型 BattleKillProvenance 同层。
internal readonly struct BattleDefeatHandlingOptions
{
    internal readonly bool CollectLoot;
    internal readonly bool RecordEnemyDefeatedAchievement;
    internal readonly BattleKillProvenance KillProvenance;

    internal BattleDefeatHandlingOptions(
        bool collectLoot = true,
        bool recordEnemyDefeatedAchievement = false,
        BattleKillProvenance killProvenance = default
    )
    {
        CollectLoot = collectLoot;
        RecordEnemyDefeatedAchievement = recordEnemyDefeatedAchievement;
        KillProvenance = killProvenance;
    }
}
