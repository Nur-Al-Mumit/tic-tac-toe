# V2 Scope — Everything Remaining

*Defines everything after V1.1: the features, the phases (V2.0 → V2.3), and the seams each builds on. Same rule as [v1-scope](v1-scope.md) — every item attaches to an interface that already exists; no rewrites. Single-node throughout (no Redis unless a phase explicitly requires it — see §7).*

Parent document: [PRD](PRD.md) · Previous: [v1-scope](v1-scope.md) · Build order: [AGENTS.md](../agents/AGENTS.md)

---

## 1. Where V1.1 left off

Shipped: auth, live game, move clock (60 s) + waiting clock (120 s), live lobby (tabs, presence panel), returning-player choice (rejoin/resign), arena UI, rate limiting + security headers, board keyboard navigation. 76 backend + 17 frontend tests, CI containerless.

Not shipped anywhere yet: ratings, matchmaking, rematch, spectators, chat, history, email flows, social login, AI opponent, more games, admin, i18n/light theme, E2E harness, deployment. That is V2.

## 2. Phase map (build order)

| Phase | Theme | Contents | Est. |
| --- | --- | --- | --- |
| **V2.0** | Compete | ELO rating + leaderboard + profile stats + game history with replay | 3 d |
| **V2.1** | Play faster | Quick-match queue + rematch + private invite links + in-game chat/emotes + spectators | 3–4 d |
| **V2.2** | Grow the platform | Email verification + password reset + social login + AI opponent | 3–4 d |
| **V2.3** | Harden & ship | HttpOnly refresh cookie + admin dashboard + i18n + light theme + Playwright E2E + load test + first real deployment | 3–4 d |
| **V2.4** | More games | `IGameEngine` abstraction + Connect Four (first second game) | 3–4 d |

Total: **≈ 15–19 dev days.** Phases are ordered by value; each is independently shippable.

## 3. V2.0 — Compete (ratings, stats, history)

### 3.1 ELO rating (every finished game)

- `PlayerProfile` gains `Rating int` (start 1200), `PeakRating`, `Wins/Losses/Draws/Timeouts`, `Streak` — one migration.
- Domain: `GameFinished` event carries outcome + reason; a new `RatingCalculation` domain service applies Elo (K=32, win/loss/draw expectations) to both profiles **in the same handler transaction** as the finish — never a separate follow-up call (no lost-update window).
- Tie-in with clocks: timeout wins count as wins; forfeits count as wins; draws split.
- New query `GetLeaderboardQuery` → top 50 by rating (`IReadDbContext` projection, cached 30 s in-memory — no Redis).

### 3.2 Leaderboard UI

- Route `/leaderboard`: rank, username, rating, W/L/D, streak; podium top-3 (arena styling); own row highlighted; updates on refresh (no hub push — ratings churn is low).
- Navbar link with trophy icon.

### 3.3 Profile + game history (US FR-12 payoff)

- Route `/profile/:id` (own via navbar avatar, others via leaderboard/lobby click): stats grid (rating chart sparkline optional, W/L/D, timeout count), rating history table, recent games list (result, opponent, reason, duration, moves count).
- `GetPlayerStatsQuery` (read-side projection over `games`/`moves`).
- **Replay**: route `/replay/:gameId` — read-only board stepping through moves (◀ ▶ buttons + scrubber; the board component already derives state from a move list, so replay is a pure read-side consumer: board slice `[0..n]`).

## 4. V2.1 — Play faster

### 4.1 Quick-match queue

- `MatchmakingTickets` table: id, playerId (unique active ticket), createdAt, matchedGameId nullable — **DB-backed, not Redis** (single-node; survives restarts).
- `POST /matchmaking/queue` → 202; `DELETE /matchmaking/queue` → 204; `GET /matchmaking/status`.
- `MatchmakerService` (hosted, 2 s scan): pairs the two longest-waiting tickets (rating proximity window widening over time: ±100 → ±∞ after 30 s), creates the game via `GameCreateCommand`/`GameJoinCommand` equivalents, notifies both via `user-{id}` hub group (`QuickMatchFound { gameId }`) → client navigates. Ticket cleanup on match/queue-leave/game end.
- UI: lobby "Quick match" primary button → searching state (animated, cancellable).

### 4.2 Rematch

- `POST /games/{id}/rematch` — only a finished game's two players; creates a NEW game with marks swapped (loser moves first as X), both players get `RematchOffered`/`RematchStarted` hub events; one-click accept flow on the result modal ("Rematch" replaces "Back to lobby" when opponent is online — presence already tells us).

### 4.3 Private invite links

- `Game` gains `InviteCode char(6)` (nanoid-style, unique among active games) + `IsPublic bool`. `POST /games` gets `{ isPublic: false }` → returns deep link `/join/{code}`; `POST /games/join-by-code/{code}` resolves + joins (same race guards). Lobby list filters `IsPublic`.

### 4.4 In-game chat + emotes

- Hub method `SendGameMessage(gameId, text)` (participant-validated, 280 chars, 5 msgs/10 s rate limit) → `GameMessage` event to `game-{id}` (not persisted in MVP; a `game_messages` table is a V2.4+ decision).
- Emotes: fixed set (👍 😂 😱 🎉 🤝) as hub method `SendEmote` → `Emote` event; client floats them over the opponent's tile.
- UI: chat drawer under the board (collapsed by default, spectating-friendly).

### 4.5 Spectators

- Hub `JoinGameRoom` gains a spectator path: non-participants join `game-{id}-spectators` (read-only: receive `GameStarted/MoveMade/GameFinished`, never mutate). `SpectatorCount` in `GameDto` (tracker map, in-memory). Lobby "In Progress" cards get a "Watch" button; spectated games show "Watching — moves only."

