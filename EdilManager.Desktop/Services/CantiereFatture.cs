using EdilManager.Desktop.ViewModels;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.Services;

// Assegnazione manuale del cantiere alle fatture, condivisa da elenco e dettaglio.
public class CantiereFatture(FatturaService fatture, CantiereService cantieri, DialogService dialogs)
{
    // true se le fatture sono state aggiornate; false se l'utente ha annullato o c'è stato un errore (già mostrato).
    public async Task<bool> AssegnaAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return false;
        try
        {
            var opzioni = CantiereOption.Elenco(await cantieri.GetAllAsync());
            if (await dialogs.ShowAsync(new AssegnaCantiereViewModel(dialogs, ids.Count, opzioni)) is not CantiereOption scelta)
                return false;

            var esito = await fatture.ImpostaCantiereAsync(ids, scelta.Id);
            AppLog.Trace($"CANTIERE fatture: {esito.Aggiornate} assegnate a «{scelta.Nome}»");
            if (esito.Avviso is not null)
            {
                AppLog.Error($"ARCHIVIO {esito.Avviso}");
                await dialogs.MessageAsync("Archivio fatture", esito.Avviso);
            }
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Assegnazione del cantiere alle fatture fallita", ex);
            await dialogs.MessageAsync("Errore", ex.Message);
            return false;
        }
    }
}
