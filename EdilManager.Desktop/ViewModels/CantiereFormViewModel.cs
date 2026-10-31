using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

// id nullo = nuovo cantiere senza CUP (cup nullo: non c'è nulla da mostrare); altrimenti rinomina del cantiere esistente.
public partial class CantiereFormViewModel(
    CantiereService service, DialogService dialogs, int? id, string nome, string? cup) : ObservableObject
{
    public string Titolo => id is null ? "Nuovo cantiere" : "Rinomina cantiere";
    public string? Descrizione => id is null
        ? "Per un lavoro che non ha un CUP. Potrai assegnargli le fatture a mano e, se arriva, aggiungere il CUP in seguito."
        : null;
    public bool HasDescrizione => Descrizione is not null;
    public string Cup { get; } = cup ?? string.Empty;
    public bool HasCup => cup is not null;

    // Avviso sulla cartella in archivio (es. non rinominabile): il nome nel database è comunque cambiato.
    public string? Avviso { get; private set; }

    // Id del cantiere appena creato (solo in modalità «nuovo»).
    public int? CantiereCreatoId { get; private set; }

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
            if (id is { } esistente) Avviso = await service.RenameAsync(esistente, Nome);
            else CantiereCreatoId = await service.CreaInternoAsync(Nome);
            dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error(id is null ? "Creazione cantiere fallita" : "Rinomina cantiere fallita", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => dialogs.Close(false);
}
