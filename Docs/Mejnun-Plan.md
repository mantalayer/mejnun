# Mejnun — Game Design & Implementation Plan

Status: draft v1 (2026-11-16), written from two interview rounds with Brennan.
Engine: Unity 6000.4.0f1 · URP 17.4 · Input System 1.19 · Wwise 2025.1.12
Targets: iOS, Android, Windows, macOS (Steam for desktop).

---

## 1. Rules (canonical spec)

The rules engine implements this section exactly. Anything that isn't covered here is in §11 *Open questions*.

### 1.1 Components
- Two standard 52-card decks shuffled together, so **104 cards** and **no jokers**.
- Every physical card has a unique id `0..103` = `deckIndex * 52 + suit * 13 + rankIndex`.
- Two cards with the same suit and rank (one from each deck) are **doubles**.

### 1.2 Players
- **2–6 players** in every mode. Seats are arranged clockwise ("left of dealer" = next seat clockwise).

### 1.3 Choosing the dealer
- If the previous game in this session/lobby had a winner, that winner deals.
- On the first game, the game shows a random **"embarrassing question"** prompt ("Who showers the least?"). Players then pick the dealer:
  - Local / pass-and-play: someone taps the chosen player's seat.
  - Online: everyone votes. The most votes wins, ties are broken at random, and the vote times out after 20 s, falling back to random.
  - Vs AI: the human taps a seat (bots included), or taps "Random".
- Questions live in a localized data table (`DealerQuestions`), so more can be added or changed per language.

### 1.4 Deal
- Cards are dealt one at a time, starting with the player left of the dealer and going clockwise, until everyone has **11 cards**.
- The remaining cards (104 − 11·N, so 82 to 38) form the face-down **draw pile** in the centre.
- The **first turn** goes to the player left of the dealer *(assumed, see Q1)*.

### 1.5 Objective
- The first player to get rid of every card in their hand wins, and the game ends at once. The win moment ("MEJNUN!") plays **automatically**.
- **Single round**: there is no scoring across rounds. The winner deals next game.

### 1.6 Valid sets ("melds")
| Type | Size | Rule |
|---|---|---|
| **Same kind** | 3–4 | Same rank; every suit different (so no doubles by definition). |
| **Straight** | 3–13 | Same suit; consecutive ranks; no repeated rank (no doubles). |

- An ace is **either** low (A-2-3…) **or** high (…Q-K-A), never both in one straight. There is **no wrap-around** (K-A-2 is invalid).
- The board is **valid** when every set on it is valid. Sets of 1 or 2 cards are never valid.

### 1.7 Player phases
Each player is either **Not down** (the starting phase) or **Down**.

**Not down**
- The player may only build **new** sets, using **only cards from their hand**, and may not touch existing board sets.
- They may put down **one or more** sets at once. There is **no minimum point value**.
- As soon as at least one valid hand-only set is on the board and the player confirms, they are **Down**. They can then **keep playing in the same turn** with full board access. This is the "Same turn, but they cannot use the board to help them go down" answer.
- Engine rule: the **go-down commit** checks that each committed set is valid and made only of cards that were in the player's hand at the start of the turn. Undoing past the commit point returns the player to Not down.

**Down**
- The player may rearrange anything on the board: split straights, take a card from a 4-card same-kind set, merge, move single cards, add cards from hand, and build temporary invalid groups.
- To end the turn normally they must have placed **at least one card from hand**, and the board must be **valid**.

### 1.8 Turn actions
- **Draw / Pass**: always allowed, whether the player is down or not. It **reverts the board** to the start of the turn, returns every placed card to the hand, draws one card, and **ends the turn**.
- **End turn**: only enabled when the board is valid **and** at least one hand card was placed (or a go-down happened).
- **Undo**: steps back one move at a time within the turn.
- **Revert** (the "make board valid" button): restores the board exactly as it was at the start of the turn and returns every card the player placed to their hand. It always works.
- A card that came from your hand can go back to your hand during the same turn. Cards that **started the turn on the board** can never go into a hand.

### 1.9 Timed mode (optional, chosen by the host)
- Presets: **30 / 60 / 90 / 120 s** per turn, or Off.
- On timeout:
  1. No hand card placed, board valid → **keep** the rearranged board and **draw**.
  2. No hand card placed, board invalid → revert, then draw.
  3. Hand card(s) placed, board valid → the turn ends normally (no draw).
  4. Hand card(s) placed, board invalid → revert, then draw.

