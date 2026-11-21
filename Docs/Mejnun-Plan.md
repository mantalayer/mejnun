# Mejnun — Game Design & Implementation Plan

Status: draft v2.5 (2026-11-19), updated after closing M7 and beginning the M8 platform foundation. **M1 (rules engine), M2 (hot-seat prototype), M3 (AI), M4 (UX & UI), M5 (Audio), M6 (Cosmetics & progression) and M7 (Online) are complete** (§9); M8 (Platforms & monetization) is in progress: the account-independent platform foundation is done and store-account work is pending.
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
- **2–6 players** in every mode. Seats are arranged clockwise; play passes to the next seat clockwise ("left").

### 1.3 Choosing who goes first
- There is no player dealer: the **computer always deals**. The only choice is who takes the first turn.
- If the previous game in this session/lobby had a winner (by emptying their hand or by fewest cards), **that winner goes first**.
- On the first game, after a Stalemate, or if the previous winner is no longer seated (e.g. they left the lobby or the seat count changed), the game shows a random **"embarrassing question"** prompt ("Who showers the least?"). Players then pick who goes first:
  - Local / pass-and-play: someone taps the chosen player's seat.
  - Online: everyone votes. The most votes wins, ties are broken at random, and the vote times out after 20 s, falling back to random.
  - Vs AI: the human taps a seat (bots included), or taps "Random".
- Questions live in a localized data table (`DealerQuestions`), so more can be added or changed per language.

### 1.4 Deal
- The computer deals cards one at a time, starting with the first player and going clockwise, until everyone has **11 cards**.
- The remaining cards (104 − 11·N, so 82 to 38) form the face-down **draw pile** in the centre.
- The **first turn** goes to the player chosen in §1.3.
- Engine note: the engine still stores a hidden `Dealer` seat, defined as the seat just before the first player; it has no player-facing meaning.

### 1.5 Objective
- The first player to get rid of every card in their hand wins, and the game ends at once. The win moment ("MEJNUN!") plays **automatically**.
- **Single round**: there is no scoring across rounds. The winner goes first next game.

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
- Engine rule: the **go-down commit** checks that each committed set is valid and made only of cards that were in the player's hand at the start of the turn. The engine commits automatically the moment the first valid hand-only set is complete.
- Cards committed in the go-down **stay on the board for the rest of the turn**: they may be rearranged on the board, but cannot go back to the hand (`GoDownCardToHand`). Otherwise a player could go down, take the set back, and keep full board access for free.
- Going down can still be undone: **Undo** past the commit point or **Revert** returns the committed cards to the hand and the player to Not down. Hand cards placed *after* going down can still return to the hand as usual.

**Down**
- The player may rearrange anything on the board: split straights, take a card from a 4-card same-kind set, merge, move single cards, add cards from hand, and build temporary invalid groups.
- To end the turn normally they must have placed **at least one card from hand**, and the board must be **valid**.

### 1.8 Turn actions
- **Draw / Pass**: always allowed, whether the player is down or not. It **reverts the board** to the start of the turn, returns every placed card to the hand, draws one card, and **ends the turn**.
- **End turn**: only enabled when the board is valid **and** at least one hand card was placed (or a go-down happened).
- **Undo**: steps back one move at a time within the turn.
- **Redo** (M4): re-applies the last undone gesture group within the same turn. The redo stack is cleared by any new move, Fix board, Revert, Draw/Pass, End turn or a timeout, and never crosses turns.
- **Confirmations** (M4): destructive or turn-ending actions ask first with a small in-HUD dialog (Confirm / Cancel, Enter/Esc on PC): **Revert** when moves were made; **Draw/Pass** when cards were placed this turn (it throws them back); **Fix board** when it will return hand cards; **Leave/quit a game in progress**; and **New game / restart**. End turn needs no confirmation since it's only enabled for a valid board. Each prompt has a "Don't ask again" setting.
- **Fix board**: repairs an invalid board while keeping valid placements from the player's hand whenever they still fit. Original board cards are preserved; invalid or unused hand cards are returned to the hand. It is one undo group, and falls back to Revert if no repair can be found within the solver budget.
- **Revert**: restores the board exactly as it was at the start of the turn and returns every card the player placed to their hand. It always works and is distinct from Fix board.
- A card that came from your hand can go back to your hand during the same turn, except cards you went down with this turn (§1.7). Cards that **started the turn on the board** can never go into a hand.
- **Fix board and hints**: the full repair above (which searches for the arrangement that keeps the most hand cards) is a hint, so it only runs while hints are on. With hints off, Fix board only returns hand cards sitting in invalid sets to the hand; if the board is still invalid after that, it falls back to Revert. Go-down cards are never returned by Fix board; the full repair re-homes them on the board instead.

### 1.9 Timed mode (optional, chosen by the host)
- Presets: **30 / 60 / 90 / 120 s** per turn, or Off. Off is only available offline, because online games are always timed (§4.5).
- On timeout, show a brief **"Halas!"** (Arabic for "stop/finished") notification, not a blocking screen overlay.
- On timeout:
  1. No hand card placed, board valid → keep the valid rearrangement, **draw**, and end the turn.
  2. No hand card placed, board invalid → restore the start-of-turn board, **draw**, and end the turn.
  3. Hand card(s) placed, board valid → end the turn normally, with **no draw**.
  4. Hand card(s) placed, board invalid → undo the turn's moves one at a time (animated) until the board is valid. This never uses Fix board's solver, so waiting out the timer is never a free hint. If at least one placed hand card remains on the valid board, end without a draw; otherwise draw (cases 1–2).

### 1.10 Empty draw pile
- Once the pile is empty, "Draw" becomes **"Pass"**: the player draws nothing and the board is reverted.
- If every active player passes in a row without placing a card, the game ends:
  - If exactly one player has the **fewest cards in hand**, they win. This is a normal win screen. The "MEJNUN!" moment is reserved for actually emptying your hand.
  - If two or more players share the fewest cards, the game is declared a **Stalemate**: there is no winner, and no win XP or win stats are awarded.
  - After a Stalemate, who goes first next game is chosen through the "Who goes first?" question prompt.

