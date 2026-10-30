using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

// Una cella della griglia: un dipendente in un giorno. Mostra le ore del tipo selezionato (ordinarie o straordinarie)
// sommate su tutte le presenze di quel giorno; la modifica del testo salva le ore (solo se c'è al massimo una presenza).
public sealed class CellaGiorno(PresenzeViewModel griglia, RigaPresenze riga, DateOnly data) : ObservableObject
{
    private static readonly Brush SfondoWeekend = Congela(Color.FromArgb(0x22, 0x80, 0x80, 0x80));
    private static readonly Brush SfondoOre = Congela(Color.FromArgb(0x33, 0x21, 0x96, 0xF3));
    private static readonly Brush SfondoPiuCantieri = Congela(Color.FromArgb(0x66, 0xFF, 0xB3, 0x00));
    private static readonly Brush SfondoNonAssunto = Congela(Color.FromArgb(0x55, 0x50, 0x50, 0x50));

    private string _testo = string.Empty;

    public RigaPresenze Riga { get; } = riga;
    public DateOnly Data { get; } = data;
    public IReadOnlyList<PresenzaDto> Presenze { get; private set; } = [];
    public Brush Sfondo { get; private set; } = Brushes.Transparent;
    public string? Suggerimento { get; private set; }

    public bool IsWeekend => Data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    // Giorno precedente all'assunzione: non ci si possono registrare ore (quelle eventualmente già presenti restano visibili).
    public bool NonModificabile => Data < Riga.DataAssunzione && Presenze.Count == 0;

    public string Testo
    {
        get => _testo;
        set
        {
            if (_testo == value) return;
            _testo = value;
            OnPropertyChanged();
            griglia.CellaModificata(this, value);
        }
    }

