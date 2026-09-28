using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SignalHouse.Core.History;
using SignalHouse.Core.Imaging;
using SignalHouse.Core.Models;
using SignalHouse.Core.Presets;

namespace SignalHouse.App.ViewModels;

/// <summary>
/// Shared behavior for all three editors: the color/detail adjustment
/// sliders, undo/redo history, the universal preset ("template") system, and
/// the pan/zoom/rotate canvas transform state. JpegEditorViewModel,
/// RawEditorViewModel, and PassportEditorViewModel each derive from this and
/// add only what's specific to their format.
/// </summary>
public abstract partial class EditorViewModelBase : ViewModelBase
{
    private readonly PresetStore _presetStore;
    private EditHistory? _history;
    private bool _isSyncingFromEditState;

    protected EditorViewModelBase(string editorName, PresetStore? presetStore = null)
    {
        EditorName = editorName;
        _presetStore = presetStore ?? new PresetStore();
        RefreshPresets();
    }

    /// <summary>Which editor this is ("Passport", "JPEG", "RAW") -- recorded on presets saved from here.</summary>
    public string EditorName { get; }

    /// <summary>
    /// Raised whenever the canvas should redraw: an adjustment slider
    /// changed, the pan/zoom/rotation view transform changed, or a new
    /// document was loaded. ImageCanvas subscribes to this directly rather
    /// than relying on bitmap-level data binding, since it needs to redraw
    /// on every slider tick.
    /// </summary>
    public event EventHandler? PreviewInvalidated;

    protected void RaisePreviewInvalidated() => PreviewInvalidated?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private ImageDocument? _document;

    [ObservableProperty]
    private string? _statusMessage;

    // --- Canvas transform (pan / zoom / rotate via mouse) -----------------
    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private double _zoom = 1.0;

    partial void OnPanXChanged(double value) => RaisePreviewInvalidated();
    partial void OnPanYChanged(double value) => RaisePreviewInvalidated();
    partial void OnZoomChanged(double value) => RaisePreviewInvalidated();

    [RelayCommand]
    private void ResetView()
    {
        PanX = 0;
        PanY = 0;
        Zoom = 1.0;
    }

    // --- Adjustment sliders --------------------------------------------------
    [ObservableProperty] private double _exposure;
    [ObservableProperty] private double _contrast;
    [ObservableProperty] private double _saturation;
    [ObservableProperty] private double _whiteBalanceTemperature = 6500;
    [ObservableProperty] private double _whiteBalanceTint;
    [ObservableProperty] private double _cyanRed;
    [ObservableProperty] private double _magentaGreen;
    [ObservableProperty] private double _yellowBlue;
    [ObservableProperty] private double _clarity;
    [ObservableProperty] private double _rotationDegrees;

    partial void OnExposureChanged(double value) => PushSlidersToEditState();
    partial void OnContrastChanged(double value) => PushSlidersToEditState();
    partial void OnSaturationChanged(double value) => PushSlidersToEditState();
    partial void OnWhiteBalanceTemperatureChanged(double value) => PushSlidersToEditState();
    partial void OnWhiteBalanceTintChanged(double value) => PushSlidersToEditState();
    partial void OnCyanRedChanged(double value) => PushSlidersToEditState();
    partial void OnMagentaGreenChanged(double value) => PushSlidersToEditState();
    partial void OnYellowBlueChanged(double value) => PushSlidersToEditState();
    partial void OnClarityChanged(double value) => PushSlidersToEditState();
    partial void OnRotationDegreesChanged(double value) => PushSlidersToEditState();

    private void PushSlidersToEditState()
    {
        if (_isSyncingFromEditState || Document is null)
        {
            return;
        }

        var edit = Document.CurrentEdit;
        edit.Exposure = Exposure;
        edit.Contrast = Contrast;
        edit.Saturation = Saturation;
        edit.WhiteBalanceTemperature = WhiteBalanceTemperature;
        edit.WhiteBalanceTint = WhiteBalanceTint;
        edit.CyanRed = CyanRed;
        edit.MagentaGreen = MagentaGreen;
        edit.YellowBlue = YellowBlue;
        edit.Clarity = Clarity;
        edit.RotationDegrees = RotationDegrees;

        RaisePreviewInvalidated();
    }

