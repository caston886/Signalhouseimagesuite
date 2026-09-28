using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace SignalHouse.App.Services;

/// <summary>
/// Thin wrapper around Avalonia's IStorageProvider (the cross-platform
/// open/save file dialog API), reached through the desktop lifetime's main
/// window. Kept as simple static helpers rather than full dependency
/// injection since this app only ever has one top-level window.
/// </summary>
public static class FilePickerService
{
    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public static async Task<string?> OpenFileAsync(string title, IReadOnlyList<string> extensions)
    {
        var window = MainWindow;
        if (window is null)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(title) { Patterns = extensions.Select(ext => $"*{ext}").ToArray() },
            },
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public static async Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<string> extensions)
    {
        var window = MainWindow;
        if (window is null)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(title) { Patterns = extensions.Select(ext => $"*{ext}").ToArray() },
            },
        });

        return file?.Path.LocalPath;
    }
}
