# UX/UI Design System

*The single source of truth for the visual language of the Multiplayer Tic-Tac-Toe SPA: design tokens (Tailwind CSS 4, CSS-first), color, typography, spacing, and the full component inventory with state-by-state specs.*

Conforms to the experience decisions in the [PRD §5–§6](../00-product/PRD.md). Page layouts, wireframes and flows: [pages-and-flows.md](pages-and-flows.md). Motion specs and keyframes: [animations.md](animations.md).

---

## 1. Design Principles

1. **Clean, modern, calm.** Chess.com-inspired professionalism, not arcade noise. One accent color (indigo) carries interactivity; the two mark colors (sky/rose) are reserved for game semantics.
2. **Dark-first.** Slate-950 stage, slate-900 cards, subtle radial ambience. A light theme is a post-MVP token swap, never a per-component concern.
3. **Generous space, rounded cards, soft shadows.** Content breathes: 24 px card padding, 24 px panel gaps, `rounded-2xl` cards, shadows that model depth, not drama.
4. **Feedback for every action.** Every click answers within 150 ms — hover, press, spinner, toast, shake, or animation. Silence is a bug.
5. **Shape before color.** X and O are strokes, not just hues; status always pairs a dot/shape with a text label. Color is never the only indicator (WCAG 1.4.1).
6. **Motion with restraint.** 150–500 ms, purposeful, never blocking input. Full policy: [animations.md](animations.md).

---

## 2. Design Tokens — Tailwind CSS 4 (CSS-first)

There is **no `tailwind.config.js`**. All tokens live in `client/src/styles.css` inside a single `@theme` block; Tailwind 4 generates utilities from them (`bg-surface`, `text-mark-x`, `rounded-card`, `shadow-card`, …). Durations/easings are consumed by the animation tokens defined in [animations.md](animations.md).

```css
/* client/src/styles.css */
@import "tailwindcss";

@theme {
  /* ---------- Surfaces & text (slate scale) ---------- */
  --color-bg:            #020617; /* slate-950 — app background */
  --color-surface:       #0f172a; /* slate-900 — cards, panels, board card */
  --color-surface-raised:#1e293b; /* slate-800 — hover fills, inputs, chips */
  --color-border-subtle: #1e293b; /* slate-800 — card & divider borders */
  --color-border-strong: #334155; /* slate-700 — input rest borders */
  --color-text-primary:  #f1f5f9; /* slate-100 — headings, body emphasis */
  --color-text-secondary:#cbd5e1; /* slate-300 — body copy, labels */
  --color-text-muted:    #94a3b8; /* slate-400 — meta, hints, timestamps */
  --color-text-disabled: #64748b; /* slate-500 — disabled labels only */

  /* ---------- Brand & semantics ---------- */
  --color-primary:       #6366f1; /* indigo-500 — primary actions, links, active tab */
  --color-primary-hover: #818cf8; /* indigo-400 — primary hover, focus ring */
  --color-mark-x:        #38bdf8; /* sky-400    — X mark, Player X identity */
  --color-mark-o:        #fb7185; /* rose-400   — O mark, Player O identity */
  --color-success:       #10b981; /* emerald-500 — success toast, Available, Connected */
  --color-danger:        #ef4444; /* red-500     — errors, invalid move flash */
  --color-danger-text:   #f87171; /* red-400     — error text (AA on dark surfaces) */
  --color-status-playing:#fbbf24; /* amber-400   — "In game", "Reconnecting..." */

  /* ---------- Typography ---------- */
  --font-sans: "Inter", ui-sans-serif, system-ui, -apple-system, sans-serif;
  --font-mono: "JetBrains Mono", ui-monospace, "SFMono-Regular", Menlo, monospace;

  /* ---------- Radius ---------- */
  --radius-card:    1rem;    /* cards, panels, board card (rounded-2xl) */
  --radius-control: 0.75rem; /* buttons, inputs, cells, toasts (rounded-xl) */
  --radius-pill:    9999px;  /* badges, count pills, connection badge */

  /* ---------- Shadows & glow ---------- */
  --shadow-card:    0 1px 2px 0 rgb(2 6 23 / 0.50), 0 8px 24px -8px rgb(2 6 23 / 0.60);
  --shadow-overlay: 0 12px 40px -8px rgb(2 6 23 / 0.80);
  --shadow-glow-primary: 0 0 20px rgb(99 102 241 / 0.40);
  --shadow-glow-x:       0 0 16px rgb(56 189 248 / 0.35);
  --shadow-glow-o:       0 0 16px rgb(251 113 133 / 0.35);

  /* ---------- Motion (specs: animations.md) ---------- */
  --duration-fast:       150ms;  /* hover, press, previews */
  --duration-normal:     200ms;  /* page, toast, modal transitions */
  --duration-slow:       300ms;  /* mark draw, shake */
  --duration-deliberate: 500ms;  /* winning line */
  --ease-out:   cubic-bezier(0.16, 1, 0.3, 1);  /* entrances */
  --ease-in-out:cubic-bezier(0.4, 0, 0.2, 1);   /* loops, moves */
  --ease-spring:cubic-bezier(0.34, 1.56, 0.64, 1); /* pops */
}

/* App backdrop: slate-950 with a subtle indigo radial ambience (PRD §6) */
body {
  background-color: var(--color-bg);
  background-image: radial-gradient(ellipse 80% 50% at 50% -10%, rgb(99 102 241 / 0.08), transparent);
  background-attachment: fixed;
  color: var(--color-text-secondary);
  font-family: var(--font-sans);
  -webkit-font-smoothing: antialiased;
}
```

