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
- Nel progetto Infrastructure ci sono anche MailKit (import PEC), QuestPDF (export presenze) e PdfPig (lettura fatture PDF)

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

- **Dipendenti:** elenco, nuovo, modifica, elimina; dettaglio con anagrafica e presenze mensili (aggiungi, modifica, elimina, totali ore)
  e pulsante «Esporta presenze» (foglio del dipendente su un intervallo di mesi, PDF o CSV).
- **Presenze** (voce di menu, `PresenzeViewModel`/`PresenzeView`): griglia mensile con tutti i dipendenti in forza (righe) e i giorni
  (colonne, generate nel code-behind perché cambiano col mese); totali Ord./Straord. fissi accanto al nome.
  Un **dipendente può avere più presenze nello stesso giorno, una per cantiere** (e al massimo una senza cantiere; max 24 ore al giorno;
  indice unico filtrato su dipendente+data+cantiere, la regola «una senza cantiere» è nel servizio perché in SQLite i NULL sono distinti).
  Si scrivono le ore nella cella e Invio (`PresenzaService.ImpostaOreGiornoAsync`): il selettore «Ore ordinarie/straordinarie» decide quale
  tipo si vede e si modifica, «Nuove ore su» sceglie il cantiere delle ore inserite in celle vuote. Clic destro sulle celle selezionate:
  assegna il cantiere (`ImpostaCantiereAsync`, solo giorni con una presenza), «Dettaglio giorno…» (`PresenzaGiornoViewModel`: una riga per
  cantiere, `SalvaGiornoAsync`), «Cancella ore» (anche tasto Canc). Una cella con più cantieri è gialla, mostra la somma e non si modifica
  al volo: si apre il dettaglio del giorno. Pulsante «Esporta»: foglio del mese in PDF/CSV, anche solo per un cantiere (`EsportaViewModel`,
  `PresenzaService.EsportaMeseAsync`/`EsportaDipendenteAsync`, `PresenzaExportService`); CSV con `;`, virgola decimale e BOM per Excel.
  **Data di assunzione:** `PresenzaService` rifiuta ore prima dell'assunzione (inserimento, modifica, giorno intero, cella; azzerare una
  presenza preesistente resta consentito) e `DipendenteService.UpdateAsync` rifiuta un'assunzione successiva a presenze già registrate.
  Le interfacce non offrono ciò che non è valido: nel dettaglio del dipendente «mese precedente» si ferma al mese di assunzione,
  nella maschera il calendario parte dall'assunzione, l'elenco dei mesi dell'esportazione parte da lì, e nella griglia i giorni prima
  dell'assunzione sono grigio scuro e non modificabili (l'avviso di «Aggiungi» resta solo per un'assunzione futura).
  Il dettaglio cantiere mostra le ore per dipendente (manodopera) e le ore totali. Il campo `OreStatordinarie` dell'entità ha un refuso
  storico (`Statordinarie`); i tipi nuovi usano `OreStraordinarie`.
