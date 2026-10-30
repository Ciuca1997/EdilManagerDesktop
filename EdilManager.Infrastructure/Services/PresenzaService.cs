using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

// Un dipendente può avere più presenze nello stesso giorno, una per cantiere (e al massimo una senza cantiere).
public class PresenzaService(IDbContextFactory<ApplicationDbContext> factory)
{
    public const decimal OreMassimeGiorno = 24m;

    public async Task<List<PresenzaDto>> GetByMeseAsync(int dipendenteId, int anno, int mese, CancellationToken ct = default)
    {
        var (primo, ultimo) = Mese(anno, mese);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Presenze
            .Where(p => p.DipendenteId == dipendenteId && p.Data >= primo && p.Data <= ultimo)
            .OrderBy(p => p.Data).ThenBy(p => p.Id)
            .Select(Proiezione)
            .ToListAsync(ct);
    }

    // Tutte le presenze del mese, di tutti i dipendenti (per la griglia mensile).
    public async Task<List<PresenzaDto>> GetMeseAsync(int anno, int mese, CancellationToken ct = default)
    {
        var (primo, ultimo) = Mese(anno, mese);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Presenze
            .Where(p => p.Data >= primo && p.Data <= ultimo)
            .OrderBy(p => p.DipendenteId).ThenBy(p => p.Data).ThenBy(p => p.Id)
            .Select(Proiezione)
            .ToListAsync(ct);
    }