Fonts are self-hosted via `@fontsource-variable/inter` and `@fontsource-variable/jetbrains-mono` (weights 400–700, `font-display: swap`) so first paint never flashes a system serif.

---

## 3. Color System — Usage & Contrast

All ratios computed against `--color-bg` (#020617) unless noted; WCAG 2.1 AA requires 4.5:1 (normal text), 3:1 (large text ≥ 24 px / 18.66 px bold, and UI component boundaries), 7:1 (AAA normal text).

| Token | Value | Contrast vs bg | Usage | Contrast note (WCAG AA) |
| --- | --- | --- | --- | --- |
| `--color-bg` | #020617 | — | App background | Base for all ratios below |
| `--color-surface` | #0f172a | 1.1:1 | Cards, panels, board card, input fills | Surface differentiation relies on border + shadow (decorative, exempt); text on surface: primary 16.3:1, secondary 12.0:1, muted 7.0:1 — all pass |
| `--color-surface-raised` | #1e293b | 1.4:1 | Hover fills, chips, secondary buttons, toast | slate-100 text on it: 13.4:1 — pass |
| `--color-border-subtle` | #1e293b | 1.4:1 | Card/divider borders | Decorative structure; component identity comes from labels + focus ring, per WCAG 1.4.11 exception |
| `--color-border-strong` | #334155 | 2.0:1 | Input rest borders | Paired with always-visible labels; focus state provides the 3:1 boundary (indigo-400: 6.8:1) |
| `--color-text-primary` | #f1f5f9 | 18.4:1 | Headings, emphasized text | AAA |
| `--color-text-secondary` | #cbd5e1 | 13.6:1 | Body copy, form labels, tab labels | AAA |
| `--color-text-muted` | #94a3b8 | 7.9:1 | Hints, timestamps, placeholders, meta | AAA; **the** muted-text color — slate-500 (4.2:1) is forbidden for readable text |
| `--color-text-disabled` | #64748b | 4.2:1 | Disabled control labels only | Disabled elements are WCAG-exempt; below AA on purpose to read "inert" |
| `--color-primary` | #6366f1 | 4.5:1 | Primary buttons (fill), active tab underline, links, focus accents | As text on bg: passes AA (borderline). As a fill, its label is **slate-950** (4.5:1) — see Button spec |
| `--color-primary-hover` | #818cf8 | 6.8:1 | Primary hover fill, focus rings, links on hover | Passes AA text; the focus-ring color (3:1 UI requirement: pass) |
| `--color-mark-x` | #38bdf8 | 9.4:1 | X strokes, X-player name/label, X win glow | AAA as text; never used for actions |
| `--color-mark-o` | #fb7185 | 7.5:1 | O strokes, O-player name/label, O win glow | AAA as text |
| `--color-success` | #10b981 | 8.0:1 | Success toast icon/border, Available dot, Connected dot | Dots always paired with text label (1.4.1) |
| `--color-danger` | #ef4444 | 5.4:1 | Error borders/icons, invalid-move flash, danger border | AA; not used for body text |
| `--color-danger-text` | #f87171 | 7.3:1 | Error messages, danger button label | AAA |
| `--color-status-playing` | #fbbf24 | 12.1:1 | "In game" dot, "Reconnecting..." dot | AAA; distinct hue from both marks and primary |
| Avatar tint text (300-level: sky-300, rose-300, indigo-300, …) | e.g. #7dd3fc | ≥ 9:1 | Avatar initials on `tint/15` fills | AAA on every surface used |

Rules:

- Never place `--color-primary` text on `--color-surface-raised` or colored fills; use it only on `--color-bg`/`--color-surface` or as a fill itself.
- White (`#ffffff`) is reserved for modal headings; body text uses the slate scale for calmer contrast.
- Marks (`--color-mark-x/o`) are game semantics only — never buttons, links, or generic accents.

---

## 4. Typography

Inter everywhere; JetBrains Mono only where characters align or count: numbers, timers/countdowns, game IDs, move coordinates, count chips, timestamps.

| Token | Element | Size / Line-height | Weight | Tracking | Font | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `text-display` | Login/Register brand headline | 30 / 36 px | 700 | −0.02em | Inter | "Tic-Tac-Toe" on brand panel |
| `text-h1` | Page title, result modal title | 24 / 32 px | 700 | −0.01em | Inter | "Welcome back", "Victory!" |
| `text-h2` | Panel titles (Games, Online Players) | 18 / 24 px | 600 | 0 | Inter | |
| `text-h3` | Card titles, modal body emphasis | 16 / 24 px | 600 | 0 | Inter | Game card host name |
| `text-body-lg` | Lead sentences, empty-state copy | 16 / 24 px | 400 | 0 | Inter | |
| `text-body` | Default body, form labels | 14 / 20 px | 400 (labels 500) | 0 | Inter | |
| `text-meta` | Player status, timestamps, hints | 13 / 18 px | 400 | 0 | Inter | Muted color |
| `text-caption` | Badge labels, overlines, count chips | 12 / 16 px | 500 | +0.02em, uppercase | Inter | |
| `text-button` | Button labels | 14 / 20 px | 600 | 0 | Inter | |
| `text-mono-md` | Game ID, board aria coordinator, timers | 14 / 20 px | 500 | 0 | JetBrains Mono | `font-variant-numeric: tabular-nums` |
| `text-mono-sm` | Timestamps in cards, move count | 13 / 18 px | 400 | 0 | JetBrains Mono | `tabular-nums` |
| `text-mono-hero` | Not Found "404" | 56 / 64 px | 600 | −0.02em | JetBrains Mono | indigo-400 |

Typographic rules: sentence case everywhere except uppercase `text-caption` labels; no italics; line length ≤ 66 ch; numeric UI (online count, timers) always `tabular-nums` so digits don't jitter during live updates.

---

## 5. Spacing & Layout Scale

4 px base grid (Tailwind default scale — used as-is, no custom spacing tokens).

| Step | Value | Primary use |
| --- | --- | --- |
| 1 | 4 px | Icon ↔ label, badge internals |
| 2 | 8 px | Related controls, board cell gap, form help spacing |
| 3 | 12 px | Card list gaps, input internal height padding |
| 4 | 16 px | Form field stack, mobile page padding, card list rows |
| 5 | 20 px | Card title ↔ content |
| 6 | 24 px | Card padding, panel gap, section spacing |
| 8 | 32 px | Desktop page padding, panel title ↔ list |
| 10 | 40 px | Between major page regions (mobile) |
| 12 | 48 px | Board card ↔ page edge breathing room |

Layout scaffolding:

- Page container: `max-w-7xl` (1280 px) centered, `px-4` (mobile) → `px-8` (≥ 768 px).
- Lobby grid: single column `< 1024 px`; `grid-cols-[1fr_340px] gap-6` at ≥ 1024 px.
- Card padding: `p-4` at < 480 px, `p-6` above.
- All interactive targets ≥ 44 × 44 px (WCAG 2.5.5 AAA target — buttons h-11, cells ≥ 88 px).
- Fixed heights: navbar 64 px; buttons h-8/h-10/h-12 (sm/md/lg); inputs h-11.

---

## 6. Elevation & Radius

| Token | Value | Applied to |
| --- | --- | --- |
| `--shadow-card` | soft two-layer slate shadow | Cards, panels, board card, toast |
| `--shadow-overlay` | deep shadow | Modal panel |
| `--shadow-glow-primary` | indigo halo | Active Create Game button hover |
| `--shadow-glow-x` | sky halo | X marks, X winning line |
| `--shadow-glow-o` | rose halo | O marks, O winning line |
| `--radius-card` (1rem) | — | Cards, panels, modal, board card, board cells |
| `--radius-control` (0.75rem) | — | Buttons, inputs, icon buttons |
| `--radius-pill` (9999px) | — | Badges, count pills, connection badge, avatar |

Glow is the dark-theme equivalent of elevation for game semantics: marks glow softly at rest; the winning line glows brighter (see [animations.md](animations.md)).

---

## 7. Component Inventory

Each spec lists variants, states, and the Tailwind recipe using the tokens above. Components are Angular standalone components (structure in [angular-architecture.md](angular-architecture.md)); only visual recipes are specified here.

### 7.1 Button

Sizes: `sm` h-8 px-3; `md` h-10 px-4 (default); `lg` h-12 px-6 (primary CTAs: Sign in, Create Game, Back to Lobby). Shared: `inline-flex items-center justify-center gap-2 rounded-control font-semibold text-button select-none transition-all duration-fast ease-out`, `active:scale-[0.97]`, focus ring per §9. Loading state replaces label with a 16 px Spinner and sets `aria-busy="true"`; width is preserved.

| Variant / State | Recipe (on top of shared classes) | Notes |
| --- | --- | --- |
| Primary — default | `bg-primary text-bg` | slate-950 label on indigo-500 = 4.5:1 AA |
| Primary — hover | `hover:bg-primary-hover` + `hover:shadow-glow-primary` | 6.8:1 |
| Primary — active | `active:scale-[0.97] active:bg-primary-hover` | Press feedback |
| Primary — focus | `focus-visible:` ring (§9) | Ring indigo-200 on indigo fill |
| Primary — disabled | `disabled:bg-surface-raised disabled:text-text-disabled disabled:shadow-none disabled:cursor-not-allowed disabled:scale-100` | WCAG-exempt styling |
| Primary — loading | Spinner + `disabled` + `aria-busy` | e.g. "Sign in…", Join while joining |
| Secondary — default | `bg-surface-raised text-text-primary border border-border-subtle` | 13.4:1 label |
| Secondary — hover/active | `hover:bg-border-strong hover:border-border-strong` / active scale | |
| Secondary — focus/disabled | ring (§9) / `disabled:opacity-50 disabled:cursor-not-allowed` | e.g. "Login" link-button on Register |
| Ghost — default | `bg-transparent text-text-secondary` | e.g. "Cancel", navbar menu items |
| Ghost — hover | `hover:bg-surface-raised/60 hover:text-text-primary` | |
| Ghost — focus/disabled | ring (§9) / `disabled:text-text-disabled disabled:cursor-not-allowed` | |
| Danger — default | `bg-danger/10 border border-danger/40 text-danger-text` | Tinted style keeps 7.3:1 label |
| Danger — hover | `hover:bg-danger/20 hover:text-red-300` | |
| Danger — active/focus/disabled | press scale / ring uses red-400 / `disabled:opacity-50` | Used for Cancel game, Logout |

### 7.2 Input

Anatomy: label (always visible, `text-body font-medium text-text-secondary`, `for`/`id` linked) → control → hint (13 px muted) or error (13 px `--color-danger-text` + 16 px alert-circle icon), wired via `aria-describedby`. Control: `h-11 w-full rounded-control bg-bg/60 border px-3 text-body text-text-primary placeholder:text-text-muted transition-colors duration-fast`.

| State | Border / extras | Notes |
| --- | --- | --- |
| Default | `border-border-strong` | On card surfaces reads as a carved-in field |
| Hover | `hover:border-text-muted` | |
| Focus | `focus:border-primary focus:ring-2 focus:ring-primary-hover/40 outline-none` | Ring provides 3:1 boundary (6.8:1) |
| Error | `border-danger` + error icon + message; input `aria-invalid="true"` | Message specific: "Username must be 3–20 characters (letters, digits, underscore)" |
| Disabled | `opacity-50 cursor-not-allowed` | Not used in MVP forms |
| With leading icon | left `pl-10`, icon 20 px muted (e.g. user, mail on Register) | `aria-hidden` |
| Password show/hide | trailing 36 px ghost icon button (lucide `eye` / `eye-off`), `aria-label="Show password"` / `"Hide password"`, `aria-pressed` toggles; input keeps focus | Never a separate checkbox |

Validation UX: validate on blur, re-validate on input once touched; errors clear the moment input becomes valid. Register's live password-strength meter sits under the password field: 12 px track (`rounded-pill bg-surface-raised`), fill width 0/33/66/100 %, colors danger → amber-400 → success, label "Weak / Fair / Strong" (13 px, color never the only indicator — the word changes too).

### 7.3 Card

`rounded-card border border-border-subtle bg-surface shadow-card` + `p-4 sm:p-6`. Variants: **panel** (with `text-h2` header row and optional trailing action); **game card** and **form card** are compositions of Card + content (see [pages-and-flows.md](pages-and-flows.md)). No hover state on static cards; interactive cards (game cards) add `transition-shadow duration-fast hover:border-border-strong hover:shadow-overlay` — the Join button remains the explicit affordance.

### 7.4 Avatar

Circular (`rounded-pill`), initials = first two characters of the username, uppercased, `font-semibold`, `text-meta`. Color derived deterministically from the username: `hash = sum(username.charCodeAt(i)) % 8` → palette index (same username always gets the same color, across sessions and clients).

| Size | Dimensions | Use |
| --- | --- | --- |
| xs | 24 px / 11 px text | Online Players rows |
| sm | 32 px / 13 px text | Game cards, board player badges |
| md | 40 px / 14 px text | Navbar menu, result modal |
| lg | 56 px / 18 px text | Result modal (own avatar) |

Palette (fill at 15 % opacity, initials at the 300-level text color): `indigo-400/15 + indigo-300`, `sky-400/15 + sky-300`, `teal-400/15 + teal-300`, `emerald-400/15 + emerald-300`, `amber-400/15 + amber-300`, `rose-400/15 + rose-300`, `fuchsia-400/15 + fuchsia-300`, `violet-400/15 + violet-300`. All pairs ≥ 9:1 on `--color-surface`. Avatars are decorative: `aria-hidden="true"` with the username as adjacent real text.

### 7.5 Badge & StatusDot

`Badge`: `inline-flex items-center gap-1.5 rounded-pill border px-2.5 py-0.5 text-caption`.

| Badge | Recipe | Meaning |
| --- | --- | --- |
| Waiting (game card) | `bg-primary/10 border-primary/30 text-primary-hover` | Game joinable |
| Full (game card) | `bg-surface-raised border-border-strong text-text-secondary` | Replaces Join button when taken |
| In Progress (game card) | `bg-status-playing/10 border-status-playing/30 text-status-playing` | Spectator-list info |
| Online count pill (navbar) | `bg-surface-raised border-border-subtle text-text-secondary`, 6 px emerald dot + mono count | "12 online" |

`StatusDot` (Online Players rows): 8 px circle + 13 px label — **Available**: `bg-success` + "Available"; **In game**: `bg-status-playing` + "In game". Text always accompanies the dot (1.4.1).

### 7.6 Toast

| Aspect | Spec |
| --- | --- |
| Position | Desktop: fixed `bottom-4 right-4`; mobile (< 768 px): `bottom-4 inset-x-4` |
| Anatomy | Icon (20 px) + title (14 px semibold) + message (13 px muted) + close X (ghost icon button, 28 px, `aria-label="Dismiss notification"`) |
| Variants | Success: emerald icon + `border-success/40`; Error: `border-danger/40`, red-400 icon, `role="alert"`; Info: `border-primary/40`, indigo-400 icon. Body: `bg-surface border rounded-control shadow-card` |
| Duration | Success/info 4000 ms; error 6000 ms; pauses on hover or focus-within |
| Stacking | Column, newest on bottom, max 3 — oldest is dismissed immediately |
| Live region | Success/info `role="status"` (polite); error `role="alert"` (assertive) |
| Motion | Slide-in 200 ms, fade-out 150 ms (see [animations.md](animations.md)) |

Copy examples: success "Account created — welcome aboard!"; error "Invalid credentials. Check your username and password."; error "Game is already full." (join race, US-07); info "Reconnected. You're back in the game."

### 7.7 Modal

Overlay: `fixed inset-0 z-50 bg-bg/70 backdrop-blur-sm`, fade 200 ms. Panel: `mx-auto w-[min(24rem,calc(100vw-2rem))] rounded-card border border-border-subtle bg-surface p-6 shadow-overlay text-center`, entrance scale 0.95→1 + fade 200 ms spring. Used by the result modal and (only if a second confirm is ever needed) destructive confirmations — Cancel game is direct, no modal.

Accessibility contract: `role="dialog"` `aria-modal="true"` `aria-labelledby` → title; Tab/Shift+Tab cycle inside the panel (focus trap); initial focus on the primary action; Esc closes **only** non-terminal modals (the result modal keeps focus on "Back to Lobby" and does not close on Esc — it requires an explicit choice); focus returns to the invoking element on close; body scroll locked while open.

### 7.8 Tabs

Used for Games panel: **Open** / **In Progress**. Container `border-b border-border-subtle`; each tab `h-10 px-4 text-sm font-medium` — inactive `text-text-muted hover:text-text-secondary`; active `text-text-primary` + 2 px bottom border `border-primary-hover` (indigo-400, 6.8:1) with `margin-bottom: -1px` overlapping the container border. Count chip inside each tab: mono 12 px `text-text-muted bg-surface-raised rounded-pill px-2` (e.g. "Open 3"). Keyboard: `role="tablist"`, arrows move between tabs, Home/End supported, Tab key exits the list; `aria-selected` on active tab, `role="tabpanel"` + `aria-labelledby` on the list below. Switching is instant (no spinner) — lists are already in memory.

### 7.9 Spinner

`animate-spin rounded-pill border-2 border-surface-raised border-t-primary-hover` (Tailwind default spin, 800 ms linear). Sizes: 16 px inline in buttons; 24 px inline in panels; 40 px centered page loading (`block mx-auto` + sr-only "Loading…"). Always paired with `aria-busy` on the owning region.

### 7.10 Skeleton

`rounded-control bg-surface-raised` blocks with shimmer (1.5 s loop, [animations.md](animations.md)). Shapes mirror real content — game card: 40 px circle + two bars (60 % / 35 % width, h-3); player row: 24 px circle + one bar; board: full board-square with 3×3 inner grid lines. Count mirrors expected data (4 game-card skeletons, 6 player rows). Containers `aria-hidden` with an `aria-busy="true"` parent; skeletons never pulse the live region.

### 7.11 GameBoard & Cell

Board card: centered, `w-[min(480px,100%)]`, `rounded-card border border-border-subtle bg-surface shadow-card p-4 sm:p-6`. Grid: `grid grid-cols-3 gap-2`; cell is a `<button>` with `aspect-square rounded-control`; effective cell size ≈ 88 px at 360 px viewport → 149 px at 480 px board width. Marks are inline SVG (viewBox `0 0 100 100`, `stroke-linecap="round"`, stroke-width 10): X = two lines stroke `--color-mark-x` + `shadow-glow-x`; O = circle stroke `--color-mark-o` + `shadow-glow-o`. A single SVG overlay across the board (viewBox `0 0 300 300`, cell centers at 50/150/250) draws the winning line.

| Cell state | Visual | Interaction / ARIA |
| --- | --- | --- |
| Empty (my turn) | `bg-bg/60 border border-border-subtle`; hover: `bg-surface-raised/60` | Enabled `<button>`; `aria-label="Row 1 Column 2, empty"` |
| Preview | Ghost of my mark at 35 % opacity, fades in 150 ms | Visible on hover/focus only; not rendered to assistive tech |
| Pending (submitted) | Preview mark at 60 % opacity, gentle pulse | `aria-disabled` until server confirms (≤ 200 ms) |
| Mine (X) / Opponent (O) | Full-color stroke-draw animation, 300 ms, glow shadow | `aria-label` becomes "Row 1 Column 2, X" (or O) |
| Win cell | Cell bg tinted `mark/10`, mark full brightness, glow pulse loop | Winning trio only |
| Disabled (not my turn / over / waiting) | `cursor-default`, no hover fill; existing marks stay full-color | `disabled` attribute; focus skipped in Tab order |

Keyboard: cells are buttons in DOM order (row-major: R1C1…R3C9); arrow keys rove focus, Enter/Space places the mark (spec: [pages-and-flows.md](pages-and-flows.md) §10).

### 7.12 ConnectionBadge

Pill, fixed placement: game card header, right edge — `inline-flex items-center gap-1.5 rounded-pill border px-2.5 py-0.5 text-caption`. **Connected**: emerald dot + "Connected" (`border-success/40 bg-success/10 text-success` — dot + word, 1.4.1). **Reconnecting...**: amber dot with soft opacity pulse + "Reconnecting..." (`border-status-playing/40 bg-status-playing/10 text-status-playing`). Changes announced via the game live region, not its own. Not shown on Login/Register/Not Found; lobby uses the navbar online-count pill instead.

### 7.13 Iconography

**lucide** (outline, MIT). Sizes 16 px (inline with text), 20 px (buttons, rows, toasts), 24 px (navbar), 48 px (empty-state illustration accents); stroke-width 2 (1.5 at 48 px). Icons inherit currentColor; decorative icons are `aria-hidden` — meaning is carried by adjacent text or `sr-only` labels. Core set: `user`, `mail`, `lock`, `eye`, `eye-off`, `plus`, `log-in`, `user-plus`, `users`, `gamepad-2`, `x`, `circle`, `trophy`, `flag` (defeat), `handshake` (draw), `wifi-off`, `alert-circle`, `check-circle-2`, `info`, `x` (dismiss), `log-out`, `home`. The brand mark is a custom 24 px X+O glyph (not lucide), indigo-400.

---

## 8. Empty States

Friendly, never blocking: icon + one-line title (`text-h3` / `text-text-primary`) + one-line support copy (`text-meta` / `text-text-muted`) + (where useful) a primary or secondary action. Illustrations are line-art lucide compositions at 25 % opacity in a 96 px circle of `bg-surface-raised/50` — no raster art.

| Context | Title | Support copy | Action |
| --- | --- | --- | --- |
| Games — Open tab | "No open games right now" | "Create a game and someone will join in seconds." | Button lg primary "Create Game" |
| Games — In Progress tab | "No games in progress" | "Live matches appear here the moment an opponent joins." | — |
| Online Players panel | "You're the first one here" | "Invite a friend — the lobby fills up fast." | — |
| Board (waiting, not empty-state panel) | "Waiting for opponent..." (animated dots) | "Share the moment — someone will join soon." | Button danger "Cancel" |

---

## 9. Focus Ring Spec

One global rule, appended in `styles.css`:

```css
:where(a, button, input, [tabindex]):focus-visible {
  outline: 2px solid var(--color-primary-hover); /* indigo-400 — 6.8:1 on bg */
  outline-offset: 2px;
  border-radius: inherit;
}
/* Elements sitting on an indigo fill (primary buttons) invert the ring */
.bg-primary:focus-visible, .bg-primary-hover:focus-visible {
  outline-color: #e0e7ff; /* indigo-200 — visible on indigo-500/400 */
}
```

`:focus-visible` only — mouse users get press scale instead; keyboard users get an unmissable ring on dark surfaces. Ring is never removed, and offset keeps the ring legible over borders and glows. Cells inherit it; the roving focus (§7.11) means the ring is always on exactly one cell.

---

## 10. Accessibility Foundations

- **Contrast**: every text/background pair in §3 passes WCAG 2.1 AA (most AAA); UI-component boundaries that must be seen (focus ring, active tab) pass 3:1; decorative borders are labeled as exempt with rationale.
- **Focus visible**: §9 — always visible, never suppressed, `:focus-visible` to spare pointer users.
- **Shape, not color**: X vs O are stroke glyphs; statuses pair dot + label; password strength pairs bar + word.
- **Keyboard**: full task completion without a pointer — forms, tabs, board (arrow-key roving + Enter/Space), modal trap/restore, toasts reachable via Tab. Logical tab order follows DOM order = visual order.
- **Live regions**: one polite live region per page for turns/results/lobby updates (placements per page: [pages-and-flows.md](pages-and-flows.md) §10).
- **Touch targets** ≥ 44 × 44 px (buttons h-11 or larger hit areas; cells 88 px+).
- **Reduced motion**: all animation degrades per the fallback table in [animations.md](animations.md) §4 — the app remains fully usable with motion off.
- **Semantics**: native elements first (`button`, `input`, `dialog`); ARIA only where native falls short (live regions, tabs, modal).
- **Zoom**: layouts remain functional to 200 % browser zoom (no fixed-position text overlap; board scales with rem).

---

## Assumptions

- Status semantics beyond the PRD: "In game" and "Reconnecting..." use amber-400 (distinct from emerald/success, sky/rose marks, and indigo/primary); "Available" and "Connected" share emerald-500 with different labels.
- Primary buttons carry a slate-950 label (4.5:1 on indigo-500) instead of white (4.47:1, fails AA); danger buttons use a tinted style with red-400 text (7.3:1) rather than a solid red-500 fill with white text (3.8:1) — the tokens stay indigo-500/indigo-400 and red-500 per the PRD.
- Inputs use slate-700 rest borders (slightly stronger than the card-border slate-800) because they are interactive controls; card borders stay slate-800 per the PRD.
- Avatar tint palette (8 hues at /15 fills with 300-level initials) and the username-hash mapping are chosen by the designer; backend/user model has no avatar data in MVP.
- Fonts are self-hosted via Fontsource (npm) rather than a CDN, for privacy, offline dev, and deterministic CI screenshots.
- Toast placement (bottom-right desktop, bottom mobile), 4/6-second durations and the 3-toast stack cap are designer defaults pending user testing.
- lucide is the icon library (outline style, tree-shakeable, MIT); the brand X+O logo glyph is a custom inline SVG.
