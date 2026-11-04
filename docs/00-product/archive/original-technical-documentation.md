# Multiplayer Tic-Tac-Toe: PRD & Technical Documentation

**Version:** 1.1 (MVP) | **Status:** Archived (frozen) | **Stack:** .NET 10 · Clean Architecture · DDD · CQRS (MediatR) · Identity · PostgreSQL 18 · SignalR · Angular 22 (zoneless) · Tailwind CSS 4

> **Archived 2026-11-04:** this is the originating single-file PRD & technical documentation, relocated from the repository root and **frozen — do not edit**. The living source of truth is [docs/00-product/PRD.md](../PRD.md); the section deep dives live throughout [docs/](../../README.md).

> **v1.1 (2026-11-04):** stack modernized to the latest stable versions — .NET 10, EF Core 10, PostgreSQL 18, Angular 22 (standalone, signals, zoneless), Tailwind CSS 4, NUnit for backend tests, .NET built-in OpenAPI + Scalar. Decisions recorded in [ADR-0011](../../01-architecture/adr/0011-latest-stable-stack.md) and [ADR-0012](../../01-architecture/adr/0012-nunit-test-framework.md).

## 1. Document Overview

This document defines the product requirements (PRD) and the technical blueprint for the first release of a real-time, two-player Tic-Tac-Toe platform. The MVP covers authentication and one simple, polished multiplayer game. The architecture is deliberately built so that later features (ratings, chess-style matchmaking, chat, more games) can be added without rewrites.

| Section | Contents |
| --- | --- |
| 2 to 4 | Product: vision, scope, personas, user stories, requirements |
| 5 | UX/UI specification |
| 6 to 9 | Architecture, domain model, CQRS, infrastructure |
| 10 to 12 | API, real-time contract, database |
| 13 to 16 | Frontend, security, testing, DevOps |
| 17 to 18 | Roadmap, risks, future scope |

## 2. Product Requirements

### 2.1 Vision

A clean, professional, chess.com-inspired experience where anyone can sign up in seconds, see who is online, and play a fast, animated game of Tic-Tac-Toe against another real person.

### 2.2 Goals (MVP)

1. Secure registration and login.
2. A lobby showing online players and open/ongoing games.
3. Create or join a game; a game accepts exactly two players, then locks for everyone else.
4. Real-time, server-authoritative gameplay (X vs O) with animated UI.
5. Many concurrent games; when one is full, other users play in other games.
6. Professional look and feel, responsive on desktop and mobile.
7. Architecture ready for future expansion.

### 2.3 Non-Goals (MVP)

Ratings/ELO, matchmaking queues, chat, friends, spectators, game history UI, social login, email verification, rematch, AI opponent, timers/clocks. These are listed in the roadmap.

### 2.4 Personas

| Persona | Description | Need |
| --- | --- | --- |
| Casual Player (Sara) | Wants a quick game during a break | Zero friction signup, instant play |
| Competitive Player (Rafi) | Likes seeing who is online and challenging others | Clear lobby, fair and reliable game |
| Admin/Developer | Maintains and extends the product | Clean, testable, documented code |

### 2.5 User Stories

| ID | Story | Priority |
| --- | --- | --- |
| US-01 | As a visitor, I can register with username, email and password. | Must |
| US-02 | As a user, I can log in and stay logged in (refresh token). | Must |
| US-03 | As a user, I can log out. | Must |
| US-04 | As a user, I see how many players are online and who they are. | Must |
| US-05 | As a user, I see a list of games: Waiting, In Progress. | Must |
| US-06 | As a user, I can create a new game and wait for an opponent. | Must |
| US-07 | As a user, I can join a waiting game; once two players are in, nobody else can join. | Must |
| US-08 | As a player, I can place my mark when it is my turn, and see my opponent's moves instantly. | Must |
| US-09 | As a player, I see win, lose or draw results with a clear animation. | Must |
| US-10 | As a player, if I leave or disconnect for too long, I forfeit and my opponent wins. | Should |
| US-11 | As a user, I can return to the lobby after a game ends. | Must |
| US-12 | As a user, I can cancel my own waiting game. | Should |
| US-13 | As a user, I can reconnect to my active game after a page refresh. | Should |

