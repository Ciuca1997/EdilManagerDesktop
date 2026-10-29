using System.Globalization;
using System.Text;
using EdilManager.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EdilManager.Infrastructure.Services;

public static class PresenzaExportService
{
    public static byte[] ExportMatriceCsv(IEnumerable<Dipendente> dipendenti, IEnumerable<Presenza> presenze, int anno, int mese)
    {
        var giorni = Enumerable.Range(1, DateTime.DaysInMonth(anno, mese)).ToList();
        var presenzeList = presenze.ToList();

        var sb = new StringBuilder();

        // Intestazione: Dipendente | 1 | 2 | ... | 31 | Totale Ord. | Totale Straord. | Totale
        var header = new List<string> { "Dipendente" };
        header.AddRange(giorni.Select(g => g.ToString()));
        header.Add("Tot. Ord.");
        header.Add("Tot. Straord.");
        header.Add("Totale");
        sb.AppendLine(string.Join(";", header));

        foreach (var dip in dipendenti.OrderBy(d => d.Cognome).ThenBy(d => d.Nome))
        {
            var riga = new List<string> { $"{dip.Cognome} {dip.Nome}" };
            var presenzeDelDip = presenzeList.Where(p => p.DipendenteId == dip.Id).ToList();

            decimal totOrd = 0, totStraord = 0;
            foreach (var g in giorni)
            {
                var p = presenzeDelDip.FirstOrDefault(p => p.Data.Day == g);
                if (p != null)
                {
                    var tot = p.OreOrdinarie + p.OreStatordinarie;
                    riga.Add(tot.ToString("0.##"));
                    totOrd += p.OreOrdinarie;
                    totStraord += p.OreStatordinarie;
                }
                else
                {
                    riga.Add("");
                }
            }

            riga.Add(totOrd.ToString("0.##"));
            riga.Add(totStraord.ToString("0.##"));
            riga.Add((totOrd + totStraord).ToString("0.##"));
            sb.AppendLine(string.Join(";", riga));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] ExportMatricePdf(IEnumerable<Dipendente> dipendenti, IEnumerable<Presenza> presenze, int anno, int mese)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var giorni = Enumerable.Range(1, DateTime.DaysInMonth(anno, mese)).ToList();
        var presenzeList = presenze.ToList();
        var dipendentiList = dipendenti.OrderBy(d => d.Cognome).ThenBy(d => d.Nome).ToList();
        var nomeMese = new DateTimeFormatInfo().GetMonthName(mese);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(15);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Foglio Presenze — {nomeMese} {anno}")
                        .FontSize(13).Bold();
                    col.Item().PaddingBottom(6);
                });

                page.Content().Table(table =>
                {
                    // Colonna nome + un colonna per giorno + 3 colonne totali
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(90);  // Nome dipendente
                        foreach (var _ in giorni)
                            c.RelativeColumn();
                        c.ConstantColumn(28);  // Tot. Ord.
                        c.ConstantColumn(28);  // Tot. Str.
                        c.ConstantColumn(28);  // Totale
                    });

                    // Intestazione giorni
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Dipendente").Bold();

                        foreach (var g in giorni)
                        {
                            var data = new DateOnly(anno, mese, g);
                            var isWeekend = data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday;
                            var bg = isWeekend ? Colors.Grey.Lighten1 : Colors.Grey.Lighten2;
                            header.Cell().Background(bg).Padding(2).AlignCenter().Text(g.ToString()).Bold();
                        }

                        header.Cell().Background(Colors.Blue.Lighten4).Padding(2).AlignCenter().Text("Ord.").Bold();
                        header.Cell().Background(Colors.Orange.Lighten4).Padding(2).AlignCenter().Text("Str.").Bold();
                        header.Cell().Background(Colors.Green.Lighten4).Padding(2).AlignCenter().Text("Tot.").Bold();
                    });

                    // Righe dipendenti
                    foreach (var dip in dipendentiList)
                    {
                        var presenzeDelDip = presenzeList.Where(p => p.DipendenteId == dip.Id).ToList();
                        decimal totOrd = 0, totStraord = 0;

                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(3).Text($"{dip.Cognome} {dip.Nome}");

                        foreach (var g in giorni)
                        {
                            var data = new DateOnly(anno, mese, g);
                            var isWeekend = data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday;
                            var p = presenzeDelDip.FirstOrDefault(x => x.Data.Day == g);

                            string testo;
                            string bgColor;

                            if (p != null)
                            {
                                var tot = p.OreOrdinarie + p.OreStatordinarie;
                                testo = tot.ToString("0.##");
                                bgColor = p.OreStatordinarie > 0 ? Colors.Orange.Lighten4 : Colors.White;
                                totOrd += p.OreOrdinarie;
                                totStraord += p.OreStatordinarie;
                            }
                            else
                            {
                                testo = "";
                                bgColor = isWeekend ? Colors.Grey.Lighten3 : Colors.White;
                            }

                            table.Cell().Background(bgColor)
                                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(2).AlignCenter().Text(testo);
                        }

                        table.Cell().Background(Colors.Blue.Lighten5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2).AlignCenter().Text(totOrd.ToString("0.##")).Bold();
                        table.Cell().Background(Colors.Orange.Lighten5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2).AlignCenter().Text(totStraord.ToString("0.##")).Bold();
                        table.Cell().Background(Colors.Green.Lighten5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(2).AlignCenter().Text((totOrd + totStraord).ToString("0.##")).Bold();
                    }
                });

                page.Footer().AlignRight()
                    .Text(t =>
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
}
