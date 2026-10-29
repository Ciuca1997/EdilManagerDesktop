using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EdilManager.Desktop.ViewModels;

public sealed record NavItem(string Title, string Icon, Func<object> CreatePage);

public partial class MainViewModel : ObservableObject, INavigationService
{
    private readonly Stack<object> _history = new();
    private readonly UpdateService _updates;

    public MainViewModel(IServiceProvider services, UpdateService updates)
    {
        _updates = updates;
        NavItems =
        [
            new NavItem("Dashboard", "ViewDashboardOutline", () => new PlaceholderViewModel("Dashboard", "ViewDashboardOutline")),
            new NavItem("Dipendenti", "AccountGroupOutline", () => services.GetRequiredService<DipendentiViewModel>()),
            new NavItem("Fatture", "ReceiptTextOutline", () => new PlaceholderViewModel("Fatture", "ReceiptTextOutline")),
            new NavItem("Cantieri", "Crane", () => new PlaceholderViewModel("Cantieri", "Crane")),
        ];
        SelectedNavItem = NavItems[0];
    }

    public IReadOnlyList<NavItem> NavItems { get; }

    public string VersionText => $"Versione {AppInfo.Version}";

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    [ObservableProperty]
    private object? _currentPage;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null) return;
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

    public Task OnWindowLoadedAsync() => _updates.CheckAsync(interactive: false);

    [RelayCommand]
    private Task CheckUpdatesAsync() => _updates.CheckAsync(interactive: true);
}
