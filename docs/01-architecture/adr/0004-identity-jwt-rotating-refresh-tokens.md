# ADR-0004: ASP.NET Core Identity with JWT and rotating hashed refresh tokens

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Authentication, token design, session lifetime |

## Context

The product requires accounts (FR-01/02/03), "stay logged in" behavior (US-02), and a stateless API that scales to 500 concurrent games without server-side sessions. The browser SPA stores tokens client-side in the MVP, which raises the XSS risk on refresh tokens (PRD §9). SignalR WebSockets cannot always send the `Authorization` header, so the hub must accept the token another way.

## Decision

Use **ASP.NET Core Identity** for credential management plus **JWT Bearer access tokens (15 minutes)** and **rotating, hashed refresh tokens (7 days)**:

- `ApplicationUser : IdentityUser<Guid>`; password policy min 8 with upper/lower/digit; unique email; lockout after 5 failed attempts for 5 minutes; login by email or username.
- Access token: JWT with `sub`, `name`, `email`, `jti` claims, 15-minute lifetime, signed with a key from configuration (never committed).
- Refresh token: 256-bit random value; only its **hash** is stored (`refresh_tokens` table with `token_hash UNIQUE`, `expires_at_utc`, `revoked_at_utc`, `replaced_by_token_hash`, `created_by_ip`); **rotated on every use** — the old token is revoked and a new one issued; **reuse of a revoked token revokes the whole family** (theft signal).
- REST endpoints use the `Authorization: Bearer` header; the SignalR hub accepts the JWT via the `access_token` query string (WebSocket limitation) and is `[Authorize]`.
- On registration a `PlayerProfile` is created in the same transaction; logout revokes the presented refresh token.
- Access token lives in the SPA's memory; refresh token in `localStorage` for MVP, with a strict CSP and a planned move to an HttpOnly cookie (v1.1) per the PRD risk table.

Token lifecycle in one view:

| Token | Lifetime | Storage client-side | Storage server-side | On use |
| --- | --- | --- | --- | --- |
| Access (JWT) | 15 minutes | memory only | none (stateless validation) | verified by signature + expiry |
| Refresh | 7 days, rotated | `localStorage` (MVP) | **hash only**, in `refresh_tokens` | old revoked, new issued; reuse of revoked → family revoked |

## Consequences

**Positive**

- The API stays fully stateless — 15-minute tokens require no server lookup; scale-out needs no session store.
- Rotation + family revocation detects and contains refresh-token theft: a replayed old token kills the entire lineage.
- Hashed-at-rest refresh tokens limit blast radius of a DB leak; Identity provides PBKDF2 hashing, lockout, and normalization out of the box.
- Short access-token lifetime bounds the damage window of a stolen access token.

**Negative**

- More moving parts than opaque server sessions: issuance, rotation, family tracking, and a resync path when the SPA races two refreshes (client queues refreshes to avoid it).
- `localStorage` refresh tokens remain XSS-exposed in the MVP — accepted, documented risk with CSP mitigation and a v1.1 cookie plan.
- Token-in-query-string for WebSockets can leak via server logs — mitigated by explicitly excluding `access_token` from Serilog.
- 15-minute expiry forces silent-refresh logic and interceptor queueing in the client.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Server-side sessions (opaque cookie) | Contradicts the stateless-API NFR and complicates the future multi-instance step |
| Long-lived JWTs only (no refresh) | Either annoying re-login or long theft windows — violates US-02 or the security posture |
| Identity + JWT with non-rotating refresh tokens | Simple, but a leaked refresh token is valid for its whole 7-day life with no theft signal |
| Third-party auth (Auth0, Firebase) | Offloads the feature, but adds external dependency/cost for an internship project whose goal includes implementing auth properly |
| Social login / OIDC only | Explicitly a non-goal for MVP (PRD future scope) |

## References

- [PRD](../../00-product/PRD.md) (FR-01/02, US-02, §9 risks)
- [Auth, Identity & JWT](../../02-backend/auth-identity-jwt.md)
- [Security](../../05-quality/security.md)
- [ADR-0008 In-memory presence tracker](0008-in-memory-presence-tracker.md) — the other side of "who is connected"

## Assumptions

- Email is never verified in the MVP (PRD); lockout is the sole brute-force backstop alongside rate limiting.
- One account may hold multiple simultaneous connections (multiple tabs/devices) sharing one presence entry and one active game.
