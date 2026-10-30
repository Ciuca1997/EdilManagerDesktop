using System.Globalization;
using System.Text.RegularExpressions;
using EdilManager.Core.Entities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EdilManager.Infrastructure.Services;

// Due percorsi: il PDF di visualizzazione standard di una fattura elettronica (blocchi Cedente/Cessionario, riepilogo IVA),
// letto con regole precise; qualsiasi altro PDF, letto con regole generiche e meno affidabili.
// Le righe di dettaglio non vengono estratte.
public static class FatturaPdfParser
{
    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Data = @"(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4})";
    private const string Importo = @"(?:€|eur(?:o)?)?\s*(-?\d{1,3}(?:\.\d{3})*,\d{2}|-?\d+,\d{2})";

    private static readonly Regex PivaEtichettata =
        new(@"(?:p\.?\s*iva|partita\s*iva|vat(?:\s*(?:number|no\.?|id))?)\s*[:\-]?\s*(?:IT)?\s*(\d{11})\b", Opt);
    private static readonly Regex PivaConPrefisso = new(@"\bIT\s?(\d{11})\b", Opt);
    private static readonly Regex CodiceFiscale =
        new(@"(?:c\.?\s*f\.?|cod(?:ice)?\.?\s*fisc(?:ale)?\.?)\s*[:\-]?\s*([A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]|\d{11})\b", Opt);
    private static readonly Regex Numero =
        new(@"(?:fattura|invoice|documento|doc\.|nota\s+(?:di\s+)?credito)\s*(?:di\s+vendita\s*)?(?:num(?:ero)?\.?|nr\.?|n°|nº|n\.?)\s*[:\-]?\s*([A-Z0-9][A-Z0-9/\-\.]*)(?:\s+del\s+" + Data + ")?", Opt);
    private static readonly Regex NumeroEtichetta =
        new(@"\bnumero(?:\s+(?:fattura|documento))?\s*[:\-]?\s*([A-Z0-9][A-Z0-9/\-\.]*)", Opt);
    private static readonly Regex DataDocumento =
        new(@"\bdata(?:\s+(?:fattura|documento|emissione))?\s*[:\-]?\s*" + Data, Opt);
    private static readonly Regex QualsiasiData = new(Data, Opt);
    private static readonly Regex Scadenza =
        new(@"scadenza(?:\s+(?:pagamento|fattura))?\s*[:\-]?\s*" + Data, Opt);
    private static readonly Regex Cup = new(@"\bCUP\b\s*[:\-]?\s*([A-Z0-9]{15})\b", Opt);
    private static readonly Regex ModalitaPagamento = new(@"\b(MP\d{2})\b");
    private static readonly Regex FormaGiuridica =
        new(@"\b(s\.?\s?r\.?\s?l\.?s?|s\.?\s?p\.?\s?a\.?|s\.?\s?n\.?\s?c\.?|s\.?\s?a\.?\s?s\.?|s\.?\s?c\.?\s?a?\s?r?\.?\s?l?\.?|ditta|impresa|di\s+[A-Z][a-z]+\s+[A-Z][a-z]+)\b", Opt);

    // Riga "TD01 fattura 266 30-12-2023 SUBM70N": tipo, descrizione, numero, data, codice destinatario.
    private static readonly Regex IntestazioneDocumento =
        new(@"^(TD\d{2})\b.*?(\S+)\s+(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4})(?:\s+\S+)?\s*$", Opt);
    private static readonly Regex TokenImporto = new(@"^-?(?:\d{1,3}(?:\.\d{3})*|\d+),\d{2}$");

    private static readonly string[] EtichetteAnagrafica =
    [
        "Identificativo fiscale ai fini IVA", "Codice fiscale", "Denominazione", "Nome", "Cognome", "Titolo",
        "Regime fiscale", "Indirizzo", "Numero civico", "Comune", "Provincia", "Cap", "Nazione", "Telefono", "Fax",
        "Email", "Codice EORI",
    ];

    private static readonly string[] EtichetteTotale =
    [
        @"totale\s+(?:documento|fattura)", @"totale\s+(?:da|a)\s+pagare", @"importo\s+totale",
        @"totale\s+complessivo", @"netto\s+a\s+pagare", @"\btotale\b",
    ];
    private static readonly string[] EtichetteImponibile =
        [@"totale\s+imponibile", @"imponibile", @"totale\s+merce", @"totale\s+netto"];
    private static readonly string[] EtichetteImposta = [@"totale\s+iva", @"importo\s+iva", @"imposta", @"\biva\b"];

