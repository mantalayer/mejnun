# Achievements and Platform Setup

This document is the catalog and setup checklist for the achievements and stats implemented in Mejnun. The IDs in the code are the stable source of truth; store-facing API IDs below are the names to enter in each partner portal.

## Current integration status

- The game logic awards all 41 achievements and records the 35 integer stats listed below.
- `AchievementId` and `StatId` are store-neutral enums. `PlatformSettings.asset` currently maps them to Steam API names, and `PlatformAchievementBridge` sends them through the existing platform interfaces.
- Steam is the only store backend currently configured in this repository. For GOG, Epic, Xbox, PlayStation, Apple, or Google, create the definitions in that platform’s portal **and** implement an `IPlatformServices` adapter that maps these local IDs to the provider’s IDs and sends progress/stats. Adding portal entries alone will not make those platforms award anything.
- `AchievementId` values `1–41` and `StatId` values `1–35` are serialized identifiers. Do not renumber or reuse them; append future IDs.
- Win-based totals and achievements use `MatchSummary.CanAwardWin`: solo, online, and single-human matches can earn wins; multi-human pass-and-play cannot. Stalemates do not count as wins or break/extend streaks.

## Achievement catalog

`Hidden` means the achievement should be marked as a spoiler/secret where the platform supports that setting. Progress bindings are suggested/configured for platforms that support progress bars; `—` means unlock on the match event or profile condition without a progress bar.

| Local ID | `AchievementId` | Steam API name | Display name | Description / unlock condition | Hidden | Progress stat |
|---|---|---|---|---|---|---|
| `first_game` | `FirstGame` (1) | `FIRST_GAME` | First Game | Finish your first game. | No | — |
| `first_win` | `FirstWin` (2) | `FIRST_WIN` | First Win | Win your first game. | No | — |
| `first_mejnun` | `FirstMejnun` (3) | `FIRST_MEJNUN` | First MEJNUN | Win by emptying your hand. | No | — |
| `ten_wins` | `TenWins` (4) | `TEN_WINS` | Ten Wins | Win 10 games. | No | `GAMES_WON`, 0–10 |
| `beat_hard_bot` | `BeatHardBot` (5) | `BEAT_HARD_BOT` | Beat a Hard Bot | Beat a hard bot. | No | — |
| `full_table` | `FullTable` (6) | `FULL_TABLE` | Full Table | Play a game with six seats. | No | — |
| `regular` | `Regular` (7) | `REGULAR` | Regular | Play 50 games. | No | `GAMES_PLAYED`, 0–50 |
| `veteran` | `Veteran` (8) | `VETERAN` | Veteran | Play 250 games. | No | `GAMES_PLAYED`, 0–250 |
| `seasoned` | `Seasoned` (9) | `SEASONED` | Seasoned | Win 50 games. | No | `GAMES_WON`, 0–50 |
| `master_of_mejnun` | `MasterOfMejnun` (10) | `MASTER_OF_MEJNUN` | Master of MEJNUN | Win 200 games. | No | `GAMES_WON`, 0–200 |
| `level_ten` | `LevelTen` (11) | `LEVEL_TEN` | Level 10 | Reach level 10. | No | — |
| `level_twenty_five` | `LevelTwentyFive` (12) | `LEVEL_TWENTY_FIVE` | Level 25 | Reach level 25. | No | — |
| `level_fifty` | `LevelFifty` (13) | `LEVEL_FIFTY` | Level 50 | Reach level 50. | No | — |
| `collector` | `Collector` (14) | `COLLECTOR` | Collector | Unlock every free or earnable deck face and felt. | No | — |
| `mejnun_ten` | `MejnunTen` (15) | `MEJNUN_TEN` | MEJNUN ×10 | Win 10 games by emptying your hand. | No | `MEJNUN_WINS`, 0–10 |
| `clean_sweep` | `CleanSweep` (16) | `CLEAN_SWEEP` | Clean Sweep | Go down and empty your hand in the same turn, with hints and Fix Board off. | No | — |
| `lightning` | `Lightning` (17) | `LIGHTNING` | Lightning | Win within your first five turns. | No | — |
| `last_gasp` | `LastGasp` (18) | `LAST_GASP` | Last Gasp | Win by emptying your hand with the draw pile empty. | No | — |
| `comeback` | `Comeback` (19) | `COMEBACK` | Comeback | Win after holding 20 or more cards. | No | — |
| `down_to_the_wire` | `DownToTheWire` (20) | `DOWN_TO_THE_WIRE` | Down to the Wire | Win the end-of-pile count by exactly one card. | No | — |
| `royal_road` | `RoyalRoad` (21) | `ROYAL_ROAD` | Royal Road | Make a full 13-card straight from ace to king. | No | — |
| `ace_high` | `AceHigh` (22) | `ACE_HIGH` | Ace High | Complete a queen-king-ace straight. | No | — |
| `big_entrance` | `BigEntrance` (23) | `BIG_ENTRANCE` | Big Entrance | Go down with at least three sets at once. | No | — |
| `architect` | `Architect` (24) | `ARCHITECT` | Architect | Place at least eight hand cards in one turn. | No | — |
| `surgeon` | `Surgeon` (25) | `SURGEON` | Surgeon | Split a straight and end the turn with a valid board. | No | — |
| `borrowed_time` | `BorrowedTime` (26) | `BORROWED_TIME` | Borrowed Time | Take a card from a four-card same-kind set and use it. | No | — |
| `seeing_double` | `SeeingDouble` (27) | `SEEING_DOUBLE` | Seeing Double | End your turn with both copies of a card on separate sets. | No | — |
| `patient` | `Patient` (28) | `PATIENT` | Patient | Win without hints, Undo, Revert, or Fix Board. | No | — |
| `no_training_wheels` | `NoTrainingWheels` (29) | `NO_TRAINING_WHEELS` | No Training Wheels | Win with hints turned off. | No | — |
| `halas` | `Halas` (30) | `HALAS` | Halas! | Have your turn time out. | No | — |
| `cool_under_pressure` | `CoolUnderPressure` (31) | `COOL_UNDER_PRESSURE` | Cool Under Pressure | End a valid turn with less than three seconds left. | No | — |
| `speed_demon` | `SpeedDemon` (32) | `SPEED_DEMON` | Speed Demon | Win a game played on the 30-second timer. | No | — |
| `giant_slayer` | `GiantSlayer` (33) | `GIANT_SLAYER` | Giant Slayer | Win against five hard bots in a full table. | No | — |
| `hat_trick` | `HatTrick` (34) | `HAT_TRICK` | Hat Trick | Win three bot games in a row. | No | — |
| `unstoppable` | `Unstoppable` (35) | `UNSTOPPABLE` | Unstoppable | Win five games in a row. | No | `BEST_WIN_STREAK`, 0–5 |
| `going_online` | `GoingOnline` (36) | `GOING_ONLINE` | Going Online | Finish an online game. | No | — |
| `crowd_pleaser` | `CrowdPleaser` (37) | `CROWD_PLEASER` | Crowd Pleaser | Win an online game with at least four human players. | No | — |
| `party_host` | `PartyHost` (38) | `PARTY_HOST` | Party Host | Host 10 online games. | No | `ONLINE_GAMES_HOSTED`, 0–10 |
| `pass_it_on` | `PassItOn` (39) | `PASS_IT_ON` | Pass It On | Finish a pass-and-play game with at least four humans. | No | — |
| `even_steven` | `EvenSteven` (40) | `EVEN_STEVEN` | Even Steven | Finish a game in a stalemate. | No | — |
| `hot_seat` | `HotSeat` (41) | `HOT_SEAT` | Hot Seat | Be chosen to go first by the question 10 times. | Yes | `HOT_SEAT_QUESTIONS`, 0–10 |

