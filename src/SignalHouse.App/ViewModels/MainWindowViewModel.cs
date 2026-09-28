using CommunityToolkit.Mvvm.ComponentModel;

namespace SignalHouse.App.ViewModels;

/// <summary>The three editing modes the shell window switches between.</summary>
public enum EditorMode
{
    Passport,
    Jpeg,
    Raw,
}

/// <summary>
/// Hosts the shell window's mode switcher and the three editor view models.
/// Each editor view model keeps its own open document, edit history, and
/// slider state independently -- switching modes never loses in-progress
/// work in the other two editors.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private EditorMode _selectedMode = EditorMode.Jpeg;

    public PassportEditorViewModel PassportEditor { get; } = new();
    public JpegEditorViewModel JpegEditor { get; } = new();
    public RawEditorViewModel RawEditor { get; } = new();
}
