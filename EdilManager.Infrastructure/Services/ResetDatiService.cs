using EdilManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EdilManager.Infrastructure.Services;

// Reset e ripristino del database. Le impostazioni (settings.json, cartelle comprese) e i file nelle cartelle
// (archivio fatture, importazioni, esportazioni) non vengono mai toccati.
// Prima di ogni operazione distruttiva si salva una copia del database in <backups>, che non viene mai cancellata.
public class ResetDatiService(IDbContextFactory<ApplicationDbContext> factory, string percorsoDatabase, string cartellaBackup)
{
    // Svuota il database ricreandolo con lo schema aggiornato. Restituisce il percorso della copia di sicurezza
    // (null se il database non esisteva ancora).
    public async Task<string?> ResetAsync()
    {
        var copia = await SalvaCopiaAsync("prima-del-reset");

        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.EnsureDeletedAsync();
        SqliteConnection.ClearAllPools();

        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.MigrateAsync();
        return copia;
    }

    // Sostituisce il database con il file di backup. Restituisce la copia di sicurezza dei dati attuali.
    // Il database ripristinato viene portato allo schema corrente all'avvio successivo (come per ogni aggiornamento).
    public async Task<string?> RipristinaAsync(string backup)
    {
        Verifica(backup);
        var copia = await SalvaCopiaAsync("prima-del-ripristino");

        SqliteConnection.ClearAllPools();
        File.Copy(backup, percorsoDatabase, overwrite: true);
        // File di giornale di un database sostituito non devono restare: altrimenti SQLite li applicherebbe al nuovo file.
        foreach (var suffisso in new[] { "-wal", "-shm", "-journal" })
        {
            var extra = percorsoDatabase + suffisso;
            if (File.Exists(extra)) File.Delete(extra);
        }
        return copia;
    }

    // Controlla che il file sia un database di EdilManager integro e non creato da una versione più recente dell'app.
    // Lancia InvalidOperationException con un messaggio leggibile se non va bene.
    public void Verifica(string backup)
    {
        if (!File.Exists(backup)) throw new InvalidOperationException("Il file scelto non esiste.");

        var cs = new SqliteConnectionStringBuilder { DataSource = backup, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        try
        {
            using var conn = new SqliteConnection(cs);
            conn.Open();

            using (var integrita = conn.CreateCommand())
            {
                integrita.CommandText = "PRAGMA integrity_check";
                if (!string.Equals(integrita.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Il file di backup è danneggiato.");
            }

            var migrazioni = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory";
                try
                {
                    using var r = cmd.ExecuteReader();
                    while (r.Read()) migrazioni.Add(r.GetString(0));
                }
                catch (SqliteException)
                {
                    throw new InvalidOperationException("Il file scelto non è un database di EdilManager.");
                }
            }

            using var db = factory.CreateDbContext();
            var note = db.Database.GetMigrations().ToHashSet();
            if (migrazioni.Any(m => !note.Contains(m)))
                throw new InvalidOperationException(
                    "Il backup è stato creato da una versione più recente di EdilManager: aggiorna l'app prima di ripristinarlo.");
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Il file scelto non è un database valido: {ex.Message}");
        }
    }

    private async Task<string?> SalvaCopiaAsync(string prefisso)
    {
        if (!File.Exists(percorsoDatabase)) return null;
        Directory.CreateDirectory(cartellaBackup);
        var copia = Path.Combine(cartellaBackup, $"{prefisso}-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        // VACUUM INTO produce una copia coerente anche se il database è aperto.
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", copia);
        return copia;
    }
}
