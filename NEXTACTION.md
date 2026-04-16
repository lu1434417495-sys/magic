# Next Action

把 `service_master_reforge` 补成一条**真实可达的端到端回归路径**，不要再只依赖 fake runtime / 局部 regression。

具体目标：

- 在可稳定进入的测试据点里提供一个确定可达的 forge 服务入口。
- 用真实 `GameSession + GameRuntimeFacade + text command` 跑通：
  - 打开据点
  - 进入 forge modal
  - 选择 `master_reforge_iron_greatsword`
  - 确认执行
  - 校验材料消耗、产物入仓、反馈文案、snapshot / text snapshot
- 这一步先不扩 save schema，也不做经济调优。

完成这条端到端路径后，再决定是否把当前共享的 `ShopWindow` 抽成专用 `ForgeWindow`。
