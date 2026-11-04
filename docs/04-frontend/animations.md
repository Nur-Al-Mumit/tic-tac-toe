# Animations & Motion

*The motion specification for the Multiplayer Tic-Tac-Toe SPA: motion principles, the token layer, and every animation implemented as CSS keyframes in Tailwind CSS 4 CSS-first syntax, with `prefers-reduced-motion` fallbacks for each.*

Implements PRD [§5.4](../00-product/PRD.md). Tokens originate in [ux-ui-design-system.md](ux-ui-design-system.md) §2; Angular wiring notes in §5 of this file and [angular-architecture.md](angular-architecture.md).

---

## 1. Motion Design Principles

1. **Fast, purposeful, calm.** Durations live in the 150–500 ms range. Nothing loops aggressively except three intentional ambient loops (turn pulse, waiting dots, skeleton shimmer), all slow (≥ 1.2 s cycles) and low-opacity.
2. **Never block interaction.** Animations run on `opacity` and `transform` only (GPU-composited, no layout thrash); `pointer-events` never turn off for the sake of an animation; no animation gates input longer than its own duration.
3. **Feedback, not decoration.** Every animation answers a question: did it work (draw), whose turn is it (pulse), what went wrong (shake), what is still loading (shimmer/dots).
4. **Reduced-motion parity.** Every animation has a defined fallback (§4); nothing communicates meaning *only* through motion — each is paired with text, live-region announcements, or persistent state.
5. **One thing at a time.** Sequenced, not stacked: a move draws (300 ms) before the win line (500 ms) before the burst (900 ms) before the modal (200 ms).

---

## 2. Motion Tokens

Defined in the `@theme` block of `client/src/styles.css` ([design system §2](ux-ui-design-system.md)); durations/easings are plain CSS variables consumed by the `--animate-*` tokens below.

| Token | Value | Used by |
| --- | --- | --- |
| `--duration-fast` | 150 ms | Hover preview, button press, toast exit, list hover |
| `--duration-normal` | 200 ms | Page transitions, toasts, modal, list enter/leave |
| `--duration-slow` | 300 ms | Mark draw (X, O), invalid-move shake |
| `--duration-deliberate` | 500 ms | Winning-line draw |
| `--ease-out` | `cubic-bezier(0.16, 1, 0.3, 1)` | Entrances: page, list items, toasts, draws, win line |
| `--ease-in-out` | `cubic-bezier(0.4, 0, 0.2, 1)` | Loops and state moves: pulse, shimmer, dots, badge |
| `--ease-spring` | `cubic-bezier(0.34, 1.56, 0.64, 1)` | Confetti burst, result-modal pop (slight overshoot) |
| `--stagger-step` | 40 ms | Lobby list-item enter delay (per index) |
| `--stagger-cap` | 240 ms | Delay cap = 6 items (index ≥ 6 waits 240 ms) |

Tailwind 4 animation tokens — utilities like `animate-draw-x`, `animate-shake`, `animate-list-enter` are generated from these; `@keyframes` live **inside** `@theme` so Tailwind tree-shakes them with their tokens:

