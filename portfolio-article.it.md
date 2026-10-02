# Costruire una piattaforma production-grade da zero: cosa ho imparato da un side project con .NET 10 e Kubernetes

La maggior parte dei side project si ferma a "funziona sul mio portatile". Volevo capire cosa serve davvero per portarne uno in produzione come farebbe un'azienda vera, con un backend pulito, un cluster Kubernetes come si deve, observability e CI/CD. Così ne ho costruito uno dall'inizio alla fine. Ecco un giro tra le scelte che ho fatto.

## Di cosa si tratta, in parole semplici

È una piattaforma per i collezionisti e i giocatori di un gioco di carte collezionabili. Puoi catalogare la tua collezione e vedere quanto vale, costruire e condividere mazzi a partire da quelle carte, scambiare con altri giocatori e usare funzionalità sociali come amici e leghe. Il dominio è Magic: The Gathering, ma i problemi che ci sono sotto sono gli stessi che deve risolvere qualunque piattaforma di inventory, marketplace o contenuti: cataloghi enormi, filtri complessi, pricing, interazione in tempo reale e un grafo sociale.

È un backend in .NET 10, già deployato e in esecuzione su un cluster Kubernetes su Hetzner, al momento in fase di pre-launch hardening.

## La funzionalità di cui sono più orgoglioso: il version control per i mazzi

Un mazzo è un documento vivo. I giocatori lo modificano di continuo, provano varianti per i diversi tornei e vogliono tornare indietro quando un esperimento va male. La maggior parte delle app modella tutto questo come "modifica e sovrascrivi". Io l'ho modellato nel modo in cui Git modella il codice sorgente.

Sotto il cofano ci sono branch, commit e un grafo a puntatori verso il parent, usando le stesse primitive del version control. Puoi creare un branch di un mazzo per provare un'idea, fare commit delle modifiche man mano che iteri, confrontare due versioni per vedere esattamente quali carte sono cambiate e ripercorrere tutta la history. La vista della history esegue una breadth-first traversal a ritroso lungo i parent dei commit per ricostruire e ordinare la timeline.

Dal punto di vista ingegneristico questo conta perché è un domain model davvero non banale, non un semplice CRUD con qualche aggiunta. Far funzionare bene la traversal del commit-graph, i branch head e il diffing è stato il lavoro di modellazione più interessante del progetto.

## L'ingegneria del backend

Il backend segue la Clean Architecture, suddivisa in layer ben distinti:

- **API**: minimal API endpoint, DTO, auth, middleware e documentazione OpenAPI (Scalar e Swagger).
- **Core**: domain model, regole di business e interfacce, senza dipendenze dal framework.
- **Infrastructure**: EF Core, PostgreSQL, repository e migration.

Alcuni pattern su cui mi sono appoggiato:

- **Lo specification pattern** per le query. Il filtraggio di collezioni e mazzi (per nome, set, rarità, condizione, lingua, fascia di prezzo e altro) è espresso come specification componibili, invece di LINQ sparso e ingestibile tra i vari service.
- **Lo Unit of Work**, in modo che i service dipendano da un'unica astrazione invece di accedere direttamente al database context. Questo li rende testabili e mantiene espliciti i confini delle transazioni.
- **Gli scambi in tempo reale.** Due giocatori possono mettere a confronto le proprie wishlist con le carte scambiabili dell'altro e negoziare in tempo reale, con lo stato della sessione tenuto in Redis così da sopravvivere tra le diverse repliche stateless dell'API.
- **Un motore di import pragmatico.** I collezionisti hanno già i loro dati sparsi su sei strumenti diversi (Manabox, Moxfield, Goldfish, Archidekt, Dragonshield, Delver Lens). L'importer li legge tutti, tollera le righe sbagliate invece di interrompere l'intero file e riporta esattamente quali righe ha saltato.
- **Un'auth curata.** Access token JWT a vita breve (un'ora) con refresh token a rotazione (trenta giorni) salvati in Redis, una blacklist dei token al logout, Google OAuth e il refresh consegnato tramite un cookie HttpOnly per il web o un header per il mobile.

Il catalogo delle carte arriva dai bulk data di Scryfall, centinaia di migliaia di carte, caricate attraverso un caching layer (basato su Redis in produzione) così che le lookup durante import, validazione dei mazzi e pricing restino veloci.

