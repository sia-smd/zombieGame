// ZombieGame UI Builder — run once in your Figma file to generate the full design system + screens.
// Figma → Plugins → Development → Import plugin from manifest → select figma/plugin/manifest.json

const W = 390;
const H = 844;
const SAFE_TOP = 56;
const SAFE_BOTTOM = 34;

const C = {
  bgPrimary: { r: 0.039, g: 0.055, b: 0.078 },
  bgSurface: { r: 0.071, g: 0.094, b: 0.125 },
  bgElevated: { r: 0.102, g: 0.133, b: 0.188 },
  accentSurvivor: { r: 0.29, g: 0.871, b: 0.502 },
  accentZombie: { r: 0.937, g: 0.267, b: 0.267 },
  accentAction: { r: 0.231, g: 0.51, b: 0.965 },
  accentWarning: { r: 0.961, g: 0.62, b: 0.043 },
  textPrimary: { r: 0.945, g: 0.961, b: 0.976 },
  textSecondary: { r: 0.58, g: 0.639, b: 0.722 },
  textDisabled: { r: 0.392, g: 0.455, b: 0.545 },
  border: { r: 0.165, g: 0.208, b: 0.267 },
};

const S = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 };

async function loadFonts() {
  await figma.loadFontAsync({ family: "Inter", style: "Regular" });
  await figma.loadFontAsync({ family: "Inter", style: "Medium" });
  await figma.loadFontAsync({ family: "Inter", style: "Semi Bold" });
  await figma.loadFontAsync({ family: "Inter", style: "Bold" });
}

async function createDesignTokens() {
  var collections = await figma.variables.getLocalVariableCollectionsAsync();
  var existing = null;
  for (var i = 0; i < collections.length; i++) {
    if (collections[i].name === "ZombieGame") {
      existing = collections[i];
      break;
    }
  }
  if (existing) return existing;

  var col = figma.variables.createVariableCollection("ZombieGame");
  var modeId = col.modes[0].modeId;

  function colorVar(name, rgb) {
    var v = figma.variables.createVariable(name, col, "COLOR");
    v.setValueForMode(modeId, rgb);
    return v;
  }

  colorVar("color/bg/primary", C.bgPrimary);
  colorVar("color/bg/surface", C.bgSurface);
  colorVar("color/bg/elevated", C.bgElevated);
  colorVar("color/accent/survivor", C.accentSurvivor);
  colorVar("color/accent/zombie", C.accentZombie);
  colorVar("color/accent/action", C.accentAction);
  colorVar("color/accent/warning", C.accentWarning);
  colorVar("color/text/primary", C.textPrimary);
  colorVar("color/text/secondary", C.textSecondary);
  colorVar("color/text/disabled", C.textDisabled);
  colorVar("color/border/default", C.border);

  return col;
}

function def(value, fallback) {
  return value !== undefined && value !== null ? value : fallback;
}

function solid(color, a) {
  if (a === undefined) a = 1;
  return [{ type: "SOLID", color: color, opacity: a }];
}

function getOrCreatePage(name) {
  const existing = figma.root.children.find((p) => p.name === name);
  if (existing) return existing;
  const page = figma.createPage();
  page.name = name;
  return page;
}

function frame(name, w, h, opts = {}) {
  const f = figma.createFrame();
  f.name = name;
  f.resize(w, h);
  f.fills = solid(opts.fill || C.bgPrimary);
  f.clipsContent = true;
  if (opts.layout) {
    f.layoutMode = opts.layout || "VERTICAL";
    f.primaryAxisSizingMode = "FIXED";
    f.counterAxisSizingMode = "FIXED";
    f.paddingTop = def(opts.padT, S.lg);
    f.paddingBottom = def(opts.padB, S.lg);
    f.paddingLeft = def(opts.padL, S.lg);
    f.paddingRight = def(opts.padR, S.lg);
    f.itemSpacing = def(opts.gap, S.lg);
    if (opts.align) f.counterAxisAlignItems = opts.align;
    if (opts.primaryAlign) f.primaryAxisAlignItems = opts.primaryAlign;
  }
  return f;
}

