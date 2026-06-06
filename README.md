# MarkDrip

**流式 Markdown 渲染引擎** — 专为大语言模型（LLM）逐 token 输出场景设计。

> MarkDrip 像水滴一样，一滴一滴地接收 Markdown，实时渲染出完整的排版效果。

---

## 特性一览

### 块级语法

| 特性 | 语法 |
|------|------|
| ATX 标题 | `# H1` · `## H2` · `###### H6` |
| Setext 标题 | 段落后跟 `====` / `----` |
| 段落 | 连续文本行，空行分隔 |
| 围栏代码块 | `` ``` ```` / `~~~` |
| 缩进代码块 | 行首 4 空格 |
| 块引用 | `>` 支持嵌套和惰性延续 |
| 无序列表 | `-` / `*` / `+` |
| 有序列表 | `1.` / `1)` |
| 主题分隔线 | `---` / `***` / `___`（3+ 个） |
| 嵌套列表 | 任意深度 |

### 内联语法

| 特性 | 语法 |
|------|------|
| 斜体 | `*text*` / `_text_` |
| 粗体 | `**text**` / `__text__` |
| 粗斜体 | `***text***` |
| 内联代码 | `` `code` ``（支持多反引号） |
| 链接 | `[text](url "title")` |
| 图片 | `![alt](url)` |
| 硬换行 | 行尾两个空格 + 换行 / `\` + 换行 |
| 软换行 | 段落内单换行符 |
| 转义 | `\*` `\[` `\\` |

### 流式特性

- **逐字符增量解析** — 适合 SSE / WebSocket 逐 token 推送
- **PartialMatch** — 标记被 chunk 边界截断时自动累积，等完整后再匹配
- **零分配前缀匹配** — `LineBuffer` 分段存储，前缀检查直接返回内部 span
- **实时 UI 更新** — `INotifyPropertyChanged` 驱动 Avalonia 控件增量刷新
- **递归嵌套** — 引用内可含任意块类型，列表可嵌套到任意深度
- **块生命周期** — `Open → Finalized`，渲染控件实时跟踪

---

## 演示

以下 Markdown 本身就是 MarkDrip 的"测试样品"——它能解析的所有语法都在这里了。

### 标题

# 一级标题
## 二级标题
### 三级标题
#### 四级标题
##### 五级标题
###### 六级标题

Setext 标题
====

### 段落与换行

这是一个段落。MarkDrip 支持段落内的软换行（单个换行符），
就像这样，会渲染为同一段落内的换行。

空行分隔不同的段落。

行尾两个空格再加换行会产生硬换行··  
就像上面这样。

反斜杠加换行也是硬换行\
同上。

###  Emphasis

*斜体* 和 _斜体_，**粗体** 和 __粗体__，***粗斜体*** 和 ___粗斜体___。

强调可以嵌套：*斜体中的**粗体***。

### 内联代码

行内代码：`var x = 42;`

多反引号：`` `code` `` 和 `` ` `` 可以这样写。

### 链接与图片

[MarkDrip](https://github.com/anomalyco/MarkDrip "流式渲染引擎")

![占位图](https://via.placeholder.com/150)

### 代码块

围栏代码块：

```csharp
// 流式解析器的核心调度
public void Feed(ReadOnlySpan<char> chunk)
{
    while (chunk.Length > 0)
    {
        var segment = index == -1 ? chunk : chunk[..(index + 1)];
        // 分支 A：已有 currentParser → 直接 Append
        // 分支 B：无 currentParser → TryMatch
    }
}
```

缩进代码块（4 空格）：

    function hello() {
        console.log("Hello, MarkDrip!");
    }

### 块引用

> 这是一段引用。
>
> > 这是嵌套引用。
> > 引用内可以包含 **强调** 和 `代码`。
>
> 引用支持惰性延续行，这行没有 `>` 前缀但仍属于引用。

### 列表

无序列表：

- 苹果
- 香蕉
- 樱桃

有序列表：

1. 第一项
2. 第二项
3. 第三项

嵌套列表：

- 水果
  - 苹果
  - 香蕉
- 蔬菜
  1. 胡萝卜
  2. 西兰花

紧凑列表与松散列表（项间有空行时为松散）：

- 紧凑项 1
- 紧凑项 2

- 松散项 1

- 松散项 2

### 分隔线

---

***

___

### 综合示例

> # 引用内的标题
>
> 引用内的段落，包含 **粗体** 和 `代码`。
>
> - 引用内的列表
> - 第二项
>
> ```js
> // 引用内的代码块
> console.log("nested");
> ```

---

## 项目结构

```
MarkDrip/
├── Model/           # 领域模型（块、内联、文档）
│   ├── DocumentBlock.cs       # 块基类
│   ├── BlockKind.cs           # 块类型枚举
│   ├── BlockStatus.cs         # Open / Finalized
│   ├── ParagraphBlock.cs
│   ├── HeadingBlock.cs
│   ├── CodeBlock.cs
│   ├── ThematicBreakBlock.cs
│   ├── ListBlock.cs
│   ├── ListItemBlock.cs
│   ├── BlockQuoteBlock.cs
│   ├── InlineCollection.cs    # 内联集合（惰性解析）
│   └── InlineElement.cs       # 内联元素类型
├── Parser/          # 流式解析器
│   ├── IBlockParser.cs        # 解析器接口 + StreamParser 调度器
│   ├── InlineParser.cs        # 内联解析器
│   └── LineBuffer.cs          # 分段存储缓冲区
├── Controls/        # Avalonia 渲染控件
│   └── MarkdownView.cs
├── MarkDrip.Demo/   # 桌面演示应用
│   └── MainWindow.cs          # 流式模拟器
├── MarkDrip.Tests/  # 单元测试（367+ 用例）
└── docs/
    └── decisions/             # 架构决策记录
```

## 快速开始

```csharp
using MarkDrip.Model;
using MarkDrip.Parser;

var doc = new MutableMarkdownDocument();
doc.Append("# Hello\n\nThis is **MarkDrip**!\n");
doc.Complete();

// doc.Blocks 包含解析后的块集合
foreach (var block in doc.Blocks)
    Console.WriteLine($"{block.Kind}: {block.Status}");
```

### 流式场景

```csharp
// 模拟 LLM 逐 token 输出
var doc = new MutableMarkdownDocument();
var tokens = new[] { "# ", "Hello\n\n", "This ", "is ", "**", "Mark", "Drip", "**!" };
foreach (var t in tokens)
    doc.Append(t);
doc.Complete();
```

### Avalonia 集成

```xml
<!-- XAML -->
<controls:MarkdownView Document="{Binding Document}" />
```

```csharp
// Code-behind
var doc = new MutableMarkdownDocument();
MarkdownView.Document = doc;
doc.Append("# 实时渲染\n\nMarkDrip 驱动 UI 增量更新。");
```

## 许可

MIT