### 2.6 Functional Requirements

| ID | Requirement |
| --- | --- |
| FR-01 | Registration requires unique username (3 to 20 chars, letters/digits/underscore), unique valid email, password (min 8, upper, lower, digit). |
| FR-02 | Login by email or username + password; returns short-lived JWT access token and a rotating refresh token. |
| FR-03 | All lobby and game features require authentication. |
| FR-04 | Presence: server tracks connected users via SignalR; lobby shows online count and list. |
| FR-05 | Creating a game sets creator as Player X; game status is Waiting. |
| FR-06 | Joining sets the joiner as Player O, status becomes InProgress, game is removed from joinable list. Joining must be race-safe (only one joiner wins). |
| FR-07 | A user cannot join their own game and cannot be in more than one active (Waiting/InProgress) game at once. |
| FR-08 | X always moves first. Server validates turn, cell range, cell emptiness and game status. |
| FR-09 | Server detects win (8 lines) and draw (9 moves, no winner) and finalizes the game. |
| FR-10 | Lobby updates in real time (game created, joined, finished, cancelled, user online/offline). |
| FR-11 | On disconnect of a player in an active game, a 30 second grace period starts; reconnect cancels it; expiry means forfeit. |
| FR-12 | Completed games are persisted (moves, result, timestamps) for future history features. |

### 2.7 Non-Functional Requirements

| Category | Requirement |
| --- | --- |
| Performance | Move round trip under 200 ms p95 on a normal connection; lobby loads under 1 s. |
| Scalability | Stateless API; target 500 concurrent games on a single node; SignalR Redis backplane planned for scale-out. |
| Reliability | Server-authoritative state; no client can corrupt a game; optimistic concurrency on game updates. |
| Security | See section 14. |
| Usability | Responsive (360 px to 1920 px), WCAG 2.1 AA contrast, keyboard accessible board. |
| Maintainability | Clean Architecture, SOLID, 80%+ unit coverage on Domain and Application. |
| Observability | Structured logging (Serilog), health checks, correlation IDs. |

### 2.8 Success Metrics

Registration completion rate above 80%; average time from login to first move under 30 s; game completion rate above 85%; error rate below 1% of requests.

### 2.9 Assumptions

- One game per user at a time. Creator is always X. Public games only (no private invite links in MVP).
- Email is not verified in MVP. Browser support: latest 2 versions of Chrome, Edge, Firefox, Safari.

## 3. Core Flows

**Auth flow:** Visit app → (not logged in) Login page → Register (optional) → on success, tokens stored → Lobby.

**Game flow:** Lobby → Create Game (X waits) → Opponent clicks Join (O) → both redirected to Game page → alternate moves → result overlay → Back to Lobby.

**Game state machine:**

`Waiting` → (second player joins) → `InProgress` → (win/draw/forfeit) → `Finished`

`Waiting` → (creator cancels or leaves) → `Cancelled`

## 4. Acceptance Criteria (Key Stories)

- **US-07:** Given a Waiting game, when two users click Join simultaneously, exactly one succeeds; the other receives a clear "Game is already full" message and the game disappears from their list.
- **US-08:** Given it is not my turn, when I click a cell, the server rejects the move and the UI shows a subtle shake; state is unchanged.
- **US-10:** Given an active game, when a player's connection drops for more than 30 s, the game finishes with the other player as winner (reason: Forfeit).
- **US-02:** Given an expired access token and valid refresh token, the client silently refreshes and retries the request without logging the user out.

## 5. UX/UI Specification

### 5.1 Design Principles

Clean, modern, calm. Dark-first theme with light option later. Generous spacing, rounded cards, soft shadows, one accent color. Feedback for every action (loading, success, error).

### 5.2 Design Tokens (Tailwind config)

| Token | Value |
| --- | --- |
| Background | slate-950 with subtle radial gradient |
| Surface/Card | slate-900, border slate-800, rounded-2xl |
| Primary | indigo-500 / hover indigo-400 |
| X color | sky-400 (glow) |
| O color | rose-400 (glow) |
| Success / Danger | emerald-500 / red-500 |
| Font | Inter (UI), JetBrains Mono (numbers/timers) |