function text(content, size, style, color) {
  const t = figma.createText();
  t.fontName = { family: "Inter", style };
  t.characters = content;
  t.fontSize = size;
  t.fills = solid(color || C.textPrimary);
  t.textAutoResize = "WIDTH_AND_HEIGHT";
  return t;
}

function rect(name, w, h, color, radius = 12) {
  const r = figma.createRectangle();
  r.name = name;
  r.resize(w, h);
  r.fills = solid(color);
  r.cornerRadius = radius;
  return r;
}

function applyAutoLayout(node, vertical = true, gap = S.lg, pad = S.lg) {
  node.layoutMode = vertical ? "VERTICAL" : "HORIZONTAL";
  node.primaryAxisSizingMode = "AUTO";
  node.counterAxisSizingMode = "AUTO";
  node.itemSpacing = gap;
  node.paddingTop = pad;
  node.paddingBottom = pad;
  node.paddingLeft = pad;
  node.paddingRight = pad;
}

// ─── Components ───────────────────────────────────────────────

async function createButtonComponent(parent) {
  function btn(label, bg, fg, w = 358) {
    const comp = figma.createComponent();
    comp.name = "Type=Primary, State=Default";
    comp.resize(w, 52);
    comp.cornerRadius = 12;
    comp.fills = solid(bg);
    applyAutoLayout(comp, false, 0, 0);
    comp.layoutMode = "HORIZONTAL";
    comp.primaryAxisAlignItems = "CENTER";
    comp.counterAxisAlignItems = "CENTER";
    comp.primaryAxisSizingMode = "FIXED";
    comp.counterAxisSizingMode = "FIXED";
    const lbl = text(label, 16, "Semi Bold", fg);
    comp.appendChild(lbl);
    lbl.layoutAlign = "CENTER";
    lbl.x = (w - lbl.width) / 2;
    lbl.y = (52 - lbl.height) / 2;
    parent.appendChild(comp);
    return comp;
  }

  const primary = btn("Button", C.accentAction, C.textPrimary);
  primary.name = "Type=Primary, State=Default";
  const secondary = btn("Button", C.bgElevated, C.textPrimary);
  secondary.name = "Type=Secondary, State=Default";
  secondary.strokes = solid(C.border);
  secondary.strokeWeight = 1;
  const danger = btn("Button", C.accentZombie, C.textPrimary);
  danger.name = "Type=Danger, State=Default";
  const ghost = btn("Button", C.bgPrimary, C.accentAction);
  ghost.fills = [];
  ghost.name = "Type=Ghost, State=Default";
  const disabled = btn("Button", C.bgElevated, C.textDisabled);
  disabled.name = "Type=Primary, State=Disabled";
  disabled.opacity = 0.5;
  const loading = btn("···", C.accentAction, C.textPrimary);
  loading.name = "Type=Primary, State=Loading";

  const set = figma.combineAsVariants([primary, secondary, danger, ghost, disabled, loading], parent);
  set.name = "Button";
  set.layoutMode = "HORIZONTAL";
  set.itemSpacing = 24;
  set.x = 16;
  set.y = 16;
  return set;
}

async function createAvatarComponent(parent) {
  function av(size, ring) {
    const comp = figma.createComponent();
    comp.name = `Size=MD, Ring=${ring}`;
    comp.resize(size + 8, size + 8);
    const circle = figma.createEllipse();
    circle.resize(size, size);
    circle.fills = solid(C.bgElevated);
    circle.x = 4;
    circle.y = 4;
    comp.appendChild(circle);
    if (ring !== "None") {
      circle.strokes = solid(ring === "Infected" ? C.accentZombie : C.accentSurvivor);
      circle.strokeWeight = 3;
    }
    const initials = text("AB", size * 0.3, "Semi Bold", C.textSecondary);
    initials.x = 4 + (size - initials.width) / 2;
    initials.y = 4 + (size - initials.height) / 2;
    comp.appendChild(initials);
    parent.appendChild(comp);
    return comp;
  }
  const a = av(56, "None");
  a.name = "Size=MD, Ring=None";
  const b = av(56, "Alive");
  b.name = "Size=MD, Ring=Alive";
  const set = figma.combineAsVariants([a, b], parent);
  set.name = "Avatar";
  set.x = 16;
  set.y = 120;
  return set;
}

