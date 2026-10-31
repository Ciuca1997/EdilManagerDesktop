using EdilManager.Core;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public record DashboardSummary(
    int Dipendenti, int Cantieri, int Fatture, decimal TotaleFatture,
    decimal OreOrdinarieMese, decimal OreStraordinarieMese,
    int FattureDaPagare, decimal TotaleDaPagare, int FattureScadute, decimal TotaleScaduto);

public class DashboardService(IDbContextFactory<ApplicationDbContext> factory, ImpostazioniService impostazioni)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var oggi = DateOnly.FromDateTime(DateTime.Today);
        var primo = new DateOnly(oggi.Year, oggi.Month, 1);
        var ultimo = primo.AddMonths(1).AddDays(-1);
        var giorni = impostazioni.Corrente.GiorniScadenzaPredefiniti;

        await using var db = await factory.CreateDbContextAsync(ct);

        // SQLite non sa sommare i decimal: si leggono i valori e si sommano in memoria.
        var fatture = await db.Fatture
            .Select(f => new { f.TipoDocumento, f.TotaleDocumento, f.Stato, f.DataScadenzaPagamento, f.DataDocumento })
            .ToListAsync(ct);
        var oreMese = await db.Presenze
            .Where(p => p.Data >= primo && p.Data <= ultimo)
            .Select(p => new { p.OreOrdinarie, p.OreStatordinarie })
            .ToListAsync(ct);

        // «Da pagare» comprende anche le scadute; le fatture in contestazione e le note di credito restano fuori.
        var daPagare = fatture
            .Where(f => Pagamenti.Calcola(f.TipoDocumento, f.Stato, f.DataScadenzaPagamento, f.DataDocumento, giorni, oggi)
                is StatoFattura.DaPagare or StatoFattura.Scaduta)
            .ToList();
        var scadute = daPagare
            .Where(f => Pagamenti.Calcola(f.TipoDocumento, f.Stato, f.DataScadenzaPagamento, f.DataDocumento, giorni, oggi)
                == StatoFattura.Scaduta)
            .ToList();

        return new DashboardSummary(
            await db.Dipendenti.CountAsync(ct),
            await db.Cantieri.CountAsync(ct),
            fatture.Count,
            fatture.Sum(f => f.TotaleDocumento),
            oreMese.Sum(o => o.OreOrdinarie),
            oreMese.Sum(o => o.OreStatordinarie),
            daPagare.Count, daPagare.Sum(f => f.TotaleDocumento),
            scadute.Count, scadute.Sum(f => f.TotaleDocumento));
    }
}