### 5.3 Pages

**Login:** Split layout on desktop (animated brand panel with floating X and O marks on the left, form card on the right); single column on mobile. Fields: email or username, password (show/hide toggle). Remember me, link to Register, inline validation, loading spinner on button, error toast.

**Register:** Same visual language. Fields: username, email, password, confirm password, live password-strength meter, inline validation messages. Success leads directly to the lobby.

**Lobby:** Top navbar (logo, online count pill, user avatar menu with Logout). Left/main: *Games* panel with tabs Open and In Progress, game cards (host name, status, time created, Join button; disabled and labeled "Full" when taken), prominent **Create Game** button. Right: *Online Players* panel with avatar initials, username, status dot (Available / In game). Empty states with friendly illustrations. Animated list enter/leave transitions.

**Game:** Centered 3x3 board card. Header shows both players (avatar, name, mark) with the active turn highlighted by a pulsing ring. Board cells have hover preview of the player's mark (when it is their turn), mark draw animation (SVG stroke-dash for X and O), winning line animated strike-through with glow, confetti or subtle burst on win, soft shake on invalid move. Waiting state shows "Waiting for opponent..." with animated dots and a Cancel button. Result modal: Victory / Defeat / Draw with Back to Lobby button. Connection badge (Connected / Reconnecting...).

### 5.4 Animations (CSS/Tailwind + Angular animations)

Page transitions fade/slide (200 ms); list items stagger; SVG stroke draw for marks (300 ms); win line draw (500 ms); turn indicator pulse; button press scale; skeleton loaders while fetching.

### 5.5 Accessibility

Visible focus rings, ARIA labels on cells ("Row 1 Column 2, empty"), live region announcing turns and results, color is never the only indicator (marks are shapes), respects prefers-reduced-motion.

## 6. System Architecture

### 6.1 High-Level

```
Angular SPA (Tailwind) ──HTTPS/REST──▶ ASP.NET Core Web API ──▶ PostgreSQL
         │                                   ▲
         └────────── WebSocket (SignalR) ────┘
```

### 6.2 Technology Stack

| Layer | Choice |
| --- | --- |
| Backend | .NET 10 (LTS) / ASP.NET Core Web API, C# 14 |
| Architecture | Clean Architecture, DDD tactical patterns, CQRS |
| Mediator | MediatR (latest; commands, queries, notifications, pipeline behaviors) |
| Validation | FluentValidation |
| Mapping | Mapster (no mapping magic in Domain) |
| ORM | EF Core 10 + Npgsql provider |
| Auth | ASP.NET Core Identity + JWT Bearer + refresh tokens |
| Real-time | SignalR |
| API docs | .NET built-in OpenAPI (Microsoft.AspNetCore.OpenApi) + Scalar UI |
| Logging | Serilog |
| Testing | NUnit 4 (mandated), FluentAssertions, NSubstitute, Testcontainers (PostgreSQL), Respawn |
| Frontend | Angular 22 (standalone components, signals, zoneless change detection), Tailwind CSS 4, @microsoft/signalr |
| Frontend tests | vitest + jsdom (unit), Playwright (E2E) |
| Database | PostgreSQL 18 |
| Containers | Docker, docker-compose |

### 6.3 Clean Architecture Layers & Dependency Rule

Dependencies point inward only: `Api → Infrastructure → Application → Domain`. Api references Application and Infrastructure (for DI composition only). Domain references nothing.

| Project | Responsibility | References |
| --- | --- | --- |
| `TicTacToe.Domain` | Entities, aggregates, value objects, domain events, domain exceptions, repository interfaces *(or in Application, see note)* | none |
| `TicTacToe.Application` | Use cases (commands/queries/handlers), DTOs, validators, behaviors, abstractions (IUnitOfWork, ICurrentUser, ITokenService, IIdentityService, IPresenceTracker, IGameNotifier) | Domain |
| `TicTacToe.Infrastructure` | EF Core DbContext, configurations, migrations, repository implementations, Identity, JWT, presence tracker, clock | Application, Domain |
| `TicTacToe.Api` | Controllers, SignalR hub, middleware, DI composition, OpenAPI/Scalar, auth setup | Application, Infrastructure |