    public void Aggiorna(IReadOnlyList<PresenzaDto> presenze, TipoOre modalita)
    {
        Presenze = presenze;
        var ore = presenze.Sum(p => modalita == TipoOre.Ordinarie ? p.OreOrdinarie : p.OreStatordinarie);
        _testo = ore == 0 ? string.Empty : ore.ToString("0.##", Formats.Italian);

        var haOre = presenze.Any(p => p.OreOrdinarie + p.OreStatordinarie > 0);
        Sfondo = presenze.Count > 1 ? SfondoPiuCantieri
            : haOre ? SfondoOre
            : NonModificabile ? SfondoNonAssunto
            : IsWeekend ? SfondoWeekend : Brushes.Transparent;
        Suggerimento = presenze.Count == 0
            ? NonModificabile ? $"Prima dell'assunzione ({Riga.DataAssunzione.ToString("dd/MM/yyyy", Formats.Italian)})" : null
            : string.Join("\n", presenze.Select(p =>
                $"{p.Cantiere ?? "Nessun cantiere"}: {p.OreOrdinarie.ToString("0.##", Formats.Italian)} ord." +
                (p.OreStatordinarie > 0 ? $" + {p.OreStatordinarie.ToString("0.##", Formats.Italian)} straord." : string.Empty)));

        OnPropertyChanged(nameof(Testo));
        OnPropertyChanged(nameof(Sfondo));
        OnPropertyChanged(nameof(Suggerimento));
    }

    private static Brush Congela(Color colore)
    {
        var brush = new SolidColorBrush(colore);
        brush.Freeze();
        return brush;
    }
}

public sealed partial class RigaPresenze(DipendenteListDto dipendente) : ObservableObject
{
    public int DipendenteId { get; } = dipendente.Id;
    public string Nome { get; } = $"{dipendente.Cognome} {dipendente.Nome}";
    public DateOnly DataAssunzione { get; } = dipendente.DataAssunzione;
    public IReadOnlyList<CellaGiorno> Giorni { get; set; } = [];

    [ObservableProperty]
    private string _totOrd = string.Empty;

    [ObservableProperty]
    private string _totStr = string.Empty;
}

public partial class PresenzeViewModel : ObservableObject, IReloadable
{
    private readonly DipendenteService _dipendenti;
    private readonly PresenzaService _presenze;
    private readonly CantiereService _cantieri;
    private readonly DialogService _dialogs;
    private readonly SemaphoreSlim _salvataggio = new(1, 1);
    private Dictionary<(int, DateOnly), List<PresenzaDto>> _perCella = [];
    private int _versione;

    public PresenzeViewModel(DipendenteService dipendenti, PresenzaService presenze, CantiereService cantieri, DialogService dialogs)
    {
        _dipendenti = dipendenti;
        _presenze = presenze;
        _cantieri = cantieri;
        _dialogs = dialogs;
        _periodo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _ = CaricaAsync(ricostruisci: true);
    }

    public ObservableCollection<RigaPresenze> Righe { get; } = [];
    public IReadOnlyList<CantiereOption> Cantieri { get; private set; } = [CantiereOption.Nessuno];

    // Il numero di colonne dipende dal mese: la vista ricostruisce le colonne quando viene sollevato.
    public event EventHandler? StrutturaCambiata;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodoText))]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand))]
    private DateTime _periodo;

    public string PeriodoText => $"{Formats.NomeMese(Periodo.Month)} {Periodo.Year}";
    public int Anno => Periodo.Year;
    public int Mese => Periodo.Month;
    public int GiorniNelMese => DateTime.DaysInMonth(Periodo.Year, Periodo.Month);

    // Ore mostrate e modificate dalla griglia: ordinarie o straordinarie.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrdinarie), nameof(IsStraordinarie))]
    private TipoOre _modalita = TipoOre.Ordinarie;

    public bool IsOrdinarie
    {
        get => Modalita == TipoOre.Ordinarie;
        set { if (value) Modalita = TipoOre.Ordinarie; }
    }

    public bool IsStraordinarie
    {
        get => Modalita == TipoOre.Straordinarie;
        set { if (value) Modalita = TipoOre.Straordinarie; }
    }

    // Cantiere assegnato alle ore inserite in celle ancora vuote.
    [ObservableProperty]
    private CantiereOption? _cantiereNuove = CantiereOption.Nessuno;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => !IsLoading && !HasError && Righe.Count == 0;

    partial void OnPeriodoChanged(DateTime value) => _ = CaricaAsync(ricostruisci: true);

    partial void OnModalitaChanged(TipoOre value)
    {
        foreach (var riga in Righe) AggiornaRiga(riga);
    }

    public Task ReloadAsync() => CaricaAsync(ricostruisci: true);

    [RelayCommand]
    private void PreviousMonth() => Periodo = Periodo.AddMonths(-1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextMonth() => Periodo = Periodo.AddMonths(1);

    private bool CanGoNext() => Periodo < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [RelayCommand]
    private async Task EsportaAsync()
    {
        var form = EsportaViewModel.PerMese(_dialogs, _presenze, new MeseOption(Anno, Mese), Cantieri);
        if (await _dialogs.ShowAsync(form) is string percorso) await _dialogs.MostraEsportazioneAsync(percorso);
    }

    // ricostruisci = true: ricarica anche dipendenti e cantieri e ricrea righe e colonne; false: aggiorna solo le ore nelle celle
    // esistenti (dopo una modifica), così selezione e posizione di scorrimento restano dove sono.
    private async Task CaricaAsync(bool ricostruisci)
    {
        var versione = ++_versione;
        IsLoading = ricostruisci;
        ErrorMessage = null;
        try
        {
            var presenze = await _presenze.GetMeseAsync(Anno, Mese);
            List<DipendenteListDto>? dipendenti = null;
            IReadOnlyList<CantiereOption>? cantieri = null;
            if (ricostruisci)
            {
                var ultimo = new DateOnly(Anno, Mese, GiorniNelMese);
                dipendenti = (await _dipendenti.GetAllAsync()).Where(d => d.DataAssunzione <= ultimo)
                    .OrderBy(d => d.Cognome, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(d => d.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
                cantieri = CantiereOption.Elenco(await _cantieri.GetAllAsync());
            }
            if (versione != _versione) return;

            _perCella = presenze.GroupBy(p => (p.DipendenteId, p.Data)).ToDictionary(g => g.Key, g => g.ToList());

            if (ricostruisci)
            {
                var scelto = CantiereNuove?.Id;
                Cantieri = cantieri!;
                OnPropertyChanged(nameof(Cantieri));
                CantiereNuove = Cantieri.FirstOrDefault(c => c.Id == scelto) ?? CantiereOption.Nessuno;

                Righe.Clear();
                foreach (var d in dipendenti!)
                {
                    var riga = new RigaPresenze(d);
                    riga.Giorni = Enumerable.Range(1, GiorniNelMese)
                        .Select(g => new CellaGiorno(this, riga, new DateOnly(Anno, Mese, g))).ToList();
                    Righe.Add(riga);
                }
                StrutturaCambiata?.Invoke(this, EventArgs.Empty);
            }

            foreach (var riga in Righe) AggiornaRiga(riga);
        }
        catch (Exception ex)
        {
            if (versione != _versione) return;
            AppLog.Error("Caricamento presenze fallito", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (versione == _versione)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    private void AggiornaRiga(RigaPresenze riga)
    {
        decimal ord = 0, str = 0;
        foreach (var cella in riga.Giorni)
        {
            var presenze = _perCella.TryGetValue((riga.DipendenteId, cella.Data), out var l) ? l : [];
            cella.Aggiorna(presenze, Modalita);
            ord += presenze.Sum(p => p.OreOrdinarie);
            str += presenze.Sum(p => p.OreStatordinarie);
        }
        riga.TotOrd = ord == 0 ? string.Empty : ord.ToString("0.##", Formats.Italian);
        riga.TotStr = str == 0 ? string.Empty : str.ToString("0.##", Formats.Italian);
    }

    // Chiamato dalla cella quando l'utente conferma un valore: i salvataggi vanno uno alla volta, nell'ordine.
    internal void CellaModificata(CellaGiorno cella, string testo) => _ = SalvaCellaAsync(cella, testo);

    private async Task SalvaCellaAsync(CellaGiorno cella, string testo)
    {
        string? errore = null;
        await _salvataggio.WaitAsync();
        try
        {
            if (!TryOre(testo, out var ore))
                errore = "Le ore devono essere un numero maggiore o uguale a zero (es. 8 oppure 7,5).";
            else
                await _presenze.ImpostaOreGiornoAsync(cella.Riga.DipendenteId, cella.Data, Modalita, ore, CantiereNuove?.Id);
        }
        catch (InvalidOperationException ex)
        {
            errore = ex.Message;
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio ore fallito", ex);
            errore = ex.Message;
        }
        finally
        {
            // In ogni caso la cella torna a mostrare ciò che c'è davvero nel database.
            await RicaricaOreAsync();
            _salvataggio.Release();
        }

        if (errore is not null) await MostraAsync("Ore non salvate", errore);
    }

    // Assegna il cantiere alle celle selezionate (solo quelle con una presenza sola).
    public async Task AssegnaCantiereAsync(IReadOnlyList<CellaGiorno> celle, CantiereOption cantiere)
    {
        if (celle.Count == 0) return;
        string? avviso = null;
        await _salvataggio.WaitAsync();
        try
        {
            var esito = await _presenze.ImpostaCantiereAsync(
                celle.Select(c => new CellaPresenza(c.Riga.DipendenteId, c.Data)).ToList(), cantiere.Id);
            if (esito.Saltate > 0)
                avviso = $"{esito.Aggiornate} giorni assegnati a «{cantiere.Nome}».\n" +
                         $"{esito.Saltate} celle non modificate: sono vuote oppure hanno più cantieri nello stesso giorno " +
                         "(per queste usa «Dettaglio giorno»).";
        }
        catch (Exception ex)
        {
            AppLog.Error("Assegnazione cantiere fallita", ex);
            avviso = ex.Message;
        }
        finally
        {
            await RicaricaOreAsync();
            _salvataggio.Release();
        }

        if (avviso is not null) await MostraAsync("Cantiere", avviso);
    }

    // Azzera le ore (del tipo mostrato) nelle celle selezionate.
    public async Task CancellaOreAsync(IReadOnlyList<CellaGiorno> celle)
    {
        var daCancellare = celle.Where(c => c.Testo.Length > 0).ToList();
        if (daCancellare.Count == 0) return;
        if (daCancellare.Count > 1 && !await _dialogs.ConfirmAsync("Cancella ore",
                $"Cancellare le ore {(Modalita == TipoOre.Ordinarie ? "ordinarie" : "straordinarie")} di {daCancellare.Count} celle?",
                "Cancella", destructive: true))
            return;

        var saltate = 0;
        string? errore = null;
        await _salvataggio.WaitAsync();
        try
        {
            foreach (var c in daCancellare)
            {
                if (c.Presenze.Count > 1) { saltate++; continue; }
                await _presenze.ImpostaOreGiornoAsync(c.Riga.DipendenteId, c.Data, Modalita, 0, null);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Cancellazione ore fallita", ex);
            errore = ex.Message;
        }
        finally
        {
            await RicaricaOreAsync();
            _salvataggio.Release();
        }

        if (errore is not null) await MostraAsync("Errore", errore);
        else if (saltate > 0)
            await MostraAsync("Cancella ore", $"{saltate} celle non modificate: hanno più cantieri nello stesso giorno (usa «Dettaglio giorno»).");
    }

    // Finestra con tutte le presenze di un giorno (una riga per cantiere): serve per dividere le ore tra più cantieri.
    public async Task ApriGiornoAsync(CellaGiorno cella)
    {
        if (cella.NonModificabile)
        {
            await MostraAsync("Dettaglio giorno",
                $"{cella.Riga.Nome} è stato assunto il {cella.Riga.DataAssunzione.ToString("dd/MM/yyyy", Formats.Italian)}: " +
                "non si possono registrare presenze prima di quella data.");
            return;
        }

        var form = new PresenzaGiornoViewModel(
            _presenze, _dialogs, cella.Riga.DipendenteId, cella.Riga.Nome, cella.Data, Cantieri, cella.Presenze,
            CantiereNuove ?? CantiereOption.Nessuno);
        if (await _dialogs.ShowAsync(form) is true) await RicaricaOreAsync();
    }

    private async Task RicaricaOreAsync()
    {
        try
        {
            var versione = _versione;
            var presenze = await _presenze.GetMeseAsync(Anno, Mese);
            if (versione != _versione) return;
            _perCella = presenze.GroupBy(p => (p.DipendenteId, p.Data)).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var riga in Righe) AggiornaRiga(riga);
        }
        catch (Exception ex)
        {
            AppLog.Error("Aggiornamento presenze fallito", ex);
        }
    }

    private async Task MostraAsync(string titolo, string messaggio)
    {
        try
        {
            await _dialogs.MessageAsync(titolo, messaggio);
        }
        catch (Exception ex)
        {
            // Può esserci già un dialogo aperto: non deve causare un secondo errore.
            AppLog.Error($"Messaggio non mostrabile ({titolo}): {messaggio}", ex);
        }
    }

    internal static bool TryOre(string testo, out decimal ore)
    {
        testo = testo.Trim();
        if (testo.Length == 0) { ore = 0; return true; }
        return decimal.TryParse(testo.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out ore) && ore >= 0;
    }
}
