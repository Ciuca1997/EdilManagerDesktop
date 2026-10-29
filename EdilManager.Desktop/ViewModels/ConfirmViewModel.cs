using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class ConfirmViewModel(
    DialogService dialogs, string title, string message, string confirmText, string? cancelText, bool isDestructive)
    : ObservableObject
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public string CancelText { get; } = cancelText ?? string.Empty;
    public bool ShowCancel { get; } = cancelText is not null;
    public bool IsDestructive { get; } = isDestructive;

    [RelayCommand]
    private void Confirm() => dialogs.Close(true);

    [RelayCommand]
    private void Cancel() => dialogs.Close(false);
}
