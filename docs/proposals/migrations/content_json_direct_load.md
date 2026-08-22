# 内容资源 JSON 直载迁移方案

> **状态**：设计提案（未实施，不含代码或资源改动）
> **版本**：v1.4（2026-08-17）
> **已确认决策**：
>
> - JSON 直接进入 plain C# import model，不创建临时 `Resource`；
> - 内容 JSON 不保存配置路径、内容路径、Godot 资产路径、脚本类型或加载指令；
> - 引擎资产使用稳定 asset ID，并由 Godot typed `.tres` catalog 统一解析；
> - 旧存档不兼容；world 迁移时升级 save/index schema 并明确拒绝旧版本；
> - 首个真实导出验证目标为 Windows Desktop；
> - `.tres` 到 JSON 由受版本控制的一次性转换器机械产出，转换器与 parity 测试共用同一个 canonical writer；
> - validator 重构必须由 diagnostic golden 语料看守，parity 测试不承担该职责；
> - 阶段 1 拆为 1a（import model + parity）和 1b（validator/projector 迁移）两道闸门；
> - **终态范围**：除引擎资产（贴图、场景、音频、着色器）及其 typed catalog 外，`data/configs/` 下不再保留任何 `.tres`；
> - **近期驱动**：装备与技能将由 LLM 大批量生成。schema 导出、离线校验 CLI 与生成闭环因此从支持性工作升为一等交付，域的迁移顺序据此重排。

---

## 1. 结论

本方案可以完整落地。终态不是“用 JSON 重新描述一棵 Godot Resource 图”，而是：

```text
content JSON
  -> plain C# JSON DTO
  -> plain C# domain import model
  -> domain validator/projector
  -> immutable *Definition
  -> ContentSnapshot
```

JSON 不知道 C# 类名、Godot `Resource` 类型、脚本路径、文件路径或资产路径。迁移期现有 `.tres` 通过 domain-specific adapter 转换到同一个 import model；某个域迁完后删除该 adapter 和对应 authoring Resource 类。

引擎资产是唯一保留 Godot Resource authoring 的边界。内容只传播稳定 asset ID；少量 typed asset catalog 由 Godot 保存真实 `Texture2D`、`PackedScene`、`AudioStream`、`Shader` 引用。

现有内容不靠人工改写：既然迁移期本来就要有 `TresAdapter`，把它接上一个 canonical writer 就是转换器，`.tres` 到 JSON 因此是机械产出而非重新录入（第 11 节）。同一个 writer 同时服务 parity 测试，两者不会发散。

需要单独说明的是验证的边界。双层 parity 比较的是同一 validator 的两个输入，因此它对 validator 自身的回归**在构造上不可见**；而本方案单笔体量最大的动作恰好就是把 Resource-bound validator 搬到 import model 上。该风险由独立的 diagnostic golden 语料看守（12.2），不由 parity 承担。

### 1.1 终态范围

终态是：**除引擎资产及其 typed catalog 外，`data/configs/` 下不再保留任何 `.tres`。**

当前 `data/configs/` 有 25 个域、约 1380 个 `.tres`，规模分布极不均匀（实施时须重新统计）：

| 量级 | 域 |
|---|---|
| 数百 | skills、traits、items |
| 数十 | equipment_abilities、enemies、quests、subraces、items_templates |
| 十余 | battle_sim、world_map、battle_encounters、races、age_profiles |
| 个位数 | barriers、professions、barrier_layers、recipes、bloodlines、ascensions、skill_special_profiles、faith、contingency_templates、stage_advancements、gear_sets |

长尾域单独看收益很低，但终态要求它们同样迁移——留任何一个 `.tres` 域，就要永久保留 `TresAdapter`、`.tres` 发现分支和双 source 生命周期，第 9 节的整个简化都拿不到。因此长尾按同一模板批量推进，不逐个论证价值。

### 1.2 两个驱动决定顺序

- **终态驱动**：全部配置 JSON 化，这决定了范围；
- **近期驱动**：装备与技能由 LLM 大批量生成，这决定了顺序。

两者不冲突，但会改变优先级。生成的瓶颈不是「写起来顺不顺手」，而是**幻觉字段的失败模式**：`.tres` 里一个编造的属性名会被静默丢弃，内容照常加载、结构完好、行为不对；JSON 的 `JsonUnmappedMemberHandling.Disallow` 把同一件事变成带 JSON pointer 的硬错误，可直接回喂修复。schema 导出与离线校验 CLI 因此不是便利设施，而是生成能否规模化的前提，必须在生成开始前就位。

顺序上的直接后果：技能与装备闭包（items、equipment_abilities、traits、gear_sets、recipes）提前，敌人/AI、world 与存档后移。

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

这些数字只描述迁移规模，实施时必须重新统计，不能作为固定验收值。

### 2.1 内容 schema 与脚本文件位置耦合

业务内容直接记录 `XxxDef.cs` 路径。类移动、改名或 authoring schema 调整会扩散到大量文件，且内容不能脱离 Godot 独立解析和生成。

### 2.2 模板能力割裂

- 物品已有手写模板合并，但存在逐字段维护、哨兵值和数组语义不一致；
- 技能、敌人缺少统一的文件内模板能力；
- 一文件一实例导致族内重复和批量生产 diff 分散。

### 2.3 路径同时承担来源、身份和资产定位

当前至少存在三类不同问题：

- 物品 icon、敌人 sprite 等内容资产直接保存路径或 typed Resource；
- world generation config 的路径进入 session、mounted submap 和存档；
- special profile、barrier layer 等内容关系依赖 Resource 或 ResourcePath。

这些关系必须分别迁移为 asset ID、配置 ID 或领域 ID，不能用通用引用语法掩盖。

### 2.4 Authoring Resource 生命周期成本

当前 path-backed `.tres` 由 `ProcessContentHost` 统一拥有。若 JSON 再绑定为 pathless Resource，会引入新的 Godot wrapper owner、异常回滚和释放顺序问题。因此本方案不创建 JSON authoring Resource，直接进入 plain C# graph。

### 2.5 弱类型残留已经越过 import 边界

`object` 不只存在于 authoring Resource，也存在于当前 immutable Definition：

- `SkillDefinition.LevelOverrides` 是 `IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>>`；
- `SkillDefinition.LevelDescriptionConfigs` 是同一形状。

因此把 `level_overrides`、`level_description_configs` typed 化不是 import 边界内的局部改动，而是改变 Definition 的公开形状。实施前必须重新统计这两个属性的消费者，并在同一提交内一起改完；不允许 import model 已 typed、Definition 仍暴露 `object` 的中间态。

### 2.6 内容构建不是独立 domain 的并列集合

`ContentSnapshotBuilder` 当前是显式依赖图，不是 domain 循环。至少存在：

- recipe 依赖 item definition；
- special profile 依赖 skill definition；
- enemy 校验和投影依赖 item + skill definition；
- battle encounter 依赖 enemy roster + enemy template；
- 一部分 item definition 由技能合成产生，没有对应 source 文件。

任何「每个 domain 一个 descriptor」的抽象都必须先回答这些关系放在哪里，否则会在阶段 3/4 才发现 descriptor 不足以描述真实构建顺序。

### 2.7 测试自身携带大量内容路径字面量

迁移规模不止于 `data/` 与 `scripts/`。当前测试代码中直接写有 `res://data/configs/...` 路径字面量，去重后数量与 `scripts/` 下的同类字面量不在一个量级，涉及的测试文件数以百计。

这意味着每个 domain 的迁移都必然连带改测试：按路径加载 `.tres` 的 fixture 需要改为按 ID 从 snapshot 取 definition，或改为加载该域的 JSON。这部分工作在原有阶段清单中完全没有计量，实际上可能超过该域生产代码的改动量。

规则：

- 每个 domain 迁移前先统计该域在测试中的路径字面量数量，作为工作量估算的一部分；
- 测试 fixture 改为 ID 驱动，不允许把路径字面量从 `.tres` 换成 `.json` 了事——那只是把同一个耦合换了个扩展名；
- 该项进入每域完成定义。

### 2.8 内容读取存在有意为之的双层语义

`GameContentCatalog` 与 `GameSession` 的内容读取不是同一条链路：一侧基于已发布 snapshot，另一侧在会话内做实时重投影，两者的差异是有意的，不是重复实现。

本方案改的正是这条读取路径，因此：

- 任何 domain 的 preflight 必须先确认该域在这两条链路上分别怎么被读；
- 迁移过程中不得「顺手」把两者合并为一份实现；
- 若某域迁移后两条链路出现行为差异，先确认差异是否本来就存在，再判断是否为回归。

## 3. 目标