The game also reports progress for these existing count achievements: `TEN_WINS`, `REGULAR`, `VETERAN`, `SEASONED`, `MASTER_OF_MEJNUN`, `MEJNUN_TEN`, `UNSTOPPABLE`, `PARTY_HOST`, and `HOT_SEAT`. For Steam, set each achievement's Progress Stat and range to the table values. The code reports current progress as well, so the in-game stat and notification stay synchronized.

## Stat catalog

All stats are integer stats in the current bridge. `Increment` means add a nonnegative per-match amount; `Max` and `Min` preserve the best record. Fastest-time/turn records use zero as “not set.” `AvgTurnsToGoDown` is maintained as a rounded weighted integer average.

| `StatId` (value) | Steam API name | Update rule |
|---|---|---|
| `GamesPlayed` (1) | `GAMES_PLAYED` | Increment for each finished match. |
| `GamesWon` (2) | `GAMES_WON` | Increment for an eligible local win, never a stalemate. |
| `MejnunWins` (3) | `MEJNUN_WINS` | Increment for an eligible empty-hand win. |
| `CountWins` (4) | `COUNT_WINS` | Increment for an eligible fewest-cards win. |
| `Stalemates` (5) | `STALEMATES` | Increment for a stalemate; does not affect win totals or streaks. |
| `GamesPlayedSolo` (6) | `GAMES_PLAYED_SOLO` | Increment for a solo match. |
| `GamesPlayedOnline` (7) | `GAMES_PLAYED_ONLINE` | Increment for an online match. |
| `GamesPlayedPassAndPlay` (8) | `GAMES_PLAYED_PASS_AND_PLAY` | Increment for a pass-and-play match. |
| `WinsVsEasy` (9) | `WINS_VS_EASY` | Increment for eligible solo wins against easy bots. |
| `WinsVsMedium` (10) | `WINS_VS_MEDIUM` | Increment for eligible solo wins whose maximum bot difficulty is medium. |
| `WinsVsHard` (11) | `WINS_VS_HARD` | Increment for eligible solo wins whose maximum bot difficulty is hard. |
| `OnlineWins` (12) | `ONLINE_WINS` | Increment for eligible online wins. |
| `CurrentWinStreak` (13) | `CURRENT_WIN_STREAK` | Set to the current eligible win streak; stalemates leave it unchanged. |
| `BestWinStreak` (14) | `BEST_WIN_STREAK` | Maximum eligible win streak. |
| `CardsPlayedFromHand` (15) | `CARDS_PLAYED_FROM_HAND` | Increment by local hand cards placed on the board. |
| `SetsCreated` (16) | `SETS_CREATED` | Increment by newly created melds. |
| `StraightsCreated` (17) | `STRAIGHTS_CREATED` | Increment by newly created straights. |
| `SameKindsCreated` (18) | `SAME_KINDS_CREATED` | Increment by newly created same-kind sets. |
| `LongestStraight` (19) | `LONGEST_STRAIGHT` | Maximum straight length made/extended locally. |
| `MostCardsInOneTurn` (20) | `MOST_CARDS_IN_ONE_TURN` | Maximum number of local hand cards played in one turn. |
| `GoDowns` (21) | `GO_DOWNS` | Increment each time the local player goes down. |
| `AvgTurnsToGoDown` (22) | `AVG_TURNS_TO_GODOWN` | Rounded weighted average of turns to go down. |
| `TurnsPlayed` (23) | `TURNS_PLAYED` | Increment for each observed local turn. |
| `Draws` (24) | `DRAWS` | Increment when the local player draws from the pile. |
| `Passes` (25) | `PASSES` | Increment for pass attempts, including empty-pile passes. |
| `Timeouts` (26) | `TIMEOUTS` | Increment when the local turn timer expires. |
| `UndosUsed` (27) | `UNDOS_USED` | Increment for user-invoked Undo. |
| `RevertsUsed` (28) | `REVERTS_USED` | Increment for user-invoked Revert. |
| `FixBoardsUsed` (29) | `FIX_BOARDS_USED` | Increment for user-invoked Fix Board, not timeout auto-repair. |
| `FastestWinTurns` (30) | `FASTEST_WIN_TURNS` | Minimum local turns to an eligible win. |
| `FastestWinSeconds` (31) | `FASTEST_WIN_SECONDS` | Minimum match duration in seconds for an eligible win. |
| `LargestHandAtWin` (32) | `LARGEST_HAND_AT_WIN` | Maximum local hand size during an eligible winning match. |
| `TotalPlaySeconds` (33) | `TOTAL_PLAY_SECONDS` | Increment by match duration in seconds. |
| `OnlineGamesHosted` (34) | `ONLINE_GAMES_HOSTED` | Increment for an online match hosted by this player. |
| `HotSeatQuestions` (35) | `HOT_SEAT_QUESTIONS` | Increment when the local player is chosen by the first-player question. |