### 1.11 Hand helpers (UI, not rules)
- Sort by suit, sort by rank, drag to reorder manually.
- "Highlight playable" hint: a personal setting offline.
  - Online, it's a **per-lobby toggle** chosen by the host (default Off) and shown in the lobby rules summary. When it's Off, the hint is unavailable to every player in that game.
  - Quick match always uses hints Off (§4.2).
  - Hints use an **outline, not a tint**, and may jiggle the playable cards. When enabled in the hint settings, they can also outline board cards that can be moved to free a hand play.

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
- `GameState` holds seats, hands, board (melds), draw pile, current seat, dealer, phase per player, turn number, timer, RNG seed, `DeckFaces[2]` (the face-skin id for each physical deck) and `TableFeltId` (§3.6). Core treats cosmetic ids as opaque strings.
- `GameSetup` carries `DeckPicks[2]` (face ids, with `null` meaning Random) and the `FeltPool` of human felt preferences used for the seeded assignment.
- `TurnSession` holds the start-of-turn snapshot (`BoardSnapshot`: board plus hand) and a move list (`MoveCards`, `SplitMeld`, `MergeMelds`, `GoDown`). **Undo** drops the last move and replays the rest from the snapshot; **Revert** clears the list. It also provides `CanEndTurn()` and timeout resolution (§1.9). Straights are kept in canonical rank order after every move, while same-kind and invalid groups keep insertion order.
- Commands carry the player's **intent**. There is a single entry point, `MejnunGame.Apply(in GameCommand)` → `CommandResult { Ok, Error }`. The engine validates every command, and an illegal one returns a `RuleError` without changing state (it never throws for player mistakes). It is identical locally and online, where clients send commands and the host applies them, and the command list doubles as the replay log.
- A deterministic **PCG32** RNG (serialisable state) is seeded per game and drives the Fisher–Yates shuffle, the random dealer, deck-face assignment and felt selection, so games can be replayed for bug reports and tests. Core uses no `System.Random` or time APIs.
- `SnapshotSerializer` writes a versioned binary snapshot (card ids as bytes) of the full state, including `TableFeltId`, the in-progress turn and the RNG state. Bump the snapshot schema version and add a migration when this field is introduced. The same seed plus the same commands produce identical snapshot bytes.
- `GameEvents` are emitted as plain C# events (CardDealt, CardMoved, MeldBecameValid, WentDown, TurnEnded, Drew, Won, …). Presentation, audio and network all subscribe to them.

### 3.3 Presentation
- The **board and hand** are 2D world-space `SpriteRenderer` cards under an orthographic camera. That makes pinch-zoom and pan natural, keeps pixel-art cards crisp with point filtering and a per-zoom pixel snap, and lets cards tween cheaply.
- **Auto-arranged board**: sets flow into rows using a wrap layout that depends on the viewport aspect, which handles both orientations. Straights auto-sort their cards, while same-kind sets keep insertion order. Each set shows a valid/invalid outline. An "empty area" drop zone creates a new group.
- **Repositionable sets** (added in v2.1): players can drag a whole set to a new place on the board (§3.4) to group related sets or keep their working area together.
  - The position is a **view-only arrangement**: a `BoardArrangement` order of meld ids in `Mejnun.Game`, like `HandOrder`. It never creates an engine move, never touches `GameState`, and doesn't use an Undo step.
  - The wrap layout still decides rows and wrapping. It just lays sets out in arrangement order instead of creation order, so repositioning keeps working in both orientations and after aspect changes.
  - New sets are appended at the end of the arrangement. Sets that disappear (merged or emptied) are dropped from it, and the remaining sets keep their relative order through Undo and Revert.
- **Zoom & pan**: pinch or two-finger drag on touch; wheel, right/middle-drag or trackpad on PC. A "fit board" button and automatic fit after each change unless the user zoomed manually. The hand is a camera-independent screen layer: panning the board must not move the hand, and zooming the board must not change hand-card size or spacing.
- **Responsive board and hand**: detect the current screen aspect at runtime and use the available board width before adding rows. Keep a bottom hand strip reserved when fitting the board; use a fanned or scrollable row in landscape and a two-row grid in portrait, with sort buttons.
- **Opponent seats**: show physical card stacks along the screen edges, with one card back per card in each opponent's hand, so hand counts are visible without reading a number. Place stacks to communicate which opponents sit to the left and right of the local player.
- **Last-card warning**: when a player has one card left, shake that player's card and show a pulsing exclamation mark above it; do not use a screen-wide overlay.
- **Gone Down tag**: show the tag next to the newly placed sets briefly, then hide it after a few seconds rather than keeping it through the player's turn.
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
- On PC: left-drag = drag, holding the left button = long-press, the wheel zooms, and right/middle-drag pans.
- **PC keyboard shortcuts** (M4, shown in button tooltips and a `?` / F1 cheat sheet, rebindable later):
  - Ctrl/Cmd+Z undo, Ctrl/Cmd+Shift+Z or Ctrl+Y redo.
  - Space end turn, D draw/pass, R revert (with confirmation), F fix board.
  - S sort by suit, K sort by rank, H toggle hints, Home / 0 fit board, +/− zoom, arrow keys pan.
  - F1 or Shift+? opens the cheat sheet. Esc cancels a drag, clears the selection, closes a dialog or opens the pause menu; Enter confirms a dialog.
  - Shortcuts are ignored while a text field has focus and when it isn't the local player's turn.
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
- **Bot styles (M4)**: Balanced uses the selected difficulty; Hold Back sometimes draws despite a legal play; Blocker uses Hard planning and public card-tracking clues to avoid exposing a known extension to an opponent. A down-player draw weakens the inference that they still hold cards that could have improved their board.
- Bots play with a visible "thinking" delay and animated moves, executed as real `TurnSession` commands so the replay is visible.
- The **"Highlight playable" hint** reuses the solver at Easy depth; M4 changes its visual treatment to outline-and-jiggle and adds optional board-card move hints.
- Bots also take over disconnected online seats (§4.4).

### 3.6 Deck art & cosmetics (data-driven, ready for unlocks)
- `DeckSkin : ScriptableObject` contains `id`, `displayName`, `Sprite[52]` faces indexed by `(suit, rank)`, a `cardBack` default, a `blank` (or highlight) sprite, a `filterMode`/pixel-art flag, `pixelsPerUnit`, and an `UnlockRequirement` reference.
- **Per-skin mapping**: the editor tool **Mejnun ▸ Deck Skin Importer** takes a sliced texture and shows a 4×13 grid. You assign each slot by click, or apply a layout preset ("row-major", "interleaved zig-zag"). The result is saved into the SO.
  - `cards.png` (Classic) is already sliced into 54 sprites `cards_0..53` (64×97, point filter). Row 1 zig-zags clubs/hearts (2♣ 2♥ 3♥ 3♣ 4♣ 4♥ …) and ends with the card back. Row 2 zig-zags spades/diamonds the same way and ends with a blank card. The importer preset reproduces this, and the mapping gets checked visually in the importer grid.
  - `ClassicIvory/cards_ivory.png` is the **second base face** (added in v2). It's a palette swap of Classic: ivory paper, navy ink, crimson red. It uses the identical slicing (`cards_ivory_0..53`), so the same layout preset applies, and it's a placeholder until the real second-deck art replaces it in place.
