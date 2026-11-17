# Mejnun — Game Design & Implementation Plan

Status: draft v2 (2026-11-16), written from two interview rounds with Brennan, plus answers to the v1 open questions (§11). **M1 (rules engine), M2 (hot-seat prototype) and M3 (AI) are complete** (§9).
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
- The **first turn** goes to the player left of the dealer (confirmed).

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
- Presets: **30 / 60 / 90 / 120 s** per turn, or Off. Off is only available offline, because online games are always timed (§4.5).
- On timeout:
  1. No hand card placed, board valid → **keep** the rearranged board and **draw**.
  2. No hand card placed, board invalid → revert, then draw.
  3. Hand card(s) placed, board valid → the turn ends normally (no draw).
  4. Hand card(s) placed, board invalid → revert, then draw.

### 1.10 Empty draw pile
- Once the pile is empty, "Draw" becomes **"Pass"**: the player draws nothing and the board is reverted.
- If every active player passes in a row without placing a card, the game ends:
  - If exactly one player has the **fewest cards in hand**, they win. This is a normal win screen. The "MEJNUN!" moment is reserved for actually emptying your hand.
  - If two or more players share the fewest cards, the game is declared a **Stalemate**: there is no winner, and no win XP or win stats are awarded.
  - After a Stalemate, the next game's dealer is chosen through the dealer-question prompt.

### 1.11 Hand helpers (UI, not rules)
- Sort by suit, sort by rank, drag to reorder manually.
- "Highlight playable" hint: a personal setting offline.
  - Online, it's a **per-lobby toggle** chosen by the host (default Off) and shown in the lobby rules summary. When it's Off, the hint is unavailable to every player in that game.
  - Quick match always uses hints Off (§4.2).

---

## 2. Modes

| Mode | Seats | Authority | Notes |
|---|---|---|---|
| **Solo vs AI** | 1 human + 1–5 bots | Local engine | Easy / Medium / Hard per bot |
| **Pass & play** | 2–6 seats, any mix of local humans and bots (at least 1 human) | Local engine | Handoff screen ("Pass to Sam — tap to reveal") between human turns. It's skipped when consecutive turns are bots |
| **Online live** | 2–6 (bots can fill seats) | Host-authoritative over UGS Relay | Room codes, quick match, friends invites. **Always timed** (§4.5) |

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
- `GameState` holds seats, hands, board (melds), draw pile, current seat, dealer, phase per player, turn number, timer, RNG seed, and `DeckFaces[2]`, the face-skin id for each physical deck (§3.6). Core treats skin ids as opaque strings.
- `TurnSession` holds the start-of-turn snapshot (`BoardSnapshot`: board plus hand) and a move list (`MoveCards`, `SplitMeld`, `MergeMelds`, `GoDown`). **Undo** drops the last move and replays the rest from the snapshot; **Revert** clears the list. It also provides `CanEndTurn()` and timeout resolution (§1.9). Straights are kept in canonical rank order after every move, while same-kind and invalid groups keep insertion order.
- Commands carry the player's **intent**. There is a single entry point, `MejnunGame.Apply(in GameCommand)` → `CommandResult { Ok, Error }`. The engine validates every command, and an illegal one returns a `RuleError` without changing state (it never throws for player mistakes). It is identical locally and online, where clients send commands and the host applies them, and the command list doubles as the replay log.
- A deterministic **PCG32** RNG (serialisable state) is seeded per game and drives the Fisher–Yates shuffle, the random dealer and the face pick, so games can be replayed for bug reports and tests. Core uses no `System.Random` or time APIs.
- `SnapshotSerializer` writes a versioned binary snapshot (card ids as bytes) of the full state, the in-progress turn and the RNG state. The same seed plus the same commands produce identical snapshot bytes.
- `GameEvents` are emitted as plain C# events (CardDealt, CardMoved, MeldBecameValid, WentDown, TurnEnded, Drew, Won, …). Presentation, audio and network all subscribe to them.

### 3.3 Presentation
- The **board and hand** are 2D world-space `SpriteRenderer` cards under an orthographic camera. That makes pinch-zoom and pan natural, keeps pixel-art cards crisp with point filtering and a per-zoom pixel snap, and lets cards tween cheaply.
- **Auto-arranged board**: sets flow into rows using a wrap layout that depends on the viewport aspect, which handles both orientations. Straights auto-sort their cards, while same-kind sets keep insertion order. Each set shows a valid/invalid outline. An "empty area" drop zone creates a new group.
- **Repositionable sets** (added in v2.1): players can drag a whole set to a new place on the board (§3.4) to group related sets or keep their working area together.
  - The position is a **view-only arrangement**: a `BoardArrangement` order of meld ids in `Mejnun.Game`, like `HandOrder`. It never creates an engine move, never touches `GameState`, and doesn't use an Undo step.
  - The wrap layout still decides rows and wrapping. It just lays sets out in arrangement order instead of creation order, so repositioning keeps working in both orientations and after aspect changes.
  - New sets are appended at the end of the arrangement. Sets that disappear (merged or emptied) are dropped from it, and the remaining sets keep their relative order through Undo and Revert.
