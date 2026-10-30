using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EdilManager.Desktop.ViewModels;

public sealed class FatturaRow(FatturaListDto dto)
{
    public int Id { get; } = dto.Id;
    public string Data { get; } = dto.DataDocumento.ToString("dd/MM/yyyy");
    public string Numero { get; } = dto.NumeroDocumento;
    public string Fornitore { get; } = dto.Fornitore;
    public string Cantiere { get; } = dto.Cantiere ?? "-";
    public string Tipo { get; } = dto.TipoDocumento;
    // Le note di credito si mostrano in negativo, coerente con i totali delle pagine di cantiere e fornitore.
    public string Totale { get; } = Documenti.ConSegno(dto.TipoDocumento, dto.TotaleDocumento).ToString("N2", Formats.Italian) + " €";
    public string Scadenza { get; } = dto.DataScadenzaPagamento?.ToString("dd/MM/yyyy") ?? "-";
}

public partial class FattureViewModel : ObservableObject, IReloadable
{
    private readonly FatturaService _service;
    private readonly DialogService _dialogs;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;
    private readonly ImpostazioniService _impostazioni;

    public FattureViewModel(FatturaService service, DialogService dialogs, INavigationService nav, IServiceProvider services,
        ImpostazioniService impostazioni)
    {
        _service = service;
        _dialogs = dialogs;
        _nav = nav;
        _services = services;
        _impostazioni = impostazioni;
        AggiornaTestoCartella();
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
    private async Task OpenAsync(FatturaRow row)
    {
        try
        {
            var detail = await _service.GetByIdAsync(row.Id);
            if (detail is null)
            {
                await ReloadAsync();
                return;
            }
            _nav.Push(ActivatorUtilities.CreateInstance<FatturaDetailViewModel>(_services, detail));
        }
        catch (Exception ex)
        {
            AppLog.Error("Apertura fattura fallita", ex);
            await _dialogs.MessageAsync("Errore", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var files = _dialogs.PickFiles(
            "Fatture (*.xml;*.pdf)|*.xml;*.pdf|Fatture elettroniche (*.xml)|*.xml|Fatture PDF (*.pdf)|*.pdf",
            "Importa fatture");
        if (files.Length == 0) return;

        AppLog.Trace($"IMPORT file: {files.Length} file selezionati");
        await EseguiImportAsync("Importazione fatture",
            (progress, ct) => _service.ImportaFileAsync(files, progress, ct));
    }

    // Testo del suggerimento del pulsante: dice da dove importa (o che la prima volta chiederà la cartella).
    [ObservableProperty]
    private string _cartellaImportazioneTesto = string.Empty;

    private void AggiornaTestoCartella()
    {
        var cartella = _impostazioni.Corrente.CartellaImportazione;
        CartellaImportazioneTesto = string.IsNullOrWhiteSpace(cartella)
            ? "Importa tutte le fatture (XML e PDF) di una cartella e delle sue sottocartelle. La prima volta ti chiede quale cartella usare."
            : $"Importa le fatture da: {cartella}";
    }

    // Un clic: importa dalla cartella predefinita. Se non è stata scelta, o non esiste più, la si sceglie adesso e viene ricordata.
    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        var cartella = _impostazioni.Corrente.CartellaImportazione;
        if (string.IsNullOrWhiteSpace(cartella) || !Directory.Exists(cartella))
        {
            if (!string.IsNullOrWhiteSpace(cartella) &&
                !await _dialogs.ConfirmAsync("Cartella non trovata",
                    $"La cartella predefinita per l'importazione non è raggiungibile:\n{cartella}\n\nVuoi sceglierne un'altra?", "Scegli cartella"))
                return;

            cartella = _dialogs.PickFolder(
                "Scegli la cartella da cui importare le fatture (XML e PDF, anche nelle sottocartelle): verrà ricordata",
                cartella);
            if (cartella is null) return;

            try
            {
                _impostazioni.Modifica(i => i.CartellaImportazione = cartella);
                AppLog.Trace($"IMPOSTAZIONI cartella predefinita per l'importazione: {cartella}");
                AggiornaTestoCartella();
            }
            catch (Exception ex)
            {
                // L'importazione prosegue comunque: il percorso semplicemente non verrà ricordato.
                AppLog.Error("Salvataggio della cartella di importazione fallito", ex);
            }
        }

        await ImportaDaCartellaAsync(cartella);
    }

    // Importa da un'altra cartella, solo questa volta (la cartella predefinita non cambia).
    [RelayCommand]
    private async Task ImportOtherFolderAsync()
    {
        var cartella = _dialogs.PickFolder(
            "Scegli la cartella con le fatture (XML e PDF, anche nelle sottocartelle)", _impostazioni.Corrente.CartellaImportazione);
        if (cartella is null) return;
        await ImportaDaCartellaAsync(cartella);
    }

    private async Task ImportaDaCartellaAsync(string cartella)
    {
        AppLog.Trace($"IMPORT cartella: {cartella}");
        await EseguiImportAsync("Importazione cartella",
            (progress, ct) => _service.ImportaCartellaAsync(cartella, progress, ct));
    }

    // Il dialogo di avanzamento blocca l'app finché l'import non finisce o l'utente non lo annulla;
    // in entrambi i casi segue il riepilogo di quanto è stato fatto.
    private async Task EseguiImportAsync(string titolo,
        Func<IProgress<ImportCartellaProgress>, CancellationToken, Task<ImportCartellaResult>> operazione)
    {
        using var cts = new CancellationTokenSource();
        var dialogo = new ImportProgressViewModel(titolo, cts);
        var progress = new Progress<ImportCartellaProgress>(p =>
        {
            dialogo.Aggiorna(p);
            Registra(p);
        });

        var apertura = _dialogs.ShowAsync(dialogo);
        await Task.WhenAny(dialogo.Shown, Task.Delay(2000));

        ImportCartellaResult? risultato = null;
        Exception? errore = null;
        try
        {
            risultato = await operazione(progress, cts.Token);
        }
        catch (Exception ex)
        {
            errore = ex;
        }
        finally
        {
            try
            {
                _dialogs.Close();
                await apertura;
            }
            catch (Exception ex)
            {
                AppLog.Error("Chiusura del dialogo di import fallita", ex);
            }
        }

        await ReloadAsync();

        if (risultato is null)
        {
            AppLog.Error($"{titolo} fallita", errore);
            await _dialogs.MessageAsync("Errore", errore?.Message ?? "Importazione non riuscita.");
            return;
        }

        AppLog.Trace($"IMPORT {(risultato.Annullata ? "annullato" : "completato")}: elaborati {risultato.Elaborati} di {risultato.Totale}, " +
                     $"importate {risultato.Importate}, già presenti {risultato.Duplicate}, scartati {risultato.Scartati.Count}, errori {risultato.Errori.Count}");
        await _dialogs.MessageAsync(risultato.Annullata ? "Importazione annullata" : "Importazione completata", Riepilogo(risultato));
    }

    private static void Registra(ImportCartellaProgress p)
    {
        switch (p.Esito)
        {
            case EsitoImport.Importata: AppLog.Trace($"IMPORT importata: {p.File}"); break;
            case EsitoImport.Duplicata: AppLog.Trace($"IMPORT già presente: {p.File}"); break;
            case EsitoImport.Scartata: AppLog.Trace($"IMPORT scartata: {p.File} ({p.Motivo})"); break;
            case EsitoImport.Errore: AppLog.Error($"IMPORT errore: {p.File}", p.Eccezione); break;
        }

        if (p.Copie is not null)
            foreach (var copia in p.Copie) AppLog.Trace($"ARCHIVIO copiata: {copia}");
        if (p.AvvisoArchivio is not null)
            AppLog.Error($"ARCHIVIO copia non riuscita per {p.File}: {p.AvvisoArchivio}");
    }

    private static string Riepilogo(ImportCartellaResult r)
    {
        var testo = new StringBuilder();
        if (r.Annullata)
            testo.AppendLine($"Operazione annullata dopo {r.Elaborati} file su {r.Totale}.").AppendLine();
        else
            testo.AppendLine($"File XML/PDF trovati: {r.Totale}");
        testo.AppendLine($"Fatture importate: {r.Importate}")
            .AppendLine($"Già presenti: {r.Duplicate}")
            .AppendLine($"Scartati perché non sono fatture valide: {r.Scartati.Count}")
            .Append($"Errori: {r.Errori.Count}");

        // La riga dell'archivio compare solo se è attivo (cioè se ha copiato qualcosa o ci ha provato).
        if (r.Copiate > 0 || r.AvvisiArchivio.Count > 0)
            testo.AppendLine().Append($"File copiati nell'archivio: {r.Copiate}");
        if (r.AvvisiArchivio.Count > 0)
        {
            testo.AppendLine().AppendLine().AppendLine(
                $"Attenzione: la copia nell'archivio non è riuscita per {r.AvvisiArchivio.Count} file (le fatture sono state importate comunque).");
            foreach (var a in r.AvvisiArchivio.Take(3)) testo.AppendLine($"{a.File}: {a.Motivo}");
            if (r.AvvisiArchivio.Count > 3) testo.AppendLine("…");
            testo.Append($"Dettagli in: {AppPaths.TraceLogFile}");
        }

        var dettagli = r.Errori.Concat(r.Scartati).Take(6).ToList();
        if (dettagli.Count > 0)
        {
            testo.AppendLine().AppendLine();
            foreach (var d in dettagli) testo.AppendLine($"{d.File}: {d.Motivo}");
            if (r.Errori.Count + r.Scartati.Count > dettagli.Count)
                testo.AppendLine("…");
            testo.Append($"Elenco completo in: {AppPaths.TraceLogFile}");
        }
        return testo.ToString();
    }
}
