using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public class CantiereService(IDbContextFactory<ApplicationDbContext> factory, ArchivioFatture archivio)
{
    public async Task<List<CantiereListDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Cantieri
            .OrderBy(c => c.Nome)
            .Select(c => new CantiereListDto
            {
                Id = c.Id,
                CUP = c.CUP,
                Nome = c.Nome,
                FattureCount = c.Fatture.Count,
            })
            .ToListAsync(ct);
    }

    public async Task<CantiereDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cantiere = await db.Cantieri
            .Where(c => c.Id == id)
            .Select(c => new CantiereDetailDto { Id = c.Id, CUP = c.CUP, Nome = c.Nome })
            .FirstOrDefaultAsync(ct);
        if (cantiere is null) return null;

        var fatture = await db.Fatture
            .Where(f => f.CantiereId == id)
            .OrderByDescending(f => f.DataDocumento).ThenByDescending(f => f.Id)
            .Select(FatturaProjections.ToListDto)
            .ToListAsync(ct);

        // SQLite non sa fare Sum su decimal: si somma in memoria.
        cantiere.Fatture = fatture;
        cantiere.TotaleFatture = fatture.Sum(f => Documenti.ConSegno(f.TipoDocumento, f.TotaleDocumento));
        cantiere.Fornitori = fatture
            .GroupBy(f => f.FornitoreId)
            .Select(g => new FornitoreRiepilogoDto
            {
                FornitoreId = g.Key,
                Denominazione = g.First().Fornitore,
                FattureCount = g.Count(),
                Totale = g.Sum(f => Documenti.ConSegno(f.TipoDocumento, f.TotaleDocumento)),
            })
            .OrderByDescending(r => r.Totale)
            .ToList();

        // Manodopera: si sommano in memoria (SQLite non sa fare Sum su decimal).
        var presenze = await db.Presenze
            .Where(p => p.CantiereId == id)
            .Select(p => new
            {
                p.DipendenteId, p.Data, p.OreOrdinarie, p.OreStatordinarie,
                Nome = p.Dipendente.Cognome + " " + p.Dipendente.Nome,
            })
            .ToListAsync(ct);
        cantiere.Manodopera = presenze
            .GroupBy(p => p.DipendenteId)
            .Select(g => new ManodoperaDto
            {
                DipendenteId = g.Key,
                Dipendente = g.First().Nome,
                Giorni = g.Select(p => p.Data).Distinct().Count(),
                OreOrdinarie = g.Sum(p => p.OreOrdinarie),
                OreStraordinarie = g.Sum(p => p.OreStatordinarie),
            })
            .OrderByDescending(m => m.OreOrdinarie + m.OreStraordinarie)
            .ThenBy(m => m.Dipendente)
            .ToList();
        return cantiere;
    }

    // Restituisce un avviso se la cartella del cantiere nell'archivio fatture non si è potuta rinominare.
    public async Task<string?> RenameAsync(int id, string nome, CancellationToken ct = default)
    {
        nome = nome.Trim();
        if (nome.Length == 0)
            throw new InvalidOperationException("Il nome del cantiere è obbligatorio.");
        if (nome.Length > 200)
            throw new InvalidOperationException("Il nome del cantiere può contenere al massimo 200 caratteri.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var cantiere = await db.Cantieri.FindAsync([id], ct)
                       ?? throw new InvalidOperationException("Cantiere non trovato.");
        var vecchioNome = cantiere.Nome;
        cantiere.Nome = nome;
        await db.SaveChangesAsync(ct);
        return archivio.RinominaCartellaCantiere(vecchioNome, nome);
    }
}
