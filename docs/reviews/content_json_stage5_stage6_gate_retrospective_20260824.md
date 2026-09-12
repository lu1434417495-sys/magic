# 阶段 5/6 闸门时序纠偏记录

日期：2026-08-24

状态：Process nonconformity / corrective record

范围：DG-5、DG-6 的历史执行顺序；不替代当前 checkout 的技术验证

## 结论

阶段 5 与阶段 6 没有留下可证明“停下并报告后才进入下一阶段”的 contemporaneous gate 证据。不得补写两份带 PASS 的 DG-5/DG-6 报告来追认已发生的迁移，也不得把当前 checkout 的复跑结果倒签成当时的授权。

本仓库采用以下处置：

- 承认阶段 5/6 以整块迁移提交交付，DG-5/DG-6 的流程要求未被满足；
- 将 `content_json_stage6_preflight_2026-08-19.md` 标为失效的历史 implementation input，不再作为 DG-6 证据；
- 保留阶段 7 之后的当前实现，不为修正文档流程而执行破坏性回滚；
- 当前 checkout 的 build、CLI、schema、回归与内容清点只证明“现在的技术状态”，不证明历史闸门曾正确执行。

## 可复核时序

当前提交历史给出的顺序为：

1. `69ac7e99 refactor: migrate enemy content to JSON`：阶段 5 的 enemy/brain/template/roster 以一个大批次迁移；
2. `8b5aadc7 refactor: migrate battle content domains to JSON`：阶段 6 battle 域整块迁移；
3. `776a35ce refactor: migrate progression content domains to JSON`：阶段 6 progression 域整块迁移；
4. `6e906609 docs: record content JSON migration stages`：之后才加入阶段 6 preflight 与阶段记录。

`docs/reviews/` 中存在 DG-3、DG-4 报告，但没有 DG-5 报告；阶段 6 文件仅定义了逐域完成条件，没有填写逐域执行结果。由此不能推出 DG-5 或 DG-6 曾在下一阶段开始前通过。

## 后续闸门规则

后续任何 DG stop-and-report 点必须同时满足：

1. 在进入下一破坏性阶段前生成带命令、退出码、entry 数、诊断与失败边界的报告；
2. 报告明确区分历史交付、当前 checkout、聚焦复跑、routine full suite 与外部 CI；
3. 任一必需域缺证据时状态只能是 `BLOCKED` 或 `NOT RUN`，不得写为 PASS；
4. 后续复跑可以形成 dated corrective audit，但不能改写原闸门的发生时序。

本记录关闭的是“如何表述与处置历史缺口”的决策，不宣称 DG-5/DG-6 已补过。