Il tutto è coperto da test unitari e di integrazione con xUnit. La suite di integrazione avvia istanze reali di PostgreSQL e Redis tramite Testcontainers, così i test girano contro i motori veri e non contro dei mock.

## Come viene deployata

L'intero sistema gira su un cluster Kubernetes su Hetzner, impacchettato in un singolo Helm umbrella chart di circa 35 template, così che l'intero ambiente sia riproducibile a partire da configurazione versionata.

```
                          Internet
                             |
                      +------v------+
                      |  Nginx /    |   TLS via cert-manager
                      |  Ingress    |   (Let's Encrypt)
                      +--+-------+--+
                  +------v--+ +--v---------+
                  | Frontend| |  API (.NET 10)   |  stateless,
                  +---------+ |  N repliche      |  anti-affinity
                              +--+--------+---+--+  tra i nodi
                                 |        |   |
                       +---------v+ +-----v+ +v--------------+
                       |PostgreSQL| |Redis | | Elasticsearch |
                       |(Stateful)| |cache+| |   + Kibana    |
                       | main DB  | |token | |   (log)       |
                       +----------+ +------+ +---------------+
                       storage persistente su hcloud-volumes
```

- **Nginx** sta davanti al traffico e **cert-manager** emette i certificati TLS Let's Encrypt per i domini pubblici.
- **L'API gira come diverse repliche stateless**, tenute su nodi separati tramite anti-affinity così che il guasto di un singolo nodo non metta giù il servizio.
- **PostgreSQL e Redis** girano come StatefulSet, appoggiati a volumi persistenti Hetzner.
- **Observability**: Serilog spedisce log strutturati (con correlation ID) a Elasticsearch, esplorabili in Kibana, con un job ILM che gestisce la retention. Adminer è disponibile per l'ispezione del database.
- **CI/CD** gira su GitHub Actions. Ogni push lancia l'intera suite di test contro container usa e getta di Postgres e Redis, builda e pusha un'immagine Docker, poi esegue un `helm upgrade` e attende il rollout, con un health check automatico contro l'API live prima di considerare il deploy concluso.

## Progettata per scalare, con una roadmap onesta

Prima del lancio non farò finta di avere traffico di produzione di cui vantarmi. Quello che posso dire è che il sistema è costruito in modo che scalare sia un cambio di configurazione e non una riscrittura:

- **L'API è stateless.** Tutto lo stato condiviso (sessioni, refresh token, cache) vive in Redis, quindi aggiungere repliche è sicuro.
- **Filtraggio e paginazione spingono il lavoro nel database** tramite le specification, invece di tirare le righe in memoria.
- **Anti-affinity e volumi persistenti** sono già in atto per la resilienza.

I prossimi passi onesti, quando il traffico li giustificherà:

- **L'Horizontal Pod Autoscaling.** L'API è pronta; semplicemente l'autoscaler non è ancora collegato.
- **Le read replica di PostgreSQL** per togliere dal primario le query, pesanti in lettura, su catalogo e collezioni.
- **Lo sharding o il CDN-fronting dei bulk card data**, che sono l'asset più grande e più facilmente cacheabile.

Sapere dove un sistema cederà sotto carico, e aver già preso le scelte architetturali che tengono aperte quelle opzioni, è stato per me più utile di qualunque singolo benchmark.

## Cosa mi sono portato a casa

Costruire tutto questo da solo ha significato indossare ogni cappello: domain modeller, backend engineer, database administrator e DevOps. Il version control per i mazzi è ciò che mi ha insegnato di più sul modellare un dominio difficile. Il lavoro con Kubernetes e Helm mi ha insegnato che "production-ready" è soprattutto una checklist di cose poco affascinanti, come TLS, health check, retention dei log e deploy riproducibili, fatte con costanza.

Al momento è live in un ambiente di integrazione pre-launch ma è pronto a tutti gli effetti. Se vuoi metterci le mani:

- Sito: https://surveyl.top
- App: Surveyl su play store
- App: https://app.surveyl.top

Sono sempre felice di parlare di architettura, che sia di questo progetto o dei problemi su cui sta lavorando il tuo team.
