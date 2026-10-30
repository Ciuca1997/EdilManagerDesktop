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
    // 15 caratteri: lettera, 2 cifre, lettera, 11 cifre. Ammessi "CUP n.", "CUP cantiere Nr", "CUP=", ecc.
    private static readonly Regex Cup =
        new(@"\bCUP\b(?:\s+(?:cantiere|n\.?|nr\.?|n°))*\s*[:=\-]?\s*([A-Z]\d{2}[A-Z]\d{11})\b", Opt);
    private static readonly Regex ModalitaPagamento = new(@"\b(MP\d{2})\b");
    private static readonly Regex FormaGiuridica =
        new(@"\b(s\.?\s?r\.?\s?l\.?s?|s\.?\s?p\.?\s?a\.?|s\.?\s?n\.?\s?c\.?|s\.?\s?a\.?\s?s\.?|s\.?\s?c\.?\s?a?\s?r?\.?\s?l?\.?|ditta|impresa|di\s+[A-Z][a-z]+\s+[A-Z][a-z]+)\b", Opt);

    // "TD01 fattura 266 30-12-2023 SUBM70N": tipo, descrizione, numero, data, codice destinatario.
    // Si cerca nel testo appiattito perché la descrizione del tipo (es. TD24) può andare a capo.
    private static readonly Regex IntestazioneDocumento =
        new(@"\b(TD\d{2})\b.*?(\S+)\s+(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4})\s+\S+", Opt);
    private static readonly Regex TokenImporto = new(@"^-?(?:\d{1,3}(?:\.\d{3})*|\d+),\d{2}$");
    // Il PDF a volte spezza un importo su due righe: "12.000,0" e "0".
    private static readonly Regex ImportoSpezzato = new(@"(\d,\d)\s+(\d)(?=\s|$)");
    private static readonly decimal[] AliquoteIva = [0m, 4m, 5m, 10m, 22m];
    private static readonly Regex AliquotaRiga = new(@"^(?:\d{1,2},\d{2}|N\d(?:\.\d)?)$", Opt);
    // Quantità e prezzo unitario possono avere più di due decimali.
    private static readonly Regex NumeroRiga = new(@"^-?(?:\d{1,3}(?:\.\d{3})+|\d+),\d+$");
    private static readonly Regex Sconto = new(@"^[+-]?\d+(?:[.,]\d+)?%$");

    private static readonly string[] EtichetteAnagrafica =
    [
        "Identificativo fiscale ai fini IVA", "Codice fiscale", "Denominazione", "Cognome nome", "Nome", "Cognome", "Titolo",
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

    // rigoroso: un PDF non standard è accettato solo se imponibile, imposta e totale sono tutti trovati e la somma torna
    // (serve quando si scansionano cartelle intere, dove ci sono anche contratti e preventivi).
    public static (Fornitore fornitore, Fattura fattura, string? cup) Parse(Stream pdf, bool rigoroso = false)
    {
        var testo = EstraiTesto(pdf);
        if (string.IsNullOrWhiteSpace(testo))
            throw new InvalidOperationException("Il PDF non contiene testo leggibile (forse è una scansione).");

        var righe = testo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var piatto = Regex.Replace(testo, @"\s+", " ");

        return IsVisualizzazioneStandard(piatto) ? ParseVisualizzazione(righe, piatto) : ParseGenerico(righe, piatto, rigoroso);
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
                            ?? Campo(cedente, "Cognome nome")
                            ?? $"{Campo(cedente, "Nome")} {Campo(cedente, "Cognome")}".Trim();

        var intestazione = IntestazioneDocumento.Match(piatto);
        var numero = intestazione.Success ? intestazione.Groups[2].Value : null;
        var data = intestazione.Success ? ParseData(intestazione.Groups[3].Value) : null;
        var documento = TotaleDocumento(piatto);
        var totale = documento?.totale;
        var riepilogo = documento is null ? null : Riepilogo(piatto, documento.Value.totale, documento.Value.altri);

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
            Righe = Righe(righe, riepilogo.Value.imponibile),
        };

        return (fornitore, fattura, TrovaCup(piatto));
    }

    private static (Fornitore, Fattura, string?) ParseGenerico(string[] righe, string piatto, bool rigoroso)
    {
        var piva = TrovaPartitaIva(piatto);
        var (numero, dataNumero) = TrovaNumero(piatto);
        var data = dataNumero ?? TrovaData(piatto);
        var totale = TrovaImporto(piatto, EtichetteTotale);
        var imponibile = TrovaImporto(piatto, EtichetteImponibile);
        var imposta = TrovaImporto(piatto, EtichetteImposta);

        VerificaDatiMancanti((piva, "partita IVA"), (numero, "numero"), (data, "data"), (totale, "totale"));
        if (rigoroso && (imponibile is null || imposta is null || Math.Abs(imponibile.Value + imposta.Value - totale!.Value) > 0.02m))
            throw new InvalidOperationException("Non sembra una fattura: imponibile, imposta e totale non sono coerenti.");

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

    // Dopo "Importo bollo / Sconto / Arr. / Totale documento" seguono i valori (bollo, sconto, arrotondamento, totale):
    // il totale è l'ultimo della serie di importi consecutivi; dopo ci può essere altro (es. importo della ritenuta).
    // "altri" sono gli importi che lo precedono, utili per spiegare uno scarto con il riepilogo IVA.
    private static (decimal totale, decimal altri)? TotaleDocumento(string piatto)
    {
        var m = Regex.Match(piatto, @"Arr\.\s+Totale documento", Opt);
        var i = m.Success ? m.Index + m.Length : piatto.LastIndexOf("Totale documento", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        if (!m.Success) i += "Totale documento".Length;

        var importi = new List<decimal>();
        foreach (var token in piatto[i..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("SI", StringComparison.OrdinalIgnoreCase)) continue; // bollo virtuale
            if (!TokenImporto.IsMatch(token)) break;
            importi.Add(LeggiImporto(token));
        }
        return importi.Count > 0 ? (importi[^1], importi[..^1].Sum(Math.Abs)) : null;
    }

    // Righe del riepilogo IVA: %IVA, [spese accessorie], imponibile, imposta; le righe esenti possono non avere l'aliquota
    // ("N1 25,01 0"). Gli importi del blocco si leggono in ordine e si cerca la sequenza di righe coerenti
    // (imposta = imponibile × aliquota) la cui somma dà il totale del documento.
    private static (decimal imponibile, decimal imposta)? Riepilogo(string piatto, decimal totale, decimal tolleranza)
    {
        var inizio = piatto.IndexOf("RIEPILOGHI IVA", StringComparison.OrdinalIgnoreCase);
        if (inizio < 0) return null;
        var blocco = piatto[inizio..];
        var fine = blocco.IndexOf("Importo bollo", StringComparison.OrdinalIgnoreCase);
        if (fine < 0) fine = blocco.LastIndexOf("Totale documento", StringComparison.OrdinalIgnoreCase);
        if (fine > 0) blocco = blocco[..fine];

        var importi = Importi(blocco);
        (decimal imponibile, decimal imposta)? migliore = null;
        var differenza = decimal.MaxValue;

        void Cerca(int pos, decimal imponibile, decimal imposta, int righe)
        {
            if (righe > 0)
            {
                var d = Math.Abs(totale - imponibile - imposta);
                // A parità di scarto vince la soluzione con più righe (meno importi scartati).
                if (d < differenza || (d == differenza && migliore is not null &&
                                       imponibile + imposta > migliore.Value.imponibile + migliore.Value.imposta))
                {
                    differenza = d;
                    migliore = (imponibile, imposta);
                }
                if (d == 0m) return;
            }
            for (var p = pos; p < importi.Count; p++)
            {
                // %IVA, imponibile, imposta
                if (p + 2 < importi.Count && Coerente(importi[p], importi[p + 1], importi[p + 2]))
                    Cerca(p + 3, imponibile + importi[p + 1], imposta + importi[p + 2], righe + 1);
                if (differenza == 0m) return;
                // %IVA, spese accessorie, imponibile, imposta
                if (p + 3 < importi.Count && Coerente(importi[p], importi[p + 2], importi[p + 3]))
                    Cerca(p + 4, imponibile + importi[p + 2], imposta + importi[p + 3], righe + 1);
                if (differenza == 0m) return;
                // imponibile, imposta nulla (operazione esente o fuori campo, senza aliquota)
                if (p + 1 < importi.Count && importi[p] != 0m && importi[p + 1] == 0m)
                    Cerca(p + 2, imponibile + importi[p], imposta, righe + 1);
                if (differenza == 0m) return;
            }
        }
        Cerca(0, 0m, 0m, 0);

        // Il totale può includere bollo o arrotondamenti: si accetta solo lo scarto che essi spiegano.
        return migliore is not null && differenza <= tolleranza + 0.02m ? migliore : null;
    }

    private static bool Coerente(decimal aliquota, decimal imponibile, decimal imposta) =>
        AliquoteIva.Contains(aliquota) && Math.Abs(Math.Round(imponibile * aliquota / 100, 2) - imposta) <= 0.05m;

    // Righe di dettaglio: testo descrittivo (anche su più righe) che si chiude con "[qtà] [prezzo] [UM] [sconto%] aliquota totale".
    // L'unità di misura può andare a capo. Si tengono solo se la somma dei totali di riga torna con l'imponibile.
    private static List<RigaFattura> Righe(string[] righe, decimal imponibile)
    {
        var inizio = Array.FindIndex(righe, r => r.StartsWith("Cod. articolo", StringComparison.OrdinalIgnoreCase));
        var fine = Array.FindIndex(righe, r => r.StartsWith("RIEPILOGHI IVA", StringComparison.OrdinalIgnoreCase));
        if (inizio < 0 || fine <= inizio) return [];

        var blocco = ImportoSpezzato.Replace(string.Join('\n', righe[inizio..fine]), "$1$2");
        var risultato = new List<RigaFattura>();
        var accumulo = new List<string>();
        var intestazione = false;
        decimal? orfano = null;

        foreach (var riga in blocco.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // L'intestazione delle colonne si ripete a ogni pagina e si chiude con "Prezzo totale".
            if (riga.StartsWith("Cod. articolo", StringComparison.OrdinalIgnoreCase)) intestazione = true;
            if (intestazione)
            {
                if (riga.Contains("Prezzo totale", StringComparison.OrdinalIgnoreCase)) intestazione = false;
                continue;
            }

            var token = riga.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string aliquota;
            decimal totale;
            string[] resto;
            if (token.Length >= 2 && AliquotaRiga.IsMatch(token[^2]) && TokenImporto.IsMatch(token[^1]))
                (aliquota, totale, resto) = (token[^2], LeggiImporto(token[^1]), token[..^2]);
            else if (orfano is not null && token.Length >= 1 && AliquotaRiga.IsMatch(token[^1]))
                (aliquota, totale, resto, orfano) = (token[^1], orfano.Value, token[..^1], null);
            else if (token.Length == 1 && TokenImporto.IsMatch(token[0]))
            {
                // A fondo pagina il PDF può stampare il totale di una riga prima della riga stessa.
                orfano = LeggiImporto(token[0]);
                continue;
            }
            else
            {
                accumulo.Add(riga);
                continue;
            }

            risultato.Add(CreaRiga(accumulo.SelectMany(a => a.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Concat(resto).ToList(),
                aliquota, totale));
            accumulo.Clear();
        }

        // Il testo rimasto dopo l'ultima riga con prezzo sono note (cantiere, informazioni documento), non righe.
        return risultato.Count > 0 && Math.Abs(risultato.Sum(r => r.PrezzoTotale) - imponibile) <= 0.05m
            ? risultato
            : [];
    }

    // Da destra: sconto%, poi l'ultima coppia "quantità prezzo" seguita al massimo da qualche parola (unità di misura);
    // se c'è un solo numero in fondo è il prezzo unitario. Tutto ciò che precede è descrizione.
    private static RigaFattura CreaRiga(List<string> parole, string aliquota, decimal totale)
    {
        decimal? sconto = null;
        if (parole.Count > 0 && Sconto.IsMatch(parole[^1]))
        {
            sconto = Math.Abs(decimal.Parse(parole[^1].TrimEnd('%').Replace(',', '.'), CultureInfo.InvariantCulture));
            parole.RemoveAt(parole.Count - 1);
        }

        decimal? quantita = null, prezzo = null;
        string? um = null;
        var k = -1;
        for (var i = parole.Count - 2; i >= 0 && i >= parole.Count - 5; i--)
        {
            if (!NumeroRiga.IsMatch(parole[i]) || !NumeroRiga.IsMatch(parole[i + 1])) continue;
            if (parole.Skip(i + 2).Any(p => NumeroRiga.IsMatch(p))) continue;
            k = i;
            break;
        }

        if (k >= 0)
        {
            quantita = LeggiNumero(parole[k]);
            prezzo = LeggiNumero(parole[k + 1]);
            um = parole.Count > k + 2 ? string.Join(' ', parole.Skip(k + 2)) : null;
            parole = parole[..k];
        }
        else if (parole.Count > 0 && NumeroRiga.IsMatch(parole[^1]))
        {
            prezzo = LeggiNumero(parole[^1]);
            parole = parole[..^1];
        }

        return new RigaFattura
        {
            Descrizione = string.Join(' ', parole),
            Quantita = quantita == 0 ? null : quantita,
            PrezzoUnitario = prezzo == 0 ? null : prezzo,
            UnitaMisura = um,
            Sconto = sconto == 0 ? null : sconto,
            AliquotaIVA = aliquota.StartsWith('N') ? 0m : LeggiImporto(aliquota),
            PrezzoTotale = totale,
        };
    }

    private static decimal LeggiNumero(string valore) =>
        decimal.Parse(valore.Replace(".", "").Replace(',', '.'), NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);

    private static List<decimal> Importi(string testo) =>
        ImportoSpezzato.Replace(testo, "$1$2")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => TokenImporto.IsMatch(t) || t == "0")
            .Select(t => t == "0" ? 0m : LeggiImporto(t)).ToList();

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
