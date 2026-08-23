# Content JSON 作者与生成边界

> 状态：`Current / Implemented`
> 核对日期：`2026-08-23`

## 定位

全部 gameplay/config content 的 production authoring truth 位于
`data/configs/json/<domain>/*.json`。这些域共享同一条 plain C# 导入边界：

```text
JSON document
  -> file-local template merge
  -> strict source-generated DTO parse
  -> immutable plain ImportModel
  -> domain-local validator
  -> Definition projector
  -> ContentSnapshotBuilder cross-domain validation
  -> immutable ContentSnapshot
```

JSON 不构造 Godot `Resource`，ImportModel 和 Definition 不保存文件路径、Godot
collection、`JsonElement` 或 raw DTO。production 没有 content `.tres` discovery、Resource
adapter、Resource-to-Definition fallback 或 authored-path reverse lookup。`data/configs/` 下唯一
保留的 `.tres` 是 `engine_assets/engine_asset_catalog.tres`；它和四类 typed entry 是唯一
Godot Resource authoring 边界。

## 当前域与所有权

| Domain group | Production owners |
|---|---|
| progression/equipment | skills、items、traits、equipment abilities、gear sets、recipes |
| enemy/battle | AI brains、enemy templates、encounter rosters、battle encounters、barriers/layers、special-profile manifest/profile |
| quest/identity | quests、contingency templates、professions、races、subraces、faith、age profiles、bloodlines、ascensions、stage advancements |
| tools/world | BattleSim profiles/scenarios、world presets/generations/shared |

`ContentJsonSchemaCatalog.All` 是当前 domain 清单的代码权威；当前规模由各 registry 的 focused
回归锁定，不在设计文档复制易漂移计数。

物品 JSON 是旧 item-template 链的最终展开结果；production 不再执行模板继承或
`MergeWithTemplate`。装备能力的 condition/action payload 是 plain import model；kind
词表继续由现有 handler spec 定义，schema 不增加第二套 handler ID。

## 构建顺序与跨域引用

`ContentSnapshotBuilder` 显式保持依赖顺序，不把跨域构建塞进单域 descriptor：

```text
progression definitions (skills + traits + equipment abilities)
  -> items
  -> gear sets (item + trait + equipment binding IDs)
  -> recipes (item IDs)
  -> remaining snapshot domains
```

单域 validator 只验证本域 shape、closed vocabulary 和局部不变量。item icon、item
trait/skill、equipment binding、gear-set member/trait、recipe input/output 等引用在组合
图上验证。`RecipeContentRegistry.Setup(itemDefinitions)` 明确表达 recipe 对 item
definition 的依赖。生成闭包的 cross-domain 层还复用正式
`ItemTraitContentValidator`、`SkillBookItemContentValidator` 与 gear-set definition
validator，避免只验证“ID 存在”却漏掉 trait source、skill learn-source、阈值顺序或
per-world-day usage-anchor 等 production business contract。

## 资产 ID

内容只传播稳定 engine-asset ID。item 的 `icon_asset_id`、skill 的 `icon_id` 与 enemy sprite
ID 在
snapshot publication 前必须由 `EngineAssetResolver` 解析为已发布 `Texture2D`；unknown
或 wrong-type ID 会使 publication 回滚。catalog 只提供 `asset_id -> typed Resource` 正向查询；
不存在 authored path 到 ID 的 migration seam。UI/HUD 不拼接或直接加载 authored 路径。

## Schema 与离线校验

`ContentJsonSchemaCatalog` 从 DTO metadata、nullability policy 和 closed-kind spec 导出
受版本控制的 schema。`tools/content_json_validation` 通过 linked compile 复用同一套
纯 CLR document、parser、ImportModel 和 domain validator；它不引用 Godot、不构建
snapshot，也不执行跨域或 BattleSim 校验。

workspace 只关联 JSON 与 tracked schema。schema regression 对重新导出结果做
byte-exact 比较；离线 CLI 对每个域输出带 `source_label`、JSON pointer 和稳定 rule ID
的 JSON/NDJSON 诊断。

## 装备生成闭包

`EquipmentClosureGenerationCatalog` 是生成方的只读输入合同，只导出：

- 五个装备闭包 domain schema；
- 已发布的 skill、profession、item、trait、equipment pack/binding、gear-set、recipe 与 texture asset
  ID 列表。

它不导出路径或 `.tres`。候选闭包依次经过：

1. schema / strict DTO；
2. domain-local validator；
3. 与当前 snapshot 合并后的跨域 ID 和业务引用；
4. `EquipmentClosureGenerationBattleSimGate` 的真实战斗抽检。

前三层通过 `EquipmentClosureGenerationValidationProtocol` 输出机器可读 stage、稳定 rule
ID、`file.json#entry_id`、JSON pointer、expected 和 actual；每层有独立非零退出码。
BattleSim adapter 位于 `scripts/systems/battle/sim/generation/`，只消费组合后的 immutable
definitions，不修改或重新发布 process snapshot，也不进入 routine regression。

## 验证入口

- schema：`tests/runtime/validation/run_content_json_schema_export_regression.cs`
- item/trait/equipment/gear/recipe focused JSON runners：`tests/runtime/validation/` 与
  `tests/progression/schema/`
- 生成 catalog 和前三门：`run_equipment_closure_generation_*_regression.cs`
- BattleSim 门：`tests/battle_runtime/simulation/run_equipment_closure_generation_battle_sim_gate_regression.cs`
- Windows package：`tests/export/run_windows_export_smoke.py`，真实导出包内构建完整 snapshot、
  复读 production JSON domains 与 typed engine-asset catalog
