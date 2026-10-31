using System.Text.Json;

namespace EdilManager.Infrastructure.Services;

public class Impostazioni
{
    // Se attivo, ogni fattura importata viene copiata in CartellaArchivio.
    public bool ArchivioAttivo { get; set; }
    public string? CartellaArchivio { get; set; }

    // Cartella da cui "Importa cartella" legge le fatture con un clic (se manca o non esiste più, la si sceglie al momento).
    public string? CartellaImportazione { get; set; }

    // Cartella in cui si aprono i salvataggi dei fogli presenze.
    public string? CartellaEsportazione { get; set; }

    // L'utente ha scelto "Più tardi" nel modulo dei dati azienda mostrato al primo avvio: non lo si ripropone a ogni avvio.
    public bool AziendaSaltata { get; set; }

    // Giorni dalla data fattura dopo i quali una fattura da pagare risulta scaduta quando il documento non indica la scadenza
    // (0 = senza scadenza indicata non è mai scaduta).
    public int GiorniScadenzaPredefiniti { get; set; } = 30;
}

// Impostazioni dell'utente in un file JSON nella cartella dati: sopravvive agli aggiornamenti e si estende aggiungendo proprietà.
public class ImpostazioniService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _file;
    private readonly object _gate = new();
    private Impostazioni _corrente;

    public ImpostazioniService(string file)
    {
        _file = file;
        _corrente = Carica(file);
    }

    // Copia: chi legge non può modificare lo stato interno senza passare da Modifica.
    public Impostazioni Corrente
    {
        get
        {
            lock (_gate) return new Impostazioni
            {
                ArchivioAttivo = _corrente.ArchivioAttivo,
                CartellaArchivio = _corrente.CartellaArchivio,
                CartellaImportazione = _corrente.CartellaImportazione,
                CartellaEsportazione = _corrente.CartellaEsportazione,
                AziendaSaltata = _corrente.AziendaSaltata,
                GiorniScadenzaPredefiniti = _corrente.GiorniScadenzaPredefiniti,
            };
        }
    }

    public void Modifica(Action<Impostazioni> modifica)
    {
        lock (_gate)
        {
            // Si lavora su una copia: se la scrittura del file fallisce (disco pieno, permessi) lo stato in memoria
            // resta quello salvato, e non diverge dal file fino alla chiusura dell'app.
            var copia = Duplica(_corrente);
            modifica(copia);
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            // Scrittura atomica: un crollo a metà non lascia un file a metà.
            var temporaneo = _file + ".tmp";
            File.WriteAllText(temporaneo, JsonSerializer.Serialize(copia, Json));
            File.Move(temporaneo, _file, overwrite: true);
            _corrente = copia;
        }
    }

    private static Impostazioni Duplica(Impostazioni i) => JsonSerializer.Deserialize<Impostazioni>(JsonSerializer.Serialize(i))!;

    private static Impostazioni Carica(string file)
    {
        try
        {
            return File.Exists(file)
                ? JsonSerializer.Deserialize<Impostazioni>(File.ReadAllText(file)) ?? new Impostazioni()
                : new Impostazioni();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // File illeggibile: si riparte dai valori predefiniti (il primo salvataggio lo sostituisce).
            return new Impostazioni();
        }
    }
}