```css
/* client/src/styles.css — appended to the @theme block */
@theme {
  /* durations, easings and stagger vars: see ux-ui-design-system.md §2 */

  --animate-page-enter:    page-enter    var(--duration-normal) var(--ease-out) both;
  --animate-list-enter:    list-enter    var(--duration-normal) var(--ease-out) both;
  --animate-list-leave:    list-leave    var(--duration-normal) var(--ease-in-out) both;
  --animate-draw-x:        draw-stroke   var(--duration-slow)   var(--ease-out) both;
  --animate-draw-o:        draw-stroke   var(--duration-slow)   var(--ease-out) both;
  --animate-win-line:      draw-stroke   var(--duration-deliberate) var(--ease-out) both;
  --animate-win-glow:      win-glow      2s   var(--ease-in-out) infinite;
  --animate-burst:         burst         900ms var(--ease-out) both;
  --animate-shake:         shake         var(--duration-slow) var(--ease-in-out);
  --animate-pulse-ring:    pulse-ring    2s   var(--ease-in-out) infinite;
  --animate-skeleton:      shimmer       1.5s var(--ease-in-out) infinite;
  --animate-dots:          dot-bounce    1.2s var(--ease-in-out) infinite;
  --animate-toast-in:      toast-in      var(--duration-normal) var(--ease-out) both;
  --animate-toast-out:     toast-out     var(--duration-fast)  var(--ease-in-out) both;
  --animate-modal-in:      modal-in      var(--duration-normal) var(--ease-spring) both;
  --animate-badge-pulse:   badge-pulse   1.5s var(--ease-in-out) infinite;

  @keyframes page-enter  { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
  @keyframes list-enter  { from { opacity: 0; transform: translateY(12px) scale(0.98); } to { opacity: 1; transform: translateY(0) scale(1); } }
  @keyframes list-leave  { from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(-8px) scale(0.98); } }
  @keyframes draw-stroke { from { stroke-dashoffset: var(--dash, 200); } to { stroke-dashoffset: 0; } }
  @keyframes win-glow    { 0%, 100% { filter: drop-shadow(0 0 4px currentColor); opacity: 0.85; } 50% { filter: drop-shadow(0 0 14px currentColor); opacity: 1; } }
  @keyframes burst       { 0% { opacity: 1; transform: translate(0, 0) scale(1) rotate(0deg); } 100% { opacity: 0; transform: translate(var(--dx), var(--dy)) scale(0.3) rotate(var(--rot)); } }
  @keyframes shake       { 0%, 100% { transform: translateX(0); } 20% { transform: translateX(-6px); } 40% { transform: translateX(5px); } 60% { transform: translateX(-3px); } 80% { transform: translateX(2px); } }
  @keyframes pulse-ring  { 0% { transform: scale(1); opacity: 0.8; } 70% { transform: scale(1.18); opacity: 0; } 100% { transform: scale(1.18); opacity: 0; } }
  @keyframes shimmer     { 0% { background-position: 100% 0; } 100% { background-position: -100% 0; } }
  @keyframes dot-bounce  { 0%, 100% { opacity: 0.3; transform: translateY(0); } 50% { opacity: 1; transform: translateY(-4px); } }
  @keyframes toast-in    { from { opacity: 0; transform: translateY(16px); } to { opacity: 1; transform: translateY(0); } }
  @keyframes toast-out   { from { opacity: 1; } to { opacity: 0; } }
  @keyframes modal-in    { from { opacity: 0; transform: scale(0.95); } to { opacity: 1; transform: scale(1); } }
  @keyframes badge-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.35; } }
}
```

---

## 3. Animation Specs

Each spec: trigger → timing → implementation. All class-based, no animation libraries — CSS does the work; Angular only toggles classes (§5).

### 3.1 Page transition — fade + slide, 200 ms

- **Trigger:** route activation (`/login` → `/lobby`, lobby → game, any navigation).
- **Timing:** 200 ms, `--ease-out`; entering view only — the old view is removed instantly (swap speed beats cross-fade elegance; the entering slide already implies direction).
- **Implementation:** host element of each routed page gets `animate-page-enter` (`opacity: 0; translateY(8px)` → settled). Applied via the route template root; runs once per activation.

### 3.2 Lobby list enter / leave with stagger

- **Trigger:** initial lobby load (list rendered from query) and every SignalR `GameCreated` / `GameRemoved` / `PresenceChanged` list mutation.
- **Timing:** 200 ms, `--ease-out`; **stagger** `calc(min(var(--i), 6) * var(--stagger-step))` — 40 ms per item, capped at 240 ms (6 items). Leave: 200 ms fade + lift, then collapse handled by Angular (§5).
- **Implementation:** each row sets `style="--i: <index>"` and carries `animate-list-enter`; removals get `animate-list-leave` during the Angular leave transition.

