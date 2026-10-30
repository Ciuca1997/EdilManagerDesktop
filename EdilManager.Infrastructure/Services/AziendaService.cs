using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

// I dati dell'azienda (una sola riga): nome mostrato nell'app, intestazione dei fogli presenze, ecc.
public class AziendaService(IDbContextFactory<ApplicationDbContext> factory)
{
    // Sollevato dopo ogni salvataggio, così l'interfaccia aggiorna subito il nome mostrato.
    public event EventHandler? Cambiata;

    public async Task<AziendaDto?> GetAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Aziende.AsNoTracking().OrderBy(a => a.Id).Select(a => new AziendaDto
        {
            RagioneSociale = a.RagioneSociale, PartitaIva = a.PartitaIva, CodiceFiscale = a.CodiceFiscale,
            Indirizzo = a.Indirizzo, Cap = a.Cap, Comune = a.Comune, Provincia = a.Provincia,
            Telefono = a.Telefono, Email = a.Email,
        }).FirstOrDefaultAsync(ct);
    }

    public async Task<AziendaDto> SalvaAsync(AziendaDto dto, CancellationToken ct = default)
    {
        var pulito = Normalizza(dto);
        Valida(pulito);

        await using var db = await factory.CreateDbContextAsync(ct);
        var azienda = await db.Aziende.OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
        if (azienda is null)
        {
            azienda = new Azienda();
            db.Aziende.Add(azienda);
        }

        azienda.RagioneSociale = pulito.RagioneSociale;
        azienda.PartitaIva = pulito.PartitaIva;
        azienda.CodiceFiscale = pulito.CodiceFiscale;
        azienda.Indirizzo = pulito.Indirizzo;
        azienda.Cap = pulito.Cap;
        azienda.Comune = pulito.Comune;
        azienda.Provincia = pulito.Provincia;
        azienda.Telefono = pulito.Telefono;
        azienda.Email = pulito.Email;
        await db.SaveChangesAsync(ct);

        Cambiata?.Invoke(this, EventArgs.Empty);
        return pulito;
    }

    // Toglie gli spazi, trasforma i campi vuoti in null e porta partita IVA, codice fiscale e provincia nella forma canonica.
    private static AziendaDto Normalizza(AziendaDto d)
    {
        static string? Testo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        var piva = Testo(d.PartitaIva)?.Replace(" ", "").ToUpperInvariant();
        if (piva is { Length: 13 } && piva.StartsWith("IT")) piva = piva[2..];

        return new AziendaDto
        {
            RagioneSociale = (d.RagioneSociale ?? string.Empty).Trim(),
            PartitaIva = piva,
            CodiceFiscale = Testo(d.CodiceFiscale)?.Replace(" ", "").ToUpperInvariant(),
            Indirizzo = Testo(d.Indirizzo),
            Cap = Testo(d.Cap),
            Comune = Testo(d.Comune),
            Provincia = Testo(d.Provincia)?.ToUpperInvariant(),
            Telefono = Testo(d.Telefono),
            Email = Testo(d.Email),
        };
    }

    private static void Valida(AziendaDto d)
    {
        if (d.RagioneSociale.Length == 0)
            throw new InvalidOperationException("La ragione sociale è obbligatoria.");
        if (d.RagioneSociale.Length > 200)
            throw new InvalidOperationException("La ragione sociale può contenere al massimo 200 caratteri.");
        if (d.PartitaIva is not null && !DatiFiscali.PartitaIvaValida(d.PartitaIva))
            throw new InvalidOperationException("La partita IVA non è valida: sono 11 cifre, controlla di averle scritte giuste.");
        if (d.CodiceFiscale is not null && !DatiFiscali.CodiceFiscaleValido(d.CodiceFiscale))
            throw new InvalidOperationException("Il codice fiscale non è valido (16 caratteri per una persona, 11 cifre per una società).");
        if (d.Cap is not null && (d.Cap.Length != 5 || !d.Cap.All(char.IsDigit)))
            throw new InvalidOperationException("Il CAP deve essere di 5 cifre.");
        if (d.Provincia is not null && (d.Provincia.Length != 2 || !d.Provincia.All(char.IsLetter)))
            throw new InvalidOperationException("La provincia si scrive con la sigla di 2 lettere (es. FM).");
        if (d.Email is not null && (!d.Email.Contains('@') || d.Email.Contains(' ')))
            throw new InvalidOperationException("L'indirizzo email non sembra valido.");
        foreach (var (valore, massimo, nome) in new[]
                 {
                     (d.Indirizzo, 200, "L'indirizzo"), (d.Comune, 100, "Il comune"),
                     (d.Telefono, 30, "Il telefono"), (d.Email, 200, "L'email"),
                 })
            if (valore is not null && valore.Length > massimo)
                throw new InvalidOperationException($"{nome} può contenere al massimo {massimo} caratteri.");
    }
}
