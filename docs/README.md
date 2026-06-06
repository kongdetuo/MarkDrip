# MarkDrip 文档

## 工作流

```
proposals/ → 提议阶段
  随便写几句，说清"想做什么 + 为什么"
  文件名: YYYY-MM-DD-简短描述.md

decisions/ → 定案阶段
  提议成熟后转为 ADR，记录背景、决策、备选方案
  文件名: NNN-标题.md

代码       → 实现阶段
  对应的代码 + 测试
```

提议和 ADR 的界限不必太严格。拿不准就扔 `proposals/`，自然演化。
