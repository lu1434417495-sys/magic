# 战斗系统已知漏洞 / Known Issues

日期：2026-04-18
覆盖文件：

- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/battle_damage_resolver.gd`
- `scripts/systems/battle_hit_resolver.gd`
- `scripts/systems/battle_repeat_attack_resolver.gd`
- `scripts/systems/battle_session_facade.gd`

条目按严重度降序排列。每条包含**现象 / 证据 / 建议修法**三部分。

---

## 严重（Correctness / 玩家资源泄漏）

### ISSUE-BATTLE-01：地面技能代价在可失败前摇前就被扣除

- **现象**：玩家释放带 precast jump 的地面技能，如果落点被挤占或 `move_unit_force` 失败，AP / AU / 冷却都已扣，但技能判为 `applied = false`，日志没有"代价已消耗"的说明。
- **证据**：`battle_runtime_module.gd:1323-1333`
  ```
  _consume_skill_costs(active_unit, skill_def)      # line 1323
  _append_changed_unit_id(batch, active_unit.unit_id)
  ...
  if not _apply_ground_precast_special_effects(...): # line 1332
      return false                                   # 代价已扣，无法回滚
  ```
- **建议修法**：
  1. 先做 precast 试算（`_apply_ground_precast_special_effects` 的 dry-run 分支），成功后再 `_consume_skill_costs`；或
  2. 失败分支内显式回滚（refund AP / AU / cooldown）。
- **优先级**：高 — 玩家可感知、无规避手段。

### ISSUE-BATTLE-02：连斩段数在 `consume_cost_on_attempt=false` 时永远不扣 AU

- **现象**：重复攻击技能如果定义为"仅命中扣 AU"，实际命中后也不扣。日志里已写"AU 消耗 N"，但角色的 aura 面板不动，等于无限连斩。
- **证据**：`battle_repeat_attack_resolver.gd:57-58`
  ```
  if _should_consume_repeat_attack_cost_on_attempt(repeat_attack_effect):
      _consume_repeat_attack_stage_cost(...)         # 只有这一条扣费路径
  ```

  没有 on-hit 分支；日志行 66-99 里的 `stage_aura_cost` 只是字符串格式化，不触发实际扣费。
- **建议修法**：补一条 on-hit 分支，命中成功后 `_consume_repeat_attack_stage_cost`；或在效果定义层去掉 `consume_cost_on_attempt=false` 的合法性。
- **优先级**：高 — 可被利用获得无限伤害。

### ISSUE-BATTLE-03：互相歼灭判为敌方胜利

- **现象**：最后一击玩家自爆 / 反伤双双清场时，`_check_battle_end` 会给出 `winner_faction_id = &"hostile"`，走到敌方胜利的结算路径（失败画面、战利品归空等）。
- **证据**：`battle_runtime_module.gd:2089-2107`
  ```
  _state.winner_faction_id = &"player" if living_allies > 0 else &"hostile"
  ```

  `living_allies == 0 and living_enemies == 0` 时直接落到 else 分支。
- **建议修法**：
  1. 引入 `&"draw"` 阵营并让结算层分别处理，或
  2. 按"最后击杀的阵营"判胜（需要 `battle_state` 记录 `last_killing_faction_id`）。
- **优先级**：高 — 边界场景下结算完全错。

### ISSUE-BATTLE-04：`preview_command` 不检查 `modal_state`

- **现象**：战斗模态锁定（升级 / 技能选择 / 剧情暂停）期间，UI 的"可走 / 可打"提示依旧亮起，玩家点击时 `issue_command` 会静默拒绝。
- **证据**：
  - `battle_runtime_module.gd:265-296` `preview_command` 未检查 `_state.modal_state`
  - `battle_runtime_module.gd:306` `issue_command` 有 `if _state.modal_state != &"": return batch`
- **建议修法**：在 `preview_command` 入口加同样的 `modal_state` 守卫，给 `preview.allowed = false` 并写提示行。
- **优先级**：中高 — 不影响数值正确性，但 UX 令人困惑。

---

## 中等（平衡 / 可预期性）

### ISSUE-BATTLE-05：伤害逐乘数 floor 到 1，导致减伤失效

- **现象**：高防守+多层减伤的目标（guarding 45% + damage_reduction 30% + resistance）实际最终伤害仍 ≥ 1 的来源并非硬最低保护，而是每一个乘数后都强制 `maxi(..., 1)`。极端场景下所有减伤链式乘起来都不能把伤害压到低于 1 的区间，"叠减伤"本质上等同于"保证挨 1 伤"。
- **证据**：`battle_damage_resolver.gd:108-132`（每一步都 `maxi(int(round(...)), 1)`）
- **建议修法**：
  1. 中间步骤保留精度（用 float 累乘），只在返回前做一次 `maxi(damage, MIN_DAMAGE_FLOOR)`；
  2. `MIN_DAMAGE_FLOOR` 改为配置项（0 或 1），避免硬编码。
- **优先级**：中 — 是平衡设计问题，但现在减伤词条的价值被系统性压平。

### ISSUE-BATTLE-06：已修复：`_apply_timeline_step` 不再用属性公式推进行动进度

- **当前口径**：离散 tick 模式下，`BattleUnitState.action_progress` 只按本次 tick 的 `tu_delta` 累加；每个单位必须用自己的 `BattleUnitState.action_threshold` 判断是否进入行动队列。
- **回归覆盖**：`tests/battle_runtime/run_battle_runtime_smoke.gd::_test_timeline_tick_uses_per_unit_action_threshold`

### ISSUE-BATTLE-07：单格移动不走 semantic 成本校准

- **现象**：`_get_move_path_cost` 的 semantic 重算只在 `anchor_path.size() > 1` 时触发。单步移动沿用 grid_service 的原始 cost，如果 semantic 层有地形惩罚 / 附加规则（非 `ARCHER_QUICKSTEP`），单步和多步的代价不一致。
- **证据**：`battle_runtime_module.gd:787-792`
- **建议修法**：移除 `anchor_path.size() > 1` 的早退，所有路径统一走 semantic 重算；或把"单步特例"明文写进 `_get_move_path_cost` 内部。
- **优先级**：中 — 现在影响面小，但加地形惩罚系统时会直接踩。

### ISSUE-BATTLE-08：burn / 踉跄 的 TU 持续时间与 turn_start 消耗耦合不清

- **现象**：`_apply_turn_start_statuses` 在单位被激活（进入 unit_acting 阶段）时触发一次 burn 伤 / AP 惩罚；`_advance_unit_status_durations` 则跟着 `tu_delta` 每 tick 推进。边界场景：
  - 1 TU burn 在 A 刚激活后被施加 → 下一 tick 前被判 expired，A 从没吃过 burn；
  - burn 在 A 行动前一 tick 施加 → 走到 `_apply_turn_start_statuses` 再吃一次。
    两种时序差整整 1 tick，策划无法预测 burn 的实际生效次数。
- **证据**：
  - `battle_runtime_module.gd:2241-2265` `_apply_turn_start_statuses`
  - `battle_runtime_module.gd:2268-2290` `_advance_unit_status_durations`
  - `battle_runtime_module.gd:243-262` `_apply_timeline_step` 调用 duration 推进
- **建议修法**：
  1. 让 `_advance_unit_status_durations` 只对**非活动单位**推进；活动单位的 duration 在回合结算处统一推进；或
  2. 把 "turn_start 的 tick 伤" 改成 "每 N TU 的周期伤"，duration 推进统一用 tu-based，互斥判断。
- **优先级**：中 — 是设计澄清问题，修法需要与策划对齐。

---

## 轻微（决定性 / 可维护性）

### ISSUE-BATTLE-09：`String.hash()` 做 RNG 种子存在 32-bit 碰撞

- **现象**：`rng.seed = int(roll_seed_source.hash())`，`String.hash()` 是 32-bit。实际碰撞概率极低，但 `attack_roll_nonce` 回卷叠加 `battle_id` / `seed` 重用时理论上可能给出完全同序列的 RNG 结果。
- **证据**：`battle_hit_resolver.gd:209-218`
- **建议修法**：改用 64-bit 哈希（Godot 4 的 `hash_djb2_one_64` 或自己拼 `int64` from `battle_id.hash() << 32 | nonce`）。
- **优先级**：低 — 现在没有观测到问题，属 future-proofing。

### ISSUE-BATTLE-10：`_get_living_units_in_order` 并不真正过滤 "living"

- **现象**：函数名叫 "living"，但实际返回 `_state.units` 的全部 key，过滤在调用方做。名字误导维护者。
- **证据**：`battle_runtime_module.gd:2345-2349`
- **建议修法**：重命名为 `_get_units_in_order`，或在函数内真正过滤 `is_alive`。
- **优先级**：低 — 纯可读性 / 防误用。

### ISSUE-BATTLE-11：战斗结束后产生的日志行不进 `_state.log_entries`

- **现象**：`issue_command` 里 `for line in batch.log_lines: _state.log_entries.append(line)` 只跑一次；之后 `_check_battle_end` 或 `_end_active_turn` 追加的新日志行不会被 flush 到历史。存档 / 回放时会缺最后几行。
- **证据**：`battle_runtime_module.gd:324-336`
- **建议修法**：
  1. `_check_battle_end` / `_end_active_turn` 内部自己 append 到 `_state.log_entries`；或
  2. 在 `issue_command` 的 `return batch` 之前再统一 flush 一次。
- **优先级**：低 — 仅影响日志回放完整性。

---

## 建议处理顺序

1. **Correctness 链**：ISSUE-01 → 02 → 03 → 05 → 04
2. **平衡 / 体感链**：ISSUE-06 → 07 → 08
3. **Polish**：ISSUE-09 → 11 → 10（10 是重命名，可以随任意 PR 顺带处理）

如果走 Ralph loop，建议把前 5 条各拆成独立 story，带上各自的回归用例（特别是 02 的"连斩不扣费"需要一条 headless 测试来钉死）。