*Note:* Repository **interfaces** live in the Domain project (they speak in aggregates, a classic DDD placement). Implementations are in Infrastructure.

### 6.4 Solution Structure

```
src/
  TicTacToe.Domain/
    Common/            (Entity, AggregateRoot, ValueObject, IDomainEvent)
    Games/             (Game, Move, Mark, GameStatus, GameResult, events, IGameRepository)
    Players/           (PlayerProfile, IPlayerProfileRepository)
    Exceptions/
  TicTacToe.Application/
    Common/            (Behaviors, Interfaces, Results, Exceptions, Mappings)
    Auth/              (Commands: Register, Login, RefreshToken, Logout)
    Games/             (Commands: CreateGame, JoinGame, MakeMove, CancelGame, ForfeitGame; Queries: GetLobbyGames, GetGameById, GetMyActiveGame)
    Players/           (Queries: GetOnlinePlayers)
    DependencyInjection.cs
  TicTacToe.Infrastructure/
    Persistence/       (AppDbContext, Configurations, Migrations, Repositories, UnitOfWork)
    Identity/          (ApplicationUser, IdentityService, TokenService, RefreshToken)
    Realtime/          (PresenceTracker, SignalRGameNotifier)
    DependencyInjection.cs
  TicTacToe.Api/
    Controllers/       (AuthController, GamesController, PlayersController)
    Hubs/              (GameHub)
    Middleware/        (ExceptionHandling, CorrelationId)
    Program.cs
tests/
  TicTacToe.Domain.UnitTests/
  TicTacToe.Application.UnitTests/
  TicTacToe.Api.IntegrationTests/
client/ (Angular app)
docs/
docker-compose.yml
```

## 7. Domain Model (DDD)

### 7.1 Ubiquitous Language

**Game** (a match between two players), **Host/Creator** (Player X), **Opponent/Joiner** (Player O), **Mark** (X or O), **Board** (3x3 grid), **Move** (a mark placed at a cell), **Turn**, **Forfeit**, **Lobby**.

### 7.2 Aggregates

**Game (Aggregate Root)**

| Member | Type | Notes |
| --- | --- | --- |
| Id | Guid |  |
| PlayerXId | Guid | creator (Identity user id) |
| PlayerOId | Guid? | null until joined |
| Status | enum | Waiting, InProgress, Finished, Cancelled |
| CurrentTurn | Mark | X initially |
| Moves | List of Move | child entity (Position 0..8, Mark, PlayerId, MoveNumber, PlayedAtUtc) |
| Result | value object | Outcome (XWins, OWins, Draw), Reason (Normal, Forfeit), WinningLine |
| CreatedAtUtc / StartedAtUtc / FinishedAtUtc | DateTime |  |
| Version | uint (xmin) | optimistic concurrency token |

**Behavior (methods on the aggregate):**

- `Game.Create(hostId)` → status Waiting, raises `GameCreated`.
- `Join(playerId)` → guards: status is Waiting, player is not host; sets O, status InProgress, raises `GameStarted`.
- `MakeMove(playerId, position)` → guards: InProgress, player is in game, it is the player's turn, position 0 to 8 and empty; appends Move, evaluates win/draw, flips turn, raises `MoveMade` and `GameFinished` if ended.
- `Forfeit(playerId)` → opponent wins, raises `GameFinished`.
- `Cancel(playerId)` → only host while Waiting, raises `GameCancelled`.

The board is **derived** from the Moves list (single source of truth); an in-memory `Board` value object is rebuilt on load for rule checks.

**PlayerProfile (small entity)**: Id = Identity user id, Username, CreatedAtUtc; future home of stats and rating. This keeps Identity concerns out of the Domain.

### 7.3 Invariants

1. A game has at most two players, distinct. 2. Moves only while InProgress. 3. Strict alternation starting with X. 4. A cell can be marked once. 5. A finished game is immutable. 6. Result is set exactly once.