- **Fatture:** elenco; un unico pulsante «Importa fatture» per uno o più file `.xml` (FatturaPA) o `.pdf` con riepilogo;
  `FatturaService.ImportaAsync` sceglie il parser in base all'estensione; i file non validi vengono rifiutati.
  Il PDF (`FatturaPdfParser`, PdfPig + regole sul testo) ha due percorsi: il PDF di visualizzazione standard della fattura
  elettronica (blocchi «Cedente/prestatore» e «RIEPILOGHI IVA E TOTALI», provato su 3 PDF reali di PA) è letto con regole
  precise e dà anche anagrafica fornitore, tipo documento (TD01, TD24...) e modalità di pagamento; qualsiasi altro PDF passa
  da regole generiche, meno affidabili. Non legge le righe di dettaglio né le scansioni (niente OCR).
  Il percorso standard è stato tarato su 597 PDF reali di ~110 fornitori (tutti in formato standard): li legge tutti, 595 con
  imponibile + imposta = totale esatto (gli altri 2 differiscono per bollo e per un arrotondamento di 0,01).
  Il riepilogo IVA si ricostruisce cercando le righe `%IVA [spese accessorie] imponibile imposta` coerenti con il totale, perché
  l'impaginazione cambia molto (85 forme diverse): numeri spezzati dal PDF (`12.000,0` + `0`), righe esenti senza aliquota
  (`N1 25,01 0`, anche con imponibile negativo), colonna spese accessorie, intestazione TD su più righe, bollo `SI`, ritenuta dopo il totale.
  Il fornitore è il blocco «Cedente/prestatore»; il «Terzo Intermediario» (es. Passepartout, TeamSystem) è un altro soggetto e va ignorato.
  Le righe di dettaglio (`Righe` in `FatturaPdfParser`) si leggono dal testo e si tengono solo se la somma dei totali di riga
  è uguale all'imponibile (altrimenti la fattura resta senza righe): 566 PDF su 597 passano. Gli scarti sono righe di sola
  quantità senza prezzo (pizzeria), documenti incoerenti, e testo/importi intrecciati dal PDF al salto pagina (servirebbe
  l'estrazione con coordinate). Il PDF a volte stampa il totale di una riga a fondo pagina *prima* della riga: è gestito.
  Il CUP si accetta solo nel formato esatto (lettera, 2 cifre, lettera, 11 cifre): CUP con refusi del fornitore (14 o 16 caratteri) vengono ignorati.
  Se P.IVA, numero, data o totale non si trovano il file è rifiutato. Per i PDF l'anagrafica di un fornitore già presente non viene sovrascritta.
  Attenzione: la P.IVA dell'azienda stessa compare come cessionario in ogni fattura, per questo nel percorso standard si legge solo il blocco del cedente.
- **Impostazioni** (voce in fondo alla barra laterale, `ImpostazioniViewModel`; i valori stanno in `settings.json` nella cartella dati,
  gestito da `ImpostazioniService`: per aggiungere un'opzione basta una proprietà in `Impostazioni`).
  Per ora c'è l'**archivio fatture** (`ArchivioFatture`): se attivo, ogni file importato (XML o PDF, anche se la fattura è già presente
  ma il file manca) viene copiato in `<destinazione>\fornitori\<fornitore>\` e, se la fattura ha un cantiere, anche in
  `<destinazione>\cantieri\<cantiere>\`. Il file si chiama `<data> <fornitore> - <numero>.<ext>` (nome dipendente solo dalla fattura:
  un reimport non crea doppioni e non sovrascrive mai). I nomi di fornitore/cantiere sono quelli del database. Rinominare un cantiere
  rinomina anche la sua cartella (`CantiereService.RenameAsync` restituisce un avviso se non ci riesce). Una copia fallita non invalida
  l'import (avviso nel riepilogo e nel log); un file già dentro la cartella archivio non viene ricopiato. Nomi ripuliti per Windows
  (caratteri vietati, punto finale, nomi riservati, max 80 caratteri). Le cartelle dei fornitori non si rinominano se la denominazione
  cambia con un XML successivo. XML e PDF della stessa fattura finiscono entrambi nell'archivio (nomi diversi per estensione).
- **Importa cartella** (pulsante nella schermata Fatture, `FatturaService.ImportaCartellaAsync`): cerca `.xml` e `.pdf` in una cartella
  e in tutte le sottocartelle, importa quelli validi e scarta gli altri con il motivo (riepilogo finale con conteggi e i primi problemi).
  Si importa con **un clic dalla cartella predefinita** (`Impostazioni.CartellaImportazione`): la prima volta, o se la cartella non esiste
  più (in quel caso lo dice), la si sceglie al momento e viene ricordata. Il pulsante con la cartella aperta accanto importa da
  un'altra cartella solo per quella volta. In Impostazioni si cambia o si toglie, e compare un avviso se coincide con (o contiene) la
  cartella dell'archivio fatture. Analogamente `Impostazioni.CartellaEsportazione` è la cartella in cui si apre il salvataggio dei fogli presenze
  (il nome del file si conferma sempre, per non sovrascrivere per sbaglio; senza impostazione Windows riapre l'ultima cartella usata).
  Gli XML sono elaborati prima dei PDF, così se esistono entrambi per la stessa fattura vince l'XML (più completo) e il PDF risulta duplicato.
  I PDF non nel formato standard sono accettati solo in modalità rigorosa (`FatturaPdfParser.Parse(..., rigoroso: true)`):
  imponibile, imposta e totale devono essere tutti trovati e coerenti, per non importare contratti o preventivi come fatture.
  Non legge `.xml.p7m` (fatture firmate) né scansioni. Provato su una cartella mista (XML non valido, PDF corrotto/vuoto, contratto,
  preventivo) e sulle 597 fatture reali (7 secondi).
  Sia «Importa cartella» sia «Importa fatture» passano da `EseguiImportAsync` (`FattureViewModel`): un dialogo di avanzamento
  (`ImportProgressViewModel`) blocca l'app e ha il pulsante «Annulla». L'annullamento si controlla tra un file e l'altro
  (ogni file è importato per intero o per niente) e il servizio restituisce il risultato parziale (`Annullata = true`), che
  viene mostrato nel riepilogo. Il dialogo si chiude solo dopo `Shown` (la View è caricata), altrimenti `DialogHost.Close` fallisce.
  Provato pilotando l'app vera con UI Automation (script usa e getta): avanzamento, annulla, riepilogo parziale.
- **Cantieri:** elenco con CUP e numero di fatture, ricerca per CUP o nome, rinomina (matita). Si creano da soli importando fatture con un CUP.
  Doppio clic sulla riga apre il **dettaglio** (`CantiereDetailViewModel`, `CantiereService.GetByIdAsync`): CUP, totale fatturato,
  riepilogo per fornitore, elenco fatture e pulsante «Rinomina».
- **Fornitori** (voce di menu tra Fatture e Cantieri): elenco con nome, P.IVA e numero di fatture, ricerca per nome o P.IVA.
  Doppio clic apre il **dettaglio** in sola lettura (`FornitoreDetailViewModel`, `FornitoreService`): anagrafica, totale e fatture.
  I fornitori non sono modificabili.
- **Navigazione tra dettagli:** `DetailOpener` (servizio) apre fattura, fornitore o cantiere da qualsiasi schermata con «indietro».
  Nel dettaglio fattura fornitore e cantiere sono collegamenti; nelle liste dei dettagli il doppio clic apre la fattura.
  I totali di cantieri e fornitori sottraggono le note di credito (TD04, TD08: `Documenti.ConSegno`), che nelle liste si
  mostrano in negativo; nel database gli importi restano positivi come nel file originale.
- **Dashboard:** contatori e ore del mese.
- **Aggiornamenti:** controllo all'avvio, pulsante "Controlla aggiornamenti" in basso a sinistra.

  Dall'elenco (doppio clic sulla riga o freccia a destra) si apre il **dettaglio fattura** (`FatturaDetailViewModel`/`FatturaDetailView`,
  dati da `FatturaService.GetByIdAsync`): fornitore, documento, pagamento, righe e totali. I codici SDI (TDxx, MPxx) sono tradotti in testo.
  Le fatture importate da PDF hanno le righe solo se il parser le ha lette con la somma coerente; altrimenti la schermata lo dice.

Esiste nel codice ma **non ha ancora una schermata**: import da PEC (`PecImportService`).

## Dati e database

- Database, impostazioni e log: `%LocalAppData%\EdilManagerData` (`edilmanager.db`, `settings.json`, `backups\`, `logs\trace.log`, `logs\error.log`).
  `AppLog.Trace` scrive la cronologia (avvio, import file per file con esito e motivo); `AppLog.Error` scrive il dettaglio
  dell'eccezione in `error.log` e una riga di sintesi anche in `trace.log`. Oltre 5 MB il file diventa `.old`.
  Sono volutamente **fuori** da `%LocalAppData%\EdilManager`, la cartella di Velopack che viene sostituita a ogni aggiornamento.
- La variabile d'ambiente `EDILMANAGER_DATA_DIR` cambia la cartella dati (utile per prove senza toccare i dati veri).
- Le migrazioni si applicano all'avvio. Se ce ne sono di nuove, prima viene salvata una copia del database (si tengono le ultime 5).
- Nuova migrazione:
  `dotnet ef migrations add NomeMigrazione --project EdilManager.Infrastructure --startup-project EdilManager.Desktop`
  (`dotnet-ef` non è installato di default: `dotnet tool install dotnet-ef --version 9.0.* --tool-path <cartella>` e poi si lancia da lì).

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
- Icona: `EdilManager.Desktop/Assets/icona.ico` (edificio bianco con gru gialla su blu; PNG a 16, 24, 32, 48, 64, 128, 256 px, con una
  versione semplificata fino a 32 px perché i dettagli non reggono) e `icona.png` a 512 px. È collegata a `ApplicationIcon` nel `.csproj`
  (icona dell'exe), a `Icon` di `MainWindow` (barra del titolo) e a `vpk pack --icon` nel workflow (Setup.exe e collegamenti).
  È stata disegnata con un programma di prova usa e getta (System.Drawing), non salvato nel repo: per cambiarla si parte da `icona.png`
  con un editor di immagini, oppure si ridisegna da zero e si rigenera il `.ico`.
- L'aggiornamento funziona solo con l'app **installata** (da `Setup.exe`), non da Visual Studio né con `dotnet run`.
- Per provarlo: installa una versione, pubblica una versione più alta, riapri l'app installata (verificato con v1.1.0).
- Il `Setup.exe` pesa circa 100 MB (runtime incluso) e non è firmato: Windows può mostrare un avviso al primo avvio.
- Tag da ripulire se vuoi: `v.1.0.2` (errore di battitura, anche sul remoto) e `v1.0.0.1` (non valido).
  `git push origin --delete v.1.0.2` e `git tag -d v.1.0.2 v1.0.0.1`.

## Trappole già incontrate

- MaterialDesign 5: i colori si chiamano `MaterialDesign.Brush.*` (le vecchie chiavi come `PrimaryHueMidBrush` non esistono più).
- `DataGrid`: per allineare a destra una colonna di importi non basta `HorizontalAlignment="Right"` sul `TextBlock` (il `ContentPresenter`
  della cella si restringe al contenuto e il valore resta a sinistra). Servono `HeaderStyle="RightHeader"`, `ElementStyle="RightCell"`
  **e** `CellStyle="RightCellContainer"` (tutti in `App.xaml`).
- `DatePicker`: i binding di `DisplayDateStart/End/DisplayDate` a proprietà di sola lettura vanno scritti con `Mode=OneWay`.
- SQLite non sa fare `Sum` né `OrderBy` su `decimal`: si leggono i valori e si sommano in memoria (vedi `DashboardService`).
- Nei progetti WPF `System.IO` non è tra gli using impliciti.
- `FatturaPaParser` tollera i campi mancanti: `FatturaService` controlla che P.IVA, numero e data ci siano.

## Come è stato verificato

Non ci sono test automatici nel repo. Durante lo sviluppo sono stati usati strumenti usa e getta, non salvati:
un piccolo programma di prova sui servizi (database temporaneo) e un test dell'interfaccia con screenshot.
Per il parser PDF: un programma di prova che legge una cartella di PDF (`C:\Users\Ciuca\Desktop\Fornitori`, fuori dal repo perché
contiene dati reali) e controlla che imponibile + imposta = totale. Senza i corrispondenti XML questo controllo è la "verità" disponibile.
La pubblicazione delle Release da GitHub Actions funziona. L'aggiornamento automatico è stato provato dall'utente con
un'app installata che si è aggiornata alla Release successiva (v1.1.0): il flusso completo funziona.

## Idee per i prossimi passi

- Un progetto di test (xUnit) con i controlli fatti a mano sui servizi.
- Schermata per l'import da PEC.
- Estrazione dei PDF con le coordinate (non solo l'ordine del testo) per recuperare il ~5% di fatture le cui righe oggi si scartano.
- Firma del codice dell'installer, per togliere l'avviso di Windows.
- Costo del lavoro per cantiere (serve la paga oraria del dipendente, oggi non c'è).
- Scadenze e documenti dei dipendenti (visita medica, formazione sicurezza, idoneità).
