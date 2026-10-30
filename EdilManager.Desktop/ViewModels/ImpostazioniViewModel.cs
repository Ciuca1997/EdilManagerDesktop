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

    public ImpostazioniViewModel(ImpostazioniService impostazioni, DialogService dialogs)
    {
        _impostazioni = impostazioni;
        _dialogs = dialogs;
        var corrente = impostazioni.Corrente;
        _archivioAttivo = corrente.ArchivioAttivo;
        _cartellaArchivio = corrente.CartellaArchivio ?? string.Empty;
        _cartellaImportazione = corrente.CartellaImportazione ?? string.Empty;
        _cartellaEsportazione = corrente.CartellaEsportazione ?? string.Empty;
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
