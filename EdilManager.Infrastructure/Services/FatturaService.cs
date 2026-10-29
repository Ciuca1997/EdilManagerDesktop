using System.Xml;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdilManager.Infrastructure.Services;

public class FatturaService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<FatturaListDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Fatture
            .OrderByDescending(f => f.DataDocumento).ThenByDescending(f => f.Id)
            .Select(f => new FatturaListDto
            {
                Id = f.Id,
                NumeroDocumento = f.NumeroDocumento,
                DataDocumento = f.DataDocumento,
                TipoDocumento = f.TipoDocumento,
                TotaleDocumento = f.TotaleDocumento,
                ModalitaPagamento = f.ModalitaPagamento,
                DataScadenzaPagamento = f.DataScadenzaPagamento,
                Fornitore = f.Fornitore.Denominazione,
                Cantiere = f.Cantiere != null ? f.Cantiere.Nome : null,
            })
            .ToListAsync(ct);
    }

    // Restituisce false se la fattura (stesso fornitore, numero e data) era già presente.
    public async Task<bool> ImportaXmlAsync(Stream xml, CancellationToken ct = default)
    {
        var (fornitore, fattura, cup) = Parse(xml);

        await using var db = await factory.CreateDbContextAsync(ct);
        var prima = await db.Fatture.CountAsync(ct);
        var import = new PecImportService(db, NullLogger<PecImportService>.Instance);
        await import.SalvaFatturaAsync(fornitore, fattura, cup, ct);
        return await db.Fatture.CountAsync(ct) > prima;
    }

    private static (Fornitore, Fattura, string?) Parse(Stream xml)
    {
        const string nonValida = "Il file non è una fattura elettronica FatturaPA valida.";
        try
        {
            var parsed = FatturaPaParser.Parse(xml);

            // Il parser tollera i campi mancanti: senza questi dati non è una fattura utilizzabile.
            if (string.IsNullOrWhiteSpace(parsed.fornitore.PartitaIva) ||
                string.IsNullOrWhiteSpace(parsed.fattura.NumeroDocumento) ||
                parsed.fattura.DataDocumento == default)
                throw new InvalidOperationException(nonValida);

            return parsed;
        }
        catch (Exception ex) when (ex is NullReferenceException or XmlException)
        {
            throw new InvalidOperationException(nonValida, ex);
        }
    }
}