- **Zoom & pan**: pinch or two-finger drag on touch; wheel, right/middle-drag or trackpad on PC. A "fit board" button and automatic fit after each change unless the user zoomed manually.
- **Hand**: a fanned or scrollable row at the bottom (landscape), or a two-row grid (portrait), with sort buttons.
- **HUD and menus** use **UI Toolkit** (recommended): USS responsive classes handle portrait/landscape and safe areas, it integrates cleanly with Unity Localization, and the "mixed" art direction (pixel cards with smooth modern UI) fits it well. uGUI is avoided apart from world-space bits if needed.
  - The deciding factor: UI Toolkit's **Advanced Text Generator** (HarfBuzz + ICU, opt-in in Unity 6.4) is the built-in way to shape **Arabic and Persian** right-to-left text. TextMeshPro would need a third-party RTL plugin (§7).

### 3.4 Input (touch-first; the mouse acts as touch)
- Input System with `EnhancedTouch`. One `PointerRouter` turns touch and mouse into the same tap, drag, long-press and pinch gestures.
- **Design rule: there are no modal tools** (no scissors, no "split mode"). Every action is a direct, discoverable gesture, and the same result can usually be reached in more than one way.

| Gesture | On a hand card | On a board card | On empty board |
|---|---|---|---|
| **Drag** | Drag up onto a set, a gap in a straight, or empty space to play it. Drag sideways within the hand to reorder | Move that one card. Pulling it out of the middle of a straight leaves two pieces, so splitting happens naturally | Pan (when zoomed in) |
| **Long-press, then drag** (~350 ms, haptic tick) | Pick up the card plus any other selected hand cards as a group | **Straight**: pick up the pressed card *and everything after it*, so dropping the tail elsewhere splits the run. **Same kind**, or the first card of a straight: pick up the whole set | none |
| **Tap** | Toggle selection (multi-select is allowed). Then tap a target to place every selected card, e.g. tap 3 hand cards, then tap empty space to go down | Select, then tap a target to move | Drop the selected card(s) here as a new group |
| **Double-tap** | Quick-place: put it on the first set it validly extends, if any *(proposed; online it counts as a hint, so it follows the lobby hint toggle)* | Send a card you placed this turn back to your hand | Fit the whole board to the screen |
| **Pinch / two-finger drag** | none | Zoom / pan | Zoom / pan |

- **Drag a set to reposition it**: each set has a grab area (its outline frame plus a small grip tab above the cards). Dragging from there picks up the **whole set as a unit** and shows an insertion marker between sets. Dropping it puts the set at that position in the board arrangement (§3.3).
  - Dropping it on empty board after the last set moves it to the end. Dropping it back where it started, or outside the board, cancels the move with no feedback.
  - It's purely visual, so it's allowed for every set: before going down, on sets that were on the board at the start of the turn, and on invalid sets.
  - In hot-seat, only the active player can rearrange the board. The arrangement is kept for the whole game, so every player sees the same table.
  - This doesn't replace long-press on a set. Long-press still moves the set's *cards* into another set or a new group, which is an engine move.
  - On PC, the same left-drag on the grab area works, and the cursor changes to a grab hand over it.
- **Pick-up feel, deck-builder style** (added in v2.2): any card you drag is physically *picked up* and travels with your finger.
  - On pickup the card lifts: it scales up slightly (about 1.15×), rises above every other card, gets a soft drop shadow, and tilts a little in the direction of motion. It follows the pointer with light smoothing, offset slightly above the finger so the finger never hides it. A `Play_Card_Pickup` sound and haptic tick play.
  - **Group pickup**: if the pressed hand card is part of a multi-selection, pressing and holding it picks up **every selected card**, and they travel as a small fanned stack under the finger. The cards left in the hand close the gaps. The same applies to the run tail or whole set that a long-press on a board set picks up.
  - **Reorganize the hand**: while the held card(s) are over the hand, the hand opens a gap at the insertion point (neighbours slide aside), and dropping inserts them there. A group keeps its relative order. Like sorting, this is a view-only `HandOrder` change, not an engine move and not an Undo step.
  - **Dropping**: on a legal target the cards tween into their slot. A cancel (dropping where they started, or outside any target) tweens them back to where they were picked up. Illegal drops spring back as described below.
  - The board drop preview (the target highlight) stays. The lifted card is just the presentation on top of it.
