using EdilManager.Core.Entities;
using EdilManager.Desktop.ViewModels;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.Services;

// Cambio di stato di pagamento condiviso da elenco e dettaglio: per «Pagata» chiede prima la data, poi salva.
public class StatoFatture(FatturaService service, DialogService dialogs)
{
    // true se lo stato è stato cambiato; false se l'utente ha annullato o c'è stato un errore (già mostrato).
    public async Task<bool> ImpostaAsync(IReadOnlyCollection<int> ids, StatoPagamento stato)
    {
        if (ids.Count == 0) return false;
        try
        {
            DateOnly? data = null;
            if (stato == StatoPagamento.Pagata)
            {
                if (await dialogs.ShowAsync(new PagamentoFormViewModel(dialogs, ids.Count)) is not DateOnly scelta) return false;
                data = scelta;
            }

            var aggiornate = await service.ImpostaStatoAsync(ids, stato, data);
            AppLog.Trace($"STATO fatture: {aggiornate} impostate a {stato}{(data is { } d ? $" il {d:dd/MM/yyyy}" : "")}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Cambio di stato delle fatture fallito", ex);
            await dialogs.MessageAsync("Errore", ex.Message);
            return false;
        }
    }
}