### 7.4 Domain Events

`GameCreated`, `GameStarted`, `MoveMade`, `GameFinished`, `GameCancelled`. Dispatched after a successful `SaveChanges` (via an interceptor/UnitOfWork) to MediatR `INotificationHandler`s in Application, which push SignalR messages through the `IGameNotifier` abstraction.

### 7.5 Domain Exceptions

`GameFullException`, `NotYourTurnException`, `CellOccupiedException`, `InvalidPositionException`, `GameNotInProgressException`, `CannotJoinOwnGameException`. Application maps these to `Result` errors / HTTP 409 or 422.

## 8. Application Layer (CQRS)

### 8.1 Principles

Commands change state and go through aggregates/repositories. Queries are read-only; they use `AsNoTracking` projections straight to DTOs via the DbContext read side (a lightweight read model abstraction `IReadDbContext`) and never load aggregates. Same database for MVP; the split allows separate read stores later.

### 8.2 Commands & Queries

| Type | Name | Returns |
| --- | --- | --- |
| Command | RegisterCommand(username, email, password) | AuthResponse |
| Command | LoginCommand(identifier, password) | AuthResponse |
| Command | RefreshTokenCommand(refreshToken) | AuthResponse |
| Command | LogoutCommand(refreshToken) | Unit |
| Command | CreateGameCommand | GameDto |
| Command | JoinGameCommand(gameId) | GameDto |
| Command | MakeMoveCommand(gameId, position) | GameDto |
| Command | CancelGameCommand(gameId) | Unit |
| Command | ForfeitGameCommand(gameId, playerId) | Unit |
| Query | GetLobbyGamesQuery(status?) | List of LobbyGameDto |
| Query | GetGameByIdQuery(gameId) | GameDto |
| Query | GetMyActiveGameQuery | GameDto? |
| Query | GetOnlinePlayersQuery | List of OnlinePlayerDto |

### 8.3 MediatR Pipeline Behaviors (in order)

1. `LoggingBehavior`, 2. `ValidationBehavior` (FluentValidation, throws ValidationException → 400), 3. `UnitOfWorkBehavior` (commits transaction for commands, then dispatches domain events), 4. `PerformanceBehavior` (warn on slow handlers).

### 8.4 Result Pattern

Handlers return `Result<T>` (IsSuccess, Value, Error with Code and Message). Expected failures (invalid credentials, game full) are results, not exceptions. The API maps error codes to HTTP status via a single `ProblemDetails` factory.

### 8.5 Abstractions Owned by Application

`IUnitOfWork`, `ICurrentUser` (UserId, Username), `IIdentityService`, `ITokenService`, `IPresenceTracker`, `IGameNotifier`, `IDateTimeProvider`, `IReadDbContext`.

## 9. Infrastructure Layer

### 9.1 Persistence

`AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` plus `DbSet<Game>`, `DbSet<Move>`, `DbSet<PlayerProfile>`, `DbSet<RefreshToken>`. Fluent configurations per entity (snake\_case naming via `UseSnakeCaseNamingConvention`). Moves are an owned/child collection of Game. Result mapped as an owned type. Migrations live in Infrastructure.

### 9.2 Repositories

One repository per aggregate root: `IGameRepository` (GetByIdAsync, GetActiveGameForPlayerAsync, AddAsync, Update) and `IPlayerProfileRepository`. A generic `Repository<T>` base is optional; do not expose `IQueryable` from repositories. `IUnitOfWork.SaveChangesAsync` is the single commit point.

### 9.3 Identity & Tokens

`ApplicationUser : IdentityUser<Guid>`. Password options: min 8, digit, upper, lower; unique email; lockout after 5 failed attempts for 5 minutes. Access token: JWT, 15 min, claims sub, name, email, jti. Refresh token: 256-bit random, stored **hashed** in `refresh_tokens`, 7 days, rotated on every use, old one revoked (reuse detection revokes the family). On registration, a `PlayerProfile` is created in the same transaction.

### 9.4 Presence

`PresenceTracker` (singleton, ConcurrentDictionary of userId → set of connectionIds) so multiple tabs count as one user. MVP is in-memory; swap for Redis when scaling out.