1. 内容 JSON 不出现 C# 类名、Godot 类型名、脚本路径、配置路径、内容路径、资产路径或 UID。
2. JSON 只表达领域数据、稳定领域 ID、稳定配置 ID 和稳定 asset ID。
3. JSON 解析、模板合并、import model 和校验均可在不加载 Godot Resource 的情况下测试。
4. 每个域只有一个 canonical import model；`.tres` 和 JSON 迁移期都进入该模型。
5. 每个域只有一个 validator/projector，不长期维护 Resource 与 JSON 两套业务规则。
6. polymorphic 内容使用稳定业务 `kind` 和代码侧 closed spec，不使用 CLR/Godot 类型判别。
7. runtime、preview、AI、UI 和存档只消费 immutable definition 或稳定 ID。
8. 所有路径身份最终从 Definition、runtime state 和新存档中移除。
9. 每个域独立迁移、验证和删除旧 source，不保留永久双加载通道。
10. Windows Desktop 实际导出包能加载 JSON、asset catalog 和全部被引用资产。
11. 现有 `.tres` 到 JSON 的转换是机械可重跑的，且转换器不含独立于 `TresAdapter` 的业务理解。
12. 迁移不降低作者的可用性：JSON 有可导出的 schema，且存在不启动游戏即可运行的秒级校验入口。
13. 终态 `data/configs/` 下无 `.tres`，无 `TresAdapter`，无 `.tres` 发现分支；Godot Resource authoring 只剩引擎资产 catalog。
14. 生成侧闭环可用：结构错误、领域规则违反和数值失衡分别有自动化拦截手段，且诊断可直接回喂给生成方修复。

## 4. 非目标与禁止项

### 4.1 非目标

- 不实现热更新、远程内容分发或运行时内容卸载；
- 不引入旧 JSON schema、旧字段别名或静默兼容；
- 不在同一迁移提交中顺便调整技能、物品或敌人数值；
- 不把场景节点自身的 editor-facing Export 配置全部改成 JSON；
- 不要求引擎资产脱离 Godot 资源系统；
- 不做 JSON 到 `.tres` 的反向转换，转换器是单向且一次性的；
- 不做图形化内容编辑器。作者工具只到 schema 导出与命令行校验为止。

### 4.2 内容 JSON 禁止项

内容 JSON 不得包含：

- `$type`、`$import`、`$ref`、`$asset` 或同类加载器指令；
- `res://`、`uid://` 或其他资源路径；
- 另一份 JSON 的文件路径或 JSON pointer；
- CLR/Godot 类名；
- 任意 Resource 构造描述；
- 跨文件模板继承；
- world generation config 路径；
- 可逃逸到 runtime 的 `JsonNode`、`JsonElement`、`object` 或弱类型 Dictionary。

如果某个关系需要表达，必须归入：

- 领域实体关系：目标 domain 的稳定 ID；
- 配置关系：稳定 config ID；
- 引擎资产关系：稳定 asset ID；
- 同文件去重：文件内 template；
- 业务多态：closed `kind`；
- 动态模板变量：有明确 value schema 的 typed map。

## 5. 目标架构

### 5.1 双源迁移、单模型、单投影

每个迁移中的 domain 采用：

```text
JSON source
  -> DomainJsonDto
  -> DomainImportModel
                    \
                     -> DomainImportValidator
                     -> DomainDefinitionProjector
                    /
.tres source
  -> DomainTresAdapter
  -> DomainImportModel
```

关键规则：

- JSON importer 不调用 `new Resource()`、`ResourceLoader`、`GodotObject.Set` 或 Resource 反射；
- `DomainImportModel` 只包含 plain C# class/record、primitive、enum、数组、`List<T>`、CLR dictionary 和明确 value object；
- JSON DTO 中的字符串 ID 只在 normalizer/projector 边界转换为现有 Definition 需要的 `StringName`；
- `Godot.Collections.Array/Dictionary` 不进入 import model；
- `JsonNode/JsonElement` 只能存在于文档解析和 kind-payload 解析的同步调用栈中；
- validator 和 projector 不读取源文件，也不按来源格式分支。

### 5.1.1 「不加载 Resource」不等于「不经过 Godot 文件系统」

上述禁令针对的是 **Resource 加载与绑定**，不是文件读取。必须区分：

- **禁止**：`ResourceLoader`、`new Resource()`、`GodotObject.Set`、Resource 反射；
- **必须**：字节读取走 Godot `FileAccess`（VFS）。

原因是导出后 `res://` 下的内容位于 `.pck` 内，`System.IO` 读不到。若把「不加载 Resource」误读成「用 `System.IO` 保持 Godot-free」，编辑器与源码目录 headless 全绿，Windows 导出包直接读不到内容——这正是 12.4 导出闸门存在的意义之一，但设计阶段就应避免踩进去。仓库现有文件读写一律走 `FileAccess`，JSON 载入不得成为例外。

「无 Godot 也可测试」（目标 3）由**分层**而非绕开 VFS 达成：

```text
FileAccess  -> byte[]/string      (薄 IO 层，唯一 Godot 依赖，可替换)
            -> document loader    (纯 CLR，接受字符串输入)
            -> template merger    (纯 CLR)
            -> DTO parse          (纯 CLR)
            -> import model       (纯 CLR)
            -> validator          (纯 CLR)
```

解析层以下全部接受内存中的字符串输入，因此单测无需 Godot；IO 层只有一个方法，由测试注入替身。

此外 `.json` 是否随导出包打包，取决于 Godot 对该扩展名的处理方式与导出 preset 的非资源文件过滤设置。当前项目内 png/jpg/csv/svg/fbx/glb 均有 `.import`，`.json` 一个都没有——说明它属于非资源文件，不会像 `.tres` 那样被自动打包。阶段 0 必须显式确认打包路径并把结论写进 `export_presets.cfg` 注释，不能假设「放在 `res://data/` 下就会被打包」。

**既有代码已存在同类缺陷**：当前多个 content registry 的目录发现写成 `DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(dir))` 作为守卫，随后才 `DirAccess.Open(dir)`。后者认 `res://`、在导出包中可用；前者对 `res://` 在导出项目中按 Godot 文档不可用，会得到可执行文件目录下一个并不存在的原生路径，于是守卫返回 false、发现逻辑直接返回。该模式出现十余处，覆盖技能、物品、配方、敌人、任务、职业、套装、屏障、战斗遭遇等域。

由此得出两条：

- 这是与 JSON 无关的既有导出缺陷，建议在阶段 0 之前作为独立小提交修复（去掉 `GlobalizePath`，直接以 `res://` 路径判存在，或仅依赖 `DirAccess.Open` 的返回值）；
- 阶段 0 首次真实导出很可能暴露一批此类既有断裂。该排障时间必须单独预留，不得计入 JSON 迁移工作量，否则阶段 0 会因与本方案无关的原因超期。

### 5.2 基础设施与 domain owner

通用基础设施只负责：

- 文件发现；
- 经 `FileAccess` 的 UTF-8 字节读取；
- envelope/schema/domain/family 解析；
- 文件内 template 合并；
- source label 和 JSON pointer 诊断；
- 重复 entry ID 聚合；
- 调用对应 domain importer。

通用层不负责：

- 反射绑定任意对象；
- 猜测具体业务类型；
- 解析领域 ID；
- 加载资产；
- 执行业务校验；
- 创建 Definition。

每个 domain 注册一个 descriptor：

```csharp
internal sealed record JsonContentDomainDescriptor<TImport, TDefinition>(
    StringName DomainId,
    int SchemaVersion,
    IJsonContentDomainImporter<TImport> JsonImporter,
    ITresContentDomainAdapter<TImport> TresAdapter,
    IContentImportValidator<TImport> Validator,
    IContentDefinitionProjector<TImport, TDefinition> Projector,
    IContentSourceDiscovery SourceDiscovery);
```

`SourceDiscovery` 的目录、seed 和启动位置由代码拥有，不是 JSON 字段。domain 完成迁移后，`TresAdapter` 从 descriptor 和 production build 中删除。

### 5.2.1 descriptor 的职责边界

descriptor 只拥有 **单 domain、无跨域输入** 的那一段：

```text
source discovery -> document load -> template merge -> DTO parse -> import model -> domain-local validation
```

descriptor **不拥有** 跨域构建顺序。`ContentSnapshotBuilder` 保持现有手写的显式依赖编排，只是把每一步的「读 `.tres` 建 Def」换成「读 source 建 import model」。具体规则：

- descriptor 的 `Validator` 只做 domain-local 规则（字段范围、必填、kind 闭合、文件内一致性）；
- 需要其他 domain definition 的规则留在 `ContentSnapshotBuilder` 的 cross-domain 校验阶段，形态不变；
- descriptor 的 `Projector` 只接受本 domain import model；需要外域输入的投影（例如 enemy 投影需要 item definition）保留现有显式签名，由 builder 传入；
- 不为「descriptor 自动拓扑排序」编写通用机制。真实依赖只有个位数条，显式编排比推导更安全，也更容易在 review 中看出顺序错误。

### 5.2.2 合成内容不进入 parity

一部分 item definition 由技能合成产生（skill book 类），没有 source 文件，因此不在任何 import parity 或 Definition golden 的比较集内。规则：

- 合成步骤保持在 builder 中，输入从 Definition 读取，不从 import model 读取；
- 合成产物的正确性由跨域校验和行为回归覆盖，不由 parity 覆盖；
- 每个 domain 的完成定义中必须显式声明「本域是否存在合成产物」，避免把「parity 全绿」误读为「该域全部内容已验证」。

### 5.3 JSON DTO 与 import model

JSON DTO 负责精确格式契约：

- 使用 `System.Text.Json`；
- snake_case 名称通过显式 `JsonPropertyName` 固定；
- `JsonUnmappedMemberHandling.Disallow` 拒绝未知字段；
- required 字段缺失直接报错；
- 不使用把 C# enum 名称直接暴露到 JSON 的通用 enum converter；
- business string 由 domain typed rule 转换为 enum/value object；
- payload converter 只接受该 domain closed spec 中登记的 kind。

