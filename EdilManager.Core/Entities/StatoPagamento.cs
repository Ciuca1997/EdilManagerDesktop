namespace EdilManager.Core.Entities;

// Stato salvato nel database. «Scaduta» non c'è di proposito: si calcola (vedi Pagamenti), così non può restare indietro.
// I valori numerici sono nel database: non cambiarli.
public enum StatoPagamento
{
    DaPagare = 0,
    Pagata = 1,
    InContestazione = 2,
}
