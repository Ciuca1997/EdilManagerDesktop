using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public class FornitoreService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<FornitoreListDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Fornitori
            .OrderBy(f => f.Denominazione)
            .Select(f => new FornitoreListDto
            {
                Id = f.Id,
                PartitaIva = f.PartitaIva,
                Denominazione = f.Denominazione,
                Comune = f.Comune,
                Provincia = f.Provincia,
                FattureCount = f.Fatture.Count,
            })
            .ToListAsync(ct);
    }

    public async Task<FornitoreDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var fornitore = await db.Fornitori
            .Where(f => f.Id == id)
            .Select(f => new FornitoreDetailDto
            {
                Id = f.Id,
                PartitaIva = f.PartitaIva,
                CodiceFiscale = f.CodiceFiscale,
                Denominazione = f.Denominazione,
                Indirizzo = f.Indirizzo,
                Comune = f.Comune,
                Provincia = f.Provincia,
                Cap = f.Cap,
                Email = f.Email,
            })
            .FirstOrDefaultAsync(ct);
        if (fornitore is null) return null;

        var fatture = await db.Fatture
            .Where(f => f.FornitoreId == id)
            .OrderByDescending(f => f.DataDocumento).ThenByDescending(f => f.Id)
            .Select(FatturaProjections.ToListDto)
            .ToListAsync(ct);

        // SQLite non sa fare Sum su decimal: si somma in memoria.
        fornitore.Fatture = fatture;
        fornitore.TotaleFatture = fatture.Sum(f => Documenti.ConSegno(f.TipoDocumento, f.TotaleDocumento));
        return fornitore;
    }
}