import model 负责已经归一化的 authoring 语义。例如：

- `SkillImportModel`；
- `CombatSkillImportModel`；
- `CombatEffectImportModel`；
- `SkillLevelOverrideImportModel`；
- `EnemyAiActionImportModel` 的 closed union；
- `BattleObjectiveImportModel` 的 closed union。

旧 `Variant Dictionary` 必须在 import 边界变成 typed model：

- `level_overrides` -> `SortedDictionary<int, CombatSkillLevelOverrideImportModel>`；
- `level_description_configs` -> `SortedDictionary<int, SkillDescriptionVariables>`；
- effect legacy `params` -> 由 `effect_type` 决定的 typed payload；
- equipment action/condition payload -> 由 `kind` 决定的 typed payload。

前两项同时改变 `SkillDefinition` 的公开属性形状（见 2.5），不是纯 import 边界改动。规则：

- import model、Definition 和消费者在同一提交内一起 typed 化；
- 不保留 `object` 版本属性作为过渡重载；
- 该改动落在阶段 1a，因为 parity 的 canonical 表示依赖这两个属性已经有稳定 typed 形状。

### 5.4 迁移期 Resource adapter

每个 domain 的 `TresAdapter` 只完成一件事：把当前 Godot authoring Resource 转为同一个 import model。

以技能为例：

```text
SkillDef
  -> SkillTresImportAdapter
  -> SkillImportModel
  -> SkillImportValidator
  -> SkillDefinitionProjector
  -> SkillDefinition
```

实施时先把现有 `SkillDefinition.FromResource` 和 Resource-bound validator 拆成：

1. Resource-to-import adapter；
2. import-model validator；
3. import-model-to-definition projector。

`FromResource` 在迁移窗口只能作为上述链路的薄适配入口，不能保留第二套投影逻辑。技能域迁完后删除它及不再被其他边界使用的 Skill authoring Resource 类。

其他 domain 按相同模式逐域重构，禁止先写一套 JSON projector、以后再“找机会”合并。

## 6. JSON 文档与模板契约

### 6.1 文件结构

```json
{
  "schema": 1,
  "domain": "skills",
  "family": "mage_prismatic_ward",
  "templates": {
    "ward_base": {
      "icon_id": "mage_prismatic_sphere",
      "max_level": 5,
      "non_core_max_level": 3,
      "tags": ["mage", "magic", "defense", "control"],
      "learn_source": "book",
      "growth_tier": "basic",
      "combat_profile": {
        "delivery_categories": ["spell", "force_effect"],
        "target_mode": "unit",
        "target_team_filter": "self",
        "range_value": 0,
        "area_pattern": "self",
        "ap_cost": 2,
        "mp_cost": 60,
        "cooldown_tu": 20,
        "mastery_trigger_mode": "effect_applied",
        "target_selection_mode": "self"
      }
    }
  },
  "entries": [
    {
      "template": "ward_base",
      "skill_id": "mage_prismatic_red_ward",
      "display_name": "虹光·赤红屏障",
      "description": "创造单独的红色虹光屏障，阻挡非魔法投射物；穿越者承受火焰伤害。",
      "mastery_curve": [400, 1000, 2200, 4000, 6500],
      "attribute_growth_progress": {
        "constitution": 20,
        "intelligence": 30,
        "willpower": 10
      },
      "level_description_template": "创造一层固定虹光屏障（半径1格，持续{duration}TU）。消耗2AP/{mp}法力，冷却{cooldown}TU。",
      "level_description_configs": {
        "0": {"cooldown": "20", "duration": "40", "mp": "60"},
        "1": {"cooldown": "20", "duration": "40", "mp": "60"},
        "2": {"cooldown": "20", "duration": "40", "mp": "60"},
        "3": {"cooldown": "15", "duration": "60", "mp": "60"},
        "4": {"cooldown": "15", "duration": "60", "mp": "60"},
        "5": {"cooldown": "15", "duration": "80", "mp": "60"}
      },
      "combat_profile": {
        "skill_id": "mage_prismatic_red_ward",
        "level_overrides": {
          "3": {"cooldown_tu": 15}
        },
        "effect_defs": [
          {
            "effect_type": "layered_barrier",
            "min_skill_level": 0,
            "max_skill_level": 2,
            "duration_tu": 40,
            "save_dc_mode": "caster_spell",
            "save_dc_source_ability": "intelligence",
            "save_ability": "willpower",
            "save_tag": "magic",
            "payload": {
              "area_pattern": "diamond",
              "profile_id": "prismatic_red_ward",
              "radius_cells": 1,
              "save_dc": 16
            }
          },
          {
            "effect_type": "layered_barrier",
            "min_skill_level": 3,
            "max_skill_level": 4,
            "duration_tu": 60,
            "save_dc_mode": "caster_spell",
            "save_dc_source_ability": "intelligence",
            "save_ability": "willpower",
            "save_tag": "magic",
            "payload": {
              "area_pattern": "diamond",
              "profile_id": "prismatic_red_ward",
              "radius_cells": 1,
              "save_dc": 16
            }
          },
          {
            "effect_type": "layered_barrier",
            "min_skill_level": 5,
            "max_skill_level": -1,
            "duration_tu": 80,
            "save_dc_mode": "caster_spell",
            "save_dc_source_ability": "intelligence",
            "save_ability": "willpower",
            "save_tag": "magic",
            "payload": {
              "area_pattern": "diamond",
              "profile_id": "prismatic_red_ward",
              "radius_cells": 1,
              "save_dc": 16
            }
          }
        ]
      }
    }
  ]
}
```

说明：

- `icon_id` 已是稳定 asset ID，不是文件名或路径；
- `effect_type` 是现有技能业务判别值；
- `payload` 是按 effect type 解析的 typed business payload，不是任意 Dictionary；
- 示例字段均属于目标 skill JSON schema，不包含虚构 `effect_id` 或 Resource 类型字段。

### 6.2 模板规则

template 只解决同一文件中的重复：

| 输入 | 合并规则 |
|---|---|
| 对象 + 对象 | 按键递归合并 |
| 数组 | 子项整体替换父项 |
| 标量 | 子项显式覆盖；`0`、`false`、空字符串均为有效值 |
| 键缺失 | 保留模板值；无模板值时交给 DTO/import model 默认 |
| `null` | 仅允许 schema 声明为 nullable 的字段；表示显式空值 |
| 多级 `template` | 允许文件内链式继承，必须检测环和未知模板 |

不提供：

- 数组 append/remove/merge-by 指令；
- 跨文件 template；
- template import；
- 运行时条件模板；
- 任意对象共享引用。

复杂数组需要修改时，entry 写出完整数组。跨文件重复优先提升为正式领域定义、调整 family 划分，或在离线生成工具中复用。

### 6.3 文档身份

- `schema`：该 domain JSON schema 版本；
- `domain`：必须与代码侧 descriptor 一致；
- `family`：组织和诊断标签，不是 runtime ID；
- `template`：当前文件内名称；
- entry 的 `*_id`：正式领域或资产身份；
- `SourceLabel`：`file.json#entry_id`，只用于诊断，不能进入 import model、Definition、snapshot 或存档。

## 7. 业务多态与 closed kind

### 7.1 通用规则

当一个字段允许多个业务形态时，JSON 使用稳定 `kind` 或该 domain 已存在的同义 discriminator：

```json
{
  "kind": "use_unit_skill",
  "action_id": "basic_melee",
  "score_bucket_id": "offense",
  "payload": {
    "skill_ids": ["basic_attack"],
    "target_selector": "nearest_enemy"
  }
}
```

`kind` 的含义是“玩法动作种类”，不是“创建某个 C# 类”。代码侧 closed spec 至少包含：

```text
kind value
typed payload DTO parser
typed import model factory
validator
Definition projector
```

禁止：

- `Type.GetType`；
- 类名到类型的反射注册；
- 根据字段形状猜类型；
- 根据 `action_id`、`skill_id` 或文件名猜类型；
- 未知 kind 回退到 base class。

未知 kind、缺 payload、payload 多字段或少 required 字段都必须 fail-fast。

### 7.2 需要显式闭合的现有域

#### Enemy AI action

`EnemyAiStateDef.actions` 当前依赖具体 Resource 子类。目标 JSON 增加业务 `kind`，值由现有 `EnemyAiActionKind`/typed converter 统一拥有，例如：

- `use_unit_skill`；
- `use_ground_skill`；
- `use_multi_unit_skill`；
- `move_to_multi_unit_skill_position`；
- `use_random_chain_skill`；
- `use_charge`；
- `use_charge_path_aoe`；
- `move_to_range`；
- `move_to_advantage_position`；
- `use_ground_reposition_skill`；
- `retreat`；
- `wait`。

`EnemyAiJsonImporter` 直接创建对应 plain action import union；`EnemyAiDefinitionProjector` 创建现有具体 `EnemyAiActionDefinition`。

#### Battle objective

`BattleEncounterDef.objective` 当前依赖具体 Resource 子类。目标 JSON 使用：

- `elimination`；
- `boss`；
- `defense`；
- `control`；
- `escape`；
- `escort`；
- `intercept`；
- `rescue`；
- `node_operation`。

