# 内容资源 JSON 直载迁移方案

> **状态**：设计提案（未实施，不含代码或资源改动）
> **版本**：v1.1（2026-08-16）
> **前置决定**：不生成中间 `.tres`；内容 JSON 不引入加载器引用语法，也不直接保存 Godot 路径。

---

## 1. 结论

方案可行，但成立条件不是“把 `.tres` 改写成另一种 Resource 描述语言”，而是把两类数据明确拆开：

1. **内容定义**只表达游戏领域事实：技能、物品、敌人、任务等，以及它们之间的稳定领域 ID；
2. **引擎资产目录**集中维护稳定资产 ID 到 `Texture2D`、`PackedScene`、音频等 Godot 资产的映射。

内容 JSON 中不得出现：

- C# 类名或脚本路径；
- `res://`、`uid://`；
- 内容文件路径；
- 用于指示加载行为的特殊引用对象；
- 可由内容作者选择 CLR/Godot 运行时类型的字段。

因此，本方案明确不采用 `$type`、`$import`、`$ref`、`$asset` 一类语法。它们会让 JSON 重新承担 Resource 图、文件图或类型图的职责，只是换了一种文本格式，并未解除耦合。把引擎路径直接写成普通字符串同样不接受；它只隐藏了加载指令，没有消除路径依赖。

本方案也不再承诺“下游零改动”。格式平移域可以复用现有投影入口；涉及路径身份、引擎资产或存档身份的域，必须显式改造成稳定 ID 链路。

## 2. 当前问题与事实边界

当前静态内容主要通过：

```text
.tres
  -> ProcessContentHost
  -> ContentSnapshotBuilder
  -> *ContentRegistry
  -> *Definition.FromResource
  -> ContentSnapshot
  -> runtime / preview / UI / AI / save
```

加载。当前仓库抽样统计为：

- `data/` 下约 1380 个 `.tres`；
- 约 6784 处 `script = ExtResource(...)`；
- 技能约 701 个、敌人模板约 40 个、物品约 129 个。

这些数字用于说明迁移规模，不是验收常量；实施时应由仓库工具重新统计。

### 2.1 内容 schema 与 C# 文件位置耦合

大量内容文件直接记录 `XxxDef.cs` 路径。类移动、改名或导出属性变化会扩散到大量内容文件，且内容难以脱离 Godot 做独立解析、生成和审查。

### 2.2 模板能力割裂

- 物品已有手写模板合并，但存在逐字段维护和哨兵值问题；
- 技能、敌人缺少统一模板能力，族内重复明显；
- 不同域的数组、空值和默认值语义不统一。

### 2.3 内容、引擎资产与文件身份混在同一 Resource 图

并非所有 `.tres` 都只是纯内容：

- 敌人定义含 `Texture2D` 等引擎资产；
- 部分定义和投影继续传播 `ResourcePath`；
- world 配置路径进入存档并参与恢复匹配；
- barrier、special profile 等存在跨内容对象关系。

这些关系不能靠一个通用 JSON 引用语法替代。它们必须分别归入资产 ID、领域 ID 或存档 ID 的明确契约。

### 2.4 批量生产与审查成本

一文件一实例导致同族内容重复、批量生成 diff 分散。按族聚合可以降低样板噪声，但文件粒度仍要受可审查性和冲突成本约束。

## 3. 目标

1. 内容 JSON 不依赖 C# 类名、Godot 脚本路径、资源路径或文件布局。
2. JSON 只包含稳定的领域数据、领域 ID 和资产 ID。
3. 用一套简单、确定、可独立测试的文件内模板规则减少重复。
4. 迁移期复用现有 validator 和 `*Definition.FromResource`，避免同时重写全部投影。
5. 对格式平移与契约迁移采用不同验收标准，禁止用“快照相同”掩盖身份模型变化。
6. 每个域可以独立迁移、验证和回滚，不保留永久双加载通道。
7. 所有加载期对象都有明确生命周期 owner，失败路径也能释放。

## 4. 非目标与明确禁止

### 4.1 非目标

- 不在本提案中实现热更新、远程内容分发或引用计数卸载；
- 不在第一阶段把所有 `XxxDef : Resource` 改成 POCO；
- 不为了迁移自动加入旧 payload、旧路径或旧存档兼容；
- 不承诺所有域都能通过纯格式转换完成；
- 不把 JSON Schema 生成作为首个试点的阻塞项。

