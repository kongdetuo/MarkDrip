# 块引用递归解析：通过子解析器实例

## 动机

当前 `StreamingBlockParser` 对块引用的内容有两套独立的解析逻辑：

| 路径 | 方法 | 职责 |
|---|---|---|
| 流式（字符级） | `StartOrUpdateQuoteStreaming()` | 逐字符写入，手动处理 ATX 标题提升 |
| 整行（\n / Complete） | `ProcessQuoteContentLine()` | 手动检测 ATX、分割线、段落 |

两套逻辑都在**重复**主解析器（`ProcessLine`、`StreamCharToBlock`、检测状态）的功能：

- `ProcessQuoteContentLine` 自己检测 ATX heading、thematic break、段落——和 `ProcessLine` 中的顺序完全一致
- `StartOrUpdateQuoteStreaming` 自己检测 `#+␣` 做标题提升——和 `TryResolveLineStart` / `TryPromoteToHeading` 逻辑重复
- 引用内流式路径不支持列表、代码围栏、分割线——因为这些块类型只被 `ProcessLine` / 检测状态处理，不经过 `StartOrUpdateQuoteStreaming`

## 方案

核心思路：**剥离 `>` 前缀后，剩余内容交给一个独立的子解析器实例。子解析器有完整的检测状态，不需要知道自己在引用内。** 这直接对应 CommonMark 规范描述："Strip the `>` prefix and parse the remainder as a document."

### `StreamingBlockParser` 增加输出目标参数

```csharp
public sealed class StreamingBlockParser
{
    private readonly ObservableCollection<DocumentBlock> _outputTarget;

    // 顶层：输出到 document.Blocks
    public StreamingBlockParser(MutableMarkdownDocument document)
    {
        _outputTarget = document.Blocks;
    }

    // 引用内子解析器：输出到 BlockQuoteBlock.Children
    internal StreamingBlockParser(ObservableCollection<DocumentBlock> target)
    {
        _outputTarget = target;
    }
}
```

所有 `_document.Blocks.Add(...)` 替换为 `_outputTarget.Add(...)`，所有 `_document.LastBlock` 替换为 `CurrentLastBlock` 属性（基于 `_outputTarget[^1]`）。约 18 处替换，纯机械工作。

### 外层 Feed + Scope 管理

外层 Feed 不再有 `BlockKind.BlockQuote` 分支。字符到达时：

```
Feed(char c):
  _currentLine.Append(c)

  if (_quoteScopes.Count > 0):
    var scope = _quoteScopes.Peek()
    var prefix = ParseBlockQuotePrefix(_currentLine)

    if (prefix is null):
      PopScope()    // 引用结束，恢复顶层
      // 当前字符回退到顶层处理
    else:
      var (stripped, level) = prefix

      // 嵌套调整
      while (_quoteScopes.Count > level): PopScope()
      while (_quoteScopes.Count < level): PushScope(new BlockQuoteBlock())

      if (当前字符是前缀部分): continue  // 跳过
      if (当前字符是内容部分):
        scope.SubParser.Feed(strippedChar) // 字符转发给子解析器
      continue

  // 正常顶层处理（检测状态 + StreamCharToBlock，不再有 BlockQuote 分支）
```

`_quoteScopes` 栈替代了原有的 `_quoteStack`。

### 子解析器的工作

子解析器的 `Feed` 就是正常的完整解析流程：
- 行首 → `TryStartBlockFromFirstChar` → 检测状态 → `TryResolveLineStart`
- `#` → ATX 标题（检测状态 + 确认空格）
- `- ` → 列表
- ` ``` ` → 代码围栏
- `\n` → `OnLineEnd` → 定稿当前块

子解析器不知道自己被"转发"了。它看到的是干净内容。

### 嵌套自然

```
> > text

外层：剥离 > → 剩余 "> text" → 喂给子解析器
子解析器：收到 ">" → TryStartBlockFromFirstChar → 检测到 >
          → 创建自己的子解析器（递归）
          → 剥离第二个 > → 剩余 "text" → 喂给孙子解析器
          → 孙子解析器创建 Paragraph("text")
          → 块归子解析器的 _outputTarget → 即外层 BlockQuote.Children
```

### 流式路径字符转发

流式场景下，外层逐字符到达，子解析器接收单个字符：

```csharp
// 外层 Feed 循环，在 scope 内
scope.SubParser.Feed(c);  // Feed 方法内部 iterate over 单字符 span
```

子解析器的 `Feed(ReadOnlySpan<char>)` 会 iterate，`_currentLine` 是子解析器自己的 `StringBuilder`。字符被追加到子解析器的 `_currentLine` 中，走完整的检测/分发流程。

性能：`Feed(ReadOnlySpan<char>)` 接受 `ReadOnlySpan<char>`，单个字符传递零额外分配（用 `MemoryMarshal.CreateSpan` 或 `stackalloc char[1]`）。

### `OnLineEnd` 的处理

`\n` 到达时：
1. 如果当前在 scope 内：`scope.SubParser.Feed("\n")` 让子解析器处理行结束
2. 子解析器的 `OnLineEnd` 定稿当前块
3. 外层检查下一行首字符是否不是 `>` → 若是则继续 scope，否则 pop

## 收益

### 1. 消除 ~100 行重复代码

`ProcessQuoteContentLine`（~60 行）删除。`StartOrUpdateQuoteStreaming` 删除或大幅简化。

### 2. 引用内自动支持所有块类型

列表、代码围栏、分割线、Setext 标题——子解析器自动处理，无需额外适配。

### 3. 嵌套天然正确

`>` 递归是子解析器机制的自然结果，不需要额外的嵌套管理。

### 4. 流式路径获得完整表达力

之前流式引用内只支持段落和 ATX 标题；现在列表、代码块等全部可用。

## 改动范围

| 模块 | 改动 |
|---|---|
| `StreamingBlockParser` 构造 | 新增 `internal StreamingBlockParser(ObservableCollection<DocumentBlock>)` |
| 输出重定向 | 18 处 `_document.Blocks` → `_outputTarget`，`_document.LastBlock` → `CurrentLastBlock` |
| `_quoteScopes` 栈 | 新增，替代 `_quoteStack` |
| `Feed` 循环 | 增加 scope 预处理阶段，删除 `BlockKind.BlockQuote` 分支 |
| `StartOrUpdateQuoteStreaming` | **删除**（或极大简化） |
| `ProcessQuoteContentLine` | **删除** |
| `ProcessQuoteLine` | 保留，但内部改为通过子解析器处理 |
| `OnQuoteLineEnd` | 简化或删除 |

## 风险

- 子解析器的 `Complete()` 需要在外层 `Complete()` 中触发，确保引用内块的定稿
- 惰性延续（`> Quote\nlazy`）：第二行无 `>` 前缀，外层识别后需要通知子解析器 append 续行
- 子解析器独立状态意味着每个引用层级有独立的 `_currentLine`、`_lineBlockKind`、`_lineStartState`——这是预期的行为，但需要注意内存

## 备选方案

### Scope + 输出重定向（不创建子解析器）

修改 `MutableMarkdownDocument` 增加 `EnterScope`/`ExitScope`，解析器内部交换输出目标。

- 优点：无额外对象分配
- 缺点：`_lineBlockKind` / `_lineIsNew` 时序冲突难以解决，内容字符难以路由到正常检测路径

### 当前内联处理

保持现状。

- 优点：无需改动
- 缺点：重复代码、流式路径表达力受限