- **Two faces per game** (the "two real decks" rule):
  - Every game uses **exactly two different face skins**, one per physical deck, just like shuffling two real decks together. `DeckFaces[0]` is used for cards with `Deck == 0` and `DeckFaces[1]` for `Deck == 1`. A game never shows one face for both decks.
  - Classic and Classic Ivory are the two **free base faces**, owned by everyone.
  - Before a game, the player picks both deck slots. Each slot accepts an unlocked face or **Random**; the two resolved slots must use different faces.
  - In solo and pass-and-play, the choices are used directly (`Deck 0` = slot 1; `Deck 1` = slot 2). A Random slot resolves to an owned face and must not duplicate the other slot. In solo, bots may add catalogue faces to the candidates for Random slots only *(see Q-E)*.
  - Online, each human submits both picks. Random entries resolve from that player's owned faces; the host combines the resulting picks into a pool and draws two different faces from it with the game's seeded RNG at deal time.
  - The selected faces are stored in `GameState` and snapshots, so all clients, reconnects and replays agree. A face the local player **doesn't own** gets a small "Not unlocked yet" badge on the results screen, linking to how to unlock it.
  - A nice side effect: the two cards of a **double now look different**, which makes the board easier to read.
  - Every client must be able to render every face. Face skins are small (around 15 KB each), so all faces **ship in the build**. Addressables remote download stays an option if the catalogue grows. If a face fails to load, the client falls back to the base face for that deck index.
  - There is no separate equipped-deck state; the two picks are made for each game.
- `TableFelt : ScriptableObject` contains an opaque `id`, `displayName`, sprite/material and an `UnlockRequirement`. Green and Burgundy are free. `TableFeltCatalog` lists the available felts.
- Each human chooses an unlocked felt or **Random** as a preference. A Random preference resolves to an owned felt; the game draws one felt at random from the human preferences with the seeded RNG at deal time. Pass-and-play contributes one preference for the shared device. Store the result as `TableFeltId` in `GameState` and snapshots so all clients and replays agree.
- `UnlockRequirement` contains a kind (level, wins, achievement or purchase), target, localized description, and a progress query that can report current and required progress when countable.
- Other cosmetic SOs: `CardBack`, `Avatar`, `MejnunShout` (a Wwise Switch value plus a VFX prefab).
- `CosmeticCatalog` lists every item. `PlayerInventory` stores owned and equipped items (local save + UGS Cloud Save).
- Unlock sources: **play progression** (XP per game and win, levels), **achievements** (Steam / Game Center / Google Play via the platform layer, plus local ones), and **IAP** (Unity IAP, behind `IEntitlements`). Mobile ships with Remove ads (§5.3), and cosmetic packs aren't committed yet.
- Packaging: the two base faces ship in the build. Other cosmetics can become **Addressables** groups, so they can be downloaded or patched later without an app update.
- **Online**: each player's owned face ids, two deck picks, felt preference and equipped *card back*, *avatar* and *shout* are sent at session join. The host builds the deck-pick pool (above).
  - The draw pile uses the viewing player's equipped back. Opponents' hand stacks show that opponent's back.
  - A back is never tied to a deck index, so a back never reveals which deck the next drawn card comes from.

### 3.7 Audio (Wwise)
- `AudioService` is the only code that calls `AkSoundEngine` or `AK.Wwise.Event`. Gameplay raises `GameEvents`, and an `AudioDirector` maps them to Wwise events.
- First-pass event list: `Play_UI_Click`, `Play_Card_Pickup`, `Play_Card_Drop`, `Play_Card_Invalid`, `Play_Card_Shuffle`, `Play_Card_Deal`, `Play_Card_Draw`, `Play_Meld_Valid`, `Play_GoDown`, `Play_Turn_Start`, `Play_Timer_Tick`, `Play_Timer_Warning`, `Play_Mejnun_Shout` (Switch `Shout_Type` per cosmetic), `Play_Win_Stinger`, `Play_Lose_Stinger`, `Music_Menu`, `Music_Game`.
- RTPC `Timer_Tension` (0–1) and RTPC `Board_Complexity` (optional, for music intensity). States `Game_Phase` (Menu / Dealing / Playing / Results).
- The main menu, Gallery and Settings set `Game_Phase = Menu` and play `Music_Menu`.
- Banks: `Init` and `Main` to start with. SoundBanks are **committed to git** (decided), so the game builds without Wwise Authoring installed.
- Platform plugins: the Mac/Windows integration is committed. **iOS and Android** integration files now appear in the working tree, so it looks like they were installed through the Wwise Launcher. They still need to be reviewed and committed, and the iOS/Android SoundBank platforms added in the Wwise project, before M8.

### 3.8 Front end (menus & gallery)
- `MainMenuScreen` is the boot destination, driven by `FrontEndFlow` and a `MatchFlowScreen.MainMenu` entry point. The main menu offers:
  - **Play** → choose **Solo vs AI** or **Pass & play**, then open the existing setup screen.
  - **Resume**, shown only when `SavedGame.HasSave` is true.
  - **Online**, which opens create / join-by-code / quick match (enabled in M7).
  - **Gallery**, **Settings**, and **Quit** (desktop only).
- Screen map and navigation:

  ```mermaid
  graph LR
    Boot --> MainMenu
    MainMenu --> Setup
    MainMenu --> Gallery
    MainMenu --> Settings
    MainMenu --> Resume
    Setup --> TablePicker
    TablePicker --> Gallery
    TablePicker --> DealerQuestion
    DealerQuestion --> Game
    Game --> Pause
    Pause --> Settings
    Game --> Results
    Results --> Gallery
    Results --> MainMenu
  ```

