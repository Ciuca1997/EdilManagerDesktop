using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed class FatturaRow(FatturaListDto dto)
{
    public string Data { get; } = dto.DataDocumento.ToString("dd/MM/yyyy");
    public string Numero { get; } = dto.NumeroDocumento;
    public string Fornitore { get; } = dto.Fornitore;
    public string Cantiere { get; } = dto.Cantiere ?? "-";
    public string Tipo { get; } = dto.TipoDocumento;
    public string Totale { get; } = dto.TotaleDocumento.ToString("N2", Formats.Italian) + " €";
    public string Scadenza { get; } = dto.DataScadenzaPagamento?.ToString("dd/MM/yyyy") ?? "-";
}

public partial class FattureViewModel : ObservableObject, IReloadable
{
    private readonly FatturaService _service;
    private readonly DialogService _dialogs;

    public FattureViewModel(FatturaService service, DialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        _ = ReloadAsync();
    }

    public ObservableCollection<FatturaRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => !IsLoading && !HasError && Rows.Count == 0;

    public async Task ReloadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetAllAsync();
            Rows.Clear();
            foreach (var dto in list) Rows.Add(new FatturaRow(dto));
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento fatture fallito", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task ImportXmlAsync()
    {
        var files = _dialogs.PickFiles("Fatture elettroniche (*.xml)|*.xml", "Importa fatture XML");
        if (files.Length == 0) return;

        int importate = 0, duplicate = 0;
        var errori = new List<string>();

        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);
                if (await _service.ImportaXmlAsync(stream)) importate++;
                else duplicate++;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Import fattura fallito: {file}", ex);
                errori.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        await ReloadAsync();

        var riepilogo = $"Fatture importate: {importate}\nGià presenti: {duplicate}\nErrori: {errori.Count}";
        if (errori.Count > 0)
            riepilogo += "\n\n" + string.Join("\n", errori.Take(5));
        await _dialogs.MessageAsync("Importazione completata", riepilogo);
    }
}