- A drop that's illegal (for example a board card onto the hand, or touching the board before going down) springs the card back with a haptic buzz and the `Play_Card_Invalid` sound. Temporarily *invalid sets* are always allowed and get a red outline.
- Haptics on mobile for pickup, long-press, drop and invalid.
- On PC: left-drag = drag, holding the left button = long-press, the wheel zooms, and right/middle-drag pans. Keyboard shortcuts are optional extras: Ctrl+Z undo, R revert, Space end turn, D draw.
- A gesture tutorial covers drag, long-press-split and pinch in the first solo game, with tooltips the first time each gesture is relevant.

### 3.5 AI (`Mejnun.AI`)
- One shared **solver** (`MeldSolver`) asks: given the board cards (B), a subset of hand cards (H) and the go-down constraint, what is a valid partition of B ∪ H′ that maximises |H′|?
  - *As built (M3)*: an exact, memoised dynamic programme over the values A(low), 2 … K, A(high), one suit at a time. Doubles are interchangeable for validity, so it works on (suit, rank) counts and tracks the open runs per suit (at most two) and the same-kind cards per rank.
  - The shared aces (low *or* high) are handled with a result vector per state, one entry per "aces kept for ace-high" combination (at most 81), instead of extra state. That keeps it exact and fast: the worst late-game turn (90+ board cards, all 8 aces) takes about 20 ms on .NET and about 60 ms in the editor.
  - A deterministic state budget (no clock) replaces the time budget. If it is ever exceeded, Hard falls back to Medium's local search.
  - The bot plans on a snapshot copy of the game on a worker thread (`Task.Run`), and every command is validated by the engine on that copy before it is returned.
- **Easy**: only plays new sets from hand plus simple extensions of existing sets. It occasionally "misses" plays on purpose.
- **Medium**: extensions, splits, and taking from 4-card same-kind sets, using greedy rearrangement with local search.
- **Hard**: a full board re-partition via the solver. Before going down it searches the hand's 3-card sets for the go-down that unlocks the biggest same-turn board play.
  - *Not built yet (tuning backlog)*: strategic hold-back (a voluntary draw is legal) and tracking which doubles have been seen. Hard already beats Easy 96% of the time without them.
- Bots play with a visible "thinking" delay and animated moves, executed as real `TurnSession` commands so the replay is visible.
- The **"Highlight playable" hint** reuses the solver at Easy depth.
- Bots also take over disconnected online seats (§4.4).

### 3.6 Deck art & cosmetics (data-driven, ready for unlocks)
- `DeckSkin : ScriptableObject` contains `id`, `displayName`, `Sprite[52]` faces indexed by `(suit, rank)`, a `cardBack` default, a `blank` (or highlight) sprite, a `filterMode`/pixel-art flag, `pixelsPerUnit`, and an `UnlockRequirement` reference.
- **Per-skin mapping**: the editor tool **Mejnun ▸ Deck Skin Importer** takes a sliced texture and shows a 4×13 grid. You assign each slot by click, or apply a layout preset ("row-major", "interleaved zig-zag"). The result is saved into the SO.
  - `cards.png` (Classic) is already sliced into 54 sprites `cards_0..53` (64×97, point filter). Row 1 zig-zags clubs/hearts (2♣ 2♥ 3♥ 3♣ 4♣ 4♥ …) and ends with the card back. Row 2 zig-zags spades/diamonds the same way and ends with a blank card. The importer preset reproduces this, and the mapping gets checked visually in the importer grid.
  - `ClassicIvory/cards_ivory.png` is the **second base face** (added in v2). It's a palette swap of Classic: ivory paper, navy ink, crimson red. It uses the identical slicing (`cards_ivory_0..53`), so the same layout preset applies, and it's a placeholder until the real second-deck art replaces it in place.