- The existing setup flow adds a **Table** step after seat and timer selection and before **Start**. It has two deck slots (each an unlocked face or **Random**, with different resolved faces) and one board-background slot (an unlocked felt or **Random**). Locked items appear greyed out with a lock and link to their Gallery entry. The last deck and felt picks are remembered.
- `SettingsPanel` is one shared component used from the main menu and in-game pause menu, moved out of `HudPresenter` and backed by the same `GameSettings` keys. Changes save immediately. Its categories are **Gameplay & confirmations**, **Hints**, **Accessibility** (reduced motion and haptics), **Audio** (volume sliders), **Language**, **Online** (quick-match bot fill), and **Account / Restore purchases** (mobile).
- Front-end screens support portrait and landscape layouts, adapt to aspect changes, respect device safe areas, and follow §7's RTL mirroring rules. Esc/back returns to the previous screen.
- `TablePickerScreen` handles the setup Table step; `GalleryScreen` provides the cosmetics Gallery. Both are in `Mejnun.UI` and are driven by `FrontEndFlow`.
- The Gallery has **Decks** and **Board backgrounds** tabs. Unlocked decks show a sample hand-fan preview and unlocked felts show a swatch. Locked items have dimmed previews, a lock, localized requirement text from `UnlockRequirement` (level, wins, achievement or purchase), and a progress bar when progress is countable.
- Each tab shows owned/total counts. Items unlocked since the previous Gallery visit carry a **New** badge, tracked in the local seen set (§6). A results-screen **Not unlocked yet** badge opens the matching Gallery item.

---

## 4. Online multiplayer

### 4.1 Stack (Unity Gaming Services)
- `com.unity.services.multiplayer` (Multiplayer Services SDK: Sessions = Lobby + Relay + Matchmaker), plus `com.unity.netcode.gameobjects`, plus `com.unity.services.authentication`, plus `com.unity.services.cloudsave`, plus (optionally) Cloud Code.
- **Host-authoritative**: the host runs `Mejnun.Core`. Clients send command RPCs, and the host validates them, applies them and broadcasts the resulting events and state deltas.
- **Hidden information**: each client only ever receives **its own hand** through targeted RPCs. The host process necessarily holds every hand, which is the accepted trade-off of the "UGS with player host" choice. Because Core is pure C#, moving authority to Cloud Code or a dedicated server later doesn't require touching game logic.
- Network payloads are compact: card ids as bytes, commands as small structs, and full `GameState` snapshots for join, reconnect and migration.

### 4.1.1 Live turn mirroring (everyone sees every move)
- In multiplayer, every player at the table sees the active player's moves **as they make them**, not just the final board when they end their turn.
- Each in-turn action is sent to the host as soon as it is made: card moves, splits, merges, go-down, and also **Undo, Redo, Revert and Fix board**. The host validates it, applies it to the live `TurnSession` and immediately broadcasts the resulting board delta and events to every client. All clients animate it with the same move animations used locally.
- Nothing is hidden or batched until End turn. Watchers see cards go out, move around, get undone and redone, the board get reverted to the start of the turn, and Fix board send cards back. Draw/Pass shows the revert first, then the draw.
- Hidden information still applies. A hand card only becomes visible once it is placed on the board. When it goes back to the hand (through Undo, Revert, Fix board or a drag back), it returns face-down to that player's card-back stack, and the hand counts update live.
- Watchers' views are read-only. Only the active seat can act, and board arrangement stays a local view preference (Q-I).
- The unit that gets mirrored is each applied move (or gesture group). In-flight drag positions are not streamed *(see Q-L)*.

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
- **Presence and grace**: each seat is Empty, Connected, Reconnecting, Disconnected, AI-controlled, or a permanent Bot. An app-away notice or transport drop starts a 20-second Reconnecting grace period. The match, turn timer, and bot moves freeze, with a compact countdown banner. After grace, the seat becomes Disconnected and everyone sees the waiting screen with the room code and each seat's status.
- **Seat claims**: the same authentication ID reclaims its seat during grace, after expiry, or from AI control. A different account may claim a Disconnected or AI-controlled seat, then an Empty seat; seats in grace remain reserved. A joining client only receives its own hand snapshot.
- **Host choices**: the host can Keep waiting or Let AI play for each Disconnected seat. The match resumes when no blocking absence remains. If a player claims a seat the AI is currently playing, the AI finishes its turn first and the player sees a Please wait message until the next turn begins. Save & pause remains available; a paused-games discovery menu is not part of this flow.
- **Host migration**: clients freeze and show that a new host is being found. UGS elects a host; the promoted host restores the migration record and marks other human seats Reconnecting. If no record is available or recovery takes more than 45 seconds, the game shows a recovery failure with a Leave action. A returning former host reconnects as a client if another player now owns the host role.
- **Suspend and rejoin**: mobile operating systems may suspend the app and drop Relay, so a live socket cannot be guaranteed. Going away never leaves the session; on resume the client reconnects by saved session ID first, then falls back to the room code. After a Disconnected seat expires, the host removes its lobby member to free a slot for code-based rejoins.
- **Protocol and operations**: the custom wire channel is `mejnun.session.v2`; clients send status/reason enums rather than localized text. The bounded `SessionRecordCodec` writes v2 and reads v1 records. See `Docs/M7-Online-Spike.md` for recommended UGS lobby timings and the manual live-test checklist.
- Shared Cloud Code snapshot storage, a Paused games menu, native mobile background tasks, and the multi-device soak test remain outside this reconnect implementation.

### 4.5 Online rules & timer
- Online games are **always timed**, so nobody can stall a live game. The host picks **30 / 60 / 90 / 120 s**, and the default is **90 s**. "Off" is only available offline.
- Lobby rule toggles (host only, shown to everyone before Ready): timer preset, hints (Off by default), bot difficulty for filled seats, and the dealer-question pack.
- The join payload includes each human's owned face ids, two deck picks (face id or Random) and felt preference (felt id or Random), plus the existing equipped card back, avatar and shout. The lobby summary shows each player's picks. At deal time, the host resolves Random picks, draws two different deck faces from the combined picks and one felt from the players' preferences with the seeded RNG, then stores `DeckFaces` and `TableFeltId` in the shared game state and snapshot.

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
| **Google Cloud** | Create a Cloud project → enable the Google Sheets API → create an OAuth desktop client for the Unity Localization Google Sheets extension → share the workbook with the account used by the extension | Free for the Sheets integration; Cloud Translation API v3 fallback usage may be billed |
| **Unity** | A UGS project linked to this Unity project: Authentication, Multiplayer Services, Cloud Save/Code, LevelPlay (ad network accounts) and IAP | Free tiers to start |

