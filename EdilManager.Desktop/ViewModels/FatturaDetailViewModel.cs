using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed class RigaFatturaRow(RigaFatturaDto dto)
{
    public string Codice { get; } = dto.CodiceArticolo ?? string.Empty;
    public string Descrizione { get; } = dto.Descrizione;
    public string Quantita { get; } = dto.Quantita is { } q
        ? $"{q.ToString("#,##0.####", Formats.Italian)} {dto.UnitaMisura}".TrimEnd()
        : "-";
    public string PrezzoUnitario { get; } = dto.PrezzoUnitario is { } p ? p.ToString("#,##0.00##", Formats.Italian) : "-";
    public string Sconto { get; } = dto.Sconto is { } s ? s.ToString("0.##", Formats.Italian) + " %" : "-";
    public string Iva { get; } = dto.AliquotaIVA.ToString("0.##", Formats.Italian) + " %";
    public string Totale { get; } = dto.PrezzoTotale.ToString("N2", Formats.Italian);
}

public partial class FatturaDetailViewModel : ObservableObject
{
    private static readonly Dictionary<string, string> TipiDocumento = new()
    {
        ["TD01"] = "Fattura",
        ["TD02"] = "Acconto/anticipo su fattura",
        ["TD03"] = "Acconto/anticipo su parcella",
        ["TD04"] = "Nota di credito",
        ["TD05"] = "Nota di debito",
        ["TD06"] = "Parcella",
        ["TD16"] = "Integrazione fattura reverse charge interno",
        ["TD17"] = "Integrazione/autofattura per acquisto servizi dall'estero",
        ["TD18"] = "Integrazione per acquisto di beni intracomunitari",
        ["TD19"] = "Integrazione/autofattura per acquisto di beni (art. 17 c. 2)",
        ["TD20"] = "Autofattura per regolarizzazione e integrazione",
        ["TD24"] = "Fattura differita (art. 21 c. 4 lett. a)",
        ["TD25"] = "Fattura differita (art. 21 c. 4 terzo periodo lett. b)",
        ["TD26"] = "Cessione di beni ammortizzabili e passaggi interni",
        ["TD27"] = "Fattura per autoconsumo o cessioni gratuite senza rivalsa",
    };

    private static readonly Dictionary<string, string> ModalitaPagamento = new()
    {
        ["MP01"] = "Contanti",
        ["MP02"] = "Assegno",
        ["MP03"] = "Assegno circolare",
        ["MP04"] = "Contanti presso Tesoreria",
        ["MP05"] = "Bonifico",
        ["MP06"] = "Vaglia cambiario",
        ["MP07"] = "Bollettino bancario",
        ["MP08"] = "Carta di pagamento",
        ["MP09"] = "RID",
        ["MP10"] = "RID utenze",
        ["MP11"] = "RID veloce",
        ["MP12"] = "RIBA",
        ["MP13"] = "MAV",
        ["MP14"] = "Quietanza erario",
        ["MP15"] = "Giroconto su conti di contabilità speciale",
        ["MP16"] = "Domiciliazione bancaria",
        ["MP17"] = "Domiciliazione postale",
        ["MP18"] = "Bollettino di c/c postale",
        ["MP19"] = "SEPA Direct Debit",
        ["MP20"] = "SEPA Direct Debit CORE",
        ["MP21"] = "SEPA Direct Debit B2B",
        ["MP22"] = "Trattenuta su somme già riscosse",
        ["MP23"] = "PagoPA",
    };

    private readonly INavigationService _nav;
    private readonly DetailOpener _opener;
    private readonly int _fornitoreId;
    private int? _cantiereId;
    private readonly int _id;
    private readonly CantiereFatture _cantieriFatture;
    private readonly FatturaService _service;
    private readonly StatoFatture _stati;
    private readonly int _giorniScadenza;

    public FatturaDetailViewModel(FatturaDetailDto fattura, INavigationService nav, DetailOpener opener,
        FatturaService service, StatoFatture stati, ImpostazioniService impostazioni, CantiereFatture cantieriFatture)
    {
        _cantieriFatture = cantieriFatture;
        _nav = nav;
        _opener = opener;
        _service = service;
        _stati = stati;
        _id = fattura.Id;
        _giorniScadenza = impostazioni.Corrente.GiorniScadenzaPredefiniti;
        _fornitoreId = fattura.Fornitore.Id;
        _cantiereId = fattura.Cantiere?.Id;

        Titolo = $"Fattura {fattura.NumeroDocumento}";
        Data = fattura.DataDocumento.ToString("dd/MM/yyyy");
        Tipo = Decodifica(TipiDocumento, fattura.TipoDocumento);
        Scadenza = fattura.DataScadenzaPagamento?.ToString("dd/MM/yyyy") ?? string.Empty;
        Pagamento = fattura.ModalitaPagamento is { Length: > 0 } mp ? Decodifica(ModalitaPagamento, mp) : string.Empty;
        ImportoPagamento = fattura.ImportoPagamento is { } ip ? Euro(ip) : string.Empty;
        Cantiere = fattura.Cantiere?.Etichetta ?? string.Empty;

        var f = fattura.Fornitore;
        Fornitore = f.Denominazione;
        PartitaIva = f.PartitaIva;
        CodiceFiscale = f.CodiceFiscale ?? string.Empty;
        Indirizzo = FormattaIndirizzo(f);
        Email = f.Email ?? string.Empty;

        TotaleImponibile = Euro(fattura.TotaleImponibile);
        TotaleImposta = Euro(fattura.TotaleImposta);
        TotaleDocumento = Euro(fattura.TotaleDocumento);

        Righe = fattura.Righe.Select(r => new RigaFatturaRow(r)).ToList();

        _isNotaCredito = Documenti.IsNotaCredito(fattura.TipoDocumento);
        AggiornaStato(fattura);
    }

