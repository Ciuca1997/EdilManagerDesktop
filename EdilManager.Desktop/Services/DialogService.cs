using EdilManager.Desktop.ViewModels;
using MaterialDesignThemes.Wpf;

namespace EdilManager.Desktop.Services;

public class DialogService
{
    public const string Identifier = "RootDialog";

    public Task<object?> ShowAsync(object viewModel) => DialogHost.Show(viewModel, Identifier);

    public void Close(object? result = null) => DialogHost.Close(Identifier, result);

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Conferma", bool destructive = false)
    {
        var vm = new ConfirmViewModel(this, title, message, confirmText, "Annulla", destructive);
        return await ShowAsync(vm) is true;
    }

    public Task MessageAsync(string title, string message) =>
        ShowAsync(new ConfirmViewModel(this, title, message, "OK", null, false));
}
