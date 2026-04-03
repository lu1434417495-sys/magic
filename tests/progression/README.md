# Progression Unit Tests

运行命令：

```powershell
godot_console.cmd --headless --path . --script res://tests/progression/run_progression_tests.gd
```

目录约定：

- `tests/progression/cases/`：单独的测试用例文件
- `tests/progression/helpers/`：测试基类、fixtures 和辅助构造
- `tests/progression/run_progression_tests.gd`：测试入口

约束：

- 每个测试用例文件头必须说明：
  - 测试内容
  - 输入
  - 输出
- 当前用例覆盖：
  - 技能学习与熟练度升级
  - 转职与职业升级
  - 核心技能补位
  - 技能合并、递归来源追溯与禁止重学

