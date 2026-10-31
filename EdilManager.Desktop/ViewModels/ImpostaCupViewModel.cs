using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

// Dà il CUP vero a un cantiere creato senza CUP.
public partial class ImpostaCupViewModel(CantiereService service, DialogService dialogs, int id, string nomeCantiere) : ObservableObject
{
    public string Nome { get; } = nomeCantiere;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _cup = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        try
        {
            await service.ImpostaCupAsync(id, Cup);
            dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Impostazione del CUP fallita", ex);
            ErrorMessage = ex.Message;
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Cup);

    [RelayCommand]
    private void Cancel() => dialogs.Close(false);
}
