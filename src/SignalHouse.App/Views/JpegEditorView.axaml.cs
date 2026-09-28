using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SignalHouse.App.Views;

public partial class JpegEditorView : UserControl
{
    public JpegEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