`BattleObjectiveKind` enum/typed converter 成为唯一值集合 owner。各 kind 的 payload DTO 与对应 immutable objective definition 一一对应。

#### Equipment ability

继续使用现有 `condition.kind`、`action.kind` 和 handler spec，不再引入额外的 handler 标识。现有 spec 中的 expected payload type 需要迁为：

```text
kind -> payload import DTO/parser -> payload Definition projector
```

JSON 不构造现有 payload Resource。

#### Skill effect

`effect_type` 是现有 discriminator。legacy `params` 在 Resource adapter 中转为对应 typed payload；JSON 直接写 `payload`。每个 effect type 的 payload key 集合由单一 spec 拥有。

## 8. 引擎资产目录

### 8.1 承载形式

采用一个或少量 Godot typed `.tres` catalog root。root 不使用 polymorphic `Array<Resource>`，而是分别导出：

- `Array<EngineTextureAssetEntryDef>`；
- `Array<EngineSceneAssetEntryDef>`；
- `Array<EngineAudioAssetEntryDef>`；
- `Array<EngineShaderAssetEntryDef>`。

每类 entry 只含：

- 稳定 `asset_id`；
- 对应明确类型的 Godot Resource 属性。

这些 catalog 是引擎资产边界，不是内容文档。它们允许 Godot 跟踪导入依赖和打包；内容 JSON、Definition、runtime state 和存档都不保存 catalog 路径或资产路径。

**已知崩溃图形**：本仓库有一条成文的 `.tres` typed array 陷阱——`Godot.Collections.Array<T>` 导出属性在 `.tres` 中整个省略不写（依赖 C# 侧字段初始化器）时，内容校验回归会在 GC 阶段 `gchandle.is_released()` fatal，且报错栈全在引擎内部、不指向任何文件。catalog root 恰好是最容易踩中的形状：四类 entry 中很可能有一到两类初期为空。因此：

- catalog `.tres` 必须显式写出全部四个数组，空数组也要写成 `Array[ExtResource("<entry_def>")]([])` 形态并保留对应 `[ext_resource]` 脚本引用，元素类型靠它绑定；
- 不允许「暂时不写、以后有内容再加」；
- 该崩溃**单跑可能通过、成组才崩**，且 `--jobs 1` 同样崩，不是并行写入问题。阶段 0 验收 catalog 时必须跑完整回归分组，不能只跑单个用例判定通过。

另一条相关约束：不要为了整洁删除 entry Def 上暂时无用的 `[Export]` 属性。从 `Resource` 子类删 `[Export]` 会让内容校验回归在 finalizer 阶段高概率崩溃。需要禁止某个取值时在 validator 层 fail-closed，字段留着。

### 8.2 启动与所有权

- catalog root 的启动位置由 `EngineAssetCatalogBootstrap` 的代码常量拥有；
- `ProcessContentHost` 在构建 snapshot 前通过现有 `EngineAssetResolver` 加载并锚定 catalog root；
- resolver 构建 `asset_id -> borrowed typed Resource` 的只读索引；
- catalog root 和其中的 typed assets 由现有 process engine-asset 生命周期拥有；
- JSON import model 不借用或持有 Resource；
- quiescing 后拒绝新的 path load，并保持已发布 snapshot 的 asset ID 查询合同；
- shutdown 时先断开 UI/runtime borrower，再释放 asset index 和 catalog root。

### 8.3 Resolver API

内容消费者使用：

```csharp
T ResolveContentAssetBorrowed<T>(StringName assetId)
    where T : Resource;
```

路径入口只允许代码自身拥有的场景、shader 或 bootstrap 常量使用，并与内容 API 分名：

```csharp
T ResolveCodeAssetBorrowed<T>(string codeOwnedPath)
    where T : Resource;
```

禁止把内容字段传给 code-path API。asset ID 重复、目标缺失、类型不符必须报错；空 asset ID 只在对应内容字段声明 optional 时返回空。

### 8.4 迁移规则

asset catalog 在任何 path-bearing 内容域之前落地。迁移顺序：

1. 为当前真实资产建立唯一 canonical asset ID；
2. 校验同一底层资产不登记多个内容 asset ID；
3. migration adapter 通过 catalog 的反向索引把旧 Resource/path 转成 asset ID；
4. JSON 写 asset ID；
5. Definition、snapshot 和 consumer 改为传播 asset ID；
6. 全域完成后删除反向索引和旧路径 adapter。

具体字段：

- skill `icon_id`：保留名称，语义固定为 asset ID；
- item `icon`：迁为 `icon_asset_id`；
- enemy `battle_sprite_texture`：迁为 `battle_sprite_asset_id`；
- 其他 Texture/Scene/Audio 字段在所属 domain preflight 时同样处理。

资产移动只改 catalog typed reference；内容和存档不变。

## 9. 生命周期与发布

### 9.1 JSON import

JSON import 不创建 GodotObject，因此不需要引入任何 Godot 侧的 import 作用域或 wrapper owner 注册：

- `JsonDocument` 在单文件解析结束时释放；
- `JsonNode` 只在 template merge 和 DTO parse 调用栈内存活；
- DTO/import model 是 plain C# graph；
- validator/projector 完成后只保留 immutable Definition；
- build 失败由普通 CLR 作用域回收，不注册 Godot native owner；
- import model 不进入 process snapshot。

### 9.2 迁移期 .tres

未迁移 domain 继续由 `ProcessContentHost` 按现有 canonical path 规则拥有。`TresAdapter` 只同步读取 borrowed Resource 并创建 plain import model；不得把 raw Resource、Godot collection 或 ResourcePath 留进 model。

domain 完成后：

- 删除 production `.tres` discovery；
- 删除 TresAdapter；
- 删除该 domain 的 host root；
- 删除不再使用的 authoring Resource 类；
- 更新 lifecycle/content root 回归基线。

### 9.3 发布原子性

`ContentSnapshotBuilder` 只有在以下全部完成后才能 seal：

1. 所有 domain 文档读取和 template merge 成功；
2. DTO strict parse 成功；
3. import model normalization 成功；
4. domain validation 成功；
5. cross-domain ID/asset validation 成功；
6. Definition projection 成功；
7. asset catalog 完整且类型正确。

任一步失败都不发布 epoch；已有 `ContentSnapshotPublication` rollback 合同保持不变。

## 10. world 与存档

### 10.1 目标身份

world JSON、Definition、mounted submap、session 和新存档统一使用：

- `generation_config_id`；
- `StringName`/typed ID 作为内存身份；
- ID-keyed `WorldGenerationDefinition` index。

配置文件发现位置只由代码侧 `WorldContentSourceDiscovery` 使用，不写入 JSON、Definition 或存档。

### 10.1.1 隐藏的路径到身份推导

`WorldPresetRegistry` 当前有一条不显眼的路径语义：fallback preset 名称由配置文件名 basename 推导得到。改成 `generation_config_id` 后这条推导没有等价物。因此 world 阶段必须额外：

- 把 fallback preset 名称改为显式作者化字段，或明确它由 `generation_config_id` 直接查表得到；
- 删除按 basename 推导名称的代码路径，不保留「ID 找不到就当作路径再推一次」的兜底；
- 覆盖「preset 名称缺失」和「ID 未登记」两种错误的显式失败测试。

迁移前必须重新扫描一次 world 路径的全部消费者。已知不止内容与存档层，还包括登录/读档 UI 与 headless 会话，实施时按实际结果确定改动清单。

### 10.2 明确不兼容

不实现旧存档兼容。world 迁移提交必须：

1. 把 `SaveSchemaVersions.SaveVersion` 提升到下一版本；
2. 如果 save index/meta 字段同步变化，同时提升 `SaveIndexVersion`；
3. `SaveSerializer` 只接受新 `generation_config_id`；
4. `GameSession`、world preset、mounted submap、world runtime data 和 save list 全链改用 ID；
5. 删除 `generation_config_path` 字段、canonical path 匹配、path-to-ID 映射、别名和双查找；
6. 旧版本存档以明确版本错误拒绝，不尝试读取或修复；
7. 新档、保存、加载、列表重建和 mounted submap round-trip 全部覆盖测试。

这项决策不再是开放问题。

## 11. 内容转换器、作者工具与生成闭环

1380 个 `.tres` 不靠人工改写。转换由一个受版本控制的一次性转换器完成，转换器与验证共用同一段代码，不引入第二套对 authoring 格式的理解。

本节的三件产物用途不同：转换器是**一次性**的，随各域 `TresAdapter` 一并删除；作者工具与生成闭环是**长期**的，且因为 LLM 批量生成的计划（1.2），它们是本方案近期价值的主要载体。

### 11.1 转换方向与唯一职责

转换器只有一个方向，且完全复用迁移期已经存在的组件：

```text
.tres
  -> DomainTresAdapter        (阶段 1a 已建，迁移期本来就要有)
  -> DomainImportModel
  -> ContentCanonicalJsonWriter (与 parity 测试同一个 writer)
  -> .json
```

关键约束：

- 转换器不自己解析 `.tres` 文本。仓库过去做过手写文本格式化的一次性内容脚本，那种做法会形成第二份对 authoring 格式的理解，与 `TresAdapter` 必然发散。本转换器必须以 Godot 加载真实 Resource，走 `TresAdapter`；
- 转换器不包含任何 domain 业务规则。它是 `TresAdapter` 与 canonical writer 的组合，没有第三段逻辑；
- 转换器产出的 JSON 必须能被同一 domain 的 `JsonImporter` 直接读取，不需要人工修补即可通过 DTO strict parse。

