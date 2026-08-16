# Zombie vs Human — Frontend

Production-ready Vue 3 client for the **Zombie vs Human** multiplayer game. This package is **frontend only**; all game logic lives in the ASP.NET Core backend.

## Stack

- Vue 3 + TypeScript + Vite (`vite.config.ts`)
- Tailwind CSS (dark fantasy / mobile-game UI)
- Pinia, Vue Router, VueUse
- Axios (REST), `@microsoft/signalr` (realtime)
- GSAP (major transitions; skipped when Reduced Motion is on), Heroicons
- Vitest for unit tests

## Architecture

```
src/
  components/   # Presentational UI (common, lobby, battle, chat, layout)
  composables/  # Reusable hooks (animations, labels, session, phase navigation)
  pages/        # Route views
  services/     # API + SignalR (no UI, no game rules)
  stores/       # Pinia state synced from REST/SignalR
  types/        # DTOs & enums aligned with backend
```

**Rules:** Pages compose components. Components display data only. API calls live in `services/`. State lives in Pinia stores. SignalR events update stores automatically.

## Prerequisites

- Node.js 18+ (20 LTS recommended)
- Backend API running at `http://localhost:5232` (HTTPS: `https://localhost:7096`)

## Setup

```bash
cd frontend
cp .env.example .env
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` and `/hubs` to the backend.

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Development server |
| `npm run build` | Production build |
| `npm run preview` | Preview production build |
| `npm run typecheck` | TypeScript check |
| `npm test` | Vitest (watch) |
| `npm run test:run` | Vitest once |

## Routes

| Path | Screen |
|------|--------|
| `/` | Splash + auto guest login |
| `/login` | Login / guest |
| `/home` | Main hub |
| `/rooms` | Matchmaking |
| `/rooms/create` | Create room |
| `/room/:id` | Lobby |
| `/reveal-role/:id` | Role reveal after start |
| `/game/:id` | Game room (day / discussion / pairing) |
| `/battle/:id` | Card battle |
| `/battle-summary/:id` | Battle / day summary |
| `/voting/:id` | Voting and vote results |
| `/match-result/:id` | Match finished |
| `/profile`, `/settings`, `/shop` | Meta screens |

Live match pages use `useMatchPhaseNavigation` so a refresh lands on the current phase (lobby stays on `/room/:id` only while the phase is Lobby).

## SignalR

**Room hub** (`/hubs/room`): `RoomUpdated`, `PlayerJoined`, `GameStarted`, `BattleState`, `ChatMessage`, `VoteStarted`, `VoteFinished`, `PlayerEliminated`, `DayStarted`, `GameFinished`, `PhaseChanged`

The live UI is driven mainly by `RoomUpdated` snapshots (`snapshotVersion` drops stale payloads). Other named events are used for last-event hints or follow-up sync.

**Game hub** (`/hubs/game`): `SyncState`, `GameEvent` — legacy; current match pages use the Room hub.

Handlers are registered via `room.service.ts` / `game.service.ts` and wired in Pinia stores.

## Environment

```env
VITE_API_BASE_URL=
VITE_HUB_BASE_URL=
```

Leave empty to talk to the **same origin** (`/api` and `/hubs` on `zombie.com`). That is the IIS layout: Vue at `/`, ASP.NET at `/api` and `/hubs`. Set absolute URLs only if the API is on another host.

## IIS (same site)

Publish the API project (`dotnet publish ZombieGame/ZombieGame.csproj -c Release`). The Vue `dist` is copied into `wwwroot`. Point the IIS site at that publish folder, bind `zombie.com`, and enable WebSocket. Vue routes fall back to `index.html`; `/api` and `/hubs` stay on the backend.

Skip the frontend build with `/p:SkipFrontendBuild=true` if you only need the API.

## Mobile-first layout

Primary viewport: **390×844** portrait. Desktop centers the game frame with ambient background (`MobileFrame`). Browser pinch-zoom is allowed.

## Internationalization

- **Library:** `vue-i18n`
- **Locales shipped:** `en` (default) and `fa` in `src/i18n/locales/`
- **Language store:** `useLanguageStore()` — `changeLanguage()`, `isRTL`, `direction`
- **Switcher:** `LanguageSwitcher` in TopBar and Settings
- **Persistence:** `localStorage` key `zvh_lang`
- **RTL:** Persian sets `document.documentElement.dir` and `lang` automatically
- **Formatting:** `useLocaleFormat()` — `Intl.NumberFormat` / `Intl.DateTimeFormat`
- **Lazy loading:** non-English locales load on demand via dynamic `import()`

To add a language: create `src/i18n/locales/xx.json`, register in `i18n/index.ts` and `helpers/language.ts`.

## License

Private — part of the ZombieGame monorepo.