- **Two faces per game** (the "two real decks" rule):
  - Every game uses **exactly two different face skins**, one per physical deck, just like shuffling two real decks together. `DeckFaces[0]` is used for cards with `Deck == 0` and `DeckFaces[1]` for `Deck == 1`. A game never shows one face for both decks.
  - **Pool** = the union of face skins owned by every *human* in the game. Two **free base faces** (Classic and Classic Ivory) are owned by everyone, so the pool always has at least 2.
    - In solo, each bot also adds one random face from the full catalogue, so solo players can get a peek at locked faces too *(proposed, see Q-E)*.
  - The two faces are picked **uniformly at random, without repetition**, with the game's seeded RNG at deal time. They're stored in `GameState` and snapshots, so all clients, reconnects and replays agree.
  - A face the local player **doesn't own** gets a small "Not unlocked yet" badge on the results screen, linking to how to unlock it. That's the "exciting glimpse" moment.
  - A nice side effect: the two cards of a **double now look different**, which makes the board easier to read.
  - Every client must be able to render every face. Face skins are small (around 15 KB each), so all faces **ship in the build**. Addressables remote download stays an option if the catalogue grows. If a face fails to load, the client falls back to the base face for that deck index.
  - "Equipping" a face skin therefore isn't a thing. Owning one adds it to the pools of the games you're in. (Possible later extra: a "favourites" weighting.)
- Other cosmetic SOs: `CardBack`, `TableFelt` (sprite/material), `Avatar`, `MejnunShout` (a Wwise Switch value plus a VFX prefab).
- `CosmeticCatalog` lists every item. `PlayerInventory` stores owned and equipped items (local save + UGS Cloud Save).
- Unlock sources: **play progression** (XP per game and win, levels), **achievements** (Steam / Game Center / Google Play via the platform layer, plus local ones), and **IAP** (Unity IAP, behind `IEntitlements`). Mobile ships with Remove ads (§5.3), and cosmetic packs aren't committed yet.
- Packaging: the two base faces ship in the build. Other cosmetics can become **Addressables** groups, so they can be downloaded or patched later without an app update.
- **Online**: each player's owned face ids plus their equipped *card back*, *avatar* and *shout* are sent at session join. The host builds the face pool (above).
  - The draw pile uses the viewing player's equipped back. Opponents' hand stacks show that opponent's back.
  - A back is never tied to a deck index, so a back never reveals which deck the next drawn card comes from.

### 3.7 Audio (Wwise)
- `AudioService` is the only code that calls `AkSoundEngine` or `AK.Wwise.Event`. Gameplay raises `GameEvents`, and an `AudioDirector` maps them to Wwise events.
- First-pass event list: `Play_UI_Click`, `Play_Card_Pickup`, `Play_Card_Drop`, `Play_Card_Invalid`, `Play_Card_Shuffle`, `Play_Card_Deal`, `Play_Card_Draw`, `Play_Meld_Valid`, `Play_GoDown`, `Play_Turn_Start`, `Play_Timer_Tick`, `Play_Timer_Warning`, `Play_Mejnun_Shout` (Switch `Shout_Type` per cosmetic), `Play_Win_Stinger`, `Play_Lose_Stinger`, `Music_Menu`, `Music_Game`.
- RTPC `Timer_Tension` (0–1) and RTPC `Board_Complexity` (optional, for music intensity). States `Game_Phase` (Menu / Dealing / Playing / Results).
- Banks: `Init` and `Main` to start with. SoundBanks are **committed to git** (decided), so the game builds without Wwise Authoring installed.
- Platform plugins: the Mac/Windows integration is committed. **iOS and Android** integration files now appear in the working tree, so it looks like they were installed through the Wwise Launcher. They still need to be reviewed and committed, and the iOS/Android SoundBank platforms added in the Wwise project, before M8.

---

## 4. Online multiplayer

### 4.1 Stack (Unity Gaming Services)
- `com.unity.services.multiplayer` (Multiplayer Services SDK: Sessions = Lobby + Relay + Matchmaker), plus `com.unity.netcode.gameobjects`, plus `com.unity.services.authentication`, plus `com.unity.services.cloudsave`, plus (optionally) Cloud Code.
- **Host-authoritative**: the host runs `Mejnun.Core`. Clients send command RPCs, and the host validates them, applies them and broadcasts the resulting events and state deltas.
- **Hidden information**: each client only ever receives **its own hand** through targeted RPCs. The host process necessarily holds every hand, which is the accepted trade-off of the "UGS with player host" choice. Because Core is pure C#, moving authority to Cloud Code or a dedicated server later doesn't require touching game logic.
- Network payloads are compact: card ids as bytes, commands as small structs, and full `GameState` snapshots for join, reconnect and migration.

