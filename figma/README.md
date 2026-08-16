# ZombieGame Figma UI

Generates the complete mobile game UI inside your Figma file.

## Prerequisites

- [Figma Desktop](https://www.figma.com/downloads/) (required for plugins)
- Your file: [ZombieGame](https://www.figma.com/design/6s6nXpb1Dkjco8jHeyvOTH/ZombieGame)

## Install the plugin (one time)

1. Open **Figma Desktop**
2. Open your **ZombieGame** file
3. Menu → **Plugins** → **Development** → **Import plugin from manifest…**
4. Select: `D:\siavash\zombie\ZombieGame\figma\plugin\manifest.json`
5. Plugin appears as **ZombieGame UI Builder**

## Generate the UI

1. In Figma: **Plugins** → **Development** → **ZombieGame UI Builder**
2. Wait ~10 seconds
3. Result:
   - **🧩 Components** — Button, PlayerCard, BattleCard, Avatar, Timer, ChatBubble, VoteItem, RoomItem (with variants)
   - **14 screen pages** — each with Default, Loading, Empty, Error frames
   - **📋 Cover & Flow** — project overview

## Re-run safely

Running again **adds new pages** if names don't exist. To rebuild from scratch, delete generated pages first.

## Design reference

See [DESIGN_SYSTEM.md](./DESIGN_SYSTEM.md) for tokens, typography, navigation flow, and Unity handoff.

## Optional: API fetch

```powershell
# Put token in figma/token.txt (gitignored), then:
.\fetch-figma.ps1
```

## Security

Never commit `figma/token.txt`. Rotate Figma tokens if exposed in chat.