async function createPlayerCardComponent(parent) {
  function card(state) {
    const comp = figma.createComponent();
    comp.name = `State=${state}`;
    comp.resize(170, 88);
    comp.cornerRadius = 12;
    comp.fills = solid(state === "Selected" ? C.bgElevated : C.bgSurface);
    if (state === "Selected") {
      comp.strokes = solid(C.accentAction);
      comp.strokeWeight = 2;
    }
    if (state === "Dead") comp.opacity = 0.45;
    const row = frame("Row", 154, 72, { fill: C.bgSurface, layout: "HORIZONTAL", gap: S.sm, padL: S.sm, padR: S.sm, padT: S.sm, padB: S.sm });
    row.fills = [];
    row.resize(154, 72);
    const av = rect("Avatar", 48, 48, C.bgElevated, 24);
    row.appendChild(av);
    const col = frame("Info", 90, 48, { layout: "VERTICAL", gap: S.xs, padL: 0, padR: 0, padT: 0, padB: 0 });
    col.fills = [];
    col.appendChild(text("Player", 14, "Semi Bold", C.textPrimary));
    col.appendChild(text(state === "Bot" ? "Bot" : "Alive", 12, "Regular", C.textSecondary));
    row.appendChild(col);
    comp.appendChild(row);
    row.x = 8;
    row.y = 8;
    parent.appendChild(comp);
    return comp;
  }
  const d = card("Default");
  d.name = "State=Default";
  const s = card("Selected");
  s.name = "State=Selected";
  const dead = card("Dead");
  dead.name = "State=Dead";
  const bot = card("Bot");
  bot.name = "State=Bot";
  const set = figma.combineAsVariants([d, s, dead, bot], parent);
  set.name = "PlayerCard";
  set.x = 16;
  set.y = 220;
  return set;
}

async function createBattleCardComponent(parent) {
  function bc(state) {
    const comp = figma.createComponent();
    comp.name = `State=${state}`;
    comp.resize(80, 112);
    comp.cornerRadius = 8;
    comp.fills = solid(state === "Hidden" ? C.bgElevated : C.bgSurface);
    if (state === "Hidden") {
      const back = text("?", 32, "Bold", C.textDisabled);
      back.x = 28;
      back.y = 40;
      comp.appendChild(back);
    } else {
      comp.appendChild(text(state, 12, "Semi Bold", state === "Action" ? C.accentSurvivor : C.textSecondary));
    }
    parent.appendChild(comp);
    return comp;
  }
  const h = bc("Hidden");
  h.name = "State=Hidden";
  const a = bc("Action");
  a.name = "State=Action";
  const p = bc("Pass");
  p.name = "State=Pass";
  const set = figma.combineAsVariants([h, a, p], parent);
  set.name = "BattleCard";
  set.x = 200;
  set.y = 220;
  return set;
}

async function createTimerComponent(parent) {
  function tm(urgent) {
    const comp = figma.createComponent();
    comp.name = urgent ? "State=Urgent" : "State=Normal";
    comp.resize(120, 36);
    comp.cornerRadius = 999;
    comp.fills = solid(urgent ? C.accentZombie : C.bgElevated);
    const lbl = text("0:30", 16, "Semi Bold", C.textPrimary);
    lbl.x = 36;
    lbl.y = 8;
    comp.appendChild(lbl);
    parent.appendChild(comp);
    return comp;
  }
  const n = tm(false);
  n.name = "State=Normal";
  const u = tm(true);
  u.name = "State=Urgent";
  const set = figma.combineAsVariants([n, u], parent);
  set.name = "Timer";
  set.x = 16;
  set.y = 340;
  return set;
}