```html
<!-- class + custom-prop recipe (structure only, no TS) -->
<li class="animate-list-enter" style="--i: 2"> ... game card ... </li>
```

```css
.animate-list-enter { animation-delay: calc(min(var(--i), 6) * var(--stagger-step)); }
```

### 3.3 X mark draw — 300 ms stroke-dash

- **Trigger:** `MoveMade` broadcast places an X (server-authoritative: never on local click alone).
- **Timing:** 300 ms `--ease-out` per stroke; second stroke delayed 150 ms; total perceived 450 ms — still calm, reads as handwriting.
- **Implementation:** SVG lines with `pathLength="100"` (normalizes dash math — no per-stroke length measuring), `stroke-dasharray: 100; stroke-dashoffset: 100` → animate to 0. Stroke color `--color-mark-x` (`#38bdf8`), glow via `drop-shadow`.

```html
<svg viewBox="0 0 100 100" class="animate-draw-x text-mark-x" aria-hidden="true">
  <line x1="22" y1="22" x2="78" y2="78" pathLength="100"
        stroke="currentColor" stroke-width="10" stroke-linecap="round"
        stroke-dasharray="100" stroke-dashoffset="100"
        style="--dash: 100; filter: drop-shadow(0 0 6px currentColor)" />
  <line x1="78" y1="22" x2="22" y2="78" pathLength="100"
        stroke="currentColor" stroke-width="10" stroke-linecap="round"
        stroke-dasharray="100" stroke-dashoffset="100"
        style="--dash: 100; animation-delay: 150ms; filter: drop-shadow(0 0 6px currentColor)" />
</svg>
```

```css
.animate-draw-x line { animation: draw-stroke var(--duration-slow) var(--ease-out) both; }
.animate-draw-x line:nth-child(2) { animation-delay: 150ms; }
```

### 3.4 O mark draw — 300 ms stroke-dash

Same technique, one `<circle>` (radius 30, circumference normalized with `pathLength="100"`), stroke `--color-mark-o` (`#fb7185`), `transform: rotate(-90deg)` so the stroke starts at 12 o'clock:

```html
<svg viewBox="0 0 100 100" class="animate-draw-o text-mark-o" aria-hidden="true">
  <circle cx="50" cy="50" r="30" pathLength="100"
          stroke="currentColor" stroke-width="10" fill="none" stroke-linecap="round"
          stroke-dasharray="100" stroke-dashoffset="100"
          style="--dash: 100; transform: rotate(-90deg); transform-origin: center;
                 filter: drop-shadow(0 0 6px currentColor)" />
</svg>
```

```css
.animate-draw-o circle { animation: draw-stroke var(--duration-slow) var(--ease-out) both; }
```

### 3.5 Winning line draw + glow pulse — 500 ms + 2 s loop

- **Trigger:** `GameFinished` with `winningLine: [a, b, c]` — the overlay line spans the first to the last winning cell through the middle cell's center.
- **Timing:** draw 500 ms `--ease-out`; then glow pulse loops (2 s, `--ease-in-out`) until the result modal opens; line persists (dimmed to 70 %) behind the modal.
- **Implementation:** one absolutely-positioned SVG over the grid, `viewBox="0 0 300 300"` (cell centers at 50/150/250), `pointer-events: none`. Line color = winner's mark color; `pathLength="100"` draw; glow via `animate-win-glow` with `currentColor`.

```html
<svg class="absolute inset-0 pointer-events-none" viewBox="0 0 300 300" aria-hidden="true">
  <line x1="50" y1="50" x2="250" y2="250" pathLength="100"
        class="animate-win-line win-line-glow" stroke="currentColor" stroke-width="8"
        stroke-linecap="round" stroke-dasharray="100" stroke-dashoffset="100" />
</svg>
```

