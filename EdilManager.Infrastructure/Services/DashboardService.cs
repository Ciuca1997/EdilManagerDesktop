using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public record DashboardSummary(
    int Dipendenti, int Cantieri, int Fatture, decimal TotaleFatture,
    decimal OreOrdinarieMese, decimal OreStraordinarieMese);

public class DashboardService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var oggi = DateOnly.FromDateTime(DateTime.Today);
        var primo = new DateOnly(oggi.Year, oggi.Month, 1);
        var ultimo = primo.AddMonths(1).AddDays(-1);

        await using var db = await factory.CreateDbContextAsync(ct);

        // SQLite non sa sommare i decimal: si leggono i valori e si sommano in memoria.
        var totaliFatture = await db.Fatture.Select(f => f.TotaleDocumento).ToListAsync(ct);
        var oreMese = await db.Presenze
            .Where(p => p.Data >= primo && p.Data <= ultimo)
            .Select(p => new { p.OreOrdinarie, p.OreStatordinarie })
            .ToListAsync(ct);

        return new DashboardSummary(
            await db.Dipendenti.CountAsync(ct),
            await db.Cantieri.CountAsync(ct),
            totaliFatture.Count,
            totaliFatture.Sum(),
            oreMese.Sum(o => o.OreOrdinarie),
            oreMese.Sum(o => o.OreStatordinarie));
    }
}
