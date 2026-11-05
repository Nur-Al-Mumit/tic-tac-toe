# Tic-Tac-Toe client (Angular 22 + Tailwind CSS 4)

Angular 22 — standalone components, signals, **zoneless** change detection. Tailwind CSS 4 with CSS-first `@theme` tokens in `src/styles.css`.

## Requirements

- **Node.js ≥ 24.15** (Angular CLI 22 requires it). If your system Node is older, install a user-local Node 24:

  ```bash
  LATEST=$(curl -s https://nodejs.org/dist/index.json | python3 -c "import json,sys; print([v['version'] for v in json.load(sys.stdin) if v['version'].startswith('v24.')][0])")
  mkdir -p ~/.local/node && curl -fsSL "https://nodejs.org/dist/$LATEST/node-$LATEST-linux-x64.tar.xz" -o /tmp/node24.tar.xz \
    && tar -xJf /tmp/node24.tar.xz -C ~/.local/node && mv ~/.local/node/node-$LATEST-linux-x64 ~/.local/node/current
  export PATH="$HOME/.local/node/current/bin:$PATH"
  ```

## Commands

```bash
npm ci            # install
npm start         # dev server at http://localhost:4200 (proxies /api and /hubs to http://localhost:8080)
npm test          # vitest unit tests (single run in CI: npm test -- --watch=false)
npm run build     # production build
```

Architecture: [docs/04-frontend/angular-architecture.md](../docs/04-frontend/angular-architecture.md) · Design system: [docs/04-frontend/ux-ui-design-system.md](../docs/04-frontend/ux-ui-design-system.md)