```css
.win-line-glow { animation: draw-stroke var(--duration-deliberate) var(--ease-out) both,
                 win-glow 2s var(--ease-in-out) 500ms infinite; }
```

Winning cells additionally get `bg-{mark}/10` tint and their marks stay at full brightness (design system §7.11).

### 3.6 Victory burst (subtle CSS-only confetti) — 900 ms

- **Trigger:** result modal opens with outcome **Victory** only (defeat/draw never burst — calm principle; PRD offers "confetti or subtle burst", this spec picks the subtle burst).
- **Timing:** 900 ms `--ease-out`, per-particle delay 0–150 ms; plays once; removed after 1 s.
- **Implementation:** absolutely-positioned 24 `<span>` particles (6 px squares/circles mix) spawned at the board center, each with custom properties `--dx --dy --rot` set in the template (golden-angle spread, 90–190 px radius, deterministic pseudo-random from index — identical every victory, screenshot-testable). Colors rotate through `--color-mark-x`, `--color-mark-o`, `--color-primary`, `--color-status-playing`. Container is `pointer-events-none; overflow: hidden` on the board card.

```html
<div class="absolute inset-0 pointer-events-none" aria-hidden="true">
  <span class="burst-particle" style="--dx: 64px; --dy: -142px; --rot: 220deg;
        background: var(--color-mark-x); animation-delay: 40ms"></span>
  <!-- ... 23 more, spread radially ... -->
</div>
```

```css
.burst-particle {
  position: absolute; top: 50%; left: 50%;
  width: 6px; height: 6px; border-radius: 2px;
  animation: burst 900ms var(--ease-out) both;
}
```

### 3.7 Invalid move — soft shake, 300 ms

- **Trigger:** move rejected (server 409/422: not your turn, cell taken, wrong status — US-08), or click on a disabled cell.
- **Timing:** 300 ms, `--ease-in-out`, runs once, then resets (class removed on `animationend` so a repeat rejection shakes again).
- **Implementation:** `animate-shake` on the affected cell button; paired with a live-region message ("It's not your turn yet.") because shake alone is invisible to screen readers (motion is never the only signal — §1.4).

```css
.animate-shake { animation: shake var(--duration-slow) var(--ease-in-out); }
```

### 3.8 Turn indicator — pulsing ring, 2 s loop

- **Trigger:** active-player side of the game header (and the waiting host: the pulse plays on the host badge while `Waiting`).
- **Timing:** 2 s infinite, `--ease-in-out` — scale 1 → 1.18 + fade 0.8 → 0 on a ring pseudo-element; ambient, low-opacity.
- **Implementation:** `::after` ring in the player's mark color, `rounded-pill`, inset −3 px:

```css
.turn-active::after {
  content: ""; position: absolute; inset: -3px;
  border: 2px solid currentColor; border-radius: 9999px;
  animation: pulse-ring 2s var(--ease-in-out) infinite;
}
```

The inactive player's side drops to `opacity-60` at the same moment (200 ms color transition) so the switch is visible in two ways.

### 3.9 Button press — scale 0.97, 150 ms

- **Trigger:** `:active` on every Button variant (design system §7.1).
- **Timing:** 150 ms transform transition; springs back on release.

```css
.btn { transition: transform var(--duration-fast) var(--ease-out), ...; }
.btn:active { transform: scale(0.97); }
```

### 3.10 Skeleton shimmer — 1.5 s loop

- **Trigger:** first lobby/game fetch while skeletons show (design system §7.10).
- **Timing:** 1.5 s infinite `--ease-in-out`; sweep highlights travel left → right.
- **Implementation:** gradient background larger than the element, animated `background-position` (paint-safe on small blocks):

```css
.skeleton {
  background: linear-gradient(90deg,
    var(--color-surface-raised) 25%,
    var(--color-border-strong) 37%,
    var(--color-surface-raised) 63%);
  background-size: 300% 100%;
  animation: shimmer 1.5s var(--ease-in-out) infinite;
}
```