async function createChatBubbleComponent(parent) {
  function bubble(self) {
    const comp = figma.createComponent();
    comp.name = self ? "Align=Self" : "Align=Other";
    comp.resize(280, 48);
    comp.cornerRadius = 12;
    comp.fills = solid(self ? C.accentAction : C.bgSurface);
    const lbl = text("Message text here", 14, "Regular", C.textPrimary);
    lbl.x = 12;
    lbl.y = 14;
    comp.appendChild(lbl);
    parent.appendChild(comp);
    return comp;
  }
  const o = bubble(false);
  o.name = "Align=Other";
  const s = bubble(true);
  s.name = "Align=Self";
  const set = figma.combineAsVariants([o, s], parent);
  set.name = "ChatBubble";
  set.x = 16;
  set.y = 400;
  return set;
}

async function createVoteItemComponent(parent) {
  function vi(state) {
    const comp = figma.createComponent();
    comp.name = `State=${state}`;
    comp.resize(358, 56);
    comp.cornerRadius = 12;
    comp.fills = solid(state === "Selected" ? C.bgElevated : C.bgSurface);
    if (state === "Selected") {
      comp.strokes = solid(C.accentAction);
      comp.strokeWeight = 2;
    }
    const lbl = text("Player Name", 16, "Medium", state === "Disabled" ? C.textDisabled : C.textPrimary);
    lbl.x = 16;
    lbl.y = 18;
    comp.appendChild(lbl);
    parent.appendChild(comp);
    return comp;
  }
  const d = vi("Default");
  d.name = "State=Default";
  const s = vi("Selected");
  s.name = "State=Selected";
  const dis = vi("Disabled");
  dis.name = "State=Disabled";
  const set = figma.combineAsVariants([d, s, dis], parent);
  set.name = "VoteItem";
  set.x = 16;
  set.y = 480;
  return set;
}

async function createRoomItemComponent(parent) {
  function ri(state) {
    const comp = figma.createComponent();
    comp.name = `State=${state}`;
    comp.resize(358, 72);
    comp.cornerRadius = 12;
    comp.fills = solid(C.bgSurface);
    comp.appendChild(text("Room #1247", 16, "Semi Bold", C.textPrimary));
    comp.children[0].x = 16;
    comp.children[0].y = 14;
    const sub = text(state === "Full" ? "Full · 12/12" : "8/12 · Waiting", 12, "Regular", C.textSecondary);
    sub.x = 16;
    sub.y = 38;
    comp.appendChild(sub);
    parent.appendChild(comp);
    return comp;
  }
  const d = ri("Default");
  d.name = "State=Default";
  const f = ri("Full");
  f.name = "State=Full";
  const l = ri("Locked");
  l.name = "State=Locked";
  const set = figma.combineAsVariants([d, f, l], parent);
  set.name = "RoomItem";
  set.x = 16;
  set.y = 560;
  return set;
}

async function buildComponentLibrary(page) {
  figma.currentPage = page;
  const section = frame("📦 Component Library", 800, 700, { fill: C.bgPrimary });
  page.appendChild(section);
  section.x = 0;
  section.y = 0;
  section.appendChild(text("ZombieGame — Components", 24, "Bold", C.textPrimary));

  await createButtonComponent(section);
  await createAvatarComponent(section);
  await createPlayerCardComponent(section);
  await createBattleCardComponent(section);
  await createTimerComponent(section);
  await createChatBubbleComponent(section);
  await createVoteItemComponent(section);
  await createRoomItemComponent(section);

  section.appendChild(text("Design tokens: see DESIGN_SYSTEM.md", 12, "Regular", C.textSecondary));
}

// ─── Screen builder helpers ───────────────────────────────────

