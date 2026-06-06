using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MarkDrip.Controls;
using MarkDrip.Model;
using System.IO;

namespace MarkDrip.Demo;

public partial class MainWindow : Window
{
    private readonly ScrollViewer _scrollViewer;
    private readonly MarkdownView _markdownView;
    private readonly MutableMarkdownDocument _doc = new();
    private readonly string _fullContent;
    private int _position;

    public MainWindow()
    {
        Title = "MarkDrip Demo — 流式渲染 README";
        Width = 800;
        Height = 600;
        Background = new SolidColorBrush(Colors.White);

        var readmePath = Path.Combine(AppContext.BaseDirectory, "README.md");
        _fullContent = File.ReadAllText(readmePath);
        // _fullContent = BuildCodeblockMarkdown();
        //_fullContent = BuildListMarkdown();

        _markdownView = new MarkdownView();
        _markdownView.Document = _doc;

        _scrollViewer = new ScrollViewer
        {
           // Padding = new Thickness(24),

            Content = _markdownView,
            Margin = new Thickness(24),
        };
        Content = _scrollViewer;

        StartStreaming();
    }

    private void StartStreaming()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        var random = new Random();
        timer.Tick += (_, _) =>
        {
            if (_position >= _fullContent.Length)
            {
                _doc.Complete();
                timer.Stop();
                Title = "MarkDrip Demo — 渲染完成";
                return;
            }

            // 喂 1~5 字符给文档（内部自动转发给 Parser）
            int chunkSize = random.Next(1, 6);
            int remaining = _fullContent.Length - _position;
            int take = Math.Min(chunkSize, remaining);
            var chunk = _fullContent.AsSpan(_position, take);
            _position += take;
            _doc.Append(chunk);

            // 布局完成后滚动，确保 Extent 已包含最新内容，不裁剪最后几行
            Dispatcher.UIThread.Post(() => _scrollViewer.ScrollToEnd(), DispatcherPriority.Background);
        };

        timer.Start();
    }

    private static string BuildCodeblockMarkdown()
    {
        return """
# 代码块渲染测试

段落正文，包含一些 *强调* 和 `行内代码`。

## 围栏代码块

```csharp
public class Hello
{
    private readonly string _name;

    public Hello(string name)
    {
        _name = name;
    }

    public void Say() => Console.WriteLine($"Hello, {_name}!");
}
```

上面是一个 C# 代码块，带信息字符串。

## 多语言示例

```python
def fibonacci(n):
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

for i in fibonacci(10):
    print(i)
```

```javascript
function factorial(n) {
    if (n <= 1) return 1;
    return n * factorial(n - 1);
}
console.log(factorial(5));
// 输出: 120
```

## 无信息字符串

```
纯文本代码块，没有语言标识。
```

## 波浪线围栏

~~~
using System;

class Program
{
    static void Main()
    {
        Console.WriteLine("Hello");
    }
}
~~~

## 嵌入在引用中的代码块

> 引用段落。
>
> ```json
> {
>   "name": "MarkDrip",
>   "type": "streaming parser"
> }
> ```
>
> 引用结束。

## 列表中的代码块

- 列表项 1
- 列表项 2

  ```html
  <div class="container">
      <p>嵌套在列表项中的代码块</p>
  </div>
  ```

- 列表项 3
""";
    }

    private static string BuildListMarkdown()
    {
        return """
# 列表渲染测试

## 1. 基本无序列表

- 苹果
- 香蕉
- 樱桃

## 2. 基本有序列表

1. 第一步
2. 第二步
3. 第三步

## 3. 不同子弹字符

* 星号
+ 加号
- 减号

## 4. 续行（单段落跨行）

- 这是一个较长的段落内容，
  它在下一行继续，属于同一段落。

## 5. 多段落 Item（松散列表）

- 第一段落。

  第二段落（缩进续行）。

- 下一个 Item

## 6. 紧排列表 vs 松散列表

- Item A
- Item B

---

- Item C

- Item D

## 7. 嵌套列表

- 水果
  - 苹果
  - 香蕉
- 蔬菜
  1. 胡萝卜
  2. 西兰花

## 8. 缩进列表（前导空格）

  - 缩进一级
  - 仍在一级

## 9. 列表内代码块

- 列表项 1

  ```html
  <div class="container">
      <p>嵌套在列表项中的代码块</p>
  </div>
  ```

- 列表项 2

## 10. 列表内引用块

- 引用内容

  > 这是嵌套在列表中的引用块。
  >
  > 第二行引用。

- 列表项继续

## 11. 列表紧跟段落

- 列表项

列表后的段落。

## 12. 混合场景（* + 嵌套 + 代码块）

* 一级 A
  * 二级 A1
  * 二级 A2
    1. 三级有序
    2. 三级有序

  ```text
  二级内的代码块
  ```

* 一级 B

""";
    }
}
