using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
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
                Interno = c.Interno,
                FattureCount = c.Fatture.Count,
            })
            .ToListAsync(ct);
    }

    public async Task<CantiereDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cantiere = await db.Cantieri
            .Where(c => c.Id == id)
            .Select(c => new CantiereDetailDto { Id = c.Id, CUP = c.CUP, Nome = c.Nome, Interno = c.Interno })
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

    // Nome valido e non già usato: due cantieri con lo stesso nome (o con nomi che diventano la stessa cartella) si
    // confonderebbero nell'elenco e condividerebbero la cartella dell'archivio fatture, dove rinominarne uno
    // sposterebbe anche i file dell'altro.
    private static async Task<string> ValidaNomeAsync(ApplicationDbContext db, string nome, int? escludiId, CancellationToken ct)
    {
        nome = nome.Trim();
        if (nome.Length == 0)
            throw new InvalidOperationException("Il nome del cantiere è obbligatorio.");
        if (nome.Length > 200)
            throw new InvalidOperationException("Il nome del cantiere può contenere al massimo 200 caratteri.");

        var cartella = ArchivioFatture.NomeCartella(nome);
        var altriNomi = await db.Cantieri.Where(c => c.Id != escludiId).Select(c => c.Nome).ToListAsync(ct);
        if (altriNomi.Any(n => string.Equals(ArchivioFatture.NomeCartella(n), cartella, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Esiste già un altro cantiere con questo nome (o molto simile): scegline uno diverso.");
        return nome;
    }

    // Restituisce un avviso se la cartella del cantiere nell'archivio fatture non si è potuta rinominare.
    public async Task<string?> RenameAsync(int id, string nome, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cantiere = await db.Cantieri.FindAsync([id], ct)
                       ?? throw new InvalidOperationException("Cantiere non trovato.");
        nome = await ValidaNomeAsync(db, nome, id, ct);

        var vecchioNome = cantiere.Nome;
        cantiere.Nome = nome;
        await db.SaveChangesAsync(ct);
        return archivio.RinominaCartellaCantiere(vecchioNome, nome);
    }

    // Cantiere senza CUP, creato dall'utente. Ha un codice interno INT-0001, INT-0002... (non può coincidere con un CUP vero,
    // che ha 15 caratteri). La cartella in archivio non si crea qui: nasce con la prima fattura copiata.
    public async Task<int> CreaInternoAsync(string nome, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        nome = await ValidaNomeAsync(db, nome, null, ct);

        var esistenti = await db.Cantieri.Where(c => c.CUP.StartsWith(PrefissoInterno)).Select(c => c.CUP).ToListAsync(ct);
        var ultimo = esistenti
            .Select(c => int.TryParse(c[PrefissoInterno.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();
        var cantiere = new Cantiere { CUP = $"{PrefissoInterno}{ultimo + 1:0000}", Nome = nome, Interno = true };
        db.Cantieri.Add(cantiere);
        await db.SaveChangesAsync(ct);
        return cantiere.Id;
    }

    private const string PrefissoInterno = "INT-";

    // Quando arriva il CUP vero di un cantiere interno: lo sostituisce al codice interno e il cantiere diventa normale
    // (le fatture sono collegate all'id, non al CUP). Se quel CUP appartiene già a un altro cantiere non si unisce nulla.
    public async Task ImpostaCupAsync(int id, string cup, CancellationToken ct = default)
    {
        cup = Cup.Normalizza(cup);
        if (!Cup.Valido(cup))
            throw new InvalidOperationException("Il CUP deve avere 15 caratteri: una lettera, 2 cifre, una lettera e 11 cifre.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var cantiere = await db.Cantieri.FindAsync([id], ct)
                       ?? throw new InvalidOperationException("Cantiere non trovato.");
        if (!cantiere.Interno)
            throw new InvalidOperationException("Questo cantiere ha già un CUP.");

        var altro = await db.Cantieri.Where(c => c.CUP == cup).Select(c => c.Nome).FirstOrDefaultAsync(ct);
        if (altro is not null)
            throw new InvalidOperationException($"Il CUP {cup} appartiene già al cantiere «{altro}». I due cantieri non vengono uniti in automatico.");

        cantiere.CUP = cup;
        cantiere.Interno = false;
        await db.SaveChangesAsync(ct);
    }
}
