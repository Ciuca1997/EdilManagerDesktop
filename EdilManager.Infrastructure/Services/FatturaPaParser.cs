using System.Xml.Linq;
using EdilManager.Core.Entities;

namespace EdilManager.Infrastructure.Services;

public static class FatturaPaParser
{
    private static readonly XNamespace Ns = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    public static (Fornitore fornitore, Fattura fattura, string? cup) Parse(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);
        var root = doc.Root!;

        XElement? Desc(XElement? parent, string name)
        {
            if (parent == null) return null;
            foreach (var el in parent.Descendants())
                if (el.Name.LocalName == name) return el;
            return null;
        }

        var cedente = Desc(root, "CedentePrestatore")!;
        var datiAnag = Desc(cedente, "DatiAnagrafici")!;
        var anag = Desc(datiAnag, "Anagrafica")!;
        var sede = Desc(cedente, "Sede")!;

        var fornitore = new Fornitore
        {
            PartitaIva = Desc(datiAnag, "IdCodice")?.Value ?? string.Empty,
            CodiceFiscale = Desc(datiAnag, "CodiceFiscale")?.Value,
            Denominazione = Desc(anag, "Denominazione")?.Value
                            ?? $"{Desc(anag, "Nome")?.Value} {Desc(anag, "Cognome")?.Value}".Trim(),
            Indirizzo = Desc(sede, "Indirizzo")?.Value,
            Comune = Desc(sede, "Comune")?.Value,
            Provincia = Desc(sede, "Provincia")?.Value,
            Cap = Desc(sede, "CAP")?.Value,
            Email = Desc(cedente, "Email")?.Value,
        };

        var datiGenerali = Desc(root, "DatiGenerali")!;
        var datiGeneraliDoc = Desc(datiGenerali, "DatiGeneraliDocumento")!;

        var tipoDoc = Desc(datiGeneraliDoc, "TipoDocumento")?.Value ?? string.Empty;
        var numero = Desc(datiGeneraliDoc, "Numero")?.Value ?? string.Empty;
        var dataStr = Desc(datiGeneraliDoc, "Data")?.Value ?? string.Empty;
        DateOnly.TryParse(dataStr, out var dataDoc);

        var cup = Desc(datiGenerali, "CodiceCUP")?.Value;

        var riepilogo = Desc(root, "DatiRiepilogo");
        decimal.TryParse(Desc(riepilogo, "ImponibileImporto")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var imponibile);
        decimal.TryParse(Desc(riepilogo, "Imposta")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var imposta);

        var datiPagamento = Desc(root, "DatiPagamento");
        var modalita = Desc(datiPagamento, "ModalitaPagamento")?.Value;
        var scadenzaStr = Desc(datiPagamento, "DataScadenzaPagamento")?.Value;
        DateOnly? scadenza = DateOnly.TryParse(scadenzaStr, out var s) ? s : null;
        decimal.TryParse(Desc(datiPagamento, "ImportoPagamento")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var importoPag);

        var righe = new List<RigaFattura>();
        var corpoDoc = root.Descendants().Where(e => e.Name.LocalName == "DettaglioLinee");
        foreach (var linea in corpoDoc)
        {
            decimal.TryParse(Desc(linea, "Quantita")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty);
            decimal.TryParse(Desc(linea, "PrezzoUnitario")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var prezzo);
            decimal.TryParse(Desc(linea, "AliquotaIVA")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var iva);
            decimal.TryParse(Desc(linea, "PrezzoTotale")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var totale);
            decimal.TryParse(Desc(linea, "PercentualeSconto")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sconto);

            righe.Add(new RigaFattura
            {
                CodiceArticolo = Desc(linea, "CodiceValore")?.Value,
                Descrizione = Desc(linea, "Descrizione")?.Value ?? string.Empty,
                Quantita = qty == 0 ? null : qty,
                PrezzoUnitario = prezzo == 0 ? null : prezzo,
                UnitaMisura = Desc(linea, "UnitaMisura")?.Value,
                Sconto = sconto == 0 ? null : sconto,
                AliquotaIVA = iva,
                PrezzoTotale = totale,
            });
        }

        var fattura = new Fattura
        {
            NumeroDocumento = numero,
            DataDocumento = dataDoc,
            TipoDocumento = tipoDoc,
            TotaleImponibile = imponibile,
            TotaleImposta = imposta,
            TotaleDocumento = imponibile + imposta,
            ModalitaPagamento = modalita,
            DataScadenzaPagamento = scadenza,
            ImportoPagamento = importoPag == 0 ? null : importoPag,
            Righe = righe,
        };

        return (fornitore, fattura, cup);
    }
}