### 11.2 canonical writer 是共享组件，不是转换器私有

`ContentCanonicalJsonWriter` 同时服务三处：

1. import parity 测试的两侧 canonical 表示；
2. Definition golden 的稳定序列化；
3. 转换器的实际产出。

因此 writer 的输出必须直接就是目标 schema 的合法文档，而不是「用于比较的调试表示」。writer 规则：

- 对象键按目标 schema 声明顺序输出，不按字母序，保证产出文件可读；
- 数组保持源顺序；
- 浮点使用可逆表示（round-trip 精确），不截固定小数；
- enum/value object 输出稳定业务字符串；
- 省略等于 DTO 默认值的字段，避免产出 1380 个塞满默认值的文件；
- 不输出 source label、文件路径、UID 或任何诊断字段。

parity 比较时对同一 writer 输出做键序无关比较；文件产出时用 writer 的声明顺序。两者是同一 writer 的两种消费方式，不是两个 writer。

**默认值省略与 template 提取的交互**：6.2 规定「键缺失时保留模板值」。因此一旦某个 entry 之后被挂到 template 上，此前被 writer 省略的默认值字段会改为继承模板值，语义静默改变。规则：

- 转换器产出阶段不存在 template，省略默认值是安全的；
- template 提取（11.5 第 5 步）时，凡是模板中出现的键，所有子 entry 必须显式写出该键的值，即使等于默认值；
- 该约束由 template 提取提交自身的 round-trip 复跑保证：提取前后 import model 必须逐字段一致。提取不是纯格式化操作，必须重跑验证。

### 11.3 运行形态

- 转换器是 headless Godot 入口，不是纯 `dotnet` 工具：读取 `.tres` 需要 Godot 资源系统；
- 按 domain 调用，一次一域，与迁移阶段一一对应；
- 输入是该 domain 现有 source discovery 的结果，输出目录由命令行参数指定；
- 默认写到工作目录外的临时目录，`--in-place` 才写入 `data/`；
- 任何一个 entry 转换失败即整域非零退出，不产出部分结果。

### 11.4 转换器不做的事

转换器只做机械等价转换。以下一律不做，留给人工或后续提交：

- 不提取 template。产出是每个 entry 完整展开的文档，template 在 golden 稳定之后由人手工提取（阶段 2 第 3 项、阶段 3 第 3 项已经这样规定）；
- 不做 family 划分和文件合并。首轮产出维持一文件一 entry 的对应关系，便于逐文件 diff 复核；合并为每文件 20–40 entry 是复核之后的独立提交；
- 不调整任何数值；
- 不重命名字段以外的语义改动。路径到 asset ID 的映射是唯一例外，且必须走 catalog 反向索引（8.4），不做字符串猜测；
- 不删除源 `.tres`。删除是该 domain 完成闸门的一部分，不是转换的一部分。

### 11.5 复核与切分顺序

每个 domain 的实际操作顺序固定为：

1. 跑转换器产出到临时目录；
2. 跑 round-trip 验收（11.6），不过就修 adapter 或 writer，不手改产出；
3. `--in-place` 写入 `data/`，作为**一个只含机械转换的提交**；
4. 单独提交做 family 聚合与文件合并，提交前复跑 round-trip；
5. 单独提交做 template 提取，提交前复跑 round-trip；
6. 确认前五步全部闭合后，再单独提交删除 `.tres` source 与 `TresAdapter`。

顺序不可颠倒：第 6 步一旦执行，`.tres` 与 `TresAdapter` 消失，round-trip 失去比较基准，第 4、5 步的人工整理将无法再被验证。整理动作必须全部发生在删除之前。

不允许把机械转换与人工整理混在同一提交里。混在一起时 diff 无法复核，也无法在出问题时二分定位是转换器还是人工整理引入的差异。

### 11.6 转换器验收：round-trip 必须闭合

转换器自身的正确性由一条 round-trip 断言定义，作为该 domain 的转换前置条件：

```text
tres -> TresAdapter    -> ImportModel_A
        转换器产出 json
json -> JsonImporter   -> ImportModel_B
assert canonical(ImportModel_A) == canonical(ImportModel_B)
```

对全域每个 entry 执行，不抽样。这条断言与 12.1 双层 parity 中的 import parity 是同一比较，区别只在于 JSON 一侧来自转换器产出而非人工编写——因此转换器落地后，import parity 事实上由转换器全量覆盖，人工只需为**新写的** JSON 保留少量手写用例。

需要注意 round-trip 闭合**不证明语义正确**：它只证明「`.tres` 与产出 JSON 在 import model 上等价」。如果 `TresAdapter` 本身读错了字段，两侧会一致地错。真正看守语义的是 12.2 的 diagnostic golden 与行为回归。

### 11.7 作者工作流：schema 导出与离线校验

`.tres` 在 Godot inspector 中有类型化字段和下拉；JSON 没有。在 `JsonUnmappedMemberHandling.Disallow` 和 75 个 effect kind 的前提下，一个拼错的字段名等于一次启动失败。因此阶段 0 必须同时交付两件作者工具，它们不是可选优化：

**JSON Schema 导出**

- 由代码侧 DTO 与 closed kind spec 生成，不手写；
- 每个 domain 一份，随 DTO 变更重新生成并纳入版本控制；
- 覆盖字段名、必填、枚举取值和按 kind 的 payload 形状；
- 内容文件不引用 schema 文件路径（那会违反 4.2 的路径禁令），编辑器侧通过工作区配置做 glob 关联。

**离线校验 CLI**

- 不启动游戏、不构建完整 snapshot，只跑「文档解析 → template 合并 → DTO strict parse → domain-local validation」；
- 输入可以是单个文件或整域；
- 诊断输出为 `file.json#entry_id` 加 JSON pointer，与运行时诊断同一格式；
- 目标是秒级反馈，让作者改完立刻能验。

跨域校验仍然只在完整 snapshot 构建时发生，CLI 不承担该职责，也不假装能承担。

### 11.8 冷启动基线

当前 `.tres` 基线已于 `2026-08-18` 记录。测量机为 AMD Ryzen 9 5950X（16 核 / 32 线程）、128 GiB 内存、Windows 10 19045；运行时为 Godot `4.6.2.stable.mono`、.NET SDK `8.0.421`，默认 Debug 构建。测量源态为从 `edeea358d7b0b5af38299086feea48a858ab2a2c` 创建的 isolated clean detached worktree；测量前 tracked 状态为空，该 commit 包含 `9b21c09b`。

复现方法：

1. 在全新 worktree 先执行 `dotnet build magic.csproj`，再运行一次 `godot --headless --editor --quit --path .` 并确认 exit 0，以生成该 worktree 自己的 Godot import cache；省略这一步会使 tracked 图片因缺少 `.godot/imported` 而无法由 `ResourceLoader` 载入；
2. 构建临时探针后，使用同一个入口 `godot --headless -s res://tests/runtime/validation/run_non_ai_content_snapshot_regression.cs`，每个样本启动一个全新的 Godot 进程；
3. 在 `ApplicationLifetimeCoordinator._Ready()` 中以环境变量门控的临时探针紧贴 `ProcessContentHost.BuildAndSeal()` 前后取样：耗时使用 `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()`；当前线程托管分配使用 `GC.GetAllocatedBytesForCurrentThread()` 差值；进程托管总分配使用 `GC.GetTotalAllocatedBytes(precise: true)` 差值。探针不强制 GC，不把引擎启动、GameSession 绑定或 shutdown 纳入计时；测量完成后撤销探针；
4. 先跑 1 次方法校验 / 热身，再采集 10 个新进程样本；所有正式样本必须构建并发布完整 snapshot、报告相同 canonical root 数且 runner 正常以 0 退出；聚合值取 10 个正式样本的中位数，min–max 只描述抖动，不人为剔除离群样本。

本机 `.tres` 基线：

| 指标 | 10 次中位数 | min–max |
|---|---:|---:|
| `BuildAndSeal()` wall time | `2841.742 ms` | `2757.786–2911.314 ms` |
| 当前线程托管分配 | `158,009,892 B`（`150.690 MiB`） | `158,004,304–158,012,872 B` |
| 进程托管总分配 | `158,011,476 B`（`150.691 MiB`） | `158,005,888–158,014,456 B` |

10 个正式样本均为 `1296` 个 canonical roots、snapshot epoch `1`，且 focused runner PASS。分配口径只覆盖 CLR 托管分配，不代表 Godot native allocation、峰值 working set 或完整进程启动内存。

阶段 1/2 的 JSON 复测必须在同一台机器、同一构建配置、同一 headless 入口、同一探针边界和同一聚合规则下进行；对外只报告 JSON 中位数相对该 `.tres` 中位数的变化率 `(json - tres) / tres`，不得跨机器比较绝对值。若 source/content 数量或 `ContentSnapshotBuilder` 的职责已经变化，应先重跑当时 `.tres` 对照或明确归一化范围，不能把内容规模变化误报为解析格式成本。

### 11.9 生成闭环