### 9.5 Background Work

`DisconnectGraceService` (IHostedService) manages the 30 s forfeit timers: started when a player in an InProgress game loses all connections, cancelled on reconnect, executes `ForfeitGameCommand` on expiry. Also cancels stale Waiting games older than 15 minutes.

## 10. REST API Specification

Base URL: `/api/v1`. JSON. Errors follow RFC 7807 `ProblemDetails`. All endpoints except register/login/refresh require `Authorization: Bearer <token>`.

| Method | Endpoint | Description | Success | Errors |
| --- | --- | --- | --- | --- |
| POST | /auth/register | Create account | 201 AuthResponse | 400, 409 (duplicate) |
| POST | /auth/login | Login | 200 AuthResponse | 400, 401, 423 (locked) |
| POST | /auth/refresh | Rotate tokens | 200 AuthResponse | 401 |
| POST | /auth/logout | Revoke refresh token | 204 | 401 |
| GET | /auth/me | Current user | 200 | 401 |
| GET | /players/online | Online players | 200 list | 401 |
| GET | /games?status=waiting,inprogress | Lobby list | 200 list | 401 |
| POST | /games | Create game | 201 GameDto | 409 (already in a game) |
| GET | /games/{id} | Game detail | 200 | 404 |
| GET | /games/active | My active game | 200 or 204 | 401 |
| POST | /games/{id}/join | Join game | 200 GameDto | 404, 409 (full / own game / busy) |
| POST | /games/{id}/moves | Body: `{ position }` | 200 GameDto | 409 (not your turn), 422 (invalid) |
| POST | /games/{id}/cancel | Cancel waiting game | 204 | 403, 409 |
| POST | /games/{id}/leave | Leave / forfeit | 204 | 403 |

**AuthResponse:** `{ accessToken, refreshToken, expiresAtUtc, user: { id, username, email } }`

**GameDto:** `{ id, status, playerX: {id, username}, playerO: {id, username}|null, board: [9 x "X"|"O"|null], currentTurn, result: { outcome, reason, winningLine:[a,b,c] }|null, createdAtUtc }`

**Validation rules:** username 3 to 20 `[A-Za-z0-9_]`; email valid and max 256; password min 8 with upper, lower, digit; position integer 0 to 8.

Moves are submitted over REST (simple, easily validated and tested); results are **broadcast over SignalR**. The hub is used for push, not for state mutation, except for presence.

## 11. Real-Time Contract (SignalR)

Hub endpoint: `/hubs/game` (JWT passed via `access_token` query string for WebSockets; the hub is `[Authorize]`).

**Groups:** `lobby` (all connected users), `game-{id}` (the two players).

**Server → Client events**

| Event | Payload | Sent to |
| --- | --- | --- |
| `PresenceChanged` | `{ onlineCount, players[] }` | lobby |
| `GameCreated` | LobbyGameDto | lobby |
| `GameUpdated` | LobbyGameDto (status/players changed) | lobby |
| `GameRemoved` | `{ gameId }` (started, cancelled, or finished) | lobby |
| `GameStarted` | GameDto | game-{id} |
| `MoveMade` | GameDto | game-{id} |
| `GameFinished` | GameDto (with result) | game-{id} |
| `OpponentConnection` | `{ playerId, connected, graceSecondsLeft? }` | game-{id} |

**Client → Server hub methods:** `JoinGameRoom(gameId)` (validates the caller is a participant, adds to group), `LeaveGameRoom(gameId)`.

**Lifecycle:** OnConnected → add to `lobby`, register presence, broadcast `PresenceChanged`; if the user has an active game, re-add to its group and cancel any forfeit timer. OnDisconnected → remove presence; if the last connection and an InProgress game exists, start the grace timer and emit `OpponentConnection(false)`.

**Automatic reconnect:** the Angular client uses `withAutomaticReconnect([0, 1000, 3000, 5000])` and re-fetches `GET /games/active` after reconnecting to resync.

## 12. Database Design (PostgreSQL)