### 4.2 Finding games
- **Room codes**: Session join code, 6 characters.
- **Quick match**: Matchmaker or session query.
  - The only filter is player count (2–6, or "Any"). The rules are fixed so the player pool doesn't fragment: 90 s timer, hints Off.
  - **Bot fill is a player setting** (Settings ▸ Online ▸ *Fill empty quick-match seats with bots*): **Off (humans only)**, After 30 s, or After 60 s. The default is After 60 s.
  - When the wait expires, empty seats are filled with Medium bots **only if every human in the forming session allows bots**. The longest wait chosen applies. Otherwise the game keeps waiting for humans, and it can start early once at least 2 humans are in and everyone taps Ready.
  - Bots are always clearly labelled as bots.
- **Friends**: UGS Friends, plus platform friends (Steam invites and rich presence through the platform layer). Game Center and Google Play come later.

### 4.3 Accounts
- Anonymous sign-in on first launch, with a nickname.
- Optional linking to **Apple / Google Play Games / Steam / Unity Player Accounts**, which keeps unlocks and inventory across devices through Cloud Save.

### 4.4 Disconnects, pause/resume, host migration
- **A client drops**: their seat goes into the *disconnected* state, and their turns auto-time-out at the lobby's turn timer (online is always timed, §4.5). The **host gets a prompt** with three choices:
  1. **Wait / auto-pass** (the default while waiting).
  2. **Let AI take over** the seat. The bot difficulty is chosen by the host, and the player gets the seat back if they reconnect.
  3. **Save & pause**: the full snapshot is stored, everyone is returned to the lobby or menu, and the game can be resumed later from "Paused games".
- **Reconnect**: the player rejoins with the same auth id, gets a fresh snapshot (their own hand only) and takes the seat back.
- **The host drops**: a new host is needed. The plan is as follows:
  - After every turn, the host writes an **authoritative snapshot** (full state, including hands) to **UGS Cloud Code / Cloud Save private data** keyed by session id. Only server-side code can read it, so no other client can see hands.
  - The session or lobby **host migration** elects a new host, for example the next seat clockwise. The new host starts NGO, fetches the snapshot through Cloud Code, and the others reconnect. At most the in-progress turn is lost; that turn is replayed from its start.
  - The same snapshot store backs **Save & pause / resume later**.
  - ⚠ The first task of M7 is a **spike** to confirm how far Multiplayer Services host migration and Cloud Code snapshot storage support this flow. If they don't, the fallback is an "end game, save snapshot, the remaining players re-create the session from the snapshot" flow.

### 4.5 Online rules & timer
- Online games are **always timed**, so nobody can stall a live game. The host picks **30 / 60 / 90 / 120 s**, and the default is **90 s**. "Off" is only available offline.
- Lobby rule toggles (host only, shown to everyone before Ready): timer preset, hints (Off by default), bot difficulty for filled seats, and the dealer-question pack.

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
- Menu: **Mejnun ▸ Steam ▸ Upload…**.
  - There's no Steam app yet (§5.5). Until then, development uses Valve's test App ID **480** (Spacewar) in `steam_appid.txt`, and the uploader window stays disabled until real App and depot IDs are entered in `SteamUploadConfig`.
- **Unity 6 Build Profiles**, one per store:
  - `Steam Windows` and `Steam macOS`: `MEJNUN_STEAM`, paid, no ads, no IAP.
  - `iOS` and `Android`: `MEJNUN_MOBILE`. Free, with ads and a "Remove ads" purchase (§5.3).
  - `Dev` (any platform): Null platform services, debug menu, no ads.

#### Mobile build & upload automation (recommended: local fastlane, driven from the editor)
You asked for "as automated as possible", and you develop on a Mac. The recommendation is to mirror the Steam flow: **one editor window, one click**.
- **Mejnun ▸ Mobile ▸ Build & Upload…** shares the version-bump and git-tag code with the Steam window. One version number is used everywhere, and the iOS build number and Android `versionCode` auto-increment.
- **Android**: Unity builds a signed `.aab`, with the upload keystore path and passwords kept in ignored `UserSettings/`. Then `fastlane supply` uploads it to the **Play internal testing** track, using a Google Cloud service-account JSON key.
- **iOS**: Unity exports the Xcode project. `fastlane gym` archives and signs it, and `fastlane pilot` uploads to **TestFlight**. Signing is handled by `fastlane match`, with certificates in a private git repo. An App Store Connect **API key (.p8)** is used instead of a password, so there are no 2FA prompts.
- `fastlane deliver` and `supply` can also push the **localized store listings and screenshots** from `Store/metadata/` in the repo, which covers all 11 languages without clicking through the store dashboards.
- Why not Unity Build Automation (cloud) first: it's billed per build-minute, and it needs the same certificates and keys anyway. It mainly helps teams without a Mac. It, or GitHub Actions with GameCI, can be added later on top of the same fastlane lanes.
- I'll set this up with you step by step in M8. The one-time account work is in §5.5, and it should start earlier because store verification takes days to weeks.

