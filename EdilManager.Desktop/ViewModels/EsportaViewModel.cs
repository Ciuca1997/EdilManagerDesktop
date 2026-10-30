using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed record MeseOption(int Anno, int Mese)
{
    public string Testo => $"{Formats.NomeMese(Mese)} {Anno}";
    public DateOnly Primo => new(Anno, Mese, 1);
    public DateOnly Ultimo => new(Anno, Mese, DateTime.DaysInMonth(Anno, Mese));

    // Gli ultimi mesi fino a quello corrente compreso, dal più recente.
    public static List<MeseOption> Ultimi(int quanti)
    {
        var oggi = DateTime.Today;
        return Enumerable.Range(0, quanti).Select(i => oggi.AddMonths(-i)).Select(d => new MeseOption(d.Year, d.Month)).ToList();
    }
}

// Finestra di esportazione di un foglio presenze (PDF o CSV). Le opzioni mostrate dipendono da cosa si esporta:
// il cantiere per il foglio del mese, l'intervallo di mesi per quello di un dipendente.
public partial class EsportaViewModel : ObservableObject
{
    private readonly DialogService _dialogs;
    private readonly Func<EsportaViewModel, Task<byte[]>> _crea;
    private readonly Func<EsportaViewModel, string> _nomeSuggerito;

    private EsportaViewModel(DialogService dialogs, string titolo, string descrizione,
        Func<EsportaViewModel, Task<byte[]>> crea, Func<EsportaViewModel, string> nomeSuggerito)
    {
        _dialogs = dialogs;
        Titolo = titolo;
        Descrizione = descrizione;
        _crea = crea;
        _nomeSuggerito = nomeSuggerito;
    }

    // Foglio del mese per tutti i dipendenti, eventualmente solo per un cantiere.
    public static EsportaViewModel PerMese(DialogService dialogs, PresenzaService presenze, MeseOption mese,
        IReadOnlyList<CantiereOption> cantieri)
    {
        var vm = new EsportaViewModel(dialogs, "Esporta presenze del mese",
            $"Foglio presenze di {mese.Testo} per tutti i dipendenti.",
            v => presenze.EsportaMeseAsync(mese.Anno, mese.Mese, v.Cantiere?.Id, v.Formato),
            v => $"Presenze {mese.Anno}-{mese.Mese:00}" + (v.Cantiere?.Id is null ? string.Empty : $" - {v.Cantiere.Nome}"))
        {
            MostraCantiere = true,
            Cantieri = [new CantiereOption(null, "Tutti i cantieri"), .. cantieri.Where(c => c.Id is not null)],
        };
        vm.Cantiere = vm.Cantieri[0];
        return vm;
    }

    // Foglio di un dipendente, giorno per giorno, da un mese a un altro.
    public static EsportaViewModel PerDipendente(DialogService dialogs, PresenzaService presenze, int dipendenteId,
        string nome, MeseOption meseIniziale, DateOnly assunzione)
    {
        // Solo i mesi dall'assunzione in poi (se è futura, resta almeno il mese corrente).
        var mesi = MeseOption.Ultimi(36).Where(m => m.Ultimo >= assunzione).DefaultIfEmpty(MeseOption.Ultimi(1)[0]).ToList();
        var vm = new EsportaViewModel(dialogs, "Esporta presenze del dipendente",
            $"Foglio presenze di {nome}, con il cantiere di ogni giorno e i totali.",
            v => presenze.EsportaDipendenteAsync(dipendenteId, v.Da!.Primo, v.A!.Ultimo, v.Formato),
            v => $"Presenze {nome} {v.Da!.Anno}-{v.Da.Mese:00}" + (v.Da == v.A ? string.Empty : $" - {v.A!.Anno}-{v.A.Mese:00}"))
        {
            MostraPeriodo = true,
            Mesi = mesi,
        };
        vm.Da = mesi.FirstOrDefault(m => m == meseIniziale) ?? mesi[0];
        vm.A = vm.Da;
        return vm;
    }

    public string Titolo { get; }
    public string Descrizione { get; }

    public bool MostraCantiere { get; private init; }
    public IReadOnlyList<CantiereOption> Cantieri { get; private init; } = [];

    [ObservableProperty]
    private CantiereOption? _cantiere;

    public bool MostraPeriodo { get; private init; }
    public IReadOnlyList<MeseOption> Mesi { get; private init; } = [];

    [ObservableProperty]
    private MeseOption? _da;

    [ObservableProperty]
    private MeseOption? _a;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCsv))]
    private bool _isPdf = true;

    public bool IsCsv
    {
        get => !IsPdf;
        set => IsPdf = !value;
    }

    public FormatoExport Formato => IsPdf ? FormatoExport.Pdf : FormatoExport.Csv;

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
        if (MostraPeriodo)
        {
            if (Da is null || A is null) { ErrorMessage = "Scegli il periodo."; return; }
            if (A.Primo < Da.Primo) { ErrorMessage = "Il mese finale è precedente a quello iniziale."; return; }
        }

        var estensione = IsPdf ? "pdf" : "csv";
        var percorso = _dialogs.PickSaveFile(
            IsPdf ? "Documento PDF (*.pdf)|*.pdf" : "File CSV per Excel (*.csv)|*.csv",
            "Salva il foglio presenze", PulisciNome(_nomeSuggerito(this)) + "." + estensione);
        if (percorso is null) return;

        IsBusy = true;
        try
        {
            var dati = await _crea(this);
            await File.WriteAllBytesAsync(percorso, dati);
            AppLog.Trace($"ESPORTAZIONE presenze: {percorso} ({dati.Length} byte)");
            _dialogs.Close(percorso);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tipicamente il file è già aperto in un altro programma.
            AppLog.Error($"Salvataggio del foglio presenze fallito: {percorso}", ex);
            ErrorMessage = $"Non riesco a salvare il file (è aperto in un altro programma?): {ex.Message}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Esportazione presenze fallita", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _dialogs.Close(null);

    private static string PulisciNome(string nome) =>
        string.Concat(nome.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
}
