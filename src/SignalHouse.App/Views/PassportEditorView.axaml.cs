using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SkiaSharp;
using SignalHouse.App.ViewModels;
using SignalHouse.Core.Imaging;

namespace SignalHouse.App.Views;

public partial class PassportEditorView : UserControl
{
    public PassportEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private PassportEditorViewModel? ViewModel => DataContext as PassportEditorViewModel;

    // Quick-pick swatches for the two colors the chroma-key background
    // removal needs. A full color picker is a reasonable enhancement later;
    // these cover the overwhelming majority of real passport-photo studio
    // setups (a green or blue backdrop, replaced with a compliant white,
    // light blue, or light gray background).
    private void OnGreenKeyClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.KeyColor = ChromaKeyBackgroundRemover.CommonGreenScreen;
    }

    private void OnBlueKeyClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.KeyColor = ChromaKeyBackgroundRemover.CommonBlueScreen;
    }

    private void OnWhiteReplacementClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.ReplacementColor = SKColors.White;
    }

    private void OnLightBlueReplacementClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.ReplacementColor = new SKColor(0xCF, 0xE8, 0xF7);
    }

    private void OnLightGrayReplacementClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.ReplacementColor = new SKColor(0xE4, 0xE4, 0xE4);
    }
}