### 4.2 禁止重新发明 Resource 文件语法

JSON 不得提供下列能力：

- 按文件路径导入另一份 JSON；
- 按 JSON 指针共享任意对象；
- 按路径加载 Godot 资源；
- 由内容声明运行时具体类型；
- 跨文件继承模板；
- 构造任意 Resource 对象图。

如果某个关系需要这些能力，应先判断它究竟是：

- 领域实体关系：使用目标域的稳定 ID；
- 引擎资产关系：使用稳定资产 ID；
- 同文件去重：使用文件内模板；
- 运行时多态：由代码侧 schema/handler owner 决定；
- 存档身份：使用稳定存档 ID，并单独设计迁移策略。

## 5. 分层模型

### 5.1 内容 JSON 层

内容 JSON 是可独立解析的领域文档。示例：

```json
{
  "schema": 1,
  "domain": "skills",
  "family": "mage_prismatic_ward",
  "templates": {
    "ward_base": {
      "tags": ["mage", "magic", "ward"],
      "combat_profile": {
        "save_dc_mode": "caster_spell",
        "effect_target_team_filter": "self"
      }
    }
  },
  "entries": [
    {
      "template": "ward_base",
      "skill_id": "mage_prismatic_fire_ward",
      "display_name": "虹彩火焰护壁",
      "battle_icon_asset_id": "skill.prismatic_fire_ward",
      "combat_profile": {
        "effect_defs": [
          {
            "effect_id": "ward_damage",
            "effect_type": "damage",
            "damage_type": "fire"
          }
        ]
      }
    }
  ]
}
```

`skill_id`、`effect_id` 是领域身份；`battle_icon_asset_id` 是资产身份。它们都不是加载器指令，也不包含文件位置。

### 5.2 代码侧 domain descriptor

每个域由代码注册一个不可由内容修改的 descriptor，例如：

```text
domain name
root authoring type
entry id property
allowed directories
binding rules for ambiguous child properties
validator / projector entry point
schema version
```

根类型来自调用方的泛型参数和 descriptor；具体子类型来自目标属性类型。JSON 不能选择类型。

对于声明为 `Resource`、`Array<Resource>` 或其他无法从属性签名唯一判断的字段，必须在代码侧 `JsonBindingRuleCatalog` 以“owner type + property”注册绑定规则。缺少规则时 fail-fast，不允许从 JSON 中读取类名兜底。

业务多态沿用业务 owner。例如 equipment ability 继续由稳定 `handler_id` 和该 handler 的 payload schema 解释；内容只选业务 handler，不选 CLR 类型。

### 5.3 引擎资产目录

引擎资产继续由 Godot 原生资源系统加载，但仅集中在少量资产目录中：

```text
content JSON
  -- stable asset id -->
EngineAssetCatalog
  -- Godot typed reference -->
Texture2D / PackedScene / AudioStream / ...
```

建议按资产类型或消费边界拆分目录，例如：

- content texture catalog；
- battle scene catalog；
- content audio catalog。

每个目录项包含稳定 `asset_id` 和一个类型明确的 Godot 资产属性。原始 `res://` 或 UID 只存在于这些 Godot 原生目录及场景内部，不进入内容 JSON、Definition、snapshot 或存档。

资产目录需要：

- 全局 asset ID 唯一性校验；
- 目标类型校验；
- 缺失项聚合报错；
- 打包后解析 smoke test；
- 明确的缓存与生命周期 owner。

资产 ID 不是路径别名。路径移动只改目录；资产含义改变应新增或有意替换目录项，而不是依赖文件名推断。

### 5.4 Definition、snapshot 与消费侧

纯内容域可以继续：

```text
JSON -> pathless XxxDef graph -> XxxDefinition.FromResource -> snapshot
```

资产字段不能继续传播路径。以敌人战斗贴图为例，目标链路应是：

```text
Enemy JSON.battle_sprite_asset_id
  -> EnemyTemplateDef
  -> EnemyTemplateDefinition
  -> encounter/unit snapshot
  -> UI or battle asset resolver
  -> Texture2D
```

当前 `EnemyTemplateDefinition`、roster/unit snapshot 和 `BattleBoardController` 仍有路径传播与加载行为，因此这是一次跨层契约迁移，不是绑定器内部替换。其他资产字段也必须逐域做同类 preflight，不能宣称自动覆盖。

## 6. JSON 文档与模板契约

