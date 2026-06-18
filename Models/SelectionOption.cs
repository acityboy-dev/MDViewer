using MDViewer.Utilities;

namespace MDViewer.Models;

public sealed class SelectionOption<T> : ObservableObject
{
    private string _label;

    public SelectionOption(T value, string label)
        : this(value, label, null)
    {
    }

    public SelectionOption(T value, string label, string? description)
    {
        Value = value;
        _label = label;
        Description = description;
    }

    public T Value { get; }
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }
    public string? Description { get; }

    public override string ToString() => Label;
}
