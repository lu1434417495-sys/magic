# Phase-1 自进化训练流程（续航/生存参数 A+B）

把"参数面第一期 (A 组自身生存投影 + B 组资源续航) 在资源吃紧场景上自进化调参"串起来的执行顺序。
**当前这些步骤的脚本都已就位，但尚未真正跑验证**（等占用 CPU 的 GPU run 结束后再执行）。

相关产物：
- 参数面 A+B：已接进引擎（`BattleAiScoreProfile` + `BattleAiScoreService`，默认中性）。
- 资源吃紧场景：`data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres`（纯近战镜像，覆盖耐力续航+生存投影；MP 续航待另建不风筝法师场景）。
- 中央累积样本库：`evaluator.record_sample` → `tools/battle_sim_tuner/dataset/samples.jsonl`（跨 run 累积、flock 安全、schema 对齐 `objective`）。

---

## 步骤 0 — 前置：确认引擎与默认中性（一次性）

```bash
dotnet build magic.csproj -nologo -clp:ErrorsOnly        # 0 错误
# 默认权重下行为不变（A/B 默认 0/中性）：跑 AI 评分回归套件应原样通过
```

## 步骤 1 — 场景复核（CPU，等 GPU run 结束后）

确认 `attrition_sustain_2v2` 真能出结果（低僵局）且镜像≈50%（有调参梯度）：

```bash
cd tools
battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.validate_scenario \
    --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres \
    --workers 8                      # 8 workers x 3 seeds = 24 局 (n>=20)
```
判据：`resolves`(stalemate<=0.2) 且 `balanced`(|win-0.5|<=0.15) 都 YES 才可用。
不达标就调 roster(HP/AC/aggression/地图)再复核，**别动不可变基线**。

## 步骤 2 — 累积样本（GPU 管线，写入中央库）

用现有 GPU 管线在该场景上跑观测/主动学习；每次评估都会经 `record_sample` 自动追加到中央库。

`run_gpu_tuning_formal` 现已支持 `--scenario`/`--faction`（默认仍 two_archer/player，向后兼容），
无需再改常量：

```bash
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.run_gpu_tuning_formal \
    --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres \
    --faction player \
    --observation-candidates 64 --observation-total-workers 32 \
    --active-learning-rounds 2 --verify-top-k 4 \
    --output-dir ../.tmp_tuner/phase1_attrition
```
样本同时进入该 run 的 `observations.jsonl`（单轮）**和**中央库 `dataset/samples.jsonl`（跨 run 累积）。

## 步骤 3 — 用中央库训练 surrogate（GPU）

在**全部历史样本**(按场景+faction 过滤)上训练，而非单轮：

```bash
battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.train_surrogate_from_central \
    --scenario attrition_sustain_2v2 --faction player \
    --output-dir ../.tmp_tuner/surrogate_attrition
```
需要 CUDA。输出 surrogate `model_path` / `metadata_path` / `final_loss`。

## 步骤 4 — GPU 搜索 + 导出冠军 profile

两种,**推荐 4A**(更强,把每场 CPU 战斗榨得更干、让 GPU 真吃满):

**4A `gpu_search.py`(推荐)** — 集成 surrogate + 悲观目标(`mean−κ·std`,防钻空子)+ CMA-ES(GPU batched 评估)+ 梯度精修。无需先单独训 surrogate(内部自带集成训练),直接吃中央库:
```bash
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.gpu_search \
    --observations tools/battle_sim_tuner/dataset/samples.jsonl \
    --scenario attrition_sustain_2v2 --faction player \
    --ensemble-size 8 --kappa 1.0 --cma-popsize 128 --cma-generations 300 \
    --restarts 3 --polish-steps 300 --top-k 16 \
    --output-dir ../.tmp_tuner/gpu_search_attrition
```
输出 `ranked.json`(含 `acq`/`pred_mean`/`pred_std`)+ `champion_score_profile.tres`。
（需要 cuda venv 里有 `cma`：`pip install cma`。）

**4B `rank_and_export.py`(简版)** — 若已用步骤 3 训好单个 surrogate,只做一次性大池排序:
```bash
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.rank_and_export \
    --model ../.tmp_tuner/surrogate_attrition/surrogate.pt \
    --metadata ../.tmp_tuner/surrogate_attrition/surrogate_meta.json \
    --count 250000 --top-k 16 --output-dir ../.tmp_tuner/rank_attrition
```

## 步骤 5 — 真战斗晋升门（`promote_gate.py`，关键，防 surrogate 钻空子）

冠军 **必须**在真战斗高样本上赢过默认权重、且无回归，才采纳：

```bash
battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.promote_gate \
    --candidate ../.tmp_tuner/rank_attrition/ranked.json \
    --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres \
    --workers 16
```
判据：`Δobj>=margin` 且 loss/僵局无回归 且 n>=20 → 退出码 0 (PROMOTE)，否则 1 (REJECT)。
通过后才把 `champion_score_profile.tres` 落进正式 profile；否则回步骤 2 加样本/纠偏。

---

## 备注 / 待办
- MP 续航维度本期未覆盖（attrition 场景是纯近战）。需另建一个**不风筝的法师场景**（移除/弱化 blink 与 kite 走位）才能给 B 组 MP 参数制造梯度。
- 中央库是跨 run 累积的真值预言机；不要提交（已 gitignore）。训练前可按需 `--scenario`/`--faction` 过滤，避免混场景。
- 一次 CMA/GPU 调参维度建议 ≤30；A+B 约 15 参，单独成期正合适，其余参数冻结为默认。
