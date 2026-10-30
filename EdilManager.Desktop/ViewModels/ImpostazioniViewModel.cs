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
    }

    [ObservableProperty]
    private bool _archivioAttivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCartella), nameof(NoCartella))]
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
    private void ApriCartella()
    {
        try
        {
            Directory.CreateDirectory(CartellaArchivio);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{CartellaArchivio}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Apertura cartella archivio fallita", ex);
            ErrorMessage = $"Non riesco ad aprire la cartella: {ex.Message}";
        }
    }

    // Chiede la cartella e la salva solo se si riesce a scrivervi. Restituisce false se annullato o non utilizzabile.
    private bool ScegliEConferma()
    {
        var scelta = _dialogs.PickFolder("Scegli la cartella dove copiare le fatture importate");
        if (scelta is null) return false;

        try
        {
            Directory.CreateDirectory(scelta);
            var prova = Path.Combine(scelta, $".prova-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(prova, string.Empty);
            File.Delete(prova);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLog.Error($"Cartella archivio non utilizzabile: {scelta}", ex);
            ErrorMessage = $"Non riesco a scrivere in questa cartella: {ex.Message}";
            return false;
        }

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
