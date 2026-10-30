using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;

namespace EdilManager.Desktop.ViewModels;

// Finestra di dialogo che blocca l'app durante un import e permette di annullarlo.
public partial class ImportProgressViewModel(string titolo, CancellationTokenSource cts) : ObservableObject
{
    private readonly TaskCompletionSource _shown = new();

    public string Titolo { get; } = titolo;

    // Completato quando il dialogo è davvero visibile: solo allora si può chiuderlo in sicurezza.
    public Task Shown => _shown.Task;
    public void NotifyShown() => _shown.TrySetResult();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Conteggio), nameof(IsIndeterminate))]
    private int _elaborati;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Conteggio), nameof(IsIndeterminate))]
    private int _totale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stato))]
    private string _fileCorrente = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stato))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isCancelling;

    public bool IsIndeterminate => Totale == 0;
    public string Conteggio => Totale == 0 ? "Ricerca dei file…" : $"{Elaborati} di {Totale} file";
    public string Stato => IsCancelling ? "Annullamento in corso: termino il file corrente…" : FileCorrente;

    public void Aggiorna(ImportCartellaProgress p)
    {
        Totale = p.Totale;
        Elaborati = p.Elaborati;
        if (p.Esito is null) FileCorrente = p.File;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        IsCancelling = true;
        cts.Cancel();
    }

    private bool CanCancel() => !IsCancelling;
}