### 5.3 Monetization (decided)
- **Steam (desktop)**: a **paid** app. No ads and no IAP at launch.
- **Mobile (iOS & Android)**: **one free app** per store, with ads and a non-consumable **"Remove ads"** IAP.
  - Ads are **Unity LevelPlay** (`com.unity.services.levelplay`) behind `IAdsService`, with a Null implementation on desktop and dev builds. There are interstitials between games (frequency-capped, never mid-game, never in the first sessions) and optional rewarded ads (for example, double XP).
  - The **Remove ads** purchase uses **Unity IAP** (`com.unity.purchasing`) behind `IEntitlements` (`EntitlementId.RemoveAds`). It disables interstitials. Rewarded ads stay available because they're opt-in. A **Restore purchases** button is required by Apple.
  - So **IAP is needed at launch** on mobile. Cosmetic IAP packs can reuse the same plumbing later (`UnlockRequirement.Purchase`), and nothing is committed beyond Remove ads.
- **Privacy & consent** for ads: the iOS App Tracking Transparency prompt, a GDPR/UK consent dialog for EEA/UK users, the Apple privacy manifest and the Google Play Data Safety form. Ads must also stay off for users who declare they're under 13, or the app avoids targeting children.
- Remove ads is per store: buying it on iOS doesn't remove ads on Android *(see Q-F)*.

### 5.4 Orientation & devices
- Both orientations on phones and tablets. Layout reacts to aspect changes at runtime, with safe-area handling for notches.
- Minimum targets (confirmed): iOS 15+, Android 8 (API 26)+, Windows 10+, macOS 12+.

### 5.5 Store accounts & one-time setup checklist
Nothing exists yet. This is the order to do it in, ideally before M7, so review lead times don't block M8.