function hierarchyLabel(parent, label) {
  const tag = frame(`◆ ${label}`, 120, 20, { layout: "HORIZONTAL", gap: 0, padL: 6, padR: 6, padT: 2, padB: 2 });
  tag.fills = solid(C.accentAction, 0.2);
  tag.cornerRadius = 4;
  tag.strokes = solid(C.accentAction, 0.5);
  tag.strokeWeight = 1;
  tag.appendChild(text(label, 10, "Medium", C.accentAction));
  tag.children[0].layoutAlign = "INHERIT";
  parent.appendChild(tag);
  return tag;
}

function screenShell(name, state) {
  const root = frame(`${name} / ${state}`, W, H, { fill: C.bgPrimary, layout: "VERTICAL", gap: S.lg, padT: SAFE_TOP, padB: SAFE_BOTTOM });
  hierarchyLabel(root, state);
  return root;
}

function topBar(title, subtitle) {
  const bar = frame("TopBar", W - 32, 56, { layout: "VERTICAL", gap: S.xs, padL: 0, padR: 0, padT: 0, padB: 0 });
  bar.fills = [];
  bar.appendChild(text(title, 24, "Bold", C.textPrimary));
  if (subtitle) bar.appendChild(text(subtitle, 14, "Regular", C.textSecondary));
  return bar;
}

function bottomNav(active) {
  const nav = frame("BottomNav", W - 32, 56, { layout: "HORIZONTAL", gap: S.xl, align: "CENTER", primaryAlign: "CENTER" });
  nav.fills = solid(C.bgSurface);
  nav.cornerRadius = 16;
  ["Lobby", "Rooms", "Profile"].forEach((item) => {
    nav.appendChild(text(item, 12, item === active ? "Semi Bold" : "Regular", item === active ? C.accentAction : C.textSecondary));
  });
  return nav;
}

function stateOverlay(state) {
  if (state === "Default") return null;
  const overlay = frame("StateOverlay", W, H, { fill: C.bgPrimary });
  overlay.fills = state === "Loading" ? solid(C.bgPrimary, 0.85) : solid(C.bgPrimary);
  if (state === "Loading") {
    const spinner = rect("Spinner", 48, 48, C.accentAction, 24);
    spinner.x = (W - 48) / 2;
    spinner.y = H / 2 - 24;
    overlay.appendChild(spinner);
    overlay.appendChild(text("Loading…", 14, "Regular", C.textSecondary));
    overlay.children[1].x = (W - 60) / 2;
    overlay.children[1].y = H / 2 + 36;
  } else if (state === "Empty") {
    overlay.appendChild(text("Nothing here yet", 18, "Semi Bold", C.textPrimary));
    overlay.appendChild(text("Try a different action", 14, "Regular", C.textSecondary));
    overlay.children[0].x = 80;
    overlay.children[0].y = H / 2 - 20;
    overlay.children[1].x = 90;
    overlay.children[1].y = H / 2 + 8;
  } else if (state === "Error") {
    overlay.appendChild(text("Something went wrong", 18, "Semi Bold", C.accentZombie));
    overlay.appendChild(text("Check connection and retry", 14, "Regular", C.textSecondary));
    overlay.children[0].x = 70;
    overlay.children[0].y = H / 2 - 20;
    overlay.children[1].x = 80;
    overlay.children[1].y = H / 2 + 8;
  }
  overlay.x = 0;
  overlay.y = 0;
  return overlay;
}

