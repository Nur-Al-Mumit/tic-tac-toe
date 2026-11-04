# ADR-0007: Angular 22 with signals, zoneless change detection, and Tailwind CSS 4

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Frontend framework mode, change detection, styling approach |

## Context

The SPA has four pages and one hot path: the game board, where every `MoveMade` broadcast must re-render marks, turn indicator and (eventually) the winning-line animation within the 200 ms round-trip budget. The original document pinned Angular 17+ with a `tailwind.config.js`; the 2026-11-04 modernization (ADR-0011) targets Angular 22, where zoneless change detection is stable and Tailwind 4 has replaced the JS config file with CSS-first configuration.

## Decision

Build the client with **Angular 22** using **standalone components, signals for state, zoneless change detection**, and **Tailwind CSS 4 in CSS-first mode**:

- **Standalone components** everywhere; no NgModules. Features (`auth`, `lobby`, `game`) lazy-load via `app.routes.ts`; `core/` holds services/interceptors/guards, `shared/` holds UI components.
- **Signals for state**: `lobby.store` and `game.store` expose `signal()`/`computed()` state; components bind to signals; RxJS is confined to HTTP and the SignalR event streams, which convert to signals at the store boundary.
- **Zoneless**: `provideZonelessChangeDetection()` in `app.config.ts` — no Zone.js; re-renders are driven by signal writes (and `markForCheck` on the few async edges). This mandates signal-correct code: all mutable shared state is signals, no zone-dependent assumptions in third-party code.
- **Tailwind CSS 4, CSS-first**: design tokens (slate/indigo palette, sky-400 X, rose-400 O, Inter/JetBrains Mono fonts, keyframes for pulse/draw/shake) are declared in the root stylesheet with `@theme { … }`; **no `tailwind.config.js`** exists. Component styles use utilities plus `@apply` sparingly; animation keyframes are CSS `@keyframes` referenced from `@theme` animation tokens.
- `@microsoft/signalr` client with `withAutomaticReconnect([0, 1000, 3000, 5000])` and post-reconnect resync via `GET /api/v1/games/active`.

Design tokens declared once in CSS (illustrative shape; full token table in the [design system](../../04-frontend/ux-ui-design-system.md)):

```css
@theme {
  --color-primary: var(--color-indigo-500);   /* composes the default palette */
  --color-mark-x:  var(--color-sky-400);
  --color-mark-o:  var(--color-rose-400);
  --font-sans: "Inter", ui-sans-serif, system-ui, sans-serif;
  --font-mono: "JetBrains Mono", ui-monospace, monospace;
}
```

## Consequences

**Positive**

- Zoneless + signals removes change-detection sweeping: a `MoveMade` broadcast updates exactly the components reading affected signals — ideal for the board's 10 dynamic elements, and removes Zone.js patching overhead from the bundle and runtime.
- Signals give a single mental model for state; `computed()` derives turn labels, board arrays and result overlays without subscriptions.
- Tailwind 4 `@theme` keeps tokens in CSS where they are consumed (dark-first palette, glow shadows, reduced-motion variants), drops the config file and its JS build step, and yields smaller generated CSS.
- Standalone + lazy routes keep the initial bundle lean for the login-first audience.

**Negative**

- Zoneless demands discipline: a missed signal (mutating a plain object the template reads) renders nothing — caught by store-level unit tests in vitest.
- Ecosystem libraries that rely on Zone.js default change detection need explicit `markForCheck`/signal interop; vetted during dependency selection.
- Fewer Stack Overflow answers for the newest Angular/Tailwind majors; official docs are the primary source.
- Tailwind 4 renames some utilities/behaviors vs 3.x — the team learns the CSS-first conventions once, up front.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Angular with Zone.js (classic) | Wasteful CD sweeps on every async tick (SignalR events, timers, RxJS); zoneless is stable in 22 and the state surface is small enough to migrate cleanly |
| RxJS-only state (BehaviorSubject + async pipe) | Works but spreads subscription management across components; signals centralize derivation and play to the zoneless runtime |
| React / Svelte / Vue rewrite | The PRD fixes Angular; team skill alignment and SignalR client integration are already established |
| Tailwind 3 with `tailwind.config.js` | Superseded by v4's CSS-first model the stack modernization adopts; keeping 3.x means a config file and utility differences the docs would then have to carry |
| CSS modules / SCSS-only styling | Loses utility velocity and the token system the design (glow effects, keyframes, dark palette) leans on |

## References

- [PRD](../../00-product/PRD.md) (§6 Experience, §7 stack table)
- [Angular architecture](../../04-frontend/angular-architecture.md) · [UX/UI design system](../../04-frontend/ux-ui-design-system.md) · [Animations](../../04-frontend/animations.md)
- [ADR-0011 Latest stable stack](0011-latest-stable-stack.md)
- [ADR-0005 REST moves / SignalR broadcasts](0005-rest-moves-signalr-broadcasts.md) — client reconnect/resync counterpart

## Assumptions

- Angular 22's zoneless mode is used from day one rather than migrated later; no `provideZoneChangeDetection` fallback is planned.
- Tailwind 4's `@theme` covers all tokens (colors, fonts, shadows, keyframe animations); `tailwind.config.js` is deliberately absent from the repository.