---

## 6. Persistence
- Local: settings, cosmetics, inventory, XP and stats, last `DeckPicks` and felt preference, the Gallery's seen-item set for **New** badges, paused local games (`SnapshotSerializer` binary snapshots), plus a replay of the last game (seed + command list) for debugging.
- Cloud: inventory and progression in UGS Cloud Save (linked accounts). Online paused games live in Cloud Code storage.
- A save schema version and migrations are in place from day one.

## 7. Localization
- `com.unity.localization` from the start. All UI strings and the dealer questions go into String Tables, and Smart Strings handle plurals ("3 cards left"). Arabic has six plural forms, and Smart Strings' plural rules cover that.
- **Launch languages (21)**: English, Arabic, Persian, Armenian, Spanish, German, Greek, Italian, French, Japanese, Simplified Chinese, Brazilian Portuguese, Korean, Traditional Chinese, Vietnamese, Dutch, European Portuguese, Russian, Hindi, Swahili and Thai.
  - Locale codes are `en`, `ar`, `fa`, `hy`, `es`, `de`, `el`, `it`, `fr`, `ja`, `zh-Hans`, `pt-BR`, `ko`, `zh-Hant`, `vi`, `nl`, `pt-PT`, `ru`, `hi`, `sw` and `th`.
  - The game follows the device language, with a picker in Settings.
- **Fonts** (dynamic font assets with a fallback chain, all OFL-licensed):
  - Noto Sans for Latin text (accents for es/de/it/fr/pt and Vietnamese).
  - **Vazirmatn** or Noto Sans Arabic for `ar` and `fa`. Both cover the Persian letters پ چ ژ گ and the Persian ی/ک forms.
  - Noto Sans Armenian.
  - Noto Sans JP, KR, SC and TC; Noto Sans Devanagari and Noto Sans Thai.
  - CJK fonts are several MB each, so the CJK/Arabic font files go in per-locale Addressable groups. Player nicknames can contain any script and fall back through the whole chain.
- **Right-to-left** (`ar`, `fa`):
  - Enable the Advanced Text Generator (`-unity-text-generator: advanced` on the UI root).
  - A `.rtl` class on the root mirrors the HUD and front-end menus and panels (row-reverse, mirrored icons such as back arrows).
  - The **cards and board stay left-to-right**, so straights read A→K the same for everyone at an online table *(see Q-D)*.
  - Numbers in UI text (timer, card counts) use locale digits (Eastern Arabic in `ar`, Persian in `fa`). Card art is unaffected.
- **Front-end strings**: localize the main-menu actions and states, Settings categories and options, navigation labels, Gallery tabs and counts, lock/progress labels, and `UnlockRequirement` descriptions. Use Smart Strings for changing progress text such as "Win {count} more games".
- **Dealer questions** are authored in English and auto-translated like UI strings. Each language ships with at least 20, and native speakers review the translations before release.

### 7.1 Auto-translation workflow
- **Tools and sheet layout:** Unity Localization's Google Sheets extension (included with `com.unity.localization`; no third-party plugin) pushes and pulls the String Tables. Google Translate in Sheets fills target cells with `GOOGLETRANSLATE`; Google Cloud Translation API v3 is the fallback if formula limits or quality are insufficient. Use one tab per String Table collection (`UI`, `DealerQuestions`) and a `StoreListings` tab in the same workbook. Each tab has `Key`, `en`, one translation column per locale, and a status for each target locale. Store listings continue to be localized from this workbook and pushed by fastlane.
- **Translation formula:** for each target locale column, use `=IF($B2="","",GOOGLETRANSLATE($B2,"en","<Google-language-code>"))`. The source text is in column B; blank source cells remain blank.

  | Unity locale | Google language code | Use |
  |---|---|---|
  | `en` | `en` | Source text; no formula |
  | `ar` | `ar` | Arabic |
  | `fa` | `fa` | Persian |
  | `hy` | `hy` | Armenian |
  | `es` | `es` | Spanish |
  | `de` | `de` | German |
  | `el` | `el` | Greek |
  | `it` | `it` | Italian |
  | `fr` | `fr` | French |
  | `ja` | `ja` | Japanese |
  | `zh-Hans` | `zh-CN` | Simplified Chinese |
  | `pt-BR` | `pt` | Brazilian Portuguese |
  | `ko` | `ko` | Korean |
  | `zh-Hant` | `zh-TW` | Traditional Chinese |
  | `vi` | `vi` | Vietnamese |
  | `nl` | `nl` | Dutch |
  | `pt-PT` | `pt-PT` | European Portuguese |
  | `ru` | `ru` | Russian |
  | `hi` | `hi` | Hindi |
  | `sw` | `sw` | Swahili |
  | `th` | `th` | Thai |

- **Push and pull:** configure the Unity-to-Sheets push to include only `Key` and `en`, never locale or status columns, so formulas and reviewer edits cannot be overwritten. Pull only the locale columns back into their matching String Tables; do not import `Key`, `en`, or status values. Pull before every build so all translations ship in the build and work offline, without runtime network access or an API key in the game.
- **Review:** target cells start with the formula and a locale-specific status of `auto`. A reviewer replaces the formula with plain text and sets that locale's status to `reviewed`; later source pushes must preserve both. Review dealer questions as well as UI strings before release.
- **Adding a language:** add its Locale code and Google language code to `LocalizationProjectSetup.TargetLocales`, then run **Mejnun > Localization > Set Up Project**. Setup creates the Locale and tables and merges the missing pipeline mapping; the next sync adds the sheet translation column and formula.
- **Guardrails:** preserve Smart String placeholders such as `{0}` and `{count:plural:...}` and check them after pull; do not allow translation to break plural blocks. Write or verify Arabic six-form plural variants by hand. Keep right-to-left translations as plain text and check their rendering in the RTL UI.

## 8. Accessibility & settings
- The shared `SettingsPanel` is reachable from both the main menu and the in-game pause menu. It uses the same `GameSettings` keys and saves changes immediately.
- Settings categories: **Gameplay & confirmations**, **Hints**, **Accessibility** (reduced motion and haptics), **Audio**, **Language**, **Online** (quick-match bot fill), and **Account / Restore purchases** (mobile). Local `PlayerPrefs` settings include per-action confirmation opt-outs, optional board-card hints, hint jiggle, reduced motion and haptics on/off.
- Audio volume sliders drive the `Volume_Master`, `Volume_Music`, `Volume_SFX` and `Volume_Voice` Wwise RTPCs; these audio controls are in scope, not later work. M9 adds an opt-out-capable four-colour deck option and left-handed HUD placement; adjustable hand and opponent-card sizes are available in the existing settings.

