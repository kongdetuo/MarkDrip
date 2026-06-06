# ADR 002: Document 持有 Parser，暴露 Append(string)

## 日期

2026-06-02

## 状态

已实现

## 来源

[提议: Document 持有 Parser，暴露 Append(string)](../proposals/2026-06-02-document-owns-parser.md)

## 背景

此前 `StreamingBlockParser` 与 `MutableMarkdownDocument` 是分离的，调用方需同时管理两者：

```csharp
var doc = new MutableMarkdownDocument();
var parser = new StreamingBlockParser(doc);

// 每次收到新数据：
parser.Feed(chunk);
markdownView.SetInProgress(parser.CurrentInProgress);
markdownView.SetPartialLine(parser.PartialLine);
```

MVVM 模式下这意味 ViewModel 需要暴露 Parser 的状态，边界不清晰。

## 决策

让 `MutableMarkdownDocument` 内部持有 `StreamingBlockParser`，对外只暴露 `Append(ReadOnlySpan<char>)` / `Append(string)` 方法。Parser 的流式预览状态通过 Document 的 `CurrentInProgress` 和 `PartialLine` 属性暴露。Document 实现 `INotifyPropertyChanged`，MarkdownView 自动订阅并响应变化。

```csharp
var doc = new MutableMarkdownDocument();
doc.Append(chunk);   // MarkdownView 自动更新
```

## 使用示例

```csharp
// VM 层
var doc = new MutableMarkdownDocument();
// 绑定
MarkdownView.Document = doc;
// 流式输入
doc.Append(chunk);   // → 自动更新 Blocks 和预览
doc.Complete();      // → 定稿所有未完成块，清空预览
```

## 影响

正面：
- VM 无需知道 Parser 的存在
- 边界清晰：VM 持有 Document，View 绑定 Document
- MarkdownView 的 `SetInProgress` / `SetPartialLine` 仍保留为 public，支持手动控制

负面：
- Model 层依赖 Parser 层（同项目内，不影响程序集边界）
- Document 的 `Append` 每次都会触发 PropertyChanged，微小的性能开销可忽略

## 备选

- ViewModel 包装 Parser 和 Document：增加一层间接，但没有实际收益
- Parser 作为 Document 的内部实现细节：即本方案
