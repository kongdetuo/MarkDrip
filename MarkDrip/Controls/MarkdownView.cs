using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MarkDrip.Model;
using System.Collections.Specialized;
using AvInlineCollection = Avalonia.Controls.Documents.InlineCollection;
using MdDocument = MarkDrip.Model.MutableMarkdownDocument;
using MdInlineCollection = MarkDrip.Model.InlineCollection;

namespace MarkDrip.Controls;

/// <summary>
/// 将 <see cref="MdDocument"/> 渲染为可视化富文本视图的 Avalonia 控件。
/// 本身继承 StackPanel，每个文档块作为其子项。
/// Document 中的块状态（Open / Finalized）决定渲染方式。
/// </summary>
public class MarkdownView : StackPanel
{
    private readonly Dictionary<DocumentBlock, Control> _blockControls = new();

    public static readonly StyledProperty<MdDocument?> DocumentProperty =
        AvaloniaProperty.Register<MarkdownView, MdDocument?>(nameof(Document));

    public MarkdownView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 4;
        this.Styles.Add(new Style(p => p.OfType<Run>())
        {
            Setters =
            {
                new Setter(Run.FontFamilyProperty, new FontFamily("Microsoft YaHei")),
                new Setter(Run.ForegroundProperty, new SolidColorBrush(Color.FromArgb(255, 30, 30, 30))),
            }
        });
    }

    public MdDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DocumentProperty)
        {
            OnDocumentChanged(change.GetOldValue<MdDocument?>(), change.GetNewValue<MdDocument?>());
        }
    }

    // ── Document 绑定 ──

    private void OnDocumentChanged(MdDocument? oldDoc, MdDocument? newDoc)
    {
        UnsubscribeDocument(oldDoc);
        Children.Clear();
        _blockControls.Clear();

        if (newDoc is null) return;

        foreach (var block in newDoc.Blocks)
            AddBlockControl(block);

        SubscribeDocument(newDoc);
    }

    private void SubscribeDocument(MdDocument doc)
    {
        doc.Blocks.CollectionChanged += OnBlocksChanged;
        foreach (var block in doc.Blocks)
            SubscribeBlock(block);
    }

    private void UnsubscribeDocument(MdDocument? doc)
    {
        if (doc is null) return;
        doc.Blocks.CollectionChanged -= OnBlocksChanged;
        foreach (var block in doc.Blocks)
            UnsubscribeBlock(block);
    }

    // ── 块订阅 ──

    private void SubscribeBlock(DocumentBlock block)
    {
        block.PropertyChanged += OnBlockPropertyChanged;
        if (block is BlockQuoteBlock quote)
        {
            quote.Children.CollectionChanged += OnQuoteChildrenChanged;
            foreach (var child in quote.Children)
                SubscribeBlock(child);
        }
        if (block is ListBlock list)
        {
            list.Items.CollectionChanged += OnListItemsChanged;
            foreach (var item in list.Items)
                SubscribeListItem(item);
        }
        if (block is TableBlock table)
        {
            table.Rows.CollectionChanged += OnTableRowsChanged;
        }
    }

    private void UnsubscribeBlock(DocumentBlock block)
    {
        block.PropertyChanged -= OnBlockPropertyChanged;
        if (block is BlockQuoteBlock quote)
        {
            quote.Children.CollectionChanged -= OnQuoteChildrenChanged;
            foreach (var child in quote.Children)
                UnsubscribeBlock(child);
        }
        if (block is ListBlock list)
        {
            list.Items.CollectionChanged -= OnListItemsChanged;
            foreach (var item in list.Items)
                UnsubscribeListItem(item);
        }
        if (block is TableBlock table)
        {
            table.Rows.CollectionChanged -= OnTableRowsChanged;
        }
    }

    private void SubscribeListItem(ListItemBlock item)
    {
        SubscribeBlock(item);
        foreach (var child in item.Children)
            SubscribeBlock(child);
        item.Children.CollectionChanged += OnListItemChildrenChanged;
    }

    private void UnsubscribeListItem(ListItemBlock item)
    {
        UnsubscribeBlock(item);
        foreach (var child in item.Children)
            UnsubscribeBlock(child);
        item.Children.CollectionChanged -= OnListItemChildrenChanged;
    }

    // ── Collection 变更 ──

    private void OnBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                int addIdx = e.NewStartingIndex;
                foreach (DocumentBlock block in e.NewItems!)
                {
                    var ctrl = CreateBlockControl(block);
                    _blockControls[block] = ctrl;
                    SubscribeBlock(block);
                    Children.Insert(FindInsertIndex(addIdx), ctrl);
                    addIdx++;
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (DocumentBlock block in e.OldItems!)
                {
                    UnsubscribeBlock(block);
                    if (_blockControls.Remove(block, out var ctrl))
                        Children.Remove(ctrl);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                int rIdx = e.NewStartingIndex;
                var oldBlock = (DocumentBlock)e.OldItems![0]!;
                var newBlock = (DocumentBlock)e.NewItems![0]!;
                UnsubscribeBlock(oldBlock);
                if (_blockControls.Remove(oldBlock, out var oldCtrl))
                    Children.Remove(oldCtrl);
                var newCtrl = CreateBlockControl(newBlock);
                _blockControls[newBlock] = newCtrl;
                SubscribeBlock(newBlock);
                Children.Insert(rIdx, newCtrl);
                break;

            case NotifyCollectionChangedAction.Reset:
                foreach (var kv in _blockControls)
                    UnsubscribeBlock(kv.Key);
                _blockControls.Clear();
                Children.Clear();
                break;
        }
    }

    /// <summary>
    /// 当块引用子集合变化时，刷新整个块引用控件。
    /// </summary>
    private void OnQuoteChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 对新增的子块注册订阅
        if (e.NewItems is not null)
        {
            foreach (DocumentBlock child in e.NewItems)
                SubscribeBlock(child);
        }

        // 找到对应的顶层 BlockQuoteBlock 并刷新
        foreach (var (block, _) in _blockControls)
        {
            if (block is BlockQuoteBlock quote && quote.Children == sender)
            {
                RefreshBlockControl(quote);
                return;
            }
        }
    }

    private void OnListItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ListItemBlock item in e.NewItems)
                SubscribeListItem(item);
        }

        foreach (var (block, _) in _blockControls)
        {
            if (block is ListBlock list && (list.Items == sender || ListContainsItems(list, sender)))
            {
                RefreshBlockControl(list);
                return;
            }
        }
    }

    private void OnListItemChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (DocumentBlock child in e.NewItems)
                SubscribeBlock(child);
        }

        // 找到包含此 ListItem Children 的顶层 ListBlock 并刷新
        foreach (var (block, _) in _blockControls)
        {
            if (block is ListBlock list && ListContainsItemChildren(list, sender))
            {
                RefreshBlockControl(list);
                return;
            }
        }
    }

    private void OnTableRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var (block, _) in _blockControls)
        {
            if (block is TableBlock table && table.Rows == sender)
            {
                RefreshBlockControl(table);
                return;
            }
        }
    }

    /// <summary>
    /// 递归搜索 list 中是否有嵌套列表的 Items 集合等于 sender。
    /// </summary>
    private static bool ListContainsItems(ListBlock list, object? sender)
    {
        foreach (var item in list.Items)
            foreach (var child in item.Children)
                if (child is ListBlock nested)
                {
                    if (nested.Items == sender) return true;
                    if (ListContainsItems(nested, sender)) return true;
                }
        return false;
    }

    /// <summary>
    /// 递归搜索 list 中是否有列表项的 Children 集合等于 sender。
    /// </summary>
    private static bool ListContainsItemChildren(ListBlock list, object? sender)
    {
        foreach (var item in list.Items)
        {
            if (item.Children == sender) return true;
            foreach (var child in item.Children)
                if (child is ListBlock nested && ListContainsItemChildren(nested, sender))
                    return true;
        }
        return false;
    }

    private int FindInsertIndex(int blockIndex)
    {
        if (blockIndex <= 0) return 0;
        var prevBlock = Document!.Blocks[blockIndex - 1];
        if (_blockControls.TryGetValue(prevBlock, out var prevCtrl))
        {
            int idx = Children.IndexOf(prevCtrl);
            return idx >= 0 ? idx + 1 : Children.Count;
        }
        return Children.Count;
    }

    // ── 块内容 / 状态变更 ──

    private void OnBlockPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not DocumentBlock block) return;
        if (e.PropertyName is not ("Content" or "Status")) return;

        // 如果该块在顶层映射中，直接刷新
        if (_blockControls.ContainsKey(block))
        {
            RefreshBlockControl(block);
            return;
        }

        // 否则该块在块引用 或 列表内部 → 刷新容器
        foreach (var (topBlock, _) in _blockControls)
        {
            if (topBlock is BlockQuoteBlock quote && ContainsBlock(quote, block))
            {
                RefreshBlockControl(quote);
                return;
            }

            if (topBlock is ListBlock list && ListContainsBlock(list, block))
            {
                RefreshBlockControl(list);
                return;
            }
        }
    }

    private static bool ListContainsBlock(ListBlock list, DocumentBlock target)
    {
        foreach (var item in list.Items)
        {
            if (item == target) return true;
            foreach (var child in item.Children)
            {
                if (child == target) return true;
                if (child is ListBlock nested && ListContainsBlock(nested, target))
                    return true;
            }
        }
        return false;
    }

    private static bool ContainsBlock(BlockQuoteBlock quote, DocumentBlock target)
    {
        foreach (var child in quote.Children)
        {
            if (child == target) return true;
            if (child is BlockQuoteBlock nested && ContainsBlock(nested, target))
                return true;
        }
        return false;
    }

    private void RefreshBlockControl(DocumentBlock block)
    {
        if (!_blockControls.TryGetValue(block, out var oldCtrl)) return;

        int idx = Children.IndexOf(oldCtrl);
        if (idx < 0) return;

        var newCtrl = CreateBlockControl(block);
        _blockControls[block] = newCtrl;
        Children.Remove(oldCtrl);
        Children.Insert(idx, newCtrl);
    }

    // ── 块工厂 ──

    private void AddBlockControl(DocumentBlock block)
    {
        var ctrl = CreateBlockControl(block);
        _blockControls[block] = ctrl;
        SubscribeBlock(block);
        Children.Add(ctrl);
    }

    private Control CreateBlockControl(DocumentBlock block)
    {
        return block.Kind switch
        {
            BlockKind.Heading => CreateHeadingControl((HeadingBlock)block),
            BlockKind.Paragraph => CreateParagraphControl((ParagraphBlock)block),
            BlockKind.CodeBlock => CreateCodeBlockControl((CodeBlock)block),
            BlockKind.ThematicBreak => new Separator
            {
                Height = 2,
                Margin = new Thickness(0, 8),
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            },
            BlockKind.BlockQuote => CreateBlockQuoteControl((BlockQuoteBlock)block),
            BlockKind.List => CreateListControl((ListBlock)block),
            BlockKind.ListItem => new TextBlock { Text = "(list item)" },
            BlockKind.Table => CreateTableControl((TableBlock)block),
            _ => new TextBlock { Text = $"[unexpected block: {block.Kind}]" },
        };
    }

    // ═══════════════════════════════════════
    //  标题
    // ═══════════════════════════════════════

    private Control CreateHeadingControl(HeadingBlock heading)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4),
            FontSize = heading.Level switch
            {
                1 => 24, 2 => 20, 3 => 18,
                4 => 16, 5 => 14, 6 => 12,
                _ => 14,
            },
            FontWeight = heading.Level <= 3 ? FontWeight.Bold : FontWeight.SemiBold,
        };
        BuildInlineContent(tb, heading.Inlines);
        return tb;
    }

    // ═══════════════════════════════════════
    //  段落
    // ═══════════════════════════════════════

    private Control CreateParagraphControl(ParagraphBlock para)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 22,
            Margin = new Thickness(0, 2),
        };
        BuildInlineContent(tb, para.Inlines);
        return tb;
    }

    // ═══════════════════════════════════════
    //  代码块
    // ═══════════════════════════════════════

    private Control CreateCodeBlockControl(CodeBlock code)
    {
        var container = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 4),
        };

        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(code.InfoString))
        {
            stack.Children.Add(new TextBlock
            {
                Text = code.InfoString,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = code.Content.ToString(),
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });

        container.Child = stack;
        return container;
    }

    // ═══════════════════════════════════════
    //  块引用
    // ═══════════════════════════════════════

    private Control CreateBlockQuoteControl(BlockQuoteBlock quote)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 80, 160, 240)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(15, 80, 160, 240)),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Padding = new Thickness(12, 4),
            Margin = new Thickness(0, 2),
        };

        var innerPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        foreach (var child in quote.Children)
        {
            innerPanel.Children.Add(child switch
            {
                ParagraphBlock p => MakeParaBlock(p),
                _ => CreateBlockControl(child),
            });
        }

        border.Child = innerPanel;
        return border;
    }

    private TextBlock MakeParaBlock(ParagraphBlock para)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14 };
        BuildInlineContent(tb, para.Inlines);
        return tb;
    }

    // ═══════════════════════════════════════
    //  列表
    // ═══════════════════════════════════════

    private Control CreateListControl(ListBlock list)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        int index = 1;
        foreach (var item in list.Items)
        {
            panel.Children.Add(BuildListItem(item, list.Style, index++));
        }

        return panel;
    }

    private Control BuildListItem(ListItemBlock item, ListStyle style, int index)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var markerText = style == ListStyle.Bullet ? "\u2022" : $"{index}.";
        var marker = new TextBlock
        {
            Text = markerText,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 6, 0),
        };

        var contentPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        foreach (var child in item.Children)
            contentPanel.Children.Add(CreateBlockControl(child));

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(contentPanel, 1);
        grid.Children.Add(marker);
        grid.Children.Add(contentPanel);
        return grid;
    }

    // ═══════════════════════════════════════
    //  表格
    // ═══════════════════════════════════════

    private static readonly Color _tableLineColor = Color.FromArgb(60, 128, 128, 128);

    private Control CreateTableControl(TableBlock table)
    {
        int colCount = table.Alignments.Length;
        if (colCount == 0) return new TextBlock { Text = "(empty table)" };

        var grid = new Grid
        {
            Margin = new Thickness(0, 8),
            ColumnDefinitions = [.. Enumerable.Range(0, colCount).Select(_ => new ColumnDefinition(GridLength.Star))],
        };

        int rowIdx = 0;
        foreach (var tableRow in table.Rows)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            // Row separator: single line across all columns
            var rowLine = new Border
            {
                BorderBrush = new SolidColorBrush(_tableLineColor),
                BorderThickness = new Thickness(0, 0, 0, rowIdx == 0 ? 2 : 1),
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false,
                MinHeight = 1,
            };
            Grid.SetRow(rowLine, rowIdx);
            Grid.SetColumnSpan(rowLine, colCount);
            grid.Children.Add(rowLine);

            for (int c = 0; c < Math.Min(tableRow.Cells.Count, colCount); c++)
            {
                var tb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    FontWeight = rowIdx == 0 ? FontWeight.Bold : FontWeight.Normal,
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(c < colCount - 1 ? 0 : 0, 0, c < colCount - 1 ? 24 : 0, 0),
                };
                BuildInlineContent(tb, tableRow.Cells[c].Inlines);

                Grid.SetRow(tb, rowIdx);
                Grid.SetColumn(tb, c);
                grid.Children.Add(tb);
            }

            rowIdx++;
        }

        return grid;
    }

    // ═══════════════════════════════════════
    //  行内内容构建
    // ═══════════════════════════════════════

    /// <summary>
    /// 递归发射行内元素到 Avalonia InlineCollection。
    /// Emphasis/StrongEmphasis 将样式累积到 inheritedFw/inheritedFs，
    /// 最终在 TextRun 上直接设置 Run.FontWeight / Run.FontStyle。
    /// </summary>
    private static void EmitInline(
        AvInlineCollection inlines,
        InlineElement element,
        FontWeight? inheritedFw,
        FontStyle? inheritedFs)
    {
        switch (element)
        {
            case TextRun t:
                var run = new Run
                {
                    Text = t.Text,
                    FontWeight = inheritedFw ?? Avalonia.Media.FontWeight.Normal,
                    FontStyle = inheritedFs ?? Avalonia.Media.FontStyle.Normal,
                };
                inlines.Add(run);
                break;

            case Emphasis em:
                foreach (var child in em.Children)
                    EmitInline(inlines, child, inheritedFw, FontStyle.Italic);
                break;

            case StrongEmphasis strong:
                foreach (var child in strong.Children)
                    EmitInline(inlines, child, FontWeight.Bold, inheritedFs);
                break;

            case InlineCode code:
                inlines.Add(new Span
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 12,
                    Inlines = { new Run { Text = code.Code } },
                });
                break;

            case Link link:
                inlines.Add(new Span
                {
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 80, 200)),
                    TextDecorations = TextDecorations.Underline,
                    Inlines = { new Run { Text = concatLinkText(link) } },
                });
                break;

            case Model.Image img:
                inlines.Add(new Run { Text = $"[图片: {img.Alt ?? "(无描述)"}]" });
                break;

            case Model.LineBreak:
            case Model.SoftLineBreak:
                inlines.Add(new Run { Text = "\n" });
                break;
        }
    }

    internal static void BuildInlineContent(TextBlock textBlock, MdInlineCollection collection)
    {
        var inlines = textBlock.Inlines!;
        foreach (var element in collection.GetInlines())
            EmitInline(inlines, element, null, null);
    }

    internal static void PopulateInlines(AvInlineCollection inlines, IReadOnlyList<InlineElement> children)
    {
        foreach (var child in children)
            EmitInline(inlines, child, null, null);
    }

    private static string concatLinkText(Link link)
    {
        var sb = new System.Text.StringBuilder();
        AppendText(sb, link.Children);
        return sb.ToString();
    }

    private static void AppendText(System.Text.StringBuilder sb, IReadOnlyList<InlineElement> elements)
    {
        foreach (var el in elements)
        {
            switch (el)
            {
                case TextRun t: sb.Append(t.Text); break;
                case Emphasis em: AppendText(sb, em.Children); break;
                case StrongEmphasis s: AppendText(sb, s.Children); break;
                case Model.LineBreak:
                case Model.SoftLineBreak: sb.Append(' '); break;
            }
        }
    }
}