## Entering the data by platform

### Steamworks

1. In Steamworks, open the app and go to **Stats & Achievements**. Add the achievements from the catalog using the exact Steam API names (case-sensitive); enter the display names and descriptions and localize them for the languages you ship.
2. Set `Set By` to **Client** for the current client-side integration. Mark only `HOT_SEAT` as a spoiler/hidden achievement. Choose **Progress Stat** and the `0 → target` range for every row with a progress binding.
3. Add every stat from the stat table as an `INT` stat with default `0`. Allow both increments and direct sets: max records, min records, and streak values use `SetInt`, not only increments. Use an adequate nonnegative range for counters; do not constrain fastest records to values below plausible match durations.
4. In the Unity project, `Assets/Resources/PlatformSettings.asset` already contains the matching Steam names. Change `m_steamFullAppId` from the sample value `480` to the real app ID before release, and add/maintain `m_steamDemoAppId` separately if you ship a demo.
5. Test with a Steamworks test account and a development build launched through Steam. Confirm the app ID, API names, stat values, and progress ranges before publishing. Steamworks supports bulk achievement definitions/localization CSVs if you prefer that workflow.

Steam recommends square achieved/unachieved icons at 256×256 PNG (64×64 is accepted); use colorful achieved art and a grayscale locked version, or let Steamworks generate the locked variant. Use the API name plus `_ACHIEVED` / `_UNACHIEVED` when bulk-uploading icons.

