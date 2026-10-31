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
  **Data di assunzione:** `PresenzaService` rifiuta di *creare* presenze prima dell'assunzione (inserimento, spostamento di data, nuovo
  cantiere in un giorno, cella vuota); una presenza già esistente (dati vecchi) si può invece correggere nella stessa data o eliminare.
  `DipendenteService.UpdateAsync` rifiuta un'assunzione successiva a presenze già registrate. Una presenza deve avere almeno un'ora
  (per toglierla si elimina).
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
- **Azienda** (`Azienda` nel database, una sola riga; `AziendaService`, `AziendaFormViewModel`): ragione sociale (obbligatoria), P.IVA con controllo
  della cifra di controllo (`DatiFiscali` in Core, usata anche dal parser PDF), codice fiscale, indirizzo, CAP, comune, provincia, telefono, email.
  Al primo avvio dopo l'aggiornamento (nessun dato) il modulo si propone da solo; «Più tardi» imposta `Impostazioni.AziendaSaltata` e non lo si
  riconsidera più, ma resta il blocco «Azienda» in cima alle Impostazioni. Il nome sostituisce il titolo nell'intestazione blu della barra
  laterale (con la P.IVA sotto; finché mancano i dati resta «EdilManager»), compare nel titolo della finestra e nell'intestazione dei PDF
  delle presenze. È nel database (non in `settings.json`) perché è un dato di lavoro e viaggia con i backup.
