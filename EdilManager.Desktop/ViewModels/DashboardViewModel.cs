using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed record StatCard(string Label, string Value, string Icon, string? Detail = null);

public partial class DashboardViewModel : ObservableObject, IReloadable
{
    private static readonly string[] Mesi =
    [
        "", "gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno",
        "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre",
    ];

    private readonly DashboardService _service;

    public DashboardViewModel(DashboardService service)
    {
        _service = service;
        _ = ReloadAsync();
    }

    public ObservableCollection<StatCard> Cards { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;

    public async Task ReloadAsync()
    {
        ErrorMessage = null;
        try
        {
            var s = await _service.GetSummaryAsync();
            var mese = Mesi[DateTime.Today.Month];

            Cards.Clear();
            Cards.Add(new StatCard("Dipendenti", s.Dipendenti.ToString(), "AccountGroupOutline"));
            Cards.Add(new StatCard("Cantieri", s.Cantieri.ToString(), "Crane"));
            Cards.Add(new StatCard("Fatture", s.Fatture.ToString(), "ReceiptTextOutline",
                $"Totale {s.TotaleFatture.ToString("N2", Formats.Italian)} €"));
            Cards.Add(new StatCard($"Ore a {mese}",
                (s.OreOrdinarieMese + s.OreStraordinarieMese).ToString("0.#", Formats.Italian), "ClockOutline",
                $"di cui {s.OreStraordinarieMese.ToString("0.#", Formats.Italian)} straordinarie"));
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento dashboard fallito", ex);
            ErrorMessage = ex.Message;
        }
    }
}