### 1.10 Empty draw pile
- Once the pile is empty, "Draw" becomes **"Pass"**: the player draws nothing and the board is reverted.
- If every active player passes in a row without placing a card, the game ends and the player with the **fewest cards in hand** wins *(tie-break: see Q2)*.

### 1.11 Hand helpers (UI, not rules)
- Sort by suit, sort by rank, drag to reorder manually.
- "Highlight playable" hint: optional in Settings. It's disabled in ranked or quick-match online games *(see Q3)*.

---

## 2. Modes

| Mode | Seats | Authority | Notes |
|---|---|---|---|
| **Solo vs AI** | 1 human + 1–5 bots | Local engine | Easy / Medium / Hard per bot |
| **Pass & play** | 2–6 humans (+ optional bots) | Local engine | Handoff screen ("Pass to Sam — tap to reveal") between turns |
| **Online live** | 2–6 (bots can fill seats) | Host-authoritative over UGS Relay | Room codes, quick match, friends invites |

The same pure-C# `Mejnun.Core` engine runs every mode. Only the input source changes (local touch, AI, or network).

---

## 3. Architecture

### 3.1 Assemblies (asmdefs) under `Assets/_Project/Scripts/`
```
Core/        Mejnun.Core        pure C#, noEngineReferences: cards, melds, validator, game state, turn/undo, RNG, serialization
AI/          Mejnun.AI          → Core. Bots + hint solver (pure C#, can run off the main thread)
Game/        Mejnun.Game        → Core, AI, Audio, Platform. MonoBehaviours: table, hand, drag/drop, camera, game controller
Net/         Mejnun.Net         → Core, Game. NGO + UGS Multiplayer Services (sessions, relay, lobby), host migration, snapshots
UI/          Mejnun.UI          → Game. UI Toolkit screens, HUD, localization bindings
Audio/       Mejnun.Audio       → Wwise. Thin AudioService wrapper (events/switches/RTPCs by ID)
Cosmetics/   Mejnun.Cosmetics   → Core. DeckSkin, CardBack, TableFelt, Avatar, Shout SOs, inventory, unlock rules
Platform/    Mejnun.Platform    store-neutral interfaces (ported from terra-firma), Null backend
Platform/Steam Mejnun.Platform.Steam   (MEJNUN_STEAM define) Steamworks.NET backend
Platform/Editor Mejnun.Platform.Editor Steam build/upload window, version bump, git tag
Editor/      Mejnun.Editor      deck-skin importer, debug tools
Tests/EditMode  Mejnun.Core.Tests, Mejnun.AI.Tests
Tests/PlayMode  Mejnun.Game.Tests
```
Rule: `Core` must not reference `UnityEngine`. That keeps it fast to unit-test, deterministic, and reusable in a Cloud Code or dedicated server later.

### 3.2 Core model
- `Card` (readonly struct: `Id`, `Suit`, `Rank`, `Deck`) and `Rank` (Ace..King). An ace's role (low/high) is decided per straight by the validator.
- `Meld` holds an id plus an ordered `List<Card>`. `MeldValidator.Classify(meld)` returns `SameKind | StraightLow | StraightHigh | Invalid(reason)`. The reasons drive UI tooltips: "double", "too short", "mixed suits", and so on.
- `GameState` holds seats, hands, board (melds), draw pile, current seat, dealer, phase per player, turn number, timer, and RNG seed.
- `TurnSession` holds the start-of-turn snapshot and a move list: `MoveCard(cardId, from, to, index)`, `SplitMeld`, `MergeMelds`, `GoDown`. It supports Undo, Revert and `CanEndTurn()`, plus timeout resolution (§1.9).
- Commands carry the player's **intent**. The engine validates every one. It is identical locally and online, where clients send commands and the host applies them.
- A deterministic `Xoshiro`/`PCG` RNG is seeded per game, so games can be replayed for bug reports and tests.
- `GameEvents` are emitted as plain C# events (CardDealt, CardMoved, MeldBecameValid, WentDown, TurnEnded, Drew, Won, …). Presentation, audio and network all subscribe to them.