function buildScreenContent(name, state) {
  const root = screenShell(name, state);

  switch (name) {
    case "01 Splash":
      root.appendChild(text("ZOMBIE", 32, "Bold", C.accentSurvivor));
      root.appendChild(text("GAME", 32, "Bold", C.textPrimary));
      root.appendChild(rect("Progress", 200, 4, C.bgElevated, 2));
      break;

    case "02 Guest Login":
      root.appendChild(topBar("Welcome", "Play as guest — no account needed"));
      root.appendChild(rect("Hero", 358, 200, C.bgSurface, 16));
      root.appendChild(text("Continue as Guest", 16, "Semi Bold", C.textPrimary));
      root.appendChild(text("Device ID saved locally", 12, "Regular", C.textSecondary));
      break;

    case "03 Lobby":
      root.appendChild(topBar("Lobby", "Coins: 10"));
      root.appendChild(rect("CreateRoomCTA", 358, 52, C.accentAction, 12));
      root.appendChild(rect("RoomListCTA", 358, 52, C.bgElevated, 12));
      root.appendChild(bottomNav("Lobby"));
      break;

    case "04 Room List":
      root.appendChild(topBar("Rooms", "Join an open match"));
      for (let i = 0; i < 3; i++) root.appendChild(rect(`RoomItem${i}`, 358, 72, C.bgSurface, 12));
      break;

    case "05 Create Room":
      root.appendChild(topBar("Create Room", "Configure match"));
      root.appendChild(text("Players: 12", 16, "Medium", C.textPrimary));
      root.appendChild(text("Fill with bots: On", 14, "Regular", C.textSecondary));
      root.appendChild(rect("Confirm", 358, 52, C.accentAction, 12));
      break;

    case "06 Waiting Room":
      root.appendChild(topBar("Waiting Room", "3/12 players"));
      root.appendChild(rect("PlayerGrid", 358, 320, C.bgSurface, 16));
      root.appendChild(rect("StartGame", 358, 52, C.accentSurvivor, 12));
      break;

    case "07 Day Start":
      root.appendChild(topBar("Day 3", "12 alive · 0 dead"));
      root.appendChild(text("Cards dealt", 16, "Semi Bold", C.textPrimary));
      root.appendChild(rect("CardFan", 358, 140, C.bgSurface, 16));
      root.appendChild(rect("Continue", 358, 52, C.accentAction, 12));
      break;

    case "08 Opponent Selection":
      root.appendChild(topBar("Choose Opponent", "Tap a player to invite"));
      root.appendChild(rect("Timer", 120, 36, C.accentWarning, 18));
      root.appendChild(rect("PlayerGrid", 358, 400, C.bgSurface, 16));
      root.appendChild(rect("Ready", 358, 52, C.accentAction, 12));
      break;

    case "09 Private Battle":
      root.appendChild(topBar("Battle", "You vs Opponent"));
      root.appendChild(rect("Timer", 120, 36, C.bgElevated, 18));
      root.appendChild(rect("YourHand", 358, 120, C.bgSurface, 16));
      root.appendChild(rect("Pass", 170, 52, C.bgElevated, 12));
      root.appendChild(rect("Play", 170, 52, C.accentAction, 12));
      break;

    case "10 Battle Result":
      root.appendChild(topBar("Battle Results", "Day 3 — public summary"));
      root.appendChild(text("Player A · Action", 14, "Regular", C.textPrimary));
      root.appendChild(text("Player B · Pass", 14, "Regular", C.textSecondary));
      root.appendChild(rect("SummaryList", 358, 280, C.bgSurface, 16));
      break;

    case "11 Discussion":
      root.appendChild(topBar("Discussion", "60s remaining"));
      root.appendChild(rect("ChatArea", 358, 520, C.bgSurface, 16));
      root.appendChild(rect("ChatInput", 358, 48, C.bgElevated, 12));
      break;

    case "12 Voting":
      root.appendChild(topBar("Vote", "Secret ballot"));
      root.appendChild(rect("Timer", 120, 36, C.bgElevated, 18));
      for (let i = 0; i < 4; i++) root.appendChild(rect(`Vote${i}`, 358, 56, C.bgSurface, 12));
      break;

    case "13 Vote Result":
      root.appendChild(topBar("Vote Result", "Majority required"));
      root.appendChild(text("Player X eliminated", 18, "Semi Bold", C.accentZombie));
      root.appendChild(rect("VoteBreakdown", 358, 300, C.bgSurface, 16));
      break;

    case "14 Match Summary":
      root.appendChild(topBar("Victory!", "Humans win"));
      root.appendChild(text("MVP: Player A", 16, "Semi Bold", C.accentSurvivor));
      root.appendChild(rect("Stats", 358, 240, C.bgSurface, 16));
      root.appendChild(rect("PlayAgain", 358, 52, C.accentAction, 12));
      break;
  }

  const overlay = stateOverlay(state);
  if (overlay) root.appendChild(overlay);

  return root;
}