大批量生成的产出质量不由格式决定，由拦截手段决定。JSON 只是让生成更快——好内容和坏内容一起加速。因此生成必须在闭环中进行：

```text
生成 → schema 校验 → domain 校验 → 跨域 ID 校验 → 战斗模拟抽检 → 接受 / 回喂修复
```

各级拦截的分工与现状：

| 级别 | 拦住什么 | 承载 | 现状 |
|---|---|---|---|
| schema | 字段名幻觉、类型错误、必填缺失、枚举越界 | 导出的 JSON Schema（11.7） | 待建 |
| domain | 单域业务规则违反 | 既有 domain validator | 已存在，迁移期搬到 import model |
| 跨域 | 引用了不存在的技能/物品/特质/资产 ID | `ContentSnapshotBuilder` 跨域校验 | 已存在 |
| 数值 | 结构合法但强度离谱 | 战斗模拟 | 已存在（`scripts/systems/battle/sim/`） |

关键点：

- **前三级的诊断必须是机器可读的**，包含 `file.json#entry_id` 与 JSON pointer，能直接作为修复输入回喂，不能只是给人看的日志行；
- **第四级不可省略**。schema 与 validator 都拦不住「一个 2AP 技能打 200 点伤害」。仓库已有完整模拟设施（runner、content provider、override applier、metrics、analysis output），生成闭环接上它即可，不需要新建；
- **validator 是质量地基**。在人工作者化下，validator 丢一条规则可能几个月不被发现；在生成量级下，同样的疏漏会立刻变成上千个悄悄错掉的条目。这使 12.2 的 diagnostic golden 从「重构保险」变成「开始生成的前提」；
- **生成内容不提取 template**。template 省 token 但让生成方难以产出自洽的整体，且与默认值省略规则相互作用（11.2）。让生成方输出完整展开的 entry；template 只用于人工精修的族。

生成闭环的实现不属于本方案的交付物，但它依赖的四级拦截全部属于。每个进入生成范围的域，其完成定义额外要求：schema 已导出、离线校验 CLI 覆盖、诊断可机读、模拟抽检脚本可对该域内容运行。

## 12. 验证策略

### 12.1 双层 parity

每个迁移域必须同时比较：

#### Import parity

```text
.tres -> TresAdapter -> ImportModel
JSON  -> JsonImporter -> ImportModel
```

两者生成 canonical plain JSON 后精确比较，使用 11.2 的共享 canonical writer。转换器落地后，本层由转换器 round-trip 全量覆盖（11.6）。

#### Definition parity

两条 source 路径再通过同一个 validator/projector，生成 canonical Definition golden 并精确比较。

涉及路径到 ID 的契约归一化时，先通过 catalog/领域映射归一化 import model，再比较 Definition；同时保留字段级旧值到新 ID 的迁移清单。

#### 双层 parity 覆盖不到什么

必须明确记录这一点，否则会把 parity 全绿误读为「重构无回归」：

- parity 的两侧共用同一个 validator/projector。共用组件内部的回归对两侧同时生效，parity **在构造上**不可能发现；
- 因此 parity 无法看守本方案最大的一笔重构——把 Resource-bound validator 搬到 import model 上；
- 合成内容（5.2.2）没有 source，不在比较集内；
- round-trip 闭合不证明 `TresAdapter` 读对了字段（11.6）。

上述四项分别由 12.2 的 diagnostic golden、跨域校验和行为回归覆盖。

### 12.2 Diagnostic golden：validator 重构的唯一看守

技能域的 Resource-bound validator 是一个数千行量级的组件，其产出主要是**校验错误信息**。把它搬到 import model 上时，静默丢失一条规则不会让任何 parity 或 Definition golden 变红：内容全部合法时输出本来就是空诊断。

因此重构动手之前必须先固化诊断行为：

1. **正例语料**：用当前 validator 对全量真实内容跑一遍，记录诊断输出（正常情况应为空，但空也要作为 golden 固定下来）；
2. **反例语料**：为每一条 validator 规则构造至少一个触发用例，记录其准确诊断文本，形成受版本控制的 fixture 集；
3. **规则清点**：反例语料必须逐条对应 validator 中的规则分支，缺一条即视为语料未完成；
4. **重构后比对**：新 validator 对同一批反例产出同一批诊断（文本可差异化，但规则命中集合必须一致）。

规则：

- 该语料在阶段 1a 建立，早于任何 validator 搬迁；
- 语料建立在 `.tres` 侧，因为那是当前唯一 source；
- 重构完成后语料改由 import model 驱动，长期保留，不是一次性脚手架；
- 若某条规则无法构造反例，说明它是死规则，应在重构前单独提交删除，不是带着搬走。

其他域在各自阶段开始前做同样的事，规模按该域 validator 实际体量决定。

### 12.3 测试层

1. **Document tests**：UTF-8、schema/domain、未知根字段、重复 entry ID；
2. **Template tests**：对象合并、数组替换、显式零值、null、环和未知 template；
3. **DTO tests**：unknown member disallow、required、数字范围、ID 格式；
4. **Kind tests**：所有 closed kind、未知 kind、payload 缺失、多余字段、错 payload；
5. **Adapter parity**：`.tres` 和 JSON 的 import model 完全一致；
6. **Converter round-trip**：转换器产出 JSON 全量重新导入后与 `.tres` import model 一致（11.6）；
7. **Diagnostic golden**：validator 规则命中集合在重构前后一致（12.2）；
8. **Definition golden**：投影后完全一致；
9. **Cross-domain validation**：缺失/重复/错域 ID；
10. **Asset catalog tests**：asset ID 和底层资产唯一、类型正确、optional 空值、quiesce/shutdown；
11. **Authoring tool tests**：导出的 JSON Schema 与 DTO 同步、离线校验 CLI 诊断格式与运行时一致；
12. **Behavior tests**：execution、preview、AI、UI/HUD、headless snapshot；
13. **Persistence tests**：仅新 save/index 版本成功，旧版本明确拒绝；
14. **Lifecycle tests**：JSON 查询不增加 process native owner，`.tres` domain 删除后 root count 相应下降；
15. **Windows export smoke**：从实际 Windows Desktop 导出包启动并构建完整 ContentSnapshot。

### 12.4 Windows Desktop 导出闸门

仓库当前没有任何受版本控制的导出配置，因此这是一项绿地基建：需要安装 Godot 导出模板并在 Windows 上接入自动化。阶段 0 新增受版本控制的 `export_presets.cfg`，至少包含一个 Windows Desktop smoke preset：

- JSON 文件被打包；
- typed asset catalog root 被打包；
- catalog 引用的 Texture/Scene/Audio/Shader 被打包；
- headless 或最小启动场景能构建 snapshot；
- 缺文件、缺 catalog entry 或类型错误导致非零退出；
- 导出产物写到临时目录，不提交二进制包。

编辑器运行、`dotnet build` 或源码目录 headless PASS 都不能替代该闸门。

**闸门分级**：导出完整性是发布级关注点，不是每域关注点。为避免内容迁移被与内容无关的 CI 基建阻塞，闸门分两级：

- **DG-0 只卡最小项**：导出包能启动，且 typed asset catalog root 与其引用资产被正确打包。这是 catalog 承载方式能否成立的证伪点，必须在阶段 0 完成；
- **完整 smoke（构建完整 ContentSnapshot）**：作为每个 domain 完成定义的一项，在该域迁移完成时报告，不作为阶段 0 的开工前置。

若导出基建在阶段 0 内未能就绪，允许在最小项通过的前提下开始阶段 1a，但不得进入阶段 2 的全域扩散。

### 12.5 每域完成定义

一个 domain 只有同时满足以下条件才算完成：

- JSON 不含类名、路径、UID 或加载器语法；
- 所有 JSON entry 进入 plain import model；
- 所有 polymorphic shape 有 closed business kind；
- 不存在 `Resource`、`Godot.Collections`、`JsonNode`、`JsonElement` 或 `object` 逃逸到 import model/Definition；
- `.tres` adapter 和 JSON importer 共用 validator/projector；
- import parity、转换器 round-trip、Definition golden 和实际行为回归通过；
- 该域 validator 的 diagnostic golden 语料已建立，且重构前后规则命中集合一致；
- 已显式声明本域是否存在无 source 的合成产物，并说明其覆盖方式；
- 该域 JSON Schema 已导出，离线校验 CLI 覆盖该域；
- 若该域进入生成范围：四级拦截齐备，前三级诊断机器可读，模拟抽检可对该域内容运行（11.9）；
- 该域在测试中的内容路径字面量已清零，fixture 改为 ID 驱动（2.7）；
- 该域在 snapshot 读取与会话内重投影两条链路上的行为都已核对（2.8）；
- runtime、preview、AI、UI、headless、save 的受影响消费者已核对；
- 资产只通过 asset ID 解析；
- domain 的 `.tres` source、TresAdapter 和无剩余用途的 authoring Resource 类已删除；
- 该域的转换器调用路径随 TresAdapter 一并删除；
- focused regression、routine full suite、lifecycle 和完整 Windows export smoke 分别报告；
- 没有旧 schema alias、fallback 或双查找。

## 13. 分阶段实施

### 阶段 0：跨域基础设施