---

## 9. Milestones

| # | Milestone | Key deliverables | Exit criteria |
|---|---|---|---|
| **M0** | Project setup ✅ | git + LFS, Unity/Wwise `.gitignore`/`.gitattributes`, `Assets/_Project` layout, this plan | Clean first commit |
| **M1** | Rules engine ✅ | `Mejnun.Core` (cards, deck, RNG, `MeldValidator`, `GameState`, `TurnSession` with undo/revert/go-down/timeout, end conditions, snapshot serialization) | ≥ 95% line coverage in EditMode tests. Every rule in §1 has a named test. *Met: 123 EditMode tests pass, 95.3% line coverage* |
| **M2** | Playable hot-seat prototype ✅ | Table scene, card prefab, `DeckSkin` Classic + Classic Ivory + importer, the two-faces-per-game assignment, hand and board views, the full §3.4 gesture set, auto-layout, zoom/pan, End/Draw/Undo/Revert, handoff screen, dealer question flow | 2–6 people can finish a pass-and-play game on Mac and an Android/iOS device. *Met: hot-seat playtest passed (Brennan, 2026-11-17); 124 EditMode and 19 PlayMode tests pass. Closed* |
| **M3** | AI ✅ | Solver, Easy/Medium/Hard bots, hint highlight, solo setup screen | Hard beats Easy in more than 80% of 500 simulated games. Turn compute under 1 s on a mid-range phone. *Met in the editor: Hard beat Easy 481/500 (96.2%), the slowest Hard turn was 61 ms, and 158 EditMode and 22 PlayMode tests pass. The on-device phone timing is still to be measured* |
| **M4** | UX & UI ✅ | UI Toolkit menus/HUD, responsive portrait + landscape layouts, persistent settings, per-turn timers/Halas!, Redo and confirmations, PC shortcuts/cheat sheet, results/counts/MEJNUN moment, visual meld rearrangement, lifted/group hand drag, opponent card-back stacks, last-card warning, outline-and-jiggle/board-move hints, bot hold-back/blocking styles, camera-independent hand, wide-screen board packing, Fix board, timeout repair, short Gone Down tag | All playtest feedback implemented or tracked later; targeted Unity suites pass; human usability pass on a small phone and 4K PC. *Met: playtest passed (Brennan, 2026-11-17); EditMode 169/169 and PlayMode 51/51 pass. Closed* |
| **M5** | Audio ✅ | Wwise events/banks, AudioService/Director, music states, timer tension | Every `GameEvent` with a sound mapped. *Met: playtest passed (Brennan, 2026-11-17). Closed* |
| **M6** | Cosmetics & progression ✅ | Catalog, inventory, XP/levels, achievements (local), unlock UI, main menu and shared Settings, deck/felt Table picker, Gallery, `TableFeltCatalog` with free Green and Burgundy felts, Pixel Pastel via Addressables | From a cold boot, a player can browse the Gallery, change settings, pick two decks and a felt, and play a game that uses them; a locked item shows its requirement and progress. *Met: full PlayMode 103/103; M6-focused EditMode 22/22; Core EditMode 165/165* |
| **M7** | Online ✅ | Spike, anonymous auth, UGS sessions (codes and quick match), NGO host-authoritative sync with live turn mirroring (§4.1.1), reconnect, AI takeover, save & pause, migration data handler | Six-device soak test; killing any client or the host recovers per §4.4; every client sees each in-turn move, Undo, Redo, Revert and Fix board as it happens; shared Cloud Code save/resume. *Met: closed by Brennan (2026-11-19). Closed. Friends/platform invites remain a follow-up* |
| **M8** | Platforms & monetization (in progress) | Port the platform layer and Steam uploader, Build Profiles, Steam achievements and rich presence, iOS/Android Wwise plugins, LevelPlay ads + consent, Unity IAP Remove ads + restore, mobile Build & Upload window + fastlane lanes, Game Center / Play Games | Steam beta-branch upload from the editor. One-click TestFlight and Play internal builds. *In progress: platform layer, Steam backend/uploader tooling and achievement/stat bridge done (Platform EditMode 4/4); store accounts, Build Profiles, ads/IAP, fastlane and mobile platform services pending* |
| **M9** | Localization & polish (in progress; store listings deferred) | Google Sheets sync and String Tables for 21 locales; licensed font chain, RTL mirroring and locale digits; accessibility options; idle/battery frame-rate governor; Unity Cloud Diagnostics reporting; RC checklist. Localized store listings remain deferred with M8 store setup. | Every String Table entry is non-empty in all locales; Smart String placeholders and Arabic plural forms are checked after pull; release-candidate checklist and native-speaker review for `ar` and `fa` RTL screens |

Suggested order: M1 → M2 → M3 → M4/M5 in parallel → M6 → M7 → M8 → M9. M7 can start after M2, since it only depends on Core and the views.
Start the §5.5 store accounts during M3–M4, and enable the Advanced Text Generator plus one RTL test locale (`ar`) in M4, so right-to-left layout issues show up early rather than in M9.
The Google Sheets hookup can also start alongside the `ar` RTL test locale, so strings are synced from then on.

**M4 implementation (2026-11-17):** All gameplay/UI and playtest-feedback code is implemented, including the follow-up Redo, confirmation prompts and PC shortcuts. Added tests cover Redo, timer timeout/Halas event, bot hold-back and inference, card tracking, board arrangement, portrait hand rows and group hand ordering. Unity 6000.4.0f1 verification passes: EditMode 169/169 and PlayMode 51/51. The Advanced Text Generator project setting is not enabled, so its root USS declaration is deferred until the setting is enabled and an Arabic RTL locale can be checked. **Closed (2026-11-17): playtest passed; M4 is done.**

**M5 (2026-11-17):** Unity audio IDs/service/director and the GameEvents mapper are implemented, including haptic and timer hooks; mapper PlayMode tests are added. The Wwise structure has been authored through WAAPI by `mejnun_WwiseProject/Tools/author_m5.py`, which is idempotent and safe to re-run. It created `Mejnun` Work Units holding 27 events, with empty Random/Switch/Music Playlist containers in UI/SFX/Music/Voice folders. Those folders route to the matching buses under Main Audio Bus. It also added `Game_Phase`, `Shout_Type`, `Timer_Tension`, `Board_Complexity`, and `Volume_Master/Music/SFX/Voice`; the volume parameters drive bus volume. The `Main` SoundBank includes the whole event Work Unit, and `Init` + `Main` are generated for Mac and Windows. **Closed (2026-11-17): playtest passed; M5 is done.**

