# 评审与审计

本目录保存某一时间点的代码审查、架构审计和目录检视结果。结论可能已被后续实现修复或失效，使用前必须对照当前代码重新验证。

`scripts_review_retained_decisions.md` 是 `scripts/` 检视结论的活台账，只保留两类内容：已复核并明确“不修/暂不修”的保留决策，以及曾被报告、现已修复或已撤销的结论台账。开始任何 `scripts/` 审查前先读它——命中同一失败模式时先确认是否已关闭，不要重报。

它取代了 `scripts_directory_file_by_file_review_2026-06-17.md`（2026-06-17 逐文件模板扫描，447 条历史矩阵条目已于 2026-08-05 复核完毕且未产生新的 correctness finding，2026-08-06 删除）。原文件可用 `git log -- docs/reviews/scripts_directory_file_by_file_review_2026-06-17.md` 找回。语言迁移或文件改名不等同于问题已解决。

新一轮检视应另建文档，不要向活台账机械补行；它只接受“保留决策”和“结论关闭”两类写入。

当前测试有效性整改结果见 [`test_case_effectiveness_followup_2026-08-08.html`](test_case_effectiveness_followup_2026-08-08.html)：逐项记录 38 个源码文本扫描归零、11 个有效性目标、25 条异步处理记录（A05 后续删除，当前 24 个入口）与第二轮 R01–R18 的实际测试代码整改，并把旧审计中 118 条 non-independent helper/aggregator 记录按 52 个文件完整列出。本轮共整改 48 个 distinct routine runner（44 个既有 + 4 个新增）和 2 个 Python tooling 文件；根构建 0 warning / 0 error，新增入口聚焦 4/4、tooling 34/34、最终常规套件 427/427 PASS（451.2 秒）。首次全量 426/427、457.3 秒及 resource-validation 测试路径修正轨迹也保留在报告中。

427 个常规 runner 的完整方法级追溯见 [`test_case_method_effectiveness_audit_2026-08-08.html`](test_case_method_effectiveness_audit_2026-08-08.html)：当前动态重算得到 2,146 个可达 `Test*` 方法与 26 个 inline runner，共 2,172 条；逐条列出文件/行号、实际 Act、直接 oracle/断言摘要和三分区人工审查结论。自动提取仅用于定位展示，不作为测试有效性判定。报告同时明确保留测试暴露的 production gap：facade 尚未把 advance batch 的 report entries 持久化到 `BattleState` snapshot，本轮未修改 production。

常规测试重复性整改见 [`test_duplicate_case_audit_2026-08-08.html`](test_duplicate_case_audit_2026-08-08.html)：逐项核对实际 Act、fixture、oracle 与失败模式后，已落实 22 组高置信重复整改、删除 3 个无效/结构约束用例并重写 1 个名实不符用例；该页的 426 项结果是上一批整改完成时的历史快照，当前权威 inventory 与末轮完整套件结果为 427/427 PASS。
