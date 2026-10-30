using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

// Modulo dei dati dell'azienda: si mostra al primo avvio (con "Più tardi") e dalle Impostazioni (con "Annulla").
public partial class AziendaFormViewModel : ObservableObject
{
    private readonly AziendaService _service;
    private readonly DialogService _dialogs;

    public AziendaFormViewModel(AziendaService service, DialogService dialogs, AziendaDto? esistente, bool primoAvvio)
    {
        _service = service;
        _dialogs = dialogs;
        PrimoAvvio = primoAvvio;

        if (esistente is null) return;
        _ragioneSociale = esistente.RagioneSociale;
        _partitaIva = esistente.PartitaIva ?? string.Empty;
        _codiceFiscale = esistente.CodiceFiscale ?? string.Empty;
        _indirizzo = esistente.Indirizzo ?? string.Empty;
        _cap = esistente.Cap ?? string.Empty;
        _comune = esistente.Comune ?? string.Empty;
        _provincia = esistente.Provincia ?? string.Empty;
        _telefono = esistente.Telefono ?? string.Empty;
        _email = esistente.Email ?? string.Empty;
    }

    public bool PrimoAvvio { get; }
    public string Titolo => PrimoAvvio ? "Benvenuto! I dati della tua azienda" : "Dati dell'azienda";
    public string Descrizione => PrimoAvvio
        ? "Inserisci i dati della tua azienda: il nome comparirà in alto a sinistra e nell'intestazione dei fogli presenze. " +
          "Serve solo la ragione sociale, il resto è facoltativo. Potrai cambiarli quando vuoi da Impostazioni."
        : "Il nome compare in alto a sinistra e nell'intestazione dei fogli presenze.";
    public string TestoSecondario => PrimoAvvio ? "Più tardi" : "Annulla";

    [ObservableProperty] private string _ragioneSociale = string.Empty;
    [ObservableProperty] private string _partitaIva = string.Empty;
    [ObservableProperty] private string _codiceFiscale = string.Empty;
    [ObservableProperty] private string _indirizzo = string.Empty;
    [ObservableProperty] private string _cap = string.Empty;
    [ObservableProperty] private string _comune = string.Empty;
    [ObservableProperty] private string _provincia = string.Empty;
    [ObservableProperty] private string _telefono = string.Empty;
    [ObservableProperty] private string _email = string.Empty;

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
            await _service.SalvaAsync(new AziendaDto
            {
                RagioneSociale = RagioneSociale, PartitaIva = PartitaIva, CodiceFiscale = CodiceFiscale, Indirizzo = Indirizzo,
                Cap = Cap, Comune = Comune, Provincia = Provincia, Telefono = Telefono, Email = Email,
            });
            _dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio dati azienda fallito", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => _dialogs.Close(false);
}
