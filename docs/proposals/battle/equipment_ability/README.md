# Equipment ability design split

本文档夹保存 `equipment_ability_system.md` 中需要独立审查的高影响子设计。总文档继续作为 V1/V2/V3 范围、内容 ABI 和 by-family 覆盖矩阵的总入口；这里的子文档负责把单条运行时迁移链写到代码 owner 级别。

## 子文档

- [battle_skill_availability_migration.md](../../../archive/implementation-plans/equipment_ability/battle_skill_availability_migration.md)：已经落地的 `BattleSkillAvailabilityService` / `SkillEntryId` 迁移计划归档。
- [equipment_durability_selector_commit.md](../../../archive/implementation-plans/equipment_ability/equipment_durability_selector_commit.md)：已经落地的 selector / selected-target commit 计划归档。
- [equipment_ability_subagent_findings.md](../../../reviews/equipment_ability_subagent_findings.md)：当时的子代理检视意见。

## 维护规则

- 子文档可以引用当前代码文件和 owner，但不要复制整段源码。
- 子文档里的迁移顺序必须能映射到当前 `scripts/` 文件，不写泛泛的“更新相关模块”。
- 总文档只保留架构摘要和范围边界；详细 API、字段和测试矩阵放在对应子文档。