1. 实现 JSON document loader、文件内 template merger、source label 和 domain descriptor；
2. 实现 plain import batch/diagnostic contract；
3. 实现共享 `ContentCanonicalJsonWriter`（11.2）；
4. 实现 typed `EngineAssetCatalogDef`、typed entry Def、bootstrap 和 ID resolver；
5. 把现有 path resolver API 拆成 content-ID 与 code-owned-path 两个入口；
6. 建立 JSON Schema 导出器与离线校验 CLI 骨架（11.7）；
7. 建立 Windows Desktop export preset 和实际导出 smoke runner，并确认 `.json` 的打包机制（5.1.1）；
8. 落地 document/template/kind/catalog/lifecycle 基础测试，catalog 部分跑完整回归分组（8.1）；
9. 记录 `.tres` 冷启动基线（11.8）。

**DG-0**：纯基础设施测试、asset catalog process owner 与导出最小项（12.4）通过，且没有 JSON authoring Resource/binder，才开始 domain 迁移。完整导出 smoke 不是 DG-0 的前置。

### 阶段 1a：技能 import model 与转换器闭合

本阶段只证明「JSON 能无损表达技能」，不触碰 validator。

1. 建立 `SkillJsonDto` / `SkillImportModel` 及 combat/effect typed 子模型；
2. 把 `level_overrides`、`level_description_configs` 和 `effect params` 归一化为 typed model，同步改 `SkillDefinition` 的公开形状与其消费者（2.5、5.3）；
3. 建立 `SkillTresImportAdapter`；
4. 为 `layered_barrier` 建立 effect payload spec；
5. skill `icon_id` 接入 asset catalog；
6. 接入转换器，对 7 个 prismatic ward 技能跑 round-trip；
7. 建立技能 validator 的 diagnostic golden 语料（12.2）——本阶段只**记录**当前行为，不改 validator；
8. 导出技能 JSON Schema，接入离线校验 CLI。

本阶段 validator/projector 保持现状：JSON 侧经 import model 走既有投影入口，允许暂时存在薄转接层。

**DG-1a**：7 个试点的 `.tres` 与转换器产出 JSON 在 import model 上逐字段一致，JSON 不含类名、路径或类型字段，diagnostic 语料建立完成。不过则回到 schema 设计，此时尚未付出 validator 重构成本。

### 阶段 1b：技能 validator/projector 迁移

1. 把现有技能 Resource validator/projector 拆成 import-model validator + import-model-to-definition projector；
2. `FromResource` 收敛为薄适配入口，不保留第二套投影逻辑；
3. 删除 1a 的临时转接层；
4. 对 diagnostic golden 语料比对规则命中集合；
5. 运行 Definition golden、barrier cross-validation、execution/preview/AI/UI 回归。

这是全方案单笔体量最大的重构，且其产出主要是校验诊断。规则：不允许与阶段 2 的内容扩散合并推进；语料比对不通过时先修 validator，不修语料。

**DG-1b**：diagnostic 规则命中集合一致、Definition golden 一致、行为回归通过，才扩大全技能域。

### 阶段 2：技能全域

1. 按 effect type 分批建立 typed payload import spec；
2. 每批先跑转换器机械产出（11.4、11.5），不同时调整数值；
3. 机械转换与 family 聚合、template 提取分别独立提交；
4. 每批覆盖 execution、preview、AI 和描述；
5. 全域完成后删除技能 `.tres` source、TresAdapter、转换器调用路径和无剩余用途的 Skill authoring Resource 类。

### 阶段 3：技能生成闭环上线

技能一旦全域 JSON 化即可开始生成，不必等装备闭包。本阶段是近期驱动的第一个交付点。

1. 打通 11.9 的四级拦截：schema、domain validator、跨域 ID、战斗模拟抽检；
2. 前三级诊断改为机器可读（`file.json#entry_id` + JSON pointer + 稳定规则标识），可直接回喂修复；
3. 模拟抽检接入既有 `scripts/systems/battle/sim/` 设施，确定技能强度的判定口径与告警阈值；
4. 用一小批生成技能实跑闭环，统计各级拦截率与误报率；
5. 根据实跑结果补 schema 描述和 validator 规则——生成方反复犯的错，多数是 schema 表达不足而非模型问题。

**DG-3**：一批生成技能能在无人工修补的情况下走完四级拦截并入库；被拒条目的诊断足以定位到字段。未达成则不扩大生成规模，也不进入装备闭包生成。

### 阶段 4：装备闭包

装备生成需要的是一组域，不是单个 item 域。生成的装备会引用特质、装备能力、套装与配方，任何一个还在 `.tres` 都会让生成方无法闭合。因此这些域同批推进。

1. `ItemDef.icon` 归一化为 `icon_asset_id`（asset catalog 已先行）；
2. 现有 item template 链先通过 TresAdapter 解析成最终 `ItemImportModel`——转换器产出的是**已展开**的完整 entry，旧 template 链不带进 JSON；
3. 建立 item validator 的 diagnostic golden 语料，再迁 validator；
4. equipment abilities：现有 `condition.kind`/`action.kind` spec 改接 plain payload import model，payload Def 逐个对应 import DTO。该域的 kind 词汇是生成方的动作词表，必须完整导出到 schema；
5. traits：按同一模板迁移。装备普遍引用特质，且新装备常需要新特质，不能滞后；
6. gear sets：套装到物品、trait、equipment ability binding 的引用全部走 ID；
7. recipes、weapon/equipment requirement typed 子模型；recipe 对 item definition 的依赖保持在 builder 的显式编排中（5.2.1）；
8. round-trip 稳定后再提取文件内 template（生成内容除外，见 11.9）；
9. 把装备闭包接入 11.9 的四级拦截；
10. 全域完成后删除 `MergeWithTemplate` 等旧合并代码和 Resource source。

**DG-4**：装备生成能在四级拦截下闭合，且生成方只需 schema 与 ID 列表、不需要读取任何 `.tres`。

### 阶段 5：敌人、AI brain/action 与 roster

1. enemy template sprite 全链改为 `battle_sprite_asset_id`；
2. `EnemyAiActionKind` closed spec 覆盖全部 action kind；
3. JSON action 使用 `kind + payload`，直接投影具体 action Definition；
4. seed 发现改为 code-owned JSON discovery；
5. 覆盖 roster、unit snapshot、AI plan/evaluator、mutation diagnostics、battle board 和 UI；
6. 删除 enemy/AI/roster `.tres` source 和 Resource 子类。

### 阶段 6：剩余配置域

终态要求 `data/configs/` 下无 `.tres`，因此剩余域全部迁移，不按单域收益取舍（1.1）。以现有 content registry 为单位清点，开始前按当时实际列表复核：

**有明确关系归一化工作的**

- battle encounters：`BattleObjectiveKind` + typed payload，覆盖 elimination/boss/defense/control/escape/escort/intercept/rescue/node_operation 全部 objective 子类；
- barriers / barrier layers：profile 使用 layer ID，不嵌 Resource；
- special profiles：manifest/profile 使用 profile ID，不传播 ResourcePath。

**按同一模板批量迁移的长尾**

- quests；
- professions；
- races / subraces / race traits；
- faith；
- bloodlines；
- ascensions；
- age profiles；
- stage advancements；
- contingency templates；
- battle sim profiles 与 scenarios（当前部分由代码常量列出固定路径，迁移时改为 code-owned discovery）。

规则：

- 每个 domain 先做 Resource/path/polymorphism preflight，再进入相同完成闸门；
- 长尾域允许合并到同一批次推进，但每个 domain 仍各自报告完成定义，不共用一次验收；
- 长尾单域收益低是预期内的，不作为跳过理由——留任何一个 `.tres` 域都会让第 9 节的生命周期简化和阶段 8 的清理无法完成。

### 阶段 7：world 与不兼容 save schema

1. 重新扫描 `generation_config_path` 的全部消费者，产出改动清单（内容、存档、runtime、headless、UI、测试）；
2. 建立 `WorldGenerationImportModel` 和 ID-keyed index；
3. mounted submap 改用 `generation_config_id`；
4. world preset、GameSession、runtime data、SaveSerializer、save list 全链改用 ID；
5. 处理 fallback preset 名称的路径推导（10.1.1）：改为显式作者化或 ID 查表，删除 basename 推导；
6. 同步提升 save/index schema；
7. 删除全部 `generation_config_path` 生产字段和匹配逻辑；
8. 旧存档明确拒绝；
9. 覆盖新档、保存、恢复、index 重建、子地图和 Windows 导出启动。

### 阶段 8：终态清理

1. 删除所有 content Resource loader/discovery 分支；
2. 删除只服务 `.tres` authoring 的 Def/adapter 与全部 `TresAdapter`；
3. 删除 migration-only asset reverse lookup；
4. 确认 `data/configs/` 下已无 `.tres`，Godot Resource authoring 只剩引擎资产 catalog；
5. 确认 `ProcessContentHost` 只保留 immutable snapshot 发布和 engine asset catalog owner；
6. 更新 `docs/design/` 当前实现文档与 `project_context_units.md`；
7. 运行 routine full suite、lifecycle soak 和 Windows export smoke。

### 阶段顺序说明

顺序由 1.2 的两个驱动共同决定，与「按域大小排序」或「按改动风险排序」都不同：