### GOG GALAXY

1. In the GOG Developer Portal, open the game’s **Galaxy Features → Achievements** area and create each achievement with a stable GOG achievement ID. The portal can import definitions from a Steam setup; verify all imported IDs and localization before shipping.
2. Enter titles/descriptions, icon art, and hidden-before-unlock visibility for `hot_seat`. Define count/stat targets where supported; otherwise report current progress and unlocks through the SDK.
3. Integrate the GOG GALAXY SDK `IStats` features (or evaluate the documented SDK Wrapper if retaining Steam API calls). Request user stats/achievements before reading or writing, then store updates. Offline awards can sync later when the player was signed in to Galaxy before losing connectivity; the current GOG SDK exposes locked/unlocked achievements rather than Steam-style progress bars, so check the local count stats and unlock at each target.
4. Run with the GOG development configuration and verify both online and offline award/sync behavior. The Steam-only `PlatformSettings.asset` is not a GOG adapter.

### Epic Games Store / EOS

1. In the Epic Developer Portal, configure the EOS achievement definitions using the local IDs (for example `first_game`, `royal_road`) as stable IDs, then create the required stats in the deployment. Set stat-driven conditions for the countable achievements and keep event-driven ones manually unlockable.
2. Implement EOS authentication, stat ingestion, achievement definition/progress queries, and manual unlock calls behind `IPlatformServices`. In EOS, progress is based on achievement definitions and ingested stats; do not assume Steam stat names or Steamworks configuration are imported automatically.
3. For Epic Games Store achievements, link each EGS achievement to its corresponding EOS achievement, configure XP values, and publish through the Stage sandbox and review workflow before moving to Live. EOS achievements alone and EGS achievements are related but distinct setup steps.
4. Test in an EOS sandbox/deployment containing the stat definitions before publishing. The IDs in the local catalog use underscores, which are valid in EOS achievement IDs.

### Xbox / Microsoft Store / PlayFab

These are not one interchangeable achievement backend. Pick the target and use its supported service:

- **Xbox console or Xbox services on PC:** configure title-managed achievements in Partner Center and implement the Xbox GDK/Xbox Services APIs. Define each achievement’s ID, localized title/description, gamerscore, hidden status, and unlock logic there. Use Xbox’s test accounts/sandbox and follow the current certification requirements. Event-based achievements are an alternative when their server-side rules fit the game; they require configured events/conditions rather than the client unlock calls used here.
- **Microsoft Store PC without Xbox services:** confirm which identity/achievement service the title is enrolled in. A Store listing by itself does not map these local IDs to Xbox achievements.
- **PlayFab-backed game:** create PlayFab statistics/title configuration and implement the PlayFab client/server reporting path. PlayFab stats are not automatically surfaced as Xbox Gamerscore achievements; connect them only where the chosen Xbox/PlayFab product flow supports it.

In all cases, preserve the win eligibility and stalemate rules in the adapter, and use the approved development sandbox rather than live player accounts.

### PlayStation

PlayStation trophy authoring and SDK details are restricted to approved PlayStation Partners. Register the project, accept the required agreements, and use the partner portal’s current **Trophy** documentation and submission workflow. Define the trophy set, stable trophy IDs, bronze/silver/gold/platinum grades, hidden trophies, localized metadata, and the platform-required icon assets there; then map each local `AchievementId` to the approved trophy ID in a PlayStation-specific adapter. Confirm limits, grade totals, and asset dimensions in the current platform documentation—do not reuse Steam icon assumptions. The current repository does not include a PlayStation adapter.

### Apple Game Center

1. In App Store Connect, open the app’s **Game Center → Achievements** and add each achievement with a reference name and achievement ID. Use the local IDs as the stable identifier if available in the chosen GameKit integration.
2. Add points, hidden-before-earned state (`hot_seat`), and localized title plus pre- and post-earning descriptions. Upload an achievement image for each entry, save, and submit the Game Center metadata for review.
3. Implement GameKit reporting of earned achievements and percentage progress. For counts, calculate percentage from the matching local stat and target; Game Center does not provide a direct equivalent to this repository’s general `IStats` interface, so store/display other stats separately.
4. Test with a Game Center sandbox account and confirm metadata approval and progress sync before release.

### Google Play Games Services

