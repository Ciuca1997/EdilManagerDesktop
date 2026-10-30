using System.Linq.Expressions;
using EdilManager.Core.DTOs;
using EdilManager.Core.Entities;

namespace EdilManager.Infrastructure.Services;

// Proiezione condivisa: l'elenco fatture, il dettaglio cantiere e il dettaglio fornitore mostrano le stesse colonne.
public static class FatturaProjections
{
    public static readonly Expression<Func<Fattura, FatturaListDto>> ToListDto = f => new FatturaListDto
    {
        Id = f.Id,
        NumeroDocumento = f.NumeroDocumento,
        DataDocumento = f.DataDocumento,
        TipoDocumento = f.TipoDocumento,
        TotaleDocumento = f.TotaleDocumento,
        ModalitaPagamento = f.ModalitaPagamento,
        DataScadenzaPagamento = f.DataScadenzaPagamento,
        FornitoreId = f.FornitoreId,
        Fornitore = f.Fornitore.Denominazione,
        Cantiere = f.Cantiere != null ? f.Cantiere.Nome : null,
    };
}
