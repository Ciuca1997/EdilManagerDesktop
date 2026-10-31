using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class ImpostazioniViewModel : ObservableObject
{
    private readonly ImpostazioniService _impostazioni;
    private readonly DialogService _dialogs;
    private readonly AziendaService _azienda;

    public ImpostazioniViewModel(ImpostazioniService impostazioni, DialogService dialogs, AziendaService azienda)
    {
        _impostazioni = impostazioni;
        _dialogs = dialogs;
        _azienda = azienda;
        _ = CaricaAziendaAsync();
        var corrente = impostazioni.Corrente;
        _archivioAttivo = corrente.ArchivioAttivo;
        _cartellaArchivio = corrente.CartellaArchivio ?? string.Empty;
        _cartellaImportazione = corrente.CartellaImportazione ?? string.Empty;
        _cartellaEsportazione = corrente.CartellaEsportazione ?? string.Empty;
        _giorniScadenza = corrente.GiorniScadenzaPredefiniti.ToString();
    }

    // --- Dati dell'azienda

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAzienda), nameof(NoAzienda), nameof(TestoPulsanteAzienda))]
    private string _aziendaNome = string.Empty;

    [ObservableProperty]
    private string _aziendaDettagli = string.Empty;

    public bool HasAzienda => AziendaNome.Length > 0;
    public bool NoAzienda => !HasAzienda;
    public string TestoPulsanteAzienda => HasAzienda ? "Modifica dati" : "Inserisci i dati";

    private async Task CaricaAziendaAsync()
    {
        try
        {
            var a = await _azienda.GetAsync();
            AziendaNome = a?.RagioneSociale ?? string.Empty;
            AziendaDettagli = a is null ? string.Empty : string.Join("\n", new[]
            {
                Unisci(" · ", a.PartitaIva is { Length: > 0 } ? $"P.IVA {a.PartitaIva}" : null,
                    a.CodiceFiscale is { Length: > 0 } ? $"C.F. {a.CodiceFiscale}" : null),
                Unisci(", ", a.Indirizzo, Unisci(" ", a.Cap, a.Comune, a.Provincia is { Length: > 0 } ? $"({a.Provincia})" : null)),
                Unisci(" · ", a.Telefono, a.Email),
            }.Where(r => r.Length > 0));
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento dati azienda fallito", ex);
        }
    }

    private static string Unisci(string separatore, params string?[] parti) =>
        string.Join(separatore, parti.Where(p => !string.IsNullOrWhiteSpace(p)));

    [RelayCommand]
    private async Task ModificaAziendaAsync()
    {
        try
        {
            var form = new AziendaFormViewModel(_azienda, _dialogs, await _azienda.GetAsync(), primoAvvio: false);
            if (await _dialogs.ShowAsync(form) is true) await CaricaAziendaAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Modifica dati azienda fallita", ex);
            ErrorMessage = ex.Message;
        }
    }

    // --- Archivio fatture

    [ObservableProperty]
    private bool _archivioAttivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCartella), nameof(NoCartella), nameof(AvvisoSovrapposizione), nameof(HasAvvisoSovrapposizione))]
    private string _cartellaArchivio;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasCartella => CartellaArchivio.Length > 0;
    public bool NoCartella => !HasCartella;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Il parametro è il nuovo stato dell'interruttore dopo il clic.
    [RelayCommand]
    private void ToggleArchivio(bool attivo)
    {
        ErrorMessage = null;
        if (attivo && !HasCartella && !ScegliEConferma())
        {
            // Nessuna cartella scelta: l'interruttore torna spento (va notificato perché il valore non cambia).
            OnPropertyChanged(nameof(ArchivioAttivo));
            return;
        }

        Salva(attivo, CartellaArchivio);
    }

    [RelayCommand]
    private void ScegliCartella()
    {
        ErrorMessage = null;
        ScegliEConferma();
    }

    [RelayCommand]
    private void ApriCartella() => ApriInEsplora(CartellaArchivio);

    // --- Cartella predefinita per l'importazione delle fatture

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCartellaImportazione), nameof(NoCartellaImportazione), nameof(AvvisoSovrapposizione), nameof(HasAvvisoSovrapposizione))]
    private string _cartellaImportazione;

    public bool HasCartellaImportazione => CartellaImportazione.Length > 0;
    public bool NoCartellaImportazione => !HasCartellaImportazione;

    [RelayCommand]
    private void ScegliCartellaImportazione() => ScegliCartellaPredefinita(
        "Scegli la cartella da cui importare le fatture", CartellaImportazione, scrivibile: false,
        nuova => { _impostazioni.Modifica(i => i.CartellaImportazione = nuova); CartellaImportazione = nuova ?? string.Empty; });

    [RelayCommand]
    private void TogliCartellaImportazione() => Applica(() =>
    {
        _impostazioni.Modifica(i => i.CartellaImportazione = null);
        CartellaImportazione = string.Empty;
    });

    [RelayCommand]
    private void ApriCartellaImportazione() => ApriInEsplora(CartellaImportazione);

    // --- Cartella predefinita per le esportazioni

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCartellaEsportazione), nameof(NoCartellaEsportazione))]
    private string _cartellaEsportazione;

    public bool HasCartellaEsportazione => CartellaEsportazione.Length > 0;
    public bool NoCartellaEsportazione => !HasCartellaEsportazione;

    [RelayCommand]
    private void ScegliCartellaEsportazione() => ScegliCartellaPredefinita(
        "Scegli la cartella dove salvare le esportazioni", CartellaEsportazione, scrivibile: true,
        nuova => { _impostazioni.Modifica(i => i.CartellaEsportazione = nuova); CartellaEsportazione = nuova ?? string.Empty; });

    [RelayCommand]
    private void TogliCartellaEsportazione() => Applica(() =>
    {
        _impostazioni.Modifica(i => i.CartellaEsportazione = null);
        CartellaEsportazione = string.Empty;
    });

    [RelayCommand]
    private void ApriCartellaEsportazione() => ApriInEsplora(CartellaEsportazione);

    // Se la cartella di importazione e quella dell'archivio coincidono o una contiene l'altra, l'archivio perde senso:
    // i file già al suo interno non vengono ricopiati e ogni importazione rilegge anche l'archivio.
    public string? AvvisoSovrapposizione => Sovrapposte(CartellaImportazione, CartellaArchivio)
        ? "La cartella di importazione e quella dell'archivio fatture coincidono, o una contiene l'altra: i file già nell'archivio non " +
          "vengono copiati di nuovo e ogni importazione rilegge anche l'archivio. Conviene tenerle separate."
        : null;

    public bool HasAvvisoSovrapposizione => AvvisoSovrapposizione is not null;

    private static bool Sovrapposte(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return false;
        try
        {
            var x = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var y = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var sep = Path.DirectorySeparatorChar;
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase)
                   || x.StartsWith(y + sep, StringComparison.OrdinalIgnoreCase)
                   || y.StartsWith(x + sep, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // --- Scadenza presunta delle fatture

    [ObservableProperty]
    private string _giorniScadenza;

    // Si salva appena il valore è valido; se non lo è, l'errore compare e resta salvato l'ultimo valore buono.
    partial void OnGiorniScadenzaChanged(string value)
    {
        if (!int.TryParse(value.Trim(), out var giorni) || giorni is < 0 or > 365)
        {
            ErrorMessage = "I giorni di scadenza devono essere un numero intero da 0 a 365.";
            return;
        }
        Applica(() =>
        {
            _impostazioni.Modifica(i => i.GiorniScadenzaPredefiniti = giorni);
            AppLog.Trace($"IMPOSTAZIONI giorni di scadenza predefiniti: {giorni}");
        });
    }

    // --- Comuni

    private void ScegliCartellaPredefinita(string titolo, string iniziale, bool scrivibile, Action<string?> salva)
    {
        ErrorMessage = null;
        var scelta = _dialogs.PickFolder(titolo, iniziale);
        if (scelta is null) return;

        if (scrivibile && !ProvaScrittura(scelta)) return;
        Applica(() =>
        {
            salva(scelta);
            AppLog.Trace($"IMPOSTAZIONI {titolo}: {scelta}");
        });
    }

    private void Applica(Action azione)
    {
        ErrorMessage = null;
        try
        {
            azione();
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio impostazioni fallito", ex);
            ErrorMessage = $"Non riesco a salvare le impostazioni: {ex.Message}";
        }
    }

    private void ApriInEsplora(string percorso)
    {
        try
        {
            Directory.CreateDirectory(percorso);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{percorso}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Apertura cartella fallita", ex);
            ErrorMessage = $"Non riesco ad aprire la cartella: {ex.Message}";
        }
    }

    // Verifica che si possa scrivere nella cartella; altrimenti mostra l'errore e restituisce false.
    private bool ProvaScrittura(string cartella)
    {
        try
        {
            Directory.CreateDirectory(cartella);
            var prova = Path.Combine(cartella, $".prova-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(prova, string.Empty);
            File.Delete(prova);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLog.Error($"Cartella non utilizzabile: {cartella}", ex);
            ErrorMessage = $"Non riesco a scrivere in questa cartella: {ex.Message}";
            return false;
        }
    }

    // Chiede la cartella dell'archivio e la salva solo se si riesce a scrivervi. Restituisce false se annullato o non utilizzabile.
    private bool ScegliEConferma()
    {
        var scelta = _dialogs.PickFolder("Scegli la cartella dove copiare le fatture importate");
        if (scelta is null) return false;
        if (!ProvaScrittura(scelta)) return false;

        // Scegliere la cartella dall'apposito pulsante attiva l'archivio solo se era già acceso o si sta accendendo ora.
        Salva(ArchivioAttivo || !HasCartella, scelta);
        return true;
    }

    private void Salva(bool attivo, string cartella)
    {
        try
        {
            _impostazioni.Modifica(i =>
            {
                i.ArchivioAttivo = attivo;
                i.CartellaArchivio = cartella.Length > 0 ? cartella : null;
            });
            ArchivioAttivo = attivo;
            CartellaArchivio = cartella;
            AppLog.Trace($"IMPOSTAZIONI archivio fatture: {(attivo ? "attivo" : "disattivato")}, cartella \"{cartella}\"");
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio impostazioni fallito", ex);
            ErrorMessage = $"Non riesco a salvare le impostazioni: {ex.Message}";
            OnPropertyChanged(nameof(ArchivioAttivo));
        }
    }
}
