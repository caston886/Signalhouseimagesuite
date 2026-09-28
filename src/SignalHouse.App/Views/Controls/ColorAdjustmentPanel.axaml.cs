using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SignalHouse.App.Views.Controls;

/// <summary>
/// Shared slider panel (tone, white balance, color balance, clarity),
/// undo/redo/commit buttons, and the universal template list. Bind this
/// control's DataContext to any EditorViewModelBase-derived view model --
/// it's used identically by the Passport, JPEG, and RAW editor views.
/// </summary>
public partial class ColorAdjustmentPanel : UserControl
{
    public ColorAdjustmentPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