### 6.1 文档结构

每个文件包含：

- `schema`：该 domain schema 版本；
- `domain`：必须与加载目录的 descriptor 一致；
- `family`：诊断与内容组织标签，不参与运行时身份；
- `templates`：只在当前文件内可见；
- `entries`：0..N 个完整内容实例。

建议每个文件聚合同一模板链的一族内容，通常不超过 20~40 个实例。禁止单域巨型文件；实际上限由试点 diff 和冲突数据确认。

### 6.2 模板语义

模板只解决同一文件内的重复，不提供引用系统：

| 输入 | 合并规则 |
|---|---|
| 对象 + 对象 | 按键递归合并 |
| 数组 | 子项整体替换父项 |
| 标量 | 子项显式覆盖，`0`、`false`、空字符串均是有效值 |
| 键缺失 | 保留模板值；无模板值时保留 C# 默认值 |
| `null` | 仅允许目标属性本身可空；表示显式赋空，不表示“删除后恢复默认” |
| 多级 `template` | 允许文件内链式继承，必须检测环和未知模板 |

不提供数组追加、删除或按键合并指令。需要局部修改复杂数组时，子项写出完整数组。这样牺牲少量简写，换取确定语义、简单审查和独立实现。

跨文件共享模板不允许。若跨族重复已经大到需要共享，应优先：

1. 判断它是否应成为正式领域定义并以稳定 ID 关联；
2. 在生成工具中复用，而不是把生成期依赖带入运行时格式；
3. 重新划分 family 文件。

### 6.3 未知字段与版本

- 未知字段一律报错，错误包含文件、entry ID 和 JSON pointer；
- 缺少必填字段由 binder/validator 聚合报错；
- schema 版本不受支持时直接拒绝，不隐式猜测；
- 不自动接受旧字段别名；兼容需求必须单独获批。

## 7. 加载与绑定设计

### 7.1 加载接缝

现有“一路径一 Resource”的 `LoadCanonical<T>` 不适合多实例文件。建议新增类型化批次接口：

```csharp
ContentEntryBatch<TDef> LoadDirectory<TDef>(
    JsonContentDomainDescriptor<TDef> domain,
    string directoryPath,
    ContentImportScope importScope)
    where TDef : Resource;
```

`ContentEntryBatch<TDef>` 是急切构建的只读批次；每项至少包含：

- `TDef Definition`；
- `StringName EntryId`；
- `string SourceLabel`，形如 `file.json#entry_id`，只用于诊断。

`SourceLabel` 不是 canonical resource path，不得写入业务对象、snapshot 或存档。

迁移期同一域可由聚合 source 同时读取 `.tres` 与 `.json`，但必须统一做 entry ID 冲突检查。一个域完成后删除该域的旧 source 分支，避免永久双通道。

seed 驱动、单文件路径驱动和目录扫描域不能强行套用同一个无参目录接口。每个 domain descriptor 必须声明发现策略；world 等路径身份域留到专门阶段处理。

### 7.2 Binder

`JsonDefBinder` 按代码已知目标类型转换：

- primitive、enum、string、`StringName`；
- Godot typed collection 与普通数组；
- 具体 sub-Resource 属性；
- 由代码侧 binding rule 决定的歧义属性；
- Variant Dictionary。

所有赋值必须经过公开属性 setter，保留 `level_overrides`、`level_description_configs` 等现有解析、校验和缓存失效行为。

Binder 不做：

- 文件或资产加载；
- 领域 ID 解析；
- 类型名解析；
- 跨文档对象复用；
- 兼容字段猜测；
- validator 的业务规则复制。

领域 ID 解析由对应 registry/snapshot builder 完成，资产 ID 解析由资产目录或消费侧 resolver 完成。

### 7.3 生命周期

JSON 绑定产生的是没有 `ResourcePath` 的 Resource 对象图，不能沿用路径缓存所有权假设。新增 `ContentImportScope`：

1. `ContentSnapshotBuilder` 创建 import scope；
2. binder 创建的根 Def、子 Def 和需要释放的 Godot collection 全部注册到该 scope；
3. registry 只借用这些 authoring 对象并投影出 immutable definition；
4. registry 先释放，import scope 后按逆序释放；
5. 成功、验证失败、投影异常和重复 ID 路径都必须覆盖释放测试。