### 3.11 Waiting-for-opponent — animated dots

- **Trigger:** game status `Waiting`, in the status line "Waiting for opponent...".
- **Timing:** 1.2 s infinite per dot, delays 0 / 150 / 300 ms (staggered bounce + opacity).
- **Implementation:** three 6 px dots after the text, `aria-hidden` (the live region already carries the meaning):

```html
<span class="inline-flex gap-1 ml-1" aria-hidden="true">
  <i class="waiting-dot"></i><i class="waiting-dot" style="animation-delay: 150ms"></i>
  <i class="waiting-dot" style="animation-delay: 300ms"></i>
</span>
```

```css
.waiting-dot {
  width: 6px; height: 6px; border-radius: 9999px;
  background: var(--color-text-muted);
  animation: dot-bounce 1.2s var(--ease-in-out) infinite;
}
```

### 3.12 Hover preview fade-in — 150 ms

- **Trigger:** hover or keyboard focus on an empty cell **while it is the local player's turn** (design system §7.11).
- **Timing:** 150 ms `--ease-out`; ghost mark fades to 35 % opacity and scales 0.8 → 1.
- **Implementation:** ghost SVG (same geometry as §3.3/§3.4) inside the cell, `opacity-0` until the cell is hovered/focused; pure CSS state, no JS:

```css
.cell .ghost-mark {
  opacity: 0; transform: scale(0.8);
  transition: opacity var(--duration-fast) var(--ease-out),
              transform var(--duration-fast) var(--ease-out);
}
.cell:hover:not(:disabled) .ghost-mark,
.cell:focus-visible:not(:disabled) .ghost-mark { opacity: 0.35; transform: scale(1); }
```

### 3.13 Toast slide-in / fade-out — 200 ms / 150 ms

- **Trigger:** toast pushed (enter) and auto-dismiss/close (exit).
- **Timing:** enter 200 ms `--ease-out` slide-up + fade; exit 150 ms `--ease-in-out` fade; stack reflow animates via 200 ms `transform` transitions on the container.

```css
.toast { animation: toast-in var(--duration-normal) var(--ease-out) both; }
.toast.leaving { animation: toast-out var(--duration-fast) var(--ease-in-out) both; }
```

### 3.14 Result modal — scale + fade entrance, 200 ms spring

- **Trigger:** result modal opens (after win-line + burst sequence; draw/defeat open immediately after the final mark draw).
- **Timing:** 200 ms `--ease-spring` (slight 1.56 overshoot reads as "settled pop"); backdrop fades in parallel 200 ms.

```css
.modal-panel { animation: modal-in var(--duration-normal) var(--ease-spring) both; }
.modal-backdrop { animation: toast-in var(--duration-normal) var(--ease-out) both; }
```

The modal never animates on close — it is the end of the journey; the 200 ms page transition into the lobby provides the continuity.

### 3.15 ConnectionBadge — soft opacity pulse (Reconnecting)

- **Trigger:** badge in "Reconnecting..." state only; "Connected" is static.
- **Timing:** 1.5 s infinite `--ease-in-out` opacity 1 ↔ 0.35 on the amber dot (`animate-badge-pulse`) — the slowest, calmest loop in the system (anxiety-inducing if fast).

---

## 4. `prefers-reduced-motion`