**M6 implementation (2026-11-18):** Added snapshot v2 migration, owned cosmetics and versioned local profile persistence, XP/levels and six achievements, three felt render modes, a local Addressables group for Pixel Pastel, shared settings/audio volumes, MainMenu/Table scene handoff, the two-deck/felt picker, Gallery locks/progress/New badges/deep links, and results rewards. Validation: Core EditMode 165/165, M6 Cosmetics EditMode 22/22, PlayMode 103/103. The complete EditMode run is 257/259 because two existing `UnsortedDeckScannerTests` expect 27 alternate backs while the source fixture currently provides 23; those importer checks are unrelated to the M6 changes.

**M7 implementation foundation (2026-11-18):** Added per-seat redacted snapshots; pure-C# host/client sessions, bounded network and migration codecs, atomic host command groups, reconnect, timeout, bot takeover, pause/save, and UGS NGO/Cloud Save adapters; enabled create/join-by-code/quick-match UI; and added `TableController` relay plus remote `MatchFlow` hooks. Host migration uses UGS `WithHostMigration`; the live lobby/election path is not yet verified. Validation: Net EditMode 9/9 and the focused Online main-menu PlayMode test 1/1 pass. Full EditMode is 292/301 (seven M6 cosmetic expectation failures and two known alternate-back fixture-count failures); full PlayMode is 109/112 (felt presentation, stalemate XP expectation, and HUD root identity failures). See `Docs/M7-Online-Spike.md`. **Closed (2026-11-19): M7 is done.**

**M8 implementation (2026-11-19, foundation in progress):** Ported the store-neutral platform runtime, Null backend, define-gated Steam backend, and Steam upload editor tooling; replaced Easy Save storage with per-file atomic JSON saves; and connected the six local progression achievements plus game-played/game-won stats to the platform layer. Development settings use Steam App ID 480, while upload app/depot IDs remain empty and the Steam window rejects uploads until real IDs and manually-created Build Profiles are configured. Validation: Platform EditMode 4/4 and Cosmetics `ProfilePersistenceTests` 6/6 pass with no C# compiler errors; the Steam-specific assembly was not compiled with `MEJNUN_STEAM` enabled. Accounts, mobile ads/IAP and fastlane, mobile platform achievements, and device/store acceptance remain deferred.

**M8 next steps (as store accounts become available):** create the Steam app and replace dev App ID 480 in `Assets/Resources/PlatformSettings.asset`; fill the app/depot IDs in `SteamUploadConfig`; create the Steam Build Profiles (with `MEJNUN_STEAM`) in the Build Profiles window and compile the Steam assembly; define the six achievements and two stats in Steamworks; then do a beta-branch upload from the editor. On mobile: review and commit the iOS/Android Wwise plugins, add LevelPlay ads + consent, Unity IAP Remove ads + restore (wired to `EntitlementId.RemoveAds`), the mobile Build & Upload window and fastlane lanes, and Game Center / Play Games backends.

**M9 localization foundation (2026-11-19, in progress):** Installed `com.unity.localization` 1.5.14 with 21 locales and the `UI` and `DealerQuestions` tables. All player-facing text in UXML/C# is table-backed; the validator, raw-string scanner, CSV importer, Sheets sync and pre-build hook are in place. The startup language follows the Settings choice, then platform language APIs. The 2026-11-20 sync pushed six accessibility rows, but Google Sheets left 114 new-locale formulas in `Loading...` through the configured 600-second wait; the report has no source, placeholder or plural errors. The Hindi achievement placeholder and Persian requirement wording are maintained in the handwritten override file. Full EditMode tests pass 388/388; PlayMode is 128/136 with the same eight baseline failures. Native-speaker review remains pending.

**M9 fonts, RTL and polish (2026-11-20):** Added OFL font sources and dynamic multi-atlas assets for Latin, Arabic/Persian, Armenian, Devanagari, Thai and CJK; the CJK fonts are in locale-specific Addressables groups and load with the active locale first. Both UI panel settings share the font chain. UI roots apply advanced text generation, `.rtl` layout and direction on locale changes; cards, board and hand remain LTR per the Q-D default. Arabic/Persian digit formatting, six-form Arabic plural validation and handwritten translation overrides are implemented. Added four-colour deck and left-handed options, a mobile idle/Battery Saver frame-rate governor, and enabled Unity Cloud Diagnostics reporting. The RC checklist records remaining on-device frame/battery and crash-dashboard verification, native-speaker review and the pending 114 Sheets translations; localized store listings are excluded and remain deferred.

---

## 10. Testing strategy
- **Core (EditMode)**: a table-driven validator test for every set rule (aces, doubles, sizes, mixed suits); turn flows (go-down only from hand, go-down then board use in the same turn, End-turn gating, undo past go-down, revert, all four timeout outcomes, empty-pile pass, a sole-fewest win versus a tied **Stalemate**, the fewest-cards winner deals next, a new dealer prompt after a Stalemate); and deterministic seeded deal tests.
- **Cosmetics (EditMode)**: offline picks map slot 1 → `Deck 0` and slot 2 → `Deck 1`; the assigned faces remain distinct; online combined human picks choose two distinct faces deterministically; seeded felt draws select from the submitted preferences; snapshot round-trip preserves `DeckFaces` and `TableFeltId`; both deck indexes render with their assigned skin and missing assets use the per-index fallback.
- **AI**: solver correctness on curated puzzles, a fuzz test (random states, so every solver output must be a valid board), difficulty benchmarks, and BoardFixer coverage for retaining valid placements, returning invalid hand cards, re-homing original cards, and timeout resolution.
- **Net**: snapshot round-trip serialization for `TableFeltId`, per-seat hidden-card redaction, atomic command replay, and live mirroring after each in-turn move, Undo, Redo, Revert and Fix board; timeout, reconnect, AI takeover, save/pause/resume, and host-migration record restore are covered in EditMode. Live NGO/Relay migration and multi-device behavior still require integration/soak tests.
- **PlayMode**: main-menu navigation from cold boot, settings persistence across the main menu and pause menu, Gallery locked-item requirement/progress rendering, a scripted pass-and-play smoke game, orientation and wide-screen layout snapshots, camera pan/zoom stability for the hand, Fix board/Gone Down HUD behavior, timer timeout, Redo, opponent card-back counts, and group hand reorder.
- **M4 AI/UI**: Hold Back draws in cases where a balanced bot plays; inferred board extenders are ignored after a down-player draw; board arrangement does not mutate `GameState`; group reordering preserves relative card order.
- **Board arrangement**: repositioning a set changes only the layout order (no command, unchanged `GameState`, unchanged undo stack). New sets are appended, removed sets are dropped, and the order survives Undo, Revert and an aspect change.
- **Pick-up and drag**: a held card follows the pointer and returns to its slot on cancel. Holding a selected card lifts every selected card. Dropping a held group into the hand reorders it as a block (relative order kept, no command, unchanged undo stack).
- CI (optional, later): Unity Test Runner in batch mode on push.