`ProcessContentHost` 继续拥有路径加载的 `.tres` roots；它不接管 JSON pathless graph。资产目录中的 Godot 资产是借用对象，也不由 import scope 释放。

不得伪造 `ResourcePath`，不得把 `file.json#id` 注册为 canonical path，也不得让 host 的路径索引把 JSON entry 当成 Godot Resource 文件。

## 8. 关系分类与迁移规则

| 当前关系 | 目标表达 | 负责人 | 验收重点 |
|---|---|---|---|
| Def 的 script ExtResource | JSON schema + code-side descriptor | binder/domain owner | 类型不由内容选择 |
| skill/item/trait 等互相引用 | 稳定领域 ID | 对应 registry/snapshot builder | ID 存在性和类型约束 |
| texture/scene/audio 路径 | 稳定资产 ID | EngineAssetCatalog + consumer | 打包后可解析、类型正确 |
| barrier layer Resource 引用 | layer ID | barrier registry | 顺序、重复、缺失和投影语义 |
| special profile ResourcePath | profile ID | profile registry | preview/execution/AI 同一解析 |
| world generation config path | generation config ID | world/save owner | 保存、恢复、子地图和旧存档决策 |
| family template | 当前 JSON 文件内 template 名 | JsonTemplateMerger | 环、未知模板、确定合并 |

“稳定 ID”与通用引用 DSL 的区别是：ID 属于一个明确领域，并由该领域的 registry、validator 和版本契约解释；它不能指向任意文件、任意 JSON 节点或任意对象类型。

## 9. 验证策略

### 9.1 两类迁移必须分开

#### A. 纯格式平移

内容身份和投影契约不变的域使用 canonical golden：

1. 迁移前对 `*Definition` 图生成键排序的 canonical JSON；
2. 迁移后重新投影；
3. 做精确结构/值比较。

浮点值按实际可逆表示输出，不做固定精度截断。数组顺序默认具有语义，不能排序后比较。

#### B. 契约归一化

路径变资产 ID、Resource 引用变领域 ID、world 路径变存档 ID，都允许快照形状有意变化。此时必须提供：

- 字段级旧值到新 ID 的映射清单；
- 解析结果等价测试；
- execution、preview、AI、UI/HUD 等实际消费者测试；
- 缺失/重复/错类型 ID 的失败测试；
- 存档 round-trip 或明确的不兼容测试。

不能用“golden 不同是预期”一句话放行。

### 9.2 分层测试

1. **纯函数测试**：模板合并、缺省、null、数组替换、环与未知模板；
2. **binder 测试**：setter、StringName、enum、collection、歧义字段规则、未知键；
3. **registry 测试**：多实例、重复 ID、混合格式迁移窗口、source label；
4. **生命周期测试**：成功及所有失败路径无泄漏、无 orphan Resource；
5. **投影 golden**：格式平移域的精确等价；
6. **行为回归**：技能执行/预览/AI，物品装备，敌人生成与 UI；
7. **资产目录测试**：ID 唯一、类型正确、缺失聚合、运行时解析；
8. **存档测试**：仅在 world/save 阶段加入；
9. **导出包 smoke test**：确认 JSON 和资产目录进入包且可从实际包启动。

当前仓库没有可直接依赖的导出预设证据，因此“编辑器/headless 可读”不能当作“导出包可读”。导出策略和 smoke test 是实施前置，不是低风险收尾项。

### 9.3 每域完成定义

一个域只有同时满足以下条件才算迁移完成：

- JSON 无类名、脚本路径、资源路径、内容文件路径和通用引用语法；
- 所有关系使用明确的领域 ID 或资产 ID；
- 所有歧义类型由代码侧 descriptor/rule 决定；
- import scope 的成功和失败生命周期测试通过；
- 格式 golden 或契约归一化测试通过；
- runtime、preview、UI、AI、save 中所有受影响消费者已核对；
- 被删除路径没有残余消费者；
- focused regression、routine full suite 和导出 smoke test 分别报告；
- 旧 `.tres` 加载分支已从该域删除；
- 未经用户确认没有新增兼容别名或 fallback。

## 10. 分阶段实施

### 阶段 0：基础设施与高覆盖技能试点

1. 实现 document parser、文件内 template merger、typed binder、domain descriptor、entry batch 和 import scope；
2. 只接入 `SkillContentRegistry`；
3. 试点固定为 7 个 prismatic ward 技能，覆盖 `level_description_configs`、多等级配置和深层 effect Def；
4. 建立 canonical golden、binder 失败测试和生命周期测试；
5. 删除这一个族的旧 `.tres`。