Global kill-switch first, then per-animation parity. No fallback is a dead end: every animation below degrades to an equivalent **state**, not just an absence.

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
  .turn-active::after { animation: none; opacity: 0.9; }  /* static ring */
  .skeleton { animation: none; background: var(--color-surface-raised); }
  .waiting-dot { animation: none; opacity: 0.6; }
  .burst-particle, .burst-container { display: none; }     /* confetti off */
}
```

| # | Animation | Reduced-motion fallback |
| --- | --- | --- |
| 3.1 | Page transition | Instant swap (opacity/transform neutralized by the 0.01 ms rule) |
| 3.2 | List enter/leave + stagger | Items appear instantly; no stagger delay; leave is instant removal |
| 3.3 | X draw | X appears instantly at full stroke (`stroke-dashoffset: 0`) |
| 3.4 | O draw | O appears instantly |
| 3.5 | Win line draw + glow | Line appears instantly, full opacity, **static** glow (`filter: drop-shadow(0 0 8px currentColor)`, no loop) |
| 3.6 | Victory burst | **Off** — no particles; victory is carried by the modal text and live region |
| 3.7 | Shake | No shake; the cell border flashes `--color-danger` twice (200 ms color steps) + live-region message — still double-coded, motion-free |
| 3.8 | Turn pulse ring | Static ring: solid 2 px `currentColor` ring at 0.9 opacity (active side still dimmed vs `opacity-60` inactive) |
| 3.9 | Button press | No scale; hover/press conveyed by color change already present |
| 3.10 | Skeleton shimmer | Static `--color-surface-raised` blocks (no sweep) |
| 3.11 | Waiting dots | Static three dots at 0.6 opacity (ellipsis still present) |
| 3.12 | Hover preview | Preview appears instantly at 35 % opacity (still communicates "this cell is playable") |
| 3.13 | Toast in/out | Instant appear; dismiss removes instantly; durations still apply (4 s/6 s) |
| 3.14 | Modal entrance | Instant open, full opacity — focus management (design system §7.7) does the announcing |
| 3.15 | Badge pulse | Static amber dot + "Reconnecting..." label (the label was always the primary signal) |

---

## 5. Angular Integration (zoneless-safe)

- **Pure CSS first.** Every animation above is a class + custom properties; Angular templates bind classes from **signal** state (`isMyTurn()`, `gameStatus()`, `lastResult()`, `pendingCell()` …) — no `@Component` animation code for the loop/one-shot effects.
- **Angular animations only where CSS cannot go:** *leave* transitions (removed DOM cannot animate itself) — lobby list rows and toasts use an Angular `animate(200ms)` `:leave` transition mirroring `list-leave`/`toast-out`; the CSS keyframes above remain the single source of truth for the visuals, and the enter/leave triggers read from signals.
- **Zoneless-safe.** All triggers are signal reads (`computed`/`effect`) or `:enter`/`:leave` hooks — nothing relies on Zone.js ticks; animation classes flip exactly when the SignalR-driven store updates a signal, so timing stays correct without change-detection sweeps (see [angular-architecture.md](angular-architecture.md)).
- **One-shot hygiene.** Single-run animations (`shake`, `burst`, draws) reset via `animationend` (shake class removal) or by keying the element to the move index / result so re-renders replay cleanly; `both` fill-mode guarantees the settled state persists.
- **Testing hooks.** Deterministic delays (stagger from `--i`, burst spread from particle index) make vitest (+ jsdom, computed styles) and Playwright screenshot assertions stable; Playwright runs one reduced-motion spec emulating `prefers-reduced-motion: reduce` to verify fallbacks.

---

## Assumptions

- "Confetti or subtle burst" (PRD §5.3) is resolved as a **subtle 24-particle CSS-only burst on victory only** — matching the calm principle; a full-screen confetti celebration stays out of MVP scope.
- Draw sequencing (X's second stroke at +150 ms) means the X draw is perceived as 450 ms total while each stroke animates for the PRD-mandated 300 ms — read as one gesture, not two animations.
- Particle spread values are deterministic (derived from particle index, not runtime randomness) for reproducible tests and screenshots.
- Loop periods (pulse 2 s, shimmer 1.5 s, dots 1.2 s, badge 1.5 s) are designer-tuned defaults pending motion-sensitive user testing.
- The leave animations for list rows and toasts use Angular's animation triggers because CSS cannot animate elements out of the DOM; everything else is class-driven CSS.
