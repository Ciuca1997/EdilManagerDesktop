using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public class PresenzaService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<PresenzaDto>> GetByMeseAsync(int dipendenteId, int anno, int mese, CancellationToken ct = default)
    {
        var primo = new DateOnly(anno, mese, 1);
        var ultimo = new DateOnly(anno, mese, DateTime.DaysInMonth(anno, mese));

        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Presenze
            .Where(p => p.DipendenteId == dipendenteId && p.Data >= primo && p.Data <= ultimo)
            .OrderBy(p => p.Data)
            .Select(p => new PresenzaDto
            {
                Id = p.Id,
                Data = p.Data,
                OreOrdinarie = p.OreOrdinarie,
                OreStatordinarie = p.OreStatordinarie,
                CantiereId = p.CantiereId,
                Cantiere = p.Cantiere != null ? p.Cantiere.Nome : null,
            })
            .ToListAsync(ct);
    }

    public async Task CreateAsync(PresenzaUpsertDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await EnsureDataLiberaAsync(db, dto.DipendenteId, dto.Data, null, ct);

        var p = new Presenza { DipendenteId = dto.DipendenteId };
        Apply(dto, p);
        db.Presenze.Add(p);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(int id, PresenzaUpsertDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var p = await db.Presenze.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Presenza non trovata.");
        await EnsureDataLiberaAsync(db, p.DipendenteId, dto.Data, id, ct);

        Apply(dto, p);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var p = await db.Presenze.FindAsync([id], ct);
        if (p is null) return;

        db.Presenze.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureDataLiberaAsync(ApplicationDbContext db, int dipendenteId, DateOnly data, int? excludeId, CancellationToken ct)
    {
        var esiste = await db.Presenze.AnyAsync(p => p.DipendenteId == dipendenteId && p.Data == data && p.Id != excludeId, ct);
        if (esiste)
            throw new InvalidOperationException("Presenza già registrata per questo dipendente in questa data.");
    }

    private static void Apply(PresenzaUpsertDto dto, Presenza p)
    {
        p.CantiereId = dto.CantiereId;
        p.Data = dto.Data;
        p.OreOrdinarie = dto.OreOrdinarie;
        p.OreStatordinarie = dto.OreStatordinarie;
    }
}