**DG-0**：focused regression、技能投影 golden、启动校验、生命周期测试均通过，且 JSON 审查不需要任何路径/类型/引用 DSL，才进入下一阶段。

### 阶段 1：技能全域

1. 先机械转换并保持最终解析值，不在同一批次重平衡内容；
2. 按族逐批迁移；
3. 在 golden 稳定后再提取文件内模板；
4. 全域完成后删除技能 `.tres` source。

### 阶段 2：物品与配方

1. 先把现有模板链解析成最终值并生成 golden；
2. 迁移到 JSON 后再提炼文件内模板；
3. 只有整个物品域完成并验证后，才删除 `MergeWithTemplate` 等旧合并代码；
4. 不用通用模板 DSL 复刻历史的多套数组合并语义。

### 阶段 3：资产 ID 基础设施与敌人域

1. 建立 typed EngineAssetCatalog 和 resolver；
2. 将敌人战斗贴图从路径链改为资产 ID 链；
3. 覆盖 encounter、unit snapshot、battle board、UI 和打包测试；
4. 再迁移 enemy JSON 并删除旧 seed/path 分支。

该阶段明确是跨层实现，不属于“只改 loader”。

### 阶段 4：其他纯内容域

对 traits、quests、races/subraces、professions、gear sets、faith 等逐域做 preflight。没有路径身份或引擎资产字段的域可走格式平移闸门；发现路径消费者即转入契约归一化流程。

### 阶段 5：关系归一化域

- equipment abilities：使用既有 handler ID/schema owner，不暴露 Def 类型；
- barriers：profile 只保存 layer ID；
- special resolution profiles：消费者只传播 profile ID；
- battle encounters：先明确目录发现/seed owner，再迁移。

每个关系域独立提交和验证，避免把 binder、领域关系和行为变化混为一批。

### 阶段 6：world 与存档身份

当前存档保存 `generation_config_path`，恢复逻辑按 canonical path 匹配，mounted submap 也传播相关路径。world 不能随其他内容域一起机械迁移。

在实施前必须由用户明确选择：

1. **不兼容升级**：保存 schema 版本提升，旧存档明确拒绝并给出错误；
2. **批准一次性迁移**：提供受控的 path-to-ID 映射和迁移测试。

未经确认不实现路径别名、双查找或静默 fallback。决策完成前，world 配置保持现状。

### 阶段 7：可选的 Def POCO 化

当所有目标域完成 JSON 迁移后，再单独评估 `XxxDef : Resource` 是否改为 plain C# authoring model。该工作会改变 binder、validator 和投影接缝，不属于本提案的完成条件。

## 11. 风险与停止条件

| 风险 | 等级 | 缓解/停止条件 |
|---|---:|---|
| pathless Resource 图生命周期错误 | 高 | import scope + 异常路径测试；不能证明 owner 时停止扩大试点 |
| 资产路径残留在 snapshot/consumer | 高 | 逐字段 owner 搜索和行为测试；不得只改 JSON |
| world 迁移破坏旧存档 | 高 | 阶段 6 单独授权；无决定不迁移 |
| binder 复刻 setter 语义失败 | 高 | 一律走 setter，prismatic ward 试点覆盖复杂字段 |
| 模板改变有效值 | 高 | 先机械平移 golden，再单独提模板 |
| 导出包遗漏 JSON/目录 | 高 | 实际导出 smoke test；无 export 配置时不得宣称完成 |
| 多态重新依赖类名 | 中 | code-side rule/handler schema；内容出现类型名即拒绝 |
| 多实例文件放大冲突 | 中 | 按族、20~40 项建议上限、用真实 diff 调整 |
| 双格式长期存在 | 中 | 每域完成即删除旧 source；无永久 fallback |

若阶段 0 必须引入任意文件导入、路径引用、资产路径或内容可选类型才能工作，应停止当前设计，而不是继续扩展 JSON 语法。

## 12. 实施前待确认

只有以下决策会改变总体路线：

1. world/save 阶段选择明确不兼容，还是批准一次性迁移；
2. EngineAssetCatalog 的 Godot 原生承载形式与拆分粒度；
3. 首个实际导出预设及 smoke test 目标平台。

其余问题应通过阶段 0 的代码与测试证据回答，不需要提前扩大语法面。
