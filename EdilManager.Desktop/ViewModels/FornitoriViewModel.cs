using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed class FornitoreRow(FornitoreListDto dto)
{
    public int Id { get; } = dto.Id;
    public string Denominazione { get; } = dto.Denominazione;
    public string PartitaIva { get; } = dto.PartitaIva;
    public string FattureText { get; } = dto.FattureCount switch
    {
        0 => "Nessuna fattura",
        1 => "1 fattura",
        var n => $"{n} fatture",
    };

    public bool Corrisponde(string testo) =>
        Denominazione.Contains(testo, StringComparison.OrdinalIgnoreCase) ||
        PartitaIva.Contains(testo, StringComparison.OrdinalIgnoreCase);
}

public partial class FornitoriViewModel : ObservableObject, IReloadable
{
    private readonly FornitoreService _service;
    private readonly DetailOpener _opener;
    private List<FornitoreRow> _all = [];

    public FornitoriViewModel(FornitoreService service, DetailOpener opener)
    {
        _service = service;
        _opener = opener;
        _ = ReloadAsync();
    }

    public ObservableCollection<FornitoreRow> Items { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(NoResults))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(NoResults), nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => !IsLoading && !HasError && _all.Count == 0;
    public bool NoResults => !IsLoading && !HasError && _all.Count > 0 && Items.Count == 0;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task ReloadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            _all = (await _service.GetAllAsync()).Select(d => new FornitoreRow(d)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento fornitori fallito", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(NoResults));
        }
    }

    private void ApplyFilter()
    {
        var testo = SearchText.Trim();
        Items.Clear();
        foreach (var row in testo.Length == 0 ? _all : _all.Where(r => r.Corrisponde(testo))) Items.Add(row);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(NoResults));
    }

    [RelayCommand]
    private Task OpenAsync(FornitoreRow row) => _opener.OpenFornitoreAsync(row.Id);
}