    private void LoadEditStateIntoSliders(EditState state)
    {
        _isSyncingFromEditState = true;
        Exposure = state.Exposure;
        Contrast = state.Contrast;
        Saturation = state.Saturation;
        WhiteBalanceTemperature = state.WhiteBalanceTemperature;
        WhiteBalanceTint = state.WhiteBalanceTint;
        CyanRed = state.CyanRed;
        MagentaGreen = state.MagentaGreen;
        YellowBlue = state.YellowBlue;
        Clarity = state.Clarity;
        RotationDegrees = state.RotationDegrees;
        _isSyncingFromEditState = false;

        RaisePreviewInvalidated();
    }

    /// <summary>Called by a subclass once it has decoded/loaded pixel data into an ImageDocument.</summary>
    protected void LoadDocument(ImageDocument document)
    {
        Document = document;
        _history = new EditHistory(document.CurrentEdit);
        LoadEditStateIntoSliders(document.CurrentEdit);
        NotifyHistoryChanged();

        StatusMessage = document.FilePath is null
            ? "Image loaded."
            : $"Opened {System.IO.Path.GetFileName(document.FilePath)}";
    }

    // --- Undo / redo / commit (Tab commits the current edit) -----------------

    [RelayCommand]
    private void CommitEdit()
    {
        if (Document is null || _history is null)
        {
            return;
        }

        _history.Commit(Document.CurrentEdit);
        NotifyHistoryChanged();
        StatusMessage = "Edit committed.";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (Document is null || _history is null)
        {
            return;
        }

        Document.CurrentEdit = _history.Undo().Clone();
        LoadEditStateIntoSliders(Document.CurrentEdit);
        NotifyHistoryChanged();
    }

    private bool CanUndo() => _history?.CanUndo ?? false;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (Document is null || _history is null)
        {
            return;
        }

        Document.CurrentEdit = _history.Redo().Clone();
        LoadEditStateIntoSliders(Document.CurrentEdit);
        NotifyHistoryChanged();
    }

    private bool CanRedo() => _history?.CanRedo ?? false;

    private void NotifyHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ResetAdjustments()
    {
        if (Document is null)
        {
            return;
        }

        var reset = EditState.Default();
        Document.CurrentEdit = reset;
        LoadEditStateIntoSliders(reset);
    }

    // --- Universal preset / template system -----------------------------------

    public ObservableCollection<EditPreset> Presets { get; } = new();

    [ObservableProperty]
    private EditPreset? _selectedPreset;

    [ObservableProperty]
    private string _newPresetName = "New Template";

    [RelayCommand]
    private void SaveAsPreset()
    {
        if (Document is null)
        {
            return;
        }

        var preset = new EditPreset
        {
            Name = string.IsNullOrWhiteSpace(NewPresetName) ? "Untitled Template" : NewPresetName,
            SourceEditor = EditorName,
            State = Document.CurrentEdit.Clone(),
        };

        _presetStore.Save(preset);
        RefreshPresets();
        StatusMessage = $"Saved template \"{preset.Name}\".";
    }

    [RelayCommand]
    private void ApplyPreset(EditPreset? preset)
    {
        preset ??= SelectedPreset;
        if (preset is null || Document is null)
        {
            return;
        }

        Document.CurrentEdit = preset.State.Clone();
        LoadEditStateIntoSliders(Document.CurrentEdit);
        StatusMessage = $"Applied template \"{preset.Name}\".";
    }

    [RelayCommand]
    private void DeletePreset(EditPreset? preset)
    {
        preset ??= SelectedPreset;
        if (preset is null)
        {
            return;
        }

        _presetStore.Delete(preset);
        RefreshPresets();
    }

    /// <summary>
    /// Reloads the preset list from shared storage. Called on construction
    /// and after any save/delete here; call it from another editor's
    /// "refresh" action too if you want an instantly-live cross-editor list
    /// without waiting for the next save/delete round-trip.
    /// </summary>
    public void RefreshPresets()
    {
        Presets.Clear();
        foreach (var preset in _presetStore.LoadAll())
        {
            Presets.Add(preset);
        }
    }
}
