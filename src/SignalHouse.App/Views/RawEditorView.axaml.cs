using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SignalHouse.App.Views;

public partial class RawEditorView : UserControl
{
    public RawEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
