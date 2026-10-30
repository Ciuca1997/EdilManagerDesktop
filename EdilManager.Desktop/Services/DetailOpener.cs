using EdilManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Services;

// Apre le pagine di dettaglio (fattura, fornitore, cantiere) da qualsiasi schermata, con la navigazione "indietro".
public class DetailOpener(
    INavigationService nav, IServiceProvider services, DialogService dialogs,
    FatturaService fatture, FornitoreService fornitori, CantiereService cantieri)
{
    public Task OpenFatturaAsync(int id) => OpenAsync("Apertura fattura", async () =>
        await fatture.GetByIdAsync(id) is { } d ? ActivatorUtilities.CreateInstance<FatturaDetailViewModel>(services, d) : null);

    public Task OpenFornitoreAsync(int id) => OpenAsync("Apertura fornitore", async () =>
        await fornitori.GetByIdAsync(id) is { } d ? ActivatorUtilities.CreateInstance<FornitoreDetailViewModel>(services, d) : null);

    public Task OpenCantiereAsync(int id) => OpenAsync("Apertura cantiere", async () =>
        await cantieri.GetByIdAsync(id) is { } d ? ActivatorUtilities.CreateInstance<CantiereDetailViewModel>(services, d) : null);

    private async Task OpenAsync(string operazione, Func<Task<object?>> crea)
    {
        try
        {
            if (await crea() is { } pagina) nav.Push(pagina);
            else await dialogs.MessageAsync("Non trovato", "L'elemento non esiste più.");
        }
        catch (Exception ex)
        {
            AppLog.Error($"{operazione} fallita", ex);
            await dialogs.MessageAsync("Errore", ex.Message);
        }
    }
}