    public static (Fornitore fornitore, Fattura fattura, string? cup) Parse(Stream pdf)
    {
        var testo = EstraiTesto(pdf);
        if (string.IsNullOrWhiteSpace(testo))
            throw new InvalidOperationException("Il PDF non contiene testo leggibile (forse è una scansione).");

        var righe = testo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var piatto = Regex.Replace(testo, @"\s+", " ");

        return IsVisualizzazioneStandard(piatto) ? ParseVisualizzazione(righe, piatto) : ParseGenerico(righe, piatto);
    }

    private static bool IsVisualizzazioneStandard(string piatto) =>
        piatto.Contains("Cedente/prestatore", StringComparison.OrdinalIgnoreCase) &&
        piatto.Contains("RIEPILOGHI IVA", StringComparison.OrdinalIgnoreCase);

    private static (Fornitore, Fattura, string?) ParseVisualizzazione(string[] righe, string piatto)
    {
        var cedente = Blocco(piatto, "Cedente/prestatore", "Terzo Intermediario", "Rappresentante fiscale", "Cessionario/committente");

        var piva = Campo(cedente, "Identificativo fiscale ai fini IVA")?.Replace(" ", "");
        if (piva is not null && piva.StartsWith("IT", StringComparison.OrdinalIgnoreCase)) piva = piva[2..];
        var denominazione = Campo(cedente, "Denominazione")
                            ?? $"{Campo(cedente, "Nome")} {Campo(cedente, "Cognome")}".Trim();

        var intestazione = righe.Select(r => IntestazioneDocumento.Match(r)).FirstOrDefault(m => m.Success);
        var numero = intestazione?.Groups[2].Value;
        var data = intestazione is null ? null : ParseData(intestazione.Groups[3].Value);
        var riepilogo = Riepilogo(righe);
        var totale = TotaleDocumento(righe);

        VerificaDatiMancanti(
            (piva, "partita IVA del fornitore"), (denominazione, "denominazione del fornitore"),
            (numero, "numero"), (data, "data"), (riepilogo, "riepilogo IVA"), (totale, "totale"));

        var fornitore = new Fornitore
        {
            PartitaIva = piva!,
            CodiceFiscale = Campo(cedente, "Codice fiscale"),
            Denominazione = denominazione,
            Indirizzo = Campo(cedente, "Indirizzo"),
            Comune = Campo(cedente, "Comune"),
            Provincia = Campo(cedente, "Provincia"),
            Cap = Campo(cedente, "Cap"),
            Email = Campo(cedente, "Email"),
        };

        var fattura = new Fattura
        {
            NumeroDocumento = numero!,
            DataDocumento = data!.Value,
            TipoDocumento = intestazione!.Groups[1].Value.ToUpperInvariant(),
            TotaleImponibile = riepilogo!.Value.imponibile,
            TotaleImposta = riepilogo.Value.imposta,
            TotaleDocumento = totale!.Value,
            ModalitaPagamento = ModalitaPagamento.Match(piatto) is { Success: true } mp ? mp.Groups[1].Value : null,
            DataScadenzaPagamento = TrovaScadenza(piatto),
        };

        return (fornitore, fattura, TrovaCup(piatto));
    }

    private static (Fornitore, Fattura, string?) ParseGenerico(string[] righe, string piatto)
    {
        var piva = TrovaPartitaIva(piatto);
        var (numero, dataNumero) = TrovaNumero(piatto);
        var data = dataNumero ?? TrovaData(piatto);
        var totale = TrovaImporto(piatto, EtichetteTotale);
        var imponibile = TrovaImporto(piatto, EtichetteImponibile);
        var imposta = TrovaImporto(piatto, EtichetteImposta);

        VerificaDatiMancanti((piva, "partita IVA"), (numero, "numero"), (data, "data"), (totale, "totale"));

        // Se manca uno dei due tra imponibile e imposta, si ricava per differenza dal totale.
        var totaleDoc = totale!.Value;
        if (imponibile is null && imposta is not null) imponibile = totaleDoc - imposta;
        else if (imposta is null && imponibile is not null) imposta = totaleDoc - imponibile;
        var imponibileDoc = imponibile ?? totaleDoc;
        var impostaDoc = imposta ?? 0m;

        var fornitore = new Fornitore
        {
            PartitaIva = piva!,
            CodiceFiscale = CodiceFiscale.Match(piatto) is { Success: true } cf ? cf.Groups[1].Value.ToUpperInvariant() : null,
            Denominazione = TrovaDenominazione(righe, piva!) ?? $"Fornitore {piva}",
        };

        var fattura = new Fattura
        {
            NumeroDocumento = numero!,
            DataDocumento = data!.Value,
            TipoDocumento = Regex.IsMatch(piatto, @"nota\s+(?:di\s+)?credito", Opt) ? "TD04" : "TD01",
            TotaleImponibile = imponibileDoc,
            TotaleImposta = impostaDoc,
            TotaleDocumento = totaleDoc,
            DataScadenzaPagamento = TrovaScadenza(piatto),
        };

        return (fornitore, fattura, TrovaCup(piatto));
    }

