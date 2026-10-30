using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;
using EdilManager.Infrastructure.Persistence;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EdilManager.Infrastructure.Services;

public class PecImportOptions
{
    public string Host { get; set; } = "imaps.pec.aruba.it";
    public int Port { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class PecImportResult
{
    public int FattureImportate { get; set; }
    public int FattureDuplicate { get; set; }
    public List<string> Errori { get; set; } = new();
}

public class PecImportService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PecImportService> _logger;

    public PecImportService(ApplicationDbContext db, ILogger<PecImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PecImportResult> ImportaFattureAsync(PecImportOptions options, CancellationToken ct = default)
    {
        var result = new PecImportResult();

        using var client = new ImapClient();
        await client.ConnectAsync(options.Host, options.Port, true, ct);
        await client.AuthenticateAsync(options.Username, options.Password, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        // Le fatture SDI arrivano da questo mittente ufficiale
        var query = SearchQuery.FromContains("@pec.fatturapa.it");
        var uids = await inbox.SearchAsync(query, ct);

        foreach (var uid in uids)
        {
            try
            {
                var message = await inbox.GetMessageAsync(uid, ct);
                await ProcessMessageAsync(message, result, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore elaborazione email UID {Uid}", uid);
                result.Errori.Add($"UID {uid}: {ex.Message}");
            }
        }

        await client.DisconnectAsync(true, ct);
        return result;
    }

    private async Task ProcessMessageAsync(MimeMessage message, PecImportResult result, CancellationToken ct)
    {
        foreach (var attachment in message.Attachments)
        {
            if (attachment is not MimePart part) continue;

            var fileName = part.FileName ?? string.Empty;
            if (!fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = new MemoryStream();
            await part.Content!.DecodeToAsync(stream, ct);
            stream.Position = 0;

            try
            {
                var (fornitore, fattura, cup) = FatturaPaParser.Parse(stream);
                await SalvaFatturaAsync(fornitore, fattura, cup, ct);
                result.FattureImportate++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore parsing allegato {File}", fileName);
                result.Errori.Add($"{fileName}: {ex.Message}");
            }
        }
    }

    public async Task<EsitoSalvataggio> SalvaFatturaAsync(Fornitore fornitore, Fattura fattura, string? cup,
        CancellationToken ct = default, bool aggiornaAnagrafica = true)
    {
        // Upsert fornitore per P.IVA
        var fornitoreSalvato = await _db.Fornitori
            .FirstOrDefaultAsync(f => f.PartitaIva == fornitore.PartitaIva, ct);

        if (fornitoreSalvato == null)
        {
            _db.Fornitori.Add(fornitore);
            fornitoreSalvato = fornitore;
        }
        else if (aggiornaAnagrafica)
        {
            fornitoreSalvato.Denominazione = fornitore.Denominazione;
            fornitoreSalvato.Indirizzo = fornitore.Indirizzo;
            fornitoreSalvato.Comune = fornitore.Comune;
            fornitoreSalvato.Provincia = fornitore.Provincia;
            fornitoreSalvato.Cap = fornitore.Cap;
            fornitoreSalvato.Email = fornitore.Email;
        }

        await _db.SaveChangesAsync(ct);
        fattura.FornitoreId = fornitoreSalvato.Id;

        // Associa cantiere per CUP se presente
        string? nomeCantiere = null;
        if (!string.IsNullOrWhiteSpace(cup))
        {
            var cantiere = await _db.Cantieri.FirstOrDefaultAsync(c => c.CUP == cup, ct);
            if (cantiere == null)
            {
                cantiere = new Cantiere { CUP = cup, Nome = cup };
                _db.Cantieri.Add(cantiere);
                await _db.SaveChangesAsync(ct);
            }
            fattura.CantiereId = cantiere.Id;
            nomeCantiere = cantiere.Nome;
        }

        // Evita duplicati (stesso fornitore + numero + data)
        var esiste = await _db.Fatture.AnyAsync(f =>
            f.FornitoreId == fattura.FornitoreId &&
            f.NumeroDocumento == fattura.NumeroDocumento &&
            f.DataDocumento == fattura.DataDocumento, ct);

        if (!esiste)
        {
            _db.Fatture.Add(fattura);
            await _db.SaveChangesAsync(ct);
        }

        // Nomi dal database (non dal file): una fattura già presente può avere fornitore/cantiere rinominati.
        return new EsitoSalvataggio(!esiste, fornitoreSalvato.Denominazione, nomeCantiere,
            fattura.NumeroDocumento, fattura.DataDocumento);
    }
}
