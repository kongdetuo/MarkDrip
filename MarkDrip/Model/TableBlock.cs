using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkDrip.Model;

public enum ColumnAlignment
{
    None,
    Left,
    Center,
    Right,
}

public class TableBlock : DocumentBlock
{
    public ObservableCollection<TableRow> Rows { get; } = new();
    internal ColumnAlignment[] Alignments { get; set; } = [];

    public TableBlock()
    {
        Kind = BlockKind.Table;
    }
}

public class TableRow : INotifyPropertyChanged
{
    public ObservableCollection<TableCell> Cells { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void NotifyContentChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Content"));
    }
}

public class TableCell : INotifyPropertyChanged
{
    public InlineCollection Inlines { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void NotifyContentChanged()
    {
        Inlines.Seal();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Content"));
    }
}
