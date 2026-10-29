# EdilManager Desktop

Gestionale per l'edilizia (dipendenti, presenze, fatture, cantieri) come **singola app WPF** con aggiornamento automatico.
Nasce dalla conversione del vecchio progetto `EdilManagerAPI` (API ASP.NET + frontend Flutter), che resta intatto in
`Documenti\Projects\EdilManagerAPI` e non serve più.

Questo file riassume lo stato del progetto ed è letto in automatico da Claude Code a inizio sessione.

## Convenzioni

- Rispondere, scrivere i messaggi di commit e i testi dell'interfaccia in **italiano**.
- Non fare commit né push se non richiesto: l'utente di solito esegue il push da solo.
- Compilare con `dotnet build EdilManagerDesktop.sln`; deve restare a 0 errori e 0 avvisi.
- Per provare l'app senza toccare i dati veri, impostare `EDILMANAGER_DATA_DIR` su una cartella temporanea.
- Una nuova schermata richiede: ViewModel, View (UserControl), `DataTemplate` in `App.xaml` e voce di menu in `MainViewModel`.
- La logica sui dati va nei servizi di `EdilManager.Infrastructure`, non nei ViewModel.

## Stack

- .NET 8, WPF, pattern MVVM con CommunityToolkit.Mvvm (attributi `[ObservableProperty]` / `[RelayCommand]`)
- Interfaccia con MaterialDesignThemes 5.4.0
- EF Core 9 + SQLite, un `IDbContextFactory` (contesti di vita breve, uno per operazione)
- Aggiornamenti con Velopack 1.2.161 (il tool `vpk` nel workflow è fissato alla stessa versione)
- Nel progetto Infrastructure ci sono anche MailKit (import PEC) e QuestPDF (export presenze)

## Struttura

| Cartella | Contenuto |
|---|---|
| `EdilManager.Core` | Entità e DTO (nessuna dipendenza) |
| `EdilManager.Infrastructure` | `ApplicationDbContext`, migrazioni, servizi (`DipendenteService`, `PresenzaService`, `CantiereService`, `FatturaService`, `DashboardService`), parser FatturaPA, import PEC, export presenze |
| `EdilManager.Desktop` | App WPF: `ViewModels/`, `Views/`, `Services/` (dialog, navigazione, aggiornamenti), `App.xaml(.cs)` con DI e temi |
| `.github/workflows/release.yml` | Pubblicazione automatica delle Release |

Come funziona l'interfaccia:
- Il menu a sinistra e la navigazione stanno in `MainViewModel` (`INavigationService`: `Push`/`Pop`).
- Ogni ViewModel è collegato alla sua View con un `DataTemplate` in `App.xaml`: aggiungendo una schermata va aggiunto anche lì.
- Le finestre di dialogo (form, conferme, messaggi) usano il `DialogHost` di MaterialDesign tramite `DialogService`.

## Funzioni presenti

- **Dipendenti:** elenco, nuovo, modifica, elimina; dettaglio con anagrafica e presenze mensili (aggiungi, modifica, elimina, totali ore).
- **Fatture:** elenco; import di uno o più file XML FatturaPA con riepilogo; i file non validi vengono rifiutati.
- **Cantieri:** elenco con CUP e numero di fatture; rinomina. Si creano da soli importando fatture con un CUP.
- **Dashboard:** contatori e ore del mese.
- **Aggiornamenti:** controllo all'avvio, pulsante "Controlla aggiornamenti" in basso a sinistra.

Esistono nel codice ma **non hanno ancora una schermata**: import da PEC (`PecImportService`), export presenze PDF/CSV (`PresenzaExportService`), elenco fornitori, dettaglio fattura con righe.

## Dati e database

- Database e log: `%LocalAppData%\EdilManagerData` (`edilmanager.db`, `app.log`, `backups\`).
  Sono volutamente **fuori** da `%LocalAppData%\EdilManager`, la cartella di Velopack che viene sostituita a ogni aggiornamento.
- La variabile d'ambiente `EDILMANAGER_DATA_DIR` cambia la cartella dati (utile per prove senza toccare i dati veri).
- Le migrazioni si applicano all'avvio. Se ce ne sono di nuove, prima viene salvata una copia del database (si tengono le ultime 5).
- Nuova migrazione:
  `dotnet ef migrations add NomeMigrazione --project EdilManager.Infrastructure --startup-project EdilManager.Desktop`

## Rilascio e aggiornamento automatico

1. Lavora su `develop` (il branch predefinito del repo su GitHub è `master`), fai commit e push.
2. Crea un tag **a tre numeri** e invialo: `git tag v1.2.0` poi `git push origin v1.2.0`.
   Un tag come `v1.0.0.1` o `v.1.0.2` viene rifiutato (`vpk` accetta solo SemVer a 3 numeri).
3. Il workflow `Release` (tab Actions) compila, impacchetta con `vpk` e crea la Release nello **stesso repo**.
   Usa il token integrato di GitHub (`GITHUB_TOKEN`): non serve nessun token personale né secret.
4. L'app installata legge le Release del repo (`RepoUrl` in `UpdateService.cs`, che deve coincidere con `RELEASES_REPO` nel workflow).

Cose da ricordare:
- Il repo deve restare **pubblico**, altrimenti l'app installata non può leggere le Release senza token.
- La versione dell'app viene dal tag, non dal `.csproj`: per le build locali vale `1.0.0`.
- L'aggiornamento funziona solo con l'app **installata** (da `Setup.exe`), non da Visual Studio né con `dotnet run`.
- Per provarlo: installa una versione, pubblica una versione più alta, riapri l'app installata (verificato con v1.1.0).
- Il `Setup.exe` pesa circa 100 MB (runtime incluso) e non è firmato: Windows può mostrare un avviso al primo avvio.
- Tag da ripulire se vuoi: `v.1.0.2` (errore di battitura, anche sul remoto) e `v1.0.0.1` (non valido).
  `git push origin --delete v.1.0.2` e `git tag -d v.1.0.2 v1.0.0.1`.

## Trappole già incontrate

- MaterialDesign 5: i colori si chiamano `MaterialDesign.Brush.*` (le vecchie chiavi come `PrimaryHueMidBrush` non esistono più).
- `DatePicker`: i binding di `DisplayDateStart/End/DisplayDate` a proprietà di sola lettura vanno scritti con `Mode=OneWay`.
- SQLite non sa fare `Sum` né `OrderBy` su `decimal`: si leggono i valori e si sommano in memoria (vedi `DashboardService`).
- Nei progetti WPF `System.IO` non è tra gli using impliciti.
- `FatturaPaParser` tollera i campi mancanti: `FatturaService` controlla che P.IVA, numero e data ci siano.

## Come è stato verificato

Non ci sono test automatici nel repo. Durante lo sviluppo sono stati usati strumenti usa e getta, non salvati:
un piccolo programma di prova sui servizi (database temporaneo) e un test dell'interfaccia con screenshot.
La pubblicazione delle Release da GitHub Actions funziona. L'aggiornamento automatico è stato provato dall'utente con
un'app installata che si è aggiornata alla Release successiva (v1.1.0): il flusso completo funziona.

## Idee per i prossimi passi

- Un progetto di test (xUnit) con i controlli fatti a mano sui servizi.
- Schermate per import da PEC, export presenze PDF/CSV, dettaglio fattura e fornitori.
- Firma del codice dell'installer, per togliere l'avviso di Windows.
- Associare una presenza a un cantiere (il dato esiste, la maschera non lo chiede ancora).
