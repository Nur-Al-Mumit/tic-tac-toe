# User Stories & Acceptance Criteria — Multiplayer Tic-Tac-Toe

*Expands the 13 user stories of the [PRD](PRD.md#31-user-stories) into full, testable acceptance criteria with priority, milestone, functional-requirement traceability and UI touchpoints.*

---

## 1. How to Read This Document

Each story below is expanded from the PRD's user story table into a self-contained specification: a **description**, a metadata block (**priority**, **milestone**, **related FRs**, **UI touchpoints**), and **acceptance criteria in Gherkin** (`Given/When/Then`, one `Feature` block per story, happy and unhappy paths). The four unhappy paths called out in the PRD's key acceptance criteria — the join race (US-07), the invalid move shake (US-08), the disconnect forfeit (US-10) and the silent token refresh (US-02) — appear as named scenarios.

Conventions used in the scenarios:

- **Sara** is the host (**Player X**, sky-400), **Rafi** is the joiner (**Player O**, rose-400); the host is always X and X always moves first.
- Board cells are numbered **0–8**, row-major.
- HTTP verbs and status codes follow the [API specification](../02-backend/api-spec.md); push events follow the [SignalR contract](../02-backend/realtime-signalr.md) (hub `/hubs/game`, groups `lobby` and `game-{id}`).
- "In real time" / "instantly" means *without a manual page reload*; the move round-trip budget is the PRD's 200 ms p95.
- Milestone notation `M2 → M4` means: backend API lands in M2, the consuming UI in M4 (see the [roadmap](roadmap.md)).

Personas (Sara the casual player, Rafi the competitive player) are defined in the [PRD](PRD.md#21-personas); page and component names match [pages and flows](../04-frontend/pages-and-flows.md) and the [design system](../04-frontend/ux-ui-design-system.md).

---

## 2. Stories

### US-01 — Register an account

As a visitor, I can register with a username, email and password, so that I get an account and can play immediately.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M1 Authentication |
| **Related FRs** | FR-01 |
| **UI touchpoints** | Register page (`/register`); shared `input`, `button`, `toast` components; password-strength meter; `auth.service` |

```gherkin
Feature: Registration (US-01)
  Rule: anyone can create an account; username and email must be unique and well-formed,
  and the password must satisfy the strength policy

  Background:
    Given the visitor is on the Register page "/register"
    And the form shows the fields "Username", "Email", "Password" and "Confirm password"

  Scenario: Successful registration
    When the visitor submits username "sara_01", email "sara@example.com" and password "Str0ngPass"
    Then POST /api/v1/auth/register returns 201 with an AuthResponse
    And the response contains an access token, a refresh token, the expiry and the user profile
    And a PlayerProfile row is created in the same transaction as the Identity user
    And the client stores the tokens and redirects to "/lobby"

  Scenario: Username already taken
    When the visitor submits a username that already exists
    Then the API returns 409 with a ProblemDetails body identifying the duplicate field
    And an inline error is shown under the username field and the visitor stays on "/register"

  Scenario: Email already registered
    When the visitor submits an email that already exists
    Then the API returns 409 with a ProblemDetails body identifying the duplicate field
    And an inline error is shown under the email field

  Scenario: Password does not meet the policy
    When the visitor submits a password shorter than 8 characters,
    or one missing an uppercase letter, a lowercase letter or a digit
    Then the API returns 400 with a validation error listing the unmet rules
    And the inline field error names the missing rule(s)

  Scenario: Malformed username
    When the visitor submits a username shorter than 3 characters, longer than 20 characters,
    or containing characters outside [A-Za-z0-9_]
    Then the API returns 400 with a validation error and no account is created

  Scenario: Malformed email
    When the visitor submits an invalid email address or one longer than 256 characters
    Then the API returns 400 with a validation error under the email field

  Scenario: Confirmation mismatch caught client-side
    When the "Password" and "Confirm password" fields differ
    Then the form blocks submission with an inline message and no request is sent
```

### US-02 — Log in and stay logged in

As a user, I can log in with email or username and password, and I stay logged in thanks to a rotating refresh token.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M1 Authentication |
| **Related FRs** | FR-02, FR-03 |
| **UI touchpoints** | Login page (`/login`) with show/hide password and "remember me"; `auth.service`; `authInterceptor` with single-flight refresh; `token.storage`; `authGuard` / `guestGuard` |

```gherkin
Feature: Login and session persistence (US-02)

  Scenario: Login with email
    Given a registered user with email "sara@example.com" and password "Str0ngPass"
    When the user submits that email and password on "/login"
    Then POST /api/v1/auth/login returns 200 with an AuthResponse
    And the access token expires in 15 minutes and the refresh token in 7 days
    And the user is redirected to "/lobby"

  Scenario: Login with username
    Given the same user registered with username "sara_01"
    When the user enters "sara_01" as the identifier with the correct password
    Then login succeeds exactly as with the email identifier

  Scenario: Wrong password
    When the user submits a wrong password for an existing account
    Then the API returns 401 with a generic invalid-credentials message
    And the login form shows an error toast without revealing which field was wrong

  Scenario: Unknown identifier
    When the user submits an identifier that matches no account
    Then the API returns the same generic 401 as for a wrong password
    And no information is leaked about which accounts exist

  Scenario: Lockout after repeated failures
    Given 5 consecutive failed login attempts for the same account
    When the user submits a sixth attempt, even with the correct password
    Then the API returns 423 and the account is locked for 5 minutes

  Scenario: Silent refresh on an expired access token (PRD key acceptance criterion)
    Given the user is logged in and the access token has expired
    And the refresh token is still valid
    When the client sends a request that receives 401
    Then the auth interceptor calls POST /api/v1/auth/refresh exactly once
    And retries the original request with the new access token successfully
    And the user remains logged in with no visible interruption

  Scenario: Parallel requests queue behind one refresh
    Given an expired access token and three in-flight API calls that all receive 401
    When the interceptor handles them
    Then only a single refresh call is made and all three requests are retried with the new token

  Scenario: Refresh token rotation with reuse detection
    Given a refresh token that has already been rotated
    When it is presented to POST /api/v1/auth/refresh
    Then the API returns 401 and revokes the entire token family it belonged to
    And the client clears stored tokens and redirects to "/login"

  Scenario: Refresh token expired after 7 days
    Given the user has not visited the app for more than 7 days
    When the stored refresh token is used
    Then the API returns 401 and the user must log in again
```

### US-03 — Log out

As a user, I can log out, ending my session on this device.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M1 Authentication |
| **Related FRs** | FR-02, FR-03 |
| **UI touchpoints** | User avatar menu in the lobby navbar; `auth.service`; `token.storage`; `authGuard` |

```gherkin
Feature: Logout (US-03)

  Scenario: Logout revokes the refresh token
    Given a logged-in user on any page
    When the user opens the avatar menu and clicks "Logout"
    Then POST /api/v1/auth/logout returns 204 and the refresh token is revoked server-side
    And the client discards both tokens and navigates to "/login"

  Scenario: A revoked refresh token cannot mint a new session
    Given the user logged out in the previous scenario
    When the old refresh token is presented to POST /api/v1/auth/refresh
    Then the API returns 401 and no new tokens are issued

  Scenario: Logging out mid-game ends in forfeit if the user does not return
    Given a logged-in player in an InProgress game
    When the player logs out and does not reconnect within the 30 second grace period
    Then the game finishes with the opponent as winner and reason Forfeit (US-10)

  Scenario: Guest navigation
    Given a visitor with no stored tokens
    When they navigate to a protected route such as "/lobby"
    Then the auth guard redirects to "/login" without calling the API
```

### US-04 — See who is online

As a user, I see how many players are online and who they are.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M3 Real-time (backend presence) → M4 Lobby UI |
| **Related FRs** | FR-03, FR-04 |
| **UI touchpoints** | Lobby navbar online-count pill; Online Players panel (`online-players` component with avatars and status dots); `signalr.service`; `PresenceChanged` events |

```gherkin
Feature: Online presence (US-04)

  Background:
    Given a logged-in user on the Lobby page
    And an authenticated SignalR connection to "/hubs/game" joined to the "lobby" group

  Scenario: Online count is visible
    Then the navbar pill shows the number of online players
    And it matches the list returned by GET /api/v1/players/online

  Scenario: Another player comes online
    Given the pill shows 3 online players
    When a fourth player connects
    Then a PresenceChanged event updates the pill to 4
    And the new player appears in the Online Players panel without a page reload

  Scenario: A player goes offline
    When an online player closes their last tab
    Then PresenceChanged removes them from the panel and decrements the count within seconds

  Scenario: Multiple tabs count as one user
    Given Sara is online with one tab
    When she opens a second tab
    Then the online count does not change
    And her single presence entry maps to two connection ids

  Scenario: The hub requires authentication
    When an anonymous client tries to connect to "/hubs/game"
    Then the connection is rejected with 401 and no presence is registered
```

### US-05 — See the list of games

As a user, I see a list of games: Waiting, In Progress.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M2 (list query) + M3 (live events) → M4 Lobby UI |
| **Related FRs** | FR-03, FR-10 |
| **UI touchpoints** | Lobby Games panel (`game-list` component) with "Open" and "In Progress" tabs and game cards; `lobby.store`; `GameCreated` / `GameUpdated` / `GameRemoved` events; empty states |

```gherkin
Feature: Lobby game list (US-05)

  Background:
    Given a logged-in user on the Lobby page

  Scenario: Tabs reflect game status
    When the lobby loads
    Then GET /api/v1/games?status=waiting,inprogress returns the current games
    And the "Open" tab lists Waiting games showing host name, status and creation time, newest first
    And the "In Progress" tab lists InProgress games

  Scenario: A new game appears in real time
    When another user creates a game
    Then a GameCreated event inserts the card at the top of the "Open" tab without a reload

  Scenario: A waiting game starts
    When a second player joins a Waiting game
    Then the game leaves the "Open" tab and appears under "In Progress"

  Scenario: A game disappears when it ends
    When a game is finished or cancelled
    Then a GameRemoved event removes its card from both tabs immediately

  Scenario: Empty state
    Given no games exist
    Then the "Open" tab shows a friendly empty-state illustration
    And the "Create Game" call-to-action is prominently visible
```

### US-06 — Create a game

As a user, I can create a new game and wait for an opponent.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M2 (API) → M4 (UI) |
| **Related FRs** | FR-03, FR-05, FR-07 |
| **UI touchpoints** | "Create Game" button (`create-game-button`); Game page waiting state ("Waiting for opponent…" with animated dots and Cancel); `GameCreated` event |

```gherkin
Feature: Create game (US-06)

  Background:
    Given a logged-in user on the Lobby page who is not in any active game

  Scenario: Create and wait for an opponent
    When the user clicks "Create Game"
    Then POST /api/v1/games returns 201 with a GameDto
    And the creator is PlayerX, the status is Waiting and playerO is null
    And the user is navigated to "/game/{id}" showing "Waiting for opponent..." with a Cancel button
    And other users see the game appear in their "Open" tab via GameCreated

  Scenario: Only one active game per user
    Given the user already created or joined a game that is Waiting or InProgress
    When the user clicks "Create Game"
    Then the API returns 409 and the UI explains "You are already in a game"
    And no second game is created and no navigation happens

  Scenario: Authentication required
    Given a request without a valid bearer token
    When POST /api/v1/games is called
    Then the API returns 401 and no game row is created

  Scenario: Stale waiting game is swept
    Given a Waiting game created more than 15 minutes ago
    When the stale-game sweep runs
    Then the game status becomes Cancelled
    And a GameRemoved event removes it from every lobby
```

### US-07 — Join a game

As a user, I can join a waiting game; once two players are in, nobody else can join.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M2 (API) → M4 (UI) |
| **Related FRs** | FR-03, FR-06, FR-07, FR-10 |
| **UI touchpoints** | Join button on game cards (disabled and labelled "Full" once taken); `lobby.store`; `GameStarted` broadcast; navigation to `/game/:id` |

```gherkin
Feature: Join game (US-07)

  Background:
    Given a Waiting game created by another user is visible in the "Open" tab

  Scenario: Join successfully
    When the user clicks "Join"
    Then POST /api/v1/games/{id}/join returns 200 with a GameDto
    And the joiner becomes PlayerO, the status becomes InProgress and started_at_utc is set
    And a GameStarted event is broadcast to group "game-{id}"
    And both players are taken to the game page with X to move
    And the game is removed from every lobby's joinable list

  Scenario: Join race — exactly one winner (PRD key acceptance criterion)
    Given a Waiting game with one free slot
    When two users click "Join" at the same instant
    Then exactly one join returns 200
    And the other returns 409 with a clear "Game is already full" message
    And the game disappears from the losing user's "Open" list

  Scenario: Cannot join your own game
    When the host clicks "Join" on their own Waiting game
    Then the API returns 409 and the game stays Waiting

  Scenario: Cannot join while in another active game
    Given the user is a player in a Waiting or InProgress game
    When the user clicks "Join" on a different game
    Then the API returns 409 with a "you are already in a game" error

  Scenario: Cannot join a non-waiting game
    Given a game that is InProgress, Finished or Cancelled
    When the user calls POST /api/v1/games/{id}/join
    Then the API returns 409 and no state changes
    And joining a non-existent game id returns 404
```

### US-08 — Make moves

As a player, I can place my mark when it is my turn, and see my opponent's moves instantly.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M2 (API) → M5 (UI) |
| **Related FRs** | FR-08, FR-09, FR-12 |
| **UI touchpoints** | Game page board (`board`, `cell` components) with hover preview; turn indicator (`player-badge` pulsing ring); invalid-move shake animation; `MoveMade` broadcast; `game.store` |

```gherkin
Feature: Making moves (US-08)

  Background:
    Given an InProgress game between PlayerX Sara and PlayerO Rafi
    And both players are on "/game/{id}" with the SignalR group "game-{id}" joined

  Scenario: X always moves first
    When the game starts
    Then the turn indicator highlights Sara (X)
    And any move attempt by Rafi (O) before Sara's first move is rejected with 409

  Scenario: Place a mark on my turn
    When Sara clicks the empty cell 4
    Then POST /api/v1/games/{id}/moves with body { "position": 4 } returns 200 with the updated GameDto
    And a MoveMade event updates Rafi's board within the 200 ms p95 budget
    And the turn indicator switches to Rafi (O)

  Scenario: Not my turn (PRD key acceptance criterion)
    Given it is Sara's (X) turn
    When Rafi clicks an empty cell
    Then the server rejects the move with 409
    And Rafi's board shows a subtle shake and the state is unchanged

  Scenario: Occupied cell
    When the player whose turn it is clicks a cell that already holds a mark
    Then the server rejects the move with 422 and the board is unchanged

  Scenario: Position out of range
    When a move is submitted with position 9 or any value outside 0–8
    Then the API returns 422 with a validation error

  Scenario: Finished game is immutable
    Given the game already has a Result
    When either player submits a move
    Then the API returns 409 and no move is appended

  Scenario: Only participants can move
    Given a third user who is not a player in the game
    When they call POST /api/v1/games/{id}/moves
    Then the API returns 403
```

### US-09 — See the result

As a player, I see win, lose or draw results with a clear animation.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M2 (API) → M5 (UI) |
| **Related FRs** | FR-09, FR-10, FR-12 |
| **UI touchpoints** | Result modal (`result-modal` with Victory / Defeat / Draw and "Back to Lobby"); winning-line draw animation (500 ms, glow); subtle burst on win; ARIA live region |

```gherkin
Feature: Game result (US-09)

  Background:
    Given an InProgress game where both players see the live board

  Scenario: Win with a line
    When a player's move completes one of the 8 winning lines
    Then the server finalizes the game with a Result of reason Normal and the winning line's three cells
    And a GameFinished event with the Result reaches both players
    And the winning line draws across the three cells in 500 ms in the winner's mark colour with glow
    And the result modal shows "Victory" to the winner and "Defeat" to the loser

  Scenario: Draw
    When nine moves are played and no line is complete
    Then the Result outcome is Draw with no winning line
    And both players see the "Draw" modal

  Scenario: Both screens agree
    When the game finishes
    Then both clients display the same final board, outcome, reason and winning line

  Scenario: The board locks after the result
    When the result modal is shown
    Then cells are no longer clickable
    And any further move submission is rejected server-side with 409

  Scenario: The result is persisted
    When the game finishes
    Then all moves, the Result and finished_at_utc are stored for future history features
```

### US-10 — Disconnect means forfeit after the grace period

As a player, if I leave or disconnect for too long, I forfeit and my opponent wins.

| | |
| --- | --- |
| **Priority** | Should |
| **Milestone** | M3 Real-time (grace service); connection badge and countdown surfaced in M5 Game UI |
| **Related FRs** | FR-09, FR-11, FR-12 |
| **UI touchpoints** | Connection badge (Connected / Reconnecting…); opponent grace countdown; `OpponentConnection` event; `DisconnectGraceService`; result modal with reason Forfeit |

```gherkin
Feature: Disconnect forfeit (US-10)

  Background:
    Given an InProgress game between PlayerX and PlayerO

  Scenario: Grace expiry forfeits the game (PRD key acceptance criterion)
    When a player's last connection drops and stays gone for more than 30 seconds
    Then the grace timer expires and the game finishes
    And the opponent wins with Result reason Forfeit
    And a GameFinished event informs both sides and the lobby removes the game

  Scenario: Reconnect inside the grace window
    When a player loses connection and reconnects within 30 seconds
    Then the grace timer is cancelled
    And the game continues from the current board with no moves lost

  Scenario: The waiting side sees the grace period
    When one player's opponent disconnects
    Then the remaining player sees a "Reconnecting..." badge on the opponent
    Together with a countdown of the remaining grace seconds

  Scenario: Only the last connection counts
    Given PlayerO has the game open in two tabs
    When one of the tabs is closed
    Then no grace timer starts because the user still has an active connection

  Scenario: A forfeit is persisted like any other result
    When a forfeit finalizes the game
    Then the Result with reason Forfeit and finished_at_utc are persisted
    And both players' result modals reflect the forfeit outcome
```

### US-11 — Return to the lobby after a game

As a user, I can return to the lobby after a game ends.

| | |
| --- | --- |
| **Priority** | Must |
| **Milestone** | M5 Game UI |
| **Related FRs** | FR-07, FR-10 |
| **UI touchpoints** | "Back to Lobby" button in the result modal; lobby navbar; `lobby.store` refresh |

```gherkin
Feature: Return to lobby (US-11)

  Background:
    Given a player has just seen the result modal on "/game/{id}"

  Scenario: Back to lobby
    When the player clicks "Back to Lobby"
    Then the app navigates to "/lobby"
    And the finished game does not appear in either tab
    Because finished games are removed from the lobby immediately per the PRD's open-question resolution

  Scenario: Free to play again
    When the player returns to the lobby after their game ended
    Then "Create Game" and "Join" are enabled
    Because the one-active-game slot is released by the Finished status

  Scenario: The lobby is current
    When the player arrives at the lobby
    Then the online players list and both game tabs reflect live state without a manual reload
```

### US-12 — Cancel my waiting game

As a user, I can cancel my own waiting game.

| | |
| --- | --- |
| **Priority** | Should |
| **Milestone** | M2 (API) → M4 (UI) |
| **Related FRs** | FR-05, FR-10 |
| **UI touchpoints** | Cancel button in the Game page waiting state; `GameRemoved` event; return navigation to `/lobby` |

```gherkin
Feature: Cancel waiting game (US-12)

  Background:
    Given a host viewing their own Waiting game on "/game/{id}"

  Scenario: Host cancels
    When the host clicks "Cancel"
    Then POST /api/v1/games/{id}/cancel returns 204 and the status becomes Cancelled
    And a GameRemoved event removes the game from every lobby
    And the host is returned to "/lobby"

  Scenario: Non-host cannot cancel
    Given a user who is not the host of the game
    When they call POST /api/v1/games/{id}/cancel
    Then the API returns 403 and the game stays Waiting

  Scenario: A started game cannot be cancelled
    Given the game is InProgress
    When the host calls POST /api/v1/games/{id}/cancel
    Then the API returns 409
    Because leaving an InProgress game is a forfeit (US-10), not a cancel

  Scenario: Unknown or already ended game
    When cancel is called for a game id that does not exist
    Then the API returns 404
    When cancel is called for a game that is already Finished or Cancelled
    Then the API returns 409 and nothing changes
```

### US-13 — Reconnect to my active game

As a user, I can reconnect to my active game after a page refresh.

| | |
| --- | --- |
| **Priority** | Should |
| **Milestone** | M5 Game UI (edge cases re-verified and hardened in M6) |
| **Related FRs** | FR-03, FR-11 |
| **UI touchpoints** | `authGuard` on `/game/:id`; `GET /api/v1/games/active` resync; `signalr.service` `withAutomaticReconnect`; connection badge |

```gherkin
Feature: Reconnect after refresh (US-13)

  Background:
    Given a logged-in player with a valid refresh token

  Scenario: Refresh during an InProgress game
    When the player reloads the page mid-game
    Then GET /api/v1/games/active returns the game
    And the board is rebuilt from the persisted moves with the correct marks and turn
    And the client rejoins the SignalR group "game-{id}"
    And any running grace timer for this player is cancelled

  Scenario: Refresh while waiting for an opponent
    Given the player created a game and no opponent has joined
    When the page is reloaded
    Then the "Waiting for opponent..." state with the Cancel button is restored

  Scenario: Refresh after the game ended
    Given the game is Finished
    When the player reloads the page
    Then GET /api/v1/games/{id} returns the final board and Result
    And the result modal is shown again

  Scenario: Brief network drop
    When the connection drops and returns within the automatic retry schedule (0, 1, 3, 5 s)
    Then the client reconnects without user action
    And re-fetches the active game so no missed event can corrupt the board

  Scenario: No active game
    When the player reloads while having no Waiting or InProgress game
    Then they land on "/lobby"
```

---

## 3. Traceability Matrix

Story × functional requirement × milestone. "M2 → M4" means the API is delivered in M2 and the UI in M4; see the [roadmap](roadmap.md) for milestone contents.

| Story | Title | Priority | Milestone(s) | Functional requirements |
| --- | --- | --- | --- | --- |
| US-01 | Register an account | Must | M1 | FR-01 |
| US-02 | Log in and stay logged in | Must | M1 | FR-02, FR-03 |
| US-03 | Log out | Must | M1 | FR-02, FR-03 |
| US-04 | See who is online | Must | M3 → M4 | FR-03, FR-04 |
| US-05 | See the list of games | Must | M2 + M3 → M4 | FR-03, FR-10 |
| US-06 | Create a game | Must | M2 → M4 | FR-03, FR-05, FR-07 |
| US-07 | Join a game | Must | M2 → M4 | FR-03, FR-06, FR-07, FR-10 |
| US-08 | Make moves | Must | M2 → M5 | FR-08, FR-09, FR-12 |
| US-09 | See the result | Must | M2 → M5 | FR-09, FR-10, FR-12 |
| US-10 | Disconnect forfeit | Should | M3 (badge in M5) | FR-09, FR-11, FR-12 |
| US-11 | Return to the lobby | Must | M5 | FR-07, FR-10 |
| US-12 | Cancel my waiting game | Should | M2 → M4 | FR-05, FR-10 |
| US-13 | Reconnect to my active game | Should | M5 (hardened in M6) | FR-03, FR-11 |

**Coverage check** — every functional requirement is exercised by at least one story: FR-01 (US-01), FR-02 (US-02, US-03), FR-03 (all authenticated stories), FR-04 (US-04), FR-05 (US-06, US-12), FR-06 (US-07), FR-07 (US-06, US-07, US-11), FR-08 (US-08), FR-09 (US-08, US-09, US-10), FR-10 (US-05, US-07, US-09, US-11, US-12), FR-11 (US-10, US-13), FR-12 (US-08, US-09, US-10).

---

## Assumptions

- Scenario status codes, endpoints, events and payload shapes follow the REST and SignalR contracts of the [original technical documentation](archive/original-technical-documentation.md) (§§10–11), as refined in [api-spec](../02-backend/api-spec.md) and [realtime-signalr](../02-backend/realtime-signalr.md); exact error-code strings are finalized there, so scenarios name status codes and meanings rather than literal code enums.
- "In real time" / "instantly" means without a manual reload; the measurable budget for move delivery is the PRD NFR of 200 ms p95 round trip.
- Operational thresholds quoted in scenarios — lockout after 5 failed attempts for 5 minutes, 15-minute access-token and 7-day refresh-token lifetimes, hashed rotating refresh tokens with family revocation, multi-tab presence counting as one user — come from the technical documentation's Identity and presence design, which the PRD carries forward.
- Registration is public and email is not verified in MVP; confirm-password handling and empty-state copy are UX completions consistent with the [design system](../04-frontend/ux-ui-design-system.md).
- US-13 is functionally complete in M5; M6 re-verifies its edge cases (network drops, expired refresh mid-game) as part of hardening, per the [roadmap](roadmap.md).
