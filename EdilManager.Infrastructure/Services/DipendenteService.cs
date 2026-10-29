using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public class DipendenteService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<DipendenteListDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Dipendenti
            .OrderBy(d => d.Cognome).ThenBy(d => d.Nome)
            .Select(d => new DipendenteListDto
            {
                Id = d.Id,
                Nome = d.Nome,
                Cognome = d.Cognome,
                CodiceFiscale = d.CodiceFiscale,
                Mansione = d.Mansione,
                Livello = d.Livello,
                DataAssunzione = d.DataAssunzione,
            })
            .ToListAsync(ct);
    }

    public async Task<DipendenteDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d = await db.Dipendenti.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? null : ToDetail(d);
    }

    public async Task<DipendenteDetailDto> CreateAsync(DipendenteUpsertDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await EnsureCodiceFiscaleLiberoAsync(db, dto.CodiceFiscale, null, ct);

        var d = new Dipendente();
        Apply(dto, d);
        db.Dipendenti.Add(d);
        await db.SaveChangesAsync(ct);
        return ToDetail(d);
    }

    public async Task<DipendenteDetailDto> UpdateAsync(int id, DipendenteUpsertDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d = await db.Dipendenti.FindAsync([id], ct)
                ?? throw new InvalidOperationException("Dipendente non trovato.");
        await EnsureCodiceFiscaleLiberoAsync(db, dto.CodiceFiscale, id, ct);

        Apply(dto, d);
        await db.SaveChangesAsync(ct);
        return ToDetail(d);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d = await db.Dipendenti.FindAsync([id], ct);
        if (d is null) return;

        db.Dipendenti.Remove(d);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureCodiceFiscaleLiberoAsync(ApplicationDbContext db, string codiceFiscale, int? excludeId, CancellationToken ct)
    {
        var esiste = await db.Dipendenti.AnyAsync(x => x.CodiceFiscale == codiceFiscale && x.Id != excludeId, ct);
        if (esiste)
            throw new InvalidOperationException("Esiste già un dipendente con questo codice fiscale.");
    }

    private static void Apply(DipendenteUpsertDto dto, Dipendente d)
    {
        d.Nome = dto.Nome;
        d.Cognome = dto.Cognome;
        d.CodiceFiscale = dto.CodiceFiscale;
        d.DataNascita = dto.DataNascita;
        d.DataAssunzione = dto.DataAssunzione;
        d.Mansione = dto.Mansione;
        d.Livello = dto.Livello;
        d.Telefono = dto.Telefono;
        d.Email = dto.Email;
    }

    private static DipendenteDetailDto ToDetail(Dipendente d) => new()
    {
        Id = d.Id,
        Nome = d.Nome,
        Cognome = d.Cognome,
        CodiceFiscale = d.CodiceFiscale,
        DataNascita = d.DataNascita,
        DataAssunzione = d.DataAssunzione,
        Mansione = d.Mansione,
        Livello = d.Livello,
        Telefono = d.Telefono,
        Email = d.Email,
    };
}