### 3.3 Presentation
- The **board and hand** are 2D world-space `SpriteRenderer` cards under an orthographic camera. That makes pinch-zoom and pan natural, keeps pixel-art cards crisp with point filtering and a per-zoom pixel snap, and lets cards tween cheaply.
- **Auto-arranged board**: sets flow into rows using a wrap layout that depends on the viewport aspect, which handles both orientations. Straights auto-sort their cards, while same-kind sets keep insertion order. Each set shows a valid/invalid outline. An "empty area" drop zone creates a new group.
- **Zoom & pan**: pinch or two-finger drag on touch; wheel, right/middle-drag or trackpad on PC. A "fit board" button and automatic fit after each change unless the user zoomed manually.
- **Hand**: a fanned or scrollable row at the bottom (landscape), or a two-row grid (portrait), with sort buttons.
- **HUD and menus** use **UI Toolkit** (recommended): USS responsive classes handle portrait/landscape and safe areas, it integrates cleanly with Unity Localization, and the "mixed" art direction (pixel cards with smooth modern UI) fits it well. uGUI is avoided apart from world-space bits if needed.

### 3.4 Input (touch-first; the mouse acts as touch)
- Input System with `EnhancedTouch`. One `PointerRouter` turns touch and mouse into the same tap, drag, long-press and pinch gestures.
- Interactions:
  - **Drag & drop** a card from hand or board onto a set, between two cards of a straight (insert), onto empty space (new group), or back to hand (only cards from your hand).
  - **Tap-select then tap-target** as an accessible alternative, especially for small phones.
  - **Long-press a straight segment** to split it at that point, or drag the tail end to split.
  - Haptics on mobile for pickup, drop and invalid.
- On PC the mouse works as a pointer. Keyboard shortcuts are optional extras: Ctrl+Z undo, R revert, Space end turn, D draw.

### 3.5 AI (`Mejnun.AI`)
- One shared **solver** asks: given the board cards (B), a subset of hand cards (H) and the go-down constraint, what is a valid partition of B ∪ H′ that maximises |H′|? It works as exact-cover-style backtracking over candidate melds, with memoisation and a time budget. It runs on a worker thread and returns the best result so far when time runs out.
- **Easy**: only plays new sets from hand plus simple extensions of existing sets. It occasionally "misses" plays on purpose.
- **Medium**: extensions, splits, and taking from 4-card same-kind sets, using greedy rearrangement with local search.
- **Hard**: a full board re-partition via the solver. It holds cards back strategically (voluntary draw is legal) and tracks which doubles have been seen.
- Bots play with a visible "thinking" delay and animated moves, executed as real `TurnSession` commands so the replay is visible.
- The **"Highlight playable" hint** reuses the solver at Easy depth.
- Bots also take over disconnected online seats (§4.4).

### 3.6 Deck art & cosmetics (data-driven, ready for unlocks)
- `DeckSkin : ScriptableObject` contains `id`, `displayName`, `Sprite[52]` faces indexed by `(suit, rank)`, a `cardBack` default, a `blank` (or highlight) sprite, a `filterMode`/pixel-art flag, `pixelsPerUnit`, and an `UnlockRequirement` reference.
- **Per-skin mapping**: the editor tool **Mejnun ▸ Deck Skin Importer** takes a sliced texture and shows a 4×13 grid. You assign each slot by click, or apply a layout preset ("row-major", "interleaved zig-zag"). The result is saved into the SO.
  - `cards.png` (Classic) is already sliced into 54 sprites `cards_0..53` (64×97, point filter). Row 1 zig-zags clubs/hearts (2♣ 2♥ 3♥ 3♣ 4♣ 4♥ …) and ends with the card back. Row 2 zig-zags spades/diamonds the same way and ends with a blank card. The importer preset reproduces this, and the mapping gets checked visually in the importer grid.
- Other cosmetic SOs: `CardBack`, `TableFelt` (sprite/material), `Avatar`, `MejnunShout` (a Wwise Switch value plus a VFX prefab).
- `CosmeticCatalog` lists every item. `PlayerInventory` stores owned and equipped items (local save + UGS Cloud Save).
- Unlock sources: **play progression** (XP per game and win, levels), **achievements** (Steam / Game Center / Google Play via the platform layer, plus local ones), and **IAP** (Unity IAP, behind `IEntitlements`, not committed yet).
- Packaging: the base skin ships in the build. Extra skins become **Addressables** groups, so they can be downloaded or patched later without an app update.
- **Online**: each player's equipped *card back*, *avatar* and *shout* are sent at session join. Card **faces** come from the local player's own skin choice, since everyone sees their own preferred face art.