    private static void VerificaDatiMancanti(params (object? valore, string nome)[] campi)
    {
        var mancanti = campi.Where(c => c.valore is null || c.valore is string { Length: 0 }).Select(c => c.nome).ToList();
        if (mancanti.Count > 0)
            throw new InvalidOperationException(
                $"Non riesco a leggere questa fattura PDF, dati non trovati: {string.Join(", ", mancanti)}. " +
                "Se disponibile, importa la versione XML.");
    }

    private static string EstraiTesto(Stream pdf)
    {
        try
        {
            using var doc = PdfDocument.Open(pdf);
            return string.Join('\n', doc.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Il file non è un PDF valido o è protetto da password.", ex);
        }
    }

    // --- Visualizzazione standard

    private static string Blocco(string piatto, string inizio, params string[] fine)
    {
        var i = piatto.IndexOf(inizio, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return string.Empty;
        var da = i + inizio.Length;
        var a = fine.Select(f => piatto.IndexOf(f, da, StringComparison.OrdinalIgnoreCase))
            .Where(x => x >= 0).DefaultIfEmpty(piatto.Length).Min();
        return piatto[da..a];
    }

    // Il testo è appiattito: il valore di un campo arriva fino alla successiva etichetta nota (anche se andava a capo).
    private static string? Campo(string blocco, string etichetta)
    {
        var altre = string.Join("|", EtichetteAnagrafica.Where(e => e != etichetta).Select(Regex.Escape));
        var m = Regex.Match(blocco, $@"(?<!\w){Regex.Escape(etichetta)}:\s*(.*?)(?=\s+(?:{altre}):|$)", Opt);
        return m.Success && m.Groups[1].Value.Trim() is { Length: > 0 } v ? v : null;
    }

    // Righe di riepilogo sotto l'intestazione "Totale imponibile / Totale imposta": %IVA ... imponibile imposta.
    private static (decimal imponibile, decimal imposta)? Riepilogo(string[] righe)
    {
        var inizio = Array.FindIndex(righe, r => r.Contains("Totale imposta", StringComparison.OrdinalIgnoreCase));
        if (inizio < 0) return null;

        decimal imponibile = 0, imposta = 0;
        var trovato = false;
        foreach (var riga in righe.Skip(inizio + 1))
        {
            if (riga.Contains("bollo", StringComparison.OrdinalIgnoreCase) ||
                riga.Contains("Totale documento", StringComparison.OrdinalIgnoreCase)) break;

            var numeri = NumeriFinali(riga);
            if (numeri.Count < 3) continue;
            imponibile += LeggiImporto(numeri[^2]);
            imposta += LeggiImporto(numeri[^1]);
            trovato = true;
        }
        return trovato ? (imponibile, imposta) : null;
    }

    // L'intestazione "... Totale documento" ha i valori nella riga sotto; il totale è l'ultimo numero.
    private static decimal? TotaleDocumento(string[] righe)
    {
        var i = Array.FindIndex(righe, r => r.EndsWith("Totale documento", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= righe.Length) return null;
        var numeri = NumeriFinali(righe[i + 1]);
        return numeri.Count > 0 ? LeggiImporto(numeri[^1]) : null;
    }

    private static List<string> NumeriFinali(string riga) =>
        riga.Split(' ', StringSplitOptions.RemoveEmptyEntries).Reverse()
            .TakeWhile(t => TokenImporto.IsMatch(t)).Reverse().ToList();

    private static decimal LeggiImporto(string valore) =>
        decimal.Parse(valore.Replace(".", "").Replace(',', '.'), NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);

    // --- Generico

    // Il primo numero valido nel documento è quasi sempre il fornitore (intestazione); il cliente compare dopo.
    private static string? TrovaPartitaIva(string testo)
    {
        foreach (var regex in new[] { PivaEtichettata, PivaConPrefisso })
            foreach (Match m in regex.Matches(testo))
                if (PartitaIvaValida(m.Groups[1].Value))
                    return m.Groups[1].Value;
        return null;
    }

    private static bool PartitaIvaValida(string piva)
    {
        if (piva.Length != 11 || !piva.All(char.IsDigit)) return false;
        var somma = 0;
        for (var i = 0; i < 10; i++)
        {
            var cifra = piva[i] - '0';
            if (i % 2 == 1) { cifra *= 2; if (cifra > 9) cifra -= 9; }
            somma += cifra;
        }
        return (10 - somma % 10) % 10 == piva[10] - '0';
    }

    private static (string? numero, DateOnly? data) TrovaNumero(string testo)
    {
        foreach (Match m in Numero.Matches(testo))
            if (NumeroValido(m.Groups[1].Value) is { } n)
                return (n, m.Groups[2].Success ? ParseData(m.Groups[2].Value) : null);

        foreach (Match m in NumeroEtichetta.Matches(testo))
            if (NumeroValido(m.Groups[1].Value) is { } n)
                return (n, null);

        return (null, null);
    }

    // Scarta parole catturate per errore e le sole date.
    private static string? NumeroValido(string valore)
    {
        valore = valore.TrimEnd('.', '-', '/');
        return valore.Any(char.IsDigit) && !QualsiasiData.IsMatch(valore) ? valore : null;
    }

    // Solo date con etichetta: una data qualsiasi nel testo potrebbe essere quella di un DDT o di un ordine.
    private static DateOnly? TrovaData(string testo)
    {
        foreach (Match m in DataDocumento.Matches(testo))
            if (ParseData(m.Groups[1].Value) is { } d) return d;
        return null;
    }

    // Si prova ogni etichetta in ordine di specificità; l'ultima occorrenza è di solito il riepilogo a fine pagina.
    private static decimal? TrovaImporto(string testo, string[] etichette)
    {
        foreach (var etichetta in etichette)
        {
            var matches = Regex.Matches(testo, etichetta + @"\s*(?:documento|fattura|\(.*?\))?\s*[:\-]?\s*" + Importo, Opt);
            if (matches.Count == 0) continue;
            var valore = matches[^1].Groups[1].Value;
            if (decimal.TryParse(valore.Replace(".", "").Replace(',', '.'), NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var d))
                return d;
        }
        return null;
    }

    // L'intestazione del fornitore sta prima della sua partita IVA: si cerca la riga con una forma giuridica, altrimenti la prima riga.
    private static string? TrovaDenominazione(string[] righe, string piva)
    {
        var indice = Array.FindIndex(righe, r => r.Contains(piva, StringComparison.Ordinal));
        var candidate = righe
            .Take(Math.Max(indice, 1))
            .Where(r => r.Length is >= 3 and <= 80)
            .Where(r => !Regex.IsMatch(r, @"^(fattura|invoice|data|numero|pag|cliente|spett)", Opt))
            .ToList();

        return candidate.FirstOrDefault(r => FormaGiuridica.IsMatch(r))
               ?? candidate.FirstOrDefault(r => r.Any(char.IsLetter));
    }

    // --- Comuni

    private static DateOnly? TrovaScadenza(string testo) =>
        Scadenza.Match(testo) is { Success: true } s ? ParseData(s.Groups[1].Value) : null;

    private static string? TrovaCup(string testo) =>
        Cup.Match(testo) is { Success: true } c ? c.Groups[1].Value.ToUpperInvariant() : null;

    private static DateOnly? ParseData(string valore)
    {
        var parti = valore.Split('/', '.', '-');
        if (parti.Length != 3 || !int.TryParse(parti[0], out var g) || !int.TryParse(parti[1], out var m) ||
            !int.TryParse(parti[2], out var a)) return null;
        if (a < 100) a += 2000;
        if (a is < 2000 or > 2100 || m is < 1 or > 12 || g < 1 || g > DateTime.DaysInMonth(a, m)) return null;
        return new DateOnly(a, m, g);
    }
}