- **Impostazioni** (voce in fondo alla barra laterale, `ImpostazioniViewModel`; i valori stanno in `settings.json` nella cartella dati,
  gestito da `ImpostazioniService`: per aggiungere un'opzione basta una proprietà in `Impostazioni`).
  Per ora c'è l'**archivio fatture** (`ArchivioFatture`): se attivo, ogni file importato (XML o PDF, anche se la fattura è già presente
  ma il file manca) viene copiato in `<destinazione>\fornitori\<fornitore>\` e, se la fattura ha un cantiere, anche in
  `<destinazione>\cantieri\<cantiere>\`. Il file si chiama `<data> <fornitore> - <numero>.<ext>` (nome dipendente solo dalla fattura:
  un reimport non crea doppioni e non sovrascrive mai; due fatture diverse che danno lo stesso nome, es. numeri `12/A` e `12-A`, prendono il
  suffisso ` (2)`, ` (3)`, e un file identico già presente si salta). I nomi di fornitore/cantiere sono quelli del database. Rinominare un cantiere
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
- **Esporta fatture** (pulsante «Esporta» nella schermata Fatture, `EsportaFattureViewModel`, `EsportaFattureService`): copia in una cartella i file
  originali (XML/PDF) delle fatture con la **data della fattura** nell'anno scelto o in un periodo dal–al. Crea nella cartella scelta una sottocartella
  «Fatture 2026» (o «Fatture 2026-06-01 - 2026-07-31»), divisa a scelta: nessuna divisione, per cantiere, per fornitore, oppure cantiere e dentro fornitore
  (le fatture senza cantiere vanno in «Senza cantiere»). **I file vengono presi dall'archivio fatture** (`fornitori\<fornitore>\<nome file>`): il database non
  conserva gli originali, quindi le fatture importate con l'archivio spento compaiono solo nel riepilogo. Facoltativo il «Riepilogo fatture.csv»
  (`;`, virgola decimale, BOM; note di credito in negativo; colonna «File esportato» sì/no). Non sovrascrive mai: file identico = già a posto,
  file diverso con lo stesso nome = suffisso ` (2)`. Senza archivio impostato si può esportare solo il riepilogo. Cartella di apertura della scelta:
  `Impostazioni.CartellaEsportazione`.
- **Cantieri:** elenco con CUP e numero di fatture, ricerca per CUP o nome, rinomina (matita). Si creano da soli importando fatture con un CUP.
  **Nome dei nuovi cantieri:** un CUP mai visto crea un cantiere che si chiama come il CUP; a fine importazione (`FattureViewModel.NominaNuoviCantieriAsync`)
  compare un dialogo per ognuno (`NuovoCantiereViewModel`) con la fattura in cui è stato trovato (fornitore, numero, data, totale, prima riga) e il campo
  del nome. Salvare usa `CantiereService.RenameAsync`, quindi anche la cartella dell'archivio prende il nome scelto. «Più tardi» lo lascia così
  (nell'elenco ha l'etichetta «da nominare»: nome uguale al CUP), «Salta tutti» smette di chiedere. Il rilevamento viene da
  `EsitoSalvataggio.CantiereCreato` → `ImportCartellaResult.NuoviCantieri`.
  **Cantieri senza CUP** (pulsante «Nuovo cantiere»; `Cantiere.Interno`, `CantiereService.CreaInternoAsync`): hanno un codice interno `INT-0001`, `INT-0002`...
  nel campo CUP (mantiene l'indice unico e non può coincidere con un CUP vero), ma l'interfaccia scrive «senza CUP» (`CantiereListDto.CupTesto`/`Etichetta`).
  Le fatture si assegnano a mano: clic destro nell'elenco fatture (anche più righe) o pulsante nel dettaglio (`CantiereFatture`, `FatturaService.ImpostaCantiereAsync`);
  con l'archivio attivo i file già archiviati sotto il fornitore vengono copiati anche nella cartella del cantiere, che nasce così alla prima fattura
  (niente cartella alla creazione; i file nelle cartelle di altri cantieri non si cancellano). Quando arriva il CUP vero, «Imposta CUP» nel dettaglio
  lo sostituisce al codice interno (`ImpostaCupAsync`, formato controllato da `Cup` in Core): le fatture restano collegate perché puntano all'id. Se quel CUP
  appartiene già a un altro cantiere si rifiuta, senza unire i due cantieri.
  Non si può dare a un cantiere un nome già usato da un altro (o che per Windows darebbe la stessa cartella dell'archivio).
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
- **Stato di pagamento delle fatture** (`Fattura.Stato` + `DataPagamento`, `Pagamenti` in Core, `FatturaService.ImpostaStatoAsync`, `StatoFatture` nel Desktop):
  nel database si salvano solo *Da pagare*, *Pagata* (con la data) e *In contestazione*; **«Scaduta» non è salvata ma calcolata**
  (da pagare con la scadenza passata), così non resta mai indietro. Se il documento non ha la scadenza (spesso nei PDF) si usa data fattura +
  `Impostazioni.GiorniScadenzaPredefiniti` (30; con 0 non scade mai), mostrata tra parentesi o come «(presunta)». Le note di credito non hanno stato.
  Le fatture già presenti all'aggiornamento sono «Da pagare» (migrazione `StatoPagamentoFatture`). Elenco fatture: filtro per stato,
  colonna Stato, selezione multipla e clic destro per cambiare stato a più fatture (per «Pagata» chiede la data, `PagamentoFormViewModel`);
  stessa colonna nei dettagli di cantiere e fornitore; nel dettaglio fattura i pulsanti di cambio stato; in dashboard le card «Da pagare» e «Scadute»
  (le contestate non contano); in Impostazioni il campo dei giorni.

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
- `Directory.EnumerateFiles` è "pigro": la scansione parte quando si legge l'elenco, quindi va messa in `Task.Run` (come in
  `FatturaService.ImportaElencoAsync`), altrimenti su cartelle grandi o di rete blocca la finestra e il pulsante «Annulla».
- Il riepilogo IVA del parser (`FatturaPdfParser.Riepilogo`) è una ricerca ricorsiva con tetto di 200.000 passi: senza, un blocco anomalo
  (per esempio pieno di zeri) crescerebbe in modo esponenziale. Gli importi accettati hanno al massimo 15 cifre intere (oltre andrebbe in overflow).
- Se si modifica il codice dal terminale (Git Bash), `sed -i` toglie i backslash dalle espressioni regolari e `\x27` non è un apostrofo
  dentro un file: per il codice con regex o apostrofi usare lo strumento di modifica dei file e ricontrollare il risultato.

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
- Controllo all'importazione delle fatture intestate a un'altra azienda, ora che la P.IVA dell'azienda è nota (come avviso, non come blocco, per non rifiutare tutto se la P.IVA è scritta male); aiuterebbe anche a distinguere fornitore e cliente nei PDF non standard.
- Estrazione dei PDF con le coordinate (non solo l'ordine del testo) per recuperare il ~5% di fatture le cui righe oggi si scartano.
- Firma del codice dell'installer, per togliere l'avviso di Windows.
- Costo del lavoro per cantiere (serve la paga oraria del dipendente, oggi non c'è).
- Scadenze e documenti dei dipendenti (visita medica, formazione sicurezza, idoneità).
