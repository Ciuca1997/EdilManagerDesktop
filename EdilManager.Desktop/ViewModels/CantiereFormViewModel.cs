using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class CantiereFormViewModel(
    CantiereService service, DialogService dialogs, int id, string nome, string cup) : ObservableObject
{
    public string Cup { get; } = cup;

    // Avviso sulla cartella in archivio (es. non rinominabile): il nome nel database è comunque cambiato.
    public string? Avviso { get; private set; }

    [ObservableProperty]
    private string _nome = nome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        try
        {
            Avviso = await service.RenameAsync(id, Nome);
            dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Rinomina cantiere fallita", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => dialogs.Close(false);
}
