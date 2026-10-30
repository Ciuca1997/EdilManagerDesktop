using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EdilManager.Desktop.ViewModels;

public sealed record NavItem(string Title, string Icon, Func<object> CreatePage);

public partial class MainViewModel : ObservableObject, INavigationService
{
    private readonly Stack<object> _history = new();
    private readonly UpdateService _updates;
    private readonly AziendaService _aziendaService;
    private readonly ImpostazioniService _impostazioni;
    private readonly DialogService _dialogs;

    public MainViewModel(IServiceProvider services, UpdateService updates, AziendaService aziendaService,
        ImpostazioniService impostazioni, DialogService dialogs)
    {
        _updates = updates;
        _aziendaService = aziendaService;
        _impostazioni = impostazioni;
        _dialogs = dialogs;
        NavItems =
        [
            new NavItem("Dashboard", "ViewDashboardOutline", () => services.GetRequiredService<DashboardViewModel>()),
            new NavItem("Dipendenti", "AccountGroupOutline", () => services.GetRequiredService<DipendentiViewModel>()),
            new NavItem("Presenze", "CalendarMonthOutline", () => services.GetRequiredService<PresenzeViewModel>()),
            new NavItem("Fatture", "ReceiptTextOutline", () => services.GetRequiredService<FattureViewModel>()),
            new NavItem("Fornitori", "StorefrontOutline", () => services.GetRequiredService<FornitoriViewModel>()),
            new NavItem("Cantieri", "Crane", () => services.GetRequiredService<CantieriViewModel>()),
        ];
        BottomNavItems =
        [
            new NavItem("Impostazioni", "CogOutline", () => services.GetRequiredService<ImpostazioniViewModel>()),
        ];
        SelectedNavItem = NavItems[0];
    }

    public IReadOnlyList<NavItem> NavItems { get; }

    // Voci in fondo alla barra laterale (sopra "Controlla aggiornamenti"): hanno una selezione propria,
    // ma è sempre selezionata una sola voce tra le due liste.
    public IReadOnlyList<NavItem> BottomNavItems { get; }

    public string VersionText => $"Versione {AppInfo.Version}";

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private NavItem? _selectedBottomItem;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null) return;
        SelectedBottomItem = null;
        _history.Clear();
        CurrentPage = value.CreatePage();
    }

    partial void OnSelectedBottomItemChanged(NavItem? value)
    {
        if (value is null) return;
        SelectedNavItem = null;
        _history.Clear();
        CurrentPage = value.CreatePage();
    }

    public void Push(object page)
    {
        if (CurrentPage is not null) _history.Push(CurrentPage);
        CurrentPage = page;
    }

    public void Pop()
    {
        if (_history.Count == 0) return;
        CurrentPage = _history.Pop();
        if (CurrentPage is IReloadable reloadable) _ = reloadable.ReloadAsync();
    }

    // Nome dell'azienda mostrato nell'intestazione della barra laterale; finché i dati non ci sono resta "EdilManager".
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitoloFinestra))]
    private string _nomeAzienda = NomePredefinito;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPartitaIva))]
    private string _partitaIvaTesto = string.Empty;

    public bool HasPartitaIva => PartitaIvaTesto.Length > 0;
    public string TitoloFinestra => NomeAzienda == NomePredefinito ? NomePredefinito : $"{NomePredefinito} — {NomeAzienda}";

    private const string NomePredefinito = "EdilManager";
    private AziendaDto? _azienda;

    private void ApplicaAzienda(AziendaDto? azienda)
    {
        _azienda = azienda;
        NomeAzienda = azienda is null ? NomePredefinito : azienda.RagioneSociale;
        PartitaIvaTesto = azienda?.PartitaIva is { Length: > 0 } piva ? $"P.IVA {piva}" : string.Empty;
    }

    private async Task RicaricaAziendaAsync()
    {
        try
        {
            var azienda = await _aziendaService.GetAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => ApplicaAzienda(azienda));
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento dati azienda fallito", ex);
        }
    }

    // Al primo avvio (nessun dato e mai rimandato) propone il modulo; "Più tardi" lo rimanda alle Impostazioni senza riproporlo.
    private async Task ChiediAziendaSeMancaAsync()
    {
        try
        {
            if (_azienda is not null || _impostazioni.Corrente.AziendaSaltata) return;

            var form = new AziendaFormViewModel(_aziendaService, _dialogs, null, primoAvvio: true);
            if (await _dialogs.ShowAsync(form) is true) return;

            _impostazioni.Modifica(i => i.AziendaSaltata = true);
            AppLog.Trace("Dati azienda: modulo rimandato dall'utente");
        }
        catch (Exception ex)
        {
            AppLog.Error("Modulo dei dati azienda non mostrabile", ex);
        }
    }

    public async Task OnWindowLoadedAsync()
    {
        _aziendaService.Cambiata += (_, _) => _ = RicaricaAziendaAsync();
        await RicaricaAziendaAsync();
        await ChiediAziendaSeMancaAsync();
        await _updates.CheckAsync(interactive: false);
    }

    [RelayCommand]
    private Task CheckUpdatesAsync() => _updates.CheckAsync(interactive: true);
}
