using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

// Dopo un'importazione: un CUP mai visto ha creato un cantiere con il CUP come nome. Mostra la fattura in cui è stato trovato
// e lascia scegliere il nome (la cartella dell'archivio fatture prende il nome scelto).
// Risultato del dialogo: true = nominato, false = «Più tardi», "salta" = «Salta tutti».
public partial class NuovoCantiereViewModel(
    CantiereService service, DialogService dialogs, NuovoCantiere cantiere, int posizione, int totale) : ObservableObject
{
    public string Titolo => totale > 1 ? $"Nuovo cantiere ({posizione} di {totale})" : "Nuovo cantiere";
    public string Cup { get; } = cantiere.Cup;
    public bool PiuDiUno => totale > 1;

    public string Fornitore { get; } = cantiere.Fornitore;
    public string Fattura { get; } = $"n. {cantiere.Numero} del {cantiere.Data:dd/MM/yyyy}";
    public string Totale { get; } = cantiere.Totale.ToString("N2", Formats.Italian) + " €";
    public string Descrizione { get; } = Abbrevia(cantiere.Descrizione);

    // Avviso sulla cartella in archivio (es. non rinominabile): il nome nel database è comunque cambiato.
    public string? Avviso { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _nome = string.Empty;

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
            Avviso = await service.RenameAsync(cantiere.Id, Nome);
            dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Nome del nuovo cantiere non salvato", ex);
            ErrorMessage = ex.Message;
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Nome);

    [RelayCommand]
    private void Later() => dialogs.Close(false);

    [RelayCommand]
    private void SkipAll() => dialogs.Close("salta");

    private static string Abbrevia(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return string.Empty;
        testo = string.Join(' ', testo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return testo.Length > 160 ? testo[..160] + "…" : testo;
    }
}