### 3.7 Audio (Wwise)
- `AudioService` is the only code that calls `AkSoundEngine` or `AK.Wwise.Event`. Gameplay raises `GameEvents`, and an `AudioDirector` maps them to Wwise events.
- First-pass event list: `Play_UI_Click`, `Play_Card_Pickup`, `Play_Card_Drop`, `Play_Card_Invalid`, `Play_Card_Shuffle`, `Play_Card_Deal`, `Play_Card_Draw`, `Play_Meld_Valid`, `Play_GoDown`, `Play_Turn_Start`, `Play_Timer_Tick`, `Play_Timer_Warning`, `Play_Mejnun_Shout` (Switch `Shout_Type` per cosmetic), `Play_Win_Stinger`, `Play_Lose_Stinger`, `Music_Menu`, `Music_Game`.
- RTPC `Timer_Tension` (0–1) and RTPC `Board_Complexity` (optional, for music intensity). States `Game_Phase` (Menu / Dealing / Playing / Results).
- Banks: `Init` and `Main` to start with. SoundBanks are **committed to git** (decided), so the game builds without Wwise Authoring installed.
- Platform plugins: the Mac/Windows integration is present. The **iOS and Android Wwise platform packages must be installed** through the Wwise Launcher before the mobile milestones (M8).

---

## 4. Online multiplayer

### 4.1 Stack (Unity Gaming Services)
- `com.unity.services.multiplayer` (Multiplayer Services SDK: Sessions = Lobby + Relay + Matchmaker), plus `com.unity.netcode.gameobjects`, plus `com.unity.services.authentication`, plus `com.unity.services.cloudsave`, plus (optionally) Cloud Code.
- **Host-authoritative**: the host runs `Mejnun.Core`. Clients send command RPCs, and the host validates them, applies them and broadcasts the resulting events and state deltas.
- **Hidden information**: each client only ever receives **its own hand** through targeted RPCs. The host process necessarily holds every hand, which is the accepted trade-off of the "UGS with player host" choice. Because Core is pure C#, moving authority to Cloud Code or a dedicated server later doesn't require touching game logic.
- Network payloads are compact: card ids as bytes, commands as small structs, and full `GameState` snapshots for join, reconnect and migration.

### 4.2 Finding games
- **Room codes**: Session join code, 6 characters.
- **Quick match**: Matchmaker or session query, filtered by player count and timer preset. Bots fill after a timeout *(see Q4)*.
- **Friends**: UGS Friends, plus platform friends (Steam invites and rich presence through the platform layer). Game Center and Google Play come later.

### 4.3 Accounts
- Anonymous sign-in on first launch, with a nickname.
- Optional linking to **Apple / Google Play Games / Steam / Unity Player Accounts**, which keeps unlocks and inventory across devices through Cloud Save.

### 4.4 Disconnects, pause/resume, host migration
- **A client drops**: their seat goes into the *disconnected* state, and their turns auto-time-out at the current timer, or 60 s if untimed. The **host gets a prompt** with three choices:
  1. **Wait / auto-pass** (the default while waiting).
  2. **Let AI take over** the seat. The bot difficulty is chosen by the host, and the player gets the seat back if they reconnect.
  3. **Save & pause**: the full snapshot is stored, everyone is returned to the lobby or menu, and the game can be resumed later from "Paused games".
- **Reconnect**: the player rejoins with the same auth id, gets a fresh snapshot (their own hand only) and takes the seat back.
- **The host drops**: a new host is needed. The plan is as follows:
  - After every turn, the host writes an **authoritative snapshot** (full state, including hands) to **UGS Cloud Code / Cloud Save private data** keyed by session id. Only server-side code can read it, so no other client can see hands.
  - The session or lobby **host migration** elects a new host, for example the next seat clockwise. The new host starts NGO, fetches the snapshot through Cloud Code, and the others reconnect. At most the in-progress turn is lost; that turn is replayed from its start.
  - The same snapshot store backs **Save & pause / resume later**.
  - ⚠ The first task of M7 is a **spike** to confirm how far Multiplayer Services host migration and Cloud Code snapshot storage support this flow. If they don't, the fallback is an "end game, save snapshot, the remaining players re-create the session from the snapshot" flow.

---

## 5. Platforms, builds & distribution

