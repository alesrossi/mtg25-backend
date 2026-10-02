# Building a Production-Grade Platform from Scratch: Lessons from a .NET 10 + Kubernetes Side Project

Most side projects stop at "it runs on my laptop". I wanted to find out what it actually takes to ship one the way a real company would, with a clean backend, a proper Kubernetes cluster, observability, and CI/CD. So I built one from end to end. Here is a tour of the engineering.

## What it is, in plain terms

It is a platform for collectors and players of a trading card game. You can catalogue your collection and see what it is worth, build and share decks from those cards, trade with other players, and take part in social features like friends and leagues. The domain happens to be Magic: The Gathering, but the problems underneath are the same ones any inventory, marketplace, or content platform has to solve: large catalogues, complex filtering, pricing, real-time interaction, and a social graph.

It is a .NET 10 backend, deployed and running on a Hetzner Kubernetes cluster, currently in pre-launch hardening.

## The feature I am proudest of: version control for decks

A deck is a living document. Players tweak it constantly, try variants for different tournaments, and want to roll back when an experiment flops. Most apps model this as "edit and overwrite". I modelled it the way Git models source code.

Under the hood there are branches, commits, and a parent-pointer graph, using the same primitives version control does. You can branch a deck to try an idea, commit changes as you iterate, compare two versions to see exactly which cards moved, and walk the full history. The history view runs a breadth-first traversal back through commit parents to reconstruct and order the timeline.

This matters from an engineering point of view because it is a properly non-trivial domain model rather than CRUD with extra steps. Getting the commit-graph traversal, the branch heads, and the diffing right was the most interesting modelling work in the project.

## The backend craft

The backend follows Clean Architecture, split into clear layers:

- **API**: minimal API endpoints, DTOs, auth, middleware, and OpenAPI docs (Scalar and Swagger).
- **Core**: domain models, business rules, and interfaces, with no framework dependencies.
- **Infrastructure**: EF Core, PostgreSQL, repositories, and migrations.

A few patterns I leaned on:

- **The specification pattern** for querying. Collection and deck filtering (by name, set, rarity, condition, language, price range, and more) is expressed as composable specifications instead of sprawling LINQ scattered across services.
- **Unit of Work**, so services depend on a single abstraction rather than reaching into the database context directly. That keeps them testable and the transaction boundaries explicit.
- **Real-time trade sessions.** Two players can match their wishlists against each other's tradeable cards and negotiate live, with session state held in Redis so it survives across stateless API replicas.
- **A pragmatic import engine.** Collectors already have their data spread across six different tools (Manabox, Moxfield, Goldfish, Archidekt, Dragonshield, Delver Lens). The importer reads all of them, tolerates bad rows instead of aborting the whole file, and reports exactly which lines it skipped.
- **Careful auth.** Short-lived JWT access tokens (one hour) with rotating refresh tokens (thirty days) stored in Redis, a token blacklist on logout, Google OAuth, and refresh delivered via an HttpOnly cookie for web or a header for mobile.

The card catalogue itself comes from Scryfall's bulk data, hundreds of thousands of cards, loaded through a caching layer (Redis-backed in production) so lookups during imports, deck validation, and pricing stay fast.

All of it is covered by xUnit unit and integration tests. The integration suite spins up real PostgreSQL and Redis through Testcontainers, so tests run against the actual engines rather than mocks.

## How it is deployed

The whole system runs on a Kubernetes cluster on Hetzner, packaged as a single Helm umbrella chart of roughly 35 templates, so the entire environment is reproducible from version-controlled config.

```
                          Internet
                             |
                      +------v------+
                      |  Nginx /    |   TLS via cert-manager
                      |  Ingress    |   (Let's Encrypt)
                      +--+-------+--+
                  +------v--+ +--v---------+
                  | Frontend| |  API (.NET 10)   |  stateless,
                  +---------+ |  N replicas      |  anti-affinity
                              +--+--------+---+--+  across nodes
                                 |        |   |
                       +---------v+ +-----v+ +v--------------+
                       |PostgreSQL| |Redis | | Elasticsearch |
                       |(Stateful)| |cache+| |   + Kibana    |
                       | main DB  | |tokens| |   (logs)      |
                       +----------+ +------+ +---------------+
                       hcloud-volumes persistent storage
```

- **Nginx** fronts the traffic, and **cert-manager** issues Let's Encrypt TLS for the public domains.
- **The API runs as several stateless replicas**, kept on separate nodes through anti-affinity so a single node failure does not take the service down.
- **PostgreSQL and Redis** run as StatefulSets backed by Hetzner persistent volumes.
- **Observability**: Serilog ships structured logs (with correlation IDs) to Elasticsearch, explorable in Kibana, with an ILM job managing retention. Adminer is available for database inspection.
- **CI/CD** runs on GitHub Actions. Every push runs the full test suite against throwaway Postgres and Redis containers, builds and pushes a Docker image, then performs a `helm upgrade` and waits on the rollout, with an automated health check against the live API before the deploy is considered finished.

## Designed to scale, with an honest roadmap

Before launch I will not pretend there is production traffic to brag about. What I can speak to is that the system is built so that scaling is a configuration change rather than a rewrite:

- **The API is stateless.** All shared state (sessions, refresh tokens, cache) lives in Redis, so adding replicas is safe.
- **Filtering and pagination push work into the database** through specifications, instead of pulling rows into memory.
- **Anti-affinity and persistent volumes** are already in place for resilience.

The honest next steps, once traffic justifies them:

- **Horizontal Pod Autoscaling.** The API is ready for it; the autoscaler simply is not wired up yet.
- **PostgreSQL read replicas** to take the read-heavy catalogue and collection queries off the primary.
- **Sharding or CDN-fronting the bulk card data**, which is the largest and most cacheable asset.

Knowing where a system will bend under load, and having already made the architectural choices that keep those options open, has been more valuable to me than any single benchmark.

## What I took away

Building this on my own meant wearing every hat: domain modeller, backend engineer, database administrator, and DevOps. The version control for decks taught me the most about modelling a hard domain. The Kubernetes and Helm work taught me that "production-ready" is mostly a checklist of unfashionable things, like TLS, health checks, log retention, and reproducible deploys, done consistently.

It is live in a pre-launch integration environment now but it is a fully working site and app. If you would like to poke at it:

- Website: https://surveyl.top
- App: Surveyl on play store
- App: https://app.surveyl.top

I am always happy to talk architecture, whether about this project or the problems your team is working on.
