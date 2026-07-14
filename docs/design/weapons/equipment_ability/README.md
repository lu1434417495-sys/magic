# Equipment ability design split

本文档夹保存 `equipment_ability_system.md` 中需要独立审查的高影响子设计。总文档继续作为 V1/V2/V3 范围、内容 ABI 和 by-family 覆盖矩阵的总入口；这里的子文档负责把单条运行时迁移链写到代码 owner 级别。

## 子文档

- [battle_skill_availability_migration.md](battle_skill_availability_migration.md)：`BattleSkillAvailabilityService`、`SkillEntryId` 一等化、selection/command/HUD/AI/preview/execution 迁移。
- [equipment_durability_selector_commit.md](equipment_durability_selector_commit.md)：装备耐久 `selector -> selected-target commit` 拆分、explicit equipment ref revalidation、旧 resolver 复用。
- [subagent_review_findings.md](subagent_review_findings.md)：子代理返回的检视意见归档；尚未逐条反驳/确认。

## 维护规则

- 子文档可以引用当前代码文件和 owner，但不要复制整段源码。
- 子文档里的迁移顺序必须能映射到当前 `scripts/` 文件，不写泛泛的“更新相关模块”。
- 总文档只保留架构摘要和范围边界；详细 API、字段和测试矩阵放在对应子文档。