---

## 11. Decisions log & open questions

### 11.1 Resolved in v2.3 (answers to the v1 questions and later decisions)
| # | Topic | Decision | Where |
|---|---|---|---|
| 1 | First player | Left of the dealer | §1.4 |
| 2 | Tie on fewest cards when the pile is empty | **Stalemate** (no winner) | §1.10 |
| 3 | Hints online | Per-lobby toggle (default Off; quick match Off) | §1.11, §4.5 |
| 4 | Quick-match bots | Player setting: Off / 30 s / 60 s | §4.2 |
| 5 | Steam | No Steam app yet. Use dev App ID 480 until it's created; demo later | §5.2, §5.5 |
| 6 | Mobile automation | Local fastlane driven from an editor window (recommended), set up together in M8 | §5.2 |
| 7 | Minimum OS | iOS 15, Android 8, Windows 10, macOS 12 | §5.4 |
| 8 | Languages | en, ar, fa, hy, es, de, el, it, fr, ja, zh-Hans, zh-Hant, pt-BR, pt-PT, ko, vi, nl, ru, hi, sw, th | §7 |
| 9 | Mobile editions | One free app with a "Remove ads" IAP; Steam is paid | §5.3 |
| 10 | Card faces | Two distinct faces per game, originally chosen at random from the players' owned faces; superseded by #23 | §3.6 |
| 11 | Bots in pass-and-play | Yes, mixed freely | §2 |
| 12 | Online timer | Always on online; host picks a preset, default 90 s | §4.5 |
| 13 | Split | Gestures only, no tools; long-press picks up the tail of a straight | §3.4 |
| 14 | Dragging feel | Deck-builder style: held cards lift and follow the finger; holding a selected card picks up the whole selection; the hand opens a gap so held cards can be reorganized | §3.4, M4 |
| 15 | Playtest: hand and board camera | Hand is a stable screen layer; camera motion must not move or resize it. Board row packing uses the available viewport width and fitting reserves the hand strip | §3.3, M4 |
| 16 | Playtest: repair and timeout | Add Fix board separately from Revert; repair invalid timeout boards while preserving valid hand placements. No placed card means draw/end; a valid remaining placement ends without drawing | §1.8–1.9, M4 |
| 17 | Playtest: HUD feedback | Opponent card stacks indicate counts and left/right seats; last-card warnings animate on the card; Gone Down tags expire; timeout notification says "Halas!" | §1.9, §3.3, M4 |
| 18 | Playtest: hints and bots | Hints outline and jiggle, optionally suggesting board moves; bots gain hold-back and inferred-card blocking strategies | §1.11, §3.5, M4 |
| 19 | Playtest: safety and PC controls | Add Redo; confirm destructive actions (Revert, Draw/Pass after placing, Fix board returning cards, leave/restart); full PC keyboard shortcuts with a cheat sheet | §1.8, §3.4, M4 |
| 20 | Translation workflow | Translate in the editor before builds through Unity Localization's Google Sheets extension and `GOOGLETRANSLATE`; use Google Cloud Translation API v3 only as a fallback | §7.1, M9 |
| 21 | Dealer-question localization | Auto-translate dealer questions like other strings, then have native speakers review them before release | §7, §7.1 |
| 22 | Main menu | The main menu is the boot screen and entry point for play, resume, Gallery and Settings | §3.8 |
| 23 | Deck picker | Players pick both decks; offline picks apply directly, while online the host draws two different faces from all human picks with the seeded RNG. This replaces #10 | §3.6, §4.5 |
| 24 | Felt selection | Each human picks a felt preference, then one preference is selected at random with the seeded RNG and stored in game state | §3.2, §3.6, §4.5 |
| 25 | Shared settings | The main menu and pause menu use one `SettingsPanel` backed by the same `GameSettings` keys | §3.8, §8 |
| 26 | Live turn mirroring online | Everyone sees the active player's moves as they happen, before End turn, including Undo, Redo, Revert and Fix board; hand cards stay hidden until placed | §4.1.1, M7 |
| Q-B | After a stalemate | A tie is a Stalemate and prompts for a new dealer; a sole player with the fewest cards wins normally and deals next | §1.10 |

### 11.2 Still open (new, smaller; the defaults in the text apply until answered)
- **Q-A Classic mapping**: confirm the zig-zag sprite order in the importer once it exists (unchanged from v1).
- **Q-D RTL table**: should the board and hand also mirror for Arabic/Persian players, or stay left-to-right as proposed?
- **Q-E Faces in solo Random slots**: when a slot is set to Random, should bots contribute random catalogue faces to its candidate pool (default), or should it resolve only from the player's owned faces? This question applies only to Random slots; explicit deck picks are used as selected.
- **Q-F Remove ads across devices**: honour a Remove ads purchase from the other mobile store via a linked account (Cloud Save), or keep it per store (default)?
- **Q-G Double-tap quick-place**: keep it as proposed in §3.4, or leave it out?
- **Q-H Prices**: the Steam price and the Remove ads price tier.
- **Q-I Board arrangement online**: should a set's position be shared with everyone at the table (it would then be sent as a small view-only message), or be a local preference per client (default)?
- **Q-J Review depth**: should all 20 languages receive native-speaker review before release, or only `ar`/`fa`, with the rest shipping as machine translation?
- **Q-K Pass-and-play picks**: should each local human make their own deck and felt picks, or should the shared device make one pick set (default)?
- **Q-L Live drag ghosts online**: should watchers also see a held card following the active player's pointer before it is dropped (a throttled, view-only stream), or only each applied move (default)?