```
users (AspNetUsers via Identity)
  id uuid PK, user_name, normalized_user_name, email, normalized_email,
  password_hash, security_stamp, lockout_end, access_failed_count, ...

player_profiles
  id uuid PK (= users.id), username varchar(20) UNIQUE, created_at_utc timestamptz

refresh_tokens
  id uuid PK, user_id uuid FK users, token_hash varchar(128) UNIQUE,
  expires_at_utc, created_at_utc, revoked_at_utc NULL, replaced_by_token_hash NULL,
  created_by_ip varchar(45)

games
  id uuid PK, player_x_id uuid FK, player_o_id uuid FK NULL,
  status smallint NOT NULL, current_turn smallint NOT NULL,
  result_outcome smallint NULL, result_reason smallint NULL, result_winning_line smallint[] NULL,
  created_at_utc, started_at_utc NULL, finished_at_utc NULL,
  xmin (concurrency token)

moves
  id uuid PK, game_id uuid FK games ON DELETE CASCADE,
  move_number smallint, position smallint CHECK (0..8), mark smallint, player_id uuid,
  played_at_utc timestamptz
  UNIQUE (game_id, position), UNIQUE (game_id, move_number)
```

**Key indexes and constraints**

- `games(status, created_at_utc DESC)` for lobby queries.
- Partial unique indexes enforcing *one active game per player*: `UNIQUE (player_x_id) WHERE status IN (0,1)` and `UNIQUE (player_o_id) WHERE status = 1`.
- `moves` unique constraints are a last-line defense against double moves.

**Concurrency:** `Join` and `MakeMove` load the game and save with the `xmin` token; on `DbUpdateConcurrencyException` the handler reloads once, re-evaluates the domain rules, and returns the proper domain error (for example "Game is already full"). This makes the "two users join at the same instant" race safe.

## 13. Frontend Architecture (Angular + Tailwind)

### 13.1 Structure

```
client/src/app/
  core/        (auth.service, token.storage, interceptors: auth & error, guards: auth/guest, signalr.service, models)
  shared/      (ui components: button, input, card, avatar, toast, modal, spinner, skeleton)
  features/
    auth/      (login.page, register.page, auth.routes)
    lobby/     (lobby.page, game-list, online-players, create-game-button, lobby.store)
    game/      (game.page, board, cell, player-badge, result-modal, game.store)
  app.routes.ts, app.config.ts
```

### 13.2 Key Decisions

- Standalone components, lazy-loaded feature routes, Angular **signals** for state (`lobby.store`, `game.store`) with **zoneless** change detection, RxJS only for streams and HTTP.
- `authInterceptor` attaches the bearer token; on 401 it calls refresh once (queueing parallel requests), else logs out.
- Route guards: `authGuard` (protects lobby and game), `guestGuard` (redirects logged-in users from login and register).
- `SignalRService` wraps the hub connection, exposes typed event streams, handles reconnect and resync.
- Reactive Forms with shared validators; error messages mapped from ProblemDetails.
- Token storage: access token in memory, refresh token in `localStorage` for MVP (upgrade to an HttpOnly cookie later; see risks).
- Tailwind CSS 4: CSS-first configuration — design tokens (colors, fonts, keyframes for pulse, draw, shake) defined in a `@theme` block in `src/styles.css` (no `tailwind.config.js`); utility components with `@apply` kept minimal.

### 13.3 Routes

`/login`, `/register`, `/lobby` (default), `/game/:id`, `**` → not found.

## 14. Security

| Area | Control |
| --- | --- |
| Passwords | Identity PBKDF2 hashing, strong policy, lockout |
| Tokens | Short-lived JWT, hashed rotating refresh tokens with reuse detection |
| Transport | HTTPS only, HSTS, secure CORS (allow only the Angular origin) |
| Input | FluentValidation on every command; EF parameterization; output encoding by Angular |
| Abuse | Rate limiting (built-in .NET rate limiter): auth endpoints 10/min per IP, moves 5/s per user |
| Authorization | Every game action checks the caller is a participant; hub group joins are validated server-side |
| Headers | CSP, X-Content-Type-Options, X-Frame-Options (via middleware) |
| Secrets | JWT key and DB password from environment or user-secrets, never committed |
| Privacy | Do not log passwords, tokens, or full emails |