    private readonly bool _isNotaCredito;

    // Le note di credito non si pagano: niente stato e niente pulsanti.
    public bool HasStato => !_isNotaCredito;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSegnaPagata), nameof(CanSegnaContestata))]
    private StatoFattura _stato;

    [ObservableProperty]
    private string _statoTesto = string.Empty;

    [ObservableProperty]
    private string _scadenzaPresunta = string.Empty;

    private StatoPagamento _statoSalvato;

    public bool CanSegnaPagata => Stato != StatoFattura.Pagata;
    public bool CanSegnaDaPagare => _statoSalvato != StatoPagamento.DaPagare;
    public bool CanSegnaContestata => Stato != StatoFattura.InContestazione;

    private void AggiornaStato(FatturaDetailDto f)
    {
        _statoSalvato = f.Stato;
        var oggi = DateOnly.FromDateTime(DateTime.Today);
        Stato = Pagamenti.Calcola(f.TipoDocumento, f.Stato, f.DataScadenzaPagamento, f.DataDocumento, _giorniScadenza, oggi);
        StatoTesto = f.Stato == StatoPagamento.Pagata && f.DataPagamento is { } d
            ? $"Pagata il {d:dd/MM/yyyy}"
            : Pagamenti.Testo(Stato);
        // Scadenza non scritta nel documento: si mostra quella presunta, dicendolo.
        ScadenzaPresunta = f.DataScadenzaPagamento is null && Pagamenti.ScadenzaEffettiva(null, f.DataDocumento, _giorniScadenza) is { } p
            ? $"{p:dd/MM/yyyy} (presunta)"
            : string.Empty;
        OnPropertyChanged(nameof(CanSegnaDaPagare));
    }

    [RelayCommand]
    private Task SegnaPagataAsync() => CambiaStatoAsync(StatoPagamento.Pagata);

    [RelayCommand]
    private Task SegnaDaPagareAsync() => CambiaStatoAsync(StatoPagamento.DaPagare);

    [RelayCommand]
    private Task SegnaContestataAsync() => CambiaStatoAsync(StatoPagamento.InContestazione);

    private async Task CambiaStatoAsync(StatoPagamento nuovo)
    {
        if (!await _stati.ImpostaAsync([_id], nuovo)) return;
        try
        {
            if (await _service.GetByIdAsync(_id) is { } aggiornata) AggiornaStato(aggiornata);
        }
        catch (Exception ex)
        {
            AppLog.Error("Ricaricamento della fattura dopo il cambio di stato fallito", ex);
        }
    }

    public string Titolo { get; }
    public string Data { get; }
    public string Tipo { get; }
    public string Scadenza { get; }
    public string Pagamento { get; }
    public string ImportoPagamento { get; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCantiere))]
    private string _cantiere = string.Empty;

    public string Fornitore { get; }
    public string PartitaIva { get; }
    public string CodiceFiscale { get; }
    public string Indirizzo { get; }
    public string Email { get; }

    public string TotaleImponibile { get; }
    public string TotaleImposta { get; }
    public string TotaleDocumento { get; }

    public IReadOnlyList<RigaFatturaRow> Righe { get; }
    public bool HasRighe => Righe.Count > 0;
    public bool NoRighe => Righe.Count == 0;
    // Vero anche quando il cantiere è stato appena assegnato: il campo è il testo da mostrare.
    public bool HasCantiere => Cantiere.Length > 0;

    [RelayCommand]
    private Task OpenFornitoreAsync() => _opener.OpenFornitoreAsync(_fornitoreId);

    [RelayCommand]
    private Task OpenCantiereAsync() => _cantiereId is { } id ? _opener.OpenCantiereAsync(id) : Task.CompletedTask;

    [RelayCommand]
    private async Task AssegnaCantiereAsync()
    {
        if (!await _cantieriFatture.AssegnaAsync([_id])) return;
        try
        {
            if (await _service.GetByIdAsync(_id) is not { } aggiornata) return;
            _cantiereId = aggiornata.Cantiere?.Id;
            Cantiere = aggiornata.Cantiere?.Etichetta ?? string.Empty;
        }
        catch (Exception ex)
        {
            AppLog.Error("Ricaricamento della fattura dopo l'assegnazione del cantiere fallito", ex);
        }
    }

    [RelayCommand]
    private void GoBack() => _nav.Pop();

    private static string Euro(decimal valore) => valore.ToString("N2", Formats.Italian) + " €";

    private static string Decodifica(Dictionary<string, string> tabella, string codice) =>
        tabella.TryGetValue(codice, out var descrizione) ? $"{codice} · {descrizione}" : codice;

    // "Via Roma 1, 63900 Fermo (FM)": solo le parti presenti.
    internal static string FormattaIndirizzo(FornitoreDetailDto f)
    {
        var localita = string.Join(" ", new[] { f.Cap, f.Comune }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(f.Provincia)) localita += $" ({f.Provincia})";
        return string.Join(", ", new[] { f.Indirizzo, localita.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