| Store | Steps | Cost |
|---|---|---|
| **Steam** | Steamworks partner account (tax and bank info) → pay the Steam Direct fee → you receive the **App ID**. Create depots for Windows and macOS, plus a `beta` branch. Then add the achievements and rich-presence tokens. A demo app can be added later as a separate App ID | US$100 per app (recoupable) |
| **Apple** | Apple Developer Program (individual or organisation; organisations need a D-U-N-S number) → bundle id `com.<you>.mejnun` → app record in App Store Connect → an App Store Connect API key for fastlane → a private git repo for `fastlane match` | US$99 per year |
| **Google** | Play Console developer account (identity verification) → create the app → upload the first `.aab` **manually** once (the API can't do the very first upload) → service account with release permissions for fastlane. New *personal* accounts must run a closed test with a minimum number of testers for 14 days before production, so plan for that | US$25 once |
| **Unity** | A UGS project linked to this Unity project: Authentication, Multiplayer Services, Cloud Save/Code, LevelPlay (ad network accounts) and IAP | Free tiers to start |

---

## 6. Persistence
- Local: settings, cosmetics, inventory, XP and stats, paused local games (`SnapshotSerializer` binary snapshots), plus a replay of the last game (seed + command list) for debugging.
- Cloud: inventory and progression in UGS Cloud Save (linked accounts). Online paused games live in Cloud Code storage.
- A save schema version and migrations are in place from day one.

## 7. Localization
- `com.unity.localization` from the start. All UI strings and the dealer questions go into String Tables, and Smart Strings handle plurals ("3 cards left"). Arabic has six plural forms, and Smart Strings' plural rules cover that.
- **Launch languages (11)**: English, Arabic, Persian, Armenian, Spanish, German, Italian, French, Japanese, Chinese, Portuguese.
  - Locale codes are `en`, `ar`, `fa`, `hy`, `es`, `de`, `it`, `fr`, `ja`, `zh-Hans` and `pt-BR`. Simplified Chinese and Brazilian Portuguese are assumptions *(see Q-C)*.
  - The game follows the device language, with a picker in Settings.
- **Fonts** (dynamic font assets with a fallback chain, all OFL-licensed):
  - Noto Sans for Latin text (accents for es/de/it/fr/pt).
  - **Vazirmatn** or Noto Sans Arabic for `ar` and `fa`. Both cover the Persian letters پ چ ژ گ and the Persian ی/ک forms.
  - Noto Sans Armenian.
  - Noto Sans JP and Noto Sans SC.
  - CJK fonts are several MB each, so the CJK/Arabic font files go in per-locale Addressable groups. Player nicknames can contain any script and fall back through the whole chain.
- **Right-to-left** (`ar`, `fa`):
  - Enable the Advanced Text Generator (`-unity-text-generator: advanced` on the UI root).
  - A `.rtl` class on the root mirrors the HUD and menus (row-reverse, mirrored icons such as back arrows).
  - The **cards and board stay left-to-right**, so straights read A→K the same for everyone at an online table *(see Q-D)*.
  - Numbers in UI text (timer, card counts) use locale digits (Eastern Arabic in `ar`, Persian in `fa`). Card art is unaffected.
- **Dealer questions** are written per language (culturally adapted), not translated literally. Each language ships with at least 20.
- Workflow: Unity Localization's **Google Sheets** extension (or CSV) round-trips the string tables, with machine translation first and a native-speaker review before release. The small amount of text keeps this cheap. Store listings are localized from the same sheet and pushed by fastlane.

## 8. Accessibility & settings
- Colour-blind-friendly suit tinting (four-colour deck option per skin), adjustable card size, a reduced-motion option, haptics on/off, left-handed hand placement, and audio buses (Master/Music/SFX/Voice) through Wwise RTPCs.

---

## 9. Milestones

| # | Milestone | Key deliverables | Exit criteria |
|---|---|---|---|
| **M0** | Project setup ✅ | git + LFS, Unity/Wwise `.gitignore`/`.gitattributes`, `Assets/_Project` layout, this plan | Clean first commit |
| **M1** | Rules engine ✅ | `Mejnun.Core` (cards, deck, RNG, `MeldValidator`, `GameState`, `TurnSession` with undo/revert/go-down/timeout, end conditions, snapshot serialization) | ≥ 95% line coverage in EditMode tests. Every rule in §1 has a named test. *Met: 123 EditMode tests pass, 95.3% line coverage* |
| **M2** | Playable hot-seat prototype ✅ | Table scene, card prefab, `DeckSkin` Classic + Classic Ivory + importer, the two-faces-per-game assignment, hand and board views, the full §3.4 gesture set, auto-layout, zoom/pan, End/Draw/Undo/Revert, handoff screen, dealer question flow | 2–6 people can finish a pass-and-play game on Mac and an Android/iOS device. *Met: hot-seat playtest passed (Brennan, 2026-11-17); 124 EditMode and 19 PlayMode tests pass. Closed* |
| **M3** | AI ✅ | Solver, Easy/Medium/Hard bots, hint highlight, solo setup screen | Hard beats Easy in more than 80% of 500 simulated games. Turn compute under 1 s on a mid-range phone. *Met in the editor: Hard beat Easy 481/500 (96.2%), the slowest Hard turn was 61 ms, and 158 EditMode and 22 PlayMode tests pass. The on-device phone timing is still to be measured* |
| **M4** | UX & UI | UI Toolkit menus/HUD, portrait + landscape layouts, settings, timers, animations, results screen, MEJNUN moment, drag-to-reposition sets (§3.3, §3.4), deck-builder pick-up and drag: cards follow the finger, selected cards are picked up as a group, the hand opens a gap so held cards can be reorganized (§3.4) | Usability pass on a small phone and a 4K PC |
| **M5** | Audio | Wwise events/banks, AudioService/Director, music states, timer tension | Every `GameEvent` with a sound mapped |
| **M6** | Cosmetics & progression | Catalog, inventory, XP/levels, achievements (local), unlock UI, 2nd test deck skin via Addressables | A second skin with a different sheet layout imports and plays |
| **M7** | Online | Spike (host migration and snapshot store), auth, sessions (codes and quick match), NGO host-authoritative sync, reconnect, AI takeover, save & pause, host migration, friends | Six-device soak test. Killing any client or the host recovers per §4.4 |
| **M8** | Platforms & monetization | Port the platform layer and Steam uploader, Build Profiles, Steam achievements and rich presence, iOS/Android Wwise plugins, LevelPlay ads + consent, Unity IAP Remove ads + restore, mobile Build & Upload window + fastlane lanes, Game Center / Play Games | Steam beta-branch upload from the editor. One-click TestFlight and Play internal builds |
| **M9** | Localization & polish | The 11 languages, the font chain, RTL mirroring, localized store listings, accessibility options, perf/battery pass, crash reporting (Unity Cloud Diagnostics) | Release-candidate checklist. Native-speaker review for `ar` and `fa` RTL screens |

Suggested order: M1 → M2 → M3 → M4/M5 in parallel → M6 → M7 → M8 → M9. M7 can start after M2, since it only depends on Core and the views.
Start the §5.5 store accounts during M3–M4, and enable the Advanced Text Generator plus one RTL test locale (`ar`) in M4, so right-to-left layout issues show up early rather than in M9.

---

## 10. Testing strategy
- **Core (EditMode)**: a table-driven validator test for every set rule (aces, doubles, sizes, mixed suits); turn flows (go-down only from hand, go-down then board use in the same turn, End-turn gating, undo past go-down, revert, all four timeout outcomes, empty-pile pass, a sole-fewest win versus a tied **Stalemate**, the fewest-cards winner deals next, a new dealer prompt after a Stalemate); and deterministic seeded deal tests.
- **Faces**: the two picked faces are always distinct, always come from the pool, are deterministic for a given seed, fall back to the base faces when the pool is too small, and both deck indexes render with their assigned skin.
- **AI**: solver correctness on curated puzzles, a fuzz test (random states, so every solver output must be a valid board), and difficulty benchmarks.
- **Net**: snapshot round-trip serialization, a command replay that matches the host state, and a hidden-hand leak test that asserts no client payload contains another player's cards.
- **PlayMode**: a scripted pass-and-play smoke game and orientation layout snapshots.
- **Board arrangement**: repositioning a set changes only the layout order (no command, unchanged `GameState`, unchanged undo stack). New sets are appended, removed sets are dropped, and the order survives Undo, Revert and an aspect change.
- **Pick-up and drag**: a held card follows the pointer and returns to its slot on cancel. Holding a selected card lifts every selected card. Dropping a held group into the hand reorders it as a block (relative order kept, no command, unchanged undo stack).
- CI (optional, later): Unity Test Runner in batch mode on push.

---

## 11. Decisions log & open questions

### 11.1 Resolved in v2 (answers to the v1 questions)
| # | Topic | Decision | Where |
|---|---|---|---|
| 1 | First player | Left of the dealer | §1.4 |
| 2 | Tie on fewest cards when the pile is empty | **Stalemate** (no winner) | §1.10 |
| 3 | Hints online | Per-lobby toggle (default Off; quick match Off) | §1.11, §4.5 |
| 4 | Quick-match bots | Player setting: Off / 30 s / 60 s | §4.2 |
| 5 | Steam | No Steam app yet. Use dev App ID 480 until it's created; demo later | §5.2, §5.5 |
| 6 | Mobile automation | Local fastlane driven from an editor window (recommended), set up together in M8 | §5.2 |
| 7 | Minimum OS | iOS 15, Android 8, Windows 10, macOS 12 | §5.4 |
| 8 | Languages | en, ar, fa, hy, es, de, it, fr, ja, zh, pt | §7 |
| 9 | Mobile editions | One free app with a "Remove ads" IAP; Steam is paid | §5.3 |
| 10 | Card faces | Two distinct faces per game, randomly chosen from the faces the players own | §3.6 |
| 11 | Bots in pass-and-play | Yes, mixed freely | §2 |
| 12 | Online timer | Always on online; host picks a preset, default 90 s | §4.5 |
| 13 | Split | Gestures only, no tools; long-press picks up the tail of a straight | §3.4 |
| 14 | Dragging feel | Deck-builder style: held cards lift and follow the finger; holding a selected card picks up the whole selection; the hand opens a gap so held cards can be reorganized | §3.4, M4 |
| Q-B | After a stalemate | A tie is a Stalemate and prompts for a new dealer; a sole player with the fewest cards wins normally and deals next | §1.10 |

### 11.2 Still open (new, smaller; the defaults in the text apply until answered)
- **Q-A Classic mapping**: confirm the zig-zag sprite order in the importer once it exists (unchanged from v1).
- **Q-C Language variants**: Simplified or Traditional Chinese (or both)? Brazilian or European Portuguese? One neutral Spanish, or Spain and Latin America?
- **Q-D RTL table**: should the board and hand also mirror for Arabic/Persian players, or stay left-to-right as proposed?
- **Q-E Faces in solo**: should bots contribute random catalogue faces to the pool (default), or should solo use only the player's own faces?
- **Q-F Remove ads across devices**: honour a Remove ads purchase from the other mobile store via a linked account (Cloud Save), or keep it per store (default)?
- **Q-G Double-tap quick-place**: keep it as proposed in §3.4, or leave it out?
- **Q-H Prices**: the Steam price and the Remove ads price tier.
- **Q-I Board arrangement online**: should a set's position be shared with everyone at the table (it would then be sent as a small view-only message), or be a local preference per client (default)?