    public async Task CreateAsync(PresenzaUpsertDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await ValidaAsync(db, dto.DipendenteId, dto.Data, dto.CantiereId, dto.OreOrdinarie, dto.OreStatordinarie, null, ct);

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
        await ValidaAsync(db, p.DipendenteId, dto.Data, dto.CantiereId, dto.OreOrdinarie, dto.OreStatordinarie, id, ct);

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

    // Sostituisce tutte le presenze di un giorno con le righe indicate (una per cantiere). Le righe senza ore si scartano:
    // una lista vuota cancella il giorno. Le presenze esistenti sullo stesso cantiere vengono aggiornate, non ricreate.
    public async Task SalvaGiornoAsync(int dipendenteId, DateOnly data, IReadOnlyList<PresenzaGiornoRigaDto> righe,
        CancellationToken ct = default)
    {
        var attive = righe.Where(r => r.OreOrdinarie != 0 || r.OreStraordinarie != 0).ToList();
        foreach (var r in attive) ValidaOre(r.OreOrdinarie, r.OreStraordinarie);
        if (attive.GroupBy(r => r.CantiereId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Lo stesso cantiere (o «nessun cantiere») compare più volte nello stesso giorno.");
        if (attive.Sum(r => r.OreOrdinarie + r.OreStraordinarie) > OreMassimeGiorno)
            throw new InvalidOperationException($"Le ore di un giorno non possono superare {OreMassimeGiorno:0}.");

        await using var db = await factory.CreateDbContextAsync(ct);
        // Azzerare un giorno è sempre consentito (anche per presenze preesistenti all'assunzione); inserire ore no.
        if (attive.Count > 0) await EnsureAssuntoAsync(db, dipendenteId, data, ct);
        var esistenti = await db.Presenze.Where(p => p.DipendenteId == dipendenteId && p.Data == data).ToListAsync(ct);

        foreach (var riga in attive)
        {
            var p = esistenti.FirstOrDefault(e => e.CantiereId == riga.CantiereId);
            if (p is null)
            {
                db.Presenze.Add(new Presenza
                {
                    DipendenteId = dipendenteId, Data = data, CantiereId = riga.CantiereId,
                    OreOrdinarie = riga.OreOrdinarie, OreStatordinarie = riga.OreStraordinarie,
                });
            }
            else
            {
                p.OreOrdinarie = riga.OreOrdinarie;
                p.OreStatordinarie = riga.OreStraordinarie;
            }
        }
        db.Presenze.RemoveRange(esistenti.Where(e => attive.All(r => r.CantiereId != e.CantiereId)));
        await db.SaveChangesAsync(ct);
    }

    // Imposta le ore (ordinarie o straordinarie) di un giorno che ha al massimo una presenza: è la modifica "veloce" di una
    // cella della griglia. Senza presenze ne crea una sul cantiere indicato; con ore azzerate (entrambi i tipi) la elimina.
    public async Task ImpostaOreGiornoAsync(int dipendenteId, DateOnly data, TipoOre tipo, decimal ore, int? cantiereNuove,
        CancellationToken ct = default)
    {
        if (ore < 0) throw new InvalidOperationException("Le ore devono essere un numero maggiore o uguale a zero.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var presenze = await db.Presenze.Where(p => p.DipendenteId == dipendenteId && p.Data == data).ToListAsync(ct);
        if (presenze.Count > 1)
            throw new InvalidOperationException("Questo giorno ha più presenze (cantieri diversi): modificalo dal dettaglio del giorno.");

        if (ore > 0) await EnsureAssuntoAsync(db, dipendenteId, data, ct);
        var p = presenze.SingleOrDefault();
        if (p is null)
        {
            if (ore == 0) return;
            p = new Presenza { DipendenteId = dipendenteId, Data = data, CantiereId = cantiereNuove };
            db.Presenze.Add(p);
        }

        if (tipo == TipoOre.Ordinarie) p.OreOrdinarie = ore;
        else p.OreStatordinarie = ore;

        ValidaOre(p.OreOrdinarie, p.OreStatordinarie);
        if (p.OreOrdinarie + p.OreStatordinarie > OreMassimeGiorno)
            throw new InvalidOperationException($"Le ore di un giorno non possono superare {OreMassimeGiorno:0}.");

        if (p.OreOrdinarie == 0 && p.OreStatordinarie == 0 && p.Id != 0) db.Presenze.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    // Assegna un cantiere (o nessuno) alle celle indicate. Si cambiano solo i giorni con una presenza sola: se il giorno ne ha
    // più d'una o nessuna, la cella è saltata (il cantiere di quelle ore va deciso dal dettaglio del giorno).
    public async Task<EsitoAssegnazioneCantiere> ImpostaCantiereAsync(IReadOnlyCollection<CellaPresenza> celle, int? cantiereId,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        int aggiornate = 0, saltate = 0;

        foreach (var cella in celle.Distinct())
        {
            var presenze = await db.Presenze
                .Where(p => p.DipendenteId == cella.DipendenteId && p.Data == cella.Data).ToListAsync(ct);
            if (presenze.Count != 1) { saltate++; continue; }
            if (presenze[0].CantiereId != cantiereId) presenze[0].CantiereId = cantiereId;
            aggiornate++;
        }

        await db.SaveChangesAsync(ct);
        return new EsitoAssegnazioneCantiere(aggiornate, saltate);
    }

    // Foglio presenze del mese per tutti i dipendenti in forza; con un cantiere conta solo il tempo lavorato lì
    // (e compaiono solo i dipendenti che ci hanno lavorato).
    public async Task<byte[]> EsportaMeseAsync(int anno, int mese, int? cantiereId, FormatoExport formato,
        CancellationToken ct = default)
    {
        var (primo, ultimo) = Mese(anno, mese);
        await using var db = await factory.CreateDbContextAsync(ct);

        var presenze = await db.Presenze
            .Where(p => p.Data >= primo && p.Data <= ultimo && (cantiereId == null || p.CantiereId == cantiereId))
            .ToListAsync(ct);

        var dipendenti = cantiereId is null
            ? await db.Dipendenti.Where(d => d.DataAssunzione <= ultimo).ToListAsync(ct)
            : await db.Dipendenti.Where(d => db.Presenze.Any(p =>
                  p.DipendenteId == d.Id && p.CantiereId == cantiereId && p.Data >= primo && p.Data <= ultimo)).ToListAsync(ct);

        string? sottotitolo = null;
        if (cantiereId is not null)
        {
            var c = await db.Cantieri.FindAsync([cantiereId.Value], ct);
            sottotitolo = c is null ? null : (c.Nome == c.CUP ? $"Cantiere {c.CUP}" : $"Cantiere {c.Nome} (CUP {c.CUP})");
        }

        return formato == FormatoExport.Pdf
            ? PresenzaExportService.ExportMatricePdf(dipendenti, presenze, anno, mese, sottotitolo)
            : PresenzaExportService.ExportMatriceCsv(dipendenti, presenze, anno, mese);
    }

    // Foglio di un dipendente, giorno per giorno, tra due date incluse.
    public async Task<byte[]> EsportaDipendenteAsync(int dipendenteId, DateOnly da, DateOnly a, FormatoExport formato,
        CancellationToken ct = default)
    {
        if (a < da) throw new InvalidOperationException("La data finale è precedente a quella iniziale.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var dipendente = await db.Dipendenti.FindAsync([dipendenteId], ct)
                         ?? throw new InvalidOperationException("Dipendente non trovato.");
        var presenze = await db.Presenze.Include(p => p.Cantiere)
            .Where(p => p.DipendenteId == dipendenteId && p.Data >= da && p.Data <= a)
            .ToListAsync(ct);

        return formato == FormatoExport.Pdf
            ? PresenzaExportService.ExportDipendentePdf(dipendente, presenze, da, a)
            : PresenzaExportService.ExportDipendenteCsv(dipendente, presenze, da, a);
    }

    private static async Task ValidaAsync(ApplicationDbContext db, int dipendenteId, DateOnly data, int? cantiereId,
        decimal ordinarie, decimal straordinarie, int? excludeId, CancellationToken ct)
    {
        ValidaOre(ordinarie, straordinarie);
        await EnsureAssuntoAsync(db, dipendenteId, data, ct);

        var altre = await db.Presenze
            .Where(p => p.DipendenteId == dipendenteId && p.Data == data && p.Id != excludeId)
            .ToListAsync(ct);
        if (altre.Any(p => p.CantiereId == cantiereId))
            throw new InvalidOperationException(cantiereId is null
                ? "Esiste già una presenza senza cantiere per questo dipendente in questa data."
                : "Esiste già una presenza su questo cantiere per questo dipendente in questa data.");
        if (altre.Sum(p => p.OreOrdinarie + p.OreStatordinarie) + ordinarie + straordinarie > OreMassimeGiorno)
            throw new InvalidOperationException($"Le ore di un giorno non possono superare {OreMassimeGiorno:0}.");
    }

    // Le presenze non possono essere precedenti alla data di assunzione.
    private static async Task EnsureAssuntoAsync(ApplicationDbContext db, int dipendenteId, DateOnly data, CancellationToken ct)
    {
        var assunzione = await db.Dipendenti.Where(d => d.Id == dipendenteId)
                             .Select(d => (DateOnly?)d.DataAssunzione).FirstOrDefaultAsync(ct)
                         ?? throw new InvalidOperationException("Dipendente non trovato.");
        if (data < assunzione)
            throw new InvalidOperationException(
                $"Il dipendente è stato assunto il {assunzione.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}: " +
                "non si possono registrare presenze prima di quella data.");
    }

    private static void ValidaOre(decimal ordinarie, decimal straordinarie)
    {
        if (ordinarie < 0 || straordinarie < 0)
            throw new InvalidOperationException("Le ore devono essere un numero maggiore o uguale a zero.");
    }

    private static (DateOnly primo, DateOnly ultimo) Mese(int anno, int mese) =>
        (new DateOnly(anno, mese, 1), new DateOnly(anno, mese, DateTime.DaysInMonth(anno, mese)));

    private static readonly System.Linq.Expressions.Expression<Func<Presenza, PresenzaDto>> Proiezione = p => new PresenzaDto
    {
        Id = p.Id,
        DipendenteId = p.DipendenteId,
        Data = p.Data,
        OreOrdinarie = p.OreOrdinarie,
        OreStatordinarie = p.OreStatordinarie,
        CantiereId = p.CantiereId,
        Cantiere = p.Cantiere != null ? p.Cantiere.Nome : null,
    };

    private static void Apply(PresenzaUpsertDto dto, Presenza p)
    {
        p.CantiereId = dto.CantiereId;
        p.Data = dto.Data;
        p.OreOrdinarie = dto.OreOrdinarie;
        p.OreStatordinarie = dto.OreStatordinarie;
    }
}
