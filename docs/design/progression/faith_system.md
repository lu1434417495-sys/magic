# 信仰系统当前实现

> 状态：`Current / Implemented`
> 核对日期：`2026-08-19`

## 定位

本文记录当前信仰内容、升阶校验和角色奖励入队链。尚未接入的据点服务或新神系方案位于 [`../../proposals/progression/faith_system_expansion.md`](../../proposals/progression/faith_system_expansion.md) 和 `docs/content/faith/`，不属于当前运行时合同。

## 当前所有权

| 层 | 当前 owner | 职责 |
|---|---|---|
| Authoring | `data/configs/json/faith/content.json`、`FaithDeityJsonDto`、`FaithRankJsonDto` | 以 strict JSON 声明 deity、rank progress stat、门槛、花费和奖励条目 |
| 校验与投影 | `ProfessionIdentityJsonImport`、`ProfessionIdentityDefinitionProjector`、`FaithContentRegistry` | fail-closed 导入并唯一投影 `FaithDeityDefinition` / `FaithRankDefinition`，校验 rank 连续性、奖励和引用 |
| 运行时服务 | `FaithService` | 读取 typed deity index，校验下一阶、金币、等级、属性和成就条件 |
| 持久状态 | `PartyState`、`PartyMemberState.progression.unit_base_attributes` | 保存金币、已应用的 rank progress custom stat 和 pending reward 队列 |
| 奖励提交 | `PendingCharacterReward` 与 CharacterManagement 奖励归并链 | 把升阶收益作为可确认奖励应用到角色永久状态 |

## 升阶链

```text
faith strict JSON
  -> plain FaithImportModel
  -> ProfessionIdentityDefinitionProjector
  -> FaithContentRegistry
  -> FaithDeityDefinition index in ContentSnapshot
  -> FaithService.ExecuteDevotion(...)
  -> validate next rank and spend gold
  -> enqueue PendingCharacterReward(source_type = faith_rank_reward)
  -> reward claim / CharacterManagement applies permanent changes
```

## 实现约束

- `FaithService` 通过构造函数接收 `FaithDeityDefinition` 索引，不保留或重新加载 JSON DTO/import model。
- 一次 devotion 只推进下一阶。当前 rank 等于已应用的 rank progress stat，加上同成员、同 deity 的 pending rank reward；这样未领取奖励不能重复排队同一阶。
- 金币在成功生成非空奖励前后保持事务一致；奖励构建失败时退还已经扣除的金币。
- Rank 奖励必须使用 `PendingCharacterRewardContentRules` 支持的 typed entry，不允许在 `FaithService` 中直接改角色永久属性。
- `rank_progress_stat_id` 是 strict JSON 必填字段；运行时不提供旧 payload alias 或静默 fallback。

## 代表性回归

- `tests/progression/fate/run_faith_service_regression.cs`
- `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`

架构装载范围见 [`../project_context_units.md`](../project_context_units.md) 的 CU-11、CU-12、CU-13 和 CU-14。
