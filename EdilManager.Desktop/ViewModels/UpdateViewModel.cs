using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using Velopack;

namespace EdilManager.Desktop.ViewModels;

public partial class UpdateViewModel(DialogService dialogs, UpdateManager manager, UpdateInfo update) : ObservableObject
{
    public string Versione { get; } = update.TargetFullRelease.Version.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChoose))]
    private bool _isDownloading;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string? _errorMessage;

    public bool CanChoose => !IsDownloading;

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        ErrorMessage = null;
        IsDownloading = true;
        try
        {
            await manager.DownloadUpdatesAsync(update, p => Application.Current.Dispatcher.Invoke(() => Progress = p));
            manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            AppLog.Error("Aggiornamento fallito", ex);
            ErrorMessage = $"Aggiornamento non riuscito: {ex.Message}";
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void Later() => dialogs.Close(false);
}
