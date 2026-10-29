using EdilManager.Core.DTOs;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

public class CantiereService(IDbContextFactory<ApplicationDbContext> factory)
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

    public async Task RenameAsync(int id, string nome, CancellationToken ct = default)
    {
        nome = nome.Trim();
        if (nome.Length == 0)
            throw new InvalidOperationException("Il nome del cantiere è obbligatorio.");
        if (nome.Length > 200)
            throw new InvalidOperationException("Il nome del cantiere può contenere al massimo 200 caratteri.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var cantiere = await db.Cantieri.FindAsync([id], ct)
                       ?? throw new InvalidOperationException("Cantiere non trovato.");
        cantiere.Nome = nome;
        await db.SaveChangesAsync(ct);
    }
}
