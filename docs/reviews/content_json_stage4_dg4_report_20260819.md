# Content JSON 阶段 4 / DG-4 点检报告

> 结论：`PASS`
> 核对日期：`2026-08-19`
> 实现基线：`74dbae6f`（阶段 3 clean baseline 的 save-tag 前置修复）
> 任务定义 SHA256：`83811153FA906F3366DA578AE9CB8B305F43FB0DDA1E8A5EB43202D8A447EC61`
> 方案 v1.4 SHA256：`0D3A06D0E562498AE8FED3418F71C94A293F305040A248EA4CF3CCB4531C54C6`

## 闸门结论

阶段 4 的装备闭包已经由 JSON、tracked schema、纯 CLR import model、正式
domain validator、跨域组合校验和 BattleSim 抽检闭合。生成方的只读输入只有 schema
与发布后的 ID 列表，不需要读取 `.tres`、Resource path 或 Godot authoring Resource。

本报告在 DG-4 停止；没有开始 task.html 的阶段 5–8。

## T4.1–T4.8 交付核对

| 卡片 | 结果 | 可复核事实 |
|---|---|---|
| T4.1 | PASS | 129 条 item icon 迁移清单；109 条旧 `res://icon.svg` 由 catalog 反向索引映射为 `ui.item.icon.default`，20 条空值保持空；publication 拒绝 unknown/wrong-type ID。 |
| T4.2 | PASS | 129 item + 31 template 机械展开为 129 个完整 JSON entry；`templates=0`，entry `template`/`base_item_id` 字段为 0。 |
| T4.3 | PASS | item 30-rule inventory、逐规则反例与 exact diagnostic golden 由 focused regression 锁定。 |
| T4.4 | PASS | equipment abilities 为 55 packs / 170 bindings；3 condition + 26 action = 29 个有效 kind，正式 JSON 全覆盖；payload 为 plain import model，不构造 Resource。 |
| T4.5 | PASS | traits 为 239 entries / 239 unique IDs；save-tag 全为裸标签，语义后缀为 0；49-rule runtime/offline 共用 validator。 |
| T4.6 | PASS | gear sets 1 entry、recipes 4 entries；gear 引用走稳定 ID；`RecipeContentRegistry.Setup(itemDefinitions)` 显式拥有 item Definition 依赖。 |
| T4.7 | PASS | schema、domain、cross-domain、BattleSim 四级服务和稳定机器协议已接线；catalog 只导出 5 个 schema 与 skill/profession/item/trait/pack/binding/set/recipe/texture ID。 |
| T4.8 | PASS | item、template、trait、equipment ability、gear set、recipe 的正式 `.tres` 全部删除；旧合并、adapter/converter、Resource authoring owner 与正式 path fixture 引用清零。 |

零生产消费的 equipment `grant_skill` action 与 `on_battle_end` / `after_battle`
ghost 没有冒充生成能力，已从 handler/schema/content 删除。因此最终词表是 29 个有效
kind，而不是把 ghost 计入的旧 30 个。

## 正式内容规模

| Domain | JSON 文件 | Entries | 其他 |
|---|---:|---:|---:|
| items | 1 | 129 | templates 0；default icon IDs 109；empty icon IDs 20 |
| traits | 1 | 239 | templates 0；suffixed save-tag entries 0 |
| equipment abilities | 1 | 55 packs | 170 bindings；29 distinct handler kinds |
| gear sets | 1 | 1 | ID-only member/trait linkage |
| recipes | 1 | 4 | explicit item Definition orchestration |

`data/configs/items`、`items_templates`、`traits`、`equipment_abilities`、`gear_sets`
和 `recipes` 六个旧正式目录的文件数均为 0。

## 四级生成门

1. schema：strict DTO、unknown-member、kind/payload shape；tracked schema 重导出
   byte-exact。
2. domain：runtime 与 standalone CLI 使用同一 fail-closed validator 和 closed
   vocabulary。
3. cross-domain：候选与当前 snapshot 合并后校验 ID、typed asset、trait source、
   skill-book learn-source、gear threshold/binding 与 recipe item 引用；复用正式
   `ItemTraitContentValidator`、`SkillBookItemContentValidator` 和 gear-set validator。
4. BattleSim：真实 `BattleSimRunner` adapter 对组合后的 immutable Definition 抽检；
   默认合同固定 12 seeds、最多 8 个候选，focused smoke 用 2 seeds / 1 item 实跑。

四级报告均提供 stage、稳定 rule ID、`file.json#entry_id`、JSON pointer、expected、
actual 与独立非零退出码。正式抽检报告不含 `.tres`。

## 验证证据

以下命令在 `E:\game\magic-stage4-content-json` 执行：

```text
dotnet build magic.csproj --no-restore --nologo
  PASS: 0 warnings / 0 errors

dotnet build tools/content_json_validation/Magic.ContentJsonValidation.Cli.csproj --no-restore --nologo
  PASS: 0 warnings / 0 errors

offline CLI formal directories
  items=129/0 diagnostics
  traits=239/0 diagnostics
  equipment_abilities=55/0 diagnostics
  gear_sets=1/0 diagnostics
  recipes=4/0 diagnostics
  skills=705/0 diagnostics

godot --headless -s res://tests/runtime/validation/run_content_json_schema_export_regression.cs
godot --headless -s res://tests/runtime/validation/run_content_json_offline_validation_regression.cs
godot --headless -s res://tests/runtime/validation/run_equipment_closure_generation_catalog_regression.cs
godot --headless -s res://tests/runtime/validation/run_equipment_closure_generation_validation_pipeline_regression.cs
godot --headless -s res://tests/battle_runtime/simulation/run_equipment_closure_generation_battle_sim_gate_regression.cs
  PASS

python tests/run_regression_suite.py --jobs 4 --log-file .tmp/stage4_routine_regression_rerun.log
  PASS: 507 / 507; Failed: 0

python tests/export/run_windows_export_smoke.py
  PASS: packaged success case；missing file / missing entry / wrong type 均按预期非零失败

git diff --check
  PASS
```

首轮 routine suite 为 495/507；失败日志揭示旧 Resource fixture 在 strict import
边界抛异常后没有请求测试退出，以及扁平 item 仍断言 template override 语义。修正 fixture
和测试边界后，12 条失败逐条单跑 PASS，并完成上述从头 507/507 复跑；没有把 timeout
或局部复跑计为全套通过。

## 验证边界

- routine suite 按仓库政策不包含普通数值平衡模拟；阶段 4 专属 BattleSim gate 已单独实跑。
- 没有运行与本闸门无关的全量 battle balance / AI-vs-AI 数值分析。
- Windows export 使用临时目录，成功后已清理；未保留导出产物。
- 原工作树未提交的 Dragon Scale armor/set/ability 内容没有复制进本阶段 checkout；正式
  Stage4 JSON 中 `armor_dragon_scale*` 与 `dragon_scale_set*` 为 0。