## 5. V2.2 — Grow the platform

### 5.1 Email verification + password reset

- SMTP via `IEmailService` (Contracts/Services seam; `MailKit` implementation, configured `Smtp:*`) — **external dependency: an SMTP account** (Brevo/Mailtrap free tiers).
- `AspNetUser.EmailConfirmed` flow: register → token email → `POST /auth/verify-email { token }`. Login warns (but allows) unverified.
- Password reset: `POST /auth/forgot-password` (always 202 — no enumeration) → emailed token → `POST /auth/reset-password { token, newPassword }` revokes all refresh tokens.
- UI: `/verify` and `/reset-password` pages matching the arena theme.

### 5.2 Social login

- Google + GitHub OAuth: `ApplicationUser` logins table already supports it (`AddIdentity` stores). `GET /auth/social/{provider}` → challenge → callback mints local JWT pair (same token service). Buttons on login page (arena-styled provider icons).
- **External dependency:** OAuth apps created for the deployment origin.

### 5.3 AI opponent

- `IOpponentEngine` (Contracts/Services): `Task<int> ChooseMove(Game game, CancellationToken)`.
- Implementation 1: minimax (Tic-Tac-Toe is solved — perfect play, <1 ms) with difficulty levels (random-move probability for Easy/Medium). Registered `ai-player` user seeded on startup; quick-match "vs AI" toggle creates a game against it; moves made by a hosted `AiMoveService` reacting to `MoveMade`/`GameStarted` notifications for games with the AI participant (0.6 s thinking delay for feel).

## 6. V2.3 — Harden & ship

### 6.1 HttpOnly refresh cookie (PRD risk payoff)

- Refresh token moves from localStorage to an HttpOnly, Secure, SameSite=Strict cookie scoped to `/api/v1/auth`; CSRF-safe (refresh POST is same-site only + custom header). Client `TokenStorage` loses the localStorage path; logout clears cookie server-side. (Access token stays in memory as today.)

### 6.2 Admin dashboard

- Role `Admin` (seeded); `[Authorize(Roles = "Admin")]` area: users list (lock/unlock), active games (force-cancel), stats (DAU proxy: logins/day from Serilog files). No separate SPA — `/admin` routes in the same client guarded by role.

### 6.3 i18n + light theme

- i18n: `@angular/localize` with `en` + `bn` (locale files under `client/src/locale/`); language switcher in navbar.
- Light theme: `@theme` tokens get a `[data-theme="light"]` override block (the token system was built for this); toggle persisted in localStorage; respects `prefers-color-scheme` on first visit.

### 6.4 E2E + load

- Playwright suite (two contexts): register → quick match → play to win; timeout path (short clock via env); presence; reconnect resync. CI job `e2e` (needs Docker compose stack — returns to CI per ADR-0015's note).
- Load: k6 script — 50 VUs creating/playing games; p95 move < 200 ms, zero 5xx; run ad hoc pre-release, not per-PR.

### 6.5 First deployment

- VPS (or any Docker host): `docker compose -f docker-compose.prod.yml up` — GHCR images from CI, caddy/nginx TLS in front, `pg_dump` nightly cron to the logs volume. Deployment runbook page in [docker-and-environments](../06-devops/docker-and-environments.md).

## 7. V2.4 — More games (`IGameEngine`)

- Extract rules behind `IGameEngine { int BoardSize; IReadOnlyList<int[]> WinLines; GameResult Evaluate(IReadOnlyList<Move>); }` — Tic-Tac-Toe becomes the first implementation; `Game` aggregate takes the engine's board size (generalizes 0..N cells, `Board` helper already parameterizable).
- **Connect Four**: 7×6, win = 4-in-line, gravity column-drop input (`position` = column 0–6); move deadline shared; rating shared per-game-type (`PlayerProfile.GameStats` keyed by engine) or separate — decision ADR when building.
- Lobby/quick-match/hub all engine-agnostic already (they only speak `GameDto`); the UI needs a board renderer per engine (grid 7×6 for C4).

## 8. Redis — when, exactly

Not needed for any phase above **as long as the API runs as a single instance** (true through V2.4 at projected load). It becomes mandatory only when: (a) running >1 API replica (SignalR backplane + shared presence), or (b) quick-match wants cross-node fairness. The seams are ready: `IPresenceTracker` and `IGameNotifier` are single implementations behind interfaces; swapping in Redis versions is contained work, tracked as its own ADR when that day comes.

## 9. Traceability

| PRD future-scope item | Phase |
| --- | --- |
| Game history & profile stats | V2.0 |
| ELO rating & leaderboard | V2.0 |
| Quick-match queue | V2.1 |
| Private invite links | V2.1 |
| In-game chat & emotes | V2.1 |
| Spectators | V2.1 |
| Rematch | V2.1 |
| Email verification & password reset | V2.2 |
| Social login | V2.2 |
| AI opponent | V2.2 |
| More games (Connect Four) | V2.4 |
| Admin dashboard | V2.3 |
| i18n & light theme | V2.3 |
| HttpOnly refresh cookie | V2.3 |
| Redis backplane | On demand (§8) |

## Assumptions

- Single API instance through V2.4 (vertical scaling only); Redis per §8.
- External accounts needed: SMTP (V2.2 email), OAuth apps (V2.2 social), a host for deployment (V2.3). None block earlier phases.
- Ratings apply to Tic-Tac-Toe only until V2.4 introduces the engine split.
- Chat is not persisted in V2.1 (ephemeral); persistence is a deliberate product decision later.
- Numbers (Elo K, queue windows, emote set, AI difficulties) are starting values — tune after playtesting, not before.
