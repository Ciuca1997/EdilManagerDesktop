using EdilManager.Core.Entities;

namespace EdilManager.Core;

// Come appare lo stato di una fattura: quello salvato più «Scaduta», che si deduce dalla scadenza.
public enum StatoFattura
{
    // Le note di credito non si pagano: nessuno stato.
    NonApplicabile,
    DaPagare,
    Scaduta,
    Pagata,
    InContestazione,
}

public static class Pagamenti
{

    // Scadenza scritta nella fattura o, se manca, data fattura + giorni predefiniti (0 = nessuna scadenza presunta).
    public static DateOnly? ScadenzaEffettiva(DateOnly? scadenza, DateOnly dataDocumento, int giorniPredefiniti) =>
        scadenza ?? (giorniPredefiniti > 0 ? dataDocumento.AddDays(giorniPredefiniti) : null);

    public static StatoFattura Calcola(string tipoDocumento, StatoPagamento stato, DateOnly? scadenza,
        DateOnly dataDocumento, int giorniPredefiniti, DateOnly oggi)
    {
        if (Documenti.IsNotaCredito(tipoDocumento)) return StatoFattura.NonApplicabile;
        return stato switch
        {
            StatoPagamento.Pagata => StatoFattura.Pagata,
            StatoPagamento.InContestazione => StatoFattura.InContestazione,
            // Il giorno di scadenza la fattura è ancora da pagare: è scaduta dal giorno dopo.
            _ => ScadenzaEffettiva(scadenza, dataDocumento, giorniPredefiniti) is { } s && s < oggi
                ? StatoFattura.Scaduta
                : StatoFattura.DaPagare,
        };
    }

    public static string Testo(StatoFattura stato) => stato switch
    {
        StatoFattura.DaPagare => "Da pagare",
        StatoFattura.Scaduta => "Scaduta",
        StatoFattura.Pagata => "Pagata",
        StatoFattura.InContestazione => "In contestazione",
        _ => "-",
    };
}
