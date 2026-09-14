# 评审与审计

本目录保存某一时间点的代码审查、架构审计和目录检视结果。结论可能已被后续实现修复或失效，使用前必须对照当前代码重新验证。

`scripts_review_retained_decisions.md` 是 `scripts/` 检视结论的活台账，只保留两类内容：已复核并明确“不修/暂不修”的保留决策，以及曾被报告、现已修复或已撤销的结论台账。开始任何 `scripts/` 审查前先读它——命中同一失败模式时先确认是否已关闭，不要重报。

它取代了 `scripts_directory_file_by_file_review_2026-06-17.md`（2026-06-17 逐文件模板扫描，447 条历史矩阵条目已于 2026-08-05 复核完毕且未产生新的 correctness finding，2026-08-06 删除）。原文件可用 `git log -- docs/reviews/scripts_directory_file_by_file_review_2026-06-17.md` 找回。语言迁移或文件改名不等同于问题已解决。

新一轮检视应另建文档，不要向活台账机械补行；它只接受“保留决策”和“结论关闭”两类写入。

测试有效性审计链已全部闭环并标记为解决：`test_case_effectiveness_audit_2026-08-06.html`（初审：65 ineffective / 198 partial / 327 diagnostic / 38 文本扫描违规）、`test_text_scan_remediation_2026-08-07.html`（38 个文本扫描违规清零）、`test_ineffective_case_remediation_2026-08-07.html`（29/29 CLOSED）、`test_case_effectiveness_followup_2026-08-08.html`（最终跟进：Implemented · verified，最终常规套件 427/427 PASS）。因全部问题已关闭，这四份报告已于 2026-08-16 移除，可用 `git log -- docs/reviews/test_case_effectiveness_audit_2026-08-06.html` 等找回。

427 个常规 runner 的完整方法级追溯见 [`test_case_method_effectiveness_audit_2026-08-08.html`](test_case_method_effectiveness_audit_2026-08-08.html)：当前动态重算得到 2,146 个可达 `Test*` 方法与 26 个 inline runner，共 2,172 条；逐条列出文件/行号、实际 Act、直接 oracle/断言摘要和三分区人工审查结论。自动提取仅用于定位展示，不作为测试有效性判定。报告同时明确保留测试暴露的 production gap：facade 尚未把 advance batch 的 report entries 持久化到 `BattleState` snapshot，本轮未修改 production。

常规测试重复性整改（原 `test_duplicate_case_audit_2026-08-08.html`）已全部落实并标记为解决：22 组高置信重复整改、删除 3 个无效/结构约束用例并重写 1 个名实不符用例，末轮完整套件 427/427 PASS。报告已于 2026-08-16 移除，可用 `git log -- docs/reviews/test_duplicate_case_audit_2026-08-08.html` 找回。