## 15. Testing Strategy

| Level | Scope | Tools |
| --- | --- | --- |
| Domain unit | Game rules: join, turn order, all 8 win lines, draw, forfeit, invariants | NUnit, FluentAssertions |
| Application unit | Handlers with mocked repositories, validators, behaviors | NSubstitute |
| Integration | API + real PostgreSQL: register/login/refresh, create/join race, full game playthrough | WebApplicationFactory, Testcontainers, Respawn |
| Hub tests | Two SignalR clients receive correct events | Microsoft.AspNetCore.SignalR.Client |
| Frontend unit | Stores, guards, interceptors, board logic | vitest + jsdom |
| E2E | Register → login → create/join → play to win | Playwright (two browser contexts) |

**Definition of Done:** code reviewed, tests passing, no analyzer warnings, API documented in OpenAPI/Scalar, accessibility checked, works in the Docker environment.

## 16. DevOps & Environment

- `docker-compose.yml`: `postgres:18` (volume), `api`, `client` (nginx serving the Angular build), optional `pgadmin`.
- Config via `appsettings.{Env}.json` + environment variables: `ConnectionStrings__Default`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes`, `Cors__Origins`.
- EF migrations applied on startup in Development; as a separate step in CI/CD for other environments.
- CI (GitHub Actions): restore → build → test (with Testcontainers) → lint client → build client → build Docker images.
- Health checks at `/health` (DB connectivity); Serilog to console (JSON) with correlation ID.
- OpenAPI document (built-in Microsoft.AspNetCore.OpenApi) with Scalar UI enabled in Development, JWT auth support included.

## 17. Delivery Plan

| Milestone | Scope | Est. |
| --- | --- | --- |
| M0 Foundation | Solution skeleton, Docker, DB, CI, Angular + Tailwind setup, design tokens | 2 days |
| M1 Authentication | Identity, JWT/refresh, register/login/logout API; login and register UI | 4 days |
| M2 Domain & Game API | Game aggregate + tests, CQRS commands/queries, repositories, migrations | 4 days |
| M3 Real-time | SignalR hub, presence, lobby events, disconnect/forfeit service | 3 days |
| M4 Lobby UI | Lobby page, game list, online players, create/join/cancel | 3 days |
| M5 Game UI | Board, animations, turn indicator, result modal, reconnect handling | 4 days |
| M6 Hardening | Rate limiting, integration and E2E tests, accessibility, polish, docs | 3 days |

Roughly 23 working days for one full-stack developer; about half with two.

## 18. Risks, Open Questions & Future Scope

### Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Join race condition | Two players in one slot | xmin concurrency + partial unique index + tests |
| Disconnects / flaky mobile networks | Unfair forfeits | 30 s grace, auto-reconnect, resync endpoint |
| Refresh token in localStorage (XSS) | Account takeover | Strict CSP; move to HttpOnly cookie in v1.1 |
| In-memory presence | Breaks with multiple API instances | Redis backplane and presence store before scaling out |
| Over-engineering for a small game | Slower delivery | Keep one bounded context; add complexity only where listed here |

### Open Questions

1. Should the host choose X or O, or random assignment? (MVP: host is X.)
2. Allow one account on two devices at once? (MVP: yes, same presence entry.)
3. Should finished games remain visible in the lobby for a short time? (MVP: removed immediately.)

### Future Scope (post-MVP)

Game history and profile stats; ELO rating and leaderboard; quick-match queue; private invite links; in-game chat and emotes; spectators; rematch; move timers; email verification and password reset; social login; AI opponent; additional games (Connect Four, Chess) via a shared `IGameEngine` abstraction; Redis backplane; admin dashboard; i18n and light theme.

## 19. Glossary

**Aggregate:** a consistency boundary in DDD. **CQRS:** separating write (commands) and read (queries) models. **Backplane:** a shared message bus letting multiple SignalR servers broadcast to each other. **Optimistic concurrency:** detecting conflicting updates via a version token instead of locking. **xmin:** PostgreSQL's system column used as a row version.