const SCREENS = [
  "01 Splash",
  "02 Guest Login",
  "03 Lobby",
  "04 Room List",
  "05 Create Room",
  "06 Waiting Room",
  "07 Day Start",
  "08 Opponent Selection",
  "09 Private Battle",
  "10 Battle Result",
  "11 Discussion",
  "12 Voting",
  "13 Vote Result",
  "14 Match Summary",
];

const STATES = ["Default", "Loading", "Empty", "Error"];

async function buildAllScreens() {
  for (const screenName of SCREENS) {
    const page = getOrCreatePage(screenName);
    figma.currentPage = page;
    let x = 0;
    for (const state of STATES) {
      const scr = buildScreenContent(screenName, state);
      page.appendChild(scr);
      scr.x = x;
      scr.y = 0;
      x += W + 48;
    }
  }
}

function buildNavigationFlow(page) {
  figma.currentPage = page;
  const flow = frame("Navigation Flow", 1200, 600, { layout: "VERTICAL", gap: S.lg, padT: S.xl, padL: S.xl });
  page.appendChild(flow);
  flow.appendChild(text("Navigation Flow", 24, "Bold", C.textPrimary));

  const steps = [
    "Splash → Guest Login → Lobby",
    "Lobby → Room List → Join → Waiting Room",
    "Lobby → Create Room → Waiting Room → Start Game",
    "Waiting Room → Day Start → Opponent Selection → Private Battle",
    "Private Battle → Battle Result → Discussion → Voting → Vote Result",
    "Vote Result → Day Start (loop) | Match Summary (end)",
  ];
  steps.forEach((s) => flow.appendChild(text(s, 14, "Regular", C.textSecondary)));

  const legend = frame("States Legend", 400, 120, { layout: "VERTICAL", gap: S.sm });
  legend.fills = solid(C.bgSurface);
  legend.cornerRadius = 12;
  ["Default — normal UI", "Loading — spinner overlay", "Empty — no data", "Error — retry message"].forEach((l) =>
    legend.appendChild(text(l, 12, "Regular", C.textSecondary))
  );
  flow.appendChild(legend);
}

async function main() {
  await loadFonts();
  try {
    await createDesignTokens();
  } catch (e) {
    figma.notify("Design tokens skipped: " + e.message);
  }

  figma.notify("Building ZombieGame UI…");

  const componentsPage = getOrCreatePage("🧩 Components");
  await buildComponentLibrary(componentsPage);
  await buildAllScreens();

  // Cover + flow page
  const cover = getOrCreatePage("📋 Cover & Flow");
  figma.currentPage = cover;
  const info = frame("Project Info", 600, 400, { layout: "VERTICAL", gap: S.lg });
  cover.appendChild(info);
  info.appendChild(text("ZombieGame UI", 32, "Bold", C.textPrimary));
  info.appendChild(text("390×844 · Dark sci-fi · Mobile portrait", 14, "Regular", C.textSecondary));
  info.appendChild(text("14 screens × 4 states · Component library on 🧩 page", 14, "Regular", C.textSecondary));
  info.appendChild(text("Variables: ZombieGame collection (color tokens)", 12, "Regular", C.textSecondary));
  info.appendChild(text("Unity handoff: see DESIGN_SYSTEM.md", 12, "Regular", C.textDisabled));
  buildNavigationFlow(cover);

  figma.currentPage = componentsPage;
  figma.viewport.scrollAndZoomIntoView(figma.currentPage.children);

  figma.notify("✅ ZombieGame UI complete — 14 pages, components, 56 screen states");
  figma.closePlugin();
}

main().catch(function (err) {
  figma.notify("Plugin error: " + err.message, { error: true });
  figma.closePlugin();
});