- 阶段 0–4 服务近期驱动（生成装备与技能），在阶段 3 就产出第一个可用交付，不必等全案完成；
- 阶段 5–8 服务终态驱动（全部配置 JSON 化），可以在生成工作并行进行时逐步推进；
- 阶段 7（world/save）排在最后，因为它与生成无关且唯一涉及存档破坏性变更，放在末尾可让前面所有阶段都不受存档兼容性牵制。

## 14. 预计文件与 owner

### 通用内容基础设施

- `scripts/systems/content/json/ContentJsonDocumentLoader.cs`；
- `scripts/systems/content/json/ContentJsonTemplateMerger.cs`；
- `scripts/systems/content/json/JsonContentDomainDescriptor.cs`；
- `scripts/systems/content/json/ContentImportBatch.cs`；
- `scripts/systems/content/json/ContentCanonicalJsonWriter.cs`；
- `scripts/systems/content/ContentSnapshotBuilder.cs`。

### 引擎资产

- `scripts/systems/content/assets/EngineAssetCatalogDef.cs`；
- typed asset entry Def；
- `scripts/systems/content/EngineAssetResolver.cs`；
- `scripts/systems/content/EngineAssetAccess.cs`；
- 少量 engine asset catalog `.tres`。

### Domain import model

import model、JSON DTO、TresAdapter、validator 和 projector 放在当前 domain owner 附近：

- progression/skills：`scripts/player/progression/`；
- item/recipe：`scripts/player/warehouse/`；
- enemy/AI：`scripts/enemies/`；
- battle encounter：`scripts/systems/battle/content/`；
- world：`scripts/systems/content/world/` 与 `scripts/systems/world/`；
- save：`scripts/systems/persistence/`。

不建立一个包含所有 domain 业务规则的巨型 universal importer。

### 转换器与作者工具

- 转换器入口（headless Godot，按 domain 调用），与既有 `tools/` 脚本同处一层但以 C# 实现；
- JSON Schema 导出器；
- 离线校验 CLI。

三者都只是既有组件的组合外壳：转换器 = `TresAdapter` + canonical writer；schema 导出器 = DTO + closed kind spec 的反射输出；校验 CLI = document loader + template merger + DTO parse + domain-local validator。任何一个开始出现自己的业务规则即视为设计走偏。

转换器随各域 `TresAdapter` 一并删除；schema 导出器与校验 CLI 长期保留。

### 测试与导出

- 通用 JSON/template/catalog/writer 测试放 `tests/runtime/validation/`；
- domain parity/round-trip/diagnostic golden/behavior 测试放现有 `tests/<domain>/`；
- diagnostic golden 语料与反例 fixture 放 `tests/fixtures/` 下按域分目录；
- `export_presets.cfg`；
- Windows export smoke runner；
- 转换器与作者工具自身的测试放 `tests/tooling/`。

## 15. 项目上下文单元影响

实施会改变：

- CU-02：`ProcessContentHost` 从 content Resource root owner 收敛为 snapshot publication + engine asset catalog owner；
- CU-03：world identity 从 path 改为 config ID；
- CU-10/CU-13：item、skill、progression authoring 从 Resource 改为 plain import model；
- CU-18：展示层从内容路径改为 asset ID resolver；
- CU-20：enemy/AI authoring polymorphism 从 Resource subclass 改为 closed kind；
- CU-19：增加 JSON parity 和 Windows export smoke read set。

当前只修改 proposal，不更新 `docs/design/project_context_units.md`。每个代码阶段落地并改变真实 owner/read set 后再同步更新。

## 16. 风险与停止条件

| 风险 | 等级 | 缓解/停止条件 |
|---|---:|---|
| validator 重构静默丢规则，且 parity 在构造上看不见 | 高 | diagnostic golden 语料先于重构建立（12.2）；规则命中集合不一致即停止。生成量级下该疏漏会放大为上千条错误内容 |
| 生成产出结构合法但数值失衡 | 高 | 四级拦截的模拟抽检不可省略（11.9）；schema 与 validator 都拦不住强度问题 |
| schema 表达不足导致生成方被合法字段挡住 | 中 | schema 从 DTO 导出而非手写；阶段 3 用实跑统计误报率并据此补 schema 描述 |
| 生成内容提取 template 后语义漂移 | 中 | 生成内容一律输出完整展开 entry，不提取 template（11.9、11.2 默认值省略规则）|
| Resource adapter 与 JSON importer 形成两套投影 | 高 | 两者必须先汇合到同一 import model；发现重复业务投影即停止 |
| 转换器自建第二套 `.tres` 理解 | 高 | 转换器必须走 `TresAdapter`，禁止文本解析 `.tres`；round-trip 全量闭合 |
| polymorphic kind 未闭合 | 高 | enum/spec 全登记测试；未知 kind 必须失败 |
| asset catalog 漏项或类型错误 | 高 | snapshot seal 前全量校验 + Windows export smoke |
| 物品/敌人迁移早于 asset ID | 高 | catalog 固定在阶段 0，path-bearing domain 不得提前 |
| world 存档身份残留路径 | 高 | save/index 同步升级、旧档拒绝、全链字段清除 |
| 用 `System.IO` 读 JSON，编辑器绿、导出包读不到内容 | 高 | 字节读取必须走 `FileAccess`（5.1.1）；「无 Godot 可测」靠分层而非绕开 VFS |
| catalog `.tres` 空 typed 数组触发 gchandle fatal | 高 | 四个数组一律显式写出、空数组也保留 ext_resource 绑定（8.1）；验收跑完整分组而非单用例 |
| 测试路径字面量的迁移量未计入 | 中 | 每域迁移前先统计（2.7），fixture 改 ID 驱动并进入完成定义 |
| 顺手合并 snapshot 与会话重投影两条读取链 | 中 | 两者差异是有意设计（2.8）；preflight 分别确认，禁止合并 |
| Definition 弱类型改动外溢到消费者 | 中 | `LevelOverrides`/`LevelDescriptionConfigs` 与消费者同提交 typed 化；不留 `object` 重载 |
| 作者失去 inspector 后误写成本上升 | 中 | schema 导出 + 秒级离线校验 CLI 在阶段 0 交付；缺任一即不扩散到全域 |
| 机械转换与人工整理混提交导致无法复核 | 中 | 转换、聚合、template 提取三者分提交（11.5） |
| skill legacy `params` 无法 typed 化 | 中 | 按 effect type 建 payload spec；禁止任意 Dictionary 兜底。实施前重新统计使用面，抽样显示该字段实际使用范围远小于 effect 总数 |
| 多实例文件放大冲突 | 中 | 按族聚合，建议每文件 20–40 entry，以真实 diff 调整 |
| JSON 启动解析成本增长 | 中 | 阶段 0 记录 `.tres` 基线（11.8），阶段 1/2 复测；超预算再考虑离线编译 |
| 导出基建（绿地）阻塞内容迁移 | 中 | 闸门分级（12.4）：DG-0 只卡最小项，完整 smoke 落到每域完成定义 |
| catalog Resource 重新扩散到业务 | 中 | API 分名；Definition/runtime 只允许 asset ID |

停止条件：

- 需要任意 JSON 路径、类型名或通用引用语法才能继续；
- 需要 pathless Resource 才能复用现有 validator；
- 无法让 `.tres` 与 JSON 汇合到同一 import model；
- 转换器无法在某域达成 round-trip 全量闭合，且原因是 authoring 语义本身无法用 JSON 表达；
- diagnostic golden 语料无法覆盖某个 validator 的规则分支；
- domain kind 只能靠字段猜测或 ID 文本猜测；
- Windows 实际导出无法证明 JSON/catalog/依赖完整；
- world 仍需要路径兼容、别名或静默 fallback。

触发任一条件时停止扩大迁移范围并重新评审，不以临时兼容层继续推进。

## 17. 已闭合决策

以下事项不再开放：

1. JSON 直接进入 plain C# import model；
2. 不创建 JSON authoring Resource 或通用反射 binder；
3. polymorphism 使用 stable business kind + code-side closed spec；
4. 引擎资产使用 Godot typed `.tres` catalog；
5. asset catalog 在所有 path-bearing domain 前落地；
6. 旧存档不兼容，world 阶段升级版本并明确拒绝；
7. 首个实际导出目标为 Windows Desktop；
8. 最终删除 content `.tres` source、迁移 adapter 和无剩余用途的 authoring Resource 类；
9. `.tres` 到 JSON 由走 `TresAdapter` 的一次性转换器机械产出，与 parity 共用 canonical writer，不做 template 提取与 family 划分；
10. validator 重构由 diagnostic golden 语料看守，语料先于重构建立；
11. 阶段 1 拆为 1a/1b 两道闸门，schema 设计的证伪早于 validator 重构成本；
12. descriptor 只拥有单域链路，跨域构建顺序保留在 `ContentSnapshotBuilder` 的显式编排中；
13. 终态为除引擎资产及其 typed catalog 外，`data/configs/` 下不保留任何 `.tres`；长尾域单域收益低不构成跳过理由；
14. 迁移顺序服从两个驱动：先服务 LLM 生成（技能 → 生成闭环 → 装备闭包），再完成剩余域，world/save 排在最后；
15. 生成必须在四级拦截闭环中进行，模拟抽检不可省略；生成内容不提取 template。

阶段 0 可以在没有额外架构决策的情况下开始。若实施中出现会改变 owner、存档策略、资产承载或 JSON schema 的新分支，必须先停止并请求用户决定。
