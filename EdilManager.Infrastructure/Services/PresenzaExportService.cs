using System.Globalization;
using System.Text;
using EdilManager.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EdilManager.Infrastructure.Services;

// Fogli presenze in PDF e CSV: la matrice del mese (tutti i dipendenti, anche solo le ore di un cantiere) e il foglio di un
// singolo dipendente su un intervallo di mesi. Un dipendente può avere più presenze nello stesso giorno (cantieri diversi):
// nella matrice le ore del giorno si sommano. Il CSV usa ";" e la virgola decimale, e il BOM perché Excel legga gli accenti.
public static class PresenzaExportService
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static readonly string[] GiorniBreve = ["dom", "lun", "mar", "mer", "gio", "ven", "sab"];

    public static byte[] ExportMatriceCsv(IEnumerable<Dipendente> dipendenti, IEnumerable<Presenza> presenze, int anno, int mese)
    {
        var giorni = Enumerable.Range(1, DateTime.DaysInMonth(anno, mese)).ToList();
        var perDipendente = presenze.ToLookup(p => p.DipendenteId);

        var righe = new List<string>
        {
            string.Join(";", new[] { "Dipendente" }.Concat(giorni.Select(g => g.ToString())).Concat(["Tot. Ord.", "Tot. Straord.", "Totale"])),
        };

        foreach (var dip in dipendenti.OrderBy(d => d.Cognome).ThenBy(d => d.Nome))
        {
            var sue = perDipendente[dip.Id].ToList();
            var riga = new List<string> { Csv($"{dip.Cognome} {dip.Nome}") };
            foreach (var g in giorni)
            {
                var tot = sue.Where(p => p.Data.Day == g).Sum(p => p.OreOrdinarie + p.OreStatordinarie);
                riga.Add(tot == 0 ? string.Empty : Ore(tot));
            }
            var ord = sue.Sum(p => p.OreOrdinarie);
            var str = sue.Sum(p => p.OreStatordinarie);
            riga.AddRange([Ore(ord), Ore(str), Ore(ord + str)]);
            righe.Add(string.Join(";", riga));
        }

        return ConBom(string.Join("\r\n", righe) + "\r\n");
    }

    // sottotitolo: riga sotto il titolo (es. il cantiere a cui si limitano le ore).
    public static byte[] ExportMatricePdf(IEnumerable<Dipendente> dipendenti, IEnumerable<Presenza> presenze, int anno, int mese,
        string? sottotitolo = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var giorni = Enumerable.Range(1, DateTime.DaysInMonth(anno, mese)).ToList();
        var perDipendente = presenze.ToLookup(p => p.DipendenteId);
        var dipendentiList = dipendenti.OrderBy(d => d.Cognome).ThenBy(d => d.Nome).ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(15);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Foglio presenze — {NomeMese(mese)} {anno}").FontSize(13).Bold();
                    if (!string.IsNullOrWhiteSpace(sottotitolo))
                        col.Item().Text(sottotitolo).FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingBottom(6);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(90);
                        foreach (var _ in giorni) c.RelativeColumn();
                        c.ConstantColumn(28);
                        c.ConstantColumn(28);
                        c.ConstantColumn(28);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Dipendente").Bold();
                        foreach (var g in giorni)
                        {
                            var weekend = new DateOnly(anno, mese, g).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                            header.Cell().Background(weekend ? Colors.Grey.Lighten1 : Colors.Grey.Lighten2)
                                .Padding(2).AlignCenter().Text(g.ToString()).Bold();
                        }
                        header.Cell().Background(Colors.Blue.Lighten4).Padding(2).AlignCenter().Text("Ord.").Bold();
                        header.Cell().Background(Colors.Orange.Lighten4).Padding(2).AlignCenter().Text("Str.").Bold();
                        header.Cell().Background(Colors.Green.Lighten4).Padding(2).AlignCenter().Text("Tot.").Bold();
                    });

                    foreach (var dip in dipendentiList)
                    {
                        var sue = perDipendente[dip.Id].ToList();
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text($"{dip.Cognome} {dip.Nome}");

                        foreach (var g in giorni)
                        {
                            var delGiorno = sue.Where(p => p.Data.Day == g).ToList();
                            var weekend = new DateOnly(anno, mese, g).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                            var tot = delGiorno.Sum(p => p.OreOrdinarie + p.OreStatordinarie);
                            var sfondo = delGiorno.Any(p => p.OreStatordinarie > 0) ? Colors.Orange.Lighten4
                                : delGiorno.Count == 0 && weekend ? Colors.Grey.Lighten3 : Colors.White;

                            table.Cell().Background(sfondo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(2).AlignCenter().Text(delGiorno.Count == 0 ? string.Empty : Ore(tot));
                        }

                        var ord = sue.Sum(p => p.OreOrdinarie);
                        var str = sue.Sum(p => p.OreStatordinarie);
                        table.Cell().Background(Colors.Blue.Lighten5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2).AlignCenter().Text(Ore(ord)).Bold();
                        table.Cell().Background(Colors.Orange.Lighten5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2).AlignCenter().Text(Ore(str)).Bold();
                        table.Cell().Background(Colors.Green.Lighten5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2).AlignCenter().Text(Ore(ord + str)).Bold();
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Pagina ");
                    t.CurrentPageNumber();
                    t.Span(" di ");
                    t.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    // Foglio di un dipendente: una riga per presenza (data, cantiere, ore), subtotale per mese e riepilogo per cantiere.
    public static byte[] ExportDipendenteCsv(Dipendente dipendente, IEnumerable<Presenza> presenze, DateOnly da, DateOnly a)
    {
        var elenco = Ordina(presenze);
        var righe = new List<string>
        {
            Csv($"Foglio presenze - {dipendente.Cognome} {dipendente.Nome}"),
            Csv($"Periodo: {da.ToString("dd/MM/yyyy", It)} - {a.ToString("dd/MM/yyyy", It)}"),
            string.Empty,
            "Data;Giorno;Cantiere;Ore ordinarie;Ore straordinarie;Totale",
        };

        foreach (var p in elenco)
            righe.Add(string.Join(";", [
                p.Data.ToString("dd/MM/yyyy", It), GiorniBreve[(int)p.Data.DayOfWeek], Csv(NomeCantiere(p)),
                Ore(p.OreOrdinarie), Ore(p.OreStatordinarie), Ore(p.OreOrdinarie + p.OreStatordinarie),
            ]));

        righe.Add(string.Empty);
        righe.Add("Riepilogo per mese;;;Ore ordinarie;Ore straordinarie;Totale");
        foreach (var g in elenco.GroupBy(p => (p.Data.Year, p.Data.Month)))
            righe.Add(string.Join(";", [
                $"{NomeMese(g.Key.Month)} {g.Key.Year}", string.Empty, string.Empty,
                Ore(g.Sum(p => p.OreOrdinarie)), Ore(g.Sum(p => p.OreStatordinarie)), Ore(g.Sum(p => p.OreOrdinarie + p.OreStatordinarie)),
            ]));

        righe.Add(string.Empty);
        righe.Add("Riepilogo per cantiere;Giorni;;Ore ordinarie;Ore straordinarie;Totale");
        foreach (var g in elenco.GroupBy(NomeCantiere).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
            righe.Add(string.Join(";", [
                Csv(g.Key), g.Select(p => p.Data).Distinct().Count().ToString(), string.Empty,
                Ore(g.Sum(p => p.OreOrdinarie)), Ore(g.Sum(p => p.OreStatordinarie)), Ore(g.Sum(p => p.OreOrdinarie + p.OreStatordinarie)),
            ]));

        righe.Add(string.Join(";", [
            "Totale periodo", string.Empty, string.Empty,
            Ore(elenco.Sum(p => p.OreOrdinarie)), Ore(elenco.Sum(p => p.OreStatordinarie)),
            Ore(elenco.Sum(p => p.OreOrdinarie + p.OreStatordinarie)),
        ]));

        return ConBom(string.Join("\r\n", righe) + "\r\n");
    }

    public static byte[] ExportDipendentePdf(Dipendente dipendente, IEnumerable<Presenza> presenze, DateOnly da, DateOnly a)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var elenco = Ordina(presenze);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Foglio presenze — {dipendente.Cognome} {dipendente.Nome}").FontSize(15).Bold();
                    col.Item().Text($"Periodo: {da.ToString("dd/MM/yyyy", It)} – {a.ToString("dd/MM/yyyy", It)}")
                        .FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Column(col =>
                {
                    if (elenco.Count == 0)
                    {
                        col.Item().PaddingTop(20).Text("Nessuna presenza registrata nel periodo.").FontColor(Colors.Grey.Darken1);
                        return;
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(62);   // Data
                            c.ConstantColumn(40);   // Giorno
                            c.RelativeColumn();     // Cantiere
                            c.ConstantColumn(52);   // Ord.
                            c.ConstantColumn(52);   // Str.
                            c.ConstantColumn(52);   // Tot.
                        });

                        table.Header(h =>
                        {
                            foreach (var (testo, destra) in new[] { ("Data", false), ("Giorno", false), ("Cantiere", false), ("Ordinarie", true), ("Straord.", true), ("Totale", true) })
                            {
                                var cella = h.Cell().Background(Colors.Grey.Lighten2).Padding(4);
                                (destra ? cella.AlignRight() : cella).Text(testo).Bold();
                            }
                        });

                        foreach (var mese in elenco.GroupBy(p => (p.Data.Year, p.Data.Month)))
                        {
                            table.Cell().ColumnSpan(6).Background(Colors.Blue.Lighten5).Padding(4)
                                .Text($"{NomeMese(mese.Key.Month)} {mese.Key.Year}").Bold();

                            foreach (var p in mese)
                            {
                                var weekend = p.Data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                                var sfondo = p.OreStatordinarie > 0 ? Colors.Orange.Lighten5 : weekend ? Colors.Grey.Lighten4 : Colors.White;
                                Riga(table, sfondo, p.Data.ToString("dd/MM/yyyy", It), GiorniBreve[(int)p.Data.DayOfWeek], NomeCantiere(p),
                                    Ore(p.OreOrdinarie), p.OreStatordinarie > 0 ? Ore(p.OreStatordinarie) : string.Empty,
                                    Ore(p.OreOrdinarie + p.OreStatordinarie), false);
                            }

                            Riga(table, Colors.Blue.Lighten5, string.Empty, string.Empty, $"Totale {NomeMese(mese.Key.Month).ToLower(It)}",
                                Ore(mese.Sum(p => p.OreOrdinarie)), Ore(mese.Sum(p => p.OreStatordinarie)),
                                Ore(mese.Sum(p => p.OreOrdinarie + p.OreStatordinarie)), true);
                        }
                    });

                    col.Item().PaddingTop(16).Text("Riepilogo per cantiere").FontSize(11).Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.ConstantColumn(44);
                            c.ConstantColumn(60);
                            c.ConstantColumn(60);
                            c.ConstantColumn(60);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Cantiere").Bold();
                            foreach (var t in new[] { "Giorni", "Ordinarie", "Straord.", "Totale" })
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(t).Bold();
                        });

                        foreach (var g in elenco.GroupBy(NomeCantiere).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
                            RigaRiepilogo(table, g.Key, g.Select(p => p.Data).Distinct().Count().ToString(),
                                Ore(g.Sum(p => p.OreOrdinarie)), Ore(g.Sum(p => p.OreStatordinarie)),
                                Ore(g.Sum(p => p.OreOrdinarie + p.OreStatordinarie)), false);

                        RigaRiepilogo(table, "Totale periodo", elenco.Select(p => p.Data).Distinct().Count().ToString(),
                            Ore(elenco.Sum(p => p.OreOrdinarie)), Ore(elenco.Sum(p => p.OreStatordinarie)),
                            Ore(elenco.Sum(p => p.OreOrdinarie + p.OreStatordinarie)), true);
                    });
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Pagina ");
                    t.CurrentPageNumber();
                    t.Span(" di ");
                    t.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void Riga(TableDescriptor table, string sfondo, string data, string giorno, string cantiere,
        string ord, string str, string tot, bool grassetto)
    {
        void Cella(string testo, bool destra)
        {
            var c = table.Cell().Background(sfondo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
            var t = (destra ? c.AlignRight() : c).Text(testo);
            if (grassetto) t.Bold();
        }
        Cella(data, false); Cella(giorno, false); Cella(cantiere, false); Cella(ord, true); Cella(str, true); Cella(tot, true);
    }

    private static void RigaRiepilogo(TableDescriptor table, string cantiere, string giorni, string ord, string str, string tot, bool grassetto)
    {
        void Cella(string testo, bool destra)
        {
            var c = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
            var t = (destra ? c.AlignRight() : c).Text(testo);
            if (grassetto) t.Bold();
        }
        Cella(cantiere, false); Cella(giorni, true); Cella(ord, true); Cella(str, true); Cella(tot, true);
    }

    private static List<Presenza> Ordina(IEnumerable<Presenza> presenze) =>
        presenze.OrderBy(p => p.Data).ThenBy(p => NomeCantiere(p), StringComparer.CurrentCultureIgnoreCase).ToList();

    private static string NomeCantiere(Presenza p) => p.Cantiere?.Nome ?? "Nessun cantiere";

    private static string NomeMese(int mese)
    {
        var nome = It.DateTimeFormat.GetMonthName(mese);
        return char.ToUpper(nome[0], It) + nome[1..];
    }

    private static string Ore(decimal ore) => ore.ToString("0.##", It);

    // Campo CSV con ";" come separatore: tra virgolette se contiene separatori, virgolette o a capo.
    private static string Csv(string testo) =>
        testo.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{testo.Replace("\"", "\"\"")}\"" : testo;

    private static byte[] ConBom(string testo) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(testo)];
}
