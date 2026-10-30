using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;

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

    public FatturaDetailViewModel(FatturaDetailDto fattura, INavigationService nav)
    {
        _nav = nav;

        Titolo = $"Fattura {fattura.NumeroDocumento}";
        Data = fattura.DataDocumento.ToString("dd/MM/yyyy");
        Tipo = Decodifica(TipiDocumento, fattura.TipoDocumento);
        Scadenza = fattura.DataScadenzaPagamento?.ToString("dd/MM/yyyy") ?? string.Empty;
        Pagamento = fattura.ModalitaPagamento is { Length: > 0 } mp ? Decodifica(ModalitaPagamento, mp) : string.Empty;
        ImportoPagamento = fattura.ImportoPagamento is { } ip ? Euro(ip) : string.Empty;
        Cantiere = fattura.Cantiere is { } c ? (c.Nome == c.CUP ? c.CUP : $"{c.Nome} ({c.CUP})") : string.Empty;

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
    }

    public string Titolo { get; }
    public string Data { get; }
    public string Tipo { get; }
    public string Scadenza { get; }
    public string Pagamento { get; }
    public string ImportoPagamento { get; }
    public string Cantiere { get; }

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

    [RelayCommand]
    private void GoBack() => _nav.Pop();

    private static string Euro(decimal valore) => valore.ToString("N2", Formats.Italian) + " €";

    private static string Decodifica(Dictionary<string, string> tabella, string codice) =>
        tabella.TryGetValue(codice, out var descrizione) ? $"{codice} · {descrizione}" : codice;

    // "Via Roma 1, 63900 Fermo (FM)": solo le parti presenti.
    private static string FormattaIndirizzo(FornitoreDetailDto f)
    {
        var localita = string.Join(" ", new[] { f.Cap, f.Comune }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(f.Provincia)) localita += $" ({f.Provincia})";
        return string.Join(", ", new[] { f.Indirizzo, localita.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
