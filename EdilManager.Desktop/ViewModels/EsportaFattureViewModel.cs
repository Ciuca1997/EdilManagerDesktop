using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed record DivisioneOption(string Testo, DivisioneExport Valore);

// Esporta in una cartella i file delle fatture di un periodo (per data della fattura), divisi per cantiere e/o fornitore.
// Il dialogo si chiude con l'EsitoEsportazioneFatture; null = annullato.
public partial class EsportaFattureViewModel : ObservableObject
{
    private readonly DialogService _dialogs;
    private readonly EsportaFattureService _service;
    private readonly string? _cartellaIniziale;

    public EsportaFattureViewModel(DialogService dialogs, EsportaFattureService service, ImpostazioniService impostazioni,
        IReadOnlyList<int> anni, int annoPredefinito)
    {
        _dialogs = dialogs;
        _service = service;
        var corrente = impostazioni.Corrente;
        _cartellaIniziale = corrente.CartellaEsportazione;
        ArchivioPronto = corrente.CartellaArchivio is { Length: > 0 } c && Directory.Exists(c);

        Anni = anni;
        _anno = annoPredefinito;
        var oggi = DateTime.Today;
        _dal = new DateTime(oggi.Year, 1, 1);
        _al = oggi;
        Divisioni = DivisioniDisponibili;
        _divisione = Divisioni[3];
    }

    private static readonly IReadOnlyList<DivisioneOption> DivisioniDisponibili =
    [
        new("Tutte nella stessa cartella", DivisioneExport.Nessuna),
        new("Una cartella per cantiere", DivisioneExport.PerCantiere),
        new("Una cartella per fornitore", DivisioneExport.PerFornitore),
        new("Per cantiere e, dentro, per fornitore", DivisioneExport.PerCantiereEFornitore),
    ];

    // Senza archivio fatture i file originali non ci sono: si esporta solo il riepilogo.
    public bool ArchivioPronto { get; }
    public bool ArchivioNonPronto => !ArchivioPronto;

    public IReadOnlyList<int> Anni { get; }

    [ObservableProperty]
    private int _anno;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsaPeriodo))]
    private bool _usaAnno = true;

    public bool UsaPeriodo
    {
        get => !UsaAnno;
        set => UsaAnno = !value;
    }

    [ObservableProperty]
    private DateTime? _dal;

    [ObservableProperty]
    private DateTime? _al;

    public IReadOnlyList<DivisioneOption> Divisioni { get; }

    [ObservableProperty]
    private DivisioneOption? _divisione;

    [ObservableProperty]
    private bool _riepilogoCsv = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task EsportaAsync()
    {
        ErrorMessage = null;
        DateOnly da, a;
        if (UsaAnno)
        {
            (da, a) = (new DateOnly(Anno, 1, 1), new DateOnly(Anno, 12, 31));
        }
        else
        {
            if (Dal is null || Al is null) { ErrorMessage = "Scegli la data iniziale e quella finale."; return; }
            (da, a) = (DateOnly.FromDateTime(Dal.Value), DateOnly.FromDateTime(Al.Value));
            if (a < da) { ErrorMessage = "La data finale è precedente a quella iniziale."; return; }
        }
        if (!ArchivioPronto && !RiepilogoCsv)
        {
            ErrorMessage = "L'archivio fatture non è impostato, quindi non ci sono file da copiare: lascia attivo il riepilogo CSV.";
            return;
        }

        var cartella = _dialogs.PickFolder("Scegli la cartella dove esportare le fatture", _cartellaIniziale);
        if (cartella is null) return;

        IsBusy = true;
        try
        {
            var esito = await _service.EsportaAsync(da, a, cartella, (Divisione ?? Divisioni[0]).Valore, RiepilogoCsv);
            AppLog.Trace($"ESPORTAZIONE fatture {da:dd/MM/yyyy}-{a:dd/MM/yyyy}: {esito.Fatture} fatture, {esito.FileCopiati} file in {esito.Cartella}");
            if (esito.Avviso is not null) AppLog.Error($"ESPORTAZIONE fatture: {esito.Avviso}");
            _dialogs.Close(esito);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Esportazione delle fatture fallita", ex);
            ErrorMessage = $"Non riesco a scrivere nella cartella (un file è aperto in un altro programma?): {ex.Message}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Esportazione delle fatture fallita", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _dialogs.Close(null);
}