1. Link the Android app to a Play Games Services project in Play Console and create an achievement for each local ID. Set stable achievement IDs, localized names/descriptions, icons, and hidden state for `hot_seat`.
2. Configure count achievements as incremental achievements with the appropriate step totals (`10`, `50`, `200`, `250`); use standard achievements for event conditions. Assign point values according to the current Play Games quality and total-points guidance.
3. Implement the Play Games Services achievement APIs behind `IPlatformServices`, reporting incremental steps only from eligible match/profile progress and unlocking event achievements from the existing local conditions. Persist stats separately if they are not represented by Play Games leaderboards or achievements.
4. Publish to an internal/closed testing track, add tester accounts, and verify unlocks/progress before production rollout.

### Nintendo

Nintendo’s public Developer Portal describes registration and access to platform SDK documentation after approval; it does not document a public cross-game achievement API. Treat these achievements as **in-game achievements** on Nintendo unless the approved platform documentation for the target hardware explicitly provides another mechanism. Preserve the same local IDs and display them in the game’s own profile/gallery UI; do not promise system-level trophies or write to unsupported platform services. Access Nintendo’s restricted documentation through the Developer Portal after title/platform approval.

## Art, localization, and UX checklist

- Produce a consistent square master icon for each achievement and generate platform-specific derivatives using each portal’s current upload requirements. Steam’s recommendation is 256×256 PNG; EOS recommends 192×192 and supports up to 1024×1024. Follow the relevant console/mobile portal’s own current image rules for other stores.
- Make hidden achievements genuinely spoiler-safe: use a neutral locked icon and avoid revealing the condition in the locked-state description where the platform supports separate locked/unlocked descriptions.
- Localize display names and descriptions, including punctuation and MEJNUN capitalization. Keep the stable API IDs unchanged across languages and releases.
- Review stat ranges and progress binding targets against the code table before uploading; Steam stat and achievement identifiers are case-sensitive.
- Validate the multi-human pass-and-play guardrail, stalemate behavior, online host-only counting, and mid-game snapshot resume on every adapter. Never award a disconnected seat’s achievements to a bot takeover.

## Testing and reset guidance

- Run the Unity EditMode suites `Mejnun.Cosmetics.Tests` and `Mejnun.Platform.Tests` after changes to IDs, conditions, or mappings. The tracker tests exercise the snapshot-based per-turn accounting; mapping tests ensure every local achievement has a platform enum.
- Use separate platform test accounts/sandboxes. Steam stats/achievements, GOG achievements, EOS/EGS, Xbox, PlayStation, Apple, and Google progress can be account-persistent and cannot be reset by deleting the local save.
- The local profile is stored at `Application.persistentDataPath/mejnun_profile.json` (a corrupt-file backup may be written as `.bak`). Removing that file resets local progression only; it does **not** reset any store account. Do not test resets on a production account.
- There is no in-game platform-stat reset command. Use the provider’s documented development/test-account reset or create a fresh test account. Any Steamworks full-stat reset is destructive to that test account’s app progress; never expose it as a production control.
- After a stat-schema or API-ID change, verify with a clean test account, then verify migration behavior on an account with existing local profile data. Existing serialized fields default to zero when absent, so the local profile schema version does not need to change just to add these stat fields.

## Official references

- [Steamworks: Step by Step—Achievements](https://partner.steamgames.com/doc/features/achievements/ach_guide)
- [GOG Developer Docs: Statistics and Achievements](https://docs.gog.com/sdk-stats-and-achievements)
- [Epic Online Services: Achievements Setup Guide](https://dev.epicgames.com/docs/epic-online-services/player-and-game-data/achievements-interface/achievements-guide/set-up-achievements)
- [Epic Games Store: Achievements Setup Guide](https://dev.epicgames.com/docs/epic-games-store/services/epic-achievements/achievements-setup)
- [Microsoft GDK: Implement player achievements](https://learn.microsoft.com/en-us/gaming/gdk/docs/gdk-dev/pc-dev/tutorials/pc-e2e-guide/e2e-services/e2e-achievements?view=gdk-2610)
- [Microsoft GDK: Event-based vs. title-managed achievements](https://learn.microsoft.com/en-us/gaming/gdk/docs/services/player-data/achievements/live-achievements-eb-vs-tm?view=gdk-2604)
- [PlayStation Partners registration and access](https://sonyinteractive.com/en/news/blog/showing-your-game-to-playstation)
- [Apple: Manage Game Center achievements](https://developer.apple.com/help/app-store-connect/configure-game-center/manage-achievements)
- [Google Play Games Services: Achievements](https://developer.android.com/games/pgs/achievements)
- [Nintendo Developer Portal: The Process](https://developer.nintendo.com/the-process)