### 5.1 Platform layer (port from `../terra-firma/Assets/Scripts/Platform/`)
Port these and rename the namespace `TerraFirma.Platform` → `Mejnun.Platform`:
- Interfaces: `IPlatformServices`, `IAchievements`, `IStats`, `IRichPresence`, `IEntitlements`, `ISaveStorage`, `IControllerInfo` (the last can be trimmed).
- `PlatformServices` (bootstrap), `PlatformCallbackRunner`, `PlatformSettings` (App IDs and the achievement/stat ID table), `PlatformBuild`.
- `AchievementId`, `StatId`, `DlcId`.
- `Null/NullPlatformServices`, `Steam/SteamPlatformServices` (compiled under **`MEJNUN_STEAM`**, replacing `TF_STEAM`).
- `Storage/Es3SaveStorage` → replace it with `JsonFileSaveStorage`, since Easy Save isn't in this project. Steam Auto-Cloud covers that folder.
- Package: Steamworks.NET pinned by git URL `com.rlabrecque.steamworks.net#2025.165.0`, the same as terra-firma.
- New backends later: `AppleGameCenterServices` (iOS), `GooglePlayGamesServices` (Android).

### 5.2 Automated build / upload (port from terra-firma's Editor tooling)
- `SteamUploader`, `SteamUploadWindow`, `SteamUploadConfig` (+ asset), `SteamUploadLocalSettings` (stored in ignored `UserSettings/MejnunSteamUpload.json`), `SteamBuildVersion` (major/minor/revision bump, restored on a failed build), `SteamBuildGit` (commits ProjectSettings and adds an annotated `vX.Y.Z` tag).
- What carries over unchanged: ContentBuilder `content/<app>/<depot>/<os>/` layout, steamcmd cached-login check that opens Terminal for Steam Guard, VDF generation, an empty branch skips `SetLive`, the default branch is rejected, and `*_DoNotShip` folders are stripped.
- Menu: **Mejnun ▸ Steam ▸ Upload…**. It needs a new Steam App ID and depot IDs for Mejnun *(Q5)*.
- **Unity 6 Build Profiles**, one per store and edition:
  - `Steam Windows – Paid`, `Steam macOS – Paid` (`MEJNUN_STEAM;MEJNUN_PAID`)
  - `iOS – Paid`, `iOS – Free+Ads` (`MEJNUN_ADS`), `Android – Paid`, `Android – Free+Ads`
- Mobile upload automation is a later phase: either extend the window with **fastlane** lanes (TestFlight and the Play internal track) or use Unity Build Automation *(Q6)*.

### 5.3 Monetization
- **Paid edition**: no ads. On Steam this is the only edition.
- **Ad-supported edition**: interstitials between games (frequency-capped) and an optional rewarded ad (for example, double XP). It uses **Unity LevelPlay** (`com.unity.services.levelplay`) behind an `IAdsService` with a Null implementation for paid builds.
- IAP isn't committed yet. `IEntitlements` and the cosmetic `UnlockRequirement.Purchase` type keep the door open with no rework.

### 5.4 Orientation & devices
- Both orientations on phones and tablets. Layout reacts to aspect changes at runtime, with safe-area handling for notches.
- Minimum targets *(Q7)*: iOS 15+, Android 8 (API 26)+, Windows 10+, macOS 12+.

---

## 6. Persistence
- Local: settings, cosmetics, inventory, XP and stats, paused local games (snapshot JSON), plus a replay of the last game for debugging.
- Cloud: inventory and progression in UGS Cloud Save (linked accounts). Online paused games live in Cloud Code storage.
- A save schema version and migrations are in place from day one.

## 7. Localization
- `com.unity.localization` from the start. All UI strings and the dealer questions go into String Tables, and Smart Strings handle plurals ("3 cards left").
- Fonts need to cover the target scripts. The language list is still to be confirmed *(Q8)*.

## 8. Accessibility & settings
- Colour-blind-friendly suit tinting (four-colour deck option per skin), adjustable card size, a reduced-motion option, haptics on/off, left-handed hand placement, and audio buses (Master/Music/SFX/Voice) through Wwise RTPCs.

---

## 9. Milestones

