# ADR-0010: Mapster for object mapping (outside the Domain)

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Entity/DTO/command mapping strategy |

## Context

Every command response and query projects between domain objects and DTOs: `Game` + `Moves` → `GameDto` (board array, players, result), `Game` → `LobbyGameDto`, commands → constructor arguments, `PlayerProfile`/Identity user → `PlayerDto`/`AuthResponse`. Hand-writing this is repetitive and error-prone; reflection-based mappers hide bugs behind convention. The Domain must stay free of any mapping magic (PRD: "no AutoMapper magic in Domain").

## Decision

Use **Mapster** with an explicit, centrally-registered configuration, and **manual mapping inside the Domain project**:

- All Mapster configuration lives in Application `Common/Mappings/MappingConfigurations.cs` (registered via `TypeAdapterConfig` / `IRegister`), scanned once at startup in `DependencyInjection.cs`; no attribute decorations scattered over types.
- Mappings used: `Game` → `GameDto` (moves folded into a 9-cell board array; usernames resolved from a preloaded player lookup — no N+1), `Game` → `LobbyGameDto`, command → aggregate-method arguments, `PlayerProfile` → `PlayerDto`, `AuthResult` → `AuthResponse`.
- Explicit member ignoring and `RequireDestinationMemberSource`/strict validation in tests so a renamed DTO property fails a build-time/test-time check instead of silently mapping to null.
- The **Domain project has no mapping library**: aggregate methods construct their own `Move`/`Board`/`GameResult` values by hand; construction is domain logic, not projection.
- Query-side projections use `ProjectToType` where SQL translation is safe (simple DTO shapes); complex projections (lobby list) are explicit `Select` expressions.

Mapping inventory for the MVP (all configured centrally, none in Domain):

| From | To | Used by |
| --- | --- | --- |
| `Game` + moves + player names | `GameDto` (board array derived) | commands, `GetGameById`, `GetMyActiveGame` |
| `Game` + host name | `LobbyGameDto` | `GetLobbyGames`, `GameCreated` broadcast |
| `PlayerProfile` | `PlayerDto`, `OnlinePlayerDto` | player queries, presence broadcasts |
| Identity result + tokens | `AuthResponse` | `Register`, `Login`, `RefreshToken` |

## Consequences

**Positive**

- One registered place lists every mapping — reviewable like any other code, debuggable with breakpoints.
- Source-generation (Mapster.Emit) can be enabled later for AOT-friendly, reflection-free mapping without changing call sites.
- Keeps `MakeMove` handlers thin: handler orchestrates load/mutate/save, mapper produces the response DTO.
- Domain purity rule stays enforceable by NetArchTest (Domain references no external packages).

**Negative**

- A third-party dependency for what LINQ `Select` could express; the lobby/board projections still need custom code for the derived board array.
- Misconfigured silent-null risk exists with any convention mapper — mitigated by strict mapping validation tests in `Application.UnitTests`.
- Team familiarity skews toward AutoMapper from past projects; Mapster's config API differs enough to require a short onboarding pass.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| AutoMapper | Industry default, but heavier runtime, licensing moved to commercial tiers (Digitory) for the latest majors — same cost question as MediatR without the same free-tier clarity; also more "magic by convention" than Mapster's explicit config style |
| Hand-written mapper classes per DTO | Zero magic and trivially debuggable, but ~6 DTO families × maintenance on every Game/Move change is pure boilerplate for an internship timeline |
| Mapperly (source generator) | Strong option (compile-time, no runtime reflection), but younger ecosystem and less Angular/.NET-team familiarity; Mapster with emit covers the same goal if needed |
| JSON-level mapping / dynamic Expando | Loses all type safety; unacceptable in the write path |

## References

- [PRD](../../00-product/PRD.md) (§7 stack table — "Mapster (manual mapping in Domain)")
- [Clean Architecture & DDD](../clean-architecture-ddd.md) — where mappings live in the folder spec
- [CQRS & MediatR](../cqrs-and-mediatr.md) — DTO-producing commands/queries that consume the mappings
- [ADR-0001 Clean Architecture](0001-clean-architecture-and-ddd.md) — Domain purity this decision protects
- [ADR-0009 Result pattern](0009-result-pattern-expected-failures.md) — the payload the mappers produce

## Assumptions

- Usernames for `GameDto.playerX/playerO` are resolved by the handler with a single batched lookup (via `IReadDbContext`) before mapping; the mapper does not query.
- Mapster stays at its Apache-2.0 licensed line; a future license change would trigger a Mapperly migration decision.