| # | Milestone | Key deliverables | Exit criteria |
|---|---|---|---|
| **M0** | Project setup ✅ | git + LFS, Unity/Wwise `.gitignore`/`.gitattributes`, `Assets/_Project` layout, this plan | Clean first commit |
| **M1** | Rules engine | `Mejnun.Core` (cards, deck, RNG, `MeldValidator`, `GameState`, `TurnSession` with undo/revert/go-down/timeout, end conditions, snapshot serialization) | ≥ 95% line coverage in EditMode tests. Every rule in §1 has a named test |
| **M2** | Playable hot-seat prototype | Table scene, card prefab, `DeckSkin` Classic + importer, hand and board views, drag/drop + tap-tap, auto-layout, zoom/pan, End/Draw/Undo/Revert, handoff screen, dealer question flow | 2–6 people can finish a pass-and-play game on Mac and an Android/iOS device |
| **M3** | AI | Solver, Easy/Medium/Hard bots, hint highlight, solo setup screen | Hard beats Easy in more than 80% of 500 simulated games. Turn compute under 1 s on a mid-range phone |
| **M4** | UX & UI | UI Toolkit menus/HUD, portrait + landscape layouts, settings, timers, animations, results screen, MEJNUN moment | Usability pass on a small phone and a 4K PC |
| **M5** | Audio | Wwise events/banks, AudioService/Director, music states, timer tension | Every `GameEvent` with a sound mapped |
| **M6** | Cosmetics & progression | Catalog, inventory, XP/levels, achievements (local), unlock UI, 2nd test deck skin via Addressables | A second skin with a different sheet layout imports and plays |
| **M7** | Online | Spike (host migration and snapshot store), auth, sessions (codes and quick match), NGO host-authoritative sync, reconnect, AI takeover, save & pause, host migration, friends | Six-device soak test. Killing any client or the host recovers per §4.4 |
| **M8** | Platforms & monetization | Port the platform layer and Steam uploader, Build Profiles, Steam achievements and rich presence, iOS/Android Wwise plugins, LevelPlay ads edition, Game Center / Play Games | Steam beta-branch upload from the editor. TestFlight and Play internal builds |
| **M9** | Localization & polish | Languages, fonts, accessibility options, perf/battery pass, crash reporting (Unity Cloud Diagnostics) | Release-candidate checklist |

Suggested order: M1 → M2 → M3 → M4/M5 in parallel → M6 → M7 → M8 → M9. M7 can start after M2, since it only depends on Core and the views.

---

## 10. Testing strategy
- **Core (EditMode)**: a table-driven validator test for every set rule (aces, doubles, sizes, mixed suits); turn flows (go-down only from hand, go-down then board use in the same turn, End-turn gating, undo past go-down, revert, all four timeout outcomes, empty-pile pass and stalemate end, the winner deals next); and deterministic seeded deal tests.
- **AI**: solver correctness on curated puzzles, a fuzz test (random states, so every solver output must be a valid board), and difficulty benchmarks.
- **Net**: snapshot round-trip serialization, a command replay that matches the host state, and a hidden-hand leak test that asserts no client payload contains another player's cards.
- **PlayMode**: a scripted pass-and-play smoke game and orientation layout snapshots.
- CI (optional, later): Unity Test Runner in batch mode on push.

---

## 11. Open questions (please answer when convenient)
1. **First player**: does the player **left of the dealer** start? *(assumed yes)*
2. **Stalemate tie-break** when the pile is empty and two or more players share the fewest cards: shared win, lowest card-value total, or the player closest to the dealer's left?
3. **Hints online**: should "Highlight playable" be allowed in online games (per-lobby toggle), or only offline?
4. **Quick match bots**: should bots fill empty seats after N seconds, or must quick match always be all humans?
5. **Steam App ID / depots** for Mejnun (new Steamworks app needed). Is there a demo app?
6. **Mobile build automation**: fastlane from the editor window (like the Steam flow), or Unity Build Automation (cloud)?
7. **Minimum OS versions** — are the defaults in §5.4 OK?
8. **Launch languages** — you said "multiple". Which ones? (They affect font choices, e.g. Arabic/Persian need RTL support. The name Mejnun suggests Turkish/Persian/Arabic audiences.)
9. **Mobile editions**: two separate store apps (Paid and Free+Ads), or one free app with a "Remove ads" purchase? This changes whether IAP is needed at launch.
10. **Card faces for opponents' skins online**: is "everyone sees their own face skin" OK, or should a player's equipped skin show for the cards *they* play?
11. **Bots in pass-and-play**: allow mixing local humans and bots? *(planned yes)*
12. **Online timer default**: should online games force a timer (e.g. 90 s) so nobody can stall, while still letting the host pick a preset?
13. **Split gesture**: is long-press-to-split (plus drag-the-end) the right feel, or should there be an explicit "scissors" tool?